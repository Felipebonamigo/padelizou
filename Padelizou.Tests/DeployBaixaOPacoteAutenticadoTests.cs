using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 15/09/2026 — O DEPLOY NÃO PODE DEPENDER DE O REPOSITÓRIO SER PÚBLICO.
//
// 🕳️ O DEFEITO, visto no ar: o repositório foi marcado como privado por alguns minutos e o
// `deploy.sh` morreu na hora — ele baixa o pacote de
// `github.com/<repo>/releases/download/<tag>/padelizou.tar.gz` SEM credencial nenhuma, e em
// repositório privado essa URL devolve 404 (medido: 404 privado, 206 público, mesma tag
// `build-1450-7ec7a5d`). Pior que a queda: `set -euo pipefail` derruba o script na atribuição
// do `TAG`, então o operador lê `curl: (22) ... error: 404` e nunca a mensagem
// "não encontrei build" que o script tem pronta.
//
// 🕳️ E TEM UM SEGUNDO, que existe HOJE com o repositório público: a API do GitHub sem token
// dá 60 chamadas por HORA por IP. O laço que espera o CI gerar o build de um sha faz até 60
// chamadas em 10 minutos (`seq 1 60`, `sleep 10`) — um `deploy.sh dev <sha>` que espera até o
// fim gasta a cota inteira do VPS. O segundo deploy da mesma hora leva 403 em todas as
// tentativas, e o `|| true` do laço engole cada uma: o operador vê "não encontrei build pra
// <sha>" e vai procurar defeito no CI, que está verde. Com token são 5.000/hora.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não tem como executar um deploy — o `deploy.sh` roda no
// VPS, com systemd e symlink. É a mesma escolha do `TagDoBuildApontaProCommitTests`: travar na
// fonte o que não dá pra travar em comportamento.
public class DeployBaixaOPacoteAutenticadoTests
{
    [Fact]
    public void O_download_do_pacote_usa_o_endpoint_de_asset_da_API()
    {
        var codigo = CodigoDoDeploy();

        // A URL de navegador (`releases/download/...`) é a que 404 em repositório privado. O
        // endpoint de asset da API é o ÚNICO documentado pras duas visibilidades: com
        // `Accept: application/octet-stream` ele devolve o binário (ou redireciona pra ele)
        // tanto anônimo em repo público quanto autenticado em repo privado.
        Assert.DoesNotContain("/releases/download/", codigo);
        Assert.Contains("/releases/assets/", codigo);
        Assert.Contains("Accept: application/octet-stream", codigo);
    }

    [Fact]
    public void Nenhuma_chamada_ao_GitHub_sai_sem_passar_pela_config_do_curl()
    {
        // Onde o token entra. Uma chamada nova que esqueça o `-K` volta a ser anônima — e
        // volta a 404 no dia em que o repositório fechar, que é justamente o que este
        // arquivo existe pra impedir.
        //
        // As continuações (`\` no fim da linha) são emendadas ANTES de olhar linha a linha:
        // o `curl` do download põe a URL na segunda linha, e sem emendar o teste reprovaria
        // o código certo. A checagem é por COMANDO, não por linha de arquivo.
        foreach (var comando in CodigoDoDeploy().Split('\n'))
        {
            if (!comando.Contains("curl ", StringComparison.Ordinal)) continue;
            if (!comando.Contains("github", StringComparison.Ordinal)) continue;

            Assert.Contains("-K \"$CURL_CFG\"", comando);
        }
    }

    // ⚠️ ESTE PASSA DE GRAÇA antes da correção — o script não tinha autenticação nenhuma, então
    // não continha nenhuma das duas strings. Ele não trava um defeito que existiu; trava as
    // duas formas ERRADAS de consertar o que existiu, e só começa a valer com a correção no
    // lugar. Os outros três deste arquivo foram vistos vermelhos antes dela.
    [Fact]
    public void O_token_nunca_vai_na_linha_de_comando_nem_vaza_no_redirecionamento()
    {
        var codigo = CodigoDoDeploy();

        // Argumento de processo é legível por qualquer usuário da máquina (`ps auxww`), e a
        // app roda com outro usuário no mesmo VPS. Por isso o header mora num arquivo 600.
        //
        // ⚠️ O alvo é `-H "Authorization`, o ARGUMENTO — e não a string do header sozinha, que
        // aparece legitimamente no `printf` que escreve o arquivo de config. Uma asserção na
        // string solta reprovaria justamente o conserto certo (aconteceu ao escrever isto).
        Assert.DoesNotContain("-H \"Authorization", codigo);

        // `--location-trusted` reenvia o header de autorização pro host do redirecionamento —
        // que aqui é o armazenamento de objetos do GitHub, não o GitHub. Seria entregar o
        // token a um terceiro pra baixar um arquivo que o próprio redirecionamento já assina.
        Assert.DoesNotContain("--location-trusted", codigo);
    }

    [Fact]
    public void A_recusa_da_API_diz_o_que_fazer_em_vez_de_virar_erro_do_curl()
    {
        var deploy = Deploy();

        // 401/403/404 na API têm três causas diferentes (token errado, cota estourada, repo
        // fechado sem token) e uma só cara no `curl -fsS`: "error: 404". O script precisa
        // separar as três e dizer onde o token mora, senão a próxima sessão repete a
        // investigação desta aqui.
        Assert.Contains("rate limit", deploy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".github-token", deploy);
    }

    [Fact]
    public void A_espera_pelo_build_do_sha_nao_come_a_cota_inteira_da_API()
    {
        // Cada volta do laço é UMA chamada à API, e sem token são 60 por hora por IP. Em 10s
        // eram 60 voltas: a cota inteira num deploy só — e desde que o download passou a sair
        // do endpoint de asset, ele TAMBÉM é chamada de API, então o próprio passo seguinte
        // ficava sem cota. O laço precisa caber em metade e ainda cobrir os ~10 min que o CI
        // leva pra publicar o build.
        var espera = CodigoDoDeploy();
        var inicio = espera.IndexOf("SHA7=", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a espera pelo build do sha no deploy.sh.");
        espera = espera[inicio..];

        var voltas = int.Parse(Regex.Match(espera, @"seq 1 (\d+)").Groups[1].Value);
        var intervalo = int.Parse(Regex.Match(espera, @"sleep (\d+)").Groups[1].Value);

        Assert.True(voltas <= 30, $"{voltas} voltas passam de metade das 60 chamadas/hora sem token.");
        Assert.True(voltas * intervalo >= 600, $"A espera caiu pra {voltas * intervalo}s — o CI leva ~10 min.");
    }

    private static string Deploy() =>
        File.ReadAllText(Path.Combine(RaizDoRepositorio(), "infra", "vps", "deploy.sh"));

    // O `deploy.sh` é mais comentário que código, e os comentários citam de propósito o que o
    // script NÃO deve fazer (a URL antiga, a opção perigosa). Asserção de "não contém" tem que
    // olhar só o código, senão ela reprova a explicação em vez do defeito.
    //
    // As continuações (`\` no fim da linha) são emendadas ANTES: o `curl` do download põe a URL
    // na segunda linha, e sem emendar a checagem do `-K` reprovaria o código certo. O que se
    // examina é COMANDO, não linha de arquivo.
    private static string CodigoDoDeploy() =>
        string.Join('\n', Deploy()
            .Replace("\\\n", " ")
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith('#')));

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "infra", "vps"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a raiz do repositório (a pasta com infra/vps) subindo a partir de "
            + AppContext.BaseDirectory);
    }
}
