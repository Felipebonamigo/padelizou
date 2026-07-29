using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Middleware;
using Padelizou.Models; // Garanta que o nome da pasta Models está certo
using Padelizou.Services;
using padelizou.Models;
using System.Globalization;

// No Windows o processo herda a cultura pt-BR do SO, mas no Linux (produção) não há esse
// fallback e ele cai na invariant culture — daí o "¤" no lugar de "R$" em .ToString("C").
// Fixamos a cultura padrão da thread/processo aqui para que valha também fora de requests
// HTTP (ex: o LembreteJogoBackgroundService).
var culturaPadrao = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPadrao;
CultureInfo.DefaultThreadCurrentUICulture = culturaPadrao;

// O app grava DateTime.Now (Kind=Local/Unspecified) em várias colunas. O Npgsql 6+ é estrito
// com timestamptz/UTC; o modo legado mapeia DateTime -> timestamp (sem tz) e aceita esses
// valores sem lançar exceção. Precisa ser definido antes de qualquer uso do Npgsql.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Banco PostgreSQL (self-hosted no VPS). Migrado do Azure SQL para não depender de cota grátis.
builder.Services.AddDbContext<DbPadelContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Chaves de proteção de dados em disco. Sem isso o ASP.NET Core gera um chaveiro NOVO a cada
// start, e todo cookie de autenticação emitido antes vira lixo: cada deploy — e cada restart
// do vigia de uptime — desloga TODO MUNDO. No meio de um torneio isso derruba o organizador
// da Mesa de Controle com os jogadores esperando na quadra.
//
// O caminho vem de configuração porque prod e dev têm pastas próprias; sem ele (máquina de
// desenvolvimento) segue o comportamento padrão do framework.
var pastaDeChaves = builder.Configuration["DataProtection:CaminhoDasChaves"];
if (!string.IsNullOrWhiteSpace(pastaDeChaves))
{
    Directory.CreateDirectory(pastaDeChaves);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(pastaDeChaves))
        // Prod e dev compartilham o binário: nomes iguais fariam um decifrar o cookie do outro.
        .SetApplicationName($"Padelizou-{builder.Environment.EnvironmentName}");
}

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<GoogleCalendarSettings>(builder.Configuration.GetSection("GoogleCalendar"));
builder.Services.Configure<AcessoAntecipadoSettings>(builder.Configuration.GetSection("AcessoAntecipado"));
builder.Services.Configure<BetaSettings>(builder.Configuration.GetSection("Beta"));
builder.Services.Configure<DadosDemoSettings>(builder.Configuration.GetSection("DadosDemo"));
builder.Services.Configure<SuporteSettings>(builder.Configuration.GetSection("Suporte"));
builder.Services.Configure<TaxasExibicao>(builder.Configuration.GetSection("Taxas"));
builder.Services.Configure<RegistroResultadosSettings>(builder.Configuration.GetSection("RegistroResultados"));
builder.Services.Configure<ZApiSettings>(builder.Configuration.GetSection("ZApi"));
builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
builder.Services.Configure<AsaasSettings>(builder.Configuration.GetSection("Asaas"));
builder.Services.AddSingleton<IPasswordHasher<Jogador>, PasswordHasher<Jogador>>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IEstatisticasService, EstatisticasService>();
builder.Services.AddScoped<IPalpiteService, PalpiteService>();
builder.Services.AddScoped<ISessaoGrupoService, SessaoGrupoService>();
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppApiService>();
// O Asaas recusa com HTTP 400 ("user_agent_not_informed") qualquer requisição sem User-Agent,
// e o HttpClient do .NET não manda nenhum por padrão — sem isto, toda cobrança falharia.
builder.Services.AddHttpClient<IAsaasService, AsaasService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Padelizou");
});
builder.Services.AddScoped<IPagamentoInscricaoService, PagamentoInscricaoService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IHorarioMarcacaoService, HorarioMarcacaoService>();
builder.Services.AddScoped<OtimizacaoDeImagens>();
builder.Services.AddHostedService<LembreteJogoBackgroundService>();
builder.Services.AddHostedService<HorarioVagoBackgroundService>();
builder.Services.AddHostedService<PagamentoExpiradoBackgroundService>();
builder.Services.AddHostedService<VigiaDoBackupBackgroundService>();
builder.Services.AddHostedService<QuadraAtrasadaBackgroundService>();
builder.Services.AddHostedService<AlertaMeiBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Para onde mandar quem não está logado
        options.AccessDeniedPath = "/Auth/AcessoNegado";
    });
// Add services to the container.
//
// Carimbo antifalsificação (CSRF) em TODA ação que grava — POST, PUT, PATCH e DELETE. Sem ele,
// um site qualquer consegue fazer o navegador de quem está logado aqui enviar um formulário sem
// a pessoa saber: cancelar aula, mudar placar, trocar dado de perfil.
//
// Por que global e não um atributo por ação: eram 61 de 114 ações sem o atributo, e essa conta
// só piora — quem escreve a 115ª ação não tem como lembrar de algo que não está em lugar nenhum.
// Com o filtro global a proteção é o padrão e a exceção é que precisa ser escrita
// ([IgnoreAntiforgeryToken]), que é a ordem certa: esquecer passa a ser seguro.
//
// Formulário do Razor já manda o campo escondido sozinho; chamada por fetch() manda no cabeçalho
// (ver `cabecalhoAntifalsificacao` em site.js).
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Garante que o catálogo fixo de categorias existe no banco (idempotente, casa pelo Nome — é o
// que decide se aparece duplicado pro usuário; Codigo é só um identificador interno, sem
// constraint de unicidade, e diverge entre ambientes que foram semeados em momentos diferentes).
// Evita depender de rodar um script manual toda vez que um ambiente novo é provisionado.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
    try
    {
    // Cria/atualiza o schema no startup — num banco novo (Postgres) cria tudo do zero aqui,
    // então o deploy não precisa mais rodar 'ef database update' à mão.
    db.Database.Migrate();

    var catalogoCategorias = new (string Nome, string Codigo, string Tipo)[]
    {
        ("Categoria Open Masculino", "1CatM", "Masculina"),
        ("2ª Categoria Masculina", "2CM", "Masculina"),
        ("3ª Categoria Masculina", "3CatM", "Masculina"),
        ("4ª Categoria Masculina", "4CatM", "Masculina"),
        ("5ª Categoria Masculina", "5CatM", "Masculina"),
        ("6ª Categoria Masculina", "6CatM", "Masculina"),
        ("7ª Categoria Masculina", "7CatM", "Masculina"),
        ("Categoria Iniciantes Masculina", "ICatM", "Masculina"),
        ("Categoria Open Feminina", "1CatF", "Feminina"),
        ("2ª Categoria Feminina", "2CatF", "Feminina"),
        ("3ª Categoria Feminina", "3CatF", "Feminina"),
        ("4ª Categoria Feminina", "4CatF", "Feminina"),
        ("5ª Categoria Feminina", "5CatF", "Feminina"),
        ("6ª Categoria Feminina", "6CatF", "Feminina"),
        ("7ª Categoria Feminina", "7CatF", "Feminina"),
        ("Categoria Iniciantes Feminina", "ICatF", "Feminina"),
        ("Categoria Mista A", "MISTA-A", "Mista"),
        ("Categoria Mista B", "MISTA-B", "Mista"),
        ("Categoria Mista C", "MISTA-C", "Mista"),
        ("Categoria Mista D", "MISTA-D", "Mista"),
    };

    var nomesExistentes = db.CategoriasPadrao.Select(c => c.Nome).ToHashSet();
    foreach (var (nome, codigo, tipo) in catalogoCategorias)
    {
        if (!nomesExistentes.Contains(nome))
        {
            db.CategoriasPadrao.Add(new CategoriaPadrao { Nome = nome, Codigo = codigo, Tipo = tipo });
        }
    }
    db.SaveChanges();

    // Dado fictício é coisa de ambiente de teste: só semeia onde DadosDemo:Habilitado
    // estiver ligado (dev). Em produção fica desligado, senão a base voltaria a nascer
    // cheia de jogador que não existe toda vez que a gente limpasse.
    var demo = app.Services.GetRequiredService<IOptions<DadosDemoSettings>>().Value;
    if (demo.Habilitado)
    {
        // Roda só se o banco ainda não tem jogadores (nunca sobrescreve dados reais).
        Padelizou.Data.DadosDemo.Seed(db);

        // Os dois abaixo rodam mesmo com dados existentes — a idempotência é pelo
        // Codigo "PRIMAVERA26" e pelo nome do local de aula.
        if (!db.Torneios.Any(t => t.Codigo == "PRIMAVERA26"))
        {
            Padelizou.Data.DadosDemo.SeedApresentacao(db);
        }

        Padelizou.Data.DadosDemo.SeedProfessorEClube(db);
    }
    }
    catch (Exception ex)
    {
        // Banco indisponível no startup não derruba o app (evita crash-loop). Ele sobe e
        // volta a funcionar quando o banco responder.
        app.Logger.LogError(ex, "Falha ao migrar/semear o banco no startup — app subindo mesmo assim.");
    }
}

// Configure the HTTP request pipeline.

// Em produção o Kestrel fica atrás do Caddy (proxy reverso na mesma máquina, que termina o
// HTTPS). Sem isso, o app acha que toda requisição chegou em HTTP puro e o UseHttpsRedirection
// entra num loop de redirecionamento.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culturaPadrao),
    SupportedCultures = new[] { culturaPadrao },
    SupportedUICultures = new[] { culturaPadrao }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Endereço inexistente caía na tela do próprio navegador ("HTTP ERROR 404"), sem menu e sem
// caminho de volta — link velho de torneio compartilhado no WhatsApp é o caso mais comum.
// "ReExecute" reexecuta o pipeline mantendo a URL que a pessoa digitou na barra, em vez de
// redirecionar: quem chegou por um link errado consegue ver qual era o link errado.
app.UseStatusCodePagesWithReExecute("/Home/NaoEncontrado", "?codigo={0}");

app.UseHttpsRedirection();
// MapStaticAssets() (abaixo) só serve os arquivos que já existiam em wwwroot no momento do
// publish (manifest gerado em build) — uploads feitos em runtime (foto de perfil, etc.) não
// entram nesse manifest e voltavam 404. UseStaticFiles cobre esse caso (serve wwwroot direto do
// disco, sem manifest), então mantemos os dois.
app.UseStaticFiles();
app.UseMiddleware<AcessoAntecipadoMiddleware>();
app.UseMiddleware<AdminHostMiddleware>();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Health check pro monitor de uptime (cron do VPS + UptimeRobot): responde 200 "ok"
// quando o app está de pé E consegue falar com o banco; 503 caso contrário.
// Liberado no AcessoAntecipadoMiddleware (PrefixosLiberados).
// Aceita HEAD além de GET: o UptimeRobot (e vários monitores) checam com HEAD por
// padrão, e um endpoint só-GET responde 405 — o monitor lê isso como site fora do ar.
app.MapMethods("/healthz", new[] { "GET", "HEAD" }, async (DbPadelContext db) =>
{
    try
    {
        return await db.Database.CanConnectAsync()
            ? Results.Text("ok")
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
