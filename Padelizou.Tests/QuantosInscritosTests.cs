using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Quantos estão inscritos no torneio inteiro. A tela contava por categoria, mas só depois de
// escolher uma no seletor — pro total o organizador somava de cabeça. E é o total que ele
// responde no WhatsApp o dia inteiro ("quantos já entraram?").
public class QuantosInscritosTests
{
    private static Torneio Torneio(string formato = "Padrao") =>
        new() { Nome = "Interno", Codigo = "INT1", Formato = formato };

    private static Categoria Categoria(int id, params Dupla[] duplas) =>
        new() { Id = id, Nome = "5ª Masculina", Codigo = "C5M", Duplas = duplas.ToList() };

    private static Dupla Dupla(bool espera = false, string? time = null) =>
        new() { Codigo = "D", NomeTime = time, EmListaDeEspera = espera };

    [Fact]
    public void Soma_as_duplas_de_TODAS_as_categorias()
    {
        var contagem = QuantosInscritos.Contar(
            Torneio(),
            new[] { Categoria(1, Dupla(), Dupla()), Categoria(2, Dupla()) },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(3, contagem.Duplas);
        Assert.Equal("3 duplas", contagem.Rotulo);
    }

    [Fact]
    public void Lista_de_espera_fica_fora_do_numero_principal()
    {
        // Quem espera não está inscrito. Somar os dois faria o organizador anunciar vaga
        // que ele não tem.
        var contagem = QuantosInscritos.Contar(
            Torneio(),
            new[] { Categoria(1, Dupla(), Dupla(espera: true), Dupla(espera: true)) },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(1, contagem.Duplas);
        Assert.Equal(2, contagem.NaEspera);
    }

    [Fact]
    public void Americano_conta_PESSOAS_e_nunca_duplas()
    {
        // Ali se cobra e se joga por pessoa: falar em "duplas" seria inventar um número que
        // não existe naquele torneio.
        var contagem = QuantosInscritos.Contar(
            Torneio("Americano"),
            new[] { Categoria(1) },
            new[]
            {
                new InscricaoAmericana { CategoriaId = 1 },
                new InscricaoAmericana { CategoriaId = 1 },
                new InscricaoAmericana { CategoriaId = 1, EmListaDeEspera = true },
            });

        Assert.Equal(0, contagem.Duplas);
        Assert.Equal(2, contagem.Pessoas);
        Assert.Equal(1, contagem.NaEspera);
        Assert.Equal("2 inscritos", contagem.Rotulo);
    }

    [Fact]
    public void Time_conta_como_time_e_nao_engorda_as_duplas()
    {
        var contagem = QuantosInscritos.Contar(
            Torneio(),
            new[] { Categoria(1, Dupla(), Dupla(time: "Argentus XP"), Dupla(time: "CredHub")) },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(1, contagem.Duplas);
        Assert.Equal(2, contagem.Times);
    }

    [Fact]
    public void Dupla_e_time_no_mesmo_torneio_aparecem_os_dois()
    {
        // Categoria de times convive com as normais — mostrar só um dos números esconderia
        // metade do torneio.
        var contagem = QuantosInscritos.Contar(
            Torneio(),
            new[] { Categoria(1, Dupla(), Dupla()), Categoria(2, Dupla(time: "ST Led")) },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal("2 duplas + 1 time", contagem.Rotulo);
    }

    [Theory]
    [InlineData(1, "1 dupla")]
    [InlineData(2, "2 duplas")]
    public void O_singular_e_o_plural_saem_certos(int quantas, string esperado)
    {
        var duplas = Enumerable.Range(0, quantas).Select(_ => Dupla()).ToArray();

        Assert.Equal(esperado, QuantosInscritos.Contar(
            Torneio(), new[] { Categoria(1, duplas) }, Array.Empty<InscricaoAmericana>()).Rotulo);
    }

    [Fact]
    public void Torneio_recem_criado_diz_que_esta_vazio_em_vez_de_mostrar_zero()
    {
        var contagem = QuantosInscritos.Contar(
            Torneio(), new[] { Categoria(1) }, Array.Empty<InscricaoAmericana>());

        Assert.True(contagem.Vazio);
        Assert.Equal("ninguém inscrito ainda", contagem.Rotulo);
    }

    [Theory]
    [InlineData(0, "0 duplas")]
    [InlineData(1, "1 dupla")]
    [InlineData(4, "4 duplas")]
    public void O_badge_da_categoria_tambem_acerta_o_singular(int quantas, string esperado)
    {
        // Escrevia "1 duplas" desde sempre. Passou a incomodar quando o total do cabeçalho,
        // logo acima, começou a acertar.
        Assert.Equal(esperado, QuantosInscritos.Rotulo(quantas, "dupla", "duplas"));
    }

    [Fact]
    public void A_conta_por_categoria_bate_com_a_do_seletor()
    {
        // O seletor mostra o número de cada categoria; se divergir do total, o organizador vê
        // uma soma que não fecha.
        var torneio = Torneio();
        var c1 = Categoria(1, Dupla(), Dupla(), Dupla(espera: true));
        var c2 = Categoria(2, Dupla());
        var vazias = Array.Empty<InscricaoAmericana>();

        Assert.Equal(2, QuantosInscritos.DaCategoria(torneio, c1, vazias));
        Assert.Equal(1, QuantosInscritos.DaCategoria(torneio, c2, vazias));
        Assert.Equal(3, QuantosInscritos.Contar(torneio, new[] { c1, c2 }, vazias).Duplas);
    }
}
