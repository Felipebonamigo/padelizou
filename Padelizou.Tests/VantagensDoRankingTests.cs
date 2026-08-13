using System.Text.RegularExpressions;
using Padelizou.Services;

namespace Padelizou.Tests;

// "Vantagens em habilitar o Ranking Brasil" — o convite que faltava ao lado da caixa de
// conferir as inscrições (pedido do Felipe, 13/08/2026).
//
// O que estes testes seguram não é o texto: é a ligação entre as DUAS telas e a partial. Ela
// se desfaz calada — a tela continua abrindo, o formulário continua salvando, e o organizador
// só não vê mais o convite. Ninguém abre um chamado por um bloco que sumiu de uma tela só.
public class VantagensDoRankingTests
{
    [Theory]
    [InlineData("Create.cshtml")]   // criar torneio
    [InlineData("Details.cshtml")]  // editar torneio
    public void As_duas_telas_do_ranking_mostram_as_vantagens(string tela)
    {
        var conteudo = SemComentarioRazor(Arquivo(Path.Combine("Views", "Torneios", tela)));

        Assert.Contains("_VantagensDoRanking", conteudo);
    }

    // ⚠️ O bloco fica onde a decisão é tomada. Se um dia ele for parar numa tela em que a caixa
    // não existe, vira propaganda solta — e o organizador não tem onde clicar pra habilitar.
    [Theory]
    [InlineData("Create.cshtml")]
    [InlineData("Details.cshtml")]
    public void As_vantagens_ficam_junto_da_caixa_de_habilitar(string tela)
    {
        var conteudo = SemComentarioRazor(Arquivo(Path.Combine("Views", "Torneios", tela)));

        Assert.Contains("ValidarPeloRankingRs", conteudo);
        Assert.True(conteudo.IndexOf("ValidarPeloRankingRs", StringComparison.Ordinal)
                    < conteudo.IndexOf("_VantagensDoRanking", StringComparison.Ordinal),
            $"Em {tela} o bloco de vantagens está ANTES da caixa que habilita o ranking — "
            + "ele existe pra convencer quem está decidindo marcar, então vem depois dela.");
    }

    [Fact]
    public void O_convite_leva_pra_pagina_de_clubes_do_parceiro()
    {
        var bloco = Arquivo(Path.Combine("Views", "Shared", "_VantagensDoRanking.cshtml"));

        Assert.Contains("MarcaDoRanking.ParaClubes", bloco);
        Assert.Equal("https://mundodoatleta.com.br/parceria-clubes", MarcaDoRanking.ParaClubes);

        // Link pra fora abre em aba nova E com rel="noopener": sem ele, a página de destino
        // ganha um `window.opener` que dá pra usar pra trocar a nossa aba de endereço.
        Assert.Contains("target=\"_blank\"", bloco);
        Assert.Contains("rel=\"noopener\"", bloco);
    }

    // ⚠️ O endereço do parceiro mora num lugar só, como o nome dele. Escrito à mão numa das duas
    // telas, ele vira link quebrado EM UMA delas no dia em que eles mudarem a rota — e nada
    // quebra: a tela abre, o link só não leva a lugar nenhum.
    [Theory]
    [InlineData("Create.cshtml")]
    [InlineData("Details.cshtml")]
    public void Nenhuma_tela_digita_o_endereco_do_parceiro_a_mao(string tela)
    {
        var conteudo = Arquivo(Path.Combine("Views", "Torneios", tela));

        Assert.DoesNotContain("mundodoatleta", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    // A marca aparece pelo rótulo compartilhado, e não como "Ranking RS" digitado — eles estão
    // no meio da troca de nome, e é isso que evita a mesma tela chamando o parceiro de dois
    // jeitos. Ver Services/MarcaDoRanking.
    [Fact]
    public void O_bloco_chama_o_parceiro_pelo_rotulo_compartilhado()
    {
        var bloco = SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_VantagensDoRanking.cshtml")));

        Assert.Contains("MarcaDoRanking.Nome", bloco);
        Assert.DoesNotContain("Ranking RS", bloco);
    }

    // ⚠️ O `if` na view continua existindo mesmo com a arte no lugar: é ele que faz a ausência
    // do arquivo virar "só o texto" em vez do ícone de imagem quebrada bem ao lado do nome do
    // parceiro. Apagar o `if` porque "agora tem logo" devolveria esse risco de graça.
    [Fact]
    public void A_tela_so_desenha_a_logo_quando_ela_existe()
    {
        var bloco = SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_VantagensDoRanking.cshtml")));

        Assert.Contains("MarcaDoRanking.TemLogo", bloco);
    }

    // 🚨 O caminho da logo é TEXTO: um erro de digitação não quebra build nem teste, só entrega
    // um retângulo vazio ao lado do nome do parceiro na tela de criar torneio. Este teste vai ao
    // disco conferir que o arquivo apontado existe mesmo.
    [Fact]
    public void O_arquivo_da_logo_existe_onde_a_constante_diz()
    {
        Assert.True(MarcaDoRanking.TemLogo, "A logo foi desligada — se foi de propósito, ajuste este teste.");

        var caminho = Path.Combine(PastaDoProjeto(), "wwwroot",
            MarcaDoRanking.Logo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(caminho), $"MarcaDoRanking.Logo aponta pra {MarcaDoRanking.Logo}, "
            + $"que não existe em wwwroot (procurei em {caminho}).");
    }

    // ⚠️ E o conteúdo tem que bater com a EXTENSÃO. O arquivo chegou como `ranking-brasil.png.jpeg`
    // — um JPEG com cara de PNG. O Caddy manda `X-Content-Type-Options: nosniff` em toda resposta,
    // então um JPEG servido como `image/png` é RECUSADO pelo navegador: a logo some e não há erro
    // em lugar nenhum pra ligar uma coisa à outra.
    [Fact]
    public void O_conteudo_da_logo_bate_com_a_extensao_do_arquivo()
    {
        var caminho = Path.Combine(PastaDoProjeto(), "wwwroot",
            MarcaDoRanking.Logo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var cabecalho = File.ReadAllBytes(caminho).Take(4).ToArray();
        var extensao = Path.GetExtension(caminho).ToLowerInvariant();

        var ehJpeg = cabecalho[0] == 0xFF && cabecalho[1] == 0xD8;
        var ehPng = cabecalho[0] == 0x89 && cabecalho[1] == 0x50;
        var ehWebp = cabecalho[0] == 0x52 && cabecalho[1] == 0x49;

        var esperado = extensao switch
        {
            ".jpg" or ".jpeg" => ehJpeg,
            ".png" => ehPng,
            ".webp" => ehWebp,
            _ => false,
        };

        Assert.True(esperado, $"O arquivo da logo é {(ehJpeg ? "JPEG" : ehPng ? "PNG" : ehWebp ? "WebP" : "de formato desconhecido")} "
            + $"mas está gravado como \"{extensao}\". Com nosniff ligado, o navegador recusa a imagem.");
    }

    private static string SemComentarioRazor(string texto) =>
        Regex.Replace(texto, @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    // Sobe do diretório de saída dos testes até achar o projeto web.
    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
