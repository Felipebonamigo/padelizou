namespace Padelizou.Services;

// O PNG do QR, desenhado aqui a partir do copia e cola.
//
// O gateway até devolve uma imagem pronta junto com o código, mas quem desenha somos nós pelo
// mesmo motivo do Pix direto: o QR e o copia e cola precisam ser o MESMO código, e a única
// forma de garantir isso é ter uma fonte só. De quebra, a tela não fica sem QR no dia em que
// a imagem vier vazia — e o visual é idêntico nas duas faturas.
public static class QrDoPix
{
    // Base64 pronto pra virar `src="data:image/png;base64,..."`. A string é da ordem de poucos
    // KB — cabe no HTML sem doer, e evita uma segunda ida ao gateway só pra buscar o desenho.
    public static string Base64(string copiaECola)
    {
        using var gerador = new QRCoder.QRCodeGenerator();
        // ECC M é o padrão dos QR de Pix: aguenta tela riscada sem inflar o código.
        using var qr = gerador.CreateQrCode(copiaECola, QRCoder.QRCodeGenerator.ECCLevel.M);
        return Convert.ToBase64String(new QRCoder.PngByteQRCode(qr).GetGraphic(pixelsPerModule: 10));
    }
}
