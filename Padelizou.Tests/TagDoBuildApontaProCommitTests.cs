using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — A TAG DO BUILD TEM QUE APONTAR PRO COMMIT QUE ELA NOMEIA.
//
// 🕳️ O DEFEITO, visto no ar: o `gh release create` do `ci.yml` nascia **sem `--target`**. O
// tarball sai do `github.sha` (certo), mas a TAG GIT o `gh` cria apontando pro topo do branch
// padrão **naquele instante** — não pro commit que rodou. Com dois merges perto, a tag passa a
// apontar pro commit do outro: o `build-1011-ea86749` ficou apontando pro `f3170fe`, o merge do
// PR #164, que entrou 4 minutos depois do #168.
//
// ⚠️ Não troca o que é PUBLICADO — o `deploy.sh` instala o tarball do release, não o que a tag
// aponta. O que quebra é a PROCEDÊNCIA: quem for investigar um build no futuro (`git show` na
// tag, "que código está no ar?") lê um commit que não é o do pacote. É o tipo de pista errada
// que custa uma madrugada justamente no dia em que algo deu errado.
//
// ⚠️ POR QUE TESTE DE FONTE: o `ci.yml` só roda no GitHub, e o que se guarda aqui é uma linha
// de comando — a suíte não tem como executar um deploy. É a mesma escolha dos testes que leem
// Razor: travar na fonte o que não dá pra travar em comportamento. E ele é o PRIMEIRO teste da
// suíte a ler `.github/`, por isso o caminho da raiz mora aqui inteiro.
public class TagDoBuildApontaProCommitTests
{
    [Fact]
    public void O_release_de_build_fixa_o_TARGET_no_commit_que_rodou()
    {
        var passo = PassoDoRelease();

        Assert.Contains("--target", passo);

        // ⚠️ E o alvo é o `github.sha` — o commit que ESTE run compilou. `github.ref` ou
        // `main` reintroduziriam o defeito com outro nome: os dois são "o topo agora".
        Assert.Contains("--target \"${{ github.sha }}\"", passo);
    }

    [Fact]
    public void O_NOME_da_tag_continua_saindo_do_mesmo_commit_do_target()
    {
        // O nome (`build-N-sha7`) e o alvo têm que vir da MESMA fonte, senão volta a existir
        // uma tag cujo nome diz um commit e cujo conteúdo é outro — que é o defeito inteiro.
        var passo = PassoDoRelease();

        Assert.Contains("sha7=$(echo \"${{ github.sha }}\"", passo);
        Assert.Contains("tag=\"build-${{ github.run_number }}-$sha7\"", passo);
    }

    private static string PassoDoRelease()
    {
        var ci = File.ReadAllText(Path.Combine(RaizDoRepositorio(), ".github", "workflows", "ci.yml"));

        // Do NOME do passo, e não do `gh release create`: o `sha7=` que batiza a tag vem
        // ANTES do comando, e é ele que precisa concordar com o `--target`.
        var inicio = ci.IndexOf("- name: Criar release de build", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o passo `Criar release de build` no ci.yml.");

        // Até o próximo passo do workflow (uma linha que começa com "      - name:").
        var fim = ci.IndexOf("\n      - name:", inicio + 1, StringComparison.Ordinal);
        return fim > inicio ? ci[inicio..fim] : ci[inicio..];
    }

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a raiz do repositório (a pasta com .github/workflows) subindo a partir de "
            + AppContext.BaseDirectory);
    }
}
