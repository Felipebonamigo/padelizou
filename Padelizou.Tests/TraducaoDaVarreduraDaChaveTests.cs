using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS CONSULTAS DA VARREDURA REALMENTE VIRAM SQL?
//
// ⚠️ REGRA DA CASA (CLAUDE.md): consulta LINQ nova e não trivial — `Where` depois de projeção,
// navegação através de vários níveis — se confere com `ToQueryString()` contra um provedor
// Npgsql apontado pra lugar nenhum. O EF InMemory do resto da suíte **não traduz nada**: uma
// consulta que o Postgres recusa passa lisa pelos 6.900 testes e estoura na primeira execução
// em produção (aconteceu em 19/08/2026).
//
// A varredura tem exatamente a forma perigosa: ela navega `p.Categoria.TorneioId` — dois níveis
// — de propósito, porque `Partida.TorneioId` é anulável e a categoria é obrigatória.
public class TraducaoDaVarreduraDaChaveTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    // Cada consulta é compilada SOZINHA: `ToQueryString()` gera o SQL sem abrir conexão, então
    // nada aqui espera rede. (Chamar o serviço inteiro seria cego — a primeira consulta falharia
    // por conexão e as seguintes nunca chegariam a ser compiladas.)
    private static void Traduz(Func<DbPadelContext, IQueryable> consulta)
    {
        using var ctx = ContextoPostgres();
        _ = consulta(ctx).ToQueryString();
    }

    [Fact]
    public void A_lista_de_torneios_a_varrer_traduz()
    {
        Traduz(ctx => ctx.Torneios
            .Where(t => t.Status != AprovacaoDeChaves.Pendente
                     && t.Status != "Finalizado"
                     && t.Status != "Inscrições Abertas"
                     && !t.Status.StartsWith("Cancelado"))
            .Select(t => t.Id));
    }

    [Fact]
    public void As_partidas_pelo_caminho_da_categoria_traduzem()
    {
        // A que importa: `p.Categoria.TorneioId` é o join que o InMemory faria em memória sem
        // reclamar de nada.
        Traduz(ctx => ctx.Partidas
            .Where(p => p.Categoria.TorneioId == 1)
            .Select(p => new { p.CategoriaId, p.Fase, p.Status }));
    }

    [Fact]
    public void A_busca_dos_nomes_dos_byes_traduz()
    {
        // A outra consulta nova do dia: os nomes das duplas que folgaram, pra projeção
        // (TorneiosController.ProjetarProximasFasesAsync). Navega pros dois jogadores.
        var ids = new List<int> { 1, 2, 3 };
        Traduz(ctx => ctx.Duplas
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Where(d => ids.Contains(d.Id)));
    }
    // ⚠️ O DEFEITO QUE DERRUBOU A MESA NO MEIO DO ER (12/09/2026).
    //
    // `MontarAberturaDesenhadaAsync` perguntava ao BANCO se a categoria já tem fase além da
    // abertura usando um método C#:
    //
    //     .AnyAsync(p => p.CategoriaId == id && p.Fase != nomeFase
    //                 && ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
    //
    // O Postgres recusa: *"Translation of method 'ChaveamentoMataMata.EhFaseDeMataMata'
    // failed"*. O EF InMemory da suíte executa o método em memória sem reclamar, então os
    // ~7.000 testes passavam e só produção estourava — a mesma família de 19/08/2026.
    //
    // 🕳️ E ELE ERA LATENTE: essa linha só era alcançada por categoria com cruzamento DESENHADO
    // à mão, e nenhuma tinha. No instante em que o desenho passou a valer pra todas
    // (12/09, o congelamento), a guarda virou o caminho de todo mundo e a Mesa passou a dar
    // erro ao finalizar jogo — `POST /Partidas/ControlePlacar` e `POST /Torneios/FinalizarPartida`.
    //
    // O projeto já conhecia a armadilha: ver o comentário em Services/ClassificacaoParaCard.
    [Fact]
    public void A_guarda_de_fase_alem_da_abertura_traduz()
    {
        Traduz(ctx => ctx.Partidas
            .Where(p => p.CategoriaId == 1
                     && p.Fase != "Quartas de Final"
                     && p.Fase != "Fase de Grupos"
                     && !p.Fase.StartsWith("Grupo ")));
    }

    [Fact]
    public void O_metodo_de_fase_NAO_pode_entrar_numa_consulta_ao_banco()
    {
        // A prova de que o perigo é real, e o motivo de a guarda acima ser escrita inline:
        // este é o SQL que produção recusou.
        using var ctx = ContextoPostgres();

        var erro = Assert.Throws<InvalidOperationException>(() =>
            ctx.Partidas
               .Where(p => p.CategoriaId == 1 && ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
               .ToQueryString());

        Assert.Contains("could not be translated", erro.Message, StringComparison.Ordinal);
    }
    // O gate mecânico: a fonte do robô não pode mandar o método de fase pro banco. Teste de
    // FONTE porque a suíte roda em EF InMemory, que traduz tudo em memória — nenhum teste de
    // comportamento daqui pega isso, e foi assim que passou pros ~7.000.
    [Fact]
    public void O_robo_nao_manda_o_metodo_de_fase_pro_banco()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Services")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Services", "RoboDoChaveamento.cs")));

        // `AnyAsync`/`CountAsync`/`Where` que citem o método são consulta ao banco: o Postgres
        // recusa. Em lista já materializada o método é bem-vindo — por isso a busca é pelo
        // par "Async(" + método, e não pelo método sozinho.
        foreach (var trecho in fonte.Split("Async(p =>").Skip(1))
        {
            var ate = trecho[..Math.Min(400, trecho.Length)];
            Assert.DoesNotContain("EhFaseDeMataMata", ate, StringComparison.Ordinal);
            Assert.DoesNotContain("EhFaseDeGrupos", ate, StringComparison.Ordinal);
        }
    }
}
