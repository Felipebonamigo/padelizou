using padelizou.Models;

namespace Padelizou.ViewModels;

// As aulas do ALUNO, em duas listas.
//
// Eram uma lista só, ordenada da data maior pra menor — o que colocava a aula mais DISTANTE
// no futuro em cima e a de amanhã no meio do monte, depois de todas as outras futuras. A
// pergunta que a pessoa leva pra essa tela é "quando é a minha próxima aula?", e a resposta
// estava em qualquer lugar menos no topo.
public class MinhasAulasVM
{
    // Da mais perto pra mais longe: a primeira linha é a próxima aula.
    public List<Aula> Proximas { get; set; } = new();

    // Da mais recente pra mais antiga: aqui a pergunta é "o que eu fiz ultimamente?".
    // Entra também a aula futura que foi cancelada/recusada — ela não está marcada.
    public List<Aula> Anteriores { get; set; } = new();
}
