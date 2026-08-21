namespace Padelizou.Services;

// O QUE O AVISO DE 24H DIZ — texto em classe pura, no molde de AvisoDeInscricaoNoTorneio.
//
// ⚠️ ELE PAROU DE PERGUNTAR EM 21/08/2026. O texto era "Confirma presença no jogo fixo dia X" —
// e desde a presença presumida isso ficou errado por dentro: pedir confirmação de quem JÁ está
// na lista ensina que ainda falta um ato, que é o contrário do que o sistema passou a fazer.
// Calar também não serve: as 24h antes são o único momento em que a pessoa lembra de avisar que
// não vai, e é desse número que sai a reserva da quadra.
//
// ⚠️ O NOME DO GRUPO ENTRA NO CORPO, e antes não entrava — o texto citava só o clube. Quem joga
// em duas panelinhas no mesmo clube saía do jogo errado.
//
// ⚠️ SEM "EM 24H" NO TÍTULO. A janela é `>= DataHora - 24h` com tick de 15 minutos: quem ganha
// linha de confirmação dentro da janela (entrou na panelinha na segunda à noite) recebe o aviso
// faltando 2h. O título antigo já mentia.
public static class AvisoDoJogoDaSemana
{
    public const string TituloParaQuemEstaNaLista = "Jogo da semana: você está na lista";
    public const string TituloParaQuemConfirmou = "Jogo amanhã";

    public static string Titulo(bool confirmou) =>
        confirmou ? TituloParaQuemConfirmou : TituloParaQuemEstaNaLista;

    public static string Corpo(bool confirmou, string grupo, DateTime quando, string? clube)
    {
        var onde = string.IsNullOrWhiteSpace(clube) ? "" : $" em {clube}";
        var cabecalho = $"{grupo} · {quando:dd/MM 'às' HH:mm}{onde}.";

        return confirmou
            ? $"{cabecalho} Você confirmou."
            // A frase da saída vem junto e diz POR QUE avisar importa — sem isso, "você está na
            // lista" é só informação, e quem não vai continua não avisando.
            : $"{cabecalho} Se não puder ir, avise agora — é por esse número que a quadra é reservada.";
    }
}
