using System.Globalization;

namespace Padelizou.Services;

// A faixa de datas que o Financeiro do professor está somando.
//
// `Ate` NULO quer dizer "até hoje, e o que vier" — é o que os quatro períodos originais
// (semana, mês, ano, sempre) sempre fizeram, e continuam fazendo. Só o mês fechado tem as
// duas pontas.
public readonly record struct FaixaDoFinanceiro(DateTime De, DateTime? Ate, string Rotulo)
{
    // A pergunta "esta aula conta?", num lugar só.
    //
    // ⚠️ Existe porque agora há DOIS formatos de período na mesma tela. O controller comparava
    // `a.DataHora >= de` solto em cada lugar que precisava — com um intervalo fechado no jogo,
    // uma dessas comparações acabaria esquecendo o fim, e aí o card do topo e a lista de quem
    // está devendo discordariam sobre a mesma aula.
    public bool Contem(DateTime quando) => quando >= De && (Ate == null || quando < Ate.Value);
}

// Qual período o professor escolheu, e o que isso quer dizer em datas.
//
// 🗣️ Pedido do Felipe, 02/09/2026: "uma aba de conferir o mês passado no financeiro".
//
// 🕳️ O que faltava não era um filtro, era um CONCEITO: os quatro períodos que já existiam são
// todos ABERTOS — o controller somava tudo com `DataHora >= de`, sem fim nenhum. "Mês passado"
// é o primeiro intervalo com as duas pontas; sem a de cima ele mostraria "de 1º de agosto até
// hoje", que inclui setembro e não responde a pergunta que o professor fez.
//
// A régua saiu do `switch` de dentro do controller porque agora ela tem dois valores pra
// acertar em vez de um — e errar o fim do intervalo erra todo número de dinheiro da página de
// uma vez.
public static class PeriodoDoFinanceiro
{
    public const string MesPassado = "mespassado";

    public static FaixaDoFinanceiro Intervalo(string? periodo, DateTime hoje)
    {
        switch ((periodo ?? "mes").Trim().ToLowerInvariant())
        {
            case "semana":
                // Segunda a domingo, a mesma régua do card de semanas do mês
                // (Services/SemanasDoMes) — duas definições de semana na mesma tela seriam
                // dois números pro mesmo dia.
                return new(hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7)), null, "nesta semana");

            case "ano":
                return new(new DateTime(hoje.Year, 1, 1), null, $"em {hoje.Year}");

            case "sempre":
                return new(DateTime.MinValue, null, "desde sempre");

            case MesPassado:
            {
                var inicioDesteMes = new DateTime(hoje.Year, hoje.Month, 1);
                var inicio = inicioDesteMes.AddMonths(-1);

                // ⚠️ O FIM É EXCLUSIVO, e não "o último dia do mês": data de aula tem HORA, e
                // com fim inclusivo em 31/08 a aula das 20h do dia 31 ficaria de fora. O
                // professor veria o mês fechado com uma aula a menos do que ele deu.
                return new(inicio, inicioDesteMes, $"em {NomeDoMes(inicio)}");
            }

            default:
                return new(new DateTime(hoje.Year, hoje.Month, 1), null, "neste mês");
        }
    }

    // "agosto", não "08/2026": o rótulo entra no meio de uma frase da tela ("Recebido em
    // agosto"), e número ali lê como código.
    private static string NomeDoMes(DateTime mes)
    {
        var cultura = new CultureInfo("pt-BR");
        var nome = mes.ToString("MMMM", cultura);

        // O ano só aparece quando NÃO é o corrente — em janeiro, "em dezembro" sozinho deixa
        // o professor sem saber de qual dezembro se trata.
        return mes.Year == DateTime.Today.Year ? nome : $"{nome} de {mes.Year}";
    }
}
