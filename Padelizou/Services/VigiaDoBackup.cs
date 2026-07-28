namespace Padelizou.Services;

// Decide QUANDO avisar que o backup fora do servidor parou.
//
// O script de backup sai quieto quando não consegue subir (exit 0), de propósito: um erro por dia
// no log vira ruído que ninguém lê. O efeito colateral é que a falha fica invisível — e um backup
// que parou sem avisar é pior que não ter backup, porque você conta com ele.
//
// O risco não é teórico. O token do Google, com a tela de consentimento em modo Teste, expira em
// 7 dias; a credencial compartilhada do rclone será desligada durante 2026; e disco cheio, senha
// trocada ou pasta sem permissão derrubam a cópia a qualquer momento. Todos falham do mesmo jeito:
// em silêncio.
public static class VigiaDoBackup
{
    // Quantos dias sem cópia até virar problema. O backup roda diariamente às 4h30, então 2 dias
    // tolera uma noite ruim (rede caiu, servidor reiniciando) sem gerar alarme falso — e ainda
    // avisa antes que o buraco fique grande.
    public const int DiasTolerados = 2;

    public static bool PrecisaAvisar(DateTime? ultimoSucesso, DateTime agora)
    {
        // Nunca houve cópia: é o pior caso, não o mais inofensivo. Um servidor novo em que o
        // backup nunca funcionou parece igualzinho a um em que ele funciona todo dia.
        if (ultimoSucesso == null) return true;

        return agora - ultimoSucesso.Value > TimeSpan.FromDays(DiasTolerados);
    }

    // Quanto tempo faz, em português, pro e-mail não obrigar ninguém a fazer conta de cabeça.
    public static string DescreverAtraso(DateTime? ultimoSucesso, DateTime agora)
    {
        if (ultimoSucesso == null) return "nenhuma cópia bem-sucedida foi registrada até hoje";

        var dias = (int)(agora - ultimoSucesso.Value).TotalDays;
        return dias switch
        {
            <= 0 => "a última cópia foi hoje",
            1 => "a última cópia foi ontem",
            _ => $"a última cópia foi há {dias} dias"
        };
    }

    // Lê o arquivo que o backup-drive.sh grava a cada cópia concluída. Formato: uma linha com a
    // data em ISO 8601. Arquivo ausente, vazio ou ilegível conta como "nunca houve cópia" — na
    // dúvida, avisa. Um alarme falso custa um e-mail; o silêncio custa o backup.
    public static DateTime? LerUltimoSucesso(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return null;

            var texto = File.ReadAllText(caminho).Trim();
            return DateTime.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var quando)
                ? quando
                : null;
        }
        catch
        {
            return null;
        }
    }
}
