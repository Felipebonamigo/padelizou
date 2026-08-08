using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Encerrar inscrição era de MÃO ÚNICA (corrigido em 08/08/2026).
//
// O organizador clicava uma vez e não havia volta — nem por engano, nem por motivo legítimo.
// A única saída era apagar o torneio e criar outro, perdendo o link já compartilhado.
//
// Aconteceu com o NATA PADEL TOUR (id 22 de producao, do Lucas "Foka"): parado em "Chaves em
// Sorteio" com ninguém inscrito, inscrições encerradas antes de a primeira pessoa entrar.
public class ReabrirInscricoesTests
{
    private static Torneio Fechado() => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1", Status = PortaDaInscricao.Fechada,
    };

    [Fact]
    public void Torneio_fechado_sem_sorteio_pode_reabrir()
    {
        Assert.Null(PortaDaInscricao.PorQueNaoPodeAbrir(Fechado(), jaSorteou: false));
    }

    [Fact]
    public void Depois_do_sorteio_nao_reabre()
    {
        // ⚠️ A trava que importa: com a chave publicada, tem gente sabendo contra quem joga.
        // Aceitar mais uma dupla aí não é reabrir inscrição, é refazer o torneio por baixo de
        // quem já se organizou.
        var recusa = PortaDaInscricao.PorQueNaoPodeAbrir(Fechado(), jaSorteou: true);

        Assert.NotNull(recusa);
        Assert.Contains("já foram sorteadas", recusa);
    }

    [Fact]
    public void Torneio_cancelado_nao_reabre()
    {
        var cancelado = Fechado();
        cancelado.Status = CancelamentoDoTorneio.Status;

        Assert.NotNull(PortaDaInscricao.PorQueNaoPodeAbrir(cancelado, jaSorteou: false));
    }

    [Fact]
    public void Quem_ja_esta_aberto_nao_reabre_de_novo()
    {
        var aberto = Fechado();
        aberto.Status = PortaDaInscricao.Aberta;

        Assert.NotNull(PortaDaInscricao.PorQueNaoPodeAbrir(aberto, jaSorteou: false));
        // E o contrário: aberto é justamente quem PODE fechar.
        Assert.Null(PortaDaInscricao.PorQueNaoPodeFechar(aberto));
    }

    [Fact]
    public void Fechado_sem_ninguem_dentro_nunca_abriu()
    {
        // Zero inscritos e zero jogos não é "encerrada", é "ainda não abriu" — e a tela dizia
        // "Inscrições Encerradas! Agora você pode sortear os grupos" pra esse caso, que é a
        // frase mais confusa possível pra quem tenta entender por que ninguém entra.
        Assert.True(PortaDaInscricao.NuncaAbriu(Fechado(), temInscrito: false, jaSorteou: false));

        // Com gente dentro, encerrou de verdade.
        Assert.False(PortaDaInscricao.NuncaAbriu(Fechado(), temInscrito: true, jaSorteou: false));
    }

    [Fact]
    public async Task Reabrir_devolve_o_torneio_para_inscricoes_abertas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.Status = PortaDaInscricao.Fechada;
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controlador.ReabrirInscricoes(torneio.Id);

        Assert.Equal(PortaDaInscricao.Aberta, ctx.Torneios.Find(torneio.Id)!.Status);
    }

    [Fact]
    public async Task Reabrir_com_a_chave_ja_sorteada_e_recusado_no_servidor()
    {
        // A tela esconde o botão, mas POST feito à mão passaria por cima dela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.Status = PortaDaInscricao.Fechada;
        ctx.SaveChanges();

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToListAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = "P1",
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
        });
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controlador.ReabrirInscricoes(torneio.Id);

        Assert.Equal(PortaDaInscricao.Fechada, ctx.Torneios.Find(torneio.Id)!.Status);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_reabre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.Status = PortaDaInscricao.Fechada;
        var estranho = new Jogador { Nome = "Estranho", Cpf = "11144477735" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, estranho.Id);
        await controlador.ReabrirInscricoes(torneio.Id);

        Assert.Equal(PortaDaInscricao.Fechada, ctx.Torneios.Find(torneio.Id)!.Status);
    }
}
