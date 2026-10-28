using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 28/10/2026 — O CANCELAMENTO DAS AULAS CAIU NO MESMO MINUTO DO BLOQUEIO.
//
// 🗣️ O Jônatas pagou, foi liberado, e a agenda dele estava toda vermelha: *"só ficou tudo vermelho
// aqui"*. Vermelho é CANCELADA. Tudo dele a partir de 21/10 ~10h — o dia e a hora em que o bloqueio
// foi ligado em produção (`BloqueioAPartirDe` = 2026-10-21) — tinha sido derrubado pelo
// CancelamentoPorBloqueioBackgroundService.
//
// 🕳️ O DEFEITO: a estreia é PISO das duas datas (BloqueioDoProfessor.Prazo). Pra quem tinha o
// último direito vencido havia mais de 30 dias, `fim + 6` e `fim + 30` estão AMBOS antes da
// estreia — e os dois viram o mesmo piso, 21/10 10h. A agenda fechava e as aulas caíam no mesmo
// tique, sem o mês de prazo nem os 20 dias de avisos que o item 4 promete ("fica marcado até 1
// mês depois do vencimento"). O piso protege o BLOQUEIO de pegar alguém de surpresa; aplicado
// cru ao cancelamento, apagava a distância entre os dois.
public class CancelamentoNaoCaiJuntoComOBloqueioTests
{
    private static readonly DateTime Estreia = new(2026, 10, 21);

    private static PlanoProfessorSettings Cfg() => new() { BloqueioAPartirDe = Estreia };

    [Fact]
    public void Plano_vencido_ha_meses_bloqueia_na_estreia_mas_as_aulas_so_caem_depois_do_prazo()
    {
        // Pagou até agosto: os dois prazos "naturais" já passaram quando o bloqueio liga.
        var professor = new Jogador
        {
            Nome = "Professor antigo", Cpf = "1", IsProfessor = true,
            AssinaturaProfessorPagaAte = new DateTime(2026, 8, 1),
        };
        var cfg = Cfg();

        var bloqueia = BloqueioDoProfessor.BloqueiaEm(professor, cfg);
        var cancela = BloqueioDoProfessor.CancelaAulasEm(professor, cfg);

        Assert.Equal(Estreia.AddHours(BloqueioDoProfessor.HoraDoBloqueio), bloqueia);
        // A mesma distância que o professor em dia teria entre fechar a agenda e perder as aulas.
        Assert.Equal(bloqueia!.Value.AddDays(cfg.DiasAteCancelarAsAulas - cfg.DiasAteOBloqueio), cancela);
    }

    [Fact]
    public void Professor_antigo_sem_relogio_tambem_ganha_o_prazo_inteiro()
    {
        // Nunca pagou nem teve teste, criado antes da estreia: o relógio dele parte da estreia.
        var professor = new Jogador
        {
            Nome = "Nunca pagou", Cpf = "2", IsProfessor = true,
            CriadoEm = new DateTime(2026, 5, 1),
        };
        var cfg = Cfg();

        var bloqueia = BloqueioDoProfessor.BloqueiaEm(professor, cfg);
        var cancela = BloqueioDoProfessor.CancelaAulasEm(professor, cfg);

        Assert.NotNull(bloqueia);
        Assert.NotNull(cancela);
        Assert.True(cancela > bloqueia!.Value.AddDays(cfg.DiasAteCancelarAsAulas - cfg.DiasAteOBloqueio - 1),
            $"Aulas caem em {cancela}, colado no bloqueio em {bloqueia}.");
    }

    [Fact]
    public void Quem_vence_depois_da_estreia_segue_o_proprio_relogio()
    {
        // O piso não pode empurrar ninguém que já estava no relógio normal.
        var professor = new Jogador
        {
            Nome = "Venceu em novembro", Cpf = "3", IsProfessor = true,
            AssinaturaProfessorPagaAte = new DateTime(2026, 11, 10),
        };
        var cfg = Cfg();

        Assert.Equal(new DateTime(2026, 11, 10).AddDays(cfg.DiasAteOBloqueio).AddHours(BloqueioDoProfessor.HoraDoBloqueio),
            BloqueioDoProfessor.BloqueiaEm(professor, cfg));
        Assert.Equal(new DateTime(2026, 11, 10).AddDays(cfg.DiasAteCancelarAsAulas).AddHours(BloqueioDoProfessor.HoraDoBloqueio),
            BloqueioDoProfessor.CancelaAulasEm(professor, cfg));
    }
}
