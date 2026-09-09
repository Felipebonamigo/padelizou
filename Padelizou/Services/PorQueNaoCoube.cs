using Padelizou.Models;

namespace Padelizou.Services;

// POR QUE O TORNEIO NÃO CABE ATÉ O DIA QUE O ORGANIZADOR MARCOU COMO LIMITE.
//
// 🗣️ Felipe, 09/09/2026, sobre a grade do Er ter marcado jogo numa terça (15/09) num torneio que
// termina no domingo (13/09): *"avisa na hora do sorteio que não cabe, mas nesse caso ai, fizemos
// todo o planejamento, tem que caber, se não couber, tem q avisar por que nao coube"*.
//
// 🕳️ `Torneio.DataFim` EXISTIA E O MOTOR NUNCA A LEU. Era um aviso de tela na previsão
// (`PrevisaoGradeVM.EstouraOPrazo`, "Passa do dia 13/09") e nada mais: a grade rolava por quantos
// dias precisasse. E o aviso dizia QUE passou, nunca POR QUÊ — que é a única metade acionável.
//
// ⚠️ O AVISO NÃO TRAVA O SORTEIO, e isso é escolha. A regra do motor é que jogo sem horário é o
// único desfecho inaceitável (ver GradeDeJogos.Encaixar), e recusar o sorteio deixaria o
// organizador com um torneio inteiro sem grade nenhuma na véspera. Ele avisa, com a causa na mão,
// e quem decide é quem alugou a quadra.
//
// ── AS TRÊS CAUSAS, NA ORDEM EM QUE AJUDAM ──────────────────────────────────────────────
// As duas primeiras são configuração errada e têm conserto de um clique; a terceira é aritmética
// e pede decisão. Por isso saem nesta ordem, e não na ordem em que são calculadas.
public static class PorQueNaoCoube
{
    /// <summary>
    /// As razões pelas quais a grade passa (ou passaria) do <see cref="Torneio.DataFim"/>.
    /// Lista vazia quer dizer que cabe — ou que não há prazo marcado pra estourar.
    /// </summary>
    /// <param name="ultimoJogo">
    /// O começo do último jogo JÁ MARCADO, quando a grade existe. Nulo antes do sorteio: aí a
    /// resposta vem só da capacidade e da configuração das quadras.
    /// </param>
    public static List<string> Analisar(Torneio torneio, IReadOnlyCollection<Quadra> quadras,
        SedesDoTorneio sedes, int totalDeJogos, DateTime? ultimoJogo)
    {
        var motivos = new List<string>();

        // ⚠️ SEM `DataFim` NÃO HÁ PRAZO. Quem não marcou o dia de devolver a quadra não pode passar
        // dele, e inventar um limite ("deve ser 2 dias") faria a tela acusar torneio saudável.
        if (torneio.DataFim is not DateTime prazo) return motivos;
        if (torneio.DataInicio is not DateTime comeco) return motivos;

        var fimDoPrazo = prazo.Date;
        var duracao = VagasDaGrade.Duracao(torneio);

        // ── 1. Quadra cuja janela cai FORA das datas do torneio ─────────────────────────
        //
        // O `datetime-local` da tabela de quadras é fácil de errar em um dígito, e a quadra
        // "disponível 15/09" num torneio que acaba 13/09 é vaga que a grade conta e o organizador
        // não tem. Pior: é ela que faz a grade escorregar PARA o dia 15 — ver causa 2.
        foreach (var quadra in quadras.OrderBy(q => q.Nome, StringComparer.OrdinalIgnoreCase))
        {
            if (quadra.DisponivelDe is DateTime de && de.Date > fimDoPrazo)
            {
                motivos.Add($"A quadra \"{quadra.Nome}\" só fica disponível a partir de "
                    + $"{de:dd/MM 'às' HH:mm}, depois do fim do torneio ({fimDoPrazo:dd/MM}).");
            }
            else if (quadra.DisponivelAte is DateTime ate && ate.Date < comeco.Date)
            {
                motivos.Add($"A quadra \"{quadra.Nome}\" deixa de estar disponível em "
                    + $"{ate:dd/MM 'às' HH:mm}, antes de o torneio começar ({comeco:dd/MM}).");
            }
        }

        // ── 2. Dia do torneio sem NENHUMA quadra aberta ─────────────────────────────────
        //
        // 🕳️ É ASSIM QUE 12/09 VIRA 15/09 SEM PASSAR POR 13. O encaixe pula a vaga em que nenhuma
        // quadra do clube certo está aberta (GradeDeJogos.Encaixar, `TemOndeJogar`) e tenta o
        // horário seguinte — dia após dia, calado, até achar um com quadra. Um domingo sem quadra
        // aberta não dá erro nenhum: ele só empurra o torneio pra frente.
        for (var dia = comeco.Date; dia <= fimDoPrazo; dia = dia.AddDays(1))
        {
            var abertura = dia == comeco.Date ? torneio.HoraInicioDoDia : torneio.HoraInicioDiasSeguintes;

            bool temQuadra = false;
            for (var hora = dia.Add(abertura); hora.TimeOfDay <= torneio.HoraFimDoDia && hora.Date == dia;
                 hora = hora.AddMinutes(duracao))
            {
                if ((sedes.QuadrasAbertasEm(hora) ?? int.MaxValue) > 0) { temQuadra = true; break; }
            }

            if (!temQuadra)
            {
                motivos.Add($"Em {dia:dd/MM} ({DiaDaSemana(dia)}) não há nenhuma quadra aberta — "
                    + "a grade pula esse dia inteiro e joga os jogos pra frente.");
            }
        }

        // ── 3. VOLUME: mais jogos do que vagas ──────────────────────────────────────────
        //
        // Os DOIS números, sempre. "Não cabe" não diz o tamanho do buraco, e é o tamanho que decide
        // se a saída é mais uma quadra, jogo mais curto ou começar mais cedo.
        int vagas = VagasAte(torneio, sedes, fimDoPrazo, duracao);

        if (totalDeJogos > vagas)
        {
            motivos.Add($"São {totalDeJogos} jogos e o expediente até {fimDoPrazo:dd/MM} rende "
                + $"{vagas} vagas de quadra. Faltam {totalDeJogos - vagas} — dá pra resolver com "
                + "mais quadras, jogo mais curto ou começando mais cedo.");
        }

        // Nenhuma das três explicou, e mesmo assim tem jogo marcado depois do prazo: é o encaixe
        // cedendo a alguma restrição de horário (impedimento, concentração, sábado à noite). Dizer
        // "não sei" é melhor que ficar calado — o "Conferir grade" nomeia qual restrição foi.
        if (motivos.Count == 0 && ultimoJogo is DateTime ultimo && ultimo.Date > fimDoPrazo)
        {
            motivos.Add($"O último jogo ficou em {ultimo:dd/MM 'às' HH:mm}, depois de "
                + $"{fimDoPrazo:dd/MM}, e as quadras comportam o torneio. Sobrou uma restrição de "
                + "horário empurrando jogo — o \"Conferir grade\" diz qual.");
        }

        return motivos;
    }

    // Quantas vagas de quadra o expediente rende do começo do torneio até o fim do prazo.
    //
    // ⚠️ CONTA SÓ AS ÚTEIS: uma quadra fechada naquele horário não é vaga, e contá-la faria a
    // mensagem prometer capacidade que não existe — que é o defeito que ela veio denunciar.
    private static int VagasAte(Torneio torneio, SedesDoTorneio sedes, DateTime fimDoPrazo, int duracao)
    {
        if (torneio.DataInicio is not DateTime comeco) return 0;

        int quadras = Math.Max(torneio.QuantidadeQuadras, 1);
        int vagas = 0;

        for (var dia = comeco.Date; dia <= fimDoPrazo; dia = dia.AddDays(1))
        {
            var abertura = dia == comeco.Date ? torneio.HoraInicioDoDia : torneio.HoraInicioDiasSeguintes;

            for (var hora = dia.Add(abertura); hora.TimeOfDay <= torneio.HoraFimDoDia && hora.Date == dia;
                 hora = hora.AddMinutes(duracao))
            {
                vagas += Math.Min(sedes.QuadrasAbertasEm(hora) ?? quadras, quadras);
            }
        }

        return vagas;
    }

    private static string DiaDaSemana(DateTime dia) => dia.DayOfWeek switch
    {
        DayOfWeek.Monday => "segunda",
        DayOfWeek.Tuesday => "terça",
        DayOfWeek.Wednesday => "quarta",
        DayOfWeek.Thursday => "quinta",
        DayOfWeek.Friday => "sexta",
        DayOfWeek.Saturday => "sábado",
        _ => "domingo",
    };
}
