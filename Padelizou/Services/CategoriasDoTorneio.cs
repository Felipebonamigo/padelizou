using Padelizou.Models;

namespace Padelizou.Services;

// Mexer nas categorias de um torneio JÁ PUBLICADO.
//
// Na criação a escolha é livre — não existe ninguém inscrito. Depois de publicado, cada
// categoria pode ter gente dentro, e é aí que a regra importa: tirar uma categoria com dupla
// inscrita não é "editar o torneio", é apagar a inscrição de alguém que já pagou. O organizador
// que só queria corrigir um engano ("marquei a 7ª sem querer") não pode ter o poder de fazer
// isso sem perceber.
//
// Adicionar é sempre seguro: categoria nova nasce vazia e ninguém perde nada.
public static class CategoriasDoTorneio
{
    // Por que esta categoria NÃO pode sair, ou null quando pode.
    public static string? MotivoParaNaoRemover(Categoria categoria, Torneio torneio)
    {
        // Depois do sorteio existem grupos e jogos apontando pra categoria: removê-la
        // derrubaria a chave inteira, não só a inscrição.
        if (torneio.Status != "Inscrições Abertas")
            return $"O torneio já saiu da fase de inscrições — a categoria \"{categoria.Nome}\" "
                 + "não pode mais ser removida.";

        var inscritos = categoria.Duplas.Count;
        if (inscritos > 0)
            return $"A categoria \"{categoria.Nome}\" tem {inscritos} inscrição(ões). "
                 + "Remova os inscritos primeiro — tirar a categoria apagaria a inscrição deles.";

        return null;
    }

    // Um torneio sem categoria nenhuma é um torneio em que ninguém consegue se inscrever: o
    // formulário escolhe a categoria, e sem opção não há o que escolher. A mesma regra vale na
    // criação (ver TorneiosController.Criacao).
    public static string? MotivoParaNaoFicarSemNenhuma(int quantasSobram) =>
        quantasSobram <= 0
            ? "O torneio precisa de pelo menos uma categoria — é nela que os jogadores se inscrevem."
            : null;
}
