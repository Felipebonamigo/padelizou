using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// As regras PURAS dos Desafios (DESAFIOS.md): a semana, os prazos, quem venceu, os pontos e o
// anti-farm. Nada aqui toca banco — o caminho pelo controller está em DesafiosFluxoTests.
public class DesafiosRegrasTests
{
    private static Desafio Novo(string status = Desafio.Aceito) => new()
    {
        Id = 1,
        DesafianteJogador1Id = 1, DesafianteJogador2Id = 2,
        DesafiadoJogador1Id = 3, DesafiadoJogador2Id = 4,
        CategoriaPadraoId = 1, ClubeId = 1,
        Formato = FormatoDoDesafio.UmSet,
        Status = status,
        DataHora = new DateTime(2026, 8, 12, 20, 0, 0),
        PropostoEm = new DateTime(2026, 8, 11, 10, 0, 0),
    };

    // ── A semana ──────────────────────────────────────────────────────────────────────

    [Theory]
    // Segunda, terça, sábado: todos terminam no MESMO domingo.
    [InlineData(2026, 8, 10, 2026, 8, 16)]
    [InlineData(2026, 8, 11, 2026, 8, 16)]
    [InlineData(2026, 8, 15, 2026, 8, 16)]
    // Domingo termina nele mesmo — a semana do padel é segunda → domingo.
    [InlineData(2026, 8, 16, 2026, 8, 16)]
    public void Semana_termina_no_domingo(int ano, int mes, int dia, int aAno, int aMes, int aDia)
    {
        var fim = SemanaDoDesafio.FimDaSemanaDe(new DateTime(ano, mes, dia, 14, 0, 0));

        Assert.Equal(new DateTime(aAno, aMes, aDia), fim.Date);
        Assert.Equal(23, fim.Hour);
        Assert.Equal(59, fim.Minute);
    }

    [Fact]
    public void Sabado_a_noite_ja_sugere_a_semana_seguinte()
    {
        // Publicar "esta semana" num sábado à noite daria um anúncio de horas.
        var sabadoANoite = new DateTime(2026, 8, 15, 22, 0, 0);

        Assert.True(SemanaDoDesafio.SemanaJaEstaAcabando(sabadoANoite));
        Assert.Equal(new DateTime(2026, 8, 23), SemanaDoDesafio.FimDaSemanaSugerido(sabadoANoite).Date);
    }

    [Fact]
    public void Anuncio_vencido_sai_do_mural_sem_ninguem_gravar_nada()
    {
        // ⚠️ O ponto do teste: `Expirado` não é status. O anúncio continua `Publicado` no
        // banco e some do mural pelo RELÓGIO. Gravar exigiria um job, e job que falha calado
        // transforma anúncio vivo em anúncio morto.
        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = 1, Jogador2Id = 2,
            Status = AnuncioDeDesafio.Publicado,
            ValeAte = new DateTime(2026, 8, 16, 23, 59, 59),
        };

        Assert.True(SemanaDoDesafio.NoMural(anuncio, new DateTime(2026, 8, 16, 23, 0, 0)));
        Assert.False(SemanaDoDesafio.NoMural(anuncio, new DateTime(2026, 8, 17, 0, 1, 0)));
        Assert.Equal(AnuncioDeDesafio.Publicado, anuncio.Status);
    }

    [Fact]
    public void Anuncio_sem_o_parceiro_nao_aparece_no_mural()
    {
        var rascunho = new AnuncioDeDesafio
        {
            Jogador1Id = 1, Jogador2Id = null,
            Status = AnuncioDeDesafio.Publicado,
            ValeAte = new DateTime(2026, 8, 16, 23, 59, 59),
        };

        Assert.False(SemanaDoDesafio.NoMural(rascunho, new DateTime(2026, 8, 12)));
    }

    // ── Os dois prazos ────────────────────────────────────────────────────────────────

    [Fact]
    public void Proposta_sem_resposta_em_48h_morre_pelo_relogio()
    {
        var d = Novo(Desafio.Proposto);

        Assert.False(EstadoDoDesafio.PropostaVencida(d, d.PropostoEm.AddHours(47)));
        Assert.True(EstadoDoDesafio.PropostaVencida(d, d.PropostoEm.AddHours(49)));

        // E some da lista sem virar linha gravada.
        Assert.Equal("Sem resposta", EstadoDoDesafio.StatusNaTela(d, d.PropostoEm.AddHours(49)));
        Assert.Equal(Desafio.Proposto, d.Status);
    }

    [Fact]
    public void Depois_de_vencida_a_proposta_nao_pode_mais_ser_aceita()
    {
        var d = Novo(Desafio.Proposto);
        var depois = d.PropostoEm.AddHours(49);

        Assert.True(EstadoDoDesafio.PodeResponder(d, jogadorId: 3, d.PropostoEm.AddHours(1)));
        Assert.False(EstadoDoDesafio.PodeResponder(d, jogadorId: 3, depois));
    }

    [Fact]
    public void So_a_dupla_desafiada_responde()
    {
        var d = Novo(Desafio.Proposto);
        var agora = d.PropostoEm.AddHours(1);

        Assert.True(EstadoDoDesafio.PodeResponder(d, 3, agora));
        Assert.True(EstadoDoDesafio.PodeResponder(d, 4, agora));
        // Quem propôs não aceita o próprio desafio.
        Assert.False(EstadoDoDesafio.PodeResponder(d, 1, agora));
        Assert.False(EstadoDoDesafio.PodeResponder(d, 99, agora));
    }

    [Fact]
    public void Placar_so_depois_da_hora_marcada()
    {
        var d = Novo();

        Assert.False(EstadoDoDesafio.PodeLancarPlacar(d, 1, d.DataHora.AddMinutes(-1)));
        Assert.True(EstadoDoDesafio.PodeLancarPlacar(d, 1, d.DataHora));
        Assert.True(EstadoDoDesafio.PodeLancarPlacar(d, 4, d.DataHora.AddHours(2)));
    }

    [Fact]
    public void Quem_lancou_o_placar_nao_confirma_o_proprio_placar()
    {
        var d = Novo(Desafio.PlacarLancado);
        d.LancadoPorId = 1;                       // desafiante
        d.LancadoEm = d.DataHora.AddHours(1);
        var agora = d.LancadoEm.Value.AddHours(1);

        // O parceiro de quem lançou também não: seria a dupla assinando sozinha.
        Assert.False(EstadoDoDesafio.PodeResponderPlacar(d, 1, agora));
        Assert.False(EstadoDoDesafio.PodeResponderPlacar(d, 2, agora));
        Assert.True(EstadoDoDesafio.PodeResponderPlacar(d, 3, agora));
        Assert.True(EstadoDoDesafio.PodeResponderPlacar(d, 4, agora));
    }

    [Fact]
    public void Silencio_de_72h_confirma_o_placar_e_fecha_a_contestacao()
    {
        var d = Novo(Desafio.PlacarLancado);
        d.LancadoPorId = 1;
        d.LancadoEm = new DateTime(2026, 8, 12, 22, 0, 0);

        var dentro = d.LancadoEm.Value.AddHours(71);
        var fora = d.LancadoEm.Value.AddHours(73);

        Assert.False(EstadoDoDesafio.PlacarValeuPeloRelogio(d, dentro));
        Assert.True(EstadoDoDesafio.PlacarValeuPeloRelogio(d, fora));

        // ⚠️ A tela não depende do job: ela já diz "Confirmado".
        Assert.Equal(Desafio.Confirmado, EstadoDoDesafio.StatusNaTela(d, fora));

        // E contestar depois do prazo reabriria jogo fechado.
        Assert.True(EstadoDoDesafio.PodeResponderPlacar(d, 3, dentro));
        Assert.False(EstadoDoDesafio.PodeResponderPlacar(d, 3, fora));
    }

    // ── Quem venceu ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Games_em_branco_nao_dao_vitoria_a_quem_nao_marcou_nada()
    {
        // ⚠️ É o bug que QuemVenceu existe pra impedir: em C#, `4 > null` é false, e foi assim
        // que uma final saiu com campeão errado. Aqui o desafio usa a MESMA régua.
        var d = Novo();
        d.GamesDesafiante = 4;
        d.GamesDesafiado = null;

        Assert.Equal(1, EstadoDoDesafio.LadoVencedor(d));
    }

    [Fact]
    public void Sets_mandam_e_games_desempatam()
    {
        var d = Novo();
        d.SetsDesafiante = 1; d.SetsDesafiado = 2;
        d.GamesDesafiante = 14; d.GamesDesafiado = 13;

        // Perdeu nos sets mesmo tendo feito mais games.
        Assert.Equal(2, EstadoDoDesafio.LadoVencedor(d));
    }

    [Fact]
    public void Placar_empatado_nao_pode_ser_lancado()
    {
        Assert.NotNull(EstadoDoDesafio.MotivoParaNaoLancar(null, null, 6, 6));
        Assert.NotNull(EstadoDoDesafio.MotivoParaNaoLancar(null, null, null, null));
        Assert.Null(EstadoDoDesafio.MotivoParaNaoLancar(null, null, 6, 4));
    }

    // ── A chave da dupla ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_dupla_e_a_mesma_independente_da_ordem()
    {
        Assert.Equal(ChaveDaDupla.De(7, 3), ChaveDaDupla.De(3, 7));
        Assert.True(ChaveDaDupla.Mesma(7, 3, 3, 7));
        Assert.False(ChaveDaDupla.Mesma(7, 3, 7, 4));
    }

    // ── Os pontos ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dupla_sem_padelimetro_vale_os_dez_do_meio()
    {
        Assert.Equal(PontosDoDesafio.ExpectativaNeutra, PontosDoDesafio.Expectativa(null, 500, 500, 500));
        Assert.Equal(10, PontosDoDesafio.DaVitoria(PontosDoDesafio.ExpectativaNeutra));
    }

    [Fact]
    public void Ganhar_de_quem_e_melhor_vale_mais_e_os_extremos_ficam_presos_na_faixa()
    {
        var zebra = PontosDoDesafio.DaVitoria(0.0);      // não tinham chance nenhuma
        var passeio = PontosDoDesafio.DaVitoria(1.0);    // era barbada

        Assert.True(zebra > passeio);
        Assert.Equal(PontosDoDesafio.MaximoDaVitoria, zebra);

        // ⚠️ O piso: sem ele, ganhar de dupla mais fraca daria ZERO — e isso desestimularia
        // justamente quem topa jogar com gente nova.
        Assert.Equal(PontosDoDesafio.MinimoDaVitoria, passeio);

        // E a faixa não promete o que a conta não alcança: `20 × (1 − e)` não passa de 20.
        Assert.Equal(PontosDoDesafio.BaseDaVitoria, PontosDoDesafio.MaximoDaVitoria);
    }

    [Fact]
    public void Perder_nunca_tira_ponto_e_quem_jogou_leva_a_presenca()
    {
        var (desafiante, desafiado) = PontosDoDesafio.Do(
            ladoVencedor: 1, expectativaDoDesafiante: 0.5, confrontosAnterioresNoMes: 0);

        Assert.Equal(PontosDoDesafio.Presenca + 10, desafiante);
        Assert.Equal(PontosDoDesafio.Presenca, desafiado);
    }

    [Fact]
    public void Repetir_o_mesmo_adversario_no_mes_deixa_de_pagar()
    {
        // 1º cheio, 2º metade (arredondando pra cima), 3º em diante só a presença.
        Assert.Equal(10, PontosDoDesafio.ComDescontoDeRepeticao(10, 0));
        Assert.Equal(5, PontosDoDesafio.ComDescontoDeRepeticao(10, 1));
        Assert.Equal(0, PontosDoDesafio.ComDescontoDeRepeticao(10, 2));
        Assert.Equal(0, PontosDoDesafio.ComDescontoDeRepeticao(10, 9));

        // Ímpar arredonda pra cima: 5 vira 3, não 2.
        Assert.Equal(3, PontosDoDesafio.ComDescontoDeRepeticao(5, 1));
    }

    [Fact]
    public void No_terceiro_confronto_do_mes_sobra_so_a_presenca()
    {
        var (desafiante, desafiado) = PontosDoDesafio.Do(1, 0.5, confrontosAnterioresNoMes: 2);

        Assert.Equal(PontosDoDesafio.Presenca, desafiante);
        Assert.Equal(PontosDoDesafio.Presenca, desafiado);
    }

    [Fact]
    public void Jogo_que_nao_aconteceu_nao_vale_ponto_pra_ninguem()
    {
        var (desafiante, desafiado) = PontosDoDesafio.SemComparecimento();

        Assert.Equal(0, desafiante);
        Assert.Equal(0, desafiado);
    }

    // ── O formato ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Melhor de 3 sets", true)]
    [InlineData("1 set", false)]
    [InlineData("Games combinados", false)]
    [InlineData("qualquer coisa", false)]
    public void So_o_melhor_de_tres_pergunta_sets(string formato, bool pedeSets)
    {
        Assert.Equal(pedeSets, FormatoDoDesafio.PedeSets(formato));
    }

    [Fact]
    public void Formato_desconhecido_cai_no_padrao_em_vez_de_ser_gravado()
    {
        // Formulário antigo em cache ou link velho não pode gravar um formato que a tela do
        // placar depois não sabe desenhar.
        Assert.Equal(FormatoDoDesafio.UmSet, FormatoDoDesafio.Valido("Melhor de 5"));
        Assert.Equal(FormatoDoDesafio.UmSet, FormatoDoDesafio.Valido(null));
    }
}
