using System.Text.Json.Nodes;

namespace Padel.Core.Tests;

// Os usings moram DENTRO do namespace (como em TorneioTests): um tipo de mesmo nome posto direto em
// Padel.Core por outra tarefa não sequestra os nomes daqui.
using Padel.Core.Perfil;
using Padel.Core.Torneio;

/// <summary>
/// O perfil do jogador sem tela nem Steam: o que ele guarda, o arquivo JSON versionado (que vai pro Steam
/// Cloud), a mesclagem de dois perfis em conflito (dois PCs) e o texto do Rich Presence nas três línguas.
/// </summary>
public class PerfilTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);

    /// <summary>Um perfil com de tudo: estatísticas, golpes, vencedores e conquistas com instantes diferentes.</summary>
    private static PerfilDoJogador Completo()
    {
        var resumo = new ResumoDaPartida
        {
            Modo = ModoDaPartida.Carreira,
            Dificuldade = Dificuldade.Dificil,
            RivalDaIA = true,
            Terminada = true,
            Venceu = true,
            PontosVencidos = 30,
            PontosPerdidos = 21,
            Sets = [new SetDoResumo(6, 0)],
            DuracaoEmSegundos = 512.25f,
            MaiorRally = 17,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Saque] = 12, [TipoDeGolpe.Bandeja] = 7, [TipoDeGolpe.Vibora] = 2 },
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Vibora] = 1, [TipoDeGolpe.Normal] = 9 },
            GolpesNaRede = 2,
        };
        var perfil = AvaliadorDeConquistas.Aplicar(PerfilDoJogador.Novo("Felipe", destro: false, T0), resumo, T1).Perfil;
        return AvaliadorDeConquistas.Aplicar(perfil, new EtapaVencida(), T2).Perfil;
    }

    // ── O perfil ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Perfil_novo_comeca_zerado_com_nome_mao_e_instante()
    {
        var p = PerfilDoJogador.Novo("Felipe", destro: false, T0);
        Assert.Equal("Felipe", p.Nome);
        Assert.False(p.Destro);
        Assert.Equal(T0, p.PreferenciasAlteradasEm);
        Assert.Equal(0, p.Partidas);
        Assert.Equal(0, p.Vitorias);
        Assert.Equal(0, p.MaiorRally);
        Assert.Equal(0, p.SegundosDeJogo);
        Assert.Empty(p.GolpesPorTipo);
        Assert.Empty(p.VencedoresPorTipo);
        Assert.Empty(p.Conquistas);
        Assert.False(p.Desbloqueou("PRIMEIRO_PONTO"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_em_branco_e_recusado(string nome)
    {
        Assert.Throws<ArgumentException>(() => PerfilDoJogador.Novo(nome, destro: true, T0));
        Assert.Throws<ArgumentException>(() => PerfilDoJogador.Novo("Felipe", destro: true, T0).ComPreferencias(nome, destro: true, T1));
    }

    [Fact]
    public void Trocar_nome_e_mao_guarda_o_instante_e_nao_mexe_no_resto()
    {
        var antes = Completo();
        var depois = antes.ComPreferencias("Felipe B.", destro: true, T2.AddDays(1));
        Assert.Equal("Felipe B.", depois.Nome);
        Assert.True(depois.Destro);
        Assert.Equal(T2.AddDays(1), depois.PreferenciasAlteradasEm);
        Assert.Equal(antes.Partidas, depois.Partidas);
        Assert.Equal(antes.Conquistas, depois.Conquistas);
        Assert.Equal("Felipe", antes.Nome);   // o de antes não mudou
    }

    [Fact]
    public void Dois_perfis_com_o_mesmo_conteudo_sao_iguais_mesmo_com_dicionarios_diferentes()
    {
        var a = PerfilDoJogador.Novo("Felipe", destro: true, T0) with
        {
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Lob] = 3, [TipoDeGolpe.Smash] = 1 },
            Conquistas = new Dictionary<string, DateTimeOffset> { ["PNEU"] = T1 },
        };
        var b = PerfilDoJogador.Novo("Felipe", destro: true, T0) with
        {
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Smash] = 1, [TipoDeGolpe.Lob] = 3 },
            Conquistas = new Dictionary<string, DateTimeOffset> { ["PNEU"] = T1.ToOffset(TimeSpan.FromHours(-3)) },
        };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Lob] = 4, [TipoDeGolpe.Smash] = 1 } });
        Assert.NotEqual(a, b with { Conquistas = new Dictionary<string, DateTimeOffset> { ["PNEU"] = T2 } });
    }

    /// <summary>
    /// Todo perfil que existe pode ser salvo e lido de volta: um estado que o Carregar recusaria (e a camada de cima
    /// trocaria pelo padrão, apagando o Cloud) nem chega a se montar — ou, no único caso entre dois campos, o Salvar recusa.
    /// </summary>
    [Fact]
    public void Perfil_impossivel_nem_se_monta_e_o_arquivo_nunca_grava_o_que_nao_carrega()
    {
        var p = PerfilDoJogador.Novo("Felipe", destro: true, T0);
        Assert.ThrowsAny<ArgumentException>(() => p with { Nome = " " });
        Assert.ThrowsAny<ArgumentException>(() => p with { Partidas = -1 });
        Assert.ThrowsAny<ArgumentException>(() => p with { Vitorias = -1 });
        Assert.ThrowsAny<ArgumentException>(() => p with { MaiorRally = -1 });
        Assert.ThrowsAny<ArgumentException>(() => p with { SegundosDeJogo = -1 });
        Assert.ThrowsAny<ArgumentException>(() => p with { SegundosDeJogo = double.NaN });
        Assert.ThrowsAny<ArgumentException>(() => p with { SegundosDeJogo = double.PositiveInfinity });
        Assert.ThrowsAny<ArgumentException>(() => p with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Lob] = -1 } });
        Assert.ThrowsAny<ArgumentException>(() => p with { VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Vibora] = -1 } });
        Assert.ThrowsAny<ArgumentException>(() => p with { GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [(TipoDeGolpe)99] = 1 } });
        Assert.ThrowsAny<ArgumentException>(() => p with { Conquistas = new Dictionary<string, DateTimeOffset> { ["pneu"] = T1 } });
        Assert.ThrowsAny<ArgumentException>(() => p with { Conquistas = new Dictionary<string, DateTimeOffset> { ["_PNEU"] = T1 } });

        // Vitórias acima das partidas: nem o avaliador nem a mesclagem produzem; montado à mão, o arquivo recusa gravar.
        var torto = p with { Partidas = 1, Vitorias = 2 };
        Assert.ThrowsAny<ArgumentException>(() => PersistenciaDoPerfil.Salvar(torto));
    }

    [Fact]
    public void Os_dicionarios_do_perfil_nao_se_deixam_mudar_nem_por_cast()
    {
        var p = Completo();
        foreach (object dicionario in new object[] { p.GolpesPorTipo, p.VencedoresPorTipo, p.Conquistas })
            Assert.True(dicionario is not System.Collections.IDictionary mutavel || mutavel.IsReadOnly, $"{dicionario.GetType().Name} aceita escrita");
    }

    // ── Arquivo (JSON com versão) ────────────────────────────────────────────────────────

    [Fact]
    public void Salvar_e_carregar_devolve_o_mesmo_perfil_e_o_mesmo_texto()
    {
        var perfil = Completo();
        string json = PersistenciaDoPerfil.Salvar(perfil);
        var lido = PersistenciaDoPerfil.Carregar(json);
        Assert.Equal(perfil, lido);
        Assert.Equal(json, PersistenciaDoPerfil.Salvar(lido));

        var raiz = JsonNode.Parse(json) as JsonObject;
        Assert.NotNull(raiz);
        Assert.Equal(PersistenciaDoPerfil.VersaoDoArquivo, (int?)raiz["Versao"]);
        Assert.Equal(1, PersistenciaDoPerfil.VersaoDoArquivo);
    }

    [Fact]
    public void O_arquivo_e_canonico_a_ordem_de_insercao_e_o_fuso_do_instante_nao_mudam_o_texto()
    {
        var a = PerfilDoJogador.Novo("Felipe", destro: true, T0) with
        {
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Smash] = 1, [TipoDeGolpe.Lob] = 3 },
            Conquistas = new Dictionary<string, DateTimeOffset> { ["VIRADA"] = T2, ["PNEU"] = T1 },
        };
        var b = PerfilDoJogador.Novo("Felipe", destro: true, T0.ToOffset(TimeSpan.FromHours(-3))) with
        {
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Lob] = 3, [TipoDeGolpe.Smash] = 1 },
            Conquistas = new Dictionary<string, DateTimeOffset> { ["PNEU"] = T1.ToOffset(TimeSpan.FromHours(-3)), ["VIRADA"] = T2 },
        };
        string json = PersistenciaDoPerfil.Salvar(a);
        Assert.Equal(json, PersistenciaDoPerfil.Salvar(b));
        Assert.DoesNotContain("-03:00", json);
        Assert.True(json.IndexOf("\"PNEU\"", StringComparison.Ordinal) < json.IndexOf("\"VIRADA\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(99, true)]
    [InlineData(0, false)]
    public void Versao_desconhecida_e_recusada_com_erro_claro(int versao, bool maisNova)
    {
        var raiz = JsonNode.Parse(PersistenciaDoPerfil.Salvar(Completo())) as JsonObject;
        Assert.NotNull(raiz);
        raiz["Versao"] = versao;
        var erro = Assert.Throws<PerfilIlegivelException>(() => PersistenciaDoPerfil.Carregar(raiz.ToJsonString()));
        Assert.Equal(MotivoDoPerfilIlegivel.VersaoDesconhecida, erro.Motivo);
        Assert.Equal(versao, erro.Versao);
        Assert.Equal(maisNova, erro.VeioDeUmJogoMaisNovo);
        Assert.Contains($"{versao}", erro.Message);
        Assert.Contains($"{PersistenciaDoPerfil.VersaoDoArquivo}", erro.Message);
        Assert.Contains("versão", erro.Message);
        if (maisNova) Assert.Contains("atualize", erro.Message);
    }

    private static JsonObject Objeto(JsonObject raiz, string chave) =>
        raiz[chave] as JsonObject ?? throw new InvalidOperationException($"o JSON bom não tem o objeto {chave}");

    public static TheoryData<string, string> Corrompidos()
    {
        string bom = PersistenciaDoPerfil.Salvar(Completo());
        string Mexer(Action<JsonObject> mexer)
        {
            var raiz = JsonNode.Parse(bom) as JsonObject ?? throw new InvalidOperationException("o JSON bom não é objeto");
            mexer(raiz);
            return raiz.ToJsonString();
        }
        return new TheoryData<string, string>
        {
            { "{ isto não é json", "JSON" },
            { bom[..(bom.Length / 2)], "JSON" },
            { "", "JSON" },
            { "[1, 2, 3]", "Versao" },
            { "null", "Versao" },
            { Mexer(r => r.Remove("Versao")), "Versao" },
            { Mexer(r => r["Versao"] = "1"), "Versao" },
            { Mexer(r => r["Nome"] = null), "Nome" },
            { Mexer(r => r.Remove("Nome")), "Nome" },
            { Mexer(r => r["Nome"] = "  "), "Nome" },
            { Mexer(r => r["Partidas"] = "muitas"), "JSON" },
            { Mexer(r => r["Partidas"] = -1), "Partidas" },
            { Mexer(r => r.Remove("Vitorias")), "Vitorias" },
            { Mexer(r => r["Vitorias"] = 999), "Vitorias" },
            { Mexer(r => r["SegundosDeJogo"] = -5.0), "SegundosDeJogo" },
            { Mexer(r => r["GolpesPorTipo"] = null), "GolpesPorTipo" },
            { Mexer(r => Objeto(r, "GolpesPorTipo")["GolpeQueNaoExiste"] = 3), "GolpeQueNaoExiste" },
            { Mexer(r => Objeto(r, "VencedoresPorTipo")["Vibora"] = -2), "VencedoresPorTipo" },
            { Mexer(r => r["Conquistas"] = null), "Conquistas" },
            { Mexer(r => Objeto(r, "Conquistas")["PNEU"] = "ontem"), "JSON" },
            { Mexer(r => Objeto(r, "Conquistas")["pneu minúsculo"] = "2026-09-25T12:00:00+00:00"), "pneu minúsculo" },
            { Mexer(r => r["PreferenciasAlteradasEm"] = null), "PreferenciasAlteradasEm" },
        };
    }

    [Theory]
    [MemberData(nameof(Corrompidos))]
    public void Arquivo_corrompido_vira_erro_claro_e_nunca_um_perfil_pela_metade(string json, string mencao)
    {
        var erro = Assert.Throws<PerfilIlegivelException>(() => PersistenciaDoPerfil.Carregar(json));
        Assert.Equal(MotivoDoPerfilIlegivel.Corrompido, erro.Motivo);
        Assert.False(erro.VeioDeUmJogoMaisNovo);
        Assert.Contains(mencao, erro.Message);
    }

    [Fact]
    public void Conquista_que_este_jogo_nao_conhece_e_preservada_ao_carregar_e_salvar()
    {
        // Um jogo mais novo pode ter conquistas a mais sem mudar a versão do arquivo: o antigo não pode apagá-las do Cloud.
        var raiz = JsonNode.Parse(PersistenciaDoPerfil.Salvar(Completo())) as JsonObject;
        Assert.NotNull(raiz);
        var conquistas = raiz["Conquistas"] as JsonObject;
        Assert.NotNull(conquistas);
        conquistas["CONQUISTA_DO_FUTURO"] = "2026-12-01T10:00:00+00:00";
        var perfil = PersistenciaDoPerfil.Carregar(raiz.ToJsonString());
        Assert.True(perfil.Desbloqueou("CONQUISTA_DO_FUTURO"));
        Assert.Contains("CONQUISTA_DO_FUTURO", PersistenciaDoPerfil.Salvar(perfil));
    }

    // ── Mesclagem (conflito do Steam Cloud: dois PCs) ────────────────────────────────────

    private static PerfilDoJogador A() => PerfilDoJogador.Novo("Felipe", destro: true, T0) with
    {
        Partidas = 10,
        Vitorias = 6,
        MaiorRally = 20,
        SegundosDeJogo = 1000,
        GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 50, [TipoDeGolpe.Lob] = 5 },
        VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Normal] = 30 },
        Conquistas = new Dictionary<string, DateTimeOffset> { ["PRIMEIRO_PONTO"] = T1, ["PNEU"] = T2 },
    };

    private static PerfilDoJogador B() => PerfilDoJogador.Novo("Felipe no notebook", destro: false, T1) with
    {
        Partidas = 8,
        Vitorias = 7,
        MaiorRally = 25,
        SegundosDeJogo = 1200.5,
        GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 40, [TipoDeGolpe.Smash] = 3 },
        VencedoresPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Normal] = 10, [TipoDeGolpe.Vibora] = 1 },
        Conquistas = new Dictionary<string, DateTimeOffset> { ["PRIMEIRO_PONTO"] = T0, ["VIRADA"] = T2 },
    };

    [Fact]
    public void Mesclar_fica_com_o_maximo_de_cada_contador()
    {
        var m = PerfilDoJogador.Mesclar(A(), B());
        Assert.Equal(10, m.Partidas);
        Assert.Equal(7, m.Vitorias);
        Assert.Equal(25, m.MaiorRally);
        Assert.Equal(1200.5, m.SegundosDeJogo);
        Assert.Equal(50, m.GolpesPorTipo[TipoDeGolpe.Bandeja]);
        Assert.Equal(5, m.GolpesPorTipo[TipoDeGolpe.Lob]);
        Assert.Equal(3, m.GolpesPorTipo[TipoDeGolpe.Smash]);
        Assert.Equal(30, m.VencedoresPorTipo[TipoDeGolpe.Normal]);
        Assert.Equal(1, m.VencedoresPorTipo[TipoDeGolpe.Vibora]);
    }

    [Fact]
    public void Mesclar_une_as_conquistas_com_o_instante_mais_antigo()
    {
        var m = PerfilDoJogador.Mesclar(A(), B());
        Assert.Equal(3, m.Conquistas.Count);
        Assert.Equal(T0, m.Conquistas["PRIMEIRO_PONTO"]);   // B desbloqueou antes
        Assert.Equal(T2, m.Conquistas["PNEU"]);
        Assert.Equal(T2, m.Conquistas["VIRADA"]);
    }

    [Fact]
    public void Mesclar_pega_nome_e_mao_de_quem_mudou_por_ultimo()
    {
        var m = PerfilDoJogador.Mesclar(A(), B());
        Assert.Equal("Felipe no notebook", m.Nome);
        Assert.False(m.Destro);
        Assert.Equal(T1, m.PreferenciasAlteradasEm);

        // Jogar depois não conta como "mais recente": só trocar nome ou mão conta.
        var aJogouDepois = AvaliadorDeConquistas.Aplicar(A(), new ResumoDaPartida { Terminada = true }, T2.AddDays(3)).Perfil;
        Assert.Equal("Felipe no notebook", PerfilDoJogador.Mesclar(aJogouDepois, B()).Nome);
        var aRenomeou = A().ComPreferencias("Felipe de novo", destro: true, T2);
        Assert.Equal("Felipe de novo", PerfilDoJogador.Mesclar(aRenomeou, B()).Nome);
        Assert.True(PerfilDoJogador.Mesclar(aRenomeou, B()).Destro);
    }

    [Fact]
    public void Mesclar_e_comutativa_idempotente_e_da_o_mesmo_nos_dois_PCs()
    {
        var ab = PerfilDoJogador.Mesclar(A(), B());
        Assert.Equal(ab, PerfilDoJogador.Mesclar(B(), A()));
        Assert.Equal(A(), PerfilDoJogador.Mesclar(A(), A()));
        Assert.Equal(ab, PerfilDoJogador.Mesclar(ab, A()));
        Assert.Equal(ab, PerfilDoJogador.Mesclar(ab, B()));
        Assert.Equal(PersistenciaDoPerfil.Salvar(ab), PersistenciaDoPerfil.Salvar(PerfilDoJogador.Mesclar(B(), A())));

        // Empate no instante das preferências: a escolha é arbitrária, mas a mesma nos dois sentidos.
        var x = A() with { PreferenciasAlteradasEm = T1 };
        var y = B();
        Assert.Equal(PerfilDoJogador.Mesclar(x, y), PerfilDoJogador.Mesclar(y, x));
        Assert.Contains(PerfilDoJogador.Mesclar(x, y).Nome, new[] { x.Nome, y.Nome });
    }

    [Fact]
    public void Mesclar_nao_mexe_nos_dois_perfis_de_entrada()
    {
        var a = A();
        var b = B();
        PerfilDoJogador.Mesclar(a, b);
        Assert.Equal(A(), a);
        Assert.Equal(B(), b);
    }

    // ── Rich Presence ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Idioma.Portugues, "No menu", "Jogando um set, 4-3", "Jogando o set 2, 2-5", "Carreira: etapa 3, semifinal", "Online 2x2", "Online 1x1", "Treinando")]
    [InlineData(Idioma.Ingles, "In the menu", "Playing a set, 4-3", "Playing set 2, 2-5", "Career: stage 3, semifinal", "Online 2v2", "Online 1v1", "Training")]
    [InlineData(Idioma.Espanhol, "En el menú", "Jugando un set, 4-3", "Jugando el set 2, 2-5", "Carrera: etapa 3, semifinal", "En línea 2x2", "En línea 1x1", "Entrenando")]
    public void Rich_Presence_nas_tres_linguas(Idioma idioma, string menu, string set, string set2, string carreira, string online, string online1x1, string treino)
    {
        Assert.Equal(menu, PresencaNaSteam.Texto(new NoMenu(), idioma));
        Assert.Equal(set, PresencaNaSteam.Texto(new JogandoUmSet(4, 3), idioma));
        Assert.Equal(set2, PresencaNaSteam.Texto(new JogandoUmSet(2, 5, Set: 2), idioma));
        Assert.Equal(carreira, PresencaNaSteam.Texto(new NaCarreira(3, FaseAlcancada.Semifinal), idioma));
        Assert.Equal(online, PresencaNaSteam.Texto(new NoOnline(2), idioma));
        Assert.Equal(online1x1, PresencaNaSteam.Texto(new NoOnline(1), idioma));
        Assert.Equal(treino, PresencaNaSteam.Texto(new Treinando(), idioma));
    }

    [Fact]
    public void Toda_fase_da_carreira_tem_texto_proprio_em_cada_lingua_e_cabe_no_limite_da_Steam()
    {
        foreach (var idioma in Enum.GetValues<Idioma>())
        {
            var textos = Enum.GetValues<FaseAlcancada>().Select(f => PresencaNaSteam.Texto(new NaCarreira(12, f), idioma)).ToList();
            Assert.Equal(textos.Count, textos.Distinct().Count());
            // A Steam corta valor de Rich Presence acima de 256 caracteres (k_cchMaxRichPresenceValueLength).
            Assert.All(textos, t => Assert.InRange(t.Length, 1, 256));
        }
        Assert.Equal("Carreira: etapa 1, fase de grupos", PresencaNaSteam.Texto(new NaCarreira(1, FaseAlcancada.FaseDeGrupos), Idioma.Portugues));
        Assert.Equal("Career: stage 6, final", PresencaNaSteam.Texto(new NaCarreira(6, FaseAlcancada.Final), Idioma.Ingles));
        Assert.Equal("Carrera: etapa 4, cuartos de final", PresencaNaSteam.Texto(new NaCarreira(4, FaseAlcancada.Quartas), Idioma.Espanhol));
    }

    [Fact]
    public void Estado_de_presenca_impossivel_e_recusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JogandoUmSet(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JogandoUmSet(0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JogandoUmSet(1, 1, Set: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NaCarreira(0, FaseAlcancada.Final));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoOnline(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PresencaNaSteam.Texto(new NoMenu(), (Idioma)99));
    }
}
