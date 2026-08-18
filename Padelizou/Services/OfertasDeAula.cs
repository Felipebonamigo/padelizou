using Padelizou.Models;

namespace Padelizou.Services;

// Um horário livre de verdade: QUEM dá a aula, ONDE, QUANDO e por quanto tempo.
//
// O aluno não pensa em "professor -> local -> horário" nessa ordem. Muita gente tem uma
// janela na semana (terça de manhã) e aceita qualquer professor que caiba nela. Por isso a
// oferta carrega as quatro coisas juntas: a tela filtra por qualquer uma delas e as outras
// se ajustam.
public readonly record struct OfertaDeAula(int ProfessorId, int LocalId, DateTime Quando, int DuracaoMinutos);

// A grade do professor virada em horários livres.
//
// POR QUE É UM SERVIÇO: esta conta já morava dentro de AulasController.ObterHorarios, escopada
// a UM professor e UM local. Ao abrir a busca pra cidade inteira, ou ela saía do controller ou
// nasceria uma segunda cópia — e regra duplicada é a origem dos bugs graves deste sistema.
public static class OfertasDeAula
{
    public static List<OfertaDeAula> Gerar(
        IEnumerable<HorarioDisponivel> regras,
        IEnumerable<(int ProfessorId, DateTime DataHora, int DuracaoMinutos)> ocupadas,
        DateTime agora,
        int dias)
    {
        // Por professor: a agenda que trava um horário é a DELE, não a do local — o mesmo
        // professor não pode estar em duas quadras às 9h, ainda que sejam locais diferentes.
        var agendaPorProfessor = ocupadas
            .GroupBy(o => o.ProfessorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ofertas = new List<OfertaDeAula>();
        var hoje = agora.Date;

        foreach (var regra in regras.Where(r => r.Ativo))
        {
            var agenda = agendaPorProfessor.TryGetValue(regra.ProfessorId, out var lista)
                ? lista
                : new List<(int ProfessorId, DateTime DataHora, int DuracaoMinutos)>();

            for (var dia = 0; dia < dias; dia++)
            {
                var data = hoje.AddDays(dia);
                if ((int)data.DayOfWeek != regra.DiaSemana) continue;

                var horario = data.Add(regra.HoraInicio);
                var fimJanela = data.Add(regra.HoraFim);

                while (horario.AddMinutes(regra.DuracaoMinutos) <= fimJanela)
                {
                    // Sobreposição, não igualdade: aula de 2h ocupa DOIS slots de uma hora, e
                    // comparar só o início oferecia ao aluno o segundo deles.
                    var livre = !agenda.Any(o =>
                        DuracaoDaAula.Conflita(horario, regra.DuracaoMinutos, o.DataHora, o.DuracaoMinutos));

                    if (horario > agora && livre)
                    {
                        ofertas.Add(new OfertaDeAula(regra.ProfessorId, regra.LocalAulaId, horario, regra.DuracaoMinutos));
                    }

                    horario = horario.AddMinutes(regra.DuracaoMinutos);
                }
            }
        }

        return ofertas
            .OrderBy(o => o.Quando)
            .ThenBy(o => o.ProfessorId)
            .ThenBy(o => o.LocalId)
            .ToList();
    }
}
