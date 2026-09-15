using Xunit;

namespace Padelizou.Tests;

// 15/09/2026 — O DEPLOY PRECISA FUNCIONAR COM O REPOSITÓRIO PRIVADO.
//
// 🕳️ O DEFEITO: o `deploy.sh` fala com o GitHub em dois pontos — lista os releases pra
// descobrir a tag, e baixa o `padelizou.tar.gz` — e os DOIS iam sem credencial nenhuma.
// Enquanto o repositório é público isso passa; no instante em que ele vira privado os dois
// devolvem **404** e a publicação para. Não é hipótese: é o que acontece no clique de
// Settings → General → Danger Zone → Change visibility, sem aviso e sem nada vermelho antes.
//
// ⚠️ E 404 é o MESMO código de "esse build não existe" — sem a conferência de acesso que
// este teste cobra, um token vencido vira "não encontrei build pra ''" e manda procurar no
// CI, que está verde. É a falha calada clássica: a mensagem aponta pro lugar errado.
//
// ⚠️ A URL de browser do release (`/releases/download/TAG/arquivo`) NÃO funciona em
// repositório privado NEM COM TOKEN — o GitHub só entrega o asset pelo endpoint da API
// (`/releases/assets/ID` com `Accept: application/octet-stream`). Pôr o cabeçalho e manter a
// URL antiga é o meio-conserto que parece pronto e continua quebrado.
//
// ⚠️ POR QUE TESTE DE FONTE: o `deploy.sh` roda no VPS, por SSH, contra a API do GitHub — a
// suíte não tem como executar um deploy, e nem bash existe na máquina Windows. Mesma escolha
// do `TagDoBuildApontaProCommitTests` ao lado: travar na fonte o que não dá pra travar em
// comportamento.
public class DeployComRepositorioPrivadoTests
{
    [Fact]
    public void O_pacote_NAO_e_baixado_pela_URL_de_browser_do_release()
    {
        // A que devolve 404 em repositório privado mesmo com token. Se ela voltar, o deploy
        // volta a quebrar no dia em que a visibilidade mudar — e só naquele dia.
        Assert.DoesNotContain("releases/download/", Script());
    }

    [Fact]
    public void O_pacote_e_baixado_pelo_endpoint_de_asset_da_API()
    {
        var script = Script();

        Assert.Contains("releases/assets", script);

        // Sem este Accept a API devolve o JSON que DESCREVE o asset, não o .tar.gz. O `tar`
        // logo abaixo morreria com "not in gzip format" — erro que não fala de permissão
        // nenhuma e manda investigar o pacote, que está certo.
        Assert.Contains("Accept: application/octet-stream", script);
    }

    [Fact]
    public void O_token_sai_de_um_arquivo_e_NUNCA_da_linha_de_comando()
    {
        var script = Script();

        Assert.Contains("ARQUIVO_TOKEN", script);

        // Pela ENTRADA do curl (`-K -`): argumento de processo se lê com `ps` de qualquer
        // usuário da máquina, e o arquivo (600, do root) não.
        Assert.Contains("curl -K -", script);

        foreach (var linha in ComandosDoScript())
        {
            var naLinhaDeComando = linha.Contains("Authorization", StringComparison.Ordinal)
                                && linha.Contains("-H", StringComparison.Ordinal);
            Assert.False(naLinhaDeComando,
                "O token iria em `-H`, e argumento de processo se lê com `ps`: " + linha.Trim());
        }
    }

    [Fact]
    public void Nenhuma_chamada_ao_github_escapa_do_curl_que_leva_o_token()
    {
        // O guarda contra o meio-conserto: uma chamada nova ao GitHub com `curl` pelado
        // funciona hoje (público) e quebra calada no dia da virada, igual às duas de agora.
        foreach (var linha in ComandosDoScript())
        {
            if (!linha.Contains("curl", StringComparison.Ordinal)) continue;

            var falaComOGithub = linha.Contains("github.com", StringComparison.Ordinal)
                              || linha.Contains("$REPO", StringComparison.Ordinal)
                              || linha.Contains("ASSET", StringComparison.Ordinal);
            if (!falaComOGithub) continue;

            Assert.True(linha.Contains("curl_github", StringComparison.Ordinal),
                "Chamada ao GitHub fora do `curl_github`, então sem token: " + linha.Trim());
        }
    }

    [Fact]
    public void O_404_de_repositorio_privado_nao_se_confunde_com_build_inexistente()
    {
        var script = Script();

        // A conferência bate na RAIZ do repositório, e vem ANTES de procurar a tag: 404 aqui
        // é falta de acesso, e só; 404 lá adiante é ambíguo entre acesso e build inexistente.
        var acesso = script.IndexOf("api.github.com/repos/$REPO\"", StringComparison.Ordinal);
        var procuraDaTag = script.IndexOf("releases?per_page=30", StringComparison.Ordinal);

        Assert.True(acesso >= 0, "Não achei a conferência de acesso ao repositório.");
        Assert.True(procuraDaTag > acesso,
            "A conferência de acesso tem que vir ANTES de procurar a tag, senão o 404 continua ambíguo.");

        // E a mensagem diz onde mexer, em vez de mandar olhar o CI.
        Assert.Contains("privado", script);
        Assert.Contains("$ARQUIVO_TOKEN", script);
    }

    [Fact]
    public void O_healthcheck_do_proprio_site_nao_leva_o_token_do_github()
    {
        var script = Script();
        Assert.Contains("curl_github", script);

        // `$URL` é padelizou.com.br / dev.padelizou.com.br. Mandar o `Authorization` do
        // GitHub pra lá entregaria o token ao próprio app (e ao log do proxy) sem precisão
        // nenhuma — o /healthz é aberto.
        foreach (var linha in ComandosDoScript())
        {
            if (!linha.Contains("\"$URL\"", StringComparison.Ordinal)) continue;

            Assert.False(linha.Contains("curl_github", StringComparison.Ordinal),
                "O healthcheck do site não pode levar o token do GitHub: " + linha.Trim());
        }
    }

    private static string Script()
        => File.ReadAllText(Path.Combine(RaizDoRepositorio(), "infra", "vps", "deploy.sh"));

    private static IEnumerable<string> ComandosDoScript()
    {
        // Junta as continuações (`\` no fim da linha) antes de olhar: o `curl` e a URL dele
        // moram em linhas diferentes, e uma conferência linha a linha não veria as duas juntas.
        var junto = Script().Replace("\\\n", " ", StringComparison.Ordinal);

        return junto.Split('\n')
                    .Where(l => !l.TrimStart().StartsWith('#'));
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
