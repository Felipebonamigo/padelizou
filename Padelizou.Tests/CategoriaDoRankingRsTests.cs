using Padelizou.Services;

namespace Padelizou.Tests;

// O de-para entre a categoria escrita pelo organizador e o id do Ranking RS.
//
// Isto aqui decide QUEM É BARRADO numa inscrição, então o teste que mais importa não é o que
// casa certo — é o que se RECUSA a casar. Ver CategoriaDoRankingRs: a API deles não confere
// sexo, então um palpite errado voltaria como "APROVADO" e ninguém perceberia.
public class CategoriaDoRankingRsTests
{
    [Theory]
    [InlineData("Open Masculino", 100)]
    [InlineData("Open Feminino", 101)]
    [InlineData("1ª Masculina", 102)]
    [InlineData("1ª Feminina", 103)]
    [InlineData("4ª Masculina", 108)]
    [InlineData("5ª Feminina", 111)]
    [InlineData("6ª Masculina", 112)]
    [InlineData("6ª Feminina", 113)]
    public void Casa_o_nome_igual_ao_do_ranking(string nome, int esperado)
    {
        Assert.Equal(esperado, CategoriaDoRankingRs.Adivinhar(nome));
    }

    [Theory]
    [InlineData("6a masculina")]      // sem o "ª"
    [InlineData("6ª MASCULINA")]      // gritando
    [InlineData("  6ª Masculino  ")]  // sobra espaço e o gênero da palavra muda
    [InlineData("Categoria 6ª Masculina")]
    public void Nao_se_perde_por_acento_caixa_ou_espaco(string escrito)
    {
        Assert.Equal(112, CategoriaDoRankingRs.Adivinhar(escrito));
    }

    // O CASO QUE MAIS IMPORTA. "6ª Categoria" é como a maioria dos internos escreve, e não diz
    // o sexo. Chutar "masculina" acertaria quase sempre e erraria calado num torneio feminino
    // — e a API responderia APROVADO do mesmo jeito, porque ela não confere sexo.
    [Theory]
    [InlineData("6ª Categoria")]
    [InlineData("4ª")]
    [InlineData("Categoria B")]
    [InlineData("Mata-Mata Geral")]
    [InlineData("Masculina Feminina")]  // os dois escritos: ambíguo, não casa
    [InlineData("")]
    [InlineData(null)]
    public void Sem_sexo_escrito_NAO_casa(string? escrito)
    {
        Assert.Null(CategoriaDoRankingRs.Adivinhar(escrito));
    }

    // A 7ª e "Iniciantes" existem no Padelizou e NÃO existem no Ranking RS. Casar isso com a
    // 6ª barraria gente que nunca pontuou em lugar nenhum.
    [Theory]
    [InlineData("7ª Masculina")]
    [InlineData("Iniciantes Masculina")]
    public void Categoria_que_o_ranking_nao_tem_NAO_casa(string escrito)
    {
        Assert.Null(CategoriaDoRankingRs.Adivinhar(escrito));
    }

    [Theory]
    [InlineData("Mista A", 114)]
    [InlineData("Mista B", 115)]
    [InlineData("mista c", 116)]
    public void Mista_casa_pela_letra(string escrito, int esperado)
    {
        Assert.Equal(esperado, CategoriaDoRankingRs.Adivinhar(escrito));
    }

    // "Mista" sozinha não diz qual das três faixas do ranking é. A letra "a" de "mista" não
    // pode valer por "Mista A" — por isso a busca é por letra ISOLADA.
    [Fact]
    public void Mista_sem_letra_NAO_casa()
    {
        Assert.Null(CategoriaDoRankingRs.Adivinhar("Mista"));
        Assert.Null(CategoriaDoRankingRs.Adivinhar("Dupla Mista"));
    }

    [Fact]
    public void O_catalogo_tem_os_17_do_ranking_sem_id_repetido()
    {
        Assert.Equal(17, CategoriaDoRankingRs.Catalogo.Count);
        Assert.Equal(17, CategoriaDoRankingRs.Catalogo.Select(c => c.Id).Distinct().Count());
        Assert.All(CategoriaDoRankingRs.Catalogo, c => Assert.InRange(c.Id, 100, 116));
    }

    [Fact]
    public void Id_que_nao_existe_e_reconhecido_como_inexistente()
    {
        Assert.True(CategoriaDoRankingRs.IdExiste(112));
        Assert.False(CategoriaDoRankingRs.IdExiste(999));
        Assert.False(CategoriaDoRankingRs.IdExiste(0));
        Assert.Null(CategoriaDoRankingRs.NomeDoId(999));
        Assert.Equal("6ª Masculina", CategoriaDoRankingRs.NomeDoId(112));
    }

    // Tudo que o Adivinhar devolve tem que ser um id que existe de verdade no catálogo —
    // senão a requisição sai daqui já condenada a levar 400.
    [Fact]
    public void Todo_palpite_e_um_id_do_catalogo()
    {
        foreach (var item in CategoriaDoRankingRs.Catalogo)
        {
            var palpite = CategoriaDoRankingRs.Adivinhar(item.Nome);
            Assert.NotNull(palpite);
            Assert.True(CategoriaDoRankingRs.IdExiste(palpite!.Value));
        }
    }

    // O nome que a lista mostra pro organizador tem que voltar no mesmo id: é assim que a tela
    // de configuração e a validação falam da mesma coisa.
    [Fact]
    public void O_nome_do_catalogo_volta_no_proprio_id()
    {
        foreach (var item in CategoriaDoRankingRs.Catalogo)
        {
            Assert.Equal(item.Id, CategoriaDoRankingRs.Adivinhar(item.Nome));
        }
    }
}
