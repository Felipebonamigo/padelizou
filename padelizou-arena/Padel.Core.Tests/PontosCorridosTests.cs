namespace Padel.Core.Tests;

// O using mora DENTRO do namespace, como no RodizioTests: um tipo de mesmo nome que outra tarefa
// ponha direto em Padel.Core não sequestra os nomes daqui.
using Padel.Core.Rodizio;

/// <summary>
/// O formato de pontos corridos (Americano e Mexicano) no motor: cada ponto vale 1, sem games nem
/// sets, e a partida acaba quando a SOMA chega ao total — 24 pode acabar 13-11 ou 12-12 (empate).
/// O saque roda a cada 4 pontos pelos quatro jogadores, na ordem do padel (casa, rivais, o outro da
/// casa, o outro dos rivais), e a caixa alterna a cada ponto. Só no jogo local: não vale online.
/// </summary>
/// <remarks>
/// As sequências esperadas (quem saca em cada ponto, a caixa, os pontos das duplas no rodízio) são
/// escritas ou recontadas AQUI, a partir dos eventos e da regra — não pedidas à implementação.
/// </remarks>
public class PontosCorridosTests
{
    private const float Passo = 1f / 120f;

    private static OpcoesDaPartida SoIA(uint semente, int pontosCorridos) =>
        new() { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, PontosCorridos = pontosCorridos };

    /// <summary>Uma partida inteira, com os eventos e, a cada saque preparado, o ponto (0 = o primeiro), quem saca e a caixa.</summary>
    private sealed class Jogada
    {
        public required Partida Partida { get; init; }
        public List<EventoDaPartida> Eventos { get; } = [];
        public List<(int Ponto, Jogador Sacador, Caixa Caixa)> Saques { get; } = [];
        public int Contar(TipoDeEventoDaPartida tipo) => Eventos.Count(e => e.Tipo == tipo);
    }

    private static Jogada Jogar(OpcoesDaPartida opcoes, float limiteDeSegundos = 3600)
    {
        var partida = new Partida(opcoes);
        var jogada = new Jogada { Partida = partida };
        // O construtor já preparou o primeiro saque (antes de dar pra assinar o evento): ele entra à mão.
        jogada.Saques.Add((0, partida.Sacador, partida.CaixaDoSaque));
        int decididos = 0;
        partida.Evento += e =>
        {
            jogada.Eventos.Add(e);
            if (e.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
                decididos++;
            if (e.Tipo == TipoDeEventoDaPartida.SaquePreparado && e.Jogador is Jogador sacador)
                jogada.Saques.Add((decididos, sacador, partida.CaixaDoSaque));
        };
        float t = 0;
        while (!partida.Acabou && t < limiteDeSegundos)
        {
            partida.Avancar(Passo);
            t += Passo;
        }
        Assert.True(partida.Acabou, $"a partida de {opcoes.PontosCorridos} pontos corridos não acabou em {limiteDeSegundos} s simulados ({partida.Placar.Resumo()})");
        return jogada;
    }

    /// <summary>
    /// A regra escolhida, escrita à mão: turnos de 4 pontos; o 1º turno é do time que abre, jogador 0; depois o outro
    /// time; dentro do time os jogadores alternam a cada vez que o time volta a sacar. (time, jogador) do ponto n (0 = o 1º).
    /// </summary>
    private static (int Time, int Jogador) QuemSacaNoPonto(int n, int timeQueAbre)
    {
        int turno = n / 4;
        int time = turno % 2 == 0 ? timeQueAbre : 1 - timeQueAbre;
        int jogador = (turno / 2) % 2;
        return (time, jogador);
    }

    // ── O placar ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cada_ponto_vale_1_e_o_texto_mostra_o_numero_sem_15_30_40_nem_vantagem()
    {
        var p = Placar.DePontosCorridos(24);
        Assert.Equal(24, p.PontosCorridos);
        Assert.Equal("0", p.TextoDosPontos(0));
        p.PontoPara(0);
        Assert.Equal("1", p.TextoDosPontos(0));
        p.PontoPara(0); p.PontoPara(0);
        Assert.Equal("3", p.TextoDosPontos(0));
        p.PontoPara(1); p.PontoPara(1); p.PontoPara(1);
        // 3-3: no padel seria 40-40 com ponto de ouro. Aqui não existe ponto decisivo.
        Assert.Equal("3", p.TextoDosPontos(1));
        Assert.False(p.EmPontoDecisivo);
        Assert.False(p.PontoDeOuro);
        p.PontoPara(0);
        Assert.Equal("4", p.TextoDosPontos(0));   // nada de "AD"
        Assert.Equal("3", p.TextoDosPontos(1));
        for (int i = 0; i < 8; i++) p.PontoPara(0);
        Assert.Equal("12", p.TextoDosPontos(0));
        Assert.Equal([12, 3], p.Pontos);
        Assert.Equal("12-3", p.Resumo());
    }

    [Fact]
    public void Acaba_quando_a_soma_chega_ao_total_e_vence_quem_tem_mais_mesmo_que_o_ultimo_ponto_seja_do_outro()
    {
        var p = Placar.DePontosCorridos(24);
        for (int i = 0; i < 13; i++) Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(0).Tipo);
        for (int i = 0; i < 10; i++) Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(1).Tipo);
        Assert.False(p.Acabou);   // 13-10: 23 pontos, ainda falta um — 13 não "fecha" nada
        Assert.Null(p.Vencedor);

        var fim = p.PontoPara(1);   // 13-11: o último ponto é dos rivais, a partida é da casa
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, 0), fim);
        Assert.True(p.Acabou);
        Assert.False(p.Empate);
        Assert.Equal(0, p.Vencedor);
        Assert.Equal([13, 11], p.Pontos);   // o placar final fica: é ele que vai pro rodízio
        Assert.Equal("13-11", p.Resumo());

        // Depois do fim, ponto não conta.
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Encerrada, 0), p.PontoPara(1));
        Assert.Equal([13, 11], p.Pontos);
    }

    [Fact]
    public void Doze_a_doze_em_24_e_empate_com_Vencedor_null_e_a_partida_acabada()
    {
        var p = Placar.DePontosCorridos(24);
        EventoDoPlacar ultimo = default;
        for (int i = 0; i < 24; i++) ultimo = p.PontoPara(i % 2);
        Assert.Equal([12, 12], p.Pontos);
        Assert.True(p.Acabou);
        Assert.True(p.Empate);
        Assert.Null(p.Vencedor);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, -1), ultimo);   // -1: ninguém venceu
        Assert.Equal("12-12", p.Resumo());

        // Encerrada depois do empate não quebra (antes, Vencedor!.Value lançaria) e não mexe no placar.
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Encerrada, -1), p.PontoPara(0));
        Assert.Equal([12, 12], p.Pontos);
        Assert.True(p.Empate);
    }

    [Fact]
    public void Sem_pontos_corridos_nao_ha_empate_e_o_placar_segue_o_padel()
    {
        var p = new Placar();
        Assert.Null(p.PontosCorridos);
        Assert.False(p.Empate);
        for (int i = 0; i < 24; i++) p.PontoPara(0);   // 6-0
        Assert.True(p.Acabou);
        Assert.False(p.Empate);
        Assert.Equal(0, p.Vencedor);
        Assert.Equal("6-0", p.Resumo());
    }

    [Fact]
    public void Nenhum_game_set_tie_break_ou_ponto_de_ouro_aparece_em_pontos_corridos()
    {
        // Sequências que no padel dariam game (4-0), ponto de ouro (3-3), vantagem e tie-break (6-6 em games).
        int[][] sequencias =
        [
            [.. Enumerable.Repeat(0, 24)],
            [.. Enumerable.Range(0, 24).Select(i => (i / 4) % 2)],
            [.. Enumerable.Range(0, 24).Select(i => i % 2)],
            [0, 0, 0, 1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 1],
        ];
        foreach (var sequencia in sequencias)
        {
            var p = Placar.DePontosCorridos(24);
            var tipos = new List<TipoDeEventoDoPlacar>();
            foreach (int time in sequencia)
            {
                Assert.False(p.EmPontoDecisivo);
                tipos.Add(p.PontoPara(time).Tipo);
                Assert.False(p.EmTieBreak);
                Assert.Equal([0, 0], p.Games);
                Assert.Equal([0, 0], p.Sets);
                Assert.Empty(p.SetsAnteriores);
            }
            Assert.Equal(Enumerable.Repeat(TipoDeEventoDoPlacar.Ponto, 23).Append(TipoDeEventoDoPlacar.Partida), tipos);
            Assert.Equal(sequencia.Count(t => t == 0), p.Pontos[0]);
            Assert.Equal(sequencia.Count(t => t == 1), p.Pontos[1]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void O_saque_troca_a_cada_4_pontos_pelos_quatro_jogadores_e_a_caixa_alterna_a_cada_ponto(int timeQueAbre)
    {
        // Quem ganha o ponto não muda nada no saque: uma sequência torta de vencedores.
        int[] vencedores = [1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1];
        var p = Placar.DePontosCorridos(24, timeQueAbre);
        var sacaram = new HashSet<(int, int)>();
        for (int n = 0; n < 24; n++)
        {
            var (time, jogador) = QuemSacaNoPonto(n, timeQueAbre);
            Assert.Equal(new Sacador(time, jogador), p.Sacador);
            Assert.Equal((n / 4) % 2 == 0 ? timeQueAbre : 1 - timeQueAbre, p.SacadorDoGame);   // o time dono do turno de 4
            // Cada turno de saque abre pela direita (a caixa do "iguais"), e alterna a cada ponto.
            Assert.Equal(n % 2 == 0 ? LadoDoSaque.Direita : LadoDoSaque.Esquerda, p.LadoDoSaque);
            sacaram.Add((time, jogador));
            p.PontoPara(vencedores[n]);
        }
        Assert.True(p.Acabou);
        Assert.Equal(4, sacaram.Count);
        // O primeiro ciclo, explícito (a regra de clube: 4 seguidos de cada um, as duplas alternando).
        var p2 = Placar.DePontosCorridos(24, timeQueAbre);
        var sequencia = new List<Sacador>();
        for (int n = 0; n < 16; n++) { sequencia.Add(p2.Sacador); p2.PontoPara(0); }
        int a = timeQueAbre, b = 1 - timeQueAbre;
        Sacador[] esperado =
        [
            new(a, 0), new(a, 0), new(a, 0), new(a, 0),
            new(b, 0), new(b, 0), new(b, 0), new(b, 0),
            new(a, 1), new(a, 1), new(a, 1), new(a, 1),
            new(b, 1), new(b, 1), new(b, 1), new(b, 1),
        ];
        Assert.Equal(esperado, sequencia);
    }

    /// <summary>
    /// Turnos de 4: a vantagem do saque se anula entre as DUPLAS em todo total múltiplo de 8 (16, 24, 32). Entre os
    /// jogadores, só em múltiplo de 16: em 24 são 6 turnos, e quem abre cada dupla saca dois (8 pontos) contra um (4) do
    /// parceiro — como no Americano de clube. O placar do rodízio é da dupla, então é a conta da dupla que decide a justiça.
    /// </summary>
    [Theory]
    [InlineData(16, 8, new[] { 4, 4, 4, 4 })]
    [InlineData(24, 12, new[] { 8, 8, 4, 4 })]
    [InlineData(32, 16, new[] { 8, 8, 8, 8 })]
    public void Cada_dupla_saca_metade_dos_pontos_nos_totais_de_clube(int total, int porDupla, int[] porJogadorEsperado)
    {
        var p = Placar.DePontosCorridos(total);
        var porJogador = new Dictionary<Sacador, int>();
        for (int n = 0; n < total; n++)
        {
            porJogador[p.Sacador] = porJogador.GetValueOrDefault(p.Sacador) + 1;
            p.PontoPara(n % 3 == 0 ? 1 : 0);
        }
        Assert.True(p.Acabou);
        Assert.Equal(porDupla, porJogador.Where(s => s.Key.Time == 0).Sum(s => s.Value));
        Assert.Equal(porDupla, porJogador.Where(s => s.Key.Time == 1).Sum(s => s.Value));
        // Ordem: jogador 0 da casa, jogador 0 dos rivais, jogador 1 da casa, jogador 1 dos rivais.
        int[] porJogadorContado = [.. new[] { new Sacador(0, 0), new Sacador(1, 0), new Sacador(0, 1), new Sacador(1, 1) }.Select(s => porJogador.GetValueOrDefault(s))];
        Assert.Equal(porJogadorEsperado, porJogadorContado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-24)]
    public void Total_sem_sentido_e_recusado(int total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Placar.DePontosCorridos(total));
        Assert.ThrowsAny<ArgumentException>(() => new Partida(SoIA(1, total)));
    }

    // ── A partida ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pontos_corridos_nao_valem_online_e_a_partida_normal_vale()
    {
        Assert.Null(new OpcoesDaPartida().PontosCorridos);
        Assert.True(new OpcoesDaPartida().ValeOnline);
        Assert.False(new OpcoesDaPartida { PontosCorridos = 24 }.ValeOnline);
        Assert.False(new OpcoesDaPartida { PontosCorridos = 32, SetsParaVencer = 2 }.ValeOnline);
        Assert.Null(new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = 1 }).Placar.PontosCorridos);
    }

    [Theory]
    [InlineData(42u)]
    [InlineData(7u)]
    public void Uma_partida_de_IA_de_24_pontos_corridos_acaba_com_exatamente_24_pontos_somados(uint semente)
    {
        var jogada = Jogar(SoIA(semente, 24));
        var partida = jogada.Partida;
        var placar = partida.Placar;
        Assert.Equal(24, placar.Pontos[0] + placar.Pontos[1]);
        Assert.Equal(24, partida.Estatisticas.Pontos);
        Assert.Equal(23, jogada.Contar(TipoDeEventoDaPartida.Ponto));
        Assert.Equal(1, jogada.Contar(TipoDeEventoDaPartida.Partida));
        Assert.Equal(1, jogada.Contar(TipoDeEventoDaPartida.Fim));
        // Nenhum game nem set aparece.
        Assert.Equal(0, jogada.Contar(TipoDeEventoDaPartida.Game));
        Assert.Equal(0, jogada.Contar(TipoDeEventoDaPartida.Set));
        Assert.Equal([0, 0], placar.Games);
        Assert.Equal([0, 0], placar.Sets);
        Assert.Empty(placar.SetsAnteriores);
        // O fim diz quem venceu (ou -1 no empate), igual ao placar.
        int? esperado = placar.Pontos[0] > placar.Pontos[1] ? 0 : placar.Pontos[1] > placar.Pontos[0] ? 1 : null;
        Assert.Equal(esperado, placar.Vencedor);
        Assert.Equal(esperado is null, placar.Empate);
        Assert.Equal(esperado ?? -1, jogada.Eventos.Single(e => e.Tipo == TipoDeEventoDaPartida.Fim).Time);
        // Os pontos que os eventos dão a cada time batem com o placar.
        var decididos = jogada.Eventos.Where(e => e.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Partida).ToList();
        Assert.Equal(placar.Pontos[0], decididos.Count(e => e.Time == 0));
        Assert.Equal(placar.Pontos[1], decididos.Count(e => e.Time == 1));
        Assert.Contains(placar.Resumo(), partida.Mensagem?.Texto ?? "");
    }

    [Fact]
    public void A_mensagem_final_diz_quem_venceu_mesmo_quando_o_ultimo_ponto_e_de_quem_perdeu()
    {
        // 13-10 → 13-11: o último ponto é dos rivais, mas a casa venceu. Procura esse caso entre as sementes (a IA muda,
        // a semente exata não importa) e confere a mensagem pelo Vencedor em todas as partidas jogadas.
        int casosDoUltimoPontoDoPerdedor = 0;
        for (uint semente = 1; semente <= 60 && casosDoUltimoPontoDoPerdedor < 2; semente++)
        {
            var jogada = Jogar(SoIA(semente, pontosCorridos: 24));
            var placar = jogada.Partida.Placar;
            string texto = jogada.Partida.Mensagem?.Texto ?? "";
            string esperado = placar.Vencedor switch { 0 => "A casa venceu!", 1 => "Os rivais venceram.", _ => "Empate!" };
            Assert.StartsWith(esperado, texto);
            int ultimoPonto = jogada.Eventos.Last(e => e.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Partida).Time;
            if (placar.Vencedor is int vencedor && ultimoPonto != vencedor) casosDoUltimoPontoDoPerdedor++;
        }
        Assert.True(casosDoUltimoPontoDoPerdedor >= 1, "nenhuma partida acabou com o último ponto de quem perdeu: o caso que o teste trava não foi exercitado");
    }

    [Fact]
    public void Na_partida_o_sacador_troca_a_cada_4_pontos_todos_os_4_sacam_e_a_caixa_alterna()
    {
        var jogada = Jogar(SoIA(42, 24));
        var saques = jogada.Saques;
        Assert.Equal(Enumerable.Range(0, 24), saques.Select(s => s.Ponto).Distinct());   // todo ponto teve saque (falta e let repetem o ponto)
        foreach (var (ponto, sacador, caixa) in saques)
        {
            var (time, jogador) = QuemSacaNoPonto(ponto, timeQueAbre: 0);
            Assert.True(sacador.Time == time && sacador.Indice == jogador,
                $"ponto {ponto + 1}: sacou {sacador.Nome} (time {sacador.Time}, jogador {sacador.Indice}); a regra dá time {time}, jogador {jogador}");
            bool direita = ponto % 2 == 0;
            Assert.Equal(Quadra.CaixaDeSaque(-sacador.Lado, direita), caixa);
        }
        Assert.Equal(4, saques.Select(s => s.Sacador).Distinct().Count());
    }

    [Fact]
    public void Uma_partida_que_acaba_empatada_chega_ao_fim_sem_vencedor_e_diz_empate()
    {
        // Com 4 pontos, 2-2 sai em ~3 de cada 8 partidas. A semente não fica fixa no teste: procura a primeira que empata,
        // pra o teste sobreviver a qualquer mudança de física ou de IA.
        for (uint semente = 1; semente <= 40; semente++)
        {
            var jogada = Jogar(SoIA(semente, 4));
            var placar = jogada.Partida.Placar;
            if (placar.Pontos[0] != placar.Pontos[1]) continue;

            Assert.Equal([2, 2], placar.Pontos);
            Assert.True(placar.Empate);
            Assert.Null(placar.Vencedor);
            Assert.True(jogada.Partida.Acabou);
            Assert.Equal(EstadoDaPartida.Fim, jogada.Partida.Estado);
            Assert.Equal(-1, jogada.Eventos.Single(e => e.Tipo == TipoDeEventoDaPartida.Fim).Time);
            Assert.Equal(1, jogada.Contar(TipoDeEventoDaPartida.Partida));
            string texto = jogada.Partida.Mensagem?.Texto ?? "";
            Assert.Contains("Empate", texto);
            Assert.Contains("2-2", texto);
            Assert.DoesNotContain("venceu", texto);
            Assert.DoesNotContain("venceram", texto);
            return;
        }
        Assert.Fail("nenhuma das 40 sementes empatou uma partida de 4 pontos corridos");
    }

    // ── O rodízio ────────────────────────────────────────────────────────────────────────

    private static TorneioDeRodizio RodizioComUmHumano(FormatoDoRodizio formato = FormatoDoRodizio.Americano) =>
        new(
        [
            new JogadorDoRodizio("Eu", 60, humano: true),
            new JogadorDoRodizio("Ana", 55),
            new JogadorDoRodizio("Bia", 65),
            new JogadorDoRodizio("Caio", 50),
        ], new RegrasDoRodizio(formato, PontosPorJogo: 24), semente: 2026);

    [Theory]
    [InlineData(FormatoDoRodizio.Americano)]
    [InlineData(FormatoDoRodizio.Mexicano)]
    public void O_resultado_de_uma_partida_de_verdade_entra_no_rodizio_e_a_classificacao_soma(FormatoDoRodizio formato)
    {
        var rodizio = RodizioComUmHumano(formato);
        var jogo = Assert.Single(rodizio.JogosPendentes);
        // A dupla do humano joga em casa (time 0); ela pode ser a DuplaA ou a DuplaB do rodízio.
        int timeDaDuplaA = jogo.DuplaA.Tem("Eu") ? 0 : 1;
        var partida = Jogar(new OpcoesDaPartida
        {
            Humanos = OpcoesDaPartida.NinguemHumano,
            Semente = 42,
            PontosCorridos = rodizio.Regras.PontosPorJogo,
        }).Partida;
        int daCasa = partida.Placar.Pontos[0], dosRivais = partida.Placar.Pontos[1];

        rodizio.InformarResultado(jogo.Numero, partida.Placar, timeDaDuplaA);

        var gravado = rodizio.Jogos.Single(j => j.Numero == jogo.Numero);
        int pontosA = timeDaDuplaA == 0 ? daCasa : dosRivais, pontosB = 24 - pontosA;
        Assert.Equal(pontosA, gravado.PontosA);
        Assert.Equal(pontosB, gravado.PontosB);
        Assert.Empty(rodizio.JogosPendentes);

        // A classificação, recontada daqui: cada jogador soma os pontos da dupla dele (com 4, ninguém folga).
        var linhas = rodizio.Classificacao().ToDictionary(l => l.Jogador);
        foreach (var (dupla, pro, contra) in new[] { (jogo.DuplaA, pontosA, pontosB), (jogo.DuplaB, pontosB, pontosA) })
            foreach (var nome in new[] { dupla.Jogador1, dupla.Jogador2 })
            {
                var linha = linhas[nome];
                Assert.Equal(pro, linha.Pontos);
                Assert.Equal(pro, linha.PontosPro);
                Assert.Equal(contra, linha.PontosContra);
                Assert.Equal(1, linha.Jogos);
                Assert.Equal(pro > contra ? 1 : 0, linha.Vitorias);
                Assert.Equal(pro == contra ? 1 : 0, linha.Empates);
                Assert.Equal(pro < contra ? 1 : 0, linha.Derrotas);
            }
        Assert.True(rodizio.AvancarRodada(), "com o resultado do humano, a rodada fecha e a próxima abre");
    }

    [Fact]
    public void Sem_o_numero_o_placar_vai_pro_unico_jogo_pendente_com_a_dupla_do_humano_como_visitante()
    {
        var rodizio = RodizioComUmHumano();
        var jogo = Assert.Single(rodizio.JogosPendentes);
        var placar = Placar.DePontosCorridos(24);
        for (int i = 0; i < 24; i++) placar.PontoPara(i < 15 ? 1 : 0);   // 9-15: os visitantes venceram

        rodizio.InformarResultado(placar, timeDaDuplaA: 1);

        var gravado = rodizio.Jogos.Single(j => j.Numero == jogo.Numero);
        Assert.Equal(15, gravado.PontosA);   // a DuplaA jogou como visitante (time 1)
        Assert.Equal(9, gravado.PontosB);
    }

    [Fact]
    public void O_empate_da_partida_entra_no_rodizio_como_empate()
    {
        var rodizio = RodizioComUmHumano();
        var jogo = Assert.Single(rodizio.JogosPendentes);
        var placar = Placar.DePontosCorridos(24);
        for (int i = 0; i < 24; i++) placar.PontoPara(i % 2);

        rodizio.InformarResultado(jogo.Numero, placar, timeDaDuplaA: 0);

        var linhas = rodizio.Classificacao();
        Assert.All(linhas, l => Assert.Equal((12, 1, 0), (l.Pontos, l.Empates, l.Vitorias)));
    }

    [Fact]
    public void O_rodizio_recusa_placar_que_nao_serve_e_o_jogo_continua_pendente()
    {
        var rodizio = RodizioComUmHumano();
        var jogo = Assert.Single(rodizio.JogosPendentes);

        // Partida de games e sets (6-0), acabada: não é de pontos corridos.
        var normal = new Placar();
        for (int i = 0; i < 24; i++) normal.PontoPara(0);
        Assert.True(normal.Acabou);
        Assert.Throws<ArgumentException>(() => rodizio.InformarResultado(jogo.Numero, normal, timeDaDuplaA: 0));

        // Pontos corridos, acabada, mas de outro total (9-7 em 16; o rodízio joga 24).
        var de16 = Placar.DePontosCorridos(16);
        for (int i = 0; i < 16; i++) de16.PontoPara(i < 9 ? 0 : 1);
        Assert.True(de16.Acabou);
        var erro = Assert.Throws<ArgumentException>(() => rodizio.InformarResultado(jogo.Numero, de16, timeDaDuplaA: 0));
        Assert.Contains("16", erro.Message);
        Assert.Contains("24", erro.Message);

        // Do total certo, mas pela metade: 13-10 soma 23.
        var pelaMetade = Placar.DePontosCorridos(24);
        for (int i = 0; i < 23; i++) pelaMetade.PontoPara(i < 13 ? 0 : 1);
        Assert.False(pelaMetade.Acabou);
        Assert.Throws<ArgumentException>(() => rodizio.InformarResultado(jogo.Numero, pelaMetade, timeDaDuplaA: 0));

        // Time da dupla A que não existe.
        var certo = Placar.DePontosCorridos(24);
        for (int i = 0; i < 24; i++) certo.PontoPara(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => rodizio.InformarResultado(jogo.Numero, certo, timeDaDuplaA: 2));

        // Nada disso gravou meio resultado.
        Assert.False(rodizio.Jogos.Single(j => j.Numero == jogo.Numero).Jogado);
        Assert.Single(rodizio.JogosPendentes);

        rodizio.InformarResultado(jogo.Numero, certo, timeDaDuplaA: 0);
        Assert.Equal((24, 0), (rodizio.Jogos.Single(j => j.Numero == jogo.Numero).PontosA, rodizio.Jogos.Single(j => j.Numero == jogo.Numero).PontosB));
    }
}
