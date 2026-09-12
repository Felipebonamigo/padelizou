using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DOIS "VOTAR" DO MESMO JOGADOR NO MESMO JOGO, AO MESMO TEMPO, NÃO PODEM DERRUBAR O PALPITE.
//
// Erro em produção (10/09/2026): `DbUpdateException em POST /Partidas/Votar`. O palpitômetro
// não tem trava de clique no `palpitometro.js` — um toque duplo no nome da dupla, duas abas ou
// o voto seguido da ficha de placar disparam DOIS POSTs. Cada requisição tem o PRÓPRIO
// DbContext: as duas leem "esse jogador ainda não votou", as duas INSEREM, e o índice único
// `IX_PalpitePartida_PartidaId_JogadorId` (que existe desde a migration inicial) recusa a
// segunda com 23505. A exceção subia inteira: o controller só trata `InvalidOperationException`,
// então virava 500 e push de erro pro celular do Felipe — por um clique duplo.
//
// ⚠️ **O ÍNDICE ESTÁ CERTO E CONTINUA SENDO ELE QUEM SEGURA** (é a mesma decisão do chamado do
// mural em `DuplasController`). O que faltava era o serviço saber PERDER a corrida: quem chega
// depois grava por cima da linha de quem chegou primeiro, que é o palpite que a pessoa acabou
// de dar.
//
// ⚠️ EF InMemory NÃO valida índice único — a suíte inteira passa lisa por este defeito (é a
// verdade estrutural do CLAUDE.md). Por isso o índice entra aqui como interceptor: ele faz, no
// mesmo instante em que o Postgres faz, o que o provedor de teste não faz sozinho.
public class PalpiteEmDobroTests
{
    // O GÊMEO CHEGOU PRIMEIRO: no instante em que ESTE palpite ia ser inserido, o clique
    // irmão já gravou a linha dele — e aí o INSERT bate no índice único. É a corrida da
    // produção contada de trás pra frente, sem thread e sem relógio.
    private sealed class OGemeoChegouPrimeiro : SaveChangesInterceptor
    {
        private readonly string _banco;
        private readonly PalpitePartida? _rival;
        private bool _jaAconteceu;

        // `rival` nulo = ninguém chegou antes; o INSERT falha do mesmo jeito. É como se
        // parece uma DbUpdateException que NÃO é a corrida (FK, coluna, o que for).
        public OGemeoChegouPrimeiro(string banco, PalpitePartida? rival)
        {
            _banco = banco;
            _rival = rival;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            bool vaiInserirPalpite = eventData.Context?.ChangeTracker
                .Entries<PalpitePartida>().Any(e => e.State == EntityState.Added) == true;

            if (_jaAconteceu || !vaiInserirPalpite) return result;
            _jaAconteceu = true;

            if (_rival != null)
            {
                using var doGemeo = ContextoSobre(_banco);
                doGemeo.PalpitesPartida.Add(_rival);
                await doGemeo.SaveChangesAsync(cancellationToken);
            }

            // O que o Npgsql entrega: 23505 embrulhado em DbUpdateException.
            throw new DbUpdateException(
                "An error occurred while saving the entity changes. See the inner exception for details.",
                new Exception("23505: duplicate key value violates unique constraint "
                            + "\"IX_PalpitePartida_PartidaId_JogadorId\""));
        }
    }

    private static DbPadelContext ContextoSobre(string banco, IInterceptor? interceptor = null)
    {
        var opcoes = new DbContextOptionsBuilder<DbPadelContext>().UseInMemoryDatabase(banco);
        if (interceptor != null) opcoes.AddInterceptors(interceptor);
        return new DbPadelContext(opcoes.Options);
    }

    private static async Task<(int partidaId, int torcedorId, int dupla1Id, int dupla2Id)>
        UmJogoAgendadoAsync(string banco)
    {
        using var ctx = ContextoSobre(banco);
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = 6;
        torneio.SetsFaseGrupos = 1;
        torneio.ContagemDeGames = ContagemDeGamesDoTorneio.Ate;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = FasesTorneio.FaseDeGrupos,
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);

        var torcedor = new Jogador { Nome = "Torcedor", Cpf = "55519000001" };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();

        return (partida.Id, torcedor.Id, duplas[0].Id, duplas[1].Id);
    }

    [Fact]
    public async Task Clique_duplo_no_palpite_grava_UMA_linha_e_nao_estoura()
    {
        var banco = "palpite-em-dobro-" + Guid.NewGuid();
        var (partidaId, torcedorId, dupla1Id, _) = await UmJogoAgendadoAsync(banco);

        // O clique gêmeo: mesmo jogador, mesmo jogo, mesma dupla.
        var gemeo = new PalpitePartida
        {
            PartidaId = partidaId, JogadorId = torcedorId, DuplaEscolhidaId = dupla1Id,
        };

        using var ctx = ContextoSobre(banco, new OGemeoChegouPrimeiro(banco, gemeo));
        var servico = new PalpiteService(ctx);

        var resumo = await servico.RegistrarVotoAsync(partidaId, torcedorId, dupla1Id);

        // Um voto só na tela — e não um 500 no lugar do palpitômetro.
        Assert.Equal(1, resumo.TotalVotos);
        Assert.Equal(dupla1Id, resumo.MeuVotoDuplaId);

        using var leitura = ContextoSobre(banco);
        var gravado = await leitura.PalpitesPartida.SingleAsync();
        Assert.Equal(dupla1Id, gravado.DuplaEscolhidaId);
    }

    [Fact]
    public async Task Quem_perde_a_corrida_grava_o_proprio_palpite_por_cima_do_gemeo()
    {
        var banco = "palpite-em-dobro-placar-" + Guid.NewGuid();
        var (partidaId, torcedorId, dupla1Id, dupla2Id) = await UmJogoAgendadoAsync(banco);

        // O gêmeo gravou o toque no nome (Dupla 1, sem placar). Este POST é o de DEPOIS —
        // a ficha de placar da Dupla 2 —, e é ele que a pessoa viu por último.
        var gemeo = new PalpitePartida
        {
            PartidaId = partidaId, JogadorId = torcedorId, DuplaEscolhidaId = dupla1Id,
        };

        using var ctx = ContextoSobre(banco, new OGemeoChegouPrimeiro(banco, gemeo));
        var servico = new PalpiteService(ctx);

        var resumo = await servico.RegistrarVotoAsync(partidaId, torcedorId, dupla2Id, 4, 6);

        Assert.Equal(dupla2Id, resumo.MeuVotoDuplaId);
        Assert.Equal(4, resumo.MeuPlacarLado1);
        Assert.Equal(6, resumo.MeuPlacarLado2);

        using var leitura = ContextoSobre(banco);
        var gravado = await leitura.PalpitesPartida.SingleAsync();
        Assert.Equal(dupla2Id, gravado.DuplaEscolhidaId);
        Assert.Equal(4, gravado.GamesDupla1);
        Assert.Equal(6, gravado.GamesDupla2);
    }

    [Fact]
    public async Task Erro_de_gravacao_que_NAO_e_a_corrida_continua_estourando()
    {
        var banco = "palpite-erro-de-verdade-" + Guid.NewGuid();
        var (partidaId, torcedorId, dupla1Id, _) = await UmJogoAgendadoAsync(banco);

        // ⚠️ Sem gêmeo nenhum: a gravação falhou por OUTRO motivo. Engolir isto seria trocar
        // um 500 que avisa por um palpite que some caladinho — o defeito que falha em silêncio.
        using var ctx = ContextoSobre(banco, new OGemeoChegouPrimeiro(banco, rival: null));
        var servico = new PalpiteService(ctx);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            servico.RegistrarVotoAsync(partidaId, torcedorId, dupla1Id));
    }
}
