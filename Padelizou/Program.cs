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
using System.Threading.RateLimiting;

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
builder.Services.Configure<PlanoProfessorSettings>(builder.Configuration.GetSection("PlanoProfessor"));
builder.Services.Configure<EvolutionSettings>(builder.Configuration.GetSection("Evolution"));
builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection("Site"));
builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
builder.Services.Configure<AsaasSettings>(builder.Configuration.GetSection("Asaas"));
// Ranking RS (mundodoatleta.com.br). Nasce DESLIGADO: sem chave configurada, nenhuma inscrição
// é validada contra o ranking — ver Services/RankingRsSettings.
builder.Services.Configure<RankingRsSettings>(builder.Configuration.GetSection("RankingRs"));
// Nasce DESLIGADO: o bar do clube está em construção e, enquanto isso, só admin do Padelizou
// enxerga (ver Services/BarSettings).
builder.Services.Configure<BarSettings>(builder.Configuration.GetSection("Bar"));
builder.Services.AddSingleton<IPasswordHasher<Jogador>, PasswordHasher<Jogador>>();
// Trava de força-bruta do LOGIN: janela por conta, contada dentro da própria ação (o
// identificador vem do formulário, que middleware não lê sem risco de I/O síncrono).
builder.Services.AddSingleton<TravaDeEntrada>();
// Trava de força-bruta das demais portas de entrada (portão, recuperação de senha e
// cadastro): janela por IP no rate limiter do ASP.NET. Só vale pra quem carrega
// [EnableRateLimiting] — o resto do site, /healthz incluído, não passa por aqui.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (contexto, cancelamento) =>
    {
        // Escreve o corpo aqui mesmo: sem corpo, o UseStatusCodePages reexecutaria a
        // página de erro — e quem está fora do portão seria redirecionado pro portão,
        // vendo um 302 sem explicação no lugar do aviso.
        contexto.HttpContext.Response.Headers.RetryAfter =
            ((int)TravaDeEntrada.Janela.TotalSeconds).ToString();
        contexto.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await contexto.HttpContext.Response.WriteAsync(
            "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
            "<title>Muitas tentativas - Padelizou</title></head>" +
            "<body style=\"font-family:sans-serif;max-width:30rem;margin:4rem auto;padding:0 1rem;text-align:center\">" +
            "<h1>Calma lá!</h1><p>Muitas tentativas em pouco tempo. " +
            "Espere <strong>5 minutos</strong> e tente de novo — não foi nada que você fez de errado.</p>" +
            // Sem link, a página é um beco sem saída: quem cai aqui no meio do cadastro fica
            // sem caminho de volta justamente no primeiro contato com o site.
            "<p><a href=\"/\">Voltar pro início</a></p></body></html>", cancelamento);

        var logger = contexto.HttpContext.RequestServices
            .GetService<ILoggerFactory>()?.CreateLogger("TravaDeEntrada");
        logger?.LogWarning("Limite de tentativas estourado em {Caminho} pelo IP {Ip}.",
            contexto.HttpContext.Request.Path, contexto.HttpContext.Connection.RemoteIpAddress);
    };
    // A janela é por IP **e por ação**: a chave da partição leva o caminho junto. Com uma
    // janela só por IP, entrar pelo portão, criar conta e pedir "esqueci minha senha"
    // dividiriam as mesmas 10 tentativas — quem chega pela primeira vez faz as três coisas
    // em sequência e seria barrado no meio do próprio cadastro.
    static string Chave(HttpContext http) =>
        // O IP aqui já é o do cliente de verdade: o UseForwardedHeaders roda antes de tudo
        // e corrige o RemoteIpAddress atrás do Caddy.
        $"{http.Connection.RemoteIpAddress?.ToString() ?? "sem-ip"}|{http.Request.Path}";

    options.AddPolicy(TravaDeEntrada.PoliticaPorIp, http =>
        RateLimitPartition.GetFixedWindowLimiter(Chave(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = TravaDeEntrada.TentativasPorJanela,
                Window = TravaDeEntrada.Janela,
                QueueLimit = 0,
            }));

    options.AddPolicy(TravaDeEntrada.PoliticaCadastro, http =>
        RateLimitPartition.GetFixedWindowLimiter(Chave(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = TravaDeEntrada.TentativasDeCadastro,
                Window = TravaDeEntrada.Janela,
                QueueLimit = 0,
            }));

    options.AddPolicy(TravaDeEntrada.PoliticaConsulta, http =>
        RateLimitPartition.GetFixedWindowLimiter(Chave(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = TravaDeEntrada.ConsultasPorJanela,
                Window = TravaDeEntrada.Janela,
                QueueLimit = 0,
            }));
});
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IEstatisticasService, EstatisticasService>();
builder.Services.AddScoped<IPalpiteService, PalpiteService>();
builder.Services.AddScoped<IPadelimetroService, PadelimetroService>();
// Ranking Americano (Trilha C do RANKING.md): serviço próprio, e não mais uma consulta do
// EstatisticasService — os dois rankings não se somam, e misturá-los no mesmo serviço seria
// convidar alguém a somar um dia.
builder.Services.AddScoped<IRankingAmericanoService, RankingAmericanoService>();
builder.Services.AddScoped<ISessaoGrupoService, SessaoGrupoService>();
// Timeout curto: o envio acontece DENTRO da ação do jogador. Um provedor pendurado não pode
// segurar a resposta da tela — melhor perder a mensagem do que travar quem clicou.
builder.Services.AddHttpClient<IWhatsAppService, EvolutionApiService>(http =>
    http.Timeout = TimeSpan.FromSeconds(10));
// O Asaas recusa com HTTP 400 ("user_agent_not_informed") qualquer requisição sem User-Agent,
// e o HttpClient do .NET não manda nenhum por padrão — sem isto, toda cobrança falharia.
builder.Services.AddHttpClient<IAsaasService, AsaasService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Padelizou");
});
// A consulta ao Ranking RS acontece dentro do POST da inscrição. O timeout de verdade é o do
// RankingRsSettings (o serviço o aplica no construtor); servidor deles pendurado vira
// "não consultado" e a inscrição segue.
builder.Services.AddHttpClient<IRankingRsService, RankingRsService>();
// Consulta de CEP (ViaCEP). Timeout curto pelo mesmo motivo do WhatsApp: ela roda DENTRO do
// POST do cadastro e do perfil, e o CEP é campo opcional — serviço deles pendurado não pode
// segurar quem está criando a conta. O cache é o que evita bater lá a cada gravação.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IConsultaDeCep, ConsultaDeCep>(http =>
    http.Timeout = TimeSpan.FromSeconds(5));
// A regra de inscrição que usa o ranking. Serviço (e não código no controller) porque DUAS
// telas inscrevem gente — ver o comentário do arquivo.
builder.Services.AddScoped<ValidacaoPeloRankingRs>();
builder.Services.AddScoped<IPagamentoInscricaoService, PagamentoInscricaoService>();
// Registrado nas duas formas de propósito: a interface pra quem GERA aviso (o app inteiro),
// e a classe concreta pro entregador de fundo, que precisa do EntregarAgoraAsync — o método
// que faz a rede de verdade e não pode ser chamado de dentro de uma requisição.
// O que acontece quando um jogo acaba — as DUAS telas que finalizam partida chamam este.
builder.Services.AddScoped<EncerramentoDaPartida>();
// O "Apitouuuu!" de inscrição nova pra quem segue o torneio. Implementação única, chamada
// das DUAS portas de inscrição (dupla e individual) — ver Services/AvisoDeInscricaoNoTorneio.
builder.Services.AddScoped<AvisoDeInscricaoNoTorneio>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<IPushNotificationService>(sp => sp.GetRequiredService<PushNotificationService>());
builder.Services.AddScoped<IHorarioMarcacaoService, HorarioMarcacaoService>();
builder.Services.AddScoped<OtimizacaoDeImagens>();
// Tira da tabela de push os registros que o servidor de push já não entrega — o número de
// aparelhos é o que a gente usa pra medir o alcance do canal, e ele inflava sozinho.
builder.Services.AddScoped<ISondaDePush, SondaDePushWebPush>();
builder.Services.AddScoped<VarreduraDeFantasmas>();
// Tira do WhatsApp quem nunca pediu pra estar lá — a causa raiz da restrição de 04/08.
builder.Services.AddScoped<DesfazerOptInHerdado>();
// Todo 500 vira registro em ErroDoSistema + aviso pros admins (com janela de silêncio).
// O IExceptionHandler roda dentro do UseExceptionHandler, antes da página amiga.
builder.Services.AddScoped<RegistroDeErros>();
builder.Services.AddExceptionHandler<CapturaDeErro>();
// Quem pode abrir o bar e as contas do clube. Uma regra só pros dois módulos.
builder.Services.AddScoped<ModuloDoBar>();
builder.Services.AddHostedService<LembreteJogoBackgroundService>();
builder.Services.AddHostedService<HorarioVagoBackgroundService>();
builder.Services.AddHostedService<PagamentoExpiradoBackgroundService>();
// Cobra a FATURA a vencer × cobra a INSCRIÇÃO devendo. São dois avisos diferentes de propósito:
// quem escolheu "pago depois" não tem fatura nenhuma pra vencer.
builder.Services.AddHostedService<LembreteInscricaoNaoPagaBackgroundService>();
// E, no fechamento, PERGUNTA ao organizador o que fazer com quem ficou devendo. Pergunta —
// nunca remove sozinho: ver Services/DecisaoSobreQuemNaoPagou.
builder.Services.AddHostedService<PerguntaSobreNaoPagosBackgroundService>();
// A taxa por aula do professor cai de 3% pra 10% sozinha em dois momentos — o fim do teste e o
// vencimento da assinatura (que não é recorrente) — e nenhum dos dois avisava ninguém. Um
// serviço só pros dois: ver Services/AvisosDoPlanoDoProfessor.
builder.Services.AddHostedService<AvisosDoPlanoDoProfessorBackgroundService>();
builder.Services.AddHostedService<VigiaDoBackupBackgroundService>();
builder.Services.AddHostedService<VigiaDoWhatsAppBackgroundService>();
// O gêmeo do de cima, pro outro canal. Nasceu de 09/08/2026: a cota de e-mail estourou e 130
// e-mails morreram calados (o EmailService engole a falha de propósito). ⚠️ Este avisa pela
// caixa de entrada e push, NUNCA por e-mail — ver VigiaDoEmailBackgroundService.
builder.Services.AddHostedService<VigiaDoEmailBackgroundService>();
// Quanto e-mail saiu no dia. Singleton pelo mesmo motivo do VolumeDoWhatsApp: a contagem é do
// PROCESSO. ⚠️ Diferente dele, este NÃO barra envio — ver Services/VolumeDoEmail.
builder.Services.AddSingleton<VolumeDoEmail>();
builder.Services.AddSingleton<FilaDeWhatsApp>();
// Quanto já saiu pelo canal, e o teto que ele respeita. Singleton porque a contagem é do
// PROCESSO — ver Services/VolumeDoWhatsApp pro que o espaçamento da fila não resolve.
builder.Services.AddSingleton<VolumeDoWhatsApp>();
builder.Services.AddHostedService<EntregadorDeWhatsAppBackgroundService>();
// Aviso sai por fora da requisição: finalizar jogo não pode esperar SMTP. Ver FilaDeAvisos.
builder.Services.AddSingleton<FilaDeAvisos>();
// O portão de acesso antecipado: padrão no systemd, chave de virada no banco. Singleton
// porque o middleware lê isto em TODA requisição — ver Services/PortaoDeAcesso.
builder.Services.AddSingleton<PortaoDeAcesso>();
builder.Services.AddHostedService<EntregadorDeAvisosBackgroundService>();
builder.Services.AddHostedService<QuadraAtrasadaBackgroundService>();
// Aula fixa "sem prazo definido" não existe como linha infinita: nasce com um horizonte de
// semanas e este job repõe o que o tempo consome. Ver Services/RenovacaoDaAulaFixa.
builder.Services.AddHostedService<RenovadorDeAulaFixaBackgroundService>();
builder.Services.AddHostedService<AlertaMeiBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Para onde mandar quem não está logado
        options.AccessDeniedPath = "/Auth/AcessoNegado";

        // Quanto o login dura. O padrão do framework são 14 dias, e ele já desliza — mas
        // nada disso valia, porque o cookie era de SESSÃO (ver Services/SessaoDoJogador).
        //
        // Deslizante: cada visita empurra a validade pra frente. Quem abre o app pra ver a
        // chave no sábado não é deslogado nunca; quem sumiu três meses entra de novo. Pra
        // um app que a pessoa abre no meio de um jogo, ser deslogado é pior que o risco de
        // a sessão durar demais — e o sair continua ali pra quem usa aparelho emprestado.
        options.ExpireTimeSpan = TimeSpan.FromDays(90);
        options.SlidingExpiration = true;
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

    // Todo campo de dinheiro é <input type="number">, e o navegador manda "79.90" mesmo
    // exibindo "79,90". Em pt-BR o "." é separador de MILHAR, então o binder padrão lia isso
    // como 7990 — sem erro, sem aviso. Insert(0) porque só o primeiro provider que aceitar o
    // tipo é usado. Ver Services/DinheiroModelBinder.
    options.ModelBinderProviders.Insert(0, new DinheiroModelBinderProvider());
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

    // O estado do portão de acesso antecipado, se o admin já tiver mexido nele pelo app.
    // Precisa vir DEPOIS do Migrate (a tabela pode ter acabado de nascer) e ANTES da primeira
    // requisição — o middleware do portão roda antes do roteamento e lê isto da memória.
    await scope.ServiceProvider.GetRequiredService<PortaoDeAcesso>().CarregarAsync(db);

    // As duas "Iniciantes" nascem DESLIGADAS (Ativa: false): saíram do catálogo que as telas
    // oferecem. Não são apagadas porque torneio, preferência de jogador e aviso guardam o Id
    // delas — e ligar de volta é um UPDATE de uma linha, sem deploy.
    var catalogoCategorias = new (string Nome, string Codigo, string Tipo, bool Ativa)[]
    {
        ("Categoria Open Masculino", "1CatM", "Masculina", true),
        ("2ª Categoria Masculina", "2CM", "Masculina", true),
        ("3ª Categoria Masculina", "3CatM", "Masculina", true),
        ("4ª Categoria Masculina", "4CatM", "Masculina", true),
        ("5ª Categoria Masculina", "5CatM", "Masculina", true),
        ("6ª Categoria Masculina", "6CatM", "Masculina", true),
        ("7ª Categoria Masculina", "7CatM", "Masculina", true),
        ("Categoria Iniciantes Masculina", "ICatM", "Masculina", false),
        ("Categoria Open Feminina", "1CatF", "Feminina", true),
        ("2ª Categoria Feminina", "2CatF", "Feminina", true),
        ("3ª Categoria Feminina", "3CatF", "Feminina", true),
        ("4ª Categoria Feminina", "4CatF", "Feminina", true),
        ("5ª Categoria Feminina", "5CatF", "Feminina", true),
        ("6ª Categoria Feminina", "6CatF", "Feminina", true),
        ("7ª Categoria Feminina", "7CatF", "Feminina", true),
        ("Categoria Iniciantes Feminina", "ICatF", "Feminina", false),
        ("Categoria Mista A", "MISTA-A", "Mista", true),
        ("Categoria Mista B", "MISTA-B", "Mista", true),
        ("Categoria Mista C", "MISTA-C", "Mista", true),
        ("Categoria Mista D", "MISTA-D", "Mista", true),
        // Uma só, sem letra: casal não tem nível. Ver FaixasDePadelimetro.EhCasal.
        //
        // ⚠️ O NOME é "Casais" (plural) desde 08/08/2026, a pedido do Felipe — e trocá-lo aqui
        // foi OBRIGATÓRIO, não cosmético: este seed cria por NOME (`nomesExistentes`), então
        // renomear só no banco faria o start seguinte não achar "Categoria Casal", criar tudo
        // de novo, e o catálogo terminar com as DUAS.
        //
        // `Codigo` e `Tipo` continuam "CASAL": é por Tipo que as telas agrupam a categoria, e
        // mexer neles quebraria o bloco do coração na criação sem melhorar nada.
        ("Categoria Casais", "CASAL", "Casal", true),
    };

    var nomesExistentes = db.CategoriasPadrao.Select(c => c.Nome).ToHashSet();
    foreach (var (nome, codigo, tipo, ativa) in catalogoCategorias)
    {
        // Só CRIA o que falta. Quem já existe não é reescrito de propósito: se alguém ligar
        // uma categoria pelo banco, o start seguinte não pode desligá-la de novo.
        if (!nomesExistentes.Contains(nome))
        {
            db.CategoriasPadrao.Add(new CategoriaPadrao { Nome = nome, Codigo = codigo, Tipo = tipo, Ativa = ativa });
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
forwardedHeadersOptions.KnownIPNetworks.Clear();
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

// ⚠️ Cabeçalhos de segurança (nosniff, X-Frame-Options, Referrer-Policy) NÃO moram aqui:
// quem os põe é o **Caddy**, desde 30/07/2026, nos blocos de padelizou.com.br (que cobre www
// e admin) e de dev.padelizou.com.br — ou seja, em todo domínio nosso e em toda resposta,
// inclusive as que o app nunca vê.
//
// Foram acrescentados aqui em 06/08 por engano — a varredura procurou só no C# e não olhou a
// configuração do proxy. O resultado apareceu em produção: cada cabeçalho chegando DUAS vezes,
// e a RFC 7034 diz que servidor não deve mandar mais de um X-Frame-Options (navegador pode
// ignorar quando os valores divergem). Duplicar não protegia mais; protegia menos.

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
// Antes do portão de propósito: com o portão religado, o robots.txt precisa continuar
// legível pros buscadores em vez de virar redirect pra tela de senha.
app.UseMiddleware<RobotsMiddleware>();
app.UseMiddleware<AcessoAntecipadoMiddleware>();
app.UseMiddleware<AdminHostMiddleware>();
app.UseRouting();

// Depois do UseRouting de propósito: a política é por endpoint ([EnableRateLimiting]),
// e antes do roteamento o middleware não sabe qual endpoint foi atingido.
app.UseRateLimiter();

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
