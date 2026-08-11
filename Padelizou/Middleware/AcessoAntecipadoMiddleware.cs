using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Middleware;

// Trava geral do site (login/senha único, sem relação com as contas de Jogador) usada só na
// fase de teste fechado com conhecidos, antes de o Padelizou ficar público de verdade.
// Fica desligada em Development (appsettings.Development.json) e ligada no appsettings.json
// base (que é gitignored) — então só entra em ação quando publicado.
public class AcessoAntecipadoMiddleware
{
    public const string NomeCookie = "PadelizouAcessoLiberado";

    // Só o caminho exato do webhook entra aqui — liberar "/Pagamentos" inteiro abriria as telas
    // de extrato e configuração de recebimento pra qualquer visitante. O webhook em si não fica
    // desprotegido: ele exige o token secreto do Asaas no header.
    //
    // "/Auth/Login" e companhia entraram em 05/08/2026: quem JÁ TEM CONTA entra com o login
    // dele, sem a senha compartilhada. A recuperação de senha vem junto de propósito — quem
    // esqueceu a senha da própria conta é exatamente quem não tem como pedir a do portão.
    // "/Auth/Cadastro" continua FORA: conta nova segue sendo por convite, que é o que o
    // portão existe pra controlar.
    //
    // "/.well-known" é onde mora o assetlinks.json do app Android. Quem busca esse arquivo é um
    // robô do Google, que não tem cookie, não tem conta e NÃO segue redirecionamento: se o
    // portão for religado pelo painel e o caminho não estiver aqui, ele leva 302 pra tela de
    // senha, a verificação falha e o app instalado passa a mostrar a barra de endereço do
    // Chrome por cima — no celular de todo mundo, sem nada quebrar deste lado. Não tem nada
    // secreto lá dentro: o arquivo é público por definição.
    public static readonly string[] PrefixosLiberados =
    {
        "/AcessoAntecipado", "/lib", "/css", "/js", "/image", "/uploads", "/favicon", "/Agenda/Feed",
        "/manifest.json", "/sw.js", "/Pagamentos/Webhook", "/healthz", "/.well-known",
        "/Auth/Login", "/Auth/Logout", "/Auth/EsqueciSenha", "/Auth/RedefinirSenha"
    };

    private readonly RequestDelegate _next;

    public AcessoAntecipadoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<AcessoAntecipadoSettings> options,
        DbPadelContext db, PortaoDeAcesso portao)
    {
        // No subdomínio admin (admin.padelizou.com.br) esse gate nem entra em ação — nem a
        // senha compartilhada, nem o auto-login de demonstração como Felipe. Ele existe pra
        // liberar o site público pra quem tem a senha; deixar rodar aqui faria qualquer pessoa
        // com a senha pública (que vai ser compartilhada numa apresentação) virar admin sem
        // nunca ter feito login de verdade.
        if (AdminHostMiddleware.EhHostAdmin(context))
        {
            await _next(context);
            return;
        }

        var settings = options.Value;

        // O botão do admin manda; na falta dele, o padrão do systemd (ver Services/PortaoDeAcesso).
        if (!portao.EstaHabilitado(settings) || EhCaminhoLiberado(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // O middleware de autenticação normal do ASP.NET Core ainda não rodou nesse ponto do
        // pipeline (estamos antes do UseRouting) — por isso autenticamos manualmente aqui.
        // Isso PRECISA vir antes da decisão de barrar: quem já está logado passa.
        var resultado = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (resultado.Succeeded)
        {
            context.User = resultado.Principal!;
        }

        // Quem já tem conta no Padelizou não precisa da senha compartilhada: a conta DELE é o
        // convite, e ele já passou pelo portão uma vez pra se cadastrar. Antes o portão vinha
        // antes de tudo, então trocar de celular, limpar o navegador ou o cookie de 90 dias
        // expirar trancava um usuário de verdade do lado de fora do próprio cadastro — e a
        // saída era pedir de novo uma senha compartilhada que ele não tem por que guardar.
        if (!resultado.Succeeded && !EstaLiberado(context, settings))
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/AcessoAntecipado/Entrar?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        if (!resultado.Succeeded && !string.IsNullOrWhiteSpace(settings.LoginAutomaticoCpf))
        {
            // MODO DEMONSTRAÇÃO: quem passou pela senha do gate entra logado como esse jogador,
            // checado em toda request (não só no POST do form) pra que os menus de logado não
            // sumam quando o cookie expira ou a pessoa troca de aba. Desligado onde
            // LoginAutomaticoCpf está vazio — aí cada visitante cria a própria conta.
            var jogadorDemo = await db.Jogadores.FirstOrDefaultAsync(j => j.Cpf == settings.LoginAutomaticoCpf);
            if (jogadorDemo != null)
            {
                // Mesmo conjunto de claims do login normal — sem isso os menus condicionais
                // (Painel do Professor, Painel Admin) não apareciam nesse modo, já que dependiam
                // de IsProfessor/IsAdmin que faltavam aqui. Agora vem de IdentidadeJogador, então
                // ficar igual deixou de depender de alguém lembrar.
                var claims = IdentidadeJogador.ClaimsDe(jogadorDemo);
                var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identidade);
                // Mesmas propriedades do login de verdade (Services/SessaoDoJogador). O
                // ExpiresUtc fixo que morava aqui furava a renovação deslizante: 90 dias
                // contados do primeiro acesso, e não dos últimos 90 dias de uso.
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    SessaoDoJogador.Propriedades());
                context.User = principal;
            }
        }

        await _next(context);
    }

    // Público pra ser testável: a lista de quem o portão NÃO fecha é o tipo de coisa que se
    // afrouxa sem querer (liberar "/Auth" inteiro abriria o cadastro junto com o login).
    public static bool EhCaminhoLiberado(PathString path)
    {
        return PrefixosLiberados.Any(prefixo => path.StartsWithSegments(prefixo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EstaLiberado(HttpContext context, AcessoAntecipadoSettings settings)
    {
        return context.Request.Cookies.TryGetValue(NomeCookie, out var valor) && valor == CalcularHash(settings);
    }

    public static string CalcularHash(AcessoAntecipadoSettings settings)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{settings.Usuario}:{settings.Senha}:padelizou-acesso-antecipado"));
        return Convert.ToHexString(bytes);
    }
}
