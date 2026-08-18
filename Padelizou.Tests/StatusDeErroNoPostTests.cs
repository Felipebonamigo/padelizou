using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Padelizou.Controllers;

namespace Padelizou.Tests;

// Em produção, TODO POST que terminava em erro chegava ao cliente como 400, escondendo o status
// de verdade. Comprovado na build-576-c8edf5e: `GET /rota-que-nao-existe` respondia 404 e o
// `POST` da MESMA rota respondia 400; o webhook do meio de pagamento sem token respondia 400
// tendo o controller devolvido `Unauthorized()` (401), com o log confirmando a recusa.
//
// A causa não está em nenhum controller: as telas de erro não são REDIRECIONAMENTO, são
// REEXECUÇÃO do pipeline — e a reexecução preserva o método da requisição original. Um POST que
// dá 404 vira um POST /Home/NaoEncontrado, que o carimbo antifalsificação global recusa com 400.
// É esse 400 que substitui o status na resposta.
//
// ⚠️ E o defeito tinha DUAS cópias, não uma: o mesmo mecanismo vale pro UseExceptionHandler, e
// por isso um POST que estourava chegava 400 em vez de 500 — nenhum erro de POST aparecia como
// 5xx pra quem chamou de fora. Corrigir só a página do 404 deixaria a metade pior de pé.
//
// ⚠️ Quem tem carimbo NUNCA viu esse defeito: formulário do site e `fetch()` mandam o token, a
// reexecução passa pelo filtro e a pessoa recebe a página amiga com o status certo. Medido antes
// de escolher a correção — é por isso que a saída foi ISENTAR as telas (que não gravam nada) e
// não deixar de reexecutar no POST: deixar de reexecutar tiraria a página amiga de quem hoje a
// vê (inclusive no formulário que estoura) pra consertar quem nunca a recebeu.
public class StatusDeErroNoPostTests
{
    // ── O mecanismo: por que a isenção precisa existir ────────────────────────────────────

    // Monta o pipeline mínimo com o MESMO middleware do Program.cs e devolve como a requisição
    // chegou na reexecução. Sem servidor: o middleware só precisa de um HttpContext.
    private static async Task<(string Metodo, string Caminho, string Query, int Status)> ReexecucaoDe(
        string metodo, int statusDaPrimeiraPassada)
    {
        var servicos = new ServiceCollection().BuildServiceProvider();
        var pipeline = new ApplicationBuilder(servicos);

        pipeline.UseStatusCodePagesWithReExecute("/Home/NaoEncontrado", "?codigo={0}");

        string? metodoNaReexecucao = null, caminhoNaReexecucao = null, queryNaReexecucao = null;

        pipeline.Run(ctx =>
        {
            // Segunda passada: aqui estaria o MVC desenhando a tela de erro.
            if (ctx.Request.Path.StartsWithSegments("/Home/NaoEncontrado"))
            {
                metodoNaReexecucao = ctx.Request.Method;
                caminhoNaReexecucao = ctx.Request.Path;
                queryNaReexecucao = ctx.Request.QueryString.Value;

                // O que a ação NaoEncontrado faz: repete o status original em vez de responder 200.
                ctx.Response.StatusCode = int.Parse(ctx.Request.Query["codigo"]!);
                return Task.CompletedTask;
            }

            // Primeira passada: a rota que não existe (ou a ação que devolveu 401).
            ctx.Response.StatusCode = statusDaPrimeiraPassada;
            return Task.CompletedTask;
        });

        var http = new DefaultHttpContext { RequestServices = servicos };
        http.Request.Method = metodo;
        http.Request.Path = "/rota-que-nao-existe-xyz";
        http.Response.Body = new MemoryStream();

        await pipeline.Build()(http);

        return (metodoNaReexecucao ?? "(nao reexecutou)", caminhoNaReexecucao ?? "",
                queryNaReexecucao ?? "", http.Response.StatusCode);
    }

    [Fact]
    public async Task A_tela_de_erro_e_reexecutada_com_o_METODO_da_requisicao_original()
    {
        var r = await ReexecucaoDe("POST", 404);

        // ⚠️ Este é o fato que obriga a isenção: a tela de erro recebe um POST, não um GET.
        // Se um dia o framework passar a reexecutar como GET, a isenção vira desnecessária —
        // e este teste é quem vai avisar.
        Assert.Equal("POST", r.Metodo);
        Assert.Equal("/Home/NaoEncontrado", r.Caminho);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task O_status_original_chega_na_tela_de_erro_e_volta_pro_cliente(string metodo)
    {
        // 401 é o caso do webhook: o controller recusa o token e quem chamou precisa saber que
        // foi RECUSADO, não que mandou uma requisição malformada.
        var r = await ReexecucaoDe(metodo, 401);

        Assert.Equal("?codigo=401", r.Query);
        Assert.Equal(401, r.Status);
    }

    // ── A regra: toda tela reexecutada tem que estar isenta do carimbo ────────────────────

    [Fact]
    public void Toda_tela_que_o_pipeline_reexecuta_esta_isenta_do_carimbo()
    {
        var caminhos = CaminhosReexecutadosNoProgramCs();

        // Sem este número o teste passaria VAZIO se alguém renomeasse a chamada no Program.cs —
        // ficaria verde justamente no dia em que parou de vigiar alguma coisa. São dois: o
        // UseExceptionHandler (500) e o UseStatusCodePagesWithReExecute (404, 401 e afins).
        Assert.Equal(2, caminhos.Count);

        foreach (var caminho in caminhos)
        {
            var acao = AcaoDe(caminho);

            Assert.True(acao != null,
                $"O Program.cs reexecuta '{caminho}', mas não achei a ação correspondente.");

            Assert.True(acao!.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null,
                $"'{caminho}' é reexecutada pelo pipeline mantendo o método da requisição " +
                "original, então precisa de [IgnoreAntiforgeryToken] — senão o carimbo global " +
                "recusa a reexecução e o 400 dele substitui o status de verdade (401 do " +
                "webhook, 404 de rota inexistente, 500 de erro) na resposta ao cliente.");
        }
    }

    // As duas chamadas do Program.cs que reexecutam o pipeline num caminho nosso. Ler o arquivo
    // é o que faz o teste cobrir uma TERCEIRA tela de erro que alguém acrescente amanhã: uma
    // lista escrita aqui dentro só saberia das duas de hoje.
    private static List<string> CaminhosReexecutadosNoProgramCs()
    {
        var arquivo = Path.Combine(RaizDoRepo(), "Padelizou", "Program.cs");
        var codigo = string.Join("\n", File.ReadAllLines(arquivo)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return Regex.Matches(codigo, @"UseExceptionHandler\(""(?<p>[^""]+)""|UseStatusCodePagesWithReExecute\(""(?<p>[^""]+)""")
            .Select(m => m.Groups["p"].Value)
            .Distinct()
            .ToList();
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Program.cs")))
            dir = dir.Parent;

        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    // "/Home/NaoEncontrado" → HomeController.NaoEncontrado
    private static MethodInfo? AcaoDe(string caminho)
    {
        var partes = caminho.Trim('/').Split('/');
        if (partes.Length != 2) return null;

        var controller = typeof(HomeController).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == partes[0] + "Controller"
                              && typeof(Controller).IsAssignableFrom(t));

        return controller?
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => m.Name == partes[1]);
    }
}
