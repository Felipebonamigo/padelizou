using System.Text.RegularExpressions;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RELATÓRIO DA PARCERIA COM O RANKING e o perfil que dá acesso a ele (10/08/2026, pedido do
// Felipe: uma página que ele e o pessoal do Ranking Brasil enxerguem).
//
// Este arquivo guarda duas coisas que quebram de jeitos diferentes:
//
//  • A TRAVA. É a única credencial do sistema dada a gente de fora da empresa, e errar pra
//    mais aqui não é "um bug" — é o financeiro do Padelizou na tela de um parceiro comercial.
//
//  • A APURAÇÃO. Todo número desta tela é uma DEFINIÇÃO, e a página é lida por quem cobra da
//    gente. "Passou pelo filtro" contando quem nunca foi perguntado seria um erro que ninguém
//    veria — e que viraria compromisso.
public class RelatorioDoRankingRsTests
{
    private static Jogador Com(bool raiz = false, bool geral = false,
                               bool assistente = false, bool parceiro = false) =>
        new()
        {
            Nome = "Fulano", Cpf = "11144477735",
            IsAdminRaiz = raiz, IsAdminGeral = geral,
            IsAssistente = assistente, IsParceiroRanking = parceiro,
        };

    // ── A TRAVA ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parceiro_ve_o_relatorio()
    {
        Assert.True(PoderesNoSistema.PodeVerRelatorioDoRanking(Com(parceiro: true)));
    }

    [Fact]
    public void Parceiro_NAO_ve_o_resto_do_painel()
    {
        // ⚠️ O TESTE MAIS IMPORTANTE DO ARQUIVO. `PodeOlharTudo` destrava o painel inteiro —
        // financeiro, métricas, gestão de qualquer torneio. Se o parceiro entrar nessa régua,
        // uma empresa de fora passa a enxergar o caixa do Padelizou.
        Assert.False(PoderesNoSistema.PodeOlharTudo(Com(parceiro: true)));
    }

    [Fact]
    public void Parceiro_NAO_edita_nada()
    {
        Assert.False(PoderesNoSistema.PodeEditarTudo(Com(parceiro: true)));
    }

    [Fact]
    public void A_credencial_IsAdmin_do_cracha_NAO_inclui_o_parceiro()
    {
        var cracha = IdentidadeJogador.ClaimsDe(Com(parceiro: true));

        Assert.Equal("false", cracha.First(c => c.Type == "IsAdmin").Value);
        Assert.Equal("false", cracha.First(c => c.Type == "Assistente").Value);
        Assert.Equal("true", cracha.First(c => c.Type == "ParceiroRanking").Value);
    }

    [Fact]
    public void Parceiro_cai_direto_no_relatorio_ao_entrar_no_painel()
    {
        // Sem este desvio ele seria mandado pro /Auth/Perfil, que no subdomínio do painel é um
        // beco — e a leitura dele seria "o acesso não funciona".
        Assert.True(PoderesNoSistema.SoVeORelatorioDoRanking(Com(parceiro: true)));
    }

    [Fact]
    public void Admin_que_TAMBEM_e_parceiro_continua_com_o_painel_inteiro()
    {
        // A flag soma, não substitui: o Felipe se marcando parceiro pra conferir a tela não
        // pode perder o painel — e nem ser redirecionado pra fora dele.
        var dono = Com(raiz: true, parceiro: true);

        Assert.True(PoderesNoSistema.PodeOlharTudo(dono));
        Assert.True(PoderesNoSistema.PodeEditarTudo(dono));
        Assert.False(PoderesNoSistema.SoVeORelatorioDoRanking(dono));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Admins_e_assistente_tambem_leem_o_relatorio(bool raiz, bool geral, bool assistente)
    {
        Assert.True(PoderesNoSistema.PodeVerRelatorioDoRanking(
            Com(raiz: raiz, geral: geral, assistente: assistente)));
    }

    [Fact]
    public void Jogador_comum_nao_ve_o_relatorio()
    {
        Assert.False(PoderesNoSistema.PodeVerRelatorioDoRanking(Com()));
        Assert.False(PoderesNoSistema.PodeVerRelatorioDoRanking(null));
        Assert.False(PoderesNoSistema.SoVeORelatorioDoRanking(null));
    }

    [Fact]
    public void A_tela_do_relatorio_nao_tem_nenhum_formulario()
    {
        // ⚠️ A trava do parceiro é o verbo HTTP, igual à do assistente — e ela só é honesta
        // enquanto esta tela não oferecer nada pra clicar. Um <form> aqui seria um POST que a
        // porta do relatório não cobre.
        var view = File.ReadAllText(Path.Combine(
            PastaDoProjeto(), "Views", "Admin", "RankingRsRelatorio.cshtml"));

        Assert.DoesNotContain("<form", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("method=\"post\"", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_porta_do_parceiro_abre_uma_acao_so()
    {
        // A flag é aceita num lugar só do painel. Se ela aparecer numa segunda porta, ou o
        // desenho mudou de propósito — e aí este teste muda junto —, ou alguém acabou de
        // alargar o acesso sem perceber.
        var controllers = Directory.GetFiles(
            Path.Combine(PastaDoProjeto(), "Controllers"), "AdminController*.cs");

        var portas = controllers
            .SelectMany(File.ReadLines)
            .Count(l => l.Contains("PodeVerRelatorioDoRanking"));

        Assert.Equal(1, portas);
    }

    // ── A APURAÇÃO ────────────────────────────────────────────────────────────────────

    private static ConsultaAoRankingRs Consulta(string resultado, string cpf = "11144477735",
        bool encontrado = true, DateTime? em = null, int categoriaId = 4) =>
        new()
        {
            Cpf = cpf, CategoriaId = categoriaId, NomeConsultado = "Silvano", Resultado = resultado,
            EncontradoNoRanking = encontrado, CriadoEm = em ?? new DateTime(2026, 8, 10),
        };

    [Fact]
    public void Passou_pelo_filtro_e_SO_quem_foi_perguntado_e_liberado()
    {
        // ⚠️ A definição inteira da tela mora aqui. As três situações abaixo produzem o mesmo
        // silêncio no banco de antes — e contá-las como aprovação transformaria "ninguém
        // perguntou nada" em selo de qualidade, numa página que o parceiro lê.
        Assert.True(RelatorioDoRankingRs.PassouPeloFiltro(Consulta(ResultadoDaConsulta.Aprovado)));

        Assert.False(RelatorioDoRankingRs.PassouPeloFiltro(Consulta(ResultadoDaConsulta.Barrado)));
        Assert.False(RelatorioDoRankingRs.PassouPeloFiltro(Consulta(ResultadoDaConsulta.SemDePara)));
        Assert.False(RelatorioDoRankingRs.PassouPeloFiltro(Consulta(ResultadoDaConsulta.SemResposta)));
    }

    [Fact]
    public void Conferido_e_aprovado_mais_barrado_e_nada_mais()
    {
        Assert.True(RelatorioDoRankingRs.FoiConferida(Consulta(ResultadoDaConsulta.Aprovado)));
        Assert.True(RelatorioDoRankingRs.FoiConferida(Consulta(ResultadoDaConsulta.Barrado)));

        Assert.False(RelatorioDoRankingRs.FoiConferida(Consulta(ResultadoDaConsulta.SemDePara)));
        Assert.False(RelatorioDoRankingRs.FoiConferida(Consulta(ResultadoDaConsulta.SemResposta)));

        Assert.True(RelatorioDoRankingRs.PassouSemConferencia(Consulta(ResultadoDaConsulta.SemDePara)));
        Assert.True(RelatorioDoRankingRs.PassouSemConferencia(Consulta(ResultadoDaConsulta.SemResposta)));
    }

    // Silvano jogando as categorias 4 e 6, e as consultas que existirem sobre ele.
    private static List<RelatorioDoRankingRs.Inscrito> JuntarSilvano(
        IEnumerable<ConsultaAoRankingRs> consultas, params int[] categoriasEmQueEntrou) =>
        RelatorioDoRankingRs.Juntar(
            new[] { new AcertoComORankingRs.PessoaCobrada(7, "Silvano", new[] { "4ª Masc", "6ª Masc" }) },
            new Dictionary<int, string> { [7] = "11144477735" },
            new Dictionary<int, HashSet<int>> { [7] = new(categoriasEmQueEntrou) },
            consultas);

    [Fact]
    public void Quem_joga_duas_categorias_e_UMA_pessoa_no_relatorio()
    {
        // Mesma régua do dinheiro (Services/AcertoComORankingRs): a parceria conta pessoa.
        var inscritos = JuntarSilvano(
            new[] { Consulta(ResultadoDaConsulta.Aprovado, categoriaId: 4) }, 4, 6);

        var unico = Assert.Single(inscritos);
        Assert.Equal(2, unico.Categorias.Count);
        Assert.True(unico.Aprovado);
    }

    [Fact]
    public void Conferido_numa_categoria_vale_pela_pessoa()
    {
        // ⚠️ Ela jogou duas categorias: numa a validação rodou, na outra a categoria não tinha
        // de-para. Sem a ordenação que faz a conferida ganhar, o rótulo da pessoa dependeria de
        // qual linha o agrupamento pegasse primeiro — e o número da tela mudaria sozinho.
        var inscritos = JuntarSilvano(new[]
        {
            // A "sem de-para" é a MAIS RECENTE de propósito: se a régua fosse só a data,
            // ela venceria e a pessoa apareceria como não conferida.
            Consulta(ResultadoDaConsulta.SemDePara, categoriaId: 6, em: new DateTime(2026, 8, 10, 18, 0, 0)),
            Consulta(ResultadoDaConsulta.Aprovado, categoriaId: 4, em: new DateTime(2026, 8, 10, 9, 0, 0)),
        }, 4, 6);

        Assert.True(Assert.Single(inscritos).Conferido);
    }

    [Fact]
    public void Consulta_de_categoria_em_que_a_pessoa_NAO_entrou_nao_vale()
    {
        // ⚠️ ACHADO RODANDO O APP, e o defeito mais caro que esta tela podia ter: não quebra
        // nada e afirma o que não aconteceu.
        //
        // A consulta roda ANTES de a inscrição existir, e regras posteriores ainda podem
        // recusá-la (uma categoria por jogador, inscrição repetida, vagas). Sobra no banco um
        // "Aprovado" da categoria 9, em que ela nunca entrou. Casando só por CPF, esse
        // aprovado grudava nela na categoria que ela de fato joga — e a tela dizia "aprovado
        // pelo ranking" sobre uma categoria que ninguém perguntou.
        var inscritos = JuntarSilvano(
            new[] { Consulta(ResultadoDaConsulta.Aprovado, categoriaId: 9) },
            // Ela entrou só na 4 e na 6.
            4, 6);

        var unico = Assert.Single(inscritos);
        Assert.False(unico.Aprovado);
        Assert.False(unico.Conferido);
        Assert.Equal("Sem registro de consulta", unico.Situacao);
    }

    [Fact]
    public void Inscrito_sem_consulta_registrada_nao_conta_como_aprovado()
    {
        // O buraco entre a integração (07/08) e o registro (10/08). Ele aparece na tela como
        // "sem registro de consulta" — e nunca como aprovação.
        var inscritos = JuntarSilvano(Array.Empty<ConsultaAoRankingRs>(), 4, 6);

        var unico = Assert.Single(inscritos);
        Assert.False(unico.Aprovado);
        Assert.False(unico.Conferido);
        Assert.Equal("Sem registro de consulta", unico.Situacao);
    }

    // ── OS TOTAIS ─────────────────────────────────────────────────────────────────────

    private static RelatorioDoRankingRs.LinhaDoTorneio Linha(
        string[] resultados, decimal valor, AcertoRankingRs? acerto = null,
        params string[] situacoesDeBloqueio)
    {
        var inscritos = resultados.Select((r, i) => new RelatorioDoRankingRs.Inscrito(
            i, $"Atleta {i}", $"cpf{i}", new[] { "6ª Masc" }, Consulta(r, $"cpf{i}"))).ToList();

        var barrados = situacoesDeBloqueio.Select(s => new RelatorioDoRankingRs.Barrado(
            new BloqueioDoRanking
            {
                Cpf = "999", NomeConsultado = "Barrado", Motivo = "pontos", Situacao = s,
            }, "Torneio", "6ª Masc")).ToList();

        return new RelatorioDoRankingRs.LinhaDoTorneio(
            new Torneio { Nome = "Torneio", Codigo = "T1", Status = "Encerrado" },
            inscritos, barrados, valor, acerto);
    }

    [Fact]
    public void Os_totais_separam_aprovado_de_quem_passou_sem_conferencia()
    {
        var totais = RelatorioDoRankingRs.Somar(new[]
        {
            Linha(new[]
            {
                ResultadoDaConsulta.Aprovado,
                ResultadoDaConsulta.Aprovado,
                ResultadoDaConsulta.SemDePara,
                ResultadoDaConsulta.SemResposta,
            }, 4m),
        });

        Assert.Equal(4, totais.Inscritos);
        Assert.Equal(2, totais.Aprovados);
        Assert.Equal(2, totais.Conferidos);
        Assert.Equal(2, totais.SemConferencia);
        Assert.Equal(50, totais.CoberturaEmPorcento);
    }

    [Fact]
    public void Sem_ninguem_inscrito_a_cobertura_nao_se_aplica()
    {
        // ⚠️ 0% diria que a integração falhou em conferir gente que não existe. A tela precisa
        // do traço, não do zero.
        var totais = RelatorioDoRankingRs.Somar(new[] { Linha(Array.Empty<string>(), 0m) });

        Assert.Null(totais.CoberturaEmPorcento);
    }

    [Fact]
    public void Recusa_mantida_e_liberacao_do_organizador_sao_contadas_separadas()
    {
        var totais = RelatorioDoRankingRs.Somar(new[]
        {
            Linha(new[] { ResultadoDaConsulta.Aprovado }, 1m, null,
                SituacaoDoBloqueio.Mantido, SituacaoDoBloqueio.Liberado, SituacaoDoBloqueio.Pendente),
        });

        Assert.Equal(3, totais.Barrados);
        // Pendente ainda barra: só "Liberado" desfaz a recusa.
        Assert.Equal(2, totais.RecusasQueFicaramDePe);
        Assert.Equal(1, totais.LiberadosPeloOrganizador);
    }

    [Fact]
    public void O_valor_ja_acertado_vem_da_fotografia_e_nao_da_conta_de_agora()
    {
        // ⚠️ Recalcular o que já foi pago faria o total da tela mudar sozinho toda vez que
        // alguém se inscrevesse num torneio antigo — e o parceiro veria a dívida "voltando".
        var acerto = new AcertoRankingRs { TorneioId = 1, PessoasCobradas = 3, Valor = 3m, AcertadoEm = DateTime.Now };

        var totais = RelatorioDoRankingRs.Somar(new[]
        {
            Linha(new[] { ResultadoDaConsulta.Aprovado }, valor: 99m, acerto: acerto),
            Linha(new[] { ResultadoDaConsulta.Aprovado }, valor: 5m),
        });

        Assert.Equal(3m, totais.ValorJaAcertado);
        Assert.Equal(5m, totais.ValorEmAberto);
        Assert.Equal(8m, totais.ValorTotal);
    }

    // ── O CPF NA TELA ─────────────────────────────────────────────────────────────────

    [Fact]
    public void O_parceiro_ve_o_CPF_mascarado_e_o_dono_ve_inteiro()
    {
        const string cpf = "11144477735";

        Assert.Equal(cpf, RelatorioDoRankingRs.CpfNaTela(cpf, inteiro: true));

        var mascarado = RelatorioDoRankingRs.CpfNaTela(cpf, inteiro: false);
        Assert.DoesNotContain("111", mascarado);
        Assert.DoesNotContain("77735", mascarado);
        // Três dígitos do meio bastam pra separar homônimos, que é pra isso que serve na tela.
        Assert.Contains("444", mascarado);
    }

    [Fact]
    public void CPF_vazio_nao_estoura_nem_inventa_mascara()
    {
        Assert.Equal("—", RelatorioDoRankingRs.CpfNaTela(null, inteiro: false));
        Assert.Equal("—", RelatorioDoRankingRs.CpfNaTela("", inteiro: true));
        Assert.Equal("•••", RelatorioDoRankingRs.CpfNaTela("123", inteiro: false));
    }

    // ── A TELA DIZ A RÉGUA ────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_explica_o_que_passou_pelo_filtro_significa()
    {
        // Um número sem régua, numa página que o parceiro lê, vira compromisso. Este teste
        // existe pra que a explicação não seja apagada por engano numa faxina de layout.
        var view = File.ReadAllText(Path.Combine(
            PastaDoProjeto(), "Views", "Admin", "RankingRsRelatorio.cshtml"));

        var semComentarios = Regex.Replace(view, @"@\*.*?\*@", "", RegexOptions.Singleline);

        Assert.Contains("sem conferência", semComentarios);
        Assert.Contains("Barrado não é inscrito", semComentarios);
        Assert.Contains("pessoa, não inscrição", semComentarios);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Controllers");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
