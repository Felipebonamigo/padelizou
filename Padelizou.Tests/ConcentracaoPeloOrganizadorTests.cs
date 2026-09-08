using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "OS 2 JOGOS NA SEXTA" — a régua e a fiação do favor que só o organizador concede.
//
// 🗣️ Felipe, 08/09/2026: "apenas para os organizadores e adm do sistema, para que nós possamos
// auxiliar algumas pessoas".
//
// O que este arquivo trava, e que nenhum outro trava:
//  • a concentração e o impedimento são EXCLUSIVOS — marcar um zera o outro;
//  • ela é DE GRAÇA (decisão do Felipe), e quem tinha impedimento pago vê o valor ABAIXAR;
//  • o JOGADOR não alcança a opção pelo formulário dele, nem montando o POST à mão.
public class ConcentracaoPeloOrganizadorTests
{
    private static Torneio Torneio(decimal taxa = 0m) => new()
    {
        Nome = "T", Codigo = "T1", Status = "Inscrições Abertas",
        DataInicio = new DateTime(2026, 8, 14, 18, 0, 0),   // sexta
        TaxaPorImpedimento = taxa,
    };

    // ── A régua (Services/AlteracaoDeImpedimento) ─────────────────────────────────────────

    [Fact]
    public void TurnoAtual_devolve_a_concentracao_quando_ela_esta_marcada()
    {
        var dupla = new Dupla { ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha };

        Assert.Equal(TurnoDoImpedimento.SoSabadoManha, AlteracaoDeImpedimento.TurnoAtual(dupla));
    }

    [Fact]
    public void Concentracao_nao_custa_nada()
    {
        var dupla = new Dupla();

        Assert.Equal(0m, AlteracaoDeImpedimento.QuantoMudaOValor(dupla, Torneio(taxa: 25m),
            TurnoDoImpedimento.SoSextaNoite));
    }

    // ⚠️ O outro lado da decisão do Felipe ("grátis, e zera a taxa"): quem PAGOU um impedimento
    // e vira concentração passa a dever menos, igual a tirar o impedimento.
    [Fact]
    public void Trocar_impedimento_pago_por_concentracao_abaixa_o_valor()
    {
        var dupla = new Dupla { ImpedimentoSextaNoite = true };

        Assert.Equal(-25m, AlteracaoDeImpedimento.QuantoMudaOValor(dupla, Torneio(taxa: 25m),
            TurnoDoImpedimento.SoSabadoTarde));
    }

    [Fact]
    public void Aplicar_concentracao_zera_os_impedimentos()
    {
        var dupla = new Dupla { ImpedimentoSextaNoite = true, ValorInscricao = 325m };

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(taxa: 25m), TurnoDoImpedimento.SoSabadoManha, 1, DateTime.Now);

        Assert.Equal(TurnoDeConcentracao.SabadoManha, dupla.ConcentrarJogosEm);
        Assert.False(dupla.ImpedimentoSextaNoite);
        Assert.Equal(300m, dupla.ValorInscricao);
    }

    [Fact]
    public void Aplicar_impedimento_zera_a_concentracao()
    {
        var dupla = new Dupla { ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite };

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(taxa: 25m), TurnoDoImpedimento.SabadoTarde, 1, DateTime.Now);

        Assert.Null(dupla.ConcentrarJogosEm);
        Assert.True(dupla.ImpedimentoSabadoTarde);
    }

    [Fact]
    public void Tirar_tudo_limpa_impedimento_e_concentracao()
    {
        var dupla = new Dupla { ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite };

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(), TurnoDoImpedimento.Nenhum, 1, DateTime.Now);

        Assert.Null(dupla.ConcentrarJogosEm);
        Assert.Equal(TurnoDoImpedimento.Nenhum, AlteracaoDeImpedimento.TurnoAtual(dupla));
    }

    // ── A fronteira de confiança: o JOGADOR não concede favor a si mesmo ──────────────────

    // ⚠️ A tela do jogador não oferece as três opções, mas tela não é trava: um POST montado à
    // mão chega com `SoSextaNoite` do mesmo jeito. Sem esta recusa, qualquer inscrito se daria
    // a concentração — e ainda ABAIXARIA o próprio valor devido, porque ela é de graça.
    [Theory]
    [InlineData(TurnoDoImpedimento.SoSextaNoite)]
    [InlineData(TurnoDoImpedimento.SoSabadoManha)]
    [InlineData(TurnoDoImpedimento.SoSabadoTarde)]
    public void Jogador_nao_marca_concentracao_nem_pelo_POST(TurnoDoImpedimento turno)
    {
        var dupla = new Dupla { Id = 1, Jogador1Id = 10, Jogador2Id = 11 };

        var motivo = AlteracaoDeImpedimento.MotivoParaNaoAlterar(dupla, Torneio(), quemPede: 10, novo: turno);

        Assert.NotNull(motivo);
        Assert.Contains("organizador", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Organizador_pode_marcar_concentracao()
    {
        var dupla = new Dupla { Id = 1, Jogador1Id = 10, Jogador2Id = 11 };

        Assert.Null(AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar(dupla, Torneio(), jaSorteou: false));
    }

    // ── A fiação do controller ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_concentra_os_jogos_da_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SoSextaNoite);

        Assert.Equal(TurnoDeConcentracao.SextaNoite, (await ctx.Duplas.FindAsync(dupla.Id))!.ConcentrarJogosEm);
    }

    [Fact]
    public async Task So_organizador_concentra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000077" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SoSextaNoite);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.ConcentrarJogosEm);
    }

    // O JOGADOR pelo caminho DELE (AlterarImpedimento), que é o buraco que a régua acima fecha.
    [Fact]
    public async Task Jogador_pelo_proprio_formulario_nao_concentra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        await TestInfra.NovoTorneiosController(ctx, dupla.Jogador1Id)
            .AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SoSextaNoite);

        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.ConcentrarJogosEm);
    }

    // Mesmo alcance do impedimento trocado pelo organizador: ele mexeu por fora, os DOIS sabem.
    [Fact]
    public async Task Os_dois_da_dupla_sao_avisados()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var push = NSubstitute.Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, org.Id, push: push)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SoSabadoTarde);

        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador1Id, "O organizador mudou seu impedimento",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador2Id!.Value, "O organizador mudou seu impedimento",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }
}
