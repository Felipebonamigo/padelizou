using Padelizou.Services;

namespace Padelizou.Tests;

// O @ DO INSTAGRAM COMO RÉGUA ÚNICA (14/09/2026).
//
// Nasceu com a arte do jogo pro story, e cobre um buraco que já existia: o `Perfil.cshtml`
// montava o endereço com `Instagram.TrimStart('@')` escrito à mão, DUAS vezes — e o campo é
// texto livre no cadastro, então o que estava guardado ia direto pro `href`.
//
// A régua é a do próprio Instagram: letras, números, ponto e sublinhado, até 30 caracteres.
// O que não passa nisso não é @ que funciona em lugar nenhum — e é melhor não virar link do
// que virar link quebrado (ou pior: endereço que alguém escreveu de propósito).
public class ArrobaDoInstagramTests
{
    [Theory]
    [InlineData("er.padel", "er.padel")]
    [InlineData("@er.padel", "er.padel")]
    [InlineData("  @er.padel  ", "er.padel")]
    [InlineData("instagram.com/er.padel", "er.padel")]
    [InlineData("https://instagram.com/er.padel", "er.padel")]
    [InlineData("https://www.instagram.com/er.padel/", "er.padel")]
    [InlineData("ER.Padel", "er.padel")]
    [InlineData("xandicosta.13", "xandicosta.13")]
    [InlineData("guilherme_bagesteiro", "guilherme_bagesteiro")]
    public void As_formas_que_a_pessoa_digita_viram_uma_so(string digitado, string guardado)
    {
        Assert.Equal(guardado, ArrobaDoInstagram.Normalizar(digitado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@")]
    [InlineData("instagram.com/")]
    // Espaço no meio, acento e barra não existem em @ do Instagram: é texto que alguém
    // escreveu no campo errado, não um perfil.
    [InlineData("er padel")]
    [InlineData("ér.padel")]
    [InlineData("er/padel")]
    // ⚠️ O caso que fazia o campo virar `href`: esquema que roda script na sessão de quem
    // clicasse. Mesma família do que o FotosDoTorneio tranca no link do álbum.
    [InlineData("javascript:alert(1)")]
    [InlineData("https://sitequalquer.com/er.padel")]
    public void O_que_nao_e_arroba_de_verdade_e_recusado(string? digitado)
    {
        Assert.Null(ArrobaDoInstagram.Normalizar(digitado));
    }

    [Fact]
    public void Acima_de_trinta_caracteres_nao_e_perfil_do_instagram()
    {
        Assert.Null(ArrobaDoInstagram.Normalizar(new string('a', 31)));
        Assert.Equal(new string('a', 30), ArrobaDoInstagram.Normalizar(new string('a', 30)));
    }

    // ParaMostrar aceita o que JÁ ESTÁ no banco, que é de antes da régua existir: uns com @ na
    // frente, uns sem, uns com a URL inteira colada. Todos saem iguais na tela.
    [Theory]
    [InlineData("er.padel", "@er.padel")]
    [InlineData("@er.padel", "@er.padel")]
    [InlineData("https://www.instagram.com/er.padel/", "@er.padel")]
    public void Na_tela_sai_sempre_com_um_arroba_so(string guardado, string naTela)
    {
        Assert.Equal(naTela, ArrobaDoInstagram.ParaMostrar(guardado));
    }

    [Fact]
    public void Sem_arroba_valido_nao_ha_nada_pra_mostrar_nem_link()
    {
        Assert.Null(ArrobaDoInstagram.ParaMostrar("er padel"));
        Assert.Null(ArrobaDoInstagram.Link("er padel"));
        Assert.Null(ArrobaDoInstagram.ParaMostrar(null));
        Assert.Null(ArrobaDoInstagram.Link(null));
    }

    [Fact]
    public void O_link_sai_sempre_em_https_no_dominio_do_instagram()
    {
        Assert.Equal("https://instagram.com/er.padel", ArrobaDoInstagram.Link("@ER.Padel"));
    }
}
