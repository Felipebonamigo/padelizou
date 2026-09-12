using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — QUANTAS VAGAS CADA GRUPO DÁ: uma régua só.
//
// 🗣️ Felipe, depois de eu reportar a divergência: *"sim, alinha a outra tela também"*.
//
// 🕳️ A TELA `/Torneios/Classificacao` DISCORDAVA DO CHAVEAMENTO, e não em teoria: ela lia
// `torneio.ClassificadosPorGrupo` (que nasce 2 e nenhuma tela edita) enquanto o `AvancoDaChave`
// — quem monta o mata-mata de verdade — lê `categoria.ClassificadosPorGrupo ?? 2`. Numa
// categoria de TIMES, onde o organizador escolhe de 1 a 4 por grupo em `Times.cshtml`, isso é
// defeito NO AR em duas coisas ao mesmo tempo:
//
//   • o VERDE da tabela (`ViewBag.RegraClassificados` pinta `posicao <= N`) marcava 2 linhas
//     quando 4 times passavam — e o time em 3º lia na tela que estava fora;
//   • o painel "o que cada um precisa" simulava com 2 vagas, prometendo um corte que a chave
//     não faria.
//
// ⚠️ A CAUSA RAIZ NÃO ERA A TELA: era o número não ter casa. `Math.Max(1, categoria
// .ClassificadosPorGrupo ?? 2)` estava escrito à mão em dez lugares — quatro serviços/
// controllers e quatro views —, e uma cópia a mais era só questão de tempo. É a mesma lição do
// `ClassificacaoDeGrupos.Ordenar` (a régua da ORDEM) e do `QuemVenceu`: consertar a cópia que
// divergiu e deixar as outras nove é consertar o sintoma.
//
// Agora o NÚMERO tem régua única — `ClassificacaoDeGrupos.VagasPorGrupo` — e um gate mecânico
// aqui embaixo que quebra se alguém escrever o `?? 2` na mão outra vez.
public class VagasPorGrupoSaoUmaReguaSoTests
{
    // ═══════════════ A RÉGUA ═══════════════

    [Fact]
    public void Por_omissao_passam_dois_de_cada_grupo()
    {
        // A regra de sempre, e o que toda categoria comum faz: o campo é nulo fora dos times.
        Assert.Equal(2, ClassificacaoDeGrupos.VagasPorGrupo(new Categoria { ClassificadosPorGrupo = null }));
    }

    [Fact]
    public void A_categoria_de_times_manda_no_proprio_numero()
    {
        Assert.Equal(4, ClassificacaoDeGrupos.VagasPorGrupo(new Categoria { ClassificadosPorGrupo = 4 }));
        Assert.Equal(1, ClassificacaoDeGrupos.VagasPorGrupo(new Categoria { ClassificadosPorGrupo = 1 }));
    }

    [Fact]
    public void Nunca_menos_de_uma_vaga()
    {
        // Zero vagas seria um grupo que não classifica ninguém: a chave nasceria vazia e o
        // torneio pararia no fim dos grupos, sem erro nenhum. O `Math.Max(1, ...)` que vivia
        // copiado nos serviços existia por isso — e faltava nas quatro views.
        Assert.Equal(1, ClassificacaoDeGrupos.VagasPorGrupo(new Categoria { ClassificadosPorGrupo = 0 }));
        Assert.Equal(1, ClassificacaoDeGrupos.VagasPorGrupo(new Categoria { ClassificadosPorGrupo = -3 }));
    }

    [Fact]
    public void Sem_categoria_em_maos_vale_a_regra_de_sempre()
    {
        // As views chamam isto com navegação que pode não ter vindo do Include. Estourar ali
        // derrubaria a página do torneio inteira por causa de um número de exibição.
        Assert.Equal(2, ClassificacaoDeGrupos.VagasPorGrupo(null));
    }

    // ═══════════════ A TELA QUE DIVERGIA ═══════════════

    [Fact]
    public async Task A_classificacao_pinta_de_verde_as_vagas_da_CATEGORIA()
    {
        // 3 times por grupo, 3 classificam (número do organizador). O verde é `posicao <= N`.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = GrupoDeTimes(ctx, classificadosPorGrupo: 3);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.Classificacao(torneio.Id, categoria.Id);

        // ⚠️ 3, e não 2: com `torneio.ClassificadosPorGrupo` (que nasce 2 e ninguém edita) o
        // time em 3º lia na tela que estava fora de uma vaga que a chave ia lhe dar.
        Assert.Equal(3, (int)controller.ViewBag.RegraClassificados);
    }

    [Fact]
    public async Task O_painel_do_que_falta_na_classificacao_simula_com_as_vagas_da_CATEGORIA()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = GrupoDeTimes(ctx, classificadosPorGrupo: 3);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.Classificacao(torneio.Id, categoria.Id);

        var quadros = (Dictionary<string, OQuePrecisaParaClassificar.Quadro>)controller.ViewBag.OQueFalta;
        var quadro = quadros["A"];

        // Com 3 vagas num grupo de 3, o último jogo não decide vaga nenhuma — os três já estão
        // dentro. Simulando com 2 (o defeito), o painel prometia um corte que não existe.
        Assert.All(quadro.Situacoes, s =>
            Assert.Equal(OQuePrecisaParaClassificar.Estado.JaClassificado, s.Estado));
    }

    // ═══════════════ O GATE: NINGUÉM ESCREVE O NÚMERO NA MÃO ═══════════════

    // A divergência não voltou por descuido de uma pessoa: voltou porque o número era fácil de
    // reescrever. Este gate é o que segura a décima primeira cópia — mesma ideia do
    // `GateDeAutorizacaoDosPostsTests`, que varre o assembly em vez de confiar na lembrança.
    [Fact]
    public void O_numero_de_vagas_so_e_escrito_na_regua()
    {
        var fora = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(PastaDoProjeto(), "*.*", SearchOption.AllDirectories))
        {
            var extensao = Path.GetExtension(arquivo);
            if (extensao is not (".cs" or ".cshtml")) continue;
            // As Migrations são fotografias do schema — nunca leem o valor pra decidir nada.
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")) continue;
            // A régua é a casa do número. Aqui o `?? ` é a definição, não uma cópia.
            if (Path.GetFileName(arquivo) == "ClassificacaoDeGrupos.cs") continue;

            var fonte = TestInfra.SemComentarios(File.ReadAllText(arquivo));
            if (fonte.Contains("ClassificadosPorGrupo ??"))
                fora.Add(Path.GetRelativePath(PastaDoProjeto(), arquivo));
        }

        Assert.True(fora.Count == 0,
            "o número de vagas por grupo foi escrito na mão fora da régua "
            + $"(ClassificacaoDeGrupos.VagasPorGrupo): {string.Join(", ", fora)}");
    }

    // Um grupo de TIMES com 3 times, 2 jogos feitos e 1 por jogar — o formato em que o painel
    // "o que cada um precisa" existe. Time é uma Dupla com NomeTime (ver FinalDosTimesTests).
    private static (Torneio torneio, Categoria categoria, Jogador org) GrupoDeTimes(
        DbPadelContext ctx, int classificadosPorGrupo)
    {
        var torneio = new Torneio
        {
            Nome = "Interno dos Times", Codigo = "TIM99", Status = "Fase de Grupos",
            GamesFaseGrupos = 9, SetsFaseGrupos = 1,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria
        {
            Nome = "Times", Codigo = "TIMES", Torneio = torneio,
            DeTimes = true, QuantidadeGrupos = 1, ClassificadosPorGrupo = classificadosPorGrupo,
        };
        ctx.Categorias.Add(categoria);

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        Dupla Time(string nome)
        {
            var d = new Dupla { CategoriaId = categoria.Id, Jogador1Id = org.Id, NomeTime = nome, Grupo = "A" };
            ctx.Duplas.Add(d);
            return d;
        }

        var a = Time("CredHub");
        var b = Time("Target.it");
        var c = Time("Valandro Gestão");
        ctx.SaveChanges();

        void Jogo(Dupla d1, Dupla d2, int? g1, int? g2) => ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = d1.Id, Dupla2Id = d2.Id,
            Fase = "Grupo A", Status = g1 == null ? "Agendada" : "Finalizada",
            GamesDupla1 = g1, GamesDupla2 = g2,
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
        });

        Jogo(a, b, 9, 6);
        Jogo(b, c, 9, 4);
        Jogo(a, c, null, null);
        ctx.SaveChanges();

        return (torneio, categoria, org);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
