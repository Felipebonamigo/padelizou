using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O "vai caber?" da tela de criação vem do SERVIDOR desde 08/08/2026.
//
// ⚠️ Antes disso a tela tinha a própria cópia das contas, em JavaScript — grupos e mata-mata,
// o que fecha no Americano, o todos-contra-todos de duplas e a grade do dia. Seis blocos, e o
// comentário de lá dizia que divergir dos Services faria a tela mentir pro organizador. A
// cópia era invisível pra esta suíte (ela lê C#, não JavaScript), então a divergência só
// apareceria no dia do torneio, com a quadra reservada.
//
// Estes testes guardam a PORTA: que ela existe, que responde a mesma conta do serviço, e que
// não é porta aberta. A aritmética em si está em PrevisaoDoTorneioTests.
public class EndpointDePrevisaoTests
{
    private static DbPadelContext Contexto()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1" });
        ctx.SaveChanges();
        return ctx;
    }

    private static PrevisaoDoTorneio.Tela Pedir(string formato, int numero, int quadras = 3)
    {
        using var ctx = Contexto();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var resposta = controller.Previsao(
            formato, numero, duracao: 50, quadras: quadras,
            horaInicio: new TimeSpan(18, 0, 0),
            horaSeguintes: new TimeSpan(8, 0, 0),
            horaFim: new TimeSpan(23, 0, 0),
            dataInicio: new DateTime(2026, 8, 21),
            dataFim: new DateTime(2026, 8, 23));

        return Assert.IsType<PrevisaoDoTorneio.Tela>(Assert.IsType<JsonResult>(resposta).Value);
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Padrao, 50)]
    [InlineData(FormatoDoTorneio.Americano, 16)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas, 8)]
    public void A_porta_devolve_exatamente_o_que_o_servico_calcula(string formato, int numero)
    {
        var pelaPorta = Pedir(formato, numero);

        var direto = PrevisaoDoTorneio.ParaATela(
            formato, numero, 50, 3,
            new TimeSpan(18, 0, 0), new TimeSpan(8, 0, 0), new TimeSpan(23, 0, 0),
            new DateTime(2026, 8, 21), new DateTime(2026, 8, 23));

        // Record: a igualdade é estrutural, então isto compara a resposta INTEIRA — nenhum
        // campo pode ser recalculado ou "ajustado" no controller sem quebrar aqui.
        Assert.Equal(direto, pelaPorta);
        Assert.True(pelaPorta.TemConta);
    }

    // ⚠️ O formato TEM que chegar até a conta. Se o controller perdesse esse parâmetro, os
    // três formatos responderiam a mesma coisa — e o erro seria silencioso, porque a tela
    // continuaria mostrando um número plausível.
    [Fact]
    public void Cada_formato_responde_a_propria_conta()
    {
        int oficial = Pedir(FormatoDoTorneio.Padrao, 16).TotalDeJogos;
        int americano = Pedir(FormatoDoTorneio.Americano, 16).TotalDeJogos;
        int deDuplas = Pedir(FormatoDoTorneio.AmericanoDeDuplas, 16).TotalDeJogos;

        Assert.Equal(3, new[] { oficial, americano, deDuplas }.Distinct().Count());
    }

    [Fact]
    public void O_numero_que_nao_fecha_volta_com_a_saida_escrita()
    {
        var tela = Pedir(FormatoDoTorneio.Americano, 6);

        Assert.False(tela.Fecha);
        Assert.Null(tela.Resumo);
        Assert.False(string.IsNullOrWhiteSpace(tela.PorQueNaoFecha));
    }

    // A tela é do organizador logado: não é superfície anônima nova no site.
    [Fact]
    public void A_porta_exige_login()
    {
        var acao = typeof(TorneiosController).GetMethod(nameof(TorneiosController.Previsao));

        Assert.NotNull(acao);
        Assert.NotEmpty(acao!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true));
    }
}
