namespace Padelizou.Services;

// Em que pé está o canal de WhatsApp. Existe porque "não está enviando" tem causas com
// consertos diferentes: uma se resolve sozinha com um restart, outra exige o Felipe com o
// celular na mão lendo um QR, e outra não é problema nenhum (é o dev, desligado de propósito).
public enum EstadoDoCanal
{
    // Nem configurado — localhost e dev. Não é falha, é o desenho.
    Desligado,

    // Chip pareado e pronto pra enviar.
    Conectado,

    // A API respondeu, mas o chip não está pareado. É aqui que o restart costuma resolver.
    Desconectado,

    // A API nem respondeu: container caído, porta mudada, chave errada. O restart da instância
    // não adianta — o problema é uma camada abaixo.
    SemResposta,
}

public static class DiagnosticoDoCanal
{
    // Frase pro painel e pro e-mail. Curta o suficiente pra caber num selo.
    public static string Frase(EstadoDoCanal estado) => estado switch
    {
        EstadoDoCanal.Desligado => "desligado neste ambiente (é assim no dev e no localhost)",
        EstadoDoCanal.Conectado => "conectado e enviando",
        EstadoDoCanal.Desconectado => "o chip não está pareado — os avisos não estão saindo",
        EstadoDoCanal.SemResposta => "o serviço de mensagem não respondeu — os avisos não estão saindo",
        _ => "estado desconhecido",
    };

    // Vale acender o alarme? "Desligado" nunca acende: no dev o canal está fora de propósito,
    // e um alarme que está sempre aceso é um alarme que ninguém mais olha.
    public static bool EhProblema(EstadoDoCanal estado) =>
        estado is EstadoDoCanal.Desconectado or EstadoDoCanal.SemResposta;

    // Um restart da instância tem chance de resolver? Só quando a API está de pé pra receber o
    // pedido — com ela muda, o restart nem chega a ser tentado.
    public static bool AdiantaReligar(EstadoDoCanal estado) => estado == EstadoDoCanal.Desconectado;
}
