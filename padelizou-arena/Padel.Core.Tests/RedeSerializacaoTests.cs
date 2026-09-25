using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// O formato binário do online: ida e volta sem perder nada além da quantização, pacote estragado nunca
/// derruba ninguém (devolve falso), e o instantâneo típico cabe em 300 bytes.
/// </summary>
public class RedeSerializacaoTests
{
    /// <summary>
    /// Instantâneos tirados de uma partida real (IA x IA, com semente), com os eventos que ela emitiu nos
    /// últimos 0,5 s e a bola logo depois de cada golpe como descontinuidade — os mesmos dados que o host manda.
    /// </summary>
    private static List<(Instantaneo instantaneo, Partida partida)> InstantaneosDeUmaPartidaReal(uint semente, int aCadaTicks = 37, float segundos = 400)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente });
        var eventos = new List<EventoNumerado>();
        var descontinuidades = new List<Descontinuidade>();
        uint tick = 0, id = 0;
        partida.Evento += e =>
        {
            eventos.Add(EventoNumerado.De(e, ++id, tick + 1));
            if (e.Tipo == TipoDeEventoDaPartida.Golpe) descontinuidades.Add(new Descontinuidade(tick + 1, EstadoDaBola.De(partida.Bola)));
        };
        var capturas = new List<(Instantaneo, Partida)>();
        int total = (int)(segundos * Protocolo.TicksPorSegundo);
        for (int i = 0; i < total && !partida.Acabou; i++)
        {
            partida.Avancar(Protocolo.Passo);
            tick++;
            if (tick % aCadaTicks != 0) continue;
            var inst = Instantaneo.Capturar(partida, tick);
            inst.Eventos.AddRange(eventos.Where(e => e.Tick + Protocolo.TicksDeRepeticaoDeEventos > tick));
            inst.Descontinuidades.AddRange(descontinuidades.Where(d => d.Tick + Protocolo.TicksDeRepeticaoDeEventos > tick).TakeLast(Protocolo.MaximoDeDescontinuidades));
            inst.Confirmacoes.Add(new Confirmacao(2, tick * 3 + 17));
            inst.Confirmacoes.Add(new Confirmacao(1, tick + 5));
            capturas.Add((inst, partida));
        }
        return capturas;
    }

    private static void AssertPerto(float esperado, float obtido, float tolerancia, string campo) =>
        Assert.True(MathF.Abs(esperado - obtido) <= tolerancia, $"{campo}: esperado {esperado}, veio {obtido} (tolerância {tolerancia})");

    private static void AssertBolaPerto(EstadoDaBola a, EstadoDaBola b)
    {
        AssertPerto(a.X, b.X, 0.001f, "bola.X"); AssertPerto(a.Y, b.Y, 0.001f, "bola.Y"); AssertPerto(a.Z, b.Z, 0.001f, "bola.Z");
        AssertPerto(a.Vx, b.Vx, 0.01f, "bola.Vx"); AssertPerto(a.Vy, b.Vy, 0.01f, "bola.Vy"); AssertPerto(a.Vz, b.Vz, 0.01f, "bola.Vz");
        AssertPerto(a.Wx, b.Wx, 0.05f, "bola.Wx"); AssertPerto(a.Wy, b.Wy, 0.05f, "bola.Wy"); AssertPerto(a.Wz, b.Wz, 0.05f, "bola.Wz");
        Assert.Equal(a.EmJogo, b.EmJogo); Assert.Equal(a.Rolando, b.Rolando); Assert.Equal(a.Parada, b.Parada);
    }

    [Fact]
    public void O_instantaneo_de_uma_partida_real_vai_e_volta_so_com_a_perda_da_quantizacao()
    {
        var capturas = InstantaneosDeUmaPartidaReal(42);
        Assert.True(capturas.Count > 200, $"poucas capturas: {capturas.Count}");
        Assert.Contains(capturas, c => c.instantaneo.Eventos.Count > 0);
        Assert.Contains(capturas, c => c.instantaneo.Descontinuidades.Count > 0);
        Assert.Contains(capturas, c => c.instantaneo.Mensagem is not null);
        Assert.Contains(capturas, c => c.instantaneo.Estado == EstadoDaPartida.Rally);

        foreach (var (original, _) in capturas)
        {
            byte[] bytes = Protocolo.EscreverInstantaneo(original);
            Assert.True(Protocolo.TentarLerInstantaneo(bytes, out var lido), $"não leu o instantâneo do tick {original.Tick}");
            Assert.Equal(original.Tick, lido.Tick);
            Assert.Equal(original.Estado, lido.Estado);
            AssertPerto(original.Temporizador, lido.Temporizador, 0.001f, "Temporizador");
            AssertBolaPerto(original.Bola, lido.Bola);
            for (int i = 0; i < 4; i++)
            {
                var a = original.Jogadores[i];
                var b = lido.Jogadores[i];
                AssertPerto(a.X, b.X, 0.001f, $"j{i}.X"); AssertPerto(a.Y, b.Y, 0.001f, $"j{i}.Y");
                AssertPerto(a.Vx, b.Vx, 0.01f, $"j{i}.Vx"); AssertPerto(a.Vy, b.Vy, 0.01f, $"j{i}.Vy");
                AssertPerto(a.Balanco, b.Balanco, 0.004f, $"j{i}.Balanco");
                AssertPerto(a.TempoNoBalanco, b.TempoNoBalanco, 0.004f, $"j{i}.TempoNoBalanco");
                AssertPerto(a.Cooldown, b.Cooldown, 0.004f, $"j{i}.Cooldown");
                Assert.Equal(a.BalancoDeLob, b.BalancoDeLob);
                Assert.Equal(a.Humano, b.Humano);
            }
            Assert.Equal(original.Placar, lido.Placar);
            Assert.Equal(original.Mensagem, lido.Mensagem);
            AssertPerto(original.CaixaDoSaque.XMin, lido.CaixaDoSaque.XMin, 0.01f, "Caixa.XMin");
            AssertPerto(original.CaixaDoSaque.XMax, lido.CaixaDoSaque.XMax, 0.01f, "Caixa.XMax");
            AssertPerto(original.CaixaDoSaque.YMin, lido.CaixaDoSaque.YMin, 0.01f, "Caixa.YMin");
            AssertPerto(original.CaixaDoSaque.YMax, lido.CaixaDoSaque.YMax, 0.01f, "Caixa.YMax");
            Assert.Equal(original.Confirmacoes, lido.Confirmacoes);
            Assert.Equal(original.Eventos, lido.Eventos);
            Assert.Equal(original.Descontinuidades.Count, lido.Descontinuidades.Count);
            for (int i = 0; i < original.Descontinuidades.Count; i++)
            {
                Assert.Equal(original.Descontinuidades[i].Tick, lido.Descontinuidades[i].Tick);
                AssertBolaPerto(original.Descontinuidades[i].Bola, lido.Descontinuidades[i].Bola);
            }
            // O que foi lido, escrito de novo, dá os mesmos bytes: a quantização é idempotente.
            Assert.Equal(bytes, Protocolo.EscreverInstantaneo(lido));
        }
    }

    [Fact]
    public void O_instantaneo_tipico_de_uma_partida_real_tem_menos_de_300_bytes()
    {
        var tamanhos = InstantaneosDeUmaPartidaReal(7, aCadaTicks: 4).Select(c => Protocolo.EscreverInstantaneo(c.instantaneo).Length).OrderBy(t => t).ToList();
        double media = tamanhos.Average();
        int p95 = tamanhos[(int)(tamanhos.Count * 0.95)];
        Assert.True(media < 300, $"média de {media:F0} bytes");
        Assert.True(p95 < 300, $"p95 de {p95} bytes (máximo {tamanhos[^1]})");
    }

    [Fact]
    public void O_placar_do_instantaneo_mostra_o_mesmo_texto_que_o_placar_do_core()
    {
        // A visão do cliente não tem o Placar do Core, só o estado dele: o texto dos pontos e o resumo são
        // refeitos — e este teste prende a cópia ao original, ponto a ponto, com e sem ponto de ouro, até o tie-break.
        foreach (bool pontoDeOuro in new[] { true, false })
        {
            var placar = new Placar(pontoDeOuro, setsParaVencer: 3);
            var aleatorio = new Aleatorio(pontoDeOuro ? 11u : 12u);
            bool viuTieBreak = false, viuPontoDecisivo = false;
            while (!placar.Acabou)
            {
                // Quem tem menos games leva 70 % dos pontos: os sets ficam parelhos e o 6-6 (tie-break) aparece.
                int atras = placar.Games[0] <= placar.Games[1] ? 0 : 1;
                placar.PontoPara(aleatorio.Proximo() < 0.7f ? atras : 1 - atras);
                var estado = EstadoDoPlacar.De(placar);
                viuTieBreak |= estado.EmTieBreak;
                Assert.Equal(placar.TextoDosPontos(0), estado.TextoDosPontos(0));
                Assert.Equal(placar.TextoDosPontos(1), estado.TextoDosPontos(1));
                Assert.Equal(placar.Resumo(), estado.Resumo());
                Assert.Equal(placar.LadoDoSaque, estado.LadoDoSaque);
                Assert.Equal(placar.Sacador, estado.Sacador);
                Assert.Equal(placar.EmPontoDecisivo, estado.EmPontoDecisivo);
                viuPontoDecisivo |= estado.EmPontoDecisivo;

                var inst = new Instantaneo { Tick = 1, Placar = estado };
                Assert.True(Protocolo.TentarLerInstantaneo(Protocolo.EscreverInstantaneo(inst), out var lido));
                Assert.Equal(estado, lido.Placar);
            }
            Assert.True(viuTieBreak, "a sequência não passou por um tie-break");
            Assert.Equal(pontoDeOuro, viuPontoDecisivo);   // 40-40 com ponto de ouro é ponto decisivo; sem ele, nunca
            Assert.Contains(placar.SetsAnteriores, s => s.TieBreak is not null);
        }
    }

    [Fact]
    public void A_mensagem_com_acento_e_os_campos_de_destaque_vao_e_voltam()
    {
        var inst = new Instantaneo { Tick = 99, Mensagem = new Mensagem("Falta — saque fora da caixa. Segundo saque ção", Destaque: true, Suave: true) };
        Assert.True(Protocolo.TentarLerInstantaneo(Protocolo.EscreverInstantaneo(inst), out var lido));
        Assert.Equal(inst.Mensagem, lido.Mensagem);

        var semMensagem = new Instantaneo { Tick = 100 };
        Assert.True(Protocolo.TentarLerInstantaneo(Protocolo.EscreverInstantaneo(semMensagem), out var lido2));
        Assert.Null(lido2.Mensagem);
    }

    [Fact]
    public void As_entradas_vao_e_voltam_com_a_direcao_quantizada_e_os_contadores_que_dao_a_volta()
    {
        var entradas = new EntradaDeRede[8];
        for (int i = 0; i < 8; i++)
        {
            float dx = -1 + i * 0.28f, dy = 0.83f - i * 0.2f;
            entradas[i] = new EntradaDeRede((uint)(1000 + i), EntradaDeRede.Quantizar(dx), EntradaDeRede.Quantizar(dy), i % 2 == 0, (byte)(252 + i), (byte)(i / 3));
            AssertPerto(dx, entradas[i].DirecaoX, 1f / 127, "DirecaoX");
            AssertPerto(dy, entradas[i].DirecaoY, 1f / 127, "DirecaoY");
        }
        byte[] bytes = Protocolo.EscreverEntradas(entradas);
        Assert.True(Protocolo.TentarLerEntradas(bytes, out var lidas));
        Assert.Equal(entradas, lidas);
        Assert.Equal(1, EntradaDeRede.Quantizar(5f) / 127);   // satura em ±1
        Assert.Equal(-127, EntradaDeRede.Quantizar(float.NegativeInfinity));
        Assert.Equal(0, EntradaDeRede.Quantizar(float.NaN));
    }

    [Fact]
    public void As_mensagens_da_sala_vao_e_voltam()
    {
        Assert.True(Protocolo.TentarLerOla(Protocolo.EscreverOla(new Ola("Fê Bonamigo")), out var ola));
        Assert.Equal("Fê Bonamigo", ola.Nome);
        Assert.Equal(Protocolo.Versao, ola.Versao);

        var opcoes = new OpcoesDaPartida { Dificuldade = Dificuldade.Dificil, PontoDeOuro = false, SetsParaVencer = 2, ModoDeGolpe = ModoDeGolpe.Automatico, Semente = 12345 };
        Assert.True(Protocolo.TentarLerBemVindo(Protocolo.EscreverBemVindo(new BemVindo(2, opcoes, 12345)), out var bemVindo));
        Assert.Equal(2, bemVindo.Indice);
        Assert.Equal(12345u, bemVindo.Semente);
        Assert.Equal(Dificuldade.Dificil, bemVindo.Opcoes.Dificuldade);
        Assert.False(bemVindo.Opcoes.PontoDeOuro);
        Assert.Equal(2, bemVindo.Opcoes.SetsParaVencer);
        Assert.Equal(ModoDeGolpe.Automatico, bemVindo.Opcoes.ModoDeGolpe);
        Assert.Equal(12345u, bemVindo.Opcoes.Semente);

        Assert.True(Protocolo.TentarLerRecusado(Protocolo.EscreverRecusado(new Recusado(MotivoDaRecusa.SalaCheia)), out var recusado));
        Assert.Equal(MotivoDaRecusa.SalaCheia, recusado.Motivo);
        Assert.Equal(Protocolo.Versao, recusado.VersaoDoHost);

        Assert.True(Protocolo.TentarLerSala(Protocolo.EscreverSala(new Sala(["Host", "", "Rival", ""])), out var sala));
        Assert.Equal(["Host", "", "Rival", ""], sala.Nomes);

        Assert.True(Protocolo.TentarLerComecou(Protocolo.EscreverComecou(new Comecou([true, false, true, true], ["Host", "", "Ana", "Bia"])), out var comecou));
        Assert.Equal([true, false, true, true], comecou.Humanos);
        Assert.Equal(["Host", "", "Ana", "Bia"], comecou.Nomes);

        Assert.True(Protocolo.TentarLerTipo(Protocolo.EscreverComecou(new Comecou([true, false, false, false], ["H", "", "", ""])), out var tipo));
        Assert.Equal(TipoDeMensagem.Comecou, tipo);
    }

    [Fact]
    public void O_ola_e_o_recusado_de_outra_versao_ainda_sao_lidos_pra_recusa_ser_explicada()
    {
        // É o que permite o host dizer "versão incompatível" a um cliente velho, e o cliente velho entender.
        byte[] olaVelho = Protocolo.EscreverOla(new Ola("Velho", Versao: 99));
        Assert.True(Protocolo.TentarLerOla(olaVelho, out var ola));
        Assert.Equal(99, ola.Versao);
        Assert.True(Protocolo.TentarLerRecusado(Protocolo.EscreverRecusado(new Recusado(MotivoDaRecusa.VersaoIncompativel, VersaoDoHost: 7)), out var r));
        Assert.Equal(7, r.VersaoDoHost);
    }

    private static List<byte[]> PacotesDeExemplo()
    {
        var inst = InstantaneosDeUmaPartidaReal(3, aCadaTicks: 211, segundos: 60).Select(c => c.instantaneo).First(i => i.Eventos.Count > 0);
        return
        [
            Protocolo.EscreverInstantaneo(inst),
            Protocolo.EscreverEntradas([new EntradaDeRede(1, 10, -20, true, 1, 0), new EntradaDeRede(2, 12, -20, false, 1, 1)]),
            Protocolo.EscreverOla(new Ola("Ana")),
            Protocolo.EscreverBemVindo(new BemVindo(1, new OpcoesDaPartida { Semente = 9 }, 9)),
            Protocolo.EscreverRecusado(new Recusado(MotivoDaRecusa.PartidaEmAndamento)),
            Protocolo.EscreverSala(new Sala(["A", "B", "", ""])),
            Protocolo.EscreverComecou(new Comecou([true, true, false, false], ["A", "B", "", ""])),
        ];
    }

    private static bool LerQualquer(ReadOnlySpan<byte> p) =>
        Protocolo.TentarLerInstantaneo(p, out _) | Protocolo.TentarLerEntradas(p, out _) | Protocolo.TentarLerOla(p, out _)
        | Protocolo.TentarLerBemVindo(p, out _) | Protocolo.TentarLerRecusado(p, out _) | Protocolo.TentarLerSala(p, out _)
        | Protocolo.TentarLerComecou(p, out _) | Protocolo.TentarLerTipo(p, out _);

    [Fact]
    public void Pacote_truncado_devolve_falso_em_qualquer_tamanho()
    {
        foreach (var pacote in PacotesDeExemplo())
        {
            Assert.True(LerQualquer(pacote));
            for (int n = 0; n < pacote.Length; n++)
                Assert.False(LerQualquer(pacote.AsSpan(0, n)), $"leu um pacote truncado em {n} de {pacote.Length} bytes");
        }
    }

    [Fact]
    public void Pacote_com_qualquer_byte_corrompido_devolve_falso()
    {
        foreach (var pacote in PacotesDeExemplo())
        {
            for (int i = 0; i < pacote.Length; i++)
            {
                foreach (byte mascara in new byte[] { 0x01, 0x80, 0x5A, 0xFF })
                {
                    var estragado = (byte[])pacote.Clone();
                    estragado[i] ^= mascara;
                    Assert.False(LerQualquer(estragado), $"leu um pacote com o byte {i} corrompido (máscara {mascara:X2})");
                }
            }
            var comLixo = pacote.Append((byte)0).ToArray();
            Assert.False(LerQualquer(comLixo), "leu um pacote com um byte sobrando");
        }
    }

    [Fact]
    public void Lixo_com_cabecalho_e_crc_validos_nunca_lanca_excecao()
    {
        // O CRC barra a corrupção de transporte; isto aqui é o ataque ou o bug: conteúdo arbitrário com envelope válido.
        var aleatorio = new Aleatorio(2026);
        foreach (TipoDeMensagem tipo in Enum.GetValues<TipoDeMensagem>())
        {
            for (int n = 0; n < 400; n++)
            {
                int tamanho = (int)(aleatorio.Proximo() * 320);
                var corpo = new byte[tamanho];
                for (int i = 0; i < tamanho; i++) corpo[i] = (byte)(aleatorio.Proximo() * 256);
                byte[] pacote = Protocolo.Envelopar(tipo, corpo);
                var excecao = Record.Exception(() => LerQualquer(pacote));
                Assert.Null(excecao);
            }
        }
        var aoAcaso = new byte[64];
        for (int n = 0; n < 2000; n++)
        {
            for (int i = 0; i < aoAcaso.Length; i++) aoAcaso[i] = (byte)(aleatorio.Proximo() * 256);
            Assert.False(LerQualquer(aoAcaso.AsSpan(0, n % 64)));
        }
    }

    // ───── Validação dos leitores: envelope íntegro, corpo inválido ─────
    //
    // Os testes de truncado e de corrompido acima caem todos no CRC e nunca chegam ao leitor do corpo: as checagens
    // de faixa e de sobra podiam ser apagadas sem nenhum deles cair. Os daqui embrulham o corpo num envelope válido
    // (Protocolo.Envelopar), então quem tem de recusar é o leitor. E cada caso vem em par: o último valor válido tem
    // de ser ACEITO (prova que o corpo à mão está certo e que a recusa é por aquele campo) e o primeiro fora da
    // faixa, RECUSADO.

    private static bool LerDoTipo(TipoDeMensagem tipo, ReadOnlySpan<byte> p) => tipo switch
    {
        TipoDeMensagem.Ola => Protocolo.TentarLerOla(p, out _),
        TipoDeMensagem.BemVindo => Protocolo.TentarLerBemVindo(p, out _),
        TipoDeMensagem.Recusado => Protocolo.TentarLerRecusado(p, out _),
        TipoDeMensagem.Sala => Protocolo.TentarLerSala(p, out _),
        TipoDeMensagem.Comecou => Protocolo.TentarLerComecou(p, out _),
        TipoDeMensagem.Entradas => Protocolo.TentarLerEntradas(p, out _),
        TipoDeMensagem.Instantaneo => Protocolo.TentarLerInstantaneo(p, out _),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo sem leitor"),
    };

    [Fact]
    public void Com_envelope_integro_o_leitor_recusa_byte_sobrando_e_corpo_curto()
    {
        foreach (var pacote in PacotesDeExemplo())
        {
            var tipo = (TipoDeMensagem)pacote[2];
            byte versao = pacote[1];
            byte[] corpo = pacote[Protocolo.TamanhoDoCabecalho..^Protocolo.TamanhoDoCrc];
            Assert.True(LerDoTipo(tipo, Protocolo.Envelopar(tipo, corpo, versao)), $"{tipo}: o corpo reembrulhado não foi lido");
            Assert.False(LerDoTipo(tipo, Protocolo.Envelopar(tipo, [.. corpo, 0], versao)), $"{tipo}: leu o corpo com um byte sobrando");
            for (int n = 0; n < corpo.Length; n++)
                Assert.False(LerDoTipo(tipo, Protocolo.Envelopar(tipo, corpo.AsSpan(0, n), versao)), $"{tipo}: leu o corpo cortado em {n} de {corpo.Length} bytes");
        }
    }

    /// <summary>O pacote com outro byte mágico e o CRC refeito por cima: íntegro pro CRC, errado pra magia.</summary>
    private static byte[] ComMagicoECrcRefeitos(byte[] pacote, byte magico)
    {
        var copia = (byte[])pacote.Clone();
        copia[0] = magico;
        int semCrc = copia.Length - Protocolo.TamanhoDoCrc;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(copia.AsSpan(semCrc), Protocolo.Crc32(copia.AsSpan(0, semCrc)));
        return copia;
    }

    [Fact]
    public void Com_crc_valido_o_envelope_ainda_recusa_magia_errada_tipo_desconhecido_e_versao_diferente()
    {
        const byte OutraVersao = Protocolo.Versao + 1;
        foreach (var pacote in PacotesDeExemplo())
        {
            var tipo = (TipoDeMensagem)pacote[2];
            byte[] corpo = pacote[Protocolo.TamanhoDoCabecalho..^Protocolo.TamanhoDoCrc];

            Assert.True(LerDoTipo(tipo, ComMagicoECrcRefeitos(pacote, Protocolo.Magico)), $"{tipo}: o CRC refeito não foi lido");
            Assert.False(LerQualquer(ComMagicoECrcRefeitos(pacote, Protocolo.Magico ^ 0x01)), $"{tipo}: leu com a magia errada");

            // Só Ola e Recusado (layout congelado) são lidos de outra versão; o resto exige a do host.
            bool congelado = tipo is TipoDeMensagem.Ola or TipoDeMensagem.Recusado;
            Assert.True(LerDoTipo(tipo, Protocolo.Envelopar(tipo, corpo, Protocolo.Versao)), $"{tipo}: não leu na versão atual");
            Assert.Equal(congelado, LerDoTipo(tipo, Protocolo.Envelopar(tipo, corpo, OutraVersao)));
        }

        // Tipo que não existe, com CRC válido: não passa nem no despacho. O controle: o mesmo envelope com um tipo de verdade passa.
        Assert.True(Protocolo.TentarLerTipo(Protocolo.Envelopar(TipoDeMensagem.Instantaneo, []), out _));
        byte primeiroTipoQueNaoExiste = (byte)(UltimoValor<TipoDeMensagem>() + 1);
        foreach (byte t in new byte[] { 0, primeiroTipoQueNaoExiste, 0xFF })
            Assert.False(Protocolo.TentarLerTipo(Protocolo.Envelopar((TipoDeMensagem)t, [1, 2, 3]), out _), $"leu o tipo {t}, que não existe");
    }

    /// <summary>O maior valor de um enum — o primeiro fora da faixa é ele + 1. Derivado, e não escrito, porque outras tarefas acrescentam membros.</summary>
    private static byte UltimoValor<T>() where T : struct, Enum => Convert.ToByte(Enum.GetValues<T>().Max());

    /// <summary>
    /// Um corpo escrito à mão, campo a campo, na ordem do cabeçalho de Protocolo.cs — sem passar pelos Escrever*, que
    /// nunca escrevem valor fora da faixa. O campo chamado <paramref name="campo"/> sai com <paramref name="troca"/>;
    /// os outros, com o valor válido de sempre. Um nome de campo errado num caso não passa calado: sem troca o corpo
    /// é válido e o "recusado" do caso é aceito.
    /// </summary>
    private sealed class CorpoAMao(string? campo, Action<EscritorBinario>? troca)
    {
        private readonly EscritorBinario _e = new();

        public CorpoAMao Campo(string nome, Action<EscritorBinario> valido)
        {
            (nome == campo && troca is not null ? troca : valido)(_e);
            return this;
        }

        public byte[] Envelopar(TipoDeMensagem tipo) => Protocolo.Envelopar(tipo, _e.Escrito);
    }

    /// <summary>Um campo e dois valores pra ele: o último que o leitor aceita e o primeiro que ele tem de recusar.</summary>
    private sealed record Limite(string Campo, string Regra, Action<EscritorBinario> Aceito, Action<EscritorBinario> Recusado);

    private static readonly Action<EscritorBinario> Nada = _ => { };
    private static readonly Action<EscritorBinario> UmByteASobrar = e => e.U8(0);

    private static void ConferirLimites(Func<string?, Action<EscritorBinario>?, byte[]> aMao, Func<byte[], bool> ler, params Limite[] limites)
    {
        Assert.True(ler(aMao(null, null)), "o corpo à mão, sem troca nenhuma, devia ser válido");
        foreach (var l in limites)
        {
            Assert.True(ler(aMao(l.Campo, l.Aceito)), $"{l.Campo} ({l.Regra}): recusou o último valor válido");
            Assert.False(ler(aMao(l.Campo, l.Recusado)), $"{l.Campo} ({l.Regra}): aceitou o valor fora da faixa");
        }
    }

    [Fact]
    public void O_bem_vindo_com_crc_valido_e_campo_fora_da_faixa_e_recusado()
    {
        static byte[] AMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca)
                .Campo("índice", e => e.U8(2))
                .Campo("dificuldade", e => e.U8((byte)Dificuldade.Medio))
                .Campo("flags", e => e.U8(1))
                .Campo("sets pra vencer", e => e.VarU(2))
                .Campo("modo de golpe", e => e.U8((byte)ModoDeGolpe.Manual))
                .Campo("semente", e => e.U32(9))
                .Campo("humanos", e => e.U8(0b0101))
                .Campo("fim", Nada)
                .Envelopar(TipoDeMensagem.BemVindo);

        ConferirLimites(AMao, p => Protocolo.TentarLerBemVindo(p, out _),
            // Sem esta, um BemVindo com índice 4 punha Indice = 4 no cliente e o Passo seguinte estourava em Jogadores[4].
            new("índice", "uma das 4 vagas", e => e.U8(3), e => e.U8(4)),
            new("índice", "uma das 4 vagas", e => e.U8(3), e => e.U8(0xFF)),
            new("dificuldade", "enum", e => e.U8(UltimoValor<Dificuldade>()), e => e.U8((byte)(UltimoValor<Dificuldade>() + 1))),
            new("flags", "só o bit do ponto de ouro", e => e.U8(1), e => e.U8(2)),
            new("sets pra vencer", "ao menos 1", e => e.VarU(1), e => e.VarU(0)),
            new("sets pra vencer", "até 99", e => e.VarU(99), e => e.VarU(100)),
            new("modo de golpe", "enum", e => e.U8(UltimoValor<ModoDeGolpe>()), e => e.U8((byte)(UltimoValor<ModoDeGolpe>() + 1))),
            new("humanos", "um bit por vaga", e => e.U8(0x0F), e => e.U8(0x10)),
            new("fim", "nada depois dos humanos", Nada, UmByteASobrar));
    }

    [Fact]
    public void As_outras_mensagens_da_sala_com_crc_valido_e_campo_fora_da_faixa_sao_recusadas()
    {
        static byte[] OlaAMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca).Campo("nome", e => e.Texto("Ana")).Campo("fim", Nada).Envelopar(TipoDeMensagem.Ola);
        ConferirLimites(OlaAMao, p => Protocolo.TentarLerOla(p, out _),
            new Limite("fim", "nada depois do nome", Nada, UmByteASobrar));

        static byte[] RecusadoAMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca).Campo("motivo", e => e.U8((byte)MotivoDaRecusa.SalaCheia)).Campo("fim", Nada).Envelopar(TipoDeMensagem.Recusado);
        ConferirLimites(RecusadoAMao, p => Protocolo.TentarLerRecusado(p, out _),
            new("motivo", "enum, a partir de 1", e => e.U8(1), e => e.U8(0)),
            new("motivo", "enum", e => e.U8(UltimoValor<MotivoDaRecusa>()), e => e.U8((byte)(UltimoValor<MotivoDaRecusa>() + 1))),
            new("fim", "nada depois do motivo", Nada, UmByteASobrar));

        static void Nomes(EscritorBinario e) { e.Texto("Host"); e.Texto(""); e.Texto("Ana"); e.Texto(""); }
        static byte[] SalaAMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca).Campo("nomes", Nomes).Campo("fim", Nada).Envelopar(TipoDeMensagem.Sala);
        ConferirLimites(SalaAMao, p => Protocolo.TentarLerSala(p, out _),
            new Limite("fim", "nada depois do 4º nome", Nada, UmByteASobrar));

        static byte[] ComecouAMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca).Campo("humanos", e => e.U8(0b0101)).Campo("nomes", Nomes).Campo("fim", Nada).Envelopar(TipoDeMensagem.Comecou);
        ConferirLimites(ComecouAMao, p => Protocolo.TentarLerComecou(p, out _),
            new("humanos", "um bit por vaga", e => e.U8(0x0F), e => e.U8(0x10)),
            new("fim", "nada depois do 4º nome", Nada, UmByteASobrar));
    }

    [Fact]
    public void As_entradas_com_crc_valido_e_campo_fora_da_faixa_sao_recusadas()
    {
        // U8 n, U32 seq da mais nova, n × [I8 dx, I8 dy, U8 flags, U8 contador de ação, U8 contador de lob].
        static Action<EscritorBinario> Lote(int n, uint ultima, byte flagsDaUltima = 0) => e =>
        {
            e.U8((byte)n);
            e.U32(ultima);
            for (int i = 0; i < n; i++) { e.I8(10); e.I8(-20); e.U8(i == n - 1 ? flagsDaUltima : (byte)0); e.U8(3); e.U8(1); }
        };
        static byte[] AMao(string? campo, Action<EscritorBinario>? troca) =>
            new CorpoAMao(campo, troca).Campo("lote", Lote(2, 10)).Campo("fim", Nada).Envelopar(TipoDeMensagem.Entradas);

        ConferirLimites(AMao, p => Protocolo.TentarLerEntradas(p, out _),
            // n = 0 com a seq no teto é o único lote vazio que a checagem da seq não pega sozinha.
            new("lote", "ao menos 1 entrada", Lote(1, uint.MaxValue), Lote(0, uint.MaxValue)),
            new("lote", "a seq da mais velha não fica abaixo de 0", Lote(8, 7), Lote(8, 6)),
            new("lote", "flags: só o bit da ação segurada", Lote(2, 10, flagsDaUltima: 1), Lote(2, 10, flagsDaUltima: 2)),
            new("fim", "nada depois da última entrada", Nada, UmByteASobrar));
    }

    private const uint TickAMao = 1000;

    private static void BolaAMao(EscritorBinario e, byte flags)
    {
        for (int i = 0; i < 9; i++) e.I16((short)(i * 100));
        e.U8(flags);
    }

    private static void JogadorAMao(EscritorBinario e, byte flags)
    {
        for (int i = 0; i < 4; i++) e.I16((short)(i * 250));
        e.U8(40); e.U8(10); e.U8(0);
        e.U8(flags);
    }

    /// <summary>n eventos iguais, com ids que sobem de <paramref name="salto"/> em <paramref name="salto"/> a partir de <paramref name="primeiroId"/>.</summary>
    private static Action<EscritorBinario> EventosAMao(int n, uint primeiroId = 100, uint salto = 1, uint atras = 0,
        byte? tipo = null, byte jogador = 0xFF, byte timeMaisUm = 0, byte golpe = 0xFF, byte motivo = 0xFF) => e =>
    {
        e.U8((byte)n);
        if (n > 0) e.U32(primeiroId);
        for (int k = 0; k < n; k++)
        {
            if (k > 0) e.VarU(salto);
            e.VarU(atras);
            e.U8(tipo ?? (byte)TipoDeEventoDaPartida.Quique);
            e.U8(jogador); e.U8(timeMaisUm); e.U8(golpe); e.U8(motivo);
        }
    };

    private static Action<EscritorBinario> DescontinuidadesAMao(int n, uint atras = 0, byte flagsDaBola = 1) => e =>
    {
        e.U8((byte)n);
        for (int k = 0; k < n; k++) { e.VarU(atras); BolaAMao(e, flagsDaBola); }
    };

    private static Action<EscritorBinario> ConfirmacoesAMao(params int[] indices) => e =>
    {
        e.U8((byte)indices.Length);
        foreach (int i in indices) { e.U8((byte)i); e.U32(500); }
    };

    private static Action<EscritorBinario> DoisVarU(uint a, uint b) => e => { e.VarU(a); e.VarU(b); };

    private static byte[] InstantaneoAMao(string? campo = null, Action<EscritorBinario>? troca = null) =>
        new CorpoAMao(campo, troca)
            .Campo("tick", e => e.U32(TickAMao))
            .Campo("estado", e => e.U8((byte)EstadoDaPartida.Rally))
            .Campo("temporizador", e => e.I16(250))
            .Campo("bola", e => BolaAMao(e, flags: 1))
            .Campo("jogador 0", e => JogadorAMao(e, flags: 2))
            .Campo("jogador 1", e => JogadorAMao(e, flags: 0))
            .Campo("jogador 2", e => JogadorAMao(e, flags: 3))
            .Campo("jogador 3", e => JogadorAMao(e, flags: 0))
            .Campo("pontos", DoisVarU(2, 1))
            .Campo("games", DoisVarU(3, 4))
            .Campo("sets", DoisVarU(1, 0))
            .Campo("flags do placar", e => e.U8(2 | 16))
            .Campo("sets pra vencer", e => e.VarU(2))
            .Campo("sets encerrados", e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(7); e.VarU(4); })
            .Campo("mensagem", e => { e.U8(1 | 2); e.Texto("Ponto!"); })
            .Campo("caixa", e => { e.I16(-500); e.I16(0); e.I16(0); e.I16(695); })
            .Campo("confirmações", ConfirmacoesAMao(2, 1))
            .Campo("eventos", EventosAMao(3, atras: 10))
            .Campo("descontinuidades", DescontinuidadesAMao(1, atras: 5))
            .Campo("fim", Nada)
            .Envelopar(TipoDeMensagem.Instantaneo);

    [Fact]
    public void O_instantaneo_com_crc_valido_e_campo_fora_da_faixa_e_recusado()
    {
        byte tipoMax = UltimoValor<TipoDeEventoDaPartida>(), golpeMax = UltimoValor<TipoDeGolpe>(), motivoMax = UltimoValor<Motivo>();
        const int MaximoDePontos = 10_000, MaximoDeSets = 32;
        ConferirLimites(InstantaneoAMao, p => Protocolo.TentarLerInstantaneo(p, out _),
            new("estado", "enum", e => e.U8(UltimoValor<EstadoDaPartida>()), e => e.U8((byte)(UltimoValor<EstadoDaPartida>() + 1))),
            new("bola", "flags: em jogo, rolando, parada", e => BolaAMao(e, 7), e => BolaAMao(e, 8)),
            new("jogador 3", "flags: balanço de lob, humano", e => JogadorAMao(e, 3), e => JogadorAMao(e, 4)),

            new("pontos", "até 10.000", DoisVarU(MaximoDePontos, MaximoDePontos), DoisVarU(MaximoDePontos + 1, 0)),
            new("pontos", "até 10.000", DoisVarU(MaximoDePontos, MaximoDePontos), DoisVarU(0, MaximoDePontos + 1)),
            new("games", "até 10.000", DoisVarU(MaximoDePontos, MaximoDePontos), DoisVarU(MaximoDePontos + 1, 0)),
            new("games", "até 10.000", DoisVarU(MaximoDePontos, MaximoDePontos), DoisVarU(0, MaximoDePontos + 1)),
            new("sets", "até 32", DoisVarU(MaximoDeSets, MaximoDeSets), DoisVarU(MaximoDeSets + 1, 0)),
            new("sets", "até 32", DoisVarU(MaximoDeSets, MaximoDeSets), DoisVarU(0, MaximoDeSets + 1)),
            new("flags do placar", "6 bits", e => e.U8(63), e => e.U8(64)),
            new("flags do placar", "vencedor time 1 só com vencedor", e => e.U8(4 | 8), e => e.U8(8)),
            new("sets pra vencer", "ao menos 1", e => e.VarU(1), e => e.VarU(0)),
            new("sets pra vencer", "até 32", e => e.VarU(MaximoDeSets), e => e.VarU(MaximoDeSets + 1)),
            new("sets encerrados", "até 32",
                e => { e.VarU(MaximoDeSets); for (int i = 0; i < MaximoDeSets; i++) { e.VarU(6); e.VarU(4); e.U8(0); } },
                e => { e.VarU(MaximoDeSets + 1); for (int i = 0; i <= MaximoDeSets; i++) { e.VarU(6); e.VarU(4); e.U8(0); } }),
            new("sets encerrados", "games até 10.000",
                e => { e.VarU(1); e.VarU(MaximoDePontos); e.VarU(MaximoDePontos); e.U8(0); },
                e => { e.VarU(1); e.VarU(MaximoDePontos + 1); e.VarU(0); e.U8(0); }),
            new("sets encerrados", "games até 10.000",
                e => { e.VarU(1); e.VarU(MaximoDePontos); e.VarU(MaximoDePontos); e.U8(0); },
                e => { e.VarU(1); e.VarU(0); e.VarU(MaximoDePontos + 1); e.U8(0); }),
            // Recusado sem os pontos do tie-break: sem a checagem, o 2 passaria como "sem tie-break" e o resto alinharia.
            new("sets encerrados", "teve tie-break é 0 ou 1",
                e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(7); e.VarU(5); },
                e => { e.VarU(1); e.VarU(6); e.VarU(4); e.U8(2); }),
            new("sets encerrados", "pontos do tie-break até 10.000",
                e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(MaximoDePontos); e.VarU(MaximoDePontos); },
                e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(MaximoDePontos + 1); e.VarU(5); }),
            new("sets encerrados", "pontos do tie-break até 10.000",
                e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(MaximoDePontos); e.VarU(MaximoDePontos); },
                e => { e.VarU(1); e.VarU(7); e.VarU(6); e.U8(1); e.VarU(7); e.VarU(MaximoDePontos + 1); }),

            // 9 = 8 | 1: com o bit de "tem mensagem", só a checagem dos 3 bits pega o bit que sobra.
            new("mensagem", "flags: tem, destaque, suave", e => { e.U8(7); e.Texto("Ponto!"); }, e => { e.U8(9); e.Texto("Ponto!"); }),
            // Recusado sem texto: sem a checagem, o 2 passaria como "sem mensagem" e o resto alinharia.
            new("mensagem", "destaque e suave só com mensagem", e => { e.U8(1 | 2); e.Texto(""); }, e => e.U8(2)),

            new("confirmações", "até 4", ConfirmacoesAMao(0, 1, 2, 3), ConfirmacoesAMao(0, 1, 2, 3, 1)),
            // Sem esta, uma confirmação com índice 4 entrava no instantâneo do cliente.
            new("confirmações", "índice de uma das 4 vagas", ConfirmacoesAMao(3), ConfirmacoesAMao(4)),

            new("eventos", "até 32", EventosAMao(32), EventosAMao(33)),
            new("eventos", "o id cresce", EventosAMao(2, salto: 1), EventosAMao(2, salto: 0)),
            new("eventos", "o id não dá a volta", EventosAMao(2, primeiroId: uint.MaxValue - 1, salto: 1), EventosAMao(2, primeiroId: uint.MaxValue - 1, salto: 2)),
            new("eventos", "não é de antes do tick 0", EventosAMao(1, atras: TickAMao), EventosAMao(1, atras: TickAMao + 1)),
            new("eventos", "tipo: enum", EventosAMao(1, tipo: tipoMax), EventosAMao(1, tipo: (byte)(tipoMax + 1))),
            new("eventos", "jogador: uma das 4 vagas", EventosAMao(1, jogador: 3), EventosAMao(1, jogador: 4)),
            new("eventos", "jogador: ou nenhum (0xFF)", EventosAMao(1, jogador: 0xFF), EventosAMao(1, jogador: 0xFE)),
            new("eventos", "time + 1: 0, 1 ou 2", EventosAMao(1, timeMaisUm: 2), EventosAMao(1, timeMaisUm: 3)),
            new("eventos", "golpe: enum", EventosAMao(1, golpe: golpeMax), EventosAMao(1, golpe: (byte)(golpeMax + 1))),
            new("eventos", "motivo: enum", EventosAMao(1, motivo: motivoMax), EventosAMao(1, motivo: (byte)(motivoMax + 1))),

            new("descontinuidades", "até 3", DescontinuidadesAMao(3), DescontinuidadesAMao(4)),
            new("descontinuidades", "não é de antes do tick 0", DescontinuidadesAMao(1, atras: TickAMao), DescontinuidadesAMao(1, atras: TickAMao + 1)),
            new("descontinuidades", "flags da bola", DescontinuidadesAMao(1, flagsDaBola: 7), DescontinuidadesAMao(1, flagsDaBola: 8)),

            new("fim", "nada depois das descontinuidades", Nada, UmByteASobrar));
    }

    [Fact]
    public void O_instantaneo_a_mao_e_o_que_o_escritor_escreve()
    {
        // Amarra o corpo à mão ao layout de verdade: o que se lê dele, escrito de novo pelo EscreverInstantaneo, dá os
        // mesmos bytes. Sem isto, um corpo à mão que só por acaso passa no leitor provaria menos do que parece.
        byte[] aMao = InstantaneoAMao();
        Assert.True(Protocolo.TentarLerInstantaneo(aMao, out var lido));
        Assert.Equal(aMao, Protocolo.EscreverInstantaneo(lido));
        Assert.Equal(3, lido.Eventos.Count);
        Assert.Equal([2, 1], lido.Confirmacoes.Select(c => c.Indice));
        Assert.Equal(new Mensagem("Ponto!", Destaque: true), lido.Mensagem);
    }

    private static bool LerVarU(byte[] bytes, out uint valor)
    {
        var l = new LeitorBinario(bytes);
        return l.VarU(out valor);
    }

    [Fact]
    public void O_varu_recusa_o_que_passa_de_32_bits_e_o_que_acaba_no_meio()
    {
        Assert.True(LerVarU([0xFF, 0xFF, 0xFF, 0xFF, 0x0F], out uint maior));
        Assert.Equal(uint.MaxValue, maior);
        Assert.False(LerVarU([0xFF, 0xFF, 0xFF, 0xFF, 0x10], out _), "aceitou um 33º bit (e o valor teria dado a volta)");
        Assert.False(LerVarU([0x80, 0x80, 0x80, 0x80, 0x80, 0x00], out _), "aceitou um 6º byte");
        Assert.False(LerVarU([0x80], out _), "aceitou um VarU cortado no meio");

        var noTeto = new LeitorBinario([0x7F]);
        Assert.True(noTeto.VarU(out int v, 127));
        Assert.Equal(127, v);
        var acimaDoTeto = new LeitorBinario([0x80, 0x01]);   // 128
        Assert.False(acimaDoTeto.VarU(out int _, 127));
    }
}
