namespace Padelizou.Services;

// "SUA AULA É AMANHÃ" e "SUA AULA É DAQUI A POUCO" — a régua do lembrete de aula.
//
// 🗣️ Maickel, 16/09/2026: *"Só talvez faria um 'push' — avisando 24hs e 1hora antes da aula —
// para as opções de aula de padel"*.
//
// ⚠️ ERA A ÚNICA COISA COM HORA MARCADA QUE NÃO AVISAVA NINGUÉM ANTES. O torneio tem o lembrete
// de inscrição não paga, a panelinha tem o de 24h do jogo fixo, a cobrança tem o de 6h — a aula,
// que é o compromisso mais pessoal do sistema, marcava e calava até o dia.
//
// A ligação com o banco mora no LembreteDaAulaBackgroundService; aqui só a conta, sem I/O.
public static class LembreteDaAula
{
    // A VÉSPERA. Vale um lembrete porque é o último instante em que desmarcar ainda é de graça:
    // a política de cancelamento da maioria dos professores é justamente 24h (ver PoliticaAula).
    public const int MarcoDaVespera = 24;

    // A ÚLTIMA HORA. Aqui não há o que decidir — é "sai de casa".
    public const int MarcoDaUltimaHora = 1;

    // Do mais distante pro mais perto. ⚠️ A varredura lê `Marcos.Max()` pra limitar a consulta
    // no banco: marco novo aqui estica a janela sozinho, sem ninguém lembrar de mudar o Where.
    public static readonly int[] Marcos = { MarcoDaVespera, MarcoDaUltimaHora };

    // Ninguém é acordado às 3h da manhã pra saber de uma aula que é à noite.
    public const int PrimeiraHora = 7;
    public const int UltimaHora = 22;

    public static bool HoraCivilizada(DateTime agora) =>
        agora.Hour >= PrimeiraHora && agora.Hour < UltimaHora;

    // Como o assunto se chama no título. Duas constantes em vez de um `bool ehJogoAula` porque
    // quem lê a chamada entende "Sua aula é amanhã" sem ir ver o que `true` queria dizer.
    public const string UmaAula = "Sua aula";
    public const string UmJogoAula = "Seu jogo-aula";

    // Qual marco cabe AGORA — nulo quando não há o que avisar.
    //
    // ⚠️ QUANDO OS DOIS VENCEM DE UMA VEZ (aula marcada faltando 40 minutos), vale o MAIS
    // URGENTE e o outro é dado por cumprido: gravar `1` retira o `24` de circulação, porque a
    // regra é "só marco menor que o último enviado". Sem isso a pessoa levaria dois avisos em
    // quinze minutos, um por tick. É a mesma decisão de LembreteDeInscricaoNaoPaga.MarcoDevido.
    public static int? MarcoDevido(DateTime quando, DateTime agora, int? ultimoMarcoEnviado)
    {
        // A aula já começou (ou passou): avisar que ela "é daqui a pouco" com o aluno na quadra
        // é pior que calar.
        if (quando <= agora) return null;

        var faltamHoras = (quando - agora).TotalHours;

        var devidos = Marcos
            .Where(m => faltamHoras <= m && (ultimoMarcoEnviado == null || m < ultimoMarcoEnviado))
            .ToList();

        if (devidos.Count == 0) return null;

        var marco = devidos.Min();

        // ⚠️ A JANELA VALE SÓ PRA VÉSPERA. Segurar o de 24h até as 7h ainda entrega o aviso com
        // mais de 15 horas de sobra; segurar o de 1h é a mesma coisa que não mandá-lo — quem tem
        // aula às 6h já vai acordar às 5h de qualquer jeito.
        if (marco != MarcoDaUltimaHora && !HoraCivilizada(agora)) return null;

        return marco;
    }

    // ⚠️ O TEXTO CONTA O TEMPO DE VERDADE, NUNCA O NÚMERO DO MARCO. Quem entra pelo marco de 24h
    // com a aula em 15 horas — porque o marco caiu de madrugada e foi segurado até as 7h — não
    // pode ouvir "amanhã": a aula é HOJE. Mesma lição do lembrete de inscrição não paga.
    public static string Quando(DateTime quando, DateTime agora) =>
        (quando.Date - agora.Date).Days switch
        {
            0 => $"hoje às {quando:HH:mm}",
            1 => $"amanhã às {quando:HH:mm}",
            _ => $"{quando:dd/MM} às {quando:HH:mm}",
        };

    public static string Titulo(string oQue, DateTime quando, DateTime agora) =>
        $"{oQue} é {Proximidade(quando, agora)}";

    private static string Proximidade(DateTime quando, DateTime agora)
    {
        // "Daqui a pouco" é o que a pessoa pensa quando falta menos de uma hora e meia — e a
        // margem existe porque o varredor passa de 15 em 15 minutos: o marco de 1h pode sair
        // faltando 59 minutos ou faltando 46.
        if ((quando - agora).TotalMinutes <= 90) return "daqui a pouco";

        return (quando.Date - agora.Date).Days switch
        {
            0 => "hoje",
            1 => "amanhã",
            _ => $"em {quando:dd/MM}",
        };
    }

    // ⚠️ `ofereceDesmarcar` só no aviso de VÉSPERA e só pro ALUNO. No de 1h não há mais o que
    // decidir, e oferecer ali faria o aluno achar que ainda dá tempo de desmarcar sem custo.
    public static string Frase(string comQuem, DateTime quando, DateTime agora, string local, bool ofereceDesmarcar)
    {
        var frase = $"Com {comQuem}, {Quando(quando, agora)}, em {local}.";

        return ofereceDesmarcar ? frase + " Se não puder ir, desmarque pelo app." : frase;
    }
}
