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
    //
    // ⚠️ A RÉGUA É O QUE APONTA PRA CATEGORIA, NÃO A FASE (27/11/2026). 🗣️ Rafael (Treta): "não
    // consegui excluir essas categorias testes" — criadas com o torneio já sorteado, ficavam
    // presas. Grupo ou jogo nela = recusa (removê-la derrubaria a chave); vazia de verdade sai
    // em qualquer fase. `temGrupoOuJogo` vem do BANCO, perguntado por quem chama: navegação não
    // carregada leria vazio e liberaria o que não devia.
    public static string? MotivoParaNaoRemover(Categoria categoria, Torneio torneio, bool temGrupoOuJogo)
    {
        if (temGrupoOuJogo)
            return $"A categoria \"{categoria.Nome}\" já tem grupos ou jogos — removê-la derrubaria a chave.";

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
