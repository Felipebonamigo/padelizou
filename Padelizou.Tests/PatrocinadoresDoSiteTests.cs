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
    public void Ambiente_de_teste_sem_configuracao_mostra_os_patrocinadores_em_avaliacao()
    {
        var settings = new PatrocinadoresSettings();

        var exibidos = settings.ParaExibir(ambienteDeTeste: true);

        Assert.Equal(new[] { "Paralelo", "Grand Padel" }, exibidos.Select(p => p.Nome));

        // Cada um resolve o tema escuro do seu jeito, e são jeitos diferentes DE PROPÓSITO:
        // o Paralelo é preto puro e pode ser invertido; o Grand Padel é colorido e tem arte
        // branca própria — inverter azul viraria laranja, que não é a marca de ninguém.
        var paralelo = exibidos[0];
        Assert.True(paralelo.LogoEscuro);
        Assert.Empty(paralelo.ImagemEscura);

        var grandPadel = exibidos[1];
        Assert.False(grandPadel.LogoEscuro);
        Assert.NotEmpty(grandPadel.ImagemEscura);
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
