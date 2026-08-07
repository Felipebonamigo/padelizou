using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O QUADRO da aba "Chaves e Grupos" (Services/QuadroDoMataMata): a montagem morava solta
// no Razor, sem teste — numeração global, pareamento primeiro x último e bye nomeado só
// se conferiam abrindo a tela. Estes testes seguram a estrutura E o caminho pintado do
// jogador, que usa a MESMA corrente do "Meus jogos" (Services/MeusJogos): jogo perdido
// não entrega nada, bye entra pelo nome.
public class QuadroDoMataMataTests
{
    private int _id = 1;
    private const int Eu = 100;

    private Dupla DuplaCom(int jogador1, int jogador2) =>
        new() { Id = _id++, Jogador1Id = jogador1, Jogador2Id = jogador2 };

    private Dupla DuplaQualquer() => DuplaCom(_id * 10 + 1, _id * 10 + 2);

    private Partida Jogo(string fase, Dupla d1, Dupla d2, int? vencedorId = null) => new()
    {
        Id = _id++,
        Codigo = $"TESTE-{_id}",
        Fase = fase,
        Dupla1 = d1,
        Dupla1Id = d1.Id,
        Dupla2 = d2,
        Dupla2Id = d2.Id,
        Status = vencedorId == null ? "Agendada" : "Finalizada",
        VencedorId = vencedorId,
    };

    // Quartas 1..4 reais, sem bye; o jogador (se houver) na dupla 1 do jogo 1.
    private List<Partida> QuartasDeOito(bool comigo = false)
    {
        var minha = comigo ? DuplaCom(Eu, 101) : DuplaQualquer();
        return new List<Partida>
        {
            Jogo("Quartas de Final", minha, DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        };
    }

    // ── A estrutura do quadro (o que antes ninguém testava) ─────────────────────────

    [Fact]
    public void Quadro_de_oito_empilha_quartas_semis_e_final_com_pareamento_primeiro_x_ultimo()
    {
        var fases = QuadroDoMataMata.Montar(QuartasDeOito(), [], meuJogadorId: null);

        Assert.Equal(["Quartas de Final", "Semifinal", "Final"], fases.Select(f => f.Nome));
        Assert.Equal([1, 2, 3, 4], fases[0].Vagas.Select(v => v.Numero));
        Assert.All(fases[0].Vagas, v => Assert.NotNull(v.Jogo));

        // Primeiro contra último, como o robô vai parear de verdade.
        var semis = fases[1].Vagas;
        Assert.Equal((5, 1, 4), (semis[0].Numero, semis[0].Lado1!.VemDoJogo, semis[0].Lado2!.VemDoJogo));
        Assert.Equal((6, 2, 3), (semis[1].Numero, semis[1].Lado1!.VemDoJogo, semis[1].Lado2!.VemDoJogo));

        var final = Assert.Single(fases[2].Vagas);
        Assert.Equal((7, 5, 6), (final.Numero, final.Lado1!.VemDoJogo, final.Lado2!.VemDoJogo));
    }

    [Fact]
    public void Bye_entra_nomeado_na_fase_seguinte_na_ordem_do_pareamento()
    {
        // 2 jogos de abertura + 2 byes = 6 duplas numa chave de 8. Entrantes da fase
        // seguinte: [vencedor 1, vencedor 2, bye melhor, bye pior] → primeiro x último.
        var jogos = new List<Partida>
        {
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        };
        var byeMelhor = DuplaQualquer();
        var byePior = DuplaQualquer();

        var fases = QuadroDoMataMata.Montar(jogos, [byeMelhor, byePior], meuJogadorId: null);

        var semis = fases[1].Vagas;
        Assert.Equal(byePior.Id, semis[0].Lado2!.DuplaDeBye!.Id);
        Assert.Equal(byeMelhor.Id, semis[1].Lado2!.DuplaDeBye!.Id);
        Assert.Equal(1, semis[0].Lado1!.VemDoJogo);
        Assert.Equal(2, semis[1].Lado1!.VemDoJogo);
    }

    // ── O caminho pintado ───────────────────────────────────────────────────────────

    [Fact]
    public void Caminho_pintado_segue_do_meu_jogo_ate_a_final()
    {
        var fases = QuadroDoMataMata.Montar(QuartasDeOito(comigo: true), [], Eu);

        // Meu jogo real e, dali, a semi que cita ele e a final — o outro lado do quadro não.
        Assert.Equal([true, false, false, false], fases[0].Vagas.Select(v => v.EhMinha));
        Assert.Equal([true, false], fases[1].Vagas.Select(v => v.EhMinha));
        Assert.True(fases[2].Vagas.Single().EhMinha);
    }

    // ⚠️ O teste que importa: sem cortar a corrente em quem perdeu, o eliminado abriria
    // o quadro e veria a final pintada como "pode ser seu".
    [Fact]
    public void Quem_perdeu_ve_o_proprio_jogo_pintado_mas_nada_adiante()
    {
        var jogos = QuartasDeOito(comigo: true);
        var perdido = jogos[0];
        perdido.Status = "Finalizada";
        perdido.VencedorId = perdido.Dupla2Id; // a outra dupla

        var fases = QuadroDoMataMata.Montar(jogos, [], Eu);

        // O jogo que joguei faz parte do trajeto; o que vem depois não é mais meu.
        Assert.True(fases[0].Vagas[0].EhMinha);
        Assert.All(fases[1].Vagas, v => Assert.False(v.EhMinha));
        Assert.False(fases[2].Vagas.Single().EhMinha);
    }

    [Fact]
    public void Meu_bye_pinta_a_vaga_em_que_eu_entro_e_dali_em_diante()
    {
        var jogos = new List<Partida>
        {
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        };
        var meuBye = DuplaCom(Eu, 101);
        var outroBye = DuplaQualquer();

        // Entrantes: [V1, V2, outroBye, meuBye] → semi 1 = V1 x meuBye, semi 2 = V2 x outroBye.
        var fases = QuadroDoMataMata.Montar(jogos, [outroBye, meuBye], Eu);

        Assert.Equal([true, false], fases[1].Vagas.Select(v => v.EhMinha));
        Assert.True(fases[2].Vagas.Single().EhMinha);
        // E os jogos reais dos outros continuam sem pintura.
        Assert.All(fases[0].Vagas, v => Assert.False(v.EhMinha));
    }

    [Fact]
    public void Deslogado_ou_de_fora_da_chave_nao_ve_pintura_nenhuma()
    {
        var deslogado = QuadroDoMataMata.Montar(QuartasDeOito(comigo: true), [], meuJogadorId: null);
        var torcedor = QuadroDoMataMata.Montar(QuartasDeOito(comigo: true), [], meuJogadorId: 999_999);

        Assert.All(deslogado.SelectMany(f => f.Vagas), v => Assert.False(v.EhMinha));
        Assert.All(torcedor.SelectMany(f => f.Vagas), v => Assert.False(v.EhMinha));
    }

    [Fact]
    public void Semifinal_real_minha_e_pintada_e_alimenta_a_final_projetada()
    {
        // Ganhei as quartas: a semi já existe como jogo REAL meu — pintada direto,
        // e a final projetada continua a corrente a partir dela.
        var minha = DuplaCom(Eu, 101);
        var rival1 = DuplaQualquer();
        var rival2 = DuplaQualquer();
        var rival3 = DuplaQualquer();
        var jogos = new List<Partida>
        {
            Jogo("Quartas de Final", minha, DuplaQualquer(), vencedorId: minha.Id),
            Jogo("Quartas de Final", rival1, DuplaQualquer(), vencedorId: rival1.Id),
            Jogo("Quartas de Final", rival2, DuplaQualquer(), vencedorId: rival2.Id),
            Jogo("Quartas de Final", rival3, DuplaQualquer(), vencedorId: rival3.Id),
            Jogo("Semifinal", minha, rival3),
            Jogo("Semifinal", rival1, rival2),
        };

        var fases = QuadroDoMataMata.Montar(jogos, [], Eu);

        Assert.Equal([true, false], fases[1].Vagas.Select(v => v.EhMinha));
        Assert.True(fases[2].Vagas.Single().EhMinha);
    }
}
