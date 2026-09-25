using System.Text.RegularExpressions;

namespace Padel.Core.Tests;

// O using mora DENTRO do namespace (como em TorneioTests): um tipo de mesmo nome que outra tarefa
// ponha direto em Padel.Core não sequestra os nomes daqui.
using Padel.Core.Perfil;
using Padel.Core.Rede;

/// <summary>
/// As 20 conquistas da Steam sem Steam: o catálogo (IDs que não podem mudar), o coletor que resume uma
/// Partida de verdade a partir dos eventos dela, e o avaliador que, com o resumo ou um evento de fora
/// (carreira, online), atualiza o perfil e diz o que acabou de ser desbloqueado. Cada conquista é testada
/// na condição exata E um passo antes dela.
/// </summary>
public class ConquistasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(10);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(20);

    private static PerfilDoJogador Novo() => PerfilDoJogador.Novo("Felipe", destro: true, T0);

    /// <summary>Uma partida terminada, local, contra a IA no médio — o resto zerado; cada teste muda só o que importa.</summary>
    private static ResumoDaPartida Resumo() => new()
    {
        Modo = ModoDaPartida.Local,
        Dificuldade = Dificuldade.Medio,
        RivalDaIA = true,
        Terminada = true,
    };

    private static IReadOnlyList<string> NovasDe(ResumoDaPartida resumo, PerfilDoJogador? perfil = null) =>
        AvaliadorDeConquistas.Aplicar(perfil ?? Novo(), resumo, T1).Novas.Select(c => c.Id).ToList();

    private static IReadOnlyList<string> NovasDe(EventoDeFora evento, PerfilDoJogador? perfil = null) =>
        AvaliadorDeConquistas.Aplicar(perfil ?? Novo(), evento, T1).Novas.Select(c => c.Id).ToList();

    // ── Catálogo ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A lista congelada: o ID é o "API name" da Steam e não muda depois de publicado. Renomear um aqui
    /// quebra este teste de propósito — quem renomeia tem que ler o comentário do catálogo antes.
    /// </summary>
    [Fact]
    public void O_catalogo_tem_as_20_conquistas_com_os_IDs_congelados()
    {
        string[] esperados =
        [
            "PRIMEIRO_PONTO", "PRIMEIRA_VITORIA", "PNEU", "PONTO_DE_OURO", "TIE_BREAK", "VIRADA", "BANDEJA_100",
            "VIBORA_VENCEDORA", "POR_3", "POR_4", "CHIQUITA_VENCEDORA", "CONTRAPARED", "PELA_PORTA", "RALLY_30",
            "VITORIA_NO_DIFICIL", "SEM_BOLA_NA_REDE", "CAMPEAO_DE_ETAPA", "NUMERO_1", "VITORIA_ONLINE", "MARATONA",
        ];
        Assert.Equal(esperados, CatalogoDeConquistas.Todas.Select(c => c.Id));
        Assert.Equal(20, CatalogoDeConquistas.Todas.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Todo_ID_e_maiusculo_com_sublinhado_e_todo_texto_existe_nas_tres_linguas()
    {
        var formato = new Regex("^[A-Z0-9]+(_[A-Z0-9]+)*$");
        foreach (var c in CatalogoDeConquistas.Todas)
        {
            Assert.Matches(formato, c.Id);
            foreach (var idioma in Enum.GetValues<Idioma>())
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Nome.Em(idioma)), $"{c.Id} sem nome em {idioma}");
                Assert.False(string.IsNullOrWhiteSpace(c.Descricao.Em(idioma)), $"{c.Id} sem descrição em {idioma}");
            }
            // Os três textos não são o mesmo copiado (esquecer de traduzir). Nomes próprios do padel ("Por 3") podem coincidir.
            Assert.False(c.Descricao.Portugues == c.Descricao.Ingles && c.Descricao.Ingles == c.Descricao.Espanhol, $"{c.Id} com a descrição igual nas três línguas");
            Assert.Same(c, CatalogoDeConquistas.Por(c.Id));
        }
        Assert.Throws<ArgumentException>(() => CatalogoDeConquistas.Por("NAO_EXISTE"));
    }

    [Fact]
    public void Secretas_sao_o_por_4_e_a_contrapared_e_cumulativas_tem_estatistica_e_meta()
    {
        Assert.Equal(new[] { "POR_4", "CONTRAPARED" }, CatalogoDeConquistas.Todas.Where(c => c.Secreta).Select(c => c.Id));

        var cumulativas = CatalogoDeConquistas.Todas.Where(c => c.Cumulativa).ToList();
        Assert.Equal(new[] { "BANDEJA_100", "MARATONA" }, cumulativas.Select(c => c.Id));
        Assert.Equal("BANDEJAS", CatalogoDeConquistas.Por("BANDEJA_100").Estatistica);
        Assert.Equal(100, CatalogoDeConquistas.Por("BANDEJA_100").Meta);
        Assert.Equal("PARTIDAS", CatalogoDeConquistas.Por("MARATONA").Estatistica);
        Assert.Equal(50, CatalogoDeConquistas.Por("MARATONA").Meta);
        foreach (var c in CatalogoDeConquistas.Todas.Where(c => !c.Cumulativa))
        {
            Assert.Null(c.Estatistica);
            Assert.Equal(0, c.Meta);
        }
    }

    /// <summary>docs/CONQUISTAS.md é o que vai ser cadastrado no Steamworks: tem que listar exatamente o catálogo.</summary>
    [Fact]
    public void O_documento_das_conquistas_lista_cada_ID_do_catalogo()
    {
        string doc = File.ReadAllText(CaminhoDoDocumento());
        var linhas = doc.Split('\n');
        foreach (var c in CatalogoDeConquistas.Todas)
        {
            string? linha = linhas.FirstOrDefault(l => l.StartsWith($"| `{c.Id}` |", StringComparison.Ordinal));
            Assert.True(linha is not null, $"docs/CONQUISTAS.md não tem a linha de {c.Id}");
            // Os textos que vão pro Steamworks são os do catálogo, nas três línguas.
            foreach (var idioma in Enum.GetValues<Idioma>())
            {
                Assert.Contains(c.Nome.Em(idioma), linha);
                Assert.Contains(c.Descricao.Em(idioma), linha);
            }
            Assert.Contains(c.Secreta ? "| sim |" : "| não |", linha);
            if (c.Estatistica is string estatistica) Assert.Contains($"`{estatistica}` ≥ {c.Meta}", linha);
        }
        var idsNoDocumento = Regex.Matches(doc, @"^\| `([A-Z0-9_]+)` \|", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(CatalogoDeConquistas.Todas.Select(c => c.Id), idsNoDocumento);
    }

    private static string CaminhoDoDocumento()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            string candidato = Path.Combine(pasta.FullName, "docs", "CONQUISTAS.md");
            if (File.Exists(candidato) && Directory.Exists(Path.Combine(pasta.FullName, "Padel.Core"))) return candidato;
        }
        throw new FileNotFoundException("docs/CONQUISTAS.md não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }

    // ── Avaliador: cada conquista na condição exata e um passo antes ─────────────────────

    [Fact]
    public void PRIMEIRO_PONTO_com_um_ponto_ganho_e_nao_com_zero()
    {
        Assert.DoesNotContain("PRIMEIRO_PONTO", NovasDe(Resumo() with { PontosVencidos = 0, PontosPerdidos = 24 }));
        Assert.Contains("PRIMEIRO_PONTO", NovasDe(Resumo() with { PontosVencidos = 1, PontosPerdidos = 24 }));
        // Ponto é ponto mesmo numa partida abandonada.
        Assert.Contains("PRIMEIRO_PONTO", NovasDe(Resumo() with { Terminada = false, PontosVencidos = 1 }));
    }

    [Fact]
    public void PRIMEIRA_VITORIA_so_com_a_partida_terminada_e_vencida()
    {
        Assert.DoesNotContain("PRIMEIRA_VITORIA", NovasDe(Resumo() with { Venceu = false }));
        Assert.DoesNotContain("PRIMEIRA_VITORIA", NovasDe(Resumo() with { Terminada = false, Venceu = false }));
        Assert.Contains("PRIMEIRA_VITORIA", NovasDe(Resumo() with { Venceu = true }));
    }

    [Fact]
    public void PNEU_com_set_vencido_por_6_0_e_nao_com_6_1_nem_levando_o_pneu()
    {
        Assert.DoesNotContain("PNEU", NovasDe(Resumo() with { Sets = [new SetDoResumo(6, 1)] }));
        Assert.DoesNotContain("PNEU", NovasDe(Resumo() with { Sets = [new SetDoResumo(0, 6)] }));
        Assert.Contains("PNEU", NovasDe(Resumo() with { Sets = [new SetDoResumo(6, 0)] }));
        Assert.Contains("PNEU", NovasDe(Resumo() with { Sets = [new SetDoResumo(4, 6), new SetDoResumo(6, 0)], Terminada = false }));
    }

    [Fact]
    public void PONTO_DE_OURO_vencendo_um_e_nao_so_disputando()
    {
        Assert.DoesNotContain("PONTO_DE_OURO", NovasDe(Resumo() with { PontosDeOuroDisputados = 3, PontosDeOuroVencidos = 0 }));
        Assert.Contains("PONTO_DE_OURO", NovasDe(Resumo() with { PontosDeOuroDisputados = 3, PontosDeOuroVencidos = 1 }));
    }

    [Fact]
    public void TIE_BREAK_vencendo_o_set_no_tie_break_e_nao_perdendo_nem_7_5()
    {
        Assert.DoesNotContain("TIE_BREAK", NovasDe(Resumo() with { Sets = [new SetDoResumo(6, 7, 5, 7)] }));
        Assert.DoesNotContain("TIE_BREAK", NovasDe(Resumo() with { Sets = [new SetDoResumo(7, 5)] }));
        Assert.Contains("TIE_BREAK", NovasDe(Resumo() with { Sets = [new SetDoResumo(7, 6, 7, 5)] }));
    }

    [Fact]
    public void VIRADA_revertendo_3_games_e_nao_2()
    {
        Assert.DoesNotContain("VIRADA", NovasDe(Resumo() with { MaiorDesvantagemRevertida = 2 }));
        Assert.Contains("VIRADA", NovasDe(Resumo() with { MaiorDesvantagemRevertida = 3 }));
    }

    [Fact]
    public void BANDEJA_100_soma_entre_partidas_99_nao_100_sim()
    {
        var r1 = AvaliadorDeConquistas.Aplicar(Novo(), Resumo() with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 60 } }, T1);
        var r2 = AvaliadorDeConquistas.Aplicar(r1.Perfil, Resumo() with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 39, [TipoDeGolpe.Vibora] = 5 } }, T2);
        Assert.Equal(99, r2.Perfil.GolpesPorTipo[TipoDeGolpe.Bandeja]);
        Assert.Equal(99, CatalogoDeConquistas.ValorDaEstatistica(r2.Perfil, "BANDEJAS"));
        Assert.DoesNotContain(r2.Novas, c => c.Id == "BANDEJA_100");
        Assert.False(r2.Perfil.Desbloqueou("BANDEJA_100"));

        var r3 = AvaliadorDeConquistas.Aplicar(r2.Perfil, Resumo() with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 1 } }, T2);
        Assert.Contains(r3.Novas, c => c.Id == "BANDEJA_100");
        Assert.Equal(100, CatalogoDeConquistas.ValorDaEstatistica(r3.Perfil, "BANDEJAS"));
        // Bandeja no treino também conta: é onde se treina bandeja.
        Assert.Contains("BANDEJA_100", NovasDe(Resumo() with { Modo = ModoDaPartida.Treino, GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 100 } }));
    }

    [Theory]
    [InlineData(TipoDeGolpe.Vibora, "VIBORA_VENCEDORA")]
    [InlineData(TipoDeGolpe.SmashPor3, "POR_3")]
    [InlineData(TipoDeGolpe.SmashPor4, "POR_4")]
    [InlineData(TipoDeGolpe.Chiquita, "CHIQUITA_VENCEDORA")]
    [InlineData(TipoDeGolpe.Contrapared, "CONTRAPARED")]
    public void Golpe_vencedor_desbloqueia_e_o_golpe_sem_vencer_nao(TipoDeGolpe tipo, string id)
    {
        var soGolpes = Resumo() with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [tipo] = 10 } };
        Assert.DoesNotContain(id, NovasDe(soGolpes));
        var vencedor = soGolpes with { VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [tipo] = 1 } };
        Assert.Contains(id, NovasDe(vencedor));
    }

    [Fact]
    public void Smash_comum_vencedor_nao_e_por_3_nem_por_4()
    {
        var novas = NovasDe(Resumo() with { VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Smash] = 5 } });
        Assert.DoesNotContain("POR_3", novas);
        Assert.DoesNotContain("POR_4", novas);
    }

    [Fact]
    public void PELA_PORTA_com_uma_saida_pela_porta_e_nao_com_zero()
    {
        Assert.DoesNotContain("PELA_PORTA", NovasDe(Resumo() with { SaidasPelaPorta = 0 }));
        Assert.Contains("PELA_PORTA", NovasDe(Resumo() with { SaidasPelaPorta = 1 }));
    }

    [Fact]
    public void RALLY_30_com_30_golpes_e_nao_29()
    {
        Assert.DoesNotContain("RALLY_30", NovasDe(Resumo() with { MaiorRally = 29 }));
        Assert.Contains("RALLY_30", NovasDe(Resumo() with { MaiorRally = 30 }));
    }

    [Fact]
    public void VITORIA_NO_DIFICIL_so_vencendo_a_IA_no_dificil()
    {
        var dificil = Resumo() with { Dificuldade = Dificuldade.Dificil, Venceu = true };
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Dificuldade = Dificuldade.Medio }));
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Venceu = false }));
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Terminada = false, Venceu = false }));
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Online, RivalDaIA = false }));
        Assert.Contains("VITORIA_NO_DIFICIL", NovasDe(dificil));
        Assert.Contains("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Carreira }));
        Assert.Contains("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Coop }));
    }

    [Fact]
    public void SEM_BOLA_NA_REDE_vencendo_com_zero_na_rede_e_nao_com_uma()
    {
        Assert.DoesNotContain("SEM_BOLA_NA_REDE", NovasDe(Resumo() with { Venceu = true, GolpesNaRede = 1 }));
        Assert.DoesNotContain("SEM_BOLA_NA_REDE", NovasDe(Resumo() with { Venceu = false, GolpesNaRede = 0 }));
        Assert.Contains("SEM_BOLA_NA_REDE", NovasDe(Resumo() with { Venceu = true, GolpesNaRede = 0 }));
    }

    [Fact]
    public void MARATONA_na_quinquagesima_partida_terminada_e_nao_na_49a()
    {
        var perfil = Novo();
        for (int i = 0; i < 49; i++) perfil = AvaliadorDeConquistas.Aplicar(perfil, Resumo(), T1).Perfil;
        // Abandonada e treino não contam.
        perfil = AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { Terminada = false }, T1).Perfil;
        perfil = AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { Modo = ModoDaPartida.Treino }, T1).Perfil;
        Assert.Equal(49, perfil.Partidas);
        Assert.Equal(49, CatalogoDeConquistas.ValorDaEstatistica(perfil, "PARTIDAS"));
        Assert.False(perfil.Desbloqueou("MARATONA"));

        var r = AvaliadorDeConquistas.Aplicar(perfil, Resumo(), T2);
        Assert.Equal(50, r.Perfil.Partidas);
        Assert.Contains(r.Novas, c => c.Id == "MARATONA");
        Assert.Equal(T2, r.Perfil.Conquistas["MARATONA"]);
    }

    [Fact]
    public void Treino_so_conta_golpes_e_tempo()
    {
        var treino = Resumo() with
        {
            Modo = ModoDaPartida.Treino,
            Venceu = true,
            PontosVencidos = 20,
            Sets = [new SetDoResumo(7, 6, 7, 0)],
            PontosDeOuroVencidos = 2,
            MaiorDesvantagemRevertida = 4,
            MaiorRally = 40,
            SaidasPelaPorta = 1,
            DuracaoEmSegundos = 90,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Lob] = 7 },
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.SmashPor4] = 1 },
        };
        var r = AvaliadorDeConquistas.Aplicar(Novo(), treino, T1);
        Assert.Empty(r.Novas);
        Assert.Equal(0, r.Perfil.Partidas);
        Assert.Equal(0, r.Perfil.Vitorias);
        Assert.Equal(0, r.Perfil.MaiorRally);
        Assert.Empty(r.Perfil.VencedoresPorTipo);
        Assert.Equal(7, r.Perfil.GolpesPorTipo[TipoDeGolpe.Lob]);
        Assert.Equal(90, r.Perfil.SegundosDeJogo, 3);
    }

    [Fact]
    public void Resumo_impossivel_e_recusado_antes_de_entrar_no_perfil()
    {
        // Um perfil que já tem estatística: somar um negativo nele daria um número "válido" e errado, então não basta o perfil se defender.
        var perfil = Novo() with
        {
            Partidas = 3,
            SegundosDeJogo = 100,
            MaiorRally = 10,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 50 },
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Vibora] = 5 },
        };
        Assert.ThrowsAny<ArgumentException>(() => AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { DuracaoEmSegundos = -1 }, T1));
        Assert.ThrowsAny<ArgumentException>(() => AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { DuracaoEmSegundos = float.NaN }, T1));
        Assert.ThrowsAny<ArgumentException>(() => AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { MaiorRally = -1 }, T1));
        Assert.ThrowsAny<ArgumentException>(() => AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = -5 } }, T1));
        Assert.ThrowsAny<ArgumentException>(() => AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Vibora] = -1 } }, T1));
    }

    // ── Eventos de fora da partida ───────────────────────────────────────────────────────

    [Fact]
    public void CAMPEAO_DE_ETAPA_com_a_etapa_vencida_e_nao_com_o_circuito_em_2o()
    {
        Assert.DoesNotContain("CAMPEAO_DE_ETAPA", NovasDe(new CircuitoEncerrado(2)));
        Assert.Equal(new[] { "CAMPEAO_DE_ETAPA" }, NovasDe(new EtapaVencida()));
    }

    [Fact]
    public void NUMERO_1_com_o_circuito_em_1o_e_nao_em_2o()
    {
        Assert.Empty(NovasDe(new CircuitoEncerrado(2)));
        Assert.Equal(new[] { "NUMERO_1" }, NovasDe(new CircuitoEncerrado(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitoEncerrado(0));
    }

    [Fact]
    public void VITORIA_ONLINE_pelo_evento_do_online_ou_pelo_resumo_online_e_nunca_por_vitoria_local()
    {
        Assert.DoesNotContain("VITORIA_ONLINE", NovasDe(Resumo() with { Venceu = true }));
        Assert.DoesNotContain("VITORIA_ONLINE", NovasDe(Resumo() with { Modo = ModoDaPartida.Online, RivalDaIA = false, Venceu = false }));
        Assert.Contains("VITORIA_ONLINE", NovasDe(Resumo() with { Modo = ModoDaPartida.Online, RivalDaIA = false, Venceu = true }));

        // No cliente online não há Partida (só a visão da rede): quem avisa é a camada online. Aqui o cliente é o rival do host.
        var novas = NovasDe(new VitoriaOnline([true, false, true, false], 2));
        Assert.Contains("VITORIA_ONLINE", novas);
        Assert.Contains("PRIMEIRA_VITORIA", novas);
    }

    /// <summary>
    /// A sala do host começa com quem estiver nela quando a espera acaba (SessaoHost → ServidorDaPartida.Iniciar): vaga
    /// vazia é IA. Ganhar da IA numa sala vazia é vitória, e pode ser vitória no difícil, mas não é vitória ONLINE —
    /// essa pede ao menos um rival humano desde o começo.
    /// </summary>
    [Fact]
    public void VITORIA_ONLINE_pede_um_rival_humano_e_sala_sem_rival_contra_a_IA_nao_vale()
    {
        var salaVazia = Resumo() with { Modo = ModoDaPartida.Online, RivalDaIA = true, Venceu = true };
        var novas = NovasDe(salaVazia);
        Assert.DoesNotContain("VITORIA_ONLINE", novas);
        Assert.Contains("PRIMEIRA_VITORIA", novas);
        Assert.Contains("VITORIA_ONLINE", NovasDe(salaVazia with { RivalDaIA = false }));
    }

    /// <summary>O caso exato da revisão: sala online vazia, difícil, vencida.</summary>
    [Fact]
    public void Sala_online_vazia_no_dificil_vencida_da_as_de_vitoria_contra_a_IA_e_nao_a_online()
    {
        var r = AvaliadorDeConquistas.Aplicar(Novo(), new ResumoDaPartida
        {
            Modo = ModoDaPartida.Online,
            RivalDaIA = true,
            Dificuldade = Dificuldade.Dificil,
            Terminada = true,
            Venceu = true,
        }, T1);
        Assert.Equal(new[] { "PRIMEIRA_VITORIA", "VITORIA_NO_DIFICIL", "SEM_BOLA_NA_REDE" }, r.Novas.Select(c => c.Id));
    }

    /// <summary>
    /// "Contra a IA" é RivalDaIA, e só ele: o modo não entra (fora o treino). Um versus local entre humanos no difícil
    /// não é vitória contra a IA — a dificuldade só vale pra IA, e não havia IA do outro lado; uma sala online sem rival
    /// humano é a IA no difícil das mesmas Opções do jogo local (PartidaNode passa Configuracao.Dificuldade ao host).
    /// </summary>
    [Fact]
    public void VITORIA_NO_DIFICIL_pede_os_dois_rivais_da_IA_em_qualquer_modo_menos_o_treino()
    {
        var dificil = Resumo() with { Dificuldade = Dificuldade.Dificil, Venceu = true };
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { RivalDaIA = false }));
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Coop, RivalDaIA = false }));
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Treino }));
        Assert.Contains("VITORIA_NO_DIFICIL", NovasDe(dificil with { Modo = ModoDaPartida.Online, RivalDaIA = true }));
    }

    /// <summary>
    /// No cliente o evento traz quem era humano no começo (o Comecou do host) e a vaga do cliente: VITORIA_ONLINE só com
    /// rival humano. O host é sempre humano, então o cliente rival sempre tem; o cliente parceiro pode não ter.
    /// </summary>
    [Fact]
    public void VITORIA_ONLINE_pelo_evento_do_cliente_pede_um_rival_humano()
    {
        var contraOHost = NovasDe(new VitoriaOnline([true, false, true, false], 2));
        Assert.Contains("VITORIA_ONLINE", contraOHost);
        Assert.Contains("PRIMEIRA_VITORIA", contraOHost);
        Assert.Contains("VITORIA_ONLINE", NovasDe(new VitoriaOnline([true, true, false, true], 1)));

        // Parceiro do host contra duas IAs: vitória, mas não online.
        Assert.Equal(new[] { "PRIMEIRA_VITORIA" }, NovasDe(new VitoriaOnline([true, true, false, false], 1)));
    }

    [Fact]
    public void Evento_de_vitoria_online_impossivel_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => new VitoriaOnline([true, false, true], 2));                    // não são 4 vagas
        Assert.Throws<ArgumentOutOfRangeException>(() => new VitoriaOnline([true, false, true, false], -1));  // Indice antes do BemVindo
        Assert.Throws<ArgumentOutOfRangeException>(() => new VitoriaOnline([true, false, true, false], 4));
        Assert.Throws<ArgumentException>(() => new VitoriaOnline([true, false, false, false], 2));            // a vaga dele era da IA
    }

    /// <summary>As duas são opostas numa mesma partida: ou havia rival humano (online), ou os dois eram IA (difícil).</summary>
    [Fact]
    public void Nenhuma_partida_da_VITORIA_ONLINE_e_VITORIA_NO_DIFICIL_juntas()
    {
        foreach (var modo in Enum.GetValues<ModoDaPartida>())
            foreach (bool rivalDaIA in new[] { true, false })
            {
                var novas = NovasDe(Resumo() with { Modo = modo, RivalDaIA = rivalDaIA, Dificuldade = Dificuldade.Dificil, Venceu = true });
                Assert.False(novas.Contains("VITORIA_ONLINE") && novas.Contains("VITORIA_NO_DIFICIL"), $"{modo}, RivalDaIA = {rivalDaIA}");
            }
    }

    [Fact]
    public void Evento_de_fora_nao_mexe_nas_estatisticas()
    {
        var perfil = Novo();
        var r = AvaliadorDeConquistas.Aplicar(perfil, new VitoriaOnline([true, false, true, false], 2), T1);
        Assert.Equal(perfil.Partidas, r.Perfil.Partidas);
        Assert.Equal(perfil.Vitorias, r.Perfil.Vitorias);
        Assert.Equal(perfil.SegundosDeJogo, r.Perfil.SegundosDeJogo);
    }

    // ── Idempotência, imutabilidade e estatísticas ───────────────────────────────────────

    [Fact]
    public void Desbloquear_de_novo_nao_duplica_nem_troca_o_instante()
    {
        var primeira = AvaliadorDeConquistas.Aplicar(Novo(), Resumo() with { PontosVencidos = 3, Venceu = true }, T1);
        Assert.Equal(new[] { "PRIMEIRO_PONTO", "PRIMEIRA_VITORIA", "SEM_BOLA_NA_REDE" }, primeira.Novas.Select(c => c.Id));
        Assert.Equal(T1, primeira.Perfil.Conquistas["PRIMEIRO_PONTO"]);

        var segunda = AvaliadorDeConquistas.Aplicar(primeira.Perfil, Resumo() with { PontosVencidos = 5, Venceu = true }, T2);
        Assert.Empty(segunda.Novas);
        Assert.Equal(3, segunda.Perfil.Conquistas.Count);
        Assert.Equal(T1, segunda.Perfil.Conquistas["PRIMEIRO_PONTO"]);

        var deFora = AvaliadorDeConquistas.Aplicar(segunda.Perfil, new EtapaVencida(), T2);
        var deForaDeNovo = AvaliadorDeConquistas.Aplicar(deFora.Perfil, new EtapaVencida(), T2.AddDays(1));
        Assert.Empty(deForaDeNovo.Novas);
        Assert.Equal(T2, deForaDeNovo.Perfil.Conquistas["CAMPEAO_DE_ETAPA"]);
    }

    [Fact]
    public void Aplicar_nao_muda_o_perfil_de_entrada()
    {
        var perfil = Novo();
        var golpes = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 100 };
        var r = AvaliadorDeConquistas.Aplicar(perfil, Resumo() with { Venceu = true, GolpesPorTipo = golpes }, T1);
        Assert.Empty(perfil.Conquistas);
        Assert.Equal(0, perfil.Partidas);
        Assert.Empty(perfil.GolpesPorTipo);
        // E o perfil novo não é um espelho do dicionário de quem chamou.
        golpes[TipoDeGolpe.Bandeja] = 1;
        Assert.Equal(100, r.Perfil.GolpesPorTipo[TipoDeGolpe.Bandeja]);
    }

    [Fact]
    public void As_estatisticas_acumulam_partida_a_partida()
    {
        var a = Resumo() with
        {
            Venceu = true,
            DuracaoEmSegundos = 600.5f,
            MaiorRally = 12,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Normal] = 30, [TipoDeGolpe.Lob] = 4 },
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Normal] = 6 },
        };
        var b = Resumo() with
        {
            Venceu = false,
            DuracaoEmSegundos = 300,
            MaiorRally = 9,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Normal] = 10, [TipoDeGolpe.Smash] = 2 },
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Smash] = 1 },
        };
        var perfil = AvaliadorDeConquistas.Aplicar(AvaliadorDeConquistas.Aplicar(Novo(), a, T1).Perfil, b, T2).Perfil;
        Assert.Equal(2, perfil.Partidas);
        Assert.Equal(1, perfil.Vitorias);
        Assert.Equal(12, perfil.MaiorRally);
        Assert.Equal(900.5, perfil.SegundosDeJogo, 3);
        Assert.Equal(40, perfil.GolpesPorTipo[TipoDeGolpe.Normal]);
        Assert.Equal(4, perfil.GolpesPorTipo[TipoDeGolpe.Lob]);
        Assert.Equal(2, perfil.GolpesPorTipo[TipoDeGolpe.Smash]);
        Assert.Equal(6, perfil.VencedoresPorTipo[TipoDeGolpe.Normal]);
        Assert.Equal(1, perfil.VencedoresPorTipo[TipoDeGolpe.Smash]);
    }

    [Fact]
    public void As_novas_vem_na_ordem_do_catalogo_com_o_objeto_do_catalogo()
    {
        var r = AvaliadorDeConquistas.Aplicar(Novo(), Resumo() with
        {
            Venceu = true,
            PontosVencidos = 1,
            MaiorRally = 30,
            Sets = [new SetDoResumo(6, 0)],
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.SmashPor4] = 1 },
        }, T1);
        var ordemNoCatalogo = CatalogoDeConquistas.Todas.Select(c => c.Id).ToList();
        Assert.Equal(r.Novas.OrderBy(c => ordemNoCatalogo.IndexOf(c.Id)).Select(c => c.Id), r.Novas.Select(c => c.Id));
        foreach (var c in r.Novas) Assert.Same(CatalogoDeConquistas.Por(c.Id), c);
        Assert.Contains(r.Novas, c => c.Id == "POR_4");
        Assert.Contains(r.Novas, c => c.Id == "RALLY_30");
    }

    // ── Coletor: partidas de verdade entre IAs ───────────────────────────────────────────

    private static OpcoesDaPartida SoIA(uint semente, Dificuldade dificuldade = Dificuldade.Medio, int sets = 1, bool pontoDeOuro = true) =>
        new() { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = dificuldade, SetsParaVencer = sets, PontoDeOuro = pontoDeOuro };

    private static void JogarAteOFim(Partida partida)
    {
        float t = 0;
        while (!partida.Acabou && t < 3600) { partida.Avancar(1f / 120f); t += 1f / 120f; }
        Assert.True(partida.Acabou, "a partida não terminou em uma hora simulada");
    }

    private static bool EhPonto(TipoDeEventoDaPartida tipo) =>
        tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida;

    /// <summary>
    /// Um segundo cálculo, independente do coletor: só a sequência de quem ganhou cada ponto (Ponto/Game/Set/Partida
    /// com o Time) re-jogada à mão — sem ler o Placar. Dá pontos de ouro, desvantagem revertida e tie-breaks do time 0.
    /// </summary>
    private sealed class Oraculo
    {
        private readonly bool _pontoDeOuro;
        private readonly int[] _pontos = [0, 0], _games = [0, 0];
        private int _desvantagemNoSet;
        public int PontosDoTime0, OuroDisputados, OuroVencidos, MaiorDesvantagemRevertida, SetsNoTieBreak, Pneus;

        public Oraculo(Partida partida)
        {
            _pontoDeOuro = partida.Opcoes.PontoDeOuro;
            partida.Evento += Ver;
        }

        private void Ver(EventoDaPartida e)
        {
            if (!EhPonto(e.Tipo)) return;
            int para = e.Time;
            if (para == 0) PontosDoTime0 += 1;
            bool tieBreak = _games[0] == 6 && _games[1] == 6;
            if (_pontoDeOuro && !tieBreak && _pontos[0] == 3 && _pontos[1] == 3)
            {
                OuroDisputados += 1;
                if (para == 0) OuroVencidos += 1;
            }
            if (e.Tipo == TipoDeEventoDaPartida.Ponto) { _pontos[para] += 1; return; }
            _pontos[0] = _pontos[1] = 0;
            _games[para] += 1;
            _desvantagemNoSet = Math.Max(_desvantagemNoSet, _games[1] - _games[0]);
            if (e.Tipo == TipoDeEventoDaPartida.Game) return;
            if (para == 0)
            {
                MaiorDesvantagemRevertida = Math.Max(MaiorDesvantagemRevertida, _desvantagemNoSet);
                if (tieBreak) SetsNoTieBreak += 1;
                if (_games[0] == 6 && _games[1] == 0) Pneus += 1;
            }
            _games[0] = _games[1] = 0;
            _desvantagemNoSet = 0;
        }
    }

    public static TheoryData<uint, Dificuldade, int, bool> Partidas => new()
    {
        { 1, Dificuldade.Medio, 1, true },
        { 3, Dificuldade.Dificil, 1, true },
        { 4, Dificuldade.Facil, 1, true },
        { 8, Dificuldade.Facil, 1, true },
        { 9, Dificuldade.Dificil, 1, true },
        { 11, Dificuldade.Medio, 1, true },
        { 5, Dificuldade.Medio, 2, true },
        { 7, Dificuldade.Medio, 1, false },
    };

    [Theory]
    [MemberData(nameof(Partidas))]
    public void O_coletor_bate_com_o_placar_e_as_estatisticas_da_propria_partida(uint semente, Dificuldade dificuldade, int sets, bool pontoDeOuro)
    {
        var partida = new Partida(SoIA(semente, dificuldade, sets, pontoDeOuro));
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Carreira, 0);
        var oraculo = new Oraculo(partida);
        JogarAteOFim(partida);
        var r = coletor.Resumo();

        Assert.True(r.Terminada);
        Assert.Equal(partida.Placar.Vencedor == 0, r.Venceu);
        Assert.Equal(ModoDaPartida.Carreira, r.Modo);
        Assert.Equal(dificuldade, r.Dificuldade);
        Assert.True(r.RivalDaIA);
        Assert.Equal(new[] { 0 }, r.IndicesDoPerfil);
        Assert.Equal(0, r.TimeDoPerfil);
        Assert.Equal(partida.TempoDeJogo, r.DuracaoEmSegundos);

        // Placar final: os sets do Placar, do ponto de vista do time do perfil.
        var sets0 = partida.Placar.SetsAnteriores;
        Assert.Equal(sets0.Count, r.Sets.Count);
        for (int i = 0; i < sets0.Count; i++)
        {
            Assert.Equal(sets0[i].Games[0], r.Sets[i].GamesDoTime);
            Assert.Equal(sets0[i].Games[1], r.Sets[i].GamesDoRival);
            Assert.Equal(sets0[i].TieBreak?[0], r.Sets[i].TieBreakDoTime);
            Assert.Equal(sets0[i].TieBreak?[1], r.Sets[i].TieBreakDoRival);
            Assert.True(r.Sets[i].Encerrado);
        }
        Assert.Equal(partida.Placar.Sets[0], r.SetsVencidos);
        Assert.Equal(partida.Placar.Sets[1], r.SetsPerdidos);

        // Pontos e rally: os da Estatisticas da partida.
        Assert.Equal(partida.Estatisticas.Pontos, r.PontosVencidos + r.PontosPerdidos);
        Assert.Equal(oraculo.PontosDoTime0, r.PontosVencidos);
        Assert.Equal(partida.Estatisticas.MaiorRally, r.MaiorRally);

        // Golpes do jogador 0: os que o próprio Jogador contou, por tipo e por lado.
        Assert.Equal(partida.Jogadores[0].Golpes, r.GolpesPorTipo.Values.Sum());
        Assert.Equal(partida.Jogadores[0].Golpes, r.GolpesPorLado.Values.Sum());
        Assert.Equal(r.TotalDeGolpes, r.GolpesPorTipo.Values.Sum());
        Assert.True(r.GolpesPorTipo.GetValueOrDefault(TipoDeGolpe.Saque) > 0, "o jogador 0 nunca sacou?");

        // O que o oráculo recalcula pela sequência de pontos.
        Assert.Equal(oraculo.OuroDisputados, r.PontosDeOuroDisputados);
        Assert.Equal(oraculo.OuroVencidos, r.PontosDeOuroVencidos);
        Assert.Equal(oraculo.MaiorDesvantagemRevertida, r.MaiorDesvantagemRevertida);
        Assert.Equal(oraculo.SetsNoTieBreak, r.SetsVencidosNoTieBreak);
        Assert.Equal(oraculo.Pneus, r.Sets.Count(s => s.Pneu));
        if (!pontoDeOuro) Assert.Equal(0, r.PontosDeOuroDisputados);

        // Vencedores e erros do jogador 0 cabem nos pontos do time.
        Assert.True(r.VencedoresPorTipo.Values.Sum() <= r.PontosVencidos);
        Assert.True(r.ErrosPorMotivo.Values.Sum() <= r.PontosPerdidos);
        Assert.True(r.GolpesNaRede >= r.ErrosPorMotivo.GetValueOrDefault(Motivo.Rede));
    }

    [Fact]
    public void Os_pontos_de_ouro_aparecem_de_verdade_nas_partidas_do_teste()
    {
        // Sem isto, a comparação com o oráculo podia passar com zero de cada lado sempre.
        int disputados = 0, vencidos = 0;
        foreach (uint semente in new uint[] { 1, 4, 11 })
        {
            var partida = new Partida(SoIA(semente));
            using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
            JogarAteOFim(partida);
            disputados += coletor.Resumo().PontosDeOuroDisputados;
            vencidos += coletor.Resumo().PontosDeOuroVencidos;
        }
        Assert.True(disputados > 0, "nenhum ponto de ouro em três partidas");
        Assert.True(vencidos > 0 && vencidos <= disputados);
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(6u)]
    [InlineData(42u)]
    public void Os_quatro_jogadores_juntos_explicam_todo_ponto_da_partida(uint semente)
    {
        var partida = new Partida(SoIA(semente, (Dificuldade)(semente % 3)));
        var coletores = Enumerable.Range(0, 4).Select(i => new ColetorDaPartida(partida, ModoDaPartida.Local, i)).ToList();
        JogarAteOFim(partida);
        var resumos = coletores.Select(c => c.Resumo()).ToList();
        var e = partida.Estatisticas;

        // Todo ponto teve um último golpe: ou foi vencedor de quem bateu, ou erro dele.
        int vencedores = resumos.Sum(r => r.VencedoresPorTipo.Values.Sum());
        int erros = resumos.Sum(r => r.ErrosPorMotivo.Values.Sum());
        Assert.Equal(e.Pontos, vencedores + erros);
        // Motivos que só existem contra quem bateu: somam exatamente o que a partida contou.
        foreach (var motivo in new[] { Motivo.Rede, Motivo.NaoPassou, Motivo.ParedeSemQuicar, Motivo.DuplaFalta })
            Assert.Equal(e.Motivos.GetValueOrDefault(motivo), resumos.Sum(r => r.ErrosPorMotivo.GetValueOrDefault(motivo)));
        // Bola na rede: os pontos perdidos na rede, mais no máximo as faltas de saque.
        int naRede = resumos.Sum(r => r.GolpesNaRede);
        Assert.InRange(naRede, e.Motivos.GetValueOrDefault(Motivo.Rede), e.Motivos.GetValueOrDefault(Motivo.Rede) + e.Faltas + e.Motivos.GetValueOrDefault(Motivo.DuplaFalta));

        Assert.Equal(e.Golpes, resumos.Sum(r => r.TotalDeGolpes));
        for (int i = 0; i < 4; i++) Assert.Equal(partida.Jogadores[i].Golpes, resumos[i].TotalDeGolpes);
        // Os dois lados do mesmo placar.
        Assert.Equal(resumos[0].PontosVencidos, resumos[2].PontosPerdidos);
        Assert.Equal(resumos[0].Venceu, !resumos[2].Venceu);
        Assert.Equal(resumos[0].PontosDeOuroDisputados, resumos[3].PontosDeOuroDisputados);
        Assert.Equal(resumos[0].PontosDeOuroDisputados, resumos[0].PontosDeOuroVencidos + resumos[2].PontosDeOuroVencidos);
        Assert.Equal(resumos[0].Sets.Select(s => (s.GamesDoTime, s.GamesDoRival)), resumos[2].Sets.Select(s => (s.GamesDoRival, s.GamesDoTime)));
        foreach (var c in coletores) c.Dispose();
    }

    [Fact]
    public void No_coop_os_dois_jogadores_do_perfil_somam()
    {
        var partida = new Partida(SoIA(3, Dificuldade.Facil));
        using var dupla = new ColetorDaPartida(partida, ModoDaPartida.Coop, 0, 1);
        using var so0 = new ColetorDaPartida(partida, ModoDaPartida.Coop, 0);
        using var so1 = new ColetorDaPartida(partida, ModoDaPartida.Coop, 1);
        JogarAteOFim(partida);
        var r = dupla.Resumo();
        Assert.Equal(new[] { 0, 1 }, r.IndicesDoPerfil);
        Assert.Equal(partida.Jogadores[0].Golpes + partida.Jogadores[1].Golpes, r.TotalDeGolpes);
        Assert.Equal(so0.Resumo().VencedoresPorTipo.Values.Sum() + so1.Resumo().VencedoresPorTipo.Values.Sum(), r.VencedoresPorTipo.Values.Sum());
        Assert.Equal(so0.Resumo().GolpesNaRede + so1.Resumo().GolpesNaRede, r.GolpesNaRede);
        Assert.Equal(so0.Resumo().PontosVencidos, r.PontosVencidos);
    }

    [Fact]
    public void Indices_do_perfil_invalidos_sao_recusados()
    {
        var partida = new Partida(SoIA(1));
        Assert.Throws<ArgumentException>(() => new ColetorDaPartida(partida, ModoDaPartida.Coop, 0, 2));   // times diferentes
        Assert.Throws<ArgumentException>(() => new ColetorDaPartida(partida, ModoDaPartida.Local));        // nenhum
        Assert.Throws<ArgumentException>(() => new ColetorDaPartida(partida, ModoDaPartida.Coop, 1, 1));   // repetido
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColetorDaPartida(partida, ModoDaPartida.Local, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColetorDaPartida(partida, ModoDaPartida.Local, -1));
        var visitante = new ColetorDaPartida(partida, ModoDaPartida.Local, 3);
        Assert.Equal(1, visitante.Resumo().TimeDoPerfil);
        visitante.Dispose();
    }

    // ── Coletor: rival da IA ou humano ───────────────────────────────────────────────────

    public static TheoryData<bool[], int, bool> QuemEraHumano => new()
    {
        { [true, false, false, false], 0, true },    // local contra a IA (ou a sala online só com o host)
        { [true, true, false, false], 0, true },     // coop contra a IA
        { [true, false, true, false], 0, false },    // versus: um rival humano já basta
        { [true, false, false, true], 0, false },
        { [true, true, true, true], 1, false },
        { [true, false, false, false], 2, false },   // o perfil no time 1: o rival é o time 0, e o jogador 0 é humano
        { [false, false, true, false], 2, true },
    };

    [Theory]
    [MemberData(nameof(QuemEraHumano))]
    public void RivalDaIA_sai_de_quem_era_humano_nas_opcoes_da_partida(bool[] humanos, int indice, bool rivalDaIA)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = humanos, Semente = 1 });
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Local, indice);
        Assert.Equal(rivalDaIA, coletor.Resumo().RivalDaIA);
    }

    [Fact]
    public void Versus_local_entre_humanos_no_dificil_nao_e_vitoria_contra_a_IA()
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = [true, false, true, false], Semente = 1, Dificuldade = Dificuldade.Dificil });
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        // O fim é de mentira (dois humanos sem controle não terminam a partida): o que está em teste é o RivalDaIA do coletor.
        var vencida = coletor.Resumo() with { Terminada = true, Venceu = true };
        var novas = NovasDe(vencida);
        Assert.Contains("PRIMEIRA_VITORIA", novas);
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", novas);
        Assert.DoesNotContain("VITORIA_ONLINE", novas);
    }

    /// <summary>Uma sala online de verdade (ServidorDaPartida e clientes sobre a RedeEmMemoria), como a de RedeSalaTests.</summary>
    private sealed class SalaOnline
    {
        private readonly RedeEmMemoria _rede = new(11);
        private readonly TransporteEmMemoria _host;

        public SalaOnline()
        {
            _host = _rede.NovoPonto();
            Servidor = new ServidorDaPartida(_host, "Host", new OpcoesDaPartida { Dificuldade = Dificuldade.Dificil, Semente = 5 });
        }

        public ServidorDaPartida Servidor { get; }
        /// <summary>Os clientes que ainda mandam pacote; quem sai daqui fica calado (e, jogando, vira IA em 3 s).</summary>
        public List<ClienteDaPartida> Falando { get; } = [];

        public ClienteDaPartida Entrar(string nome)
        {
            var ponto = _rede.NovoPonto();
            _rede.Conectar(ponto, _host);
            var cliente = new ClienteDaPartida(ponto, nome);
            Falando.Add(cliente);
            Rodar(0.3);
            return cliente;
        }

        public void Rodar(double segundos)
        {
            for (int i = 0; i < (int)Math.Round(segundos * Protocolo.TicksPorSegundo); i++)
            {
                _rede.AvancarRelogio(Protocolo.Passo);
                Servidor.Passo();
                foreach (var c in Falando) c.Passo();
            }
        }
    }

    /// <summary>A sala que <c>SessaoHost</c> inicia quando a espera acaba e ninguém entrou: o host contra três IAs.</summary>
    [Fact]
    public void Sala_online_so_com_o_host_nao_da_VITORIA_ONLINE_ao_host()
    {
        var sala = new SalaOnline();
        var partida = sala.Servidor.Iniciar();
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Online, 0);
        Assert.Equal(new[] { true, false, false, false }, partida.Opcoes.Humanos);

        var r = coletor.Resumo();
        Assert.True(r.RivalDaIA);
        var novas = NovasDe(r with { Terminada = true, Venceu = true });   // o fim é de mentira: o que está em teste é a sala
        Assert.DoesNotContain("VITORIA_ONLINE", novas);
        Assert.Contains("VITORIA_NO_DIFICIL", novas);
    }

    /// <summary>
    /// O rival remoto que fica calado 3 s vira IA no meio (ServidorDaPartida.ConferirSilencio põe Jogador.Humano = false).
    /// A partida começou contra gente: continua sendo vitória online, e não vira vitória contra a IA no difícil.
    /// </summary>
    [Fact]
    public void Rival_online_que_cai_e_vira_IA_no_meio_continua_contando_como_humano()
    {
        var sala = new SalaOnline();
        var rival = sala.Entrar("Rival");
        Assert.Equal(2, rival.Indice);   // o primeiro a entrar é o rival

        var partida = sala.Servidor.Iniciar();
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Online, 0);
        sala.Rodar(0.3);
        Assert.True(partida.Jogadores[2].Humano);
        // Do lado do cliente, com o que o Comecou trouxe: o host é humano e é rival dele.
        Assert.Contains("VITORIA_ONLINE", NovasDe(new VitoriaOnline(rival.Humanos, rival.Indice)));

        sala.Falando.Remove(rival);
        sala.Rodar(Protocolo.SegundosAteVirarIA + 0.5);
        Assert.False(partida.Jogadores[2].Humano);   // a premissa: o servidor passou o rival pra IA

        var r = coletor.Resumo();
        Assert.False(r.RivalDaIA);
        var novas = NovasDe(r with { Terminada = true, Venceu = true });   // o fim é de mentira: o que está em teste é quem era gente
        Assert.Contains("VITORIA_ONLINE", novas);
        Assert.DoesNotContain("VITORIA_NO_DIFICIL", novas);
    }

    /// <summary>
    /// Ana entra (rival), Bia entra (parceira do host), Ana sai antes do começo — o caso de RedeSalaTests
    /// .Quem_sai_da_sala_libera_a_vaga_pro_proximo. Bia e o host jogam contra duas IAs: a vitória da Bia não é online.
    /// </summary>
    [Fact]
    public void Cliente_parceiro_do_host_contra_duas_IAs_nao_ganha_VITORIA_ONLINE()
    {
        var sala = new SalaOnline();
        var ana = sala.Entrar("Ana");
        var bia = sala.Entrar("Bia");
        ana.Sair();
        sala.Falando.Remove(ana);
        sala.Rodar(0.3);
        sala.Servidor.Iniciar();
        sala.Rodar(0.3);

        Assert.Equal(1, bia.Indice);
        Assert.Equal(new[] { true, true, false, false }, bia.Humanos);
        Assert.Equal(new[] { "PRIMEIRA_VITORIA" }, NovasDe(new VitoriaOnline(bia.Humanos, bia.Indice)));
    }

    [Fact]
    public void Resumo_no_meio_nao_e_vitoria_e_o_set_em_andamento_aparece_aberto()
    {
        var partida = new Partida(SoIA(4, Dificuldade.Facil));
        using var coletor = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        while (partida.Placar.Games[0] + partida.Placar.Games[1] < 3) partida.Avancar(1f / 120f);
        var r = coletor.Resumo();
        Assert.False(r.Terminada);
        Assert.False(r.Venceu);
        var aberto = Assert.Single(r.Sets);
        Assert.False(aberto.Encerrado);
        Assert.False(aberto.Vencido);
        Assert.Equal((partida.Placar.Games[0], partida.Placar.Games[1]), (aberto.GamesDoTime, aberto.GamesDoRival));
        Assert.Equal(0, r.SetsVencidos + r.SetsPerdidos);
    }

    [Fact]
    public void Depois_do_Dispose_o_coletor_para_de_contar()
    {
        var partida = new Partida(SoIA(1));
        var coletor = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        while (partida.Estatisticas.Pontos < 5) partida.Avancar(1f / 120f);
        coletor.Dispose();
        var antes = coletor.Resumo();
        JogarAteOFim(partida);
        var depois = coletor.Resumo();
        Assert.Equal(antes.PontosVencidos + antes.PontosPerdidos, depois.PontosVencidos + depois.PontosPerdidos);
        Assert.Equal(antes.TotalDeGolpes, depois.TotalDeGolpes);
    }

    // ── Saída pela porta: a física deixa, mas é rara — o lance é montado à mão ───────────

    /// <summary>
    /// Joga até o jogador 0 dar um golpe de rally (não saque) e, no quadro seguinte, troca a trajetória da bola pela dada —
    /// no lado dos rivais (y &lt; 0). Os rivais vão pro fundo pra não interceptar. Devolve o evento que encerrou o lance.
    /// </summary>
    /// <summary>
    /// Joga até o jogador 0 dar um golpe de rally, tira os rivais do caminho e lança a bola montada. O rally até ali pode já
    /// ter dado pontos (e vencedores) — muda sempre que a IA muda —, então quem conta usa <paramref name="antes"/>: o resumo
    /// de cada coletor no instante do lance, e confere a DIFERENÇA, nunca o total da partida.
    /// </summary>
    private static EventoDaPartida LanceMontado(Partida partida, float x, float y, float z, float vx, float vy, float vz,
        out ResumoDaPartida[] antes, params ColetorDaPartida[] coletores)
    {
        bool golpeDoZero = false;
        EventoDaPartida? fim = null;
        void Ver(EventoDaPartida e)
        {
            if (e.Tipo == TipoDeEventoDaPartida.Golpe) golpeDoZero = e.Jogador == partida.Jogadores[0] && e.Golpe != TipoDeGolpe.Saque;
            if (EhPonto(e.Tipo) || e.Tipo is TipoDeEventoDaPartida.Falta or TipoDeEventoDaPartida.Let) { golpeDoZero = false; fim = e; }
        }
        partida.Evento += Ver;
        float t = 0;
        while (!(golpeDoZero && partida.Estado == EstadoDaPartida.Rally))
        {
            partida.Avancar(1f / 120f);
            t += 1f / 120f;
            Assert.True(t < 600 && !partida.Acabou, "o jogador 0 não deu golpe de rally");
        }
        foreach (var rival in partida.JogadoresDoTime(1)) { rival.X = 0; rival.Y = rival.Lado * 9.5f; rival.Vx = rival.Vy = 0; }
        antes = coletores.Select(c => c.Resumo()).ToArray();
        fim = null;
        partida.Bola.Posicionar(x, y, z);
        partida.Bola.Lancar(new Velocidade(vx, vy, vz, 0));
        for (int i = 0; i < 480 && fim is null; i++) partida.Avancar(1f / 120f);
        partida.Evento -= Ver;
        Assert.NotNull(fim);
        return fim;
    }

    [Fact]
    public void Bola_do_jogador_que_quica_do_outro_lado_e_sai_pela_porta_e_saida_pela_porta_e_ponto_dele()
    {
        var partida = new Partida(SoIA(1));
        using var jogador = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        using var parceiro = new ColetorDaPartida(partida, ModoDaPartida.Local, 1);
        // Quica logo (z = 6 cm, descendo) em y ≈ -0,75 e chega a x = 5 com |y| ≈ 0,8 e baixa: a porta (|y| 0,45–1,25, z < 2).
        var ponto = LanceMontado(partida, 3.9f, -0.75f, 0.06f, 9f, -0.5f, -2f, out var antes, jogador, parceiro);

        Assert.Equal(TipoDeEventoDaPartida.Ponto, ponto.Tipo);
        Assert.Equal(0, ponto.Time);
        Assert.Equal(Motivo.Fora, ponto.Motivo);
        Assert.Equal(1, jogador.Resumo().SaidasPelaPorta - antes[0].SaidasPelaPorta);
        Assert.Equal(0, parceiro.Resumo().SaidasPelaPorta - antes[1].SaidasPelaPorta);   // o ponto é do time, mas a bola não era dele
        Assert.Contains("PELA_PORTA", NovasDe(jogador.Resumo()));
    }

    [Fact]
    public void Bola_que_sai_pela_porta_sem_quicar_e_erro_e_nao_saida_pela_porta()
    {
        var partida = new Partida(SoIA(1));
        using var jogador = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        // A 1 m do chão, reta: passa pela porta sem quicar.
        var ponto = LanceMontado(partida, 3.9f, -0.75f, 1.0f, 9f, -0.5f, 0f, out var antes, jogador);

        Assert.Equal(1, ponto.Time);
        Assert.Equal(Motivo.Fora, ponto.Motivo);
        Assert.Equal(0, jogador.Resumo().SaidasPelaPorta - antes[0].SaidasPelaPorta);
        Assert.Equal(1, jogador.Resumo().ErrosPorMotivo.GetValueOrDefault(Motivo.Fora) - antes[0].ErrosPorMotivo.GetValueOrDefault(Motivo.Fora));
    }

    [Fact]
    public void Bola_que_quica_e_sai_por_cima_da_grade_lateral_e_vencedor_mas_nao_pela_porta()
    {
        // Quica em (2, -3) e sobe alto: cruza x = 5 acima dos 3 m da grade do meio da lateral — sai, mas não pela porta.
        var partida = new Partida(SoIA(1));
        using var jogador = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        var ponto = LanceMontado(partida, 2f, -3f, 0.05f, 3.75f, 0f, -14f, out var antes, jogador);

        Assert.Equal(0, ponto.Time);
        Assert.Equal(Motivo.Fora, ponto.Motivo);
        Assert.Equal(0, jogador.Resumo().SaidasPelaPorta - antes[0].SaidasPelaPorta);
        Assert.Equal(1, jogador.Resumo().VencedoresPorTipo.Values.Sum() - antes[0].VencedoresPorTipo.Values.Sum());
    }

    [Fact]
    public void Bola_que_quica_e_bate_na_grade_ao_lado_da_porta_nao_sai()
    {
        // Mesma bola da porta, 1,2 m mais longe da rede: ali a lateral é grade até 3 m — a bola bate e volta.
        var partida = new Partida(SoIA(1));
        using var jogador = new ColetorDaPartida(partida, ModoDaPartida.Local, 0);
        var ponto = LanceMontado(partida, 3.9f, -1.95f, 0.06f, 9f, -0.5f, -2f, out var antes, jogador);
        Assert.NotEqual(Motivo.Fora, ponto.Motivo);
        Assert.Equal(0, jogador.Resumo().SaidasPelaPorta - antes[0].SaidasPelaPorta);
    }
}
