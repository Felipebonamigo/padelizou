using Padelizou.Services;

namespace Padelizou.Tests;

// 10/11/2026 — CATEGORIAS DE "MESCLA": UM ATLETA DE CADA NÍVEL.
//
// 🗣️ Felipe, com o print de um seletor "FEM A … MAS D": *"precisamos criar essas categorias
// aonde é um atleta de cada categoria, fazendo uma mescla"* · *"D = 5ª e 6ª, C = 4ª e 5ª,
// B = 3ª e 4ª, A = 2ª e 3ª"* · *"já cria a E também, que é 6ª e 7ª"*.
//
// Pro Padelímetro elas são irmãs da mista: os jogos contam, quem estreia por uma nasce na MÉDIA
// das duas entradas, e não há campanha nem âncora de faixa — a dupla mistura dois degraus.
public class CategoriasDeMesclaTests
{
    [Theory]
    [InlineData("Categoria Masculina A", 750)]   // 2ª (800) e 3ª (700)
    [InlineData("Categoria Masculina B", 650)]   // 3ª e 4ª
    [InlineData("Categoria Masculina C", 550)]   // 4ª e 5ª
    [InlineData("Categoria Masculina D", 450)]   // 5ª e 6ª
    [InlineData("Categoria Masculina E", 350)]   // 6ª e 7ª
    [InlineData("Categoria Feminina A", 450)]    // 2ª fem (500) e 3ª fem (400)
    [InlineData("Categoria Feminina B", 350)]
    [InlineData("Categoria Feminina C", 250)]
    [InlineData("Categoria Feminina D", 160)]    // 5ª fem (200) e 6ª fem (120)
    [InlineData("Categoria Feminina E", 80)]     // 6ª fem (120) e 7ª fem (40)
    public void Quem_estreia_nasce_na_media_das_duas_entradas(string nome, int entrada)
    {
        Assert.Equal(entrada, FaixasDePadelimetro.Entrada(nome));
    }

    [Theory]
    [InlineData("Categoria Masculina B")]
    [InlineData("Categoria Feminina E")]
    public void Fica_fora_da_escada_sem_faixa_e_sem_campanha(string nome)
    {
        Assert.True(FaixasDePadelimetro.EhMescla(nome));
        Assert.True(FaixasDePadelimetro.ForaDaEscada(nome));
        Assert.Null(FaixasDePadelimetro.DaCategoria(nome));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, CampeoesDoTorneio.FaseDeCampeao, "Semifinal", nome));
    }

    [Theory]
    [InlineData("Categoria Mista A")]
    [InlineData("4ª Categoria Masculina")]
    [InlineData("Categoria Open Feminina")]
    [InlineData("Categoria Masculina")]
    [InlineData("Categoria Masculina Z")]
    public void O_que_nao_e_mescla_continua_como_era(string nome)
    {
        Assert.False(FaixasDePadelimetro.EhMescla(nome));
    }

    [Fact]
    public void Nao_vira_ancora_do_rotulo()
    {
        var hoje = new DateTime(2026, 11, 10);
        Assert.Equal("4ª Categoria Masculina", FaixasDePadelimetro.CategoriaDeReferencia(new (string?, DateTime?)[]
        {
            ("Categoria Masculina B", hoje),
            ("4ª Categoria Masculina", hoje.AddDays(-30)),
        }, hoje));
    }

    [Theory]
    [InlineData("Categoria Masculina A", "2ª e 3ª Masc")]
    [InlineData("Categoria Masculina E", "6ª e 7ª Masc")]
    [InlineData("Categoria Feminina C", "4ª e 5ª Fem")]
    public void A_tela_mostra_os_dois_niveis(string nome, string legenda)
    {
        Assert.Equal(legenda, CategoriaNaTela.Legenda(nome));
    }

    [Fact]
    public void Na_lista_ela_fica_logo_abaixo_da_categoria_mais_forte_que_mistura()
    {
        var nomes = new[]
        {
            "4ª Categoria Masculina", "Categoria Masculina B", "3ª Categoria Masculina",
            "Categoria Masculina A", "2ª Categoria Masculina", "3ª Categoria Feminina",
        };
        var ordem = nomes.OrderBy(CategoriaNaTela.Ordem).ToArray();
        Assert.Equal(new[]
        {
            "2ª Categoria Masculina", "Categoria Masculina A",
            "3ª Categoria Masculina", "3ª Categoria Feminina", "Categoria Masculina B",
            "4ª Categoria Masculina",
        }, ordem);
    }

    [Fact]
    public void E_de_um_sexo_so()
    {
        Assert.Equal(SexoDoJogador.Masculino, CategoriaNaTela.SexoDaEscada("Categoria Masculina C"));
        Assert.Equal(SexoDoJogador.Feminino, CategoriaNaTela.SexoDaEscada("Categoria Feminina C"));
        Assert.False(SexoDoJogador.ExigeUmDeCada("Categoria Masculina C"));
    }
}
