using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A ORDEM EM QUE AS CATEGORIAS SE LEEM NO SITE.
//
// 🗣️ Felipe, 10/09/2026, com o seletor de "Chaves e Grupos" no ar: *"esta fora de ordem"* — e,
// escolhendo entre as três ordens possíveis, a por NÍVEL com a masculina na frente.
//
// 🕳️ Ela nasceu em 08/08 agrupando por sexo: TODAS as masculinas na escada, depois todas as
// femininas. Num torneio com as duas escadas, isso empurra a 3ª Feminina pra depois da 6ª
// Masculina — quem procura a chave da 3ª acha duas listas de níveis em vez de uma.
//
// ⚠️ A MUDANÇA É DA RÉGUA DO SITE INTEIRO, e não só do seletor: é ela que as outras listas de
// categoria já usam (Details, Times/Detalhes, ElencoPorCategoria). Duas ordens para a mesma
// lista é o defeito que se está consertando, não o remédio.
public class OrdemDeTelaDasCategoriasTests
{
    [Fact]
    public void Os_niveis_andam_juntos_com_a_masculina_na_frente()
    {
        var nomes = new[] { "6ª Categoria Masculina", "3ª Categoria Feminina", "Mista A",
                            "3ª Categoria Masculina", "5ª Categoria Feminina", "Open Masculina" };

        Assert.Equal(
            new[] { "Open Masculina", "3ª Categoria Masculina", "3ª Categoria Feminina",
                    "5ª Categoria Feminina", "6ª Categoria Masculina", "Mista A" },
            nomes.OrderBy(CategoriaNaTela.Ordem).ToList());
    }

    [Fact]
    public void Mista_e_casais_fecham_a_lista_mesmo_sem_nivel()
    {
        // Elas não têm degrau ("Mista A" não diz nível nenhum), então caem num degrau só, no
        // fim — e se desempatam pelo nome, que é o que põe Mista A antes de Mista B.
        var nomes = new[] { "Casais B", "Mista B", "Mista A", "7ª Categoria Masculina" };

        Assert.Equal(
            new[] { "7ª Categoria Masculina", "Mista A", "Mista B", "Casais B" },
            nomes.OrderBy(CategoriaNaTela.Ordem).ToList());
    }

    // A `Ordem` também responde de que escada a categoria é (SexoDaEscada lê o Grupo dela) —
    // mexer na comparação não pode mexer nisso.
    [Fact]
    public void A_escada_de_cada_categoria_continua_a_mesma()
    {
        Assert.Equal(SexoDoJogador.Masculino, CategoriaNaTela.SexoDaEscada("3ª Categoria Masculina"));
        Assert.Equal(SexoDoJogador.Feminino, CategoriaNaTela.SexoDaEscada("3ª Categoria Feminina"));
        Assert.Null(CategoriaNaTela.SexoDaEscada("Mista A"));
    }
}
