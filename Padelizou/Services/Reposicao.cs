using padelizou.Models;

namespace Padelizou.Services;

// "Vai recuperar": o aluno não vem hoje, a aula é COBRADA assim mesmo, e ele reponha outro dia.
//
// POR QUE EXISTE: quem cobra por mês não desconta a falta — o aluno paga o mês inteiro e
// recupera a aula depois. O sistema só sabia dizer duas coisas sobre quem não vem, e nenhuma
// das duas é essa:
//
//   Cancelada  — some da agenda e NÃO cobra. Perde o dinheiro do mensalista.
//   Faltou + CobrarMesmoFaltando — cobra, mas não gera crédito nenhum: nada lembra o
//              professor de que ele deve uma aula, e a fila de reposições vive no WhatsApp.
//
// O QUE MUDA: a aula vira `PoliticaAula.ARecuperar`, que (1) sai das ativas, liberando o
// horário pra outro aluno no mesmo dia; (2) continua cobrável, porque marca
// `CobrarMesmoFaltando` — o financeiro não precisou aprender nada novo; e (3) fica numa
// fila de pendências até o professor encaixar a reposição.
public static class Reposicao
{
    // A reposição NÃO cobra de novo. O dinheiro ficou na aula original, que segue cobrável —
    // dar preço à reposição faria o professor cobrar duas vezes a mesma aula, que é o oposto
    // do combinado com o aluno ("tu paga o mês e recupera").
    //
    // Zero gravado, e não "excluída das somas": um zero é visível na tela e no relatório,
    // enquanto uma exceção escondida numa consulta é a que ninguém encontra quando a conta
    // não fecha.
    public const decimal PrecoDaReposicao = 0m;

    // Só aula CONFIRMADA vira "vai recuperar". Pendente ainda nem foi aceita — não há o que
    // repor. Realizada aconteceu. Cancelada e Recusada já são fato registrado, e a que já foi
    // marcada não se remarca de novo.
    public static bool PodeMarcar(Aula aula) => aula.Status == PoliticaAula.Confirmada;

    public static string MotivoParaNaoMarcar(Aula aula) => aula.Status switch
    {
        PoliticaAula.Pendente => "Essa aula ainda está esperando sua confirmação — aceite ou recuse antes.",
        PoliticaAula.Realizada => "Essa aula já foi dada.",
        PoliticaAula.ARecuperar => "Essa aula já está na fila de reposição.",
        PoliticaAula.Faltou => "Essa aula está registrada como falta.",
        PoliticaAula.Cancelada => "Essa aula foi cancelada — cancelamento não gera reposição.",
        PoliticaAula.Recusada => "Essa aula foi recusada.",
        _ => "Essa aula não pode ir pra fila de reposição.",
    };

    // A fila: aulas a recuperar que ainda NÃO têm reposição marcada. Sai da fila no momento
    // em que o professor encaixa a aula nova — não quando ela acontece, senão o horário
    // apareceria como pendente até o dia da reposição e ele encaixaria a mesma aula duas vezes.
    public static List<Aula> AindaSemEncaixe(IEnumerable<Aula> aulas, ISet<int> jaEncaixadas) =>
        aulas
            .Where(a => a.Status == PoliticaAula.ARecuperar && !jaEncaixadas.Contains(a.Id))
            .OrderBy(a => a.DataHora)
            .ToList();

    // A aula nova, montada a partir da que ficou devendo. Herda o aluno, o tamanho e quem
    // paga a quadra — é a MESMA aula, noutro dia. O que não herda é o preço (ver acima) e a
    // recorrência: reposição é avulsa por definição, e entrar na série faria o renovador da
    // aula fixa repeti-la toda semana.
    //
    // Nasce Confirmada porque quem está marcando é o professor, combinando com o aluno — não
    // é pedido esperando resposta.
    public static Aula Encaixar(Aula original, int localId, DateTime quando, int duracaoMinutos) =>
        new()
        {
            ProfessorId = original.ProfessorId,
            AlunoId = original.AlunoId,
            NomeAlunoAvulso = original.NomeAlunoAvulso,
            TelefoneAlunoAvulso = original.TelefoneAlunoAvulso,
            NomeCompletoAluno = original.NomeCompletoAluno,
            Acompanhantes = original.Acompanhantes,
            QuantidadeAlunos = original.QuantidadeAlunos,
            AlunoPagaQuadra = original.AlunoPagaQuadra,
            LocalAulaId = localId,
            DataHora = quando,
            DuracaoMinutos = duracaoMinutos,
            Preco = PrecoDaReposicao,
            Status = PoliticaAula.Confirmada,
            RecuperaAulaId = original.Id,
        };

    // O que a tela escreve na aula que repõe outra. Sem isto, o professor abre a agenda daqui
    // a duas semanas, vê uma aula de R$ 0,00 e não faz ideia de por quê.
    public static string EtiquetaDaReposicao(DateTime dataDaOriginal) =>
        $"Reposição da aula de {dataDaOriginal:dd/MM}";
}
