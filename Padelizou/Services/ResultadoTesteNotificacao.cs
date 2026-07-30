namespace Padelizou.Services;

// Por que o aviso chegou (ou não) em cada canal. Existe porque "não chegou" tem meia dúzia de
// causas MUITO diferentes — jogador sem o app, número em branco, preferência desmarcada, canal
// desligado no ambiente, provedor fora do ar — e uma tela que só diz "enviado" faz o admin
// caçar no escuro qual delas foi.
public enum ResultadoDoCanal
{
    NaoPedido,
    Enviado,
    SemAppInstalado,
    SemNumeroNoCadastro,
    PreferenciaDesmarcada,
    CanalDesligadoNesteAmbiente,
    FalhouNoEnvio,
}

public record ResultadoTesteNotificacao
{
    public ResultadoDoCanal Push { get; init; } = ResultadoDoCanal.NaoPedido;

    // Quantos aparelhos receberam. Uma pessoa pode ter o app no celular e no computador.
    public int Aparelhos { get; init; }

    public ResultadoDoCanal WhatsApp { get; init; } = ResultadoDoCanal.NaoPedido;

    // Pra onde foi, já formatado. O admin confere se é o número que ele esperava — errar de
    // pessoa num teste é fácil quando se digita o login na mão.
    public string? Numero { get; init; }
}

// A frase que o painel mostra. Fica aqui, pura, em vez de virar um `switch` dentro da view:
// é texto que precisa estar certo, e texto certo se testa.
public static class TextoDoTeste
{
    public static string Frase(ResultadoDoCanal resultado, int aparelhos = 0, string? numero = null) => resultado switch
    {
        ResultadoDoCanal.NaoPedido => "não foi pedido neste teste.",
        ResultadoDoCanal.Enviado when numero != null => $"enviado para {numero}.",
        ResultadoDoCanal.Enviado => aparelhos == 1
            ? "enviado para 1 aparelho."
            : $"enviado para {aparelhos} aparelhos.",
        ResultadoDoCanal.SemAppInstalado =>
            "esse jogador não tem o app instalado (ou não aceitou notificações) em nenhum aparelho.",
        ResultadoDoCanal.SemNumeroNoCadastro =>
            "esse jogador não tem celular válido no cadastro.",
        ResultadoDoCanal.PreferenciaDesmarcada =>
            "esse jogador desmarcou o WhatsApp nas preferências dele.",
        ResultadoDoCanal.CanalDesligadoNesteAmbiente =>
            "o canal está desligado neste ambiente — é assim no dev e no localhost, de propósito.",
        ResultadoDoCanal.FalhouNoEnvio =>
            "o provedor recusou o envio. Veja o log do servidor e se a instância está conectada.",
        _ => "resultado desconhecido.",
    };

    public static bool DeuCerto(ResultadoDoCanal resultado) => resultado == ResultadoDoCanal.Enviado;
}
