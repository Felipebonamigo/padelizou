using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using padelizou.Controllers;   // o AdminController ficou no namespace legado, em minúsculo
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O FILTRO DA TELA DE ERROS, por TIPO e por CAMINHO.
//
// 🗣️ Felipe, 10/09/2026, depois do `DbUpdateException em POST /Partidas/Votar`: *"faz o filtro
// por tipo e caminho na tela de erros"*. A pergunta que ele precisava responder era estreita —
// "existe ESTE tipo NESTE caminho depois das 15h07?" — e a tela só sabia mostrar as últimas 100
// linhas de tudo, misturadas.
//
// ⚠️ O QUE ESTES TESTES GUARDAM, e é uma coisa só: **o filtro roda na TABELA, não na página**.
// Filtrar depois do `Take(100)` compila, parece certo na tela cheia e mente exatamente quando
// mais importa — no dia em que outro erro estourou 200 vezes e empurrou o que se procura pra
// fora da janela. A tela diria "nenhum erro deste tipo" com o erro registrado no banco.
public class FiltroDaTelaDeErrosTests
{
    private const int IdDoAdmin = 4000;

    private static DbPadelContext ContextoComAdmin()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = IdDoAdmin, Nome = "Felipe Bonamigo", Cpf = "99900000001",
            Email = "felipe@exemplo.com", SenhaHash = "hash", IsAdminRaiz = true,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static AdminController Controlador(DbPadelContext ctx)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, IdDoAdmin.ToString()) }, "Teste")),
        };
        http.Request.Method = HttpMethods.Get;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static void Registrar(DbPadelContext ctx, string tipo, string caminho, DateTime quando)
    {
        ctx.ErrosDoSistema.Add(new ErroDoSistema
        {
            QuandoEm = quando, Tipo = tipo, Caminho = caminho, Metodo = "POST",
            Mensagem = $"{tipo} em {caminho}", Detalhe = "stack",
        });
        ctx.SaveChanges();
    }

    private static async Task<(List<ErroDoSistema> lista, ViewDataDictionary dados)> AbrirAsync(
        DbPadelContext ctx, string? tipo = null, string? caminho = null)
    {
        var controller = Controlador(ctx);
        var view = Assert.IsType<ViewResult>(await controller.Erros(tipo, caminho));
        return (Assert.IsType<List<ErroDoSistema>>(view.Model), view.ViewData);
    }

    private static readonly DateTime Tarde = new(2026, 9, 10, 15, 0, 0);

    [Fact]
    public async Task Filtrar_por_TIPO_deixa_so_aquele_tipo()
    {
        using var ctx = ContextoComAdmin();
        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", Tarde);
        Registrar(ctx, "NullReferenceException", "/Partidas/Votar", Tarde.AddMinutes(1));

        var (lista, _) = await AbrirAsync(ctx, tipo: "DbUpdateException");

        Assert.Equal(new[] { "DbUpdateException" }, lista.Select(e => e.Tipo).Distinct());
    }

    [Fact]
    public async Task Filtrar_por_CAMINHO_deixa_so_aquele_caminho()
    {
        using var ctx = ContextoComAdmin();
        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", Tarde);
        Registrar(ctx, "DbUpdateException", "/Torneios/GerarChaves", Tarde.AddMinutes(1));

        var (lista, _) = await AbrirAsync(ctx, caminho: "/Partidas/Votar");

        Assert.Equal(new[] { "/Partidas/Votar" }, lista.Select(e => e.Caminho).Distinct());
    }

    [Fact]
    public async Task Os_dois_juntos_respondem_a_pergunta_estreita()
    {
        using var ctx = ContextoComAdmin();
        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", Tarde);
        Registrar(ctx, "DbUpdateException", "/Torneios/GerarChaves", Tarde.AddMinutes(1));
        Registrar(ctx, "NullReferenceException", "/Partidas/Votar", Tarde.AddMinutes(2));

        var (lista, _) = await AbrirAsync(ctx, "DbUpdateException", "/Partidas/Votar");

        var achado = Assert.Single(lista);
        Assert.Equal("DbUpdateException", achado.Tipo);
        Assert.Equal("/Partidas/Votar", achado.Caminho);
    }

    [Fact]
    public async Task O_filtro_procura_na_TABELA_INTEIRA_e_nao_so_nas_ultimas_100()
    {
        using var ctx = ContextoComAdmin();

        // O que se procura: uma linha só, e VELHA.
        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", Tarde);

        // E, por cima dela, uma rajada de outro erro — o dia em que alguma coisa entrou em
        // laço. Sem filtrar no banco, as 120 empurram a linha de cima pra fora do `Take(100)`
        // e a tela responde "nenhum erro deste tipo" com o erro gravado.
        for (int i = 1; i <= 120; i++)
            Registrar(ctx, "NullReferenceException", "/Home/Index", Tarde.AddMinutes(i));

        var (lista, _) = await AbrirAsync(ctx, "DbUpdateException", "/Partidas/Votar");

        var achado = Assert.Single(lista);
        Assert.Equal(Tarde, achado.QuandoEm);
    }

    [Fact]
    public async Task As_opcoes_do_filtro_saem_da_tabela_inteira()
    {
        using var ctx = ContextoComAdmin();

        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", Tarde);
        for (int i = 1; i <= 120; i++)
            Registrar(ctx, "NullReferenceException", "/Home/Index", Tarde.AddMinutes(i));

        var (_, dados) = await AbrirAsync(ctx);

        // ⚠️ As opções nascem da MESMA cegueira do filtro: montadas a partir das últimas 100,
        // o tipo que se procura some da caixinha justo no dia da rajada — e some sem avisar.
        Assert.Contains("DbUpdateException", (List<string>)dados["TiposDoRegistro"]!);
        Assert.Contains("/Partidas/Votar", (List<string>)dados["CaminhosDoRegistro"]!);
    }

    [Fact]
    public async Task Sem_filtro_a_tela_continua_sendo_as_ultimas_100()
    {
        using var ctx = ContextoComAdmin();
        for (int i = 1; i <= 120; i++)
            Registrar(ctx, "NullReferenceException", "/Home/Index", Tarde.AddMinutes(i));

        var (lista, _) = await AbrirAsync(ctx);

        Assert.Equal(100, lista.Count);
        Assert.Equal(Tarde.AddMinutes(120), lista.First().QuandoEm);   // a mais nova no topo
    }

    [Fact]
    public async Task O_contador_de_24h_conta_o_que_o_FILTRO_escolheu()
    {
        using var ctx = ContextoComAdmin();
        var agora = DateTime.Now;

        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", agora.AddHours(-1));
        Registrar(ctx, "DbUpdateException", "/Partidas/Votar", agora.AddHours(-30));   // fora da janela
        Registrar(ctx, "NullReferenceException", "/Home/Index", agora.AddHours(-2));   // fora do filtro

        var (_, dados) = await AbrirAsync(ctx, "DbUpdateException", "/Partidas/Votar");

        Assert.Equal(1, (int)dados["Ultimas24h"]!);
    }

    [Fact]
    public async Task O_contador_de_24h_nao_para_em_100()
    {
        using var ctx = ContextoComAdmin();
        var agora = DateTime.Now;

        // ⚠️ Contado sobre a página, "nas últimas 24h" empaca em 100 e vira número errado
        // exatamente no dia ruim — que é o único dia em que alguém abre esta tela.
        for (int i = 1; i <= 120; i++)
            Registrar(ctx, "NullReferenceException", "/Home/Index", agora.AddMinutes(-i));

        var (_, dados) = await AbrirAsync(ctx);

        Assert.Equal(120, (int)dados["Ultimas24h"]!);
    }
}
