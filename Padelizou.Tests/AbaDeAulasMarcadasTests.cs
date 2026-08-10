using Microsoft.AspNetCore.Mvc;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A ABA "AULAS MARCADAS" (10/08/2026).
//
// Marcar aula e ver o que já está marcado eram duas telas sem caminho uma pra outra: o menu
// tem um item só ("Aula") e ele caía sempre no formulário. "Minhas aulas" existia, mas só era
// achada por quem passasse pelo perfil ou por um card da Home — quem terminava de marcar não
// tinha de onde conferir o horário depois.
//
// E, achada a tela, ela respondia a pergunta errada: uma lista só, da data MAIOR pra menor,
// que jogava a aula mais distante no futuro pro topo e a de amanhã pro meio do monte.
public class AbaDeAulasMarcadasTests
{
    private static (DbPadelContext ctx, Jogador professor, Jogador aluno, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Marcio", Login = "marcio", Cpf = "66600000011", IsProfessor = true };
        var aluno = new Jogador { Nome = "Maickel Konrad", Login = "maickel", Cpf = "66600000012" };
        ctx.Jogadores.AddRange(professor, aluno);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, aluno, local);
    }

    private static Aula Marcar(DbPadelContext ctx, Jogador professor, Jogador aluno, LocalAula local,
        DateTime quando, string status = "Confirmada", int duracao = 60)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id,
            AlunoId = aluno.Id,
            LocalAulaId = local.Id,
            DataHora = quando,
            DuracaoMinutos = duracao,
            Preco = 100m,
            Status = status,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    private static async Task<MinhasAulasVM> Abrir(DbPadelContext ctx, int alunoId)
    {
        var controller = TestInfra.NovoAulasController(ctx, alunoId);
        var result = (ViewResult)await controller.MinhasAulas();
        return (MinhasAulasVM)result.Model!;
    }

    // O QUE A PESSOA VEM PERGUNTAR: "quando é a minha próxima aula?". A resposta tem que ser
    // a primeira linha — e antes ela era a última das futuras, porque a lista vinha da data
    // maior pra menor.
    [Fact]
    public async Task A_proxima_aula_e_a_primeira_da_lista()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var amanha = DateTime.Now.AddDays(1);
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(30));
        Marcar(ctx, professor, aluno, local, amanha);
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(7));

        var vm = await Abrir(ctx, aluno.Id);

        Assert.Equal(3, vm.Proximas.Count);
        Assert.Equal(amanha, vm.Proximas[0].DataHora);
        Assert.True(vm.Proximas[0].DataHora < vm.Proximas[1].DataHora,
            "As próximas têm que vir da mais perto pra mais longe.");
        Assert.Empty(vm.Anteriores);
    }

    // O histórico é a pergunta OPOSTA ("o que eu fiz ultimamente?"), então a ordem se inverte.
    [Fact]
    public async Task O_que_ja_passou_vem_do_mais_recente_pro_mais_antigo()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var semanaPassada = DateTime.Now.AddDays(-7);
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(-30), "Realizada");
        Marcar(ctx, professor, aluno, local, semanaPassada, "Realizada");
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(2));

        var vm = await Abrir(ctx, aluno.Id);

        Assert.Single(vm.Proximas);
        Assert.Equal(2, vm.Anteriores.Count);
        Assert.Equal(semanaPassada, vm.Anteriores[0].DataHora);
    }

    // ⚠️ O corte olha o FIM da aula, não o início. Sem isso, a aula de 1h30 sumiria da lista
    // de marcadas no minuto seguinte ao começo — com o aluno ainda na quadra, e com o card
    // do professor e o link do WhatsApp junto.
    [Fact]
    public async Task Aula_em_andamento_continua_marcada()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        Marcar(ctx, professor, aluno, local, DateTime.Now.AddMinutes(-30), duracao: 90);

        var vm = await Abrir(ctx, aluno.Id);

        Assert.Single(vm.Proximas);
        Assert.Empty(vm.Anteriores);
    }

    // Aula desmarcada tem data no futuro e não está marcada coisa nenhuma. Se entrasse em
    // "Próximas", a tela mandaria a pessoa pra uma quadra onde não vai ter ninguém.
    [Fact]
    public async Task Aula_futura_desmarcada_sai_das_marcadas()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(3), PoliticaAula.Cancelada);
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(4), PoliticaAula.Recusada);
        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(5), PoliticaAula.Pendente);

        var vm = await Abrir(ctx, aluno.Id);

        // Pendente conta: o professor ainda pode confirmar, e a pessoa precisa saber que pediu.
        Assert.Single(vm.Proximas);
        Assert.Equal(PoliticaAula.Pendente, vm.Proximas[0].Status);
        Assert.Equal(2, vm.Anteriores.Count);
    }

    [Fact]
    public async Task A_aula_de_outro_aluno_nao_aparece()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var outro = new Jogador { Nome = "Rafael", Login = "rafael", Cpf = "66600000013" };
        ctx.Jogadores.Add(outro);
        ctx.SaveChanges();

        Marcar(ctx, professor, aluno, local, DateTime.Now.AddDays(1));
        Marcar(ctx, professor, outro, local, DateTime.Now.AddDays(2));

        var vm = await Abrir(ctx, aluno.Id);

        Assert.Single(vm.Proximas);
        Assert.Equal(aluno.Id, vm.Proximas[0].AlunoId);
    }

    // ---- As abas, no arquivo ----
    //
    // A aba é HTML puro: não passa por controller nenhum, e o defeito seria MUDO — a tela
    // continua abrindo, sem erro em log nenhum, só sem o caminho de uma pra outra. Mesmo
    // espírito do PwaArquivosTests.

    [Fact]
    public void As_duas_telas_de_aula_mostram_as_abas()
    {
        Assert.Contains("_AbasDaAula", Arquivo(Path.Combine("Views", "Aulas", "Solicitar.cshtml")));
        Assert.Contains("_AbasDaAula", Arquivo(Path.Combine("Views", "Aulas", "MinhasAulas.cshtml")));
    }

    [Fact]
    public void As_abas_levam_uma_na_outra()
    {
        var abas = Arquivo(Path.Combine("Views", "Shared", "_AbasDaAula.cshtml"));

        Assert.Contains("asp-action=\"Solicitar\"", abas);
        Assert.Contains("asp-action=\"MinhasAulas\"", abas);

        // A aba acesa sai do action da vez. Passar por parâmetro é o caminho pra uma das duas
        // telas dizer que está na outra — que é pior do que não ter aba.
        Assert.Contains("RouteData", abas);
    }

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
