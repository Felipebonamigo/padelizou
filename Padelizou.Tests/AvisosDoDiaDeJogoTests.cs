using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Os dois avisos que faltavam no dia do torneio.
//
// O "seu jogo é o próximo" é disparado pelo FIM do jogo anterior, e não por relógio, porque
// torneio atrasa: um aviso preso ao HorarioPrevisto chegaria com o jogador ainda almoçando,
// ou depois de ele já ter jogado. Quem sabe que a quadra vagou é o jogo que acabou nela.
public class AvisosDoDiaDeJogoTests
{
    private static int _proximoId = 1;

    private static Partida Jogo(string status, string? quadra, DateTime? previsto,
        DateTime? avisado = null, int? torneioId = 1)
    {
        var id = _proximoId++;
        return new Partida
        {
            Id = id,
            Codigo = $"P{id}",
            Fase = "Grupo A",
            TorneioId = torneioId,
            Status = status,
            NomeQuadra = quadra,
            HorarioPrevisto = previsto,
            AvisoProximoEnviadoEm = avisado,
            Dupla1 = new Dupla { Jogador1Id = id * 10 + 1, Jogador2Id = id * 10 + 2 },
            Dupla2 = new Dupla { Jogador1Id = id * 10 + 3, Jogador2Id = id * 10 + 4 },
        };
    }

    private static readonly DateTime Sabado = new(2026, 9, 5, 9, 0, 0);

    // ── Qual partida é avisada ────────────────────────────────────────────────────────

    [Fact]
    public void Avisa_a_proxima_da_MESMA_quadra()
    {
        // O jogo da Quadra 2 não interessa: quem vagou foi a Quadra 1.
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var naOutraQuadra = Jogo("Agendada", "Quadra 2", Sabado.AddMinutes(10));
        var certa = Jogo("Agendada", "Quadra 1", Sabado.AddMinutes(50));

        var escolhida = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { naOutraQuadra, certa });

        Assert.Equal(certa.Id, escolhida!.Id);
    }

    [Fact]
    public void Entre_dois_da_mesma_quadra_avisa_o_mais_cedo()
    {
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var depois = Jogo("Agendada", "Quadra 1", Sabado.AddHours(3));
        var agora = Jogo("Agendada", "Quadra 1", Sabado.AddMinutes(50));

        var escolhida = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { depois, agora });

        Assert.Equal(agora.Id, escolhida!.Id);
    }

    [Fact]
    public void Torneio_sem_quadra_nomeada_tambem_recebe_o_aviso()
    {
        // Torneio pequeno costuma não nomear quadra nenhuma. Se "sem nome" não casasse com
        // "sem nome", o aviso simplesmente nunca sairia pra eles — a feature não existiria.
        var terminada = Jogo("Finalizada", null, Sabado);
        var proxima = Jogo("Agendada", null, Sabado.AddMinutes(50));

        Assert.Equal(proxima.Id, AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { proxima })!.Id);
    }

    [Fact]
    public void Quadra_com_caixa_ou_espaco_diferente_e_a_mesma_quadra()
    {
        // "Quadra 1" e "quadra 1 " saem do mesmo formulário digitadas por gente diferente.
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var proxima = Jogo("Agendada", " quadra 1 ", Sabado.AddMinutes(50));

        Assert.NotNull(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { proxima }));
    }

    [Fact]
    public void Nao_avisa_duas_vezes_a_mesma_partida()
    {
        // O organizador finalizar sem querer e desfazer é comum no meio do torneio. Sem a
        // marca, cada desfazer mandaria o push de novo pros mesmos quatro jogadores.
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var jaAvisada = Jogo("Agendada", "Quadra 1", Sabado.AddMinutes(50), avisado: Sabado.AddMinutes(45));

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { jaAvisada }));
    }

    [Fact]
    public void Nao_avisa_quem_ja_esta_jogando_nem_quem_ja_jogou()
    {
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var aoVivo = Jogo("AoVivo", "Quadra 1", Sabado.AddMinutes(50));
        var acabada = Jogo("Finalizada", "Quadra 1", Sabado.AddMinutes(10));

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { aoVivo, acabada }));
    }

    [Fact]
    public void Partida_sem_horario_nao_e_avisada()
    {
        // Sem horário não dá pra dizer que ela é "a próxima" de nada.
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);
        var semHorario = Jogo("Agendada", "Quadra 1", null);

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { semHorario }));
    }

    [Fact]
    public void A_propria_partida_que_acabou_nunca_e_a_proxima()
    {
        var terminada = Jogo("Agendada", "Quadra 1", Sabado);

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { terminada }));
    }

    [Fact]
    public void Ultimo_jogo_do_dia_nao_avisa_ninguem()
    {
        var terminada = Jogo("Finalizada", "Quadra 1", Sabado);

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, Array.Empty<Partida>()));
    }

    // ── Quem recebe ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Avisa_os_quatro_jogadores_da_partida()
    {
        var p = Jogo("Agendada", "Quadra 1", Sabado);

        Assert.Equal(4, AvisosDoDiaDeJogo.JogadoresDa(p).Count);
    }

    [Fact]
    public void Dupla_sem_parceiro_nao_quebra_a_lista()
    {
        var p = Jogo("Agendada", "Quadra 1", Sabado);
        p.Dupla1!.Jogador2Id = null;

        var jogadores = AvisosDoDiaDeJogo.JogadoresDa(p);

        Assert.Equal(3, jogadores.Count);
        Assert.DoesNotContain(0, jogadores);
    }

    [Fact]
    public void Ninguem_recebe_o_mesmo_push_duas_vezes()
    {
        // Não deveria acontecer, mas um dado torto não pode fazer o celular vibrar em dobro.
        var p = Jogo("Agendada", "Quadra 1", Sabado);
        p.Dupla2!.Jogador1Id = p.Dupla1!.Jogador1Id;

        var jogadores = AvisosDoDiaDeJogo.JogadoresDa(p);

        Assert.Equal(jogadores.Count, jogadores.Distinct().Count());
    }

    // ── O texto ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_aviso_do_proximo_diz_a_quadra_quando_ela_tem_nome()
    {
        var comQuadra = Jogo("Agendada", "Quadra Central", Sabado);
        var semQuadra = Jogo("Agendada", null, Sabado);

        Assert.Contains("Quadra Central", AvisosDoDiaDeJogo.CorpoDoProximo(comQuadra));
        Assert.DoesNotContain("null", AvisosDoDiaDeJogo.CorpoDoProximo(semQuadra));
        Assert.Contains("próximo", AvisosDoDiaDeJogo.CorpoDoProximo(semQuadra));
    }

    [Fact]
    public void O_aviso_das_chaves_diz_QUANDO_a_pessoa_joga()
    {
        // "As chaves saíram" sozinho obriga a pessoa a ir procurar; o horário resolve.
        var corpo = AvisosDoDiaDeJogo.CorpoDasChaves(new DateTime(2026, 9, 5, 9, 30, 0));

        Assert.Contains("05/09", corpo);
        Assert.Contains("09:30", corpo);
    }

    [Fact]
    public void Sem_horario_o_aviso_das_chaves_ainda_chama_pra_ver()
    {
        var corpo = AvisosDoDiaDeJogo.CorpoDasChaves(null);

        Assert.Contains("chaves", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("01/01", corpo);
    }
}
