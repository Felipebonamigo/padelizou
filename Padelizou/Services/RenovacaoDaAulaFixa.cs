using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// A aula fixa SEM PRAZO ("todo sábado às 9h, e pronto").
//
// POR QUE ISTO EXISTE: a aula fixa só sabia repetir por um número de semanas, e o professor
// tinha que escolher um N que ele não tem — o combinado com o aluno não tem data pra acabar.
// Marcava 26 e a série morria calada no fim do semestre; marcava 4 e voltava aqui todo mês.
//
// COMO FUNCIONA: aula sem prazo não gera linha infinita no banco. Ela nasce com um HORIZONTE
// de semanas criadas de uma vez, e este serviço repõe o que o tempo consome — enquanto
// `RecorrenciaSemFim` estiver ligado na série. Desligar (o professor "encerra a repetição")
// não apaga nada: só para de repor.
public static class RenovacaoDaAulaFixa
{
    // Quantas semanas ficam criadas à frente. Alto o bastante pra o aluno enxergar o
    // compromisso e o professor planejar o mês; baixo o bastante pra encerrar a série não
    // virar faxina de um ano de aulas.
    public const int HorizonteSemanas = 12;

    // Renova todas as séries sem prazo que ficaram com menos aulas futuras que o horizonte.
    // Devolve quantas aulas criou.
    public static async Task<int> RenovarAsync(DbPadelContext context, DateTime agora,
        CancellationToken cancellationToken = default)
    {
        // As séries vivas: aula sem prazo, ainda ativa e daqui pra frente. Série cujas aulas
        // futuras acabaram TODAS (professor apagou o resto) não volta a nascer sozinha — sem
        // âncora não dá pra saber em que dia e hora ela repetia.
        var candidatas = await context.Aulas
            .Where(a => a.RecorrenciaSemFim && a.RecorrenciaId != null
                     && a.DataHora >= agora
                     && (a.Status == PoliticaAula.Pendente || a.Status == PoliticaAula.Confirmada))
            .ToListAsync(cancellationToken);

        var criadas = 0;

        foreach (var serie in candidatas.GroupBy(a => a.RecorrenciaId!.Value))
        {
            var ultima = serie.OrderByDescending(a => a.DataHora).First();
            var faltam = HorizonteSemanas - serie.Count();
            if (faltam <= 0) continue;

            // As aulas do professor perto do horizonte, pra não criar aula em cima de outra
            // coisa que ele marcou no meio (inclusive de outro aluno).
            var deOlho = ultima.DataHora;
            var vizinhas = await context.Aulas
                .Where(a => a.ProfessorId == ultima.ProfessorId
                         && a.DataHora > deOlho
                         && a.DataHora <= deOlho.AddDays(7 * (faltam + 1))
                         && (a.Status == PoliticaAula.Pendente || a.Status == PoliticaAula.Confirmada))
                .Select(a => new { a.DataHora, a.DuracaoMinutos, a.TurmaId })
                .ToListAsync(cancellationToken);

            // Colega de TURMA não é conflito: são N séries independentes (uma RecorrenciaId
            // por aluno — ver Models/Aula.TurmaId) que compartilham o mesmo horário DE
            // PROPÓSITO, porque são a mesma sessão. Sem excluir quem tem o TurmaId da
            // própria `ultima`, o renovador de um aluno enxergaria os colegas de turma como
            // "já tem outra coisa marcada" e pularia a semana à toa — o que acontece toda vez
            // que um deles cancela uma aula sozinho e a série dele fica um passo atrás.
            var ocupados = vizinhas
                .Where(v => ultima.TurmaId == null || v.TurmaId != ultima.TurmaId)
                .Select(v => (v.DataHora, v.DuracaoMinutos))
                .ToList();

            for (var i = 1; i <= faltam; i++)
            {
                var quando = ultima.DataHora.AddDays(7 * i);

                if (ocupados.Any(o => DuracaoDaAula.Conflita(quando, ultima.DuracaoMinutos, o.DataHora, o.DuracaoMinutos)))
                {
                    // Semana ocupada é semana PULADA, não motivo pra parar a série: o professor
                    // pode ter encaixado um torneio numa data e a aula segue nas outras.
                    continue;
                }

                context.Aulas.Add(Copiar(ultima, quando));
                ocupados.Add((quando, ultima.DuracaoMinutos));
                criadas++;
            }
        }

        if (criadas > 0) await context.SaveChangesAsync(cancellationToken);

        return criadas;
    }

    // A aula nova é a última da série em outra data. Não leva presença, cancelamento,
    // anotação nem o id do evento no Google — nada disso pertence à aula que ainda não houve.
    private static Aula Copiar(Aula modelo, DateTime quando) => new()
    {
        ProfessorId = modelo.ProfessorId,
        AlunoId = modelo.AlunoId,
        LocalAulaId = modelo.LocalAulaId,
        DataHora = quando,
        DuracaoMinutos = modelo.DuracaoMinutos,
        Preco = modelo.Preco,
        // Nasce no mesmo estado da série: a fixa do professor já vem confirmada; a que o aluno
        // pediu e o professor ainda não aceitou continua esperando resposta.
        Status = modelo.Status,
        NomeAlunoAvulso = modelo.NomeAlunoAvulso,
        TelefoneAlunoAvulso = modelo.TelefoneAlunoAvulso,
        NomeCompletoAluno = modelo.NomeCompletoAluno,
        Acompanhantes = modelo.Acompanhantes,
        QuantidadeAlunos = modelo.QuantidadeAlunos,
        // ESTÁVEL (ver Models/Aula.TurmaId): a semana nova continua sendo a MESMA turma, não
        // uma turma diferente que por acaso caiu no mesmo horário.
        TurmaId = modelo.TurmaId,
        AlunoPagaQuadra = modelo.AlunoPagaQuadra,
        RecorrenciaId = modelo.RecorrenciaId,
        RecorrenciaSemFim = true,
    };
}
