using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma tentativa de inscrição que o Ranking RS reprovou.
//
// Por que isso vira LINHA no banco em vez de só uma mensagem de erro: a recusa não é a palavra
// final. O jogador é barrado e avisado na hora, mas quem decide se a recusa fica de pé é o
// ORGANIZADOR — e pra ele decidir, precisa existir uma lista pra ele olhar. Sem isto, o
// barrado teria que caçar o organizador no WhatsApp e o organizador não saberia de nada.
//
// ⚠️ A identidade aqui é o CPF, não o JogadorId. Na hora em que a validação roda, a pessoa
// pode ainda NÃO ter linha em Jogador — a inscrição cria o jogador só depois de passar por
// todas as regras (ver DuplasController.Create, passo 3). O CPF é o que existe desde o
// primeiro instante e é por ele que o resto do sistema reencontra a pessoa.
[Table("BloqueioDoRanking")]
public class BloqueioDoRanking
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public virtual Torneio Torneio { get; set; } = null!;

    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; } = null!;

    public string Cpf { get; set; } = null!;

    // O nome exatamente como foi mandado pra API — é ele que casa (ou não) com o ranking, e
    // ver o texto que foi consultado é o que permite entender um bloqueio errado por homônimo.
    public string NomeConsultado { get; set; } = null!;

    // Contra qual categoria do ranking a pergunta foi feita.
    public int CategoriaRsId { get; set; }
    public string? CategoriaRsNome { get; set; }

    // A explicação que veio da API, já montada pra leitura ("... pontos em 2ª Masculina
    // (235 pts) e 3ª Masculina (170 pts)"). Guardada porque o ranking muda com o tempo:
    // reconsultar meses depois daria outra resposta, e a decisão do organizador foi tomada
    // com base NESTA.
    public string Motivo { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string Situacao { get; set; } = SituacaoDoBloqueio.Pendente;

    public int? DecididoPorId { get; set; }
    public virtual Jogador? DecididoPor { get; set; }
    public DateTime? DecididoEm { get; set; }
}

public static class SituacaoDoBloqueio
{
    // Barrado, e o organizador ainda não olhou.
    public const string Pendente = "Pendente";

    // O organizador liberou: esta pessoa passa a poder se inscrever NESTA categoria, e a
    // consulta ao ranking nem chega a ser feita de novo pra ela.
    public const string Liberado = "Liberado";

    // O organizador olhou e manteve a recusa. Continua barrando, mas some da fila de "pra
    // decidir" — senão a mesma linha voltaria a pedir atenção toda vez que a tela abrisse.
    public const string Mantido = "Mantido";

    public static bool EhValida(string? situacao) =>
        situacao is Pendente or Liberado or Mantido;
}
