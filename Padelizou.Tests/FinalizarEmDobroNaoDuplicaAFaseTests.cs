using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DOIS "FINALIZAR" NO MESMO JOGO, AO MESMO TEMPO, NÃO PODEM CRIAR A FASE SEGUINTE EM DOBRO.
//
// Ensaio do Er (10/09/2026, anomalia C1): dois POSTs iguais de FinalizarPartida no último jogo
// de grupo — clique duplo, duas abas, ou a fila offline da Mesa reentregando — criaram a
// Semifinal DUAS VEZES: 4 jogos em vez de 2, a prévia projetando "Vencedor Semifinal 1 x
// Vencedor Semifinal 4", e a Final que nunca ia nascer (o robô de avanço espera as 4 semis,
// devolve 4 vencedores, "Semifinal" já existe → para). A categoria fica presa no sábado.
//
// As guardas existentes ("já finalizada" no controller, `mataMataJaGerado` no robô) são
// check-then-insert: cada requisição tem o PRÓPRIO DbContext, as duas passam pela checagem
// antes de qualquer uma gravar, e as duas gravam.
//
// Por isso os dois testes usam DOIS CONTEXTOS INDEPENDENTES sobre o MESMO banco em memória —
// é assim que duas requisições chegam em produção. Um contexto só esconderia o defeito: a
// segunda carga devolveria a instância já rastreada, com o status que a primeira acabou de
// escrever.
public class FinalizarEmDobroNaoDuplicaAFaseTests
{
    private static DbPadelContext ContextoSobre(string banco, IInterceptor? interceptor = null)
    {
        var opcoes = new DbContextOptionsBuilder<DbPadelContext>().UseInMemoryDatabase(banco);
        if (interceptor != null) opcoes.AddInterceptors(interceptor);
        return new DbPadelContext(opcoes.Options);
    }

    // 6 duplas sorteadas (2 grupos de 3 → 2 classificam → Semifinal com 2 jogos), todos os
    // jogos de grupo finalizados menos o último — que fica com o placar gravado, pronto pra
    // ser finalizado.
    private static async Task<(int torneioId, int categoriaId, int organizadorId, int ultimoJogoId)>
        AteOUltimoJogoDeGrupoAsync(string banco)
    {
        using var ctx = ContextoSobre(banco);
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .OrderBy(p => p.Id)
            .ToListAsync();
        Assert.Equal(6, jogos.Count);

        foreach (var jogo in jogos.Take(jogos.Count - 1))
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var ultimo = jogos[^1];
        ultimo.GamesDupla1 = 9;
        ultimo.GamesDupla2 = 5;
        ultimo.SetsDupla1 = 1;
        ultimo.SetsDupla2 = 0;
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal"));
        return (torneio.Id, categoria.Id, org.Id, ultimo.Id);
    }

    // Segura UM SaveChanges do contexto — o primeiro que satisfizer a condição — até o teste
    // liberar. É o que permite parar uma requisição no meio, num ponto exato, sem relógio.
    private sealed class SeguraOSalvar : SaveChangesInterceptor
    {
        private readonly Func<ChangeTracker, bool> _quando;
        private readonly TaskCompletionSource _chegou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _pode = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SeguraOSalvar(Func<ChangeTracker, bool> quando) => _quando = quando;

        public Task Chegou => _chegou.Task;
        public void Liberar() => _pode.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_chegou.Task.IsCompleted && eventData.Context is { } contexto && _quando(contexto.ChangeTracker))
            {
                _chegou.TrySetResult();
                await _pode.Task;
            }
            return result;
        }
    }

    private static bool VaiGravarFinalizada(ChangeTracker rastreador, int partidaId) =>
        rastreador.Entries<Partida>().Any(e =>
            e.State == EntityState.Modified && e.Entity.Id == partidaId && e.Entity.Status == "Finalizada");

    private static bool VaiGravarAFase(ChangeTracker rastreador, string fase) =>
        rastreador.Entries<Partida>().Any(e => e.State == EntityState.Added && e.Entity.Fase == fase);

    // A INTERLEAVING EXATA DA PRODUÇÃO, determinística (sem relógio na parte que importa):
    //
    //   A carrega (por finalizar), passa pela guarda, e PARA no instante de gravar "Finalizada".
    //   B carrega — fresca, ainda por finalizar —, passa pela guarda, grava, roda o robô (que
    //     não vê Semifinal nenhuma) e PARA no instante de gravar a Semifinal dele.
    //   A acorda, grava "Finalizada", roda o robô (que TAMBÉM não vê Semifinal nenhuma) e grava.
    //   B acorda e grava a dele.
    //
    // Sem serialização: 4 semis. Com a trava por torneio, B só entra depois de A terminar
    // inteira — e aí relê a partida, já finalizada, e sai sem gravar nada.
    [Fact]
    public async Task Segundo_finalizar_que_entra_antes_de_o_primeiro_gravar_nao_cria_a_semifinal_de_novo()
    {
        var banco = "finalizar-em-dobro-" + Guid.NewGuid();
        var (_, categoriaId, organizadorId, ultimoJogoId) = await AteOUltimoJogoDeGrupoAsync(banco);

        var seguraA = new SeguraOSalvar(r => VaiGravarFinalizada(r, ultimoJogoId));
        var seguraB = new SeguraOSalvar(r => VaiGravarAFase(r, "Semifinal"));
        using var ctxA = ContextoSobre(banco, seguraA);
        using var ctxB = ContextoSobre(banco, seguraB);

        // O MESMO dublê de push nos dois lados: é ele que conta quantas vezes o jogo "acabou".
        var push = Substitute.For<IPushNotificationService>();
        var a = TestInfra.NovoTorneiosController(ctxA, organizadorId, push: push);
        var b = TestInfra.NovoTorneiosController(ctxB, organizadorId, push: push);

        // A: passou pela guarda e está a um passo de gravar "Finalizada".
        var tarefaA = a.FinalizarPartida(ultimoJogoId);
        if (await Task.WhenAny(seguraA.Chegou, tarefaA) == tarefaA) await tarefaA;   // se A quebrou antes, o erro aparece aqui
        Assert.True(seguraA.Chegou.IsCompleted, "A requisição A não chegou a gravar 'Finalizada'.");

        // B: entra agora. Sem a trava atravessa tudo até o INSERT da Semifinal dele; com a trava
        // fica esperando A — e o prazo aqui só existe pra este segundo caso não travar o teste.
        var tarefaB = b.FinalizarPartida(ultimoJogoId);
        await Task.WhenAny(seguraB.Chegou, tarefaB, Task.Delay(TimeSpan.FromMilliseconds(300)));

        // A acorda e termina INTEIRA antes de B ser solta — senão o INSERT de B, já a um passo,
        // vence a checagem de A e a corrida some por sorte de relógio.
        seguraA.Liberar();
        await tarefaA;
        seguraB.Liberar();
        await tarefaB;

        using var leitura = ContextoSobre(banco);
        var semis = await leitura.Partidas
            .Where(p => p.CategoriaId == categoriaId && p.Fase == "Semifinal")
            .ToListAsync();
        Assert.Equal(2, semis.Count);

        // Finalizada UMA vez: o "Vitória!" saiu uma vez pra cada um dos 2 vencedores, e não 4.
        await push.Received(2).EnviarParaJogadorAsync(
            Arg.Any<int>(), "Vitória!", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

        // E B nunca chegou a montar fase nenhuma: quem montou foi A, sozinha.
        Assert.False(seguraB.Chegou.IsCompleted, "A segunda requisição chegou a gravar uma Semifinal.");
    }

    // A corrida DE VERDADE, em dois threads, repetida: sem a trava ela é probabilística (basta
    // uma rodada duplicar); com a trava tem que ser determinística — nenhuma rodada duplica.
    [Fact]
    public async Task Dois_finalizar_em_paralelo_no_ultimo_jogo_de_grupo_criam_uma_semifinal_so()
    {
        for (int rodada = 1; rodada <= 20; rodada++)
        {
            var banco = $"corrida-{rodada}-" + Guid.NewGuid();
            var (_, categoriaId, organizadorId, ultimoJogoId) = await AteOUltimoJogoDeGrupoAsync(banco);

            using var ctxA = ContextoSobre(banco);
            using var ctxB = ContextoSobre(banco);
            var a = TestInfra.NovoTorneiosController(ctxA, organizadorId);
            var b = TestInfra.NovoTorneiosController(ctxB, organizadorId);

            await Task.WhenAll(
                Task.Run(async () => { await Task.Yield(); await a.FinalizarPartida(ultimoJogoId); }),
                Task.Run(async () => { await Task.Yield(); await b.FinalizarPartida(ultimoJogoId); }));

            using var leitura = ContextoSobre(banco);
            int semis = await leitura.Partidas.CountAsync(p => p.CategoriaId == categoriaId && p.Fase == "Semifinal");
            Assert.True(semis == 2, $"Rodada {rodada}: {semis} jogos de Semifinal (esperava 2).");

            var ultimo = await leitura.Partidas.SingleAsync(p => p.Id == ultimoJogoId);
            Assert.Equal("Finalizada", ultimo.Status);
        }
    }
}
