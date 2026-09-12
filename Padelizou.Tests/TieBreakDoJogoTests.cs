using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TIE-BREAK EM PONTOS (Felipe, 12/09/2026): *"no placar ao vivo, ao ficar 8x8, deveria
// aparecer uma contagem de tie break, que pode ir até 7 ou até 10, depende do torneio"*.
//
// Até aqui "tie-break" neste projeto era só um COMENTÁRIO: o 9º game do jogo até 9 era
// chamado de tie-break porque é ele que desempata o 8x8, e nenhum ponto era contado em
// lugar nenhum. Quem está na quadra conta 7 (ou 10) pontos de verdade, e o placar ao vivo
// não tinha onde mostrar isso.
//
// ⚠️ A DECISÃO QUE SEGURA TUDO: quem fecha o tie-break leva o 9º GAME (9x8). Os games
// continuam sendo a verdade do sistema — classificação de grupo, saldo de games, desempate,
// Padelímetro, chave e API não sabem que tie-break existe. Os pontos são informação a mais.
public class TieBreakDoJogoTests
{
    private static FormatoDaPartida.Formato Ate(int games, int pontosDoTieBreak) =>
        new(1, games, ContagemDeGamesDoTorneio.Ate, pontosDoTieBreak);

    private static FormatoDaPartida.Formato Soma(int games, int pontosDoTieBreak) =>
        new(1, games, ContagemDeGamesDoTorneio.Soma, pontosDoTieBreak);

    // ── QUANDO A CONTAGEM APARECE ────────────────────────────────────────────────────────

    [Fact]
    public void No_8x8_do_jogo_ate_9_o_tie_break_comeca()
    {
        Assert.True(TieBreakDoJogo.EmAndamento(Ate(9, 7), 8, 8));
    }

    [Theory]
    [InlineData(8, 7)]   // 8x7 ainda é jogo de games
    [InlineData(7, 7)]   // empate, mas não a um game do fim
    [InlineData(9, 8)]   // já fechou: o 9º game saiu
    [InlineData(0, 0)]
    public void Fora_do_empate_a_um_game_do_fim_nao_ha_tie_break(int g1, int g2)
    {
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(9, 7), g1, g2));
    }

    [Fact]
    public void Torneio_com_tie_break_desligado_segue_resolvendo_o_8x8_no_9o_game()
    {
        // Zero é o que a migration grava em todo torneio que já existe: nada muda pra quem
        // está em quadra hoje.
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(9, TieBreakDoJogo.Desligado), 8, 8));
    }

    [Fact]
    public void Jogo_de_numero_PAR_continua_no_vencer_por_dois_e_nao_vira_tie_break()
    {
        // Decisão do Felipe (12/09/2026): a contagem entra só onde o limite é ÍMPAR. Num jogo
        // até 4, o 3x3 continua indo pro 5º game — mexer nisso mudaria a regra de um torneio
        // que já está rodando com o "vencer por dois".
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(4, 7), 3, 3));
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(6, 10), 5, 5));
    }

    [Fact]
    public void Na_soma_nao_ha_tie_break()
    {
        // Numa soma de 9 o 8x8 nem existe (o bolo é 9 games), mas a régua não pode depender
        // disso: sem a pergunta pela contagem, o empate em `Games - 1` abriria tie-break em
        // toda soma ímpar.
        Assert.False(TieBreakDoJogo.EmAndamento(Soma(9, 7), 8, 8));
    }

    [Fact]
    public void Jogo_de_um_game_nao_abre_tie_break_em_0x0()
    {
        // `Games - 1` é zero aqui: sem a trava, o placar inicial de um jogo até 1 já nasceria
        // em tie-break.
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(1, 7), 0, 0));
    }

    // ── O TIE-BREAK TAMBÉM SE VENCE POR DOIS ─────────────────────────────────────────────
    // Decisão do Felipe (12/09/2026): alcançar o alvo não basta — precisa de 2 pontos de
    // frente. É a regra da quadra, e é o que faz o alvo NÃO ser um teto seco: 7-6 continua.

    [Theory]
    [InlineData(7, 7, 5, true)]
    [InlineData(7, 5, 7, true)]
    [InlineData(7, 7, 6, false)]   // alcançou o alvo, mas por um ponto só: segue jogando
    [InlineData(7, 8, 6, true)]
    [InlineData(7, 8, 7, false)]
    [InlineData(7, 9, 7, true)]
    [InlineData(7, 6, 4, false)]   // nem chegou no alvo
    [InlineData(7, 0, 0, false)]
    [InlineData(10, 10, 8, true)]
    [InlineData(10, 10, 9, false)]
    [InlineData(10, 12, 10, true)]
    [InlineData(10, 7, 5, false)]  // o alvo de 10 não fecha em 7
    public void Fechar_exige_o_alvo_com_dois_pontos_de_frente(int alvo, int p1, int p2, bool pode)
    {
        Assert.Equal(pode, TieBreakDoJogo.PodeFechar(Ate(9, alvo), p1, p2));
    }

    [Fact]
    public void Tie_break_desligado_nunca_fecha()
    {
        Assert.False(TieBreakDoJogo.PodeFechar(Ate(9, TieBreakDoJogo.Desligado), 7, 5));
    }

    // ── O QUE O FECHAMENTO ESCREVE NO PLACAR ─────────────────────────────────────────────

    [Fact]
    public void Quem_fecha_o_tie_break_leva_o_9o_game()
    {
        Assert.Equal((9, 8), TieBreakDoJogo.GamesAoFechar(Ate(9, 7), 7, 5));
        Assert.Equal((8, 9), TieBreakDoJogo.GamesAoFechar(Ate(9, 7), 5, 7));
    }

    [Fact]
    public void Sem_poder_fechar_nao_ha_placar_a_escrever()
    {
        // Nulo obriga quem chama a tratar o caso em vez de receber um 9x8 inventado — um
        // "fechar" em 7-6 escreveria o fim de um jogo que ainda está sendo jogado.
        Assert.Null(TieBreakDoJogo.GamesAoFechar(Ate(9, 7), 7, 6));
    }

    [Fact]
    public void O_placar_do_fechamento_acompanha_o_limite_da_fase()
    {
        // Num jogo até 5 (ímpar) o tie-break nasce em 4x4 e fecha em 5x4 — o 9 não está
        // cravado em lugar nenhum.
        Assert.True(TieBreakDoJogo.EmAndamento(Ate(5, 7), 4, 4));
        Assert.Equal((5, 4), TieBreakDoJogo.GamesAoFechar(Ate(5, 7), 7, 5));
    }

    // ── OS GAMES NÃO MUDAM DE REGRA ──────────────────────────────────────────────────────

    [Fact]
    public void O_8x8_continua_sendo_um_jogo_que_nao_da_pra_encerrar()
    {
        // ⚠️ O tie-break NÃO mexe na régua dos games: 8x8 segue empatado, e só o 9º game
        // (escrito pelo fechamento ou na mão) decide. Se isto mudar, `QuemVenceu`,
        // `ClassificacaoDeGrupos` e o Padelímetro mudam junto — e não é o que foi pedido.
        var formato = Ate(9, 7);
        Assert.False(FormatoDaPartida.PodeEncerrar(formato, 8, 8));
        Assert.True(FormatoDaPartida.PodeEncerrar(formato, 9, 8));
        Assert.Equal(9, FormatoDaPartida.TetoDeGames(9, 8, 8));
    }

    // ── PONTO GRAVADO: LIXO NÃO ENTRA ────────────────────────────────────────────────────

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(500, TieBreakDoJogo.TetoDosPontos)]
    public void Ponto_gravado_fica_entre_zero_e_o_teto(int recebido, int gravado)
    {
        Assert.Equal(gravado, TieBreakDoJogo.PontoValido(recebido));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(0, 0, false)]      // 0-0 é "não começou": não vale poluir o card com isso
    [InlineData(7, 5, true)]
    [InlineData(0, 1, true)]
    public void Houve_tie_break_neste_jogo(int? p1, int? p2, bool houve)
    {
        Assert.Equal(houve, TieBreakDoJogo.Houve(p1, p2));
    }

    // ── A CONFIGURAÇÃO É POR FASE, COMO SETS E GAMES JÁ SÃO ──────────────────────────────

    private static Torneio TorneioComTieBreak() => new()
    {
        Nome = "2ª Etapa",
        SetsFaseGrupos = 1, GamesFaseGrupos = 9,
        SetsFaseMataMata = 1, GamesFaseMataMata = 9,
        SetsFaseFinal = 1, GamesFaseFinal = 9,
        PontosTieBreakGrupos = 7,
        PontosTieBreakMataMata = 7,
        PontosTieBreakFinal = 10,     // super tie-break na decisão
    };

    [Theory]
    [InlineData("Grupo A", 7)]
    [InlineData("Fase de Grupos", 7)]
    [InlineData("Americano - Rodada 1", 7)]
    [InlineData("Primeira Rodada", 7)]
    [InlineData("Quartas de Final", 7)]
    [InlineData("Semifinal", 10)]
    [InlineData("Final", 10)]
    public void Cada_fase_pega_o_tie_break_que_o_organizador_configurou(string fase, int pontos)
    {
        Assert.Equal(pontos, FormatoDaPartida.De(TorneioComTieBreak(), fase).PontosTieBreak);
    }

    [Fact]
    public void Torneio_que_nao_configurou_vem_desligado()
    {
        // ⚠️ É o caso de TODO torneio que já existe (a migration grava 0) — e também o do
        // torneio sem nada no ViewBag. Desligado significa o comportamento de sempre.
        Assert.Equal(TieBreakDoJogo.Desligado, FormatoDaPartida.De(new Torneio { Nome = "Antigo" }, "Grupo A").PontosTieBreak);
        Assert.Equal(TieBreakDoJogo.Desligado, FormatoDaPartida.De(null, "Final").PontosTieBreak);
    }

    [Fact]
    public void Numero_negativo_gravado_na_coluna_nao_liga_tie_break()
    {
        var torneio = TorneioComTieBreak();
        torneio.PontosTieBreakGrupos = -7;

        Assert.Equal(TieBreakDoJogo.Desligado, FormatoDaPartida.De(torneio, "Grupo A").PontosTieBreak);
        Assert.False(TieBreakDoJogo.EmAndamento(FormatoDaPartida.De(torneio, "Grupo A"), 8, 8));
    }

    // ── A ARMADILHA DA FASE DE NÚMERO PAR ────────────────────────────────────────────────

    [Fact]
    public void A_fase_comporta_tie_break_e_pergunta_separada_de_estar_configurado()
    {
        // É a pergunta da TELA DE CONFIGURAÇÃO, e ela é diferente do `PodeAcontecer`: ali o
        // organizador ainda não escolheu o alvo (ou acabou de escolher "desligado"), e mesmo
        // assim precisa ser avisado de que aquela fase nunca terá tie-break. Sem separar as
        // duas, o aviso só apareceria depois de configurar — tarde.
        Assert.True(Padelizou.Services.TieBreakDoJogo.AFaseComporta(Ate(9, TieBreakDoJogo.Desligado)));
        Assert.False(Padelizou.Services.TieBreakDoJogo.AFaseComporta(Ate(6, TieBreakDoJogo.Desligado)));
        Assert.False(Padelizou.Services.TieBreakDoJogo.AFaseComporta(Soma(9, TieBreakDoJogo.Desligado)));
        Assert.False(Padelizou.Services.TieBreakDoJogo.AFaseComporta(Ate(1, 7)));
    }

    [Fact]
    public void Tie_break_configurado_em_fase_de_numero_PAR_e_inerte_e_a_regua_sabe_dizer()
    {
        // O padrão da final é 6 games. Configurar "até 10 pontos" ali não faz nada, porque o
        // 5x5 vai pro 7º game — e o organizador precisa ser avisado na tela, senão ele
        // configura, confia e descobre na quadra.
        Assert.False(TieBreakDoJogo.PodeAcontecer(Ate(6, 10)));
        Assert.True(TieBreakDoJogo.PodeAcontecer(Ate(9, 7)));
        Assert.False(TieBreakDoJogo.PodeAcontecer(Ate(9, TieBreakDoJogo.Desligado)));
        Assert.False(TieBreakDoJogo.PodeAcontecer(Soma(9, 7)));
    }
}
