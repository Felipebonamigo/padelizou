using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 26/10/2026 — O DEPLOY NÃO PODE DEPENDER DA COTA ANÔNIMA DA API DO GITHUB NA VPS.
//
// 🕳️ O DEFEITO, visto no ar: o deploy do PR #387 em `dev` (run 457) ficou 10 minutos recebendo
// **403** e desistiu com "não encontrei build" — com o build já publicado. O `deploy.sh` resolvia
// o sha → `build-N-sha7` perguntando à API do GitHub **sem autenticação** a cada 10 s; a cota
// anônima é de 60 consultas por hora por IP, e dois deploys seguidos na mesma hora a esgotaram.
// Pelo NOME do build passou na hora (run 458): nesse caminho o script nem pergunta.
//
// 🗣️ Felipe: *"corrija o deploy.sh com token e publique"*.
//
// A CORREÇÃO MORA NO WORKFLOW, e não só no script: o `deploy.sh` da VPS é uma cópia instalada à
// mão (infra/vps/README.md) — mudar o arquivo do repositório não muda o que roda no próximo
// deploy. O workflow resolve o build com o token do próprio Actions (cota autenticada, longe da
// VPS) e entrega ao servidor SÓ o nome `build-N-sha7`. Token na linha do ssh também não: ele
// ficaria visível na lista de processos do servidor.
//
// O script ganha o token OPCIONAL (env GITHUB_TOKEN) pra quem roda na mão, por SSH.
public class DeployResolveOBuildComTokenTests
{
    [Fact]
    public void O_workflow_resolve_o_build_autenticado_antes_de_entrar_na_VPS()
    {
        var passo = Passo("- name: Descobrir o build");

        // Autenticado com o token do run — `gh` lê GH_TOKEN.
        Assert.Contains("GH_TOKEN: ${{ github.token }}", passo);
        Assert.Contains("gh api", passo);
        // E entrega o resultado como saída do passo, que o passo do ssh lê.
        Assert.Contains("GITHUB_OUTPUT", passo);
    }

    [Fact]
    public void A_VPS_recebe_so_o_nome_do_build_ja_resolvido()
    {
        var publicar = Passo("- name: ${{ inputs.acao == 'rollback'");

        Assert.Contains("steps.build.outputs.tag", publicar);
        // ⚠️ Nunca mais o input cru: com sha ele faria a VPS voltar a perguntar à API.
        Assert.DoesNotContain("deploy.sh $AMBIENTE $BUILD", publicar);
    }

    [Fact]
    public void O_nome_resolvido_passa_por_formato_fechado_antes_do_ssh()
    {
        // O nome vira argumento de comando remoto: mesmo vindo da API, entra só se for build-N-sha7.
        var passo = Passo("- name: Descobrir o build");

        Assert.Contains("^build-[0-9]+-[0-9a-f]{7}$", passo);
    }

    [Fact]
    public void O_script_usa_token_quando_ele_existe_no_ambiente()
    {
        var script = File.ReadAllText(Path.Combine(Raiz(), "infra", "vps", "deploy.sh"));
        var api = Regex.Match(script, @"api\(\)\s*\{[^\n]*\}|api\(\)\s*\{.*?\n\}", RegexOptions.Singleline).Value;

        Assert.Contains("GITHUB_TOKEN", api);
        Assert.Contains("Authorization: Bearer", api);
    }

    private static string Passo(string inicioDoPasso)
    {
        var yml = File.ReadAllText(Path.Combine(Raiz(), ".github", "workflows", "deploy.yml"));
        var inicio = yml.IndexOf(inicioDoPasso, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Não achei o passo `{inicioDoPasso}` no deploy.yml.");
        var fim = yml.IndexOf("\n      - name:", inicio + 1, StringComparison.Ordinal);
        return fim > inicio ? yml[inicio..fim] : yml[inicio..];
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório subindo a partir de " + AppContext.BaseDirectory);
    }
}
