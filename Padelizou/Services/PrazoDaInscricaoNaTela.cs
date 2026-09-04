using Padelizou.Models;

namespace Padelizou.Services;

// "Até quando dá pra se inscrever", na frase que a tela escreve.
//
// 🗣️ Pedido do Felipe, 04/09/2026, olhando a lista de torneios: "acho que é bom colocar até
// quando vai as inscrições de cada torneio também".
//
// O campo `PrevisaoEncerramentoInscricoes` já existia e já vinha preenchido — ele só não
// aparecia na LISTA, que é justamente onde a pessoa decide se clica. A frase mora aqui, e não
// nas views, pelo mesmo motivo que fez `DataDoTorneioNaTela` nascer: são duas telas mostrando
// a mesma coisa, e duas cópias divergem na primeira mudança.
public static class PrazoDaInscricaoNaTela
{
    // A partir de quantos dias a data seca informa mais que a contagem. "Faltam 61 dias" não é
    // urgência, é ruído; "08/11" a pessoa encaixa no próprio calendário.
    private const int DiasParaContar = 7;

    // Null = não há o que dizer, e a tela simplesmente não mostra a linha.
    public static string? Frase(Torneio torneio, DateTime hoje)
    {
        if (torneio.PrevisaoEncerramentoInscricoes is not DateTime prazo) return null;

        // Prazo só interessa a quem ainda pode entrar. Num torneio que já começou a frase é
        // informação morta ocupando a linha, e num finalizado chega a confundir.
        if (torneio.Status != "Inscrições Abertas") return null;

        var dias = (prazo.Date - hoje.Date).Days;

        // ⚠️ PRAZO VENCIDO CALA, e este é o ramo que não se adivinha lendo o campo: a data é
        // PROMESSA, não gatilho — quem encerra as inscrições é o organizador, no botão (ver
        // Models/Torneio). Passado o dia sem ele fechar, a inscrição continua ABERTA; escrever
        // "inscrições até 08/09" no dia 09 desanimaria quem ainda pode entrar, e contradiria o
        // botão logo abaixo, que segue dizendo "Inscrever-se".
        if (dias < 0) return null;

        return dias switch
        {
            0 => "Inscrições encerram hoje",
            1 => "Inscrições encerram amanhã",
            <= DiasParaContar => $"Inscrições encerram em {dias} dias",
            _ => $"Inscrições até {prazo:dd/MM}",
        };
    }
}
