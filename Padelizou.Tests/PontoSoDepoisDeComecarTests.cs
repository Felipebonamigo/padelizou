using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "SÓ DÊ ESSES PONTOS QUANDO O TORNEIO COMEÇAR E A PESSOA TIVER INSCRITA" (Felipe, 10/08/2026).
//
// ⚠️ ESTES TESTES NÃO SE SOBREPÕEM AOS DE `PontosDoTorneioTests`. Lá o motor puro é
// conferido com o status na mão; aqui o que se guarda é o CAMINHO — as oito consultas que
// somam ponto, e os `.Where` que decidem quem entra na soma e no tamanho da categoria.
// Um `.Where` esquecido não quebra o motor: quebra o ranking, calado.
public class PontoSoDepoisDeComecarTests
{
    // Uma categoria com `duplasQueJogaram` pares completos, o primeiro deles campeão.
    // Devolve o serviço e o id do campeão.
    private static (DbPadelContext ctx, EstatisticasService svc, int campeaoId, Categoria cat) Cenario(
        string status, int duplasQueJogaram = 5)
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio
        {
            Nome = "Copa", Codigo = "CPT", DataInicio = new DateTime(2026, 6, 1), Status = status,
        };
        var cat = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "C2M", Torneio = torneio };
        ctx.AddRange(torneio, cat);
        ctx.SaveChanges();

        int campeaoId = 0;
        for (int i = 0; i < duplasQueJogaram; i++)
        {
            var a = new Jogador { Nome = $"J{i}A", Cpf = $"9990000{i:00}1" };
            var b = new Jogador { Nome = $"J{i}B", Cpf = $"9990000{i:00}2" };
            ctx.Jogadores.AddRange(a, b);
            ctx.SaveChanges();

            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b.Id,
                UltimaFase = i == 0 ? "Campeao" : "Grupos",
            });
            if (i == 0) campeaoId = a.Id;
        }
        ctx.SaveChanges();

        return (ctx, new EstatisticasService(ctx), campeaoId, cat);
    }

    // ── O torneio precisa ter começado ────────────────────────────────────────────────
    [Theory]
    [InlineData(PortaDaInscricao.Aberta)]
    [InlineData(PortaDaInscricao.Fechada)]
    [InlineData(CancelamentoDoTorneio.Status)]
    public async Task Torneio_que_nao_comecou_nao_pontua_em_nenhuma_das_telas(string status)
    {
        // ⚠️ As três telas lem por caminhos DIFERENTES (resumo do perfil, mapa de pontos da
        // busca e ranking por categoria). Antes de 10/08/2026 as três pagavam pela inscrição.
        var (ctx, svc, campeaoId, _) = Cenario(status);
        using var _ctx = ctx;

        var resumo = await svc.ObterResumoJogadorAsync(campeaoId);
        var mapa = await svc.ObterPontosPorJogadorAsync(new[] { campeaoId });
        var porCategoria = await svc.ObterRankingPorCategoriaAsync();

        Assert.Equal(0, resumo.Pontos);
        Assert.Equal(0, mapa[campeaoId]);

        // ⚠️ E a categoria não aparece NEM VAZIA. Quem tem zero não é ranqueado (decisão do
        // Felipe, 10/08/2026): a tela mostrava "1º lugar — Fulano — 0 pontos" em produção,
        // que é o que se vê quando o único torneio existente ainda não começou.
        Assert.Empty(porCategoria);
    }

    [Fact]
    public async Task Quem_tem_zero_sai_da_lista_mas_quem_pontuou_fica()
    {
        // A contraprova que separa "escondi o zero" de "quebrei o ranking": no MESMO ranking,
        // o campeão aparece e o inscrito que não jogou (lista de espera) não.
        var (ctx, svc, campeaoId, cat) = Cenario("Finalizado");
        using var _ctx = ctx;

        var esperando = new Jogador { Nome = "Na Espera", Cpf = "99966600001" };
        var parceiro = new Jogador { Nome = "Parceiro Espera", Cpf = "99966600002" };
        ctx.Jogadores.AddRange(esperando, parceiro);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = esperando.Id, Jogador2Id = parceiro.Id,
            UltimaFase = "Grupos", EmListaDeEspera = true,
        });
        ctx.SaveChanges();

        var linhas = (await svc.ObterRankingPorCategoriaAsync()).SelectMany(c => c.Linhas).ToList();

        Assert.Contains(linhas, l => l.Jogador.Id == campeaoId && l.Pontos == 100);
        Assert.DoesNotContain(linhas, l => l.Jogador.Id == esperando.Id);
        Assert.DoesNotContain(linhas, l => l.Pontos == 0);
    }

    [Fact]
    public async Task Time_sem_ponto_tambem_sai_do_ranking_de_times()
    {
        // Metade da tela consertada seria pior que nenhuma: producao listava 25 times, todos
        // com 0 ponto, numerados de 1º a 25º, ao lado da aba de pontos ja limpa.
        var (ctx, svc, _, cat) = Cenario(PortaDaInscricao.Aberta);
        using var _ctx = ctx;

        var time = new Time { Nome = "Nata Padel" };
        ctx.Add(time);
        ctx.SaveChanges();
        foreach (var j in ctx.Jogadores.ToList()) j.TimeId = time.Id;
        ctx.SaveChanges();

        Assert.Empty(await svc.ObterRankingTimesAsync());
    }

    [Fact]
    public async Task Torneio_comecado_pontua_pelos_mesmos_tres_caminhos()
    {
        // A contraprova do teste acima: sem ela, "0 em todo lugar" também seria o resultado
        // de eu ter quebrado a soma inteira.
        var (ctx, svc, campeaoId, _) = Cenario("Finalizado");
        using var _ctx = ctx;

        var resumo = await svc.ObterResumoJogadorAsync(campeaoId);
        var mapa = await svc.ObterPontosPorJogadorAsync(new[] { campeaoId });
        var porCategoria = await svc.ObterRankingPorCategoriaAsync();

        Assert.Equal(100, resumo.Pontos);   // 5 duplas = peso 1,0
        Assert.Equal(100, mapa[campeaoId]);
        Assert.Contains(porCategoria.SelectMany(c => c.Linhas), l => l.Pontos == 100);
    }

    // ── A pessoa precisa ter entrado na chave ─────────────────────────────────────────
    [Fact]
    public async Task Lista_de_espera_nao_pontua_e_nao_incha_o_peso()
    {
        // ⚠️ O SEGUNDO ASSERT É O QUE PEGA O DEFEITO SUTIL. Tirar a lista de espera da soma e
        // esquecê-la na CONTAGEM faria a categoria valer por 8 duplas (peso 1,3) enquanto só
        // 5 jogaram — o campeão sairia com 130 em vez de 100, e ninguém desconfiaria.
        var (ctx, svc, campeaoId, cat) = Cenario("Finalizado");
        using var _ctx = ctx;

        for (int i = 0; i < 3; i++)
        {
            var a = new Jogador { Nome = $"Espera{i}A", Cpf = $"9998000{i:00}1" };
            var b = new Jogador { Nome = $"Espera{i}B", Cpf = $"9998000{i:00}2" };
            ctx.Jogadores.AddRange(a, b);
            ctx.SaveChanges();
            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b.Id,
                UltimaFase = "Grupos", EmListaDeEspera = true,
            });
        }
        ctx.SaveChanges();

        var naEspera = ctx.Jogadores.First(j => j.Nome == "Espera0A");
        var mapa = await svc.ObterPontosPorJogadorAsync(new[] { campeaoId, naEspera.Id });

        Assert.Equal(0, mapa[naEspera.Id]);     // não jogou, não pontua
        Assert.Equal(100, mapa[campeaoId]);     // e a categoria continua valendo 5 duplas
    }

    [Fact]
    public async Task Inscrito_sem_parceiro_nao_pontua_e_nao_incha_o_peso()
    {
        // Mesma família da lista de espera, e a mesma régua (`ForaDoSorteio`): quem se
        // inscreveu "ainda não tenho parceiro" e não fechou a dupla não entra no sorteio.
        var (ctx, svc, campeaoId, cat) = Cenario("Finalizado");
        using var _ctx = ctx;

        var solo = new Jogador { Nome = "Solo", Cpf = "99977700001" };
        ctx.Jogadores.Add(solo);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = solo.Id, Jogador2Id = null, UltimaFase = "Grupos",
        });
        ctx.SaveChanges();

        var mapa = await svc.ObterPontosPorJogadorAsync(new[] { campeaoId, solo.Id });

        Assert.Equal(0, mapa[solo.Id]);
        Assert.Equal(100, mapa[campeaoId]);
    }

    // ── A participação multiplicando, pelo caminho de verdade ─────────────────────────
    [Fact]
    public async Task Eliminado_num_campo_grande_pontua_mais_que_num_pequeno()
    {
        // O pedido do Felipe, medido no serviço e não só no motor: a mesma campanha ("caiu
        // nos grupos") vale 25 numa categoria de 20 duplas e 10 numa de 5.
        var (ctxGrande, svcGrande, _, _) = Cenario("Finalizado", duplasQueJogaram: 20);
        using var _a = ctxGrande;
        var (ctxPequeno, svcPequeno, _, _) = Cenario("Finalizado", duplasQueJogaram: 5);
        using var _b = ctxPequeno;

        // O jogador "J1A" é o primeiro da segunda dupla — caiu nos grupos nos dois cenários.
        var eliminadoGrande = ctxGrande.Jogadores.First(j => j.Nome == "J1A");
        var eliminadoPequeno = ctxPequeno.Jogadores.First(j => j.Nome == "J1A");

        var mapaGrande = await svcGrande.ObterPontosPorJogadorAsync(new[] { eliminadoGrande.Id });
        var mapaPequeno = await svcPequeno.ObterPontosPorJogadorAsync(new[] { eliminadoPequeno.Id });

        Assert.Equal(25, mapaGrande[eliminadoGrande.Id]);
        Assert.Equal(10, mapaPequeno[eliminadoPequeno.Id]);
    }
}
