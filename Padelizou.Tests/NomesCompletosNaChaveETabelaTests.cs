using Xunit;

namespace Padelizou.Tests;

// OS NOMES NA CHAVE E NO GRUPO, COMPLETOS (14/10/2026). 🗣️ Reclamações de jogadores, com o print do
// ER do fim de semana:
//   - *"no mata-mata não dá pra saber qual time elas representam e também não tem o sobrenome"* —
//     a vaga mostrava "Marcos / Emerson" e "Lucas / Lucas" (duas duplas com Lucas, sem como
//     separar), sem o escudo do time que a TABELA DO GRUPO já mostrava;
//   - *"no grupo, às vezes não aparece o nome e sobrenome, porque não cabe no mobile"* — o chip
//     compacto cortava com reticências ("Felipe Bonam…").
//
// ✅ A vaga do mata-mata passa a desenhar o MESMO chip do grupo (`_LadoDaPartida`: foto, selo do
// time e nome com sobrenome) — um desenho só, em vez de uma segunda regra de nome. ✅ E o chip
// compacto QUEBRA em duas linhas em vez de cortar.
//
// O projeto testa essas telas pelo fonte (não há render de Razor na suíte): o que estes testes
// travam é a escolha de desenho que as reclamações pediram.
public class NomesCompletosNaChaveETabelaTests
{
    private static string Projeto()
    {
        var pasta = AppContext.BaseDirectory;
        while (pasta != null)
        {
            if (Directory.Exists(Path.Combine(pasta, "Padelizou", "Views"))) return Path.Combine(pasta, "Padelizou");
            pasta = Path.GetDirectoryName(pasta);
        }
        throw new DirectoryNotFoundException("Raiz do projeto não encontrada.");
    }

    private static string Ler(params string[] partes) => File.ReadAllText(Path.Combine(new[] { Projeto() }.Concat(partes).ToArray()));

    [Theory]
    [InlineData("_ChaveDoMataMata.cshtml")]
    [InlineData("_ChaveVaga.cshtml")]
    public void A_vaga_do_jogo_desenha_a_dupla_com_o_chip_do_grupo_e_nao_so_o_primeiro_nome(string arquivo)
    {
        var fonte = Ler("Views", "Torneios", arquivo);

        Assert.Contains("<partial name=\"_LadoDaPartida\"", fonte);
        // O selo do time é o que responde "qual time ela representa".
        Assert.Contains("EscudoComoSelo", fonte);
        // E o nome de uma palavra só ("Marcos / Emerson") não é mais o que a vaga escreve.
        Assert.DoesNotContain("<span class=\"pdz-chave-nome\">@NomeDaDupla(jogo.Dupla1)</span>", fonte);
        Assert.DoesNotContain("<span class=\"pdz-chave-nome\">@Nome(jogo.Dupla1)</span>", fonte);
    }

    [Fact]
    public void A_vaga_do_quadro_desenha_a_dupla_ja_conhecida_com_o_chip_tambem()
    {
        // Quem folgou a 1ª rodada e já tem nome numa fase futura é desenhado igual ao jogo.
        var fonte = Ler("Views", "Torneios", "_ChaveDoMataMata.cshtml");

        Assert.Contains("lado?.ComNome is { } duplaDaVaga", fonte);
        Assert.Contains("model=\"duplaDaVaga\"", fonte);
    }

    [Fact]
    public void A_campea_aparece_com_nome_e_sobrenome()
    {
        var fonte = Ler("Views", "Torneios", "_ChaveDoMataMata.cshtml");

        Assert.DoesNotContain("@NomeDaDupla(campea)", fonte);
        Assert.Contains("NomeCompletoDaDupla(campea)", fonte);
    }

    [Fact]
    public void O_chip_compacto_do_grupo_quebra_o_nome_em_vez_de_cortar_com_reticencias()
    {
        var css = Ler("wwwroot", "css", "site.css");
        var regra = Bloco(css, ".pdz-chip-compacto .pdz-chip-nome {");

        Assert.DoesNotContain("nowrap", regra);
        Assert.DoesNotContain("ellipsis", regra);
        Assert.Contains("overflow-wrap", regra);
    }

    [Fact]
    public void A_vaga_com_chips_nao_corta_o_nome_nem_fica_numa_linha_so()
    {
        var css = Ler("wwwroot", "css", "site.css");
        var regra = Bloco(css, ".pdz-chave-nome.pdz-chave-nome-chips {");

        Assert.Contains("white-space: normal", regra);
        Assert.DoesNotContain("ellipsis", regra);
    }

    // CSS mudou: o app instalado serve a CÓPIA GUARDADA até o número do cache virar.
    [Fact]
    public void O_cache_do_service_worker_virou_junto_com_o_site_css()
    {
        var sw = Ler("wwwroot", "sw.js");

        Assert.Contains("padelizou-static-v39", sw);
    }

    private static string Bloco(string css, string seletor)
    {
        var i = css.IndexOf(seletor, StringComparison.Ordinal);
        Assert.True(i >= 0, $"{seletor} sumiu do site.css");
        var f = css.IndexOf('}', i);
        return css[i..f];
    }
}
