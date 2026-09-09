using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// ATÉ QUANDO DÁ PRA DEFINIR O PARCEIRO (09/09/2026).
//
// 🗣️ Felipe: "tem q manter o Paulo, ele vai colocar o parceiro dele depois". A dupla sem
// parceiro passou a entrar na chave — e sem esta janela isso não serviria pra nada, porque os
// SEIS caminhos de fechar dupla (definir por CPF, gerar convite, aceitar convite, chamar no
// mural, aceitar chamado) exigiam `Status == "Inscrições Abertas"`, que já passou quando o
// sorteio acontece. A janela vai até a bola rolar pra AQUELA dupla.
//
// 🕳️ E o alerta amarelo da tela já prometia isso ANTES de existir: "quem está sem parceiro
// ainda pode fechar a dupla" era renderizado em "Chaves em Sorteio", quando nenhum dos seis
// caminhos aceitava mais nada. A tela mentia; agora ela passa a dizer a verdade.
//
// ⚠️ POR QUE A RÉGUA NÃO OLHA O PLACAR: `Partida.GamesDupla1/2` e `SetsDupla1/2` são `int?` com
// `HasDefaultValue(0)` no Postgres — nascem 0 em produção e NULOS no EF InMemory da suíte.
// Qualquer régua escrita sobre "games == null" fecharia a janela em produção no segundo em que
// a chave saísse e passaria verde nos ~5.700 testes. Ela olha `Status`/`HorarioInicioReal`.
public class JanelaDoParceiroTests
{
    private static Dupla Solo(int id = 1) =>
        new() { Id = id, Codigo = $"D{id}", Jogador1Id = 10, Jogador2Id = null };

    private static Dupla Fechada(int id = 1) =>
        new() { Id = id, Codigo = $"D{id}", Jogador1Id = 10, Jogador2Id = 11 };

    // ── A JANELA ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Com_inscricoes_abertas_pode_definir()
        => Assert.Null(JanelaDoParceiro.MotivoParaNaoDefinir(Solo(), "Inscrições Abertas", jaComecouAJogar: false));

    [Fact]
    public void Depois_de_encerrar_as_inscricoes_ainda_pode_definir()
    {
        // O caso do Paulo: as inscrições fecharam, o organizador ainda não sorteou, e o
        // parceiro apareceu. Antes desta janela isto era recusado pelos seis caminhos.
        Assert.Null(JanelaDoParceiro.MotivoParaNaoDefinir(Solo(), "Chaves em Sorteio", jaComecouAJogar: false));
    }

    [Fact]
    public void Depois_de_sortear_a_chave_ainda_pode_definir()
    {
        // É o coração do pedido: a dupla JÁ está na chave, com a vaga em aberto, e o segundo
        // nome entra ali. A grade não muda — quem joga naquele horário continua sendo ela.
        Assert.Null(JanelaDoParceiro.MotivoParaNaoDefinir(Solo(), "Fase de Grupos", jaComecouAJogar: false));
    }

    [Fact]
    public void Depois_que_a_bola_rolou_para_ESSA_dupla_nao_pode_mais()
    {
        var motivo = JanelaDoParceiro.MotivoParaNaoDefinir(Solo(), "Fase de Grupos", jaComecouAJogar: true);

        Assert.NotNull(motivo);
        Assert.Contains("já entrou em quadra", motivo);
    }

    [Fact]
    public void Torneio_cancelado_nao_aceita_parceiro_novo()
    {
        var motivo = JanelaDoParceiro.MotivoParaNaoDefinir(Solo(), "Cancelado", jaComecouAJogar: false);

        Assert.NotNull(motivo);
    }

    [Fact]
    public void Dupla_que_ja_esta_completa_nao_passa_por_aqui()
    {
        // Esta régua é só pra DEFINIR o segundo nome que falta. TROCAR um parceiro que já
        // existe continua preso em "Inscrições Abertas" — trocar A por B numa chave já
        // sorteada bagunçaria jogos que outras pessoas já estão vendo.
        var motivo = JanelaDoParceiro.MotivoParaNaoDefinir(Fechada(), "Chaves em Sorteio", jaComecouAJogar: false);

        Assert.NotNull(motivo);
        Assert.Contains("já está completa", motivo);
    }

    [Fact]
    public void Time_nao_tem_parceiro_pra_definir()
    {
        var time = Solo(9);
        time.NomeTime = "Nata Padel";

        Assert.NotNull(JanelaDoParceiro.MotivoParaNaoDefinir(time, "Chaves em Sorteio", jaComecouAJogar: false));
    }

    // ── O FATO, APURADO NO BANCO ──────────────────────────────────────────────────────────

    private static async Task<(DbPadelContext ctx, Dupla dupla, Categoria categoria)> ComUmaDuplaAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (_, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var jogador = new Jogador { Nome = "Paulo", Cpf = "11144477735" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogador.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return (ctx, dupla, categoria);
    }

    private static async Task<Dupla> OutraDuplaAsync(DbPadelContext ctx, Categoria categoria)
    {
        var a = new Jogador { Nome = "A", Cpf = "22233344456" };
        var b = new Jogador { Nome = "B", Cpf = "33344455567" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return dupla;
    }

    [Fact]
    public async Task Sem_jogo_nenhum_a_bola_nao_rolou()
    {
        var (ctx, dupla, _) = await ComUmaDuplaAsync();
        using var _1 = ctx;

        Assert.False(await JanelaDoParceiro.JaComecouAJogarAsync(ctx, dupla.Id));
    }

    [Fact]
    public async Task Jogo_agendado_e_nao_comecado_nao_fecha_a_janela()
    {
        // É EXATAMENTE o estado da dupla depois do sorteio: tem jogo marcado e não jogou. Se
        // esta linha ficasse vermelha, a janela nova não serviria pra nada.
        var (ctx, dupla, categoria) = await ComUmaDuplaAsync();
        using var _1 = ctx;
        var adversaria = await OutraDuplaAsync(ctx, categoria);
        ctx.Partidas.Add(new Partida
        {
            TorneioId = categoria.TorneioId, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = dupla.Id, Dupla2Id = adversaria.Id, Status = "Agendada",
        });
        await ctx.SaveChangesAsync();

        Assert.False(await JanelaDoParceiro.JaComecouAJogarAsync(ctx, dupla.Id));
    }

    [Fact]
    public async Task Jogo_finalizado_fecha_a_janela()
    {
        var (ctx, dupla, categoria) = await ComUmaDuplaAsync();
        using var _1 = ctx;
        var adversaria = await OutraDuplaAsync(ctx, categoria);
        ctx.Partidas.Add(new Partida
        {
            TorneioId = categoria.TorneioId, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = adversaria.Id, Dupla2Id = dupla.Id, Status = "Finalizada",
        });
        await ctx.SaveChangesAsync();

        Assert.True(await JanelaDoParceiro.JaComecouAJogarAsync(ctx, dupla.Id));
    }

    [Fact]
    public async Task Jogo_em_andamento_fecha_a_janela()
    {
        // Bola rolando: não existe status "Em andamento", o que marca é o HorarioInicioReal.
        var (ctx, dupla, categoria) = await ComUmaDuplaAsync();
        using var _1 = ctx;
        var adversaria = await OutraDuplaAsync(ctx, categoria);
        ctx.Partidas.Add(new Partida
        {
            TorneioId = categoria.TorneioId, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = dupla.Id, Dupla2Id = adversaria.Id, Status = "Agendada",
            HorarioInicioReal = new DateTime(2026, 7, 1, 9, 0, 0),
        });
        await ctx.SaveChangesAsync();

        Assert.True(await JanelaDoParceiro.JaComecouAJogarAsync(ctx, dupla.Id));
    }

    [Fact]
    public async Task O_jogo_de_OUTRA_dupla_nao_fecha_a_minha_janela()
    {
        // "Primeiro jogo DELA", não do torneio: a categoria pode já ter começado enquanto o
        // jogo desta dupla é só amanhã.
        var (ctx, dupla, categoria) = await ComUmaDuplaAsync();
        using var _1 = ctx;
        var umaDupla = await OutraDuplaAsync(ctx, categoria);
        var outraDupla = await OutraDuplaAsync(ctx, categoria);
        ctx.Partidas.Add(new Partida
        {
            TorneioId = categoria.TorneioId, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = umaDupla.Id, Dupla2Id = outraDupla.Id, Status = "Finalizada",
        });
        await ctx.SaveChangesAsync();

        Assert.False(await JanelaDoParceiro.JaComecouAJogarAsync(ctx, dupla.Id));
    }
}
