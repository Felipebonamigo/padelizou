using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// "Um card só, com os N nomes" — a decisão do Felipe pra como a turma aparece na agenda. As
// N linhas de Aula que dividem um TurmaId (ver Models/Aula.TurmaId) continuam existindo de
// verdade no banco — é assim que cada aluno tem sua ficha e sua cobrança —, mas a TELA (Minha
// Agenda, calendário e lista) mostra uma representante só, com o preço somado e os nomes
// juntos. As ações que valem pra sessão inteira (Concluir, Cancelar, Apagar, Editar horário)
// cascadeiam pro grupo direto no controller — isto aqui é só o que aparece na tela.
public static class AgendaDeTurma
{
    // Colapsa cada grupo de TurmaId numa linha representante. Aula sem TurmaId passa direto.
    public static List<Aula> Colapsar(IEnumerable<Aula> aulas)
    {
        var resultado = new List<Aula>();

        foreach (var grupo in aulas.GroupBy(a => a.TurmaId))
        {
            if (grupo.Key == null)
            {
                resultado.AddRange(grupo);
                continue;
            }

            // A ordem por Id é só pra ser DETERMINÍSTICA (mesmo grupo sempre escolhe a mesma
            // representante) — quem decide o que aparece de verdade é NomesJuntos, que junta
            // todo mundo, não só o primeiro.
            var linhas = grupo.OrderBy(a => a.Id).ToList();
            var representante = linhas[0];

            resultado.Add(new Aula
            {
                Id = representante.Id,
                ProfessorId = representante.ProfessorId,
                AlunoId = representante.AlunoId,
                Aluno = representante.Aluno,
                LocalAulaId = representante.LocalAulaId,
                LocalAula = representante.LocalAula,
                DataHora = representante.DataHora,
                DuracaoMinutos = representante.DuracaoMinutos,
                // A soma das fatias: é o valor da sessão inteira, que é o que faz sentido
                // mostrar num card que representa a turma toda.
                Preco = linhas.Sum(a => a.Preco),
                Status = representante.Status,
                QuantidadeAlunos = representante.QuantidadeAlunos,
                TurmaId = representante.TurmaId,
                // Prioridade mais alta na leitura do nome (ver NomeDoAluno em
                // MinhaAgenda.cshtml) — sobrepõe Aluno?.Nome e NomeAlunoAvulso de propósito.
                NomeCompletoAluno = NomesJuntos(linhas.Select(NomeDeExibicao)),
                NomeAlunoAvulso = representante.NomeAlunoAvulso,
                TelefoneAlunoAvulso = representante.TelefoneAlunoAvulso,
                Acompanhantes = representante.Acompanhantes,
                RecorrenciaId = representante.RecorrenciaId,
                RecorrenciaSemFim = representante.RecorrenciaSemFim,
                GoogleEventId = representante.GoogleEventId,
                Compareceu = representante.Compareceu,
                CanceladaEm = representante.CanceladaEm,
                CanceladaPor = representante.CanceladaPor,
                CobrarMesmoFaltando = representante.CobrarMesmoFaltando,
                AlunoPagaQuadra = representante.AlunoPagaQuadra,
                RecuperaAulaId = representante.RecuperaAulaId,
                RecuperaAula = representante.RecuperaAula,
            });
        }

        return resultado.OrderBy(a => a.DataHora).ToList();
    }

    private static string NomeDeExibicao(Aula a) =>
        a.NomeCompletoAluno ?? a.Aluno?.Nome ?? a.NomeAlunoAvulso ?? "Aluno avulso";

    // "Fulano, Beltrano e Cicrano" — vírgula entre todos, "e" só antes do último. Mesma regra
    // do título do evento único na Google Agenda (ver AulasController.Agenda.NomesJuntos).
    public static string NomesJuntos(IEnumerable<string?> nomes)
    {
        var lista = nomes.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return lista.Count switch
        {
            0 => "Aluno",
            1 => lista[0]!,
            _ => string.Join(", ", lista.Take(lista.Count - 1)) + " e " + lista[^1],
        };
    }
}
