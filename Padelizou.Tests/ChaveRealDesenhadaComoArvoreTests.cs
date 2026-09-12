using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — A CHAVE DE VERDADE VOLTA A TER AS LINHAS: UM DESENHO SÓ, ANTES E DEPOIS.
//
// 🗣️ Felipe, com o print da 4ª Categoria Masculina do ER já no mata-mata: *"e ele mudou o
// visual quando terminou a chave, era para manter como estava, tava bom"*.
//
// 🕳️ A ABA TINHA DOIS DESENHOS e trocava de um pro outro sozinha no dia em que o mata-mata
// nascia: a PRÉVIA era a árvore deitada com as linhas de ligação (`_ChaveProjetadaArvore`,
// 11/09/2026) e a CHAVE REAL eram fases empilhadas em cartões (`_ChaveDoMataMata`). O
// STATUS.md de 11/09 prometia justamente o contrário — *"no dia em que os grupos acabam a
// tela não muda de cara, os cartões só ganham nome e placar"* — e a promessa valia só pro
// CARTÃO: o esqueleto ao redor dele era outro arquivo, com outra geometria.
//
// ✅ Agora é UM PARTIAL SÓ (`_ChaveDoMataMata`), alimentado pelos dois montadores, e a
// geometria continua sendo a conta única de Services/ArvoreDaChave. Pra isso faltava um dado:
// o quadro real CALCULA de qual jogo vem cada lado e jogava fora essa informação assim que a
// vaga virava jogo — sem ela a linha da semifinal real não tem onde se prender.
public class ChaveRealDesenhadaComoArvoreTests
{
    private int _id = 1;

    private Dupla DuplaQualquer() => new() { Id = _id++, Jogador1Id = _id * 10 + 1, Jogador2Id = _id * 10 + 2 };

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

    private List<Partida> QuartasDeOito() => new()
    {
        Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
    };

    // ── A procedência que faltava nas vagas REAIS ───────────────────────────────────

    [Fact]
    public void Vaga_real_guarda_de_qual_jogo_vem_cada_lado()
    {
        // As quartas terminaram e as semifinais já existem como PARTIDA: é exatamente aqui
        // que a informação se perdia, porque a vaga com jogo não guardava mais a procedência.
        var jogos = QuartasDeOito();
        foreach (var j in jogos)
        {
            j.Status = "Finalizada";
            j.VencedorId = j.Dupla1Id;
        }
        jogos.Add(Jogo("Semifinal", jogos[0].Dupla1!, jogos[3].Dupla1!));
        jogos.Add(Jogo("Semifinal", jogos[1].Dupla1!, jogos[2].Dupla1!));

        var fases = QuadroDoMataMata.Montar(jogos, [], meuJogadorId: null);

        // Quem abre a chave não vem de lugar nenhum — é a primeira rodada.
        Assert.All(fases[0].Vagas, v => Assert.Equal((null, null), (v.VemDoJogo1, v.VemDoJogo2)));

        // E as semifinais REAIS carregam o mesmo pareamento primeiro x último que as vagas
        // futuras já mostravam em "quem ganhar o jogo 4".
        Assert.Equal((1, 4), (fases[1].Vagas[0].VemDoJogo1, fases[1].Vagas[0].VemDoJogo2));
        Assert.Equal((2, 3), (fases[1].Vagas[1].VemDoJogo1, fases[1].Vagas[1].VemDoJogo2));
    }

    [Fact]
    public void Vaga_real_alimentada_por_bye_tem_um_lado_sem_procedencia()
    {
        // 2 jogos de abertura + 2 byes: a semifinal real cruza o vencedor do jogo 1 com a
        // dupla que folgou. Quem folgou não vem de jogo nenhum — e é a AUSÊNCIA de linha
        // chegando na vaga que conta isso no desenho.
        var jogos = new List<Partida>
        {
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
            Jogo("Quartas de Final", DuplaQualquer(), DuplaQualquer()),
        };
        var byeMelhor = DuplaQualquer();
        var byePior = DuplaQualquer();
        jogos[0].Status = "Finalizada";
        jogos[0].VencedorId = jogos[0].Dupla1Id;
        jogos.Add(Jogo("Semifinal", jogos[0].Dupla1!, byePior));

        var fases = QuadroDoMataMata.Montar(jogos, [byeMelhor, byePior], meuJogadorId: null);

        var semiReal = fases[1].Vagas[0];
        Assert.NotNull(semiReal.Jogo);
        Assert.Equal(1, semiReal.VemDoJogo1);
        Assert.Null(semiReal.VemDoJogo2);
    }

    // ── A geometria: a chave real entra no MESMO quadro da prévia ───────────────────

    [Fact]
    public void A_chave_real_vira_arvore_com_as_ligacoes_e_a_ordem_do_quadro()
    {
        var quadro = QuadroDoMataMata.Geometria(
            QuadroDoMataMata.Montar(QuartasDeOito(), [], meuJogadorId: null));

        // A conta é a de ArvoreDaChave: a final cobre a chave inteira, e a ordem dentro da
        // primeira rodada é 1, 4, 2, 3 — a numérica faria as linhas se cruzarem no meio.
        Assert.Equal(4, quadro.Colunas);
        Assert.Equal(4, quadro.Rodadas[^1].Vagas.Single().Largura);
        Assert.Equal([1, 4, 2, 3], quadro.Rodadas[0].Vagas.Select(v => v.Jogo.Numero));

        // A semifinal tem duas linhas chegando (cotovelo); quem abre a chave não tem nenhuma.
        var semi = quadro.Rodadas[1].Vagas.First(v => v.Jogo.Numero == 5);
        var liga = Assert.IsType<(bool Reta, string Estilo)>(quadro.Ligacao(semi));
        Assert.False(liga.Reta);
        Assert.Null(quadro.Ligacao(quadro.Rodadas[0].Vagas[0]));
    }

    [Fact]
    public void A_previa_entra_no_mesmo_formato_da_chave_real()
    {
        var rodadas = ChaveProjetada.MontarCompleta(["Grupo A", "Grupo B"], 2, [4, 4]);

        var fases = QuadroDoMataMata.DaPrevia(rodadas);

        // Prévia é chave SEM jogo nenhum criado — o cartão é o mesmo, o conteúdo é o rótulo.
        Assert.All(fases.SelectMany(f => f.Vagas), v => Assert.Null(v.Jogo));
        Assert.Equal("1º do Grupo A", fases[0].Vagas[0].Lado1!.Rotulo);

        // E a procedência vem junto, que é o que a linha desenha.
        var final = fases[^1].Vagas.Single();
        Assert.Equal((1, 2), (final.VemDoJogo1, final.VemDoJogo2));

        // O sufixo "(passou direto)" existe pra quem lê a prévia em LISTA; na árvore quem
        // conta isso é a ausência de linha, e o Felipe pediu pra tirar (11/09/2026).
        Assert.DoesNotContain("passou direto",
            string.Join(" ", fases.SelectMany(f => f.Vagas)
                .SelectMany(v => new[] { v.Lado1?.Rotulo, v.Lado2?.Rotulo })));
    }

    // ── UM desenho só: a tela não muda de cara quando o mata-mata nasce ─────────────
    //
    // ⚠️ POR QUE TESTE DE FONTE: nada na suíte renderiza Razor. Sem estas travas, um segundo
    // desenho volta a nascer ao lado do primeiro e NENHUM outro teste fica vermelho — que é
    // exatamente como esta queixa apareceu.

    [Fact]
    public void A_aba_desenha_a_previa_e_a_chave_real_com_o_mesmo_partial()
    {
        var details = File.ReadAllText(Path.Combine(Views(), "Details.cshtml"));

        Assert.Equal(2, Vezes(details, "<partial name=\"_ChaveDoMataMata\""));
        Assert.DoesNotContain("_ChaveProjetadaArvore", details);
        Assert.False(File.Exists(Path.Combine(Views(), "_ChaveProjetadaArvore.cshtml")),
            "O partial só da prévia voltou a existir: são dois desenhos de novo.");
    }

    // 🗣️ Felipe, com a chave nova no ar: *"esse 'folgou a primeira rodada' nao ficou legal"* e,
    // logo depois, *"até pode ter algo, mas menor, que nao fique chamando tanto a atenção"*.
    //
    // 🕳️ O QUE CHAMAVA ATENÇÃO ERA A COR, e não a informação: o selo era uma pílula `--pdz-lime`
    // com texto marinho em CAIXA ALTA — o mesmo verde que nesta tela significa **o seu caminho**
    // (`.pdz-chave-vaga-minha`, `.pdz-chave-selomeu`). Numa chave de 12 duplas eram quatro
    // pílulas verdes disputando o olho com o caminho pintado de quem está olhando.
    //
    // ✅ A marca FICA, miúda e apagada: texto pequeno na cor de apoio, sem fundo e sem caixa
    // alta. Quem procura acha; quem está seguindo o próprio caminho não tropeça nela.
    [Fact]
    public void Quem_folgou_a_primeira_rodada_tem_uma_marca_discreta()
    {
        var partial = File.ReadAllText(Path.Combine(Views(), "_ChaveDoMataMata.cshtml"));
        var css = File.ReadAllText(Path.Combine(Views(), "..", "..", "wwwroot", "css", "site.css"));

        Assert.Contains("pdz-chave-selobye", partial);

        // ⚠️ UMA REGRA SÓ. Havia DUAS `.pdz-chave-selobye` no arquivo, em lugares distantes, e a
        // de baixo vencia calada — mexer na de cima não mudava nada na tela.
        Assert.Equal(1, Vezes(css, ".pdz-chave-selobye {"));

        var inicio = css.IndexOf(".pdz-chave-selobye {", StringComparison.Ordinal);
        var regra = css[inicio..css.IndexOf('}', inicio)];

        // O que fazia a marca gritar: fundo lime e caixa alta. Nenhum dos dois volta.
        Assert.DoesNotContain("background", regra);
        Assert.DoesNotContain("text-transform: uppercase", regra);

        // E ela fica na cor de apoio — nunca no verde, que aqui quer dizer outra coisa.
        Assert.Contains("var(--pdz-muted)", regra);
        Assert.DoesNotContain("--pdz-lime", regra);
        Assert.DoesNotContain("--padel-green", regra);

        // ⚠️ E o selo de PROCEDÊNCIA fica como está: "venceu o jogo 4" é outra coisa — é o nome
        // de quem JÁ passou aparecendo antes de a rodada fechar, e nenhuma linha conta isso.
        Assert.Contains("pdz-chave-selovem", partial);
    }

    [Fact]
    public void A_chave_usa_o_trilho_da_arvore_e_nao_as_fases_empilhadas()
    {
        var partial = File.ReadAllText(Path.Combine(Views(), "_ChaveDoMataMata.cshtml"));

        // O trilho com as linhas — a rodada é COLUNA e a vaga ocupa LINHAS.
        Assert.Contains("pdz-chd-trilho", partial);
        Assert.Contains("pdz-chd-liga", partial);
        Assert.Contains("grid-row:", partial);

        // E as fases empilhadas não voltam por cima: era esse esqueleto que substituía o
        // quadro no dia em que os grupos acabavam.
        Assert.DoesNotContain("pdz-mm-fase", partial);
        Assert.DoesNotContain("pdz-mm-grade", partial);
    }

    private static int Vezes(string texto, string agulha)
    {
        int n = 0;
        for (int i = texto.IndexOf(agulha, StringComparison.Ordinal); i >= 0;
             i = texto.IndexOf(agulha, i + agulha.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static string Views()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return Path.Combine(dir!.FullName, "Padelizou", "Views", "Torneios");
    }
}
