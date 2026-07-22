using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Padelizou.Middleware;
using Padelizou.Models; // Garanta que o nome da pasta Models está certo
using Padelizou.Services;

var builder = WebApplication.CreateBuilder(args);

// Adicione esta linha logo abaixo do CreateBuilder:
builder.Services.AddDbContext<DbPadelContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<GoogleCalendarSettings>(builder.Configuration.GetSection("GoogleCalendar"));
builder.Services.Configure<AcessoAntecipadoSettings>(builder.Configuration.GetSection("AcessoAntecipado"));
builder.Services.Configure<ZApiSettings>(builder.Configuration.GetSection("ZApi"));
builder.Services.AddSingleton<IPasswordHasher<Jogador>, PasswordHasher<Jogador>>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IEstatisticasService, EstatisticasService>();
builder.Services.AddScoped<IPalpiteService, PalpiteService>();
builder.Services.AddScoped<ISessaoGrupoService, SessaoGrupoService>();
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppApiService>();
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<AcessoAntecipadoMiddleware>();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
