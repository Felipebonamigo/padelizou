using Padelizou.Services;

namespace Padelizou.Tests;

// Um backup que para sem avisar é pior que não ter backup: você conta com ele. O script sai
// quieto de propósito (erro diário no log vira ruído que ninguém lê), então quem grita é isto.
public class VigiaDoBackupTests
{
    private static readonly DateTime Agora = new(2026, 7, 28, 22, 0, 0);

    [Fact]
    public void Copia_de_hoje_nao_gera_aviso()
    {
        Assert.False(VigiaDoBackup.PrecisaAvisar(Agora.AddHours(-6), Agora));
    }

    [Fact]
    public void Uma_noite_ruim_nao_vira_alarme_falso()
    {
        // O backup roda todo dia às 4h30. Rede caindo uma noite, ou o servidor reiniciando na
        // hora errada, não pode disparar e-mail — alarme falso ensina a ignorar alarme.
        Assert.False(VigiaDoBackup.PrecisaAvisar(Agora.AddDays(-1), Agora));
    }

    [Fact]
    public void Passou_do_limite_avisa()
    {
        Assert.True(VigiaDoBackup.PrecisaAvisar(Agora.AddDays(-3), Agora));
        Assert.True(VigiaDoBackup.PrecisaAvisar(Agora.AddDays(-30), Agora));
    }

    [Fact]
    public void Nunca_ter_feito_backup_e_o_PIOR_caso_e_nao_o_mais_inofensivo()
    {
        // Servidor onde o backup nunca funcionou parece igualzinho a um onde ele funciona todo
        // dia — em ambos não há erro nenhum na tela. Tratar "nunca" como "tudo bem" seria
        // justamente o silêncio que estamos combatendo.
        Assert.True(VigiaDoBackup.PrecisaAvisar(null, Agora));
    }

    [Fact]
    public void Arquivo_ausente_ilegivel_ou_com_lixo_conta_como_nunca()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "pdz-vigia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            Assert.Null(VigiaDoBackup.LerUltimoSucesso(Path.Combine(pasta, "nao-existe.txt")));

            var vazio = Path.Combine(pasta, "vazio.txt");
            File.WriteAllText(vazio, "");
            Assert.Null(VigiaDoBackup.LerUltimoSucesso(vazio));

            var lixo = Path.Combine(pasta, "lixo.txt");
            File.WriteAllText(lixo, "isto não é uma data");
            Assert.Null(VigiaDoBackup.LerUltimoSucesso(lixo));

            // Na dúvida, avisa: um alarme falso custa um e-mail, o silêncio custa o backup.
            Assert.True(VigiaDoBackup.PrecisaAvisar(VigiaDoBackup.LerUltimoSucesso(lixo), Agora));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public void Le_a_data_que_o_script_grava()
    {
        // Formato combinado com o backup-drive.sh: `date --iso-8601=seconds`.
        var pasta = Path.Combine(Path.GetTempPath(), "pdz-vigia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arquivo = Path.Combine(pasta, "ultimo.txt");
            const string gravadoPeloScript = "2026-07-28T21:24:31-03:00";
            File.WriteAllText(arquivo, gravadoPeloScript + "\n");

            var lido = VigiaDoBackup.LerUltimoSucesso(arquivo);

            // Comparado com o MESMO instante convertido pro fuso da máquina, e não com uma data
            // escrita à mão: o servidor roda em UTC e a máquina de desenvolvimento em UTC-3, então
            // "o dia 28" é dia diferente em cada uma. Um teste que passa aqui e falha no CI não
            // encontrou bug nenhum — só o fuso de quem o escreveu.
            Assert.NotNull(lido);
            Assert.Equal(DateTimeOffset.Parse(gravadoPeloScript).LocalDateTime, lido!.Value);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "hoje")]
    [InlineData(1, "ontem")]
    [InlineData(5, "5 dias")]
    public void O_email_diz_ha_quanto_tempo_sem_obrigar_a_fazer_conta(int diasAtras, string trecho)
    {
        var texto = VigiaDoBackup.DescreverAtraso(Agora.AddDays(-diasAtras), Agora);
        Assert.Contains(trecho, texto);
    }

    [Fact]
    public void Sem_nenhuma_copia_o_email_diz_isso_com_todas_as_letras()
    {
        Assert.Contains("nenhuma cópia", VigiaDoBackup.DescreverAtraso(null, Agora));
    }
}
