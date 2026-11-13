namespace Padelizou.Services;

// O FUSO DO PROCESSO TEM QUE SER O DE SÃO PAULO (13/11/2026).
//
// O app grava e compara hora LOCAL o tempo todo (colunas `timestamp without time zone`, modo legado
// do Npgsql — CLAUDE.md): `DateTime.Now` é o relógio da quadra. Isso depende de o processo rodar
// em America/Sao_Paulo, e quem garante é um drop-in do systemd (`Environment=TZ=...`) que só existe
// no servidor. Em agosto/2026 o VPS rodava em UTC e todo jogo aparecia "atrasado" desde o primeiro
// minuto, sem ninguém ver (STATUS.md). Um VPS reprovisionado nasce em UTC de novo.
//
// O `/healthz` usa isto: fora de São Paulo ele responde 503, e o rollback automático do
// `deploy.sh` segura a versão em vez de deixar a hora errada em produção calada.
//
// ⚠️ PELO FUSO, NÃO PELO DESLOCAMENTO: Buenos Aires também está em −3 hoje, mas é outro relógio
// (e outra regra se o Brasil voltar a ter horário de verão). "Brazil/East" é o apelido antigo do
// mesmo fuso; no Windows (máquina do Felipe) o nome é "E. South America Standard Time".
public static class FusoDoServidor
{
    public const string Esperado = "America/Sao_Paulo";

    public static bool EhSaoPaulo(TimeZoneInfo fuso)
    {
        if (fuso.Id is Esperado or "Brazil/East") return true;

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(fuso.Id, "BR", out var iana) && iana == Esperado;
    }
}
