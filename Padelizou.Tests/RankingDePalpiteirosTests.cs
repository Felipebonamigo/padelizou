using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RANKING DE PALPITEIROS (12/08/2026).
//
// O palpitrômetro existia desde sempre e nunca dizia quem ACERTOU: mostrava a barra da partida
// e esquecia. Todo o histórico já estava gravado (`PalpitePartida.DuplaEscolhidaId` contra
// `Partida.VencedorId`), então o ranking nasce derivado — sem coluna nova e sem migration.
//
// A régua é **quantidade de acertos** (decisão do Felipe): um acerto, um ponto. Duas versões
// anteriores caíram — ponderar pelo tamanho da zebra (por legibilidade) e aproveitamento puro
// (pelo exemplo do 9/11 × 8/8, que também levou junto o piso mínimo que a média exigia).
//
// ⚠️ Com o total NÃO EXISTE problema de volume mínimo: quem acertou 1 de 1 tem 1 ponto e cai
// pro fim da lista sozinho. Ver Services/PontosDoPalpite.
public class RankingDePalpiteirosTests
{
    // ── A régua pura ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 2, 50.0)]
    [InlineData(3, 4, 75.0)]
    [InlineData(2, 3, 66.7)]
    [InlineData(0, 5, 0.0)]
    [InlineData(9, 11, 81.8)]
    [InlineData(7, 7, 100.0)]
    public void O_aproveitamento_e_acertos_sobre_resolvidos(int acertos, int resolvidos, double esperado)
    {
        // O aproveitamento não ordena nada — ele acompanha o total na tela, porque "9 de 11" e
        // "9 de 40" contam histórias diferentes sobre o mesmo 9.
        Assert.Equal(esperado, PontosDoPalpite.Aproveitamento(acertos, resolvidos));
    }

    [Fact]
    public void Sem_palpite_resolvido_o_aproveitamento_e_nulo_e_nao_zero()
    {
        // "0%" mentiria dizendo que a pessoa errou tudo, quando ela só não palpitou ainda.
        Assert.Null(PontosDoPalpite.Aproveitamento(acertos: 0, palpitesResolvidos: 0));
    }

    // ── O ranking de verdade, contra o banco ───────────────────────────────────────────

    [Fact]
    public async Task Nove_acertos_em_onze_ficam_na_frente_de_oito_em_oito()
    {
        // É O TESTE QUE DEFINE A RÉGUA, com o exemplo do Felipe. Por aproveitamento o resultado
        // seria o contrário (81,8% contra 100%): se este teste inverter, a regra mudou.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var nove = c.Espectador("Nove de Onze");
        var oito = c.Espectador("Oito de Oito");

        for (var i = 0; i < 11; i++)
        {
            var p = c.Partida();
            // O de 11 erra os dois últimos; o de 8 só palpita nos 8 primeiros, e acerta todos.
            c.Votam(p, i < 9 ? p.Dupla1Id : p.Dupla2Id, new[] { nove });
            if (i < 8) c.Votam(p, p.Dupla1Id, new[] { oito });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Equal(nove.Id, ranking[0].JogadorId);
        Assert.Equal(9, ranking[0].Acertos);
        Assert.Equal(81.8, ranking[0].Aproveitamento);

        Assert.Equal(oito.Id, ranking[1].JogadorId);
        Assert.Equal(8, ranking[1].Acertos);
        Assert.Equal(100.0, ranking[1].Aproveitamento);
    }

    [Fact]
    public async Task Quem_acerta_mais_fica_na_frente()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var bom = c.Espectador("Bom");      // 3 de 4
        var ruim = c.Espectador("Ruim");    // 1 de 4

        for (var i = 0; i < 4; i++)
        {
            var p = c.Partida();
            c.Votam(p, i < 3 ? p.Dupla1Id : p.Dupla2Id, new[] { bom });
            c.Votam(p, i < 1 ? p.Dupla1Id : p.Dupla2Id, new[] { ruim });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Equal(bom.Id, ranking[0].JogadorId);
        Assert.Equal(3, ranking[0].Acertos);
        Assert.Equal(1, ranking[1].Acertos);
    }

    [Fact]
    public async Task Um_acerto_em_um_palpite_nao_lidera_o_ranking()
    {
        // O 100% de quem palpitou uma vez não passa na frente de quem acertou 3 de 4 — e aqui
        // isso sai de graça do total, sem piso nenhum: 1 ponto é menos que 3.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var sortudo = c.Espectador("Sortudo");     // 1 de 1 = 100%
        var constante = c.Espectador("Constante"); // 3 de 4 = 75%

        var primeira = c.Partida();
        c.Votam(primeira, primeira.Dupla1Id, new[] { sortudo, constante });
        c.Finaliza(primeira, vencedora: primeira.Dupla1Id);

        for (var i = 0; i < 3; i++)
        {
            var p = c.Partida();
            c.Votam(p, i < 2 ? p.Dupla1Id : p.Dupla2Id, new[] { constante });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Equal(constante.Id, ranking[0].JogadorId);
        // ⚠️ Ele CONTINUA na lista — não há piso mínimo, e não precisa haver.
        Assert.Contains(ranking, l => l.JogadorId == sortudo.Id);
        Assert.Equal(sortudo.Id, ranking[^1].JogadorId);
    }

    [Fact]
    public async Task No_empate_de_acertos_quem_errou_menos_vem_primeiro()
    {
        // Entre dois de 3 acertos, o de 3 palpites errou menos que o de 5. O aproveitamento
        // entra SÓ aqui: pra separar quem empatou no total.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var certeiro = c.Espectador("Certeiro"); // 3 de 3
        var chutador = c.Espectador("Chutador"); // 3 de 5

        for (var i = 0; i < 5; i++)
        {
            var p = c.Partida();
            if (i < 3) c.Votam(p, p.Dupla1Id, new[] { certeiro });
            c.Votam(p, i < 3 ? p.Dupla1Id : p.Dupla2Id, new[] { chutador });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Equal(3, ranking[0].Acertos);
        Assert.Equal(3, ranking[1].Acertos);
        Assert.Equal(certeiro.Id, ranking[0].JogadorId);
        Assert.Equal(chutador.Id, ranking[1].JogadorId);
    }

    [Fact]
    public async Task Quem_joga_a_partida_nao_conta_nela()
    {
        // Os quatro em quadra são os únicos que podem MUDAR o resultado do próprio palpite.
        // Um ranking em que dá pra decidir se você acertou não é ranking.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var queJoga = (Jogador?)null;
        for (var i = 0; i < 3; i++)
        {
            var p = c.Partida();
            queJoga ??= c.JogadorDaDupla(p.Dupla1Id);
            c.Votam(p, p.Dupla1Id, new[] { queJoga });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        // Palpitou em 3 partidas e acertou as 3, mas uma delas era a DELE: só 2 contam.
        var desempenho = await c.Servico.ObterDesempenhoAsync(queJoga!.Id);

        Assert.Equal(2, desempenho.PalpitesResolvidos);
        Assert.Equal(2, desempenho.Acertos);
    }

    [Fact]
    public async Task Em_categoria_de_times_ninguem_e_excluido()
    {
        // ⚠️ Numa linha de TIME o Jogador1 é o ORGANIZADOR que cadastrou, não quem entra em
        // quadra (Dupla.NomeTime). Excluir por ali tiraria o acerto de quem nem jogou.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var cadastrou = (Jogador?)null;
        for (var i = 0; i < 3; i++)
        {
            var p = c.PartidaDeTimes();
            cadastrou ??= c.JogadorDaDupla(p.Dupla1Id);
            c.Votam(p, p.Dupla1Id, new[] { cadastrou });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Contains(ranking, l => l.JogadorId == cadastrou!.Id);
    }

    [Fact]
    public async Task Jogo_que_ainda_nao_aconteceu_nao_conta_como_erro()
    {
        // O denominador é o palpite JÁ RESPONDIDO. Contar jogo futuro faria todo mundo parecer
        // que erra muito no meio do torneio.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var acabou = c.Partida();
        var vaiAcontecer = c.Partida();
        var espectador = c.Espectador("Espectador");

        c.Votam(acabou, acabou.Dupla1Id, new[] { espectador });
        c.Finaliza(acabou, vencedora: acabou.Dupla1Id);
        c.Votam(vaiAcontecer, vaiAcontecer.Dupla1Id, new[] { espectador });
        await ctx.SaveChangesAsync();

        var desempenho = await c.Servico.ObterDesempenhoAsync(espectador.Id);

        Assert.Equal(1, desempenho.PalpitesResolvidos);
        Assert.Equal(1, desempenho.Acertos);
        Assert.Equal(1, desempenho.PalpitesEmAberto);
        Assert.Equal(100.0, desempenho.Aproveitamento);
    }

    [Fact]
    public async Task Quem_palpitou_uma_vez_so_aparece_no_perfil_e_na_lista()
    {
        // Sem piso, o novato existe nos dois lugares desde o primeiro palpite respondido — ele
        // só não lidera. Era a média que precisava barrar gente; o total não precisa.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);
        var novato = c.Espectador("Novato");

        var p = c.Partida();
        c.Votam(p, p.Dupla1Id, new[] { novato });
        c.Finaliza(p, vencedora: p.Dupla1Id);
        await ctx.SaveChangesAsync();

        var desempenho = await c.Servico.ObterDesempenhoAsync(novato.Id);
        var ranking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.True(desempenho.TemHistorico);
        Assert.Equal(1, desempenho.Acertos);
        Assert.Equal(100.0, desempenho.Aproveitamento);
        Assert.Contains(ranking, l => l.JogadorId == novato.Id);
    }

    [Fact]
    public async Task Quem_nunca_palpitou_nao_aparece_com_zero_por_cento()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);
        var ninguem = c.Espectador("Nunca Palpitou");
        await ctx.SaveChangesAsync();

        var desempenho = await c.Servico.ObterDesempenhoAsync(ninguem.Id);

        Assert.False(desempenho.TemHistorico);
        Assert.Null(desempenho.Aproveitamento);
    }

    [Fact]
    public async Task O_ranking_tem_ordem_total_e_nao_muda_entre_duas_visitas()
    {
        // Mesmo motivo da classificação de grupos: sem desempate até o fim, quem aparece no
        // pódio depende da ordem em que o banco devolveu as linhas.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var empatados = c.Espectadores(5).ToList();
        for (var i = 0; i < 3; i++)
        {
            var p = c.Partida();
            c.Votam(p, p.Dupla1Id, empatados);
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var primeira = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);
        var segunda = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);

        Assert.Equal(5, primeira.Count);
        Assert.Equal(primeira.Select(l => l.JogadorId), segunda.Select(l => l.JogadorId));
        Assert.Equal(primeira.Select(l => l.JogadorId).OrderBy(id => id), primeira.Select(l => l.JogadorId));
    }

    [Fact]
    public async Task O_ranking_de_um_torneio_nao_traz_palpite_de_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);
        var outro = new Cenario(ctx);

        var espectador = c.Espectador("Só Deste");

        for (var i = 0; i < 3; i++)
        {
            var daqui = c.Partida();
            c.Votam(daqui, daqui.Dupla1Id, new[] { espectador });
            c.Finaliza(daqui, vencedora: daqui.Dupla1Id);

            var dela = outro.Partida();
            outro.Votam(dela, dela.Dupla1Id, new[] { espectador });
            outro.Finaliza(dela, vencedora: dela.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var daquiRanking = await c.Servico.ObterRankingDoTorneioAsync(c.Torneio.Id);
        var geral = await c.Servico.ObterRankingGeralAsync();

        Assert.Equal(3, daquiRanking.Single(l => l.JogadorId == espectador.Id).PalpitesResolvidos);
        Assert.Equal(6, geral.Single(l => l.JogadorId == espectador.Id).PalpitesResolvidos);
    }

    [Fact]
    public async Task O_filtro_regional_do_hub_vale_pra_esta_aba_tambem()
    {
        // Ignorar a cidade escolhida faria a tela mostrar gente de fora ao lado de tabelas que
        // obedeceram — a página inteira passaria a mentir por causa de uma aba.
        using var ctx = TestInfra.NovoContexto();
        var c = new Cenario(ctx);

        var daqui = c.Espectador("Daqui");
        var deFora = c.Espectador("De Fora");

        for (var i = 0; i < 3; i++)
        {
            var p = c.Partida();
            c.Votam(p, p.Dupla1Id, new[] { daqui, deFora });
            c.Finaliza(p, vencedora: p.Dupla1Id);
        }
        await ctx.SaveChangesAsync();

        var semFiltro = await c.Servico.ObterRankingGeralAsync();
        var comFiltro = await c.Servico.ObterRankingGeralAsync(
            jogadoresDoLocal: new HashSet<int> { daqui.Id });

        Assert.Contains(semFiltro, l => l.JogadorId == deFora.Id);
        Assert.DoesNotContain(comFiltro, l => l.JogadorId == deFora.Id);
        Assert.Contains(comFiltro, l => l.JogadorId == daqui.Id);
    }

    // ── Montagem de cenário ────────────────────────────────────────────────────────────

    private sealed class Cenario
    {
        private readonly DbPadelContext _ctx;
        private readonly Categoria _categoria;
        private int _seq;
        private static int _global;

        public Torneio Torneio { get; }
        public PalpiteService Servico { get; }

        public Cenario(DbPadelContext ctx)
        {
            _ctx = ctx;
            Servico = new PalpiteService(ctx);
            var n = ++_global;

            Torneio = new Torneio { Nome = $"Torneio {n}", Codigo = $"TST{n:000}", Status = "Em Andamento" };
            _ctx.Torneios.Add(Torneio);
            _categoria = new Categoria { Nome = "2ª Masculina", Codigo = $"CAT{n:000}", Torneio = Torneio };
            _ctx.Categorias.Add(_categoria);
            _ctx.SaveChanges();
        }

        public Jogador Espectador(string nome)
        {
            var j = new Jogador { Nome = $"{nome} {++_seq}", Cpf = $"999{_global:00}{_seq:000000}" };
            _ctx.Jogadores.Add(j);
            _ctx.SaveChanges();
            return j;
        }

        public IEnumerable<Jogador> Espectadores(int quantos) =>
            Enumerable.Range(1, quantos).Select(i => Espectador($"Torcedor {i}")).ToList();

        private Dupla NovaDupla(string? nomeTime = null)
        {
            var dupla = new Dupla
            {
                Categoria = _categoria,
                Jogador1 = Espectador("Atleta"),
                Jogador2 = nomeTime == null ? Espectador("Atleta") : null,
                NomeTime = nomeTime,
            };
            _ctx.Duplas.Add(dupla);
            _ctx.SaveChanges();
            return dupla;
        }

        public Partida Partida() => MontarPartida(NovaDupla(), NovaDupla());

        public Partida PartidaDeTimes() =>
            MontarPartida(NovaDupla("Time A"), NovaDupla("Time B"));

        private Partida MontarPartida(Dupla d1, Dupla d2)
        {
            var partida = new Partida
            {
                CategoriaId = _categoria.Id,
                TorneioId = Torneio.Id,
                Dupla1Id = d1.Id,
                Dupla2Id = d2.Id,
                Codigo = $"P{_global:00}{++_seq:000}",
                Status = "Agendada",
            };
            _ctx.Partidas.Add(partida);
            _ctx.SaveChanges();
            return partida;
        }

        // O primeiro jogador da dupla — quem está em quadra (ou, num time, quem cadastrou).
        public Jogador JogadorDaDupla(int duplaId)
        {
            var dupla = _ctx.Duplas.Single(d => d.Id == duplaId);
            return _ctx.Jogadores.Single(j => j.Id == dupla.Jogador1Id);
        }

        public void Votam(Partida partida, int duplaId, IEnumerable<Jogador> jogadores)
        {
            foreach (var j in jogadores)
                _ctx.PalpitesPartida.Add(new PalpitePartida
                {
                    PartidaId = partida.Id,
                    JogadorId = j.Id,
                    DuplaEscolhidaId = duplaId,
                });
            _ctx.SaveChanges();
        }

        public void Finaliza(Partida partida, int vencedora)
        {
            partida.Status = "Finalizada";
            partida.VencedorId = vencedora;
            _ctx.SaveChanges();
        }
    }
}
