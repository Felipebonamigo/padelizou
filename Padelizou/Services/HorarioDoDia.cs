using System.Globalization;
using Padelizou.Models;

namespace Padelizou.Services;

// O HORÁRIO DE CADA DIA DO TORNEIO (11/11/2026) — o lugar ÚNICO que o motor da grade consulta.
//
// 🗣️ Felipe, com o Planejamento do THE LAST DANCE aberto: *"tem que permitir separar por dia,
// que horas começa e que horas termina"*. O torneio só tinha três horas — abre no 1º dia, abre
// nos demais e UM "último jogo começa até" pra todos os dias. Agora um dia pode ter a própria
// faixa (`Torneio.HorariosPorDia`); o dia sem faixa segue os três campos.
//
// ⚠️ TORNEIO SEM FAIXA NENHUMA PASSA PELO CÓDIGO DE ANTES, literalmente: `Horarios` e `DepoisDe`
// delegam ao GradeDeJogos com os mesmos argumentos de sempre. Toda grade que já existe sai
// idêntica — é o que HorarioProprioPorDiaTests prova, e o que os ~8 mil testes do motor seguem
// protegendo. O caminho novo só roda em torneio que gravou faixa.
public static class HorarioDoDia
{
    /// <summary>A faixa de um dia: quando abre e até que horas um jogo pode COMEÇAR.</summary>
    public sealed record Faixa(TimeSpan Abre, TimeSpan UltimoJogo);

    // "yyyy-MM-dd=HH:mm-HH:mm;..." — o formato do `Torneio.HorariosPorDia`. Entrada ilegível,
    // ou faixa que fecha antes de abrir, é IGNORADA: um dia assim faria a grade não virar e
    // empilhar o torneio inteiro numa madrugada.
    public static IReadOnlyDictionary<DateTime, Faixa> Ler(string? texto)
    {
        var faixas = new Dictionary<DateTime, Faixa>();
        if (string.IsNullOrWhiteSpace(texto)) return faixas;

        foreach (var pedaco in texto.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = pedaco.Split('=');
            if (partes.Length != 2) continue;
            if (!DateTime.TryParseExact(partes[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dia)) continue;

            var horas = partes[1].Split('-');
            if (horas.Length != 2 || LerHora(horas[0]) is not TimeSpan abre || LerHora(horas[1]) is not TimeSpan ultimo)
                continue;
            if (ultimo <= abre) continue;

            faixas[dia.Date] = new Faixa(abre, ultimo);
        }

        return faixas;
    }

    // Em ordem de data. Nulo quando não sobra dia nenhum — é o "sem horário próprio".
    public static string? Escrever(IReadOnlyDictionary<DateTime, Faixa> faixas) =>
        faixas.Count == 0
            ? null
            : string.Join(';', faixas.OrderBy(f => f.Key)
                .Select(f => $"{f.Key:yyyy-MM-dd}={f.Value.Abre:hh\\:mm}-{f.Value.UltimoJogo:hh\\:mm}"));

    public static TimeSpan? LerHora(string? texto) =>
        TimeSpan.TryParseExact((texto ?? "").Trim(), [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss"],
            CultureInfo.InvariantCulture, out var hora) ? hora : null;

    // Sem `DataInicio`, o 1º dia é HOJE — a mesma convenção do `Torneio.AberturaDaGrade` de sempre.
    public static Calendario Do(Torneio torneio) => new(
        (torneio.DataInicio ?? DateTime.Today).Date, torneio.HoraInicioDoDia, torneio.HoraInicioDiasSeguintes,
        torneio.HoraFimDoDia, Ler(torneio.HorariosPorDia));

    /// <summary>O calendário de um torneio: a faixa de qualquer dia, e a grade que nasce dele.</summary>
    public sealed class Calendario
    {
        private readonly DateTime? _primeiroDia;
        private readonly TimeSpan _abrePrimeiro, _abreDemais, _limite;
        private readonly IReadOnlyDictionary<DateTime, Faixa> _porDia;

        public Calendario(DateTime? primeiroDia, TimeSpan abrePrimeiro, TimeSpan abreDemais, TimeSpan limite,
            IReadOnlyDictionary<DateTime, Faixa> porDia)
        {
            _primeiroDia = primeiroDia;
            _abrePrimeiro = abrePrimeiro;
            _abreDemais = abreDemais;
            _limite = limite;
            _porDia = porDia;
        }

        public bool TemDiaProprio => _porDia.Count > 0;

        public TimeSpan Abre(DateTime dia) =>
            _porDia.TryGetValue(dia.Date, out var faixa) ? faixa.Abre
            : dia.Date == _primeiroDia ? _abrePrimeiro
            : _abreDemais;

        // Variante pros laços que contam "o 1º dia" a partir de outro começo (o recálculo do meio do
        // torneio, por exemplo): sem faixa própria, a regra é a de sempre — a abertura do 1º dia
        // DA CONTA —, e torneio sem faixa nenhuma não muda um minuto.
        public TimeSpan Abre(DateTime dia, bool primeiroDaConta) =>
            _porDia.TryGetValue(dia.Date, out var faixa) ? faixa.Abre
            : primeiroDaConta ? _abrePrimeiro
            : _abreDemais;

        public TimeSpan Limite(DateTime dia) =>
            _porDia.TryGetValue(dia.Date, out var faixa) ? faixa.UltimoJogo : _limite;

        // A grade a partir de `inicio`, como GradeDeJogos.Horarios: as quadras enchem antes de o
        // relógio andar, e passado o último início do dia a grade vira pra abertura do seguinte
        // — aqui, a abertura DAQUELE dia.
        public IEnumerable<DateTime> Horarios(DateTime inicio, int quadras, int duracaoMinutos, int quantidade)
        {
            if (!TemDiaProprio)
                return GradeDeJogos.Horarios(inicio, _limite, quadras, duracaoMinutos, quantidade, _abreDemais);

            return PorDia(inicio, quadras, duracaoMinutos, quantidade);
        }

        private IEnumerable<DateTime> PorDia(DateTime inicio, int quadras, int duracaoMinutos, int quantidade)
        {
            if (quantidade <= 0) yield break;
            quadras = Math.Max(quadras, 1);
            duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;

            var horario = inicio;
            int naQuadra = 0;

            for (int i = 0; i < quantidade; i++)
            {
                if (naQuadra == quadras)
                {
                    horario = Seguinte(horario, duracaoMinutos);
                    naQuadra = 0;
                }

                yield return horario;
                naQuadra++;
            }
        }

        // O próximo horário depois de `h`, como GradeDeJogos.DepoisDe.
        public DateTime DepoisDe(DateTime h, int duracaoMinutos)
        {
            if (!TemDiaProprio)
                return GradeDeJogos.DepoisDe(h, _limite, _abreDemais, duracaoMinutos);

            return Seguinte(h, duracaoMinutos > 0 ? duracaoMinutos : 50);
        }

        private DateTime Seguinte(DateTime h, int duracaoMinutos)
        {
            var proximo = h.AddMinutes(duracaoMinutos);

            // Mesma guarda do motor: dia cujo limite não passa da abertura não vira — sem ela a
            // virada procuraria pra sempre um dia que comporte jogo.
            if (Limite(h.Date) > Abre(h.Date) && proximo > h.Date.Add(Limite(h.Date)))
            {
                var amanha = h.Date.AddDays(1);
                proximo = amanha.Add(Abre(amanha));
            }

            return proximo;
        }
    }
}
