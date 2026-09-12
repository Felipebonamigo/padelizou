using SkiaSharp;

namespace Padelizou.Services;

// O que o card do placar precisa saber. Os NOMES chegam prontos: quem os monta é
// `AvisoDePlacarAoVivo.NomeDaDupla`, a mesma régua que escreve o título da notificação — o
// desenho não pode chamar a dupla de um jeito enquanto a linha logo acima a chama de outro.
public record PlacarParaCard(
    string Dupla1, string Dupla2, int Games1, int Games2, bool Encerrado, string? Contexto);

// O PNG QUE APARECE DENTRO DA NOTIFICAÇÃO DO PLACAR AO VIVO (12/09/2026).
//
// 🗣️ Felipe, com a notificação já chegando e dois prints na mão — a bolha de placar do app do
// Google na tela inicial e o placar da Copa na Dynamic Island: *"as notificações estao
// acontecendo, mas eu queria algo tipo esses prints, tem como ?"*.
//
// ⚠️ A RESPOSTA HONESTA PROS DOIS PRINTS É NÃO, e é a mesma de 16/08: a bolha é desenhada pelo
// APP DO GOOGLE com dado do Google (nem app nativo de terceiro cria aquilo), e a Dynamic Island
// é Live Activity/ActivityKit, que exige app nativo iOS. O que a notificação da WEB tem é a
// `image` do `showNotification`: no Android, puxando a notificação pra baixo, ela abre um PNG.
// Este é esse PNG — o mais perto que dá sem um segundo código pra manter (ver ANDROID.md).
//
// ⚠️ DEITADO (1080×540), não retrato: o Android recorta a `image` em ~2:1. O formato dos cards
// de story daqui (1080×1350) chegaria cortado pelo meio, e o que sumiria no corte é a linha de
// baixo — o placar da segunda dupla.
//
// ⚠️ SEM EMOJI e SEM LOGO LIDO DO DISCO, como todo card daqui: a Poppins não tem glifo de emoji
// (ver FonteDoCartao), e ler arquivo pra uma imagem que o celular busca a cada game gastaria
// disco por um enfeite que, em 1080×540, não teria nem espaço.
public static class CartaoDoPlacarAoVivo
{
    // A metade da largura da marca — o 2:1 que o Android recorta.
    public const int Altura = CartaoCompartilhavel.Largura / 2;

    private const float MargemEsquerda = 90;
    private const float ColunaDoPlacarX = 940;
    private const float LarguraDoNome = 660;

    private const float LinhaDaDupla1Y = 268;
    private const float LinhaDaDupla2Y = 420;
    private const float DivisoriaY = 320;
    private const float ContextoY = 500;

    public static byte[] Desenhar(PlacarParaCard placar, FonteDoCartao fontes) =>
        CartaoCompartilhavel.EmPng(canvas =>
        {
            CartaoCompartilhavel.Fundo(canvas, Altura);
            CartaoCompartilhavel.FaixaDoTopo(canvas);

            // AO VIVO em lime, ENCERRADO em cinza: a mesma diferença que o cartão da lista faz
            // com o ponto vermelho. É o estado, não o placar, que muda de cor aqui.
            CartaoCompartilhavel.Pilula(
                canvas, placar.Encerrado ? "ENCERRADO" : "AO VIVO", 96, fontes, 32,
                fundo: placar.Encerrado ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Lime);

            Linha(canvas, fontes, placar.Dupla1, placar.Games1, LinhaDaDupla1Y,
                naFrente: placar.Games1 > placar.Games2);
            Linha(canvas, fontes, placar.Dupla2, placar.Games2, LinhaDaDupla2Y,
                naFrente: placar.Games2 > placar.Games1);

            using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(60), IsAntialias = true })
                canvas.DrawRect(new SKRect(MargemEsquerda, DivisoriaY, CartaoCompartilhavel.Largura - MargemEsquerda, DivisoriaY + 2), tinta);

            if (!string.IsNullOrWhiteSpace(placar.Contexto))
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, placar.Contexto, ContextoY, fontes.Media, 34,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemEsquerda * 2);
        }, Altura);

    // Nome à ESQUERDA e número em coluna fixa à direita: centralizar as duas linhas faria a
    // coluna do placar dançar conforme o nome é curto ou comprido — e é ela que a pessoa lê
    // primeiro (mesma razão do CartaoDosJogos, onde a hora tem coluna própria).
    //
    // Quem está na frente sai em lime e no peso forte. É COR E PESO, não símbolo: sem fonte de
    // fallback, uma seta ou um troféu sairiam como espaço em branco.
    private static void Linha(SKCanvas canvas, FonteDoCartao fontes, string nome, int games,
        float y, bool naFrente)
    {
        var cor = naFrente ? CartaoCompartilhavel.Lime : CartaoCompartilhavel.Branco;
        var familia = naFrente ? fontes.Forte : fontes.Media;

        CartaoCompartilhavel.TextoAEsquerda(canvas, nome, MargemEsquerda, y, familia, 56, cor, LarguraDoNome, 28);
        CartaoCompartilhavel.Texto(canvas, games.ToString(), ColunaDoPlacarX, y, fontes.Forte, 96, cor, 200, 48);
    }
}
