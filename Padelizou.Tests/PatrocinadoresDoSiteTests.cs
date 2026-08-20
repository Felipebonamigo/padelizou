using Padelizou.Services;

namespace Padelizou.Tests;

// A faixa de patrocínio do rodapé nasce desligada e só existe onde alguém a ligou. O erro
// caro aqui é o inverso de sempre: patrocinador aparecendo em PRODUÇÃO antes de contrato
// fechado — por isso o fallback do ambiente de teste precisa de teste que prove que ele
// NÃO vaza pra fora do dev.
public class PatrocinadoresDoSiteTests
{
    [Fact]
    public void Producao_sem_configuracao_nao_mostra_patrocinador_nenhum()
    {
        var settings = new PatrocinadoresSettings();

        Assert.Empty(settings.ParaExibir(ambienteDeTeste: false));
    }

    [Fact]
    public void Ambiente_de_teste_sem_configuracao_mostra_o_patrocinador_em_avaliacao()
    {
        var settings = new PatrocinadoresSettings();

        var exibidos = settings.ParaExibir(ambienteDeTeste: true);

        var paralelo = Assert.Single(exibidos);
        Assert.Equal("Paralelo", paralelo.Nome);
        // Logo preto puro: sem o flag, ele some no tema escuro (ver .pdz-logo-escuro).
        Assert.True(paralelo.LogoEscuro);
    }

    [Fact]
    public void So_entra_no_embutido_quem_mandou_a_arte()
    {
        // A trava do 20/08/2026: um logo recriado à mão entrou aqui pra adiantar o teste de
        // posicionamento. Arte de marca não se recria — sem o arquivo oficial, o
        // patrocinador espera do lado de fora. Este teste é o que impede a pressa de
        // repetir isso: quem for somar um nome aqui vai ter que vir mexer nesta linha.
        var embutidos = new PatrocinadoresSettings().ParaExibir(ambienteDeTeste: true);

        Assert.Equal(new[] { "Paralelo" }, embutidos.Select(p => p.Nome));
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

        var exibido = Assert.Single(settings.ParaExibir(ambienteDeTeste: false));

        Assert.Equal("/image/patrocinadores/colorida-branca.webp", exibido.ImagemEscura);
        Assert.False(exibido.LogoEscuro);
    }

    [Fact]
    public void Configuracao_preenchida_manda_mesmo_no_ambiente_de_teste()
    {
        // Quando o dev (ou produção) configurar a lista de verdade, o embutido sai de cena —
        // senão o Paralelo continuaria aparecendo ao lado dos patrocinadores reais.
        var settings = new PatrocinadoresSettings
        {
            Lista = { new Patrocinador { Nome = "Outra Marca", Imagem = "/image/patrocinadores/outra.webp" } }
        };

        var exibidos = settings.ParaExibir(ambienteDeTeste: true);

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

        var exibidos = settings.ParaExibir(ambienteDeTeste: false);

        Assert.Equal("Com Logo", Assert.Single(exibidos).Nome);
    }
}
