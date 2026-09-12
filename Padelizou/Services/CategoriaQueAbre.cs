using Padelizou.Models;

namespace Padelizou.Services;

// EM QUE CATEGORIA A ABA "Chaves e Grupos" ABRE.
//
// 🗣️ Felipe, 10/09/2026: *"venha sempre selecionado a categoria que o usuario esta cadastrado
// (se estiver em duas, vem na melhor delas 1>2>3>4)"*.
//
// Quem abre a aba quase sempre quer UMA chave: a sua. Abrindo na primeira da lista, o jogador
// de 6ª tinha que escolher a dele toda vez que entrasse — e num torneio de 12 categorias isso
// é uma escolha por visita pra ver o que ele veio ver.
//
// ⚠️ NÃO É UMA SEGUNDA ORDEM DE TELA: quem diz o que vem antes continua sendo o
// `CategoriaNaTela.Ordem` (masculinas na escada, depois femininas, depois mista e casais), e é
// dele que sai o NÍVEL usado aqui. Duas contas de "qual é a melhor" divergiriam no dia em que
// o catálogo ganhasse um degrau novo.
public static class CategoriaQueAbre
{
    // `null` só quando não há categoria nenhuma pra mostrar — aí a tela não desenha seletor.
    public static Categoria? Escolher(IEnumerable<Categoria> comChave, int? meuJogadorId)
    {
        var lista = comChave.ToList();

        // Sem ninguém logado `meuJogadorId` é null, nenhuma dupla casa, e a tela cai na lista
        // inteira pelo MESMO caminho de quem não está inscrito em nenhuma das listadas — o
        // organizador, o curioso, e quem se inscreveu numa categoria que ainda não tem chave
        // (essa nem chega aqui).
        var minhas = lista.Where(c => c.Duplas.Any(d => d.Jogador1Id == meuJogadorId
                                                     || d.Jogador2Id == meuJogadorId)).ToList();

        // "A melhor" é a PRIMEIRA da ordem de tela, sem critério novo: desde 10/09 aquela régua
        // já lidera pelo nível, que é exatamente o "1>2>3>4" que o Felipe pediu. Escrever aqui
        // uma segunda conta de "qual é a melhor" faria as duas divergirem no dia em que o
        // catálogo ganhasse um degrau novo.
        return (minhas.Count > 0 ? minhas : lista)
            .OrderBy(c => CategoriaNaTela.Ordem(c.Nome))
            .FirstOrDefault();
    }
}
