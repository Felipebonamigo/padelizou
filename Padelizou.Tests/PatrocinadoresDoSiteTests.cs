using Padelizou.Services;

namespace Padelizou.Tests;

// A faixa de patrocínio do rodapé mostra quem está escrito em PatrocinadoresSettings, em
// todo ambiente — contrato fechado, logo no ar (20/08/2026). O que estes testes guardam,
// então, é o outro lado: que a lista de código valha em produção sem depender de ninguém
// configurar nada, que o systemd continue podendo substituí-la, e que ninguém entre nela
// sem arte oficial e sem link.
public class PatrocinadoresDoSiteTests
{
    [Fact]
    public void Producao_sem_configuracao_ja_mostra_os_patrocinadores()
    {
        // Produção não precisa de systemd pra ter patrocinador: quem paga aparece por estar
        // no código. Era o contrário até 20/08/2026, quando a lista só valia no dev.
        var exibidos = new PatrocinadoresSettings().ParaExibir();

        Assert.NotEmpty(exibidos);
    }

    [Fact]
    public void Sem_configuracao_valem_os_patrocinadores_escritos_no_codigo()
    {
        var settings = new PatrocinadoresSettings();

        var exibidos = settings.ParaExibir();

        Assert.Equal(new[] { "Paralelo", "Grand Padel" }, exibidos.Select(p => p.Nome));

        // Cada um resolve o fundo escuro por um caminho, e são caminhos diferentes DE
        // PROPÓSITO: o Paralelo é preto puro e o CSS inverte; o Grand Padel é colorido e
        // tem negativa branca no kit da marca — inverter azul daria laranja.
        var paralelo = exibidos[0];
        Assert.True(paralelo.LogoEscuro);
        Assert.Empty(paralelo.ImagemEscura);

        var grandPadel = exibidos[1];
        Assert.False(grandPadel.LogoEscuro);
        Assert.NotEmpty(grandPadel.ImagemEscura);
    }

    [Fact]
    public void Cada_patrocinador_leva_pro_site_dele()
    {
        // O clique é parte do que se vende: quem paga pra aparecer espera receber a visita,
        // e logo sem link é só enfeite. Some calado se alguém mexer na lista sem o endereço.
        var emCartaz = new PatrocinadoresSettings().ParaExibir();

        Assert.All(emCartaz, p => Assert.StartsWith("https://", p.Link));
    }

    [Fact]
    public void So_entra_na_lista_quem_mandou_a_arte()
    {
        // A trava do 20/08/2026: um logo recriado à mão entrou aqui pra adiantar o teste de
        // posicionamento, e o Felipe reparou na hora. Arte de marca não se recria — sem o
        // arquivo oficial, o patrocinador espera do lado de fora. (O Grand Padel voltou no
        // mesmo dia, agora com o kit que a marca mandou.) Esta lista é a trava: quem somar
        // um nome aqui vai ter que vir mexer nela, e lembrar da regra ao fazer isso.
        var emCartaz = new PatrocinadoresSettings().ParaExibir();

        Assert.Equal(new[] { "Paralelo", "Grand Padel" }, emCartaz.Select(p => p.Nome));
    }

    [Fact]
    public void Logo_colorido_troca_de_arquivo_no_tema_escuro_em_vez_de_ser_invertido()
    {
        // Os dois caminhos pro fundo escuro existem e são diferentes de propósito: preto
        // puro o CSS inverte (LogoEscuro), colorido recebe a arte branca oficial da marca
        // (ImagemEscura) — inverter azul daria laranja, que não é a marca de ninguém.
        var settings = new PatrocinadoresSettings
        {
            Lista =
            {
                new Patrocinador
                {
                    Nome = "Marca Colorida",
                    Imagem = "/image/patrocinadores/colorida.webp",
                    ImagemEscura = "/image/patrocinadores/colorida-branca.webp",
                },
            }
        };

        var exibido = Assert.Single(settings.ParaExibir());

        Assert.Equal("/image/patrocinadores/colorida-branca.webp", exibido.ImagemEscura);
        Assert.False(exibido.LogoEscuro);
    }

    [Fact]
    public void Configuracao_preenchida_substitui_a_lista_do_codigo()
    {
        // O systemd é a saída de emergência: trocar o cartaz no ar sem esperar deploy. Quando
        // ele fala, a lista do código sai inteira de cena — senão o patrocinador que se quis
        // tirar continuaria aparecendo ao lado do novo.
        var settings = new PatrocinadoresSettings
        {
            Lista = { new Patrocinador { Nome = "Outra Marca", Imagem = "/image/patrocinadores/outra.webp" } }
        };

        var exibidos = settings.ParaExibir();

        Assert.Equal("Outra Marca", Assert.Single(exibidos).Nome);
    }

    [Fact]
    public void Patrocinador_sem_imagem_nao_aparece()
    {
        // No rodapé o logo É o anúncio: entrada só com nome é configuração pela metade, e
        // meia configuração não pode virar texto solto na tela.
        var settings = new PatrocinadoresSettings
        {
            Lista =
            {
                new Patrocinador { Nome = "Sem Logo" },
                new Patrocinador { Nome = "Com Logo", Imagem = "/image/patrocinadores/com-logo.webp" },
            }
        };

        var exibidos = settings.ParaExibir();

        Assert.Equal("Com Logo", Assert.Single(exibidos).Nome);
    }
}
