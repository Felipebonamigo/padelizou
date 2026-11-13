using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 13/11/2026 — TRÊS BURACOS DE OPERAÇÃO ACHADOS NA ANÁLISE DE HOSPEDAGEM (PROXIMOS-PASSOS.md, item 2).
//
// 🗣️ Felipe: *"faça uma análise do nosso sistema e hospedagem, veja se está tudo certinho"* — e
// depois *"pode começar pelo item 1, 2, 3, 4"*.
//
// 1. O CARIMBO DO BACKUP MENTIA. O `backup-offsite.sh` grava o carimbo que o VigiaDoBackup lê
//    ("a última cópia fora do servidor deu certo") depois do `rclone copy` — e o `copy` passa
//    LISO quando não há dump novo pra copiar. Se o `pg_dump` das 4h parar (disco, senha, versão),
//    o vigia fica calado pra sempre e o `rclone delete --min-age 30d` esvazia o histórico do B2
//    em 30 dias. Agora: sem dump DO DIA, inteiro e COMPLETO, nada sobe, nada é podado e o
//    carimbo não é gravado — o vigia avisa em 2 dias.
//
// 2. O DEPLOY APLICAVA MIGRATION SEM CÓPIA DO BANCO. O app roda `Migrate()` no startup e o
//    rollback do `deploy.sh` volta o BINÁRIO, nunca o banco: 26 das 168 migrations apagam coluna,
//    tabela ou fazem UPDATE/DELETE. Desfazer uma delas custaria até 12h de dados (o intervalo
//    entre backups). Agora o `deploy.sh` copia o banco antes de reiniciar — sem cópia, sem deploy.
//
// 3. NADA CONFERIA O FUSO. O app grava e compara hora LOCAL (CLAUDE.md) e depende do processo
//    em America/Sao_Paulo — um drop-in do systemd que só existe no servidor. Agora o `/healthz`
//    responde 503 fora de São Paulo, e o rollback automático do `deploy.sh` segura a versão.
//
// ⚠️ Os dois scripts MORAM NO VPS: o merge não os instala (infra/vps/README.md). Estes testes
// travam o que o repositório guarda; a instalação é passo do Felipe.
public class BackupEDeployGuardamOBancoTests
{
    // ── 1. O dump do dia, conferido pela função DE VERDADE do script ───────────────────────────
    //
    // Roda a função `dump_do_dia_ok` recortada do `backup-offsite.sh` num bash de verdade, contra
    // arquivos de exemplo: o que ela aceita é o que libera o carimbo.

    private const string FimDoPgDump = "-- PostgreSQL database dump complete";

    [Fact]
    public void Dump_de_hoje_inteiro_e_completo_libera_a_rodada()
    {
        if (!BashDisponivel()) return; // Windows sem bash; no CI ele existe (ver No_CI_os_testes_de_script_rodam_de_verdade)
        using var pasta = new PastaTemporaria();
        Dump(pasta, "db_padel_20261113_040001.sql.gz", completo: true, idade: TimeSpan.FromMinutes(30));

        Assert.Equal(0, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void Dump_que_morreu_no_meio_nao_libera()
    {
        if (!BashDisponivel()) return;
        using var pasta = new PastaTemporaria();
        Dump(pasta, "db_padel_20261113_040001.sql.gz", completo: false, idade: TimeSpan.FromMinutes(30));

        Assert.Equal(1, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void Dump_antigo_nao_libera_nem_o_do_meio_dia_de_ontem()
    {
        // 4h30 UTC: o dump do meio-dia (16h UTC de ontem) tem 12h30. Aceitar ele esconderia um
        // cron das 4h morto — o mesmo motivo de o backup do meio-dia não gravar o carimbo.
        if (!BashDisponivel()) return;
        using var pasta = new PastaTemporaria();
        Dump(pasta, "db_padel_20261112_160001.sql.gz", completo: true, idade: TimeSpan.FromHours(12.5));

        Assert.Equal(1, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void Arquivo_corrompido_nao_libera()
    {
        if (!BashDisponivel()) return;
        using var pasta = new PastaTemporaria();
        var caminho = Path.Combine(pasta.Caminho, "db_padel_20261113_040001.sql.gz");
        File.WriteAllText(caminho, "isto não é gzip");
        File.SetLastWriteTimeUtc(caminho, DateTime.UtcNow.AddMinutes(-30));

        Assert.Equal(1, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void Pasta_sem_dump_nao_libera()
    {
        if (!BashDisponivel()) return;
        using var pasta = new PastaTemporaria();

        Assert.Equal(1, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void Vale_o_dump_MAIS_NOVO_um_bom_mais_velho_nao_esconde_o_de_hoje_quebrado()
    {
        if (!BashDisponivel()) return;
        using var pasta = new PastaTemporaria();
        Dump(pasta, "db_padel_20261113_020000.sql.gz", completo: true, idade: TimeSpan.FromHours(2));
        Dump(pasta, "db_padel_20261113_040001.sql.gz", completo: false, idade: TimeSpan.FromMinutes(30));

        Assert.Equal(1, ConferirDump(pasta.Caminho));
    }

    [Fact]
    public void No_CI_os_testes_de_script_rodam_de_verdade()
    {
        // Os testes acima voltam cedo sem bash (Windows). No CI isso não pode acontecer calado:
        // ali eles são a única rede da função que libera o carimbo do backup.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.True(BashDisponivel(), "O CI precisa de bash pra rodar os testes do backup-offsite.sh.");
    }

    // ── 1b. A ordem no script: nada sobe, nada é podado e nada é carimbado sem o dump ─────────

    [Fact]
    public void O_backup_de_fora_confere_o_dump_antes_de_copiar_podar_ou_carimbar()
    {
        var script = Script("backup-offsite.sh");
        int confere = script.IndexOf("dump_do_dia_ok \"$LOCAL\"", StringComparison.Ordinal);
        int copia = script.IndexOf("copiar_para \"$COFRE\"", StringComparison.Ordinal);
        int carimbo = script.IndexOf("> /var/lib/padelizou/ultimo-backup-drive", StringComparison.Ordinal);

        Assert.True(confere > 0, "o backup-offsite.sh não confere o dump do dia");
        Assert.True(copia > confere, "a cópia (e a poda do histórico, dentro dela) vem antes da conferência");
        Assert.True(carimbo > confere, "o carimbo vem antes da conferência");
    }

    // ── 2. O deploy copia o banco antes de reiniciar ─────────────────────────────────────────

    [Fact]
    public void O_deploy_copia_o_banco_antes_de_reiniciar_o_servico()
    {
        var script = Script("deploy.sh");
        int copia = script.IndexOf("pg_dump", StringComparison.Ordinal);
        int restart = script.IndexOf("systemctl restart \"$SERVICO\"", StringComparison.Ordinal);

        Assert.True(copia > 0, "o deploy.sh não copia o banco");
        Assert.True(restart > copia, "o restart (que roda a migration) vem antes da cópia do banco");
    }

    [Fact]
    public void A_copia_do_deploy_e_do_banco_certo_de_cada_ambiente()
    {
        var script = Script("deploy.sh");

        Assert.Matches(@"prod\)[^\n]*BANCO=""db_padel""", script);
        Assert.Matches(@"dev\)[^\n]*BANCO=""db_padel_dev""", script);
        Assert.Contains("pg_dump \"$BANCO\"", script);
    }

    [Fact]
    public void Sem_copia_completa_nao_tem_deploy()
    {
        var script = Script("deploy.sh");
        var trecho = Trecho(script, "pg_dump", "systemctl restart \"$SERVICO\"");

        // Confere que o dump terminou (a linha que o pg_dump só escreve no fim) e sai com erro
        // ANTES de trocar a versão quando não terminou.
        Assert.Contains(FimDoPgDump.Replace("-- ", ""), trecho);
        Assert.Contains("exit 1", trecho);
    }

    [Fact]
    public void A_copia_do_deploy_nao_se_mistura_com_o_backup_do_dia()
    {
        // Na pasta do backup, um arquivo "db_padel_*" do deploy viraria o "dump do dia" pro
        // backup de fora e esconderia um pg_dump das 4h quebrado.
        var trecho = Trecho(Script("deploy.sh"), "pg_dump", "systemctl restart \"$SERVICO\"");
        var pasta = Regex.Match(Script("deploy.sh"), @"COPIAS=""([^""]+)""");

        Assert.True(pasta.Success, "o deploy.sh não diz onde guarda a cópia (COPIAS=...)");
        Assert.DoesNotContain("/var/backups/padelizou\"", pasta.Value);
        Assert.False(pasta.Groups[1].Value.TrimEnd('/') == "/var/backups/padelizou");
        Assert.DoesNotContain("$COPIAS/db_padel_", trecho);
    }

    // ── 3. O fuso ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sao_Paulo_e_aceito()
    {
        Assert.True(FusoDoServidor.EhSaoPaulo(TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")));
    }

    [Fact]
    public void UTC_e_recusado()
    {
        // O VPS novo nasce em UTC — foi assim em agosto, com todo jogo "atrasado" desde o 1º minuto.
        Assert.False(FusoDoServidor.EhSaoPaulo(TimeZoneInfo.Utc));
    }

    [Fact]
    public void Mesmo_deslocamento_de_outro_lugar_tambem_e_recusado()
    {
        // Buenos Aires é −3 como São Paulo hoje, mas é outro relógio (e outra regra de horário
        // de verão se o Brasil voltar a ter). A conferência é pelo fuso, não pela conta de hoje.
        Assert.False(FusoDoServidor.EhSaoPaulo(TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")));
    }

    [Fact]
    public void O_healthz_responde_503_fora_de_Sao_Paulo()
    {
        var program = File.ReadAllText(Path.Combine(Raiz(), "Padelizou", "Program.cs"));
        var healthz = Trecho(program, "app.MapMethods(\"/healthz\"", "Results.Text(\"ok\")");

        Assert.Contains("FusoDoServidor.EhSaoPaulo(TimeZoneInfo.Local)", healthz);
    }

    // ── apoio ────────────────────────────────────────────────────────────────────────────────

    private static int ConferirDump(string pasta)
    {
        var script = Script("backup-offsite.sh");
        var funcao = Regex.Match(script, @"^dump_do_dia_ok\(\) \{.*?^\}", RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(funcao.Success, "o backup-offsite.sh não tem a função dump_do_dia_ok");

        var programa = "set -eo pipefail\nlog() { echo \"$*\" >&2; }\n" + funcao.Value + "\ndump_do_dia_ok \"$1\" 6\n";
        var psi = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(programa);
        psi.ArgumentList.Add("teste");
        psi.ArgumentList.Add(pasta);
        using var processo = Process.Start(psi)!;
        processo.StandardOutput.ReadToEnd();
        processo.StandardError.ReadToEnd();
        Assert.True(processo.WaitForExit(30_000), "a função do backup não terminou em 30 s");
        return processo.ExitCode;
    }

    private static void Dump(PastaTemporaria pasta, string nome, bool completo, TimeSpan idade)
    {
        var conteudo = new StringBuilder("--\n-- PostgreSQL database dump\n--\n\n\\restrict ABC123\n\nCREATE TABLE \"Jogador\" (\"Id\" integer);\n");
        // O fim de um dump DE VERDADE (pg_dump 16.15, conferido em 13/11/2026): desde a correção de
        // segurança de ago/2025 o "\unrestrict" vem DEPOIS da linha de completo — quem procurar
        // só na última linha não acha.
        if (completo) conteudo.Append("\n\n--\n").Append(FimDoPgDump).Append("\n--\n\n\\unrestrict ABC123\n\n");

        var caminho = Path.Combine(pasta.Caminho, nome);
        using (var arquivo = File.Create(caminho))
        using (var gzip = new GZipStream(arquivo, CompressionLevel.Fastest))
            gzip.Write(Encoding.UTF8.GetBytes(conteudo.ToString()));
        File.SetLastWriteTimeUtc(caminho, DateTime.UtcNow - idade);
    }

    private static bool BashDisponivel() => File.Exists("/bin/bash") || File.Exists("/usr/bin/bash");

    private static string Script(string nome) => File.ReadAllText(Path.Combine(Raiz(), "infra", "vps", nome));

    private static string Trecho(string texto, string de, string ate)
    {
        int inicio = texto.IndexOf(de, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"não achei \"{de}\"");
        int fim = texto.IndexOf(ate, inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, $"não achei \"{ate}\" depois de \"{de}\"");
        return texto[inicio..fim];
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório subindo a partir de " + AppContext.BaseDirectory);
    }

    private sealed class PastaTemporaria : IDisposable
    {
        public string Caminho { get; } = Directory.CreateTempSubdirectory("padelizou-backup-").FullName;
        public void Dispose() { try { Directory.Delete(Caminho, recursive: true); } catch { /* temporária */ } }
    }
}
