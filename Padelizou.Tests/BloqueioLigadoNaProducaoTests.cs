using System.Text.Json;
using Padelizou.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Padelizou.Tests;

// O INTERRUPTOR DO BLOQUEIO, LIGADO (14/10/2026). 🗣️ Felipe: *"publica com o bloqueio ligado"*,
// *"para quem não pagou ou venceu o teste"*. `appsettings.json` é ignorado pelo git (guarda
// segredos) e a configuração de produção mora no servidor — por isso a data vive num arquivo
// RASTREADO, sem segredo nenhum, que o ASP.NET carrega por cima em Production.
//
// ⚠️ Este teste existe porque o interruptor nulo é DORMENTE e silencioso: um JSON com a chave
// escrita errada (`BloqueioAPartirDe`) não dá erro nenhum — o bloqueio só não liga, e ninguém
// percebe até alguém reclamar que professor segue usando sem pagar.
public class BloqueioLigadoNaProducaoTests
{
    private static string Arquivo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "appsettings.Production.json")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Padelizou", "appsettings.Production.json");
    }

    [Fact]
    public void O_arquivo_de_producao_liga_o_bloqueio_com_uma_data_valida()
    {
        var cfg = new ConfigurationBuilder().AddJsonFile(Arquivo()).Build();

        var plano = cfg.GetSection("PlanoProfessor").Get<PlanoProfessorSettings>();

        Assert.NotNull(plano?.BloqueioAPartirDe);
        Assert.Equal(new DateTime(2026, 10, 21), plano!.BloqueioAPartirDe!.Value.Date);
    }

    // Arquivo rastreado vai pro GitHub: nada de segredo nele.
    [Fact]
    public void O_arquivo_de_producao_so_carrega_o_plano_do_professor()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Arquivo()));

        Assert.Equal(new[] { "PlanoProfessor" },
            doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
    }
}
