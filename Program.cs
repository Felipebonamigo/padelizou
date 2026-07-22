using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
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

var builder = WebApplication.CreateBuilder(args);

// Adicione esta linha logo abaixo do CreateBuilder:
builder.Services.AddDbContext<DbPadelContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<GoogleCalendarSettings>(builder.Configuration.GetSection("GoogleCalendar"));
builder.Services.Configure<AcessoAntecipadoSettings>(builder.Configuration.GetSection("AcessoAntecipado"));
builder.Services.Configure<ZApiSettings>(builder.Configuration.GetSection("ZApi"));
builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddSingleton<IPasswordHasher<Jogador>, PasswordHasher<Jogador>>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IEstatisticasService, EstatisticasService>();
builder.Services.AddScoped<IPalpiteService, PalpiteService>();
builder.Services.AddScoped<ISessaoGrupoService, SessaoGrupoService>();
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppApiService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddHostedService<LembreteJogoBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Para onde mandar quem não está logado
        options.AccessDeniedPath = "/Auth/AcessoNegado";
    });
// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Garante que o catálogo fixo de categorias existe no banco (idempotente, casa pelo Nome — é o
// que decide se aparece duplicado pro usuário; Codigo é só um identificador interno, sem
// constraint de unicidade, e diverge entre ambientes que foram semeados em momentos diferentes).
// Evita depender de rodar um script manual toda vez que um ambiente novo é provisionado.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
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

app.UseHttpsRedirection();
// MapStaticAssets() (abaixo) só serve os arquivos que já existiam em wwwroot no momento do
// publish (manifest gerado em build) — uploads feitos em runtime (foto de perfil, etc.) não
// entram nesse manifest e voltavam 404. UseStaticFiles cobre esse caso (serve wwwroot direto do
// disco, sem manifest), então mantemos os dois.
app.UseStaticFiles();
app.UseMiddleware<AcessoAntecipadoMiddleware>();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
