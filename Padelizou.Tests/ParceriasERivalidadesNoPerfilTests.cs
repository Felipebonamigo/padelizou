using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// 10/11/2026 — MELHOR PARCEIRO, CARRASCO DE, FREGUÊS DE: NO PERFIL DE TODO MUNDO.
//
// 🗣️ Felipe, com o print do card de outro sistema (Deivid: "Melhor parceiro Emerson Pisoni
// 41V·27D · Carrasco de Juliano Bender 6V·2D · Freguês de Felipe Bonamigo 1V·5D"): *"outro
// sistema tem isso, o nosso tem que ter"*.
//
// O Padelizou JÁ CALCULAVA (EstatisticasService.MontarDestaques), mas só mostrava no PRÓPRIO
// perfil, em segunda pessoa ("quem mais te venceu"). Agora sai no perfil de qualquer jogador —
// é resultado de padel, público como o ranking e as chaves (o perfil privado esconde contato,
// e só) — em terceira pessoa e com o placar V·D de cada linha.
//
// ⚠️ "MELHOR" PARCEIRO É QUEM MAIS VENCEU JUNTO, não quem mais jogou junto: o print põe 41V·27D,
// e o parceiro de 68 jogos com 20 vitórias não é o "melhor".
public class ParceriasERivalidadesNoPerfilTests
{
    private static Jogador J(int id, string nome) => new() { Id = id, Nome = nome, Cpf = id.ToString() };

    [Fact]
    public void Melhor_parceiro_e_quem_mais_venceu_junto()
    {
        var muitoJogo = new ParceiroResumoVM { Parceiro = J(1, "Muito Jogo"), Jogos = 20, Vitorias = 5 };
        var muitaVitoria = new ParceiroResumoVM { Parceiro = J(2, "Muita Vitória"), Jogos = 12, Vitorias = 9 };

        var d = EstatisticasService.MontarDestaques(new() { muitoJogo, muitaVitoria }, new());

        Assert.Same(muitaVitoria, d.MelhorParceiro);
    }

    [Fact]
    public void Empate_em_vitorias_desempata_por_quem_jogou_mais()
    {
        var a = new ParceiroResumoVM { Parceiro = J(1, "A"), Jogos = 8, Vitorias = 4 };
        var b = new ParceiroResumoVM { Parceiro = J(2, "B"), Jogos = 10, Vitorias = 4 };

        Assert.Same(b, EstatisticasService.MontarDestaques(new() { a, b }, new()).MelhorParceiro);
    }

    [Fact]
    public void Carrasco_e_quem_ele_vence_mais_do_que_perde_e_fregues_o_contrario()
    {
        // Medido no navegador: com "Vitorias > 0" e "Derrotas > 0", o mesmo adversário de 1V·2D
        // saía como Carrasco E como Freguês no mesmo card. No print de referência, carrasco é
        // 6V·2D e freguês 1V·5D: o saldo é que decide de que lado a pessoa fica.
        var equilibrado = new ConfrontoResumoVM { Oponente = J(1, "Equilibrado"), Jogos = 2, Vitorias = 1, Derrotas = 1 };
        var perdeuMais = new ConfrontoResumoVM { Oponente = J(2, "Perdeu Mais"), Jogos = 3, Vitorias = 1, Derrotas = 2 };
        var venceuMais = new ConfrontoResumoVM { Oponente = J(3, "Venceu Mais"), Jogos = 3, Vitorias = 2, Derrotas = 1 };

        var d = EstatisticasService.MontarDestaques(new(), new() { equilibrado, perdeuMais, venceuMais });

        Assert.Same(venceuMais, d.VoceMaisVenceu);   // carrasco de
        Assert.Same(perdeuMais, d.MaisTeVenceu);     // freguês de
    }

    [Fact]
    public void Ninguem_e_carrasco_e_fregues_da_mesma_pessoa()
    {
        var unico = new ConfrontoResumoVM { Oponente = J(1, "Rafael"), Jogos = 3, Vitorias = 1, Derrotas = 2 };

        var d = EstatisticasService.MontarDestaques(new(), new() { unico });

        Assert.Null(d.VoceMaisVenceu);
        Assert.Same(unico, d.MaisTeVenceu);
    }

    [Fact]
    public async Task O_perfil_de_outra_pessoa_tambem_traz_os_destaques()
    {
        using var ctx = TestInfra.NovoContexto();
        var dono = TestInfra.NovoJogador(1);
        var visitante = TestInfra.NovoJogador(2);
        ctx.Jogadores.AddRange(dono, visitante);
        ctx.SaveChanges();

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoJogadoresController(ctx, visitante.Id)
            .Perfil(dono.Id, TestInfra.PortaDosDesafiosDe(ctx)));

        Assert.IsType<DestaquesJogadorVM>(view.ViewData["Destaques"]);
    }

    [Fact]
    public async Task Quem_nao_esta_logado_tambem_ve()
    {
        using var ctx = TestInfra.NovoContexto();
        var dono = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(dono);
        ctx.SaveChanges();

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoJogadoresController(ctx)
            .Perfil(dono.Id, TestInfra.PortaDosDesafiosDe(ctx)));

        Assert.IsType<DestaquesJogadorVM>(view.ViewData["Destaques"]);
    }

    [Fact]
    public void O_card_fala_em_terceira_pessoa_e_mostra_o_placar()
    {
        var card = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Shared", "_DestaquesJogador.cshtml"));

        Assert.Contains("Melhor parceiro", card);
        Assert.Contains("Carrasco de", card);
        Assert.Contains("Freguês de", card);
        // O card vale pra qualquer perfil: "te venceu"/"pra você" mentiria no perfil dos outros.
        Assert.DoesNotContain("te venceu", card);
        Assert.DoesNotContain("pra você", card);
        // O placar no formato do print: "41V · 27D", sem espaço entre o número e a letra.
        Assert.Contains("V · @", card);
        Assert.DoesNotContain(" V · ", card);
    }
}
