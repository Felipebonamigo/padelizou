namespace Padelizou.Services;

// Credenciais da API do Inter, usadas SÓ pra LER o extrato Pix da conta — não emitimos
// cobrança dinâmica por aqui (isso tem tarifa de QR dinâmico até pro MEI; ver
// Services/ConciliacaoAutomaticaDoPix). A cobrança continua sendo o nosso QR estático
// de sempre (Services/PixCopiaECola); o Inter só entra pra dizer "isto aqui caiu".
//
// Nasce DESLIGADO: sem client_id nenhum, o ConciliacaoAutomaticaDoPixBackgroundService nem
// liga o timer, e a conferência manual em /Admin/PixDireto continua sendo o caminho —
// mesma ideia do Bar e dos Desafios (ver Services/BarSettings).
public class InterPixSettings
{
    public bool Habilitado { get; set; } = false;

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Caminho, no SERVIDOR, do certificado que o próprio Inter gera no onboarding
    // (Internet Banking PJ → Soluções para sua empresa → Nova Integração). PEM: .crt e .key
    // separados. ⚠️ Nunca no repo nem no publish local — vai por variável de ambiente do
    // systemd, como o resto de segredo (ver reference_padelizou_deploy_manual).
    public string CertPath { get; set; } = "";
    public string KeyPath { get; set; } = "";

    // De quanto em quanto tempo varre o extrato de novo.
    public int IntervaloDeVarreduraMinutos { get; set; } = 10;

    // Na primeira varredura (sem marcador salvo ainda) o quanto olha pra trás. Cobre o Pix
    // que já tenha caído entre a conta ser aberta e o serviço ligar.
    public int JanelaInicialHoras { get; set; } = 48;
}
