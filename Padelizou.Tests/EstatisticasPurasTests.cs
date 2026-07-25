using Padelizou.Services;

namespace Padelizou.Tests;

// Regras puras que sustentam ranking, troféus e anti-sandbagging.
public class EstatisticasPurasTests
{
    private static readonly EstatisticasService Svc = new(null!);

    [Theory]
    [InlineData("Campeao", 100)]
    [InlineData("Final", 60)]
    [InlineData("Semifinal", 35)]
    [InlineData("Quartas de Final", 20)]
    [InlineData("Grupos", 10)]
    [InlineData(null, 10)]
    public void Pontos_por_fase(string? fase, int esperado)
        => Assert.Equal(esperado, Svc.PontosPorFase(fase));

    [Theory]
    [InlineData("Categoria Open Masculino", 8)]
    [InlineData("1ª Categoria Masculina", 7)]
    [InlineData("2ª Categoria Masculina", 6)]
    [InlineData("3ª Categoria Feminina", 5)]
    [InlineData("7ª Categoria", 1)]
    [InlineData("Iniciantes", 1)]
    [InlineData("Categoria Mista A", 0)] // sem tier reconhecido → não trava nível
    public void Ordem_de_forca_da_categoria(string nome, int ordem)
        => Assert.Equal(ordem, EstatisticasService.OrdemCategoria(nome));

    [Fact]
    public void Ordem_open_e_sempre_a_mais_forte()
    {
        Assert.True(EstatisticasService.OrdemCategoria("Categoria Open Feminina")
                  > EstatisticasService.OrdemCategoria("2ª Categoria Feminina"));
    }

    [Theory]
    [InlineData("Categoria Open Masculino", "Diamante")]
    [InlineData("2ª Categoria Masculina", "Ouro")]
    [InlineData("3ª Categoria Masculina", "Prata")]
    [InlineData("4ª Categoria", "Bronze")]
    [InlineData("Iniciantes", "Plastico")]
    [InlineData("Categoria Mista A", "Geral")]
    public void Tier_do_trofeu_pelo_nome_da_categoria(string nome, string tierEsperado)
        => Assert.Equal(tierEsperado, EstatisticasService.TierDaCategoria(nome).Chave);

    [Fact]
    public void Gatilhos_de_comprovacao_de_nivel()
    {
        Assert.Equal(new[] { "Final", "Campeao" }, EstatisticasService.FasesQueComprovam("Final"));
        Assert.Equal(new[] { "Semifinal", "Final", "Campeao" }, EstatisticasService.FasesQueComprovam("Semifinal"));
        Assert.Equal(4, EstatisticasService.FasesQueComprovam("SaiuChave").Length);
        Assert.Empty(EstatisticasService.FasesQueComprovam("Livre"));
        Assert.Empty(EstatisticasService.FasesQueComprovam(null));
    }

    // Regressão do bug de 25/07/2026: os jogos de grupo existem com DUAS grafias de Fase
    // ("Fase de Grupos" nos dados antigos, "Grupo A/B/..." no GerarChaves atual) e os
    // gatilhos de mata-mata precisam reconhecer as duas.
    [Theory]
    [InlineData("Fase de Grupos", true)]
    [InlineData("Grupo A", true)]
    [InlineData("Grupo H", true)]
    [InlineData("Quartas de Final", false)]
    [InlineData("Semifinal", false)]
    [InlineData("Final", false)]
    [InlineData("Americano R1", false)]
    [InlineData(null, false)]
    public void Reconhece_fase_de_grupos_nas_duas_grafias(string? fase, bool esperado)
        => Assert.Equal(esperado, FasesTorneio.EhFaseDeGrupos(fase));
}

public class DocumentosTests
{
    [Theory]
    [InlineData("111.444.777-35", "11144477735")]
    [InlineData("11144477735", "11144477735")]
    [InlineData("(51) 99999-8888", "51999998888")]
    [InlineData("", "")]
    public void Somente_digitos_limpa_mascaras(string entrada, string esperado)
        => Assert.Equal(esperado, Documentos.SomenteDigitos(entrada));

    [Theory]
    [InlineData("11144477735", true)]   // 11 dígitos
    [InlineData("111.444.777-35", true)] // com máscara ainda vale (11 dígitos)
    [InlineData("1114447773", false)]   // 10 dígitos
    [InlineData("", false)]
    public void Formato_de_cpf(string cpf, bool esperado)
        => Assert.Equal(esperado, Documentos.CpfTemFormatoValido(cpf));
}
