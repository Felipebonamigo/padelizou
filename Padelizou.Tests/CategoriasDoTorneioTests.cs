using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Dava pra escolher as categorias só na criação: quem esquecia a Mista tinha que criar OUTRO
// torneio e dividir os inscritos entre os dois. Adicionar é sempre seguro (categoria nova
// nasce vazia); TIRAR é que é delicado, e é isso que estes testes seguram.
public class CategoriasDoTorneioTests
{
    private static Torneio Torneio(string status = "Inscrições Abertas") =>
        new() { Id = 1, Nome = "Copa", Codigo = "C1", Status = status };

    private static Categoria Categoria(int inscritos)
    {
        var cat = new Categoria { Id = 5, Nome = "Categoria Mista A", Codigo = "MA", TorneioId = 1 };
        for (int i = 0; i < inscritos; i++)
        {
            cat.Duplas.Add(new Dupla { Id = i + 1, CategoriaId = 5, Jogador1Id = i + 1, Codigo = $"D{i}" });
        }
        return cat;
    }

    [Fact]
    public void Categoria_vazia_com_inscricoes_abertas_pode_sair()
        => Assert.Null(CategoriasDoTorneio.MotivoParaNaoRemover(Categoria(0), Torneio()));

    [Fact]
    public void Categoria_com_inscrito_nao_sai()
    {
        // Não é "editar o torneio", é apagar a inscrição de alguém que já pagou.
        var motivo = CategoriasDoTorneio.MotivoParaNaoRemover(Categoria(3), Torneio());

        Assert.Contains("3 inscrição", motivo);
        Assert.Contains("Remova os inscritos primeiro", motivo);
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Em Andamento")]
    [InlineData("Finalizado")]
    public void Depois_do_sorteio_nao_sai_nem_vazia(string status)
    {
        // Existem grupos e jogos apontando pra categoria: removê-la derrubaria a chave.
        var motivo = CategoriasDoTorneio.MotivoParaNaoRemover(Categoria(0), Torneio(status));

        Assert.Contains("fase de inscrições", motivo);
    }

    [Fact]
    public void Torneio_nao_pode_ficar_sem_categoria_nenhuma()
    {
        // O formulário de inscrição ESCOLHE a categoria: sem nenhuma, ninguém entra.
        Assert.NotNull(CategoriasDoTorneio.MotivoParaNaoFicarSemNenhuma(0));
        Assert.Null(CategoriasDoTorneio.MotivoParaNaoFicarSemNenhuma(1));
    }
}
