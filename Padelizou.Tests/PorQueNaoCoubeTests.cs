using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "SE NÃO COUBER, TEM Q AVISAR POR QUE NÃO COUBE."
//
// 🗣️ Felipe, 09/09/2026, perguntado sobre o que a grade deve fazer quando os jogos não cabem até
// o fim do torneio: *"avisa na hora do sorteio que não cabe, mas nesse caso ai, fizemos todo o
// planejamento, tem que caber, se não couber, tem q avisar por que nao coube"*.
//
// 🕳️ O QUE ELE VIU: a grade do Er marcou jogo em **15/09**, uma terça, num torneio que ele diz
// terminar no domingo **13/09** — e sem nada em 13 nem em 14. `Torneio.DataFim` existia desde
// sempre e o motor NUNCA a leu: era um aviso de tela na previsão (`PrevisaoGradeVM.EstouraOPrazo`)
// e mais nada.
//
// ⚠️ E O AVISO SOZINHO NÃO BASTAVA, porque ele não dizia a causa. As três que este arquivo cobre
// são as que produzem uma grade estourada sem ninguém ter digitado nada de absurdo:
//
//   1. QUADRA COM JANELA FORA DAS DATAS DO TORNEIO — o `datetime-local` da tabela de quadras é
//      fácil de errar em um dígito, e uma quadra "disponível 15/09" num torneio que acaba 13/09
//      é vaga que a grade conta e o organizador não tem;
//   2. DIA DO TORNEIO SEM QUADRA ABERTA NENHUMA — as janelas cobrem sábado e terça e deixam
//      domingo e segunda vazios; a grade escorrega por cima dos dias mortos, que é exatamente
//      como 12/09 vira 15/09 sem passar por 13;
//   3. VOLUME — são mais jogos do que vagas, e nenhuma quadra está errada.
public class PorQueNaoCoubeTests
{
    [Fact]
    public void Quadra_disponivel_so_depois_do_fim_do_torneio_e_nomeada()
    {
        var torneio = Torneio();
        var quadras = new[]
        {
            Quadra("Er Padel · Arena Nclass", null, null),
            // O dígito errado: a Radar 1 foi cadastrada pra TERÇA, e o torneio acaba no domingo.
            Quadra("Radar · Radar 1", new DateTime(2026, 9, 15, 19, 0, 0), new DateTime(2026, 9, 15, 23, 0, 0)),
        };

        var motivos = PorQueNaoCoube.Analisar(torneio, quadras, Sedes(quadras), totalDeJogos: 10, ultimoJogo: null);

        Assert.Contains(motivos, m => m.Contains("Radar · Radar 1") && m.Contains("15/09"));
    }

    [Fact]
    public void Dia_do_torneio_sem_nenhuma_quadra_aberta_e_nomeado()
    {
        var torneio = Torneio();
        // Só sábado tem quadra: domingo 13/09 fica sem nenhuma, e é o dia que o torneio promete.
        var quadras = new[]
        {
            Quadra("Arena 1", new DateTime(2026, 9, 12, 8, 0, 0), new DateTime(2026, 9, 12, 23, 0, 0)),
        };

        var motivos = PorQueNaoCoube.Analisar(torneio, quadras, Sedes(quadras), totalDeJogos: 10, ultimoJogo: null);

        Assert.Contains(motivos, m => m.Contains("13/09"));
    }

    [Fact]
    public void Sem_quadra_errada_o_motivo_e_o_VOLUME_e_ele_traz_os_dois_numeros()
    {
        // 12 e 13/09, 8h às 23h, 2 quadras, 50 min. São 19 rodadas por dia (8h00 … 23h00 de 50 em
        // 50, e as 23h em ponto CONTAM — o limite é a hora de COMEÇAR), 2 vagas cada, 2 dias: 76.
        //
        // ⚠️ ESCREVI 64 AQUI NA PRIMEIRA VERSÃO e o teste pegou. Fica o número derivado à mão do
        // lado do esperado justamente por isso: número mágico em teste de capacidade é o jeito
        // mais fácil de "consertar" o código pra bater com uma conta errada.
        var torneio = Torneio();
        var quadras = new[] { Quadra("Arena 1", null, null), Quadra("Arena 2", null, null) };

        var motivos = PorQueNaoCoube.Analisar(torneio, quadras, Sedes(quadras), totalDeJogos: 500, ultimoJogo: null);

        // O organizador precisa dos DOIS números pra decidir o que mudar: quantos jogos tem e
        // quantas vagas o expediente rende. Um só não diz o tamanho do buraco.
        Assert.Contains(motivos, m => m.Contains("500") && m.Contains("76"));
    }

    [Fact]
    public void Cabendo_folgado_nao_ha_motivo_nenhum()
    {
        var torneio = Torneio();
        var quadras = new[] { Quadra("Arena 1", null, null), Quadra("Arena 2", null, null) };

        Assert.Empty(PorQueNaoCoube.Analisar(torneio, quadras, Sedes(quadras), totalDeJogos: 10, ultimoJogo: null));
    }

    [Fact]
    public void Sem_DataFim_o_torneio_nao_tem_prazo_pra_estourar()
    {
        // Nada a comparar: quem não marcou o dia de devolver a quadra não pode passar dele.
        var torneio = Torneio();
        torneio.DataFim = null;

        var quadras = new[]
        {
            Quadra("Radar · Radar 1", new DateTime(2026, 9, 15, 19, 0, 0), new DateTime(2026, 9, 15, 23, 0, 0)),
        };

        Assert.Empty(PorQueNaoCoube.Analisar(torneio, quadras, Sedes(quadras), totalDeJogos: 500, ultimoJogo: null));
    }

    // ── O BURACO NA GRADE ────────────────────────────────────────────────────────────────
    //
    // 🗣️ Felipe, 09/09/2026, DEPOIS de refazer a grade: *"refiz a grade, continua com jogo dia 15,
    // 16, do nada ele pula do dia 12 p dia 15"*.
    //
    // 🕳️ A ASSINATURA ESTAVA NO PRÓPRIO PRINT: a cadência não se perde (18:00 → 18:50 → 19:40 →
    // 20:30), só a DATA salta. Isso é `GradeDeJogos.Encaixar` pulando vaga por vaga porque nenhuma
    // quadra está aberta naquele horário (`TemOndeJogar`), e voltando a marcar assim que uma abre.
    // Ele não dá erro nenhum: só empurra o torneio pra frente, calado.
    //
    // ⚠️ E ISTO NÃO PODE DEPENDER DE `DataFim`. O aviso de prazo fica MUDO quando o organizador não
    // preencheu o campo — e um torneio com dois dias vazios no meio é anômalo de qualquer jeito.
    // Foi a lição deste print: eu tinha pendurado o único aviso num campo opcional.
    [Fact]
    public void Dois_dias_sem_jogo_no_meio_da_grade_sao_apontados()
    {
        var torneio = Torneio();
        torneio.DataFim = null;                      // de propósito: o aviso não pode depender disso

        var quadras = new[]
        {
            Quadra("Arena 1", new DateTime(2026, 9, 12, 8, 0, 0), new DateTime(2026, 9, 12, 18, 0, 0)),
            Quadra("Arena 2", new DateTime(2026, 9, 15, 18, 0, 0), new DateTime(2026, 9, 15, 23, 0, 0)),
        };

        var jogos = new[]
        {
            Jogo(new DateTime(2026, 9, 12, 17, 10, 0)),
            Jogo(new DateTime(2026, 9, 12, 18, 0, 0)),
            Jogo(new DateTime(2026, 9, 15, 18, 50, 0)),
            Jogo(new DateTime(2026, 9, 15, 19, 40, 0)),
        };

        var buracos = PorQueNaoCoube.BuracosNaGrade(torneio, Sedes(quadras), jogos);

        Assert.Contains(buracos, b => b.Contains("13/09"));
        Assert.Contains(buracos, b => b.Contains("14/09"));
        // Tem que dizer a CAUSA, não só o buraco: é a causa que o organizador consegue consertar.
        Assert.Contains(buracos, b => b.Contains("quadra"));
    }

    [Fact]
    public void Grade_em_dias_seguidos_nao_tem_buraco()
    {
        var torneio = Torneio();
        var quadras = new[] { Quadra("Arena 1", null, null) };

        var jogos = new[]
        {
            Jogo(new DateTime(2026, 9, 12, 17, 10, 0)),
            Jogo(new DateTime(2026, 9, 13, 9, 0, 0)),
        };

        Assert.Empty(PorQueNaoCoube.BuracosNaGrade(torneio, Sedes(quadras), jogos));
    }

    [Fact]
    public void Dia_vazio_COM_quadra_aberta_e_apontado_sem_culpar_a_quadra()
    {
        // O buraco existe, mas a quadra estava aberta — a causa é outra (impedimento, folga do
        // organizador). Dizer "não havia quadra" seria mandar o organizador consertar o que não
        // está quebrado.
        var torneio = Torneio();
        var quadras = new[] { Quadra("Arena 1", null, null) };

        var jogos = new[]
        {
            Jogo(new DateTime(2026, 9, 12, 17, 10, 0)),
            Jogo(new DateTime(2026, 9, 14, 9, 0, 0)),
        };

        var buracos = PorQueNaoCoube.BuracosNaGrade(torneio, Sedes(quadras), jogos);

        Assert.Contains(buracos, b => b.Contains("13/09"));
        Assert.DoesNotContain(buracos, b => b.Contains("nenhuma quadra"));
    }

    private static Partida Jogo(DateTime quando) =>
        new() { Codigo = "X", Fase = "Grupo A", HorarioPrevisto = quando };

    // ── O sorteio de verdade avisa ───────────────────────────────────────────────────────
    [Fact]
    public async Task O_sorteio_avisa_quando_a_grade_passa_do_fim_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 24);

        torneio.DataInicio = new DateTime(2026, 9, 12, 8, 0, 0);
        torneio.HoraInicioDoDia = new TimeSpan(8, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(10, 0, 0);   // três rodadas por dia: não cabe
        torneio.DataFim = new DateTime(2026, 9, 13);
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 50;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var aviso = controller.TempData["Aviso"] as string;

        Assert.False(string.IsNullOrWhiteSpace(aviso),
            "o sorteio saiu calado com jogo marcado depois do fim do torneio");
        Assert.Contains("13/09", aviso!);
    }

    private static Torneio Torneio() => new()
    {
        Id = 1,
        Nome = "Torneio de Teste",
        Codigo = "TST123",
        DataInicio = new DateTime(2026, 9, 12, 8, 0, 0),
        DataFim = new DateTime(2026, 9, 13),
        HoraInicioDoDia = new TimeSpan(8, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
    };

    // A MESMA fábrica que a grade usa (Services/SedesDoTorneio), e não uma montada no teste:
    // uma segunda régua de "esta quadra está aberta?" um dia diria sim onde a grade diz não.
    private static SedesDoTorneio Sedes(IReadOnlyCollection<Quadra> quadras) =>
        SedesDoTorneio.Montar(clubePrincipalId: 0, minutosParaTrocarDeClube: 0,
            quadras, Array.Empty<Categoria>());

    private static Quadra Quadra(string nome, DateTime? de, DateTime? ate) =>
        new() { TorneioId = 1, Nome = nome, DisponivelDe = de, DisponivelAte = ate };
}
