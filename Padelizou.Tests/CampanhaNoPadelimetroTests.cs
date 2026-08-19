using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O AJUSTE DE CAMPANHA do Padelímetro (RANKING.md, "A campanha também move o número"):
// campeão ganha, quem fica na chave (ou cai na estreia do mata-mata) perde — e as portas
// da faixa limitam o ajuste, pra régua não drenar o jogador mediano que passa 20 torneios
// na mesma categoria.
public class CampanhaNoPadelimetroMotorTests
{
    // 4ª masculina: faixa 550–649 → linha de descida 500, linha de subida 650.
    private const string Quarta = "4ª Categoria Masculina";

    [Fact]
    public void Primeira_fase_do_mata_mata_e_a_mais_cedo_que_existe_de_verdade()
    {
        Assert.Equal("Semifinal", CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(
            new[] { "Grupo A", "Grupo B", "Semifinal", "Final" }));
        Assert.Equal("Quartas de Final", CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(
            new[] { "Fase de Grupos", "Final", "Semifinal", "Quartas de Final" }));
        Assert.Equal(ChaveamentoMataMata.PrimeiraRodada, CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(
            new[] { "Grupo A", ChaveamentoMataMata.PrimeiraRodada, "Oitavas de Final", "Final" }));

        // Sem mata-mata não há chave da qual sair: rodízio de Americano e grupo puro.
        Assert.Null(CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(new[] { "Grupo A", "Grupo B" }));
        Assert.Null(CampanhaNoPadelimetro.PrimeiraFaseDoMataMata(new[] { "Americano Rodada 1" }));
    }

    [Fact]
    public void Campeao_ganha_quem_fica_na_chave_perde_e_o_meio_nao_mexe()
    {
        var campeao = CampanhaNoPadelimetro.Ajuste(600, "Campeao", "Semifinal", Quarta);
        Assert.Equal(10, campeao!.Value.Delta);
        Assert.Contains("Campeão da 4ª Categoria Masculina", campeao.Value.Motivo);

        var naChave = CampanhaNoPadelimetro.Ajuste(600, "Grupos", "Semifinal", Quarta);
        Assert.Equal(-10, naChave!.Value.Delta);
        Assert.Contains("Não saiu da chave", naChave.Value.Motivo);

        var estreia = CampanhaNoPadelimetro.Ajuste(600, "Semifinal", "Semifinal", Quarta);
        Assert.Equal(-5, estreia!.Value.Delta);
        Assert.Contains("estreia do mata-mata", estreia.Value.Motivo);

        // Vice e fase do meio: o Elo já pagou esses degraus jogo a jogo.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Final", "Semifinal", Quarta));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Semifinal", "Quartas de Final", Quarta));
    }

    [Fact]
    public void Final_direta_nao_pune_o_vice_como_estreante()
    {
        // 1 grupo → Final direta: a primeira fase do mata-mata É a final. Quem perdeu é o
        // VICE — chegou na final, não leva pena de estreia. O campeão e quem ficou no
        // grupo seguem valendo.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Final", "Final", Quarta));
        Assert.Equal(10, CampanhaNoPadelimetro.Ajuste(600, "Campeao", "Final", Quarta)!.Value.Delta);
        Assert.Equal(-10, CampanhaNoPadelimetro.Ajuste(600, "Grupos", "Final", Quarta)!.Value.Delta);
    }

    [Fact]
    public void A_pena_para_na_linha_de_descida_e_nao_atravessa()
    {
        // 505 na 4ª (linha de descida 500): a pena de −10 vira −5 e PARA na porta.
        Assert.Equal(-5, CampanhaNoPadelimetro.Ajuste(505, "Grupos", "Semifinal", Quarta)!.Value.Delta);

        // Na porta ou abaixo dela, nada: dali pra baixo só derrota de verdade move.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(500, "Grupos", "Semifinal", Quarta));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(460, "Grupos", "Semifinal", Quarta));
    }

    [Fact]
    public void Quem_joga_pra_cima_nao_leva_pena_pela_aventura()
    {
        // Nível de 4ª (600) caindo na estreia da 2ª (faixa 750–849, linha de descida 700):
        // essa régua não é a dele — subir é sempre livre, e a aventura não é campanha ruim.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Quartas de Final", "Quartas de Final",
            "2ª Categoria Masculina"));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Grupos", "Quartas de Final",
            "2ª Categoria Masculina"));
    }

    [Fact]
    public void O_bonus_para_na_linha_de_subida_e_farmar_titulo_nao_infla()
    {
        // 645 na 4ª (linha de subida 650): o bônus de +10 vira +5 — cruza a porta e para.
        Assert.Equal(5, CampanhaNoPadelimetro.Ajuste(645, "Campeao", "Semifinal", Quarta)!.Value.Delta);

        // Número já acima da categoria: título ali não prova nada — trava anti-farm.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(650, "Campeao", "Semifinal", Quarta));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(700, "Campeao", "Semifinal", Quarta));
    }

    [Fact]
    public void Categoria_sem_faixa_nao_tem_campanha()
    {
        // Mista, casal e nome fora da convenção: sem faixa não há porta pra régua mirar.
        // (Os JOGOS dessas categorias seguem movendo o número — isso é do motor Elo.)
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Campeao", "Semifinal", "Mista B"));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Grupos", "Semifinal", "Categoria Casais"));
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Grupos", "Semifinal", "Sub-14"));

        // E sem mata-mata não há campanha nenhuma.
        Assert.Null(CampanhaNoPadelimetro.Ajuste(600, "Campeao", null, Quarta));
    }

    [Fact]
    public void As_portas_da_faixa_ficam_presas_na_regua()
    {
        // Bordas da escada: a 7ª (piso 0) não promete porta abaixo do zero, e a Open
        // (teto 1000) não promete porta acima do mil.
        var setima = FaixasDePadelimetro.DaCategoria("7ª Categoria Masculina")!;
        Assert.Equal(0, FaixasDePadelimetro.LinhaDeDescida(setima));

        var open = FaixasDePadelimetro.DaCategoria("Categoria Open Masculino")!;
        Assert.Equal(1000, FaixasDePadelimetro.LinhaDeSubida(open));

        // A 4ª: piso 550 − 50 = 500; teto 649 + 1 = 650.
        var quarta = FaixasDePadelimetro.DaCategoria(Quarta)!;
        Assert.Equal(500, FaixasDePadelimetro.LinhaDeDescida(quarta));
        Assert.Equal(650, FaixasDePadelimetro.LinhaDeSubida(quarta));
    }
}

// O serviço aplicando a campanha de verdade: gancho da final, replay, desfazer.
public class CampanhaNoPadelimetroServiceTests
{
    private sealed record Cenario(Torneio Torneio, Categoria Categoria, Partida Final,
        Jogador[] Campeoes, Jogador[] Vices, Jogador[] CairamNaEstreia, Jogador[] FicaramNaChave);

    // Uma 4ª masculina JOGADA até a final: A campeã, B vice, C e D caíram na semifinal
    // (a estreia do mata-mata) e E e F ficaram na chave. Todo mundo entrou em quadra.
    // As duplas já vêm carimbadas como o sistema deixa depois da final (FinalizarPartida
    // carimba o perdedor; CoroarCampeao carimba o campeão).
    private static Cenario MontarCategoriaJogada(DbPadelContext ctx,
        string nomeCategoria = "4ª Categoria Masculina", int? nivelInicial = 600,
        bool restrito = false, bool deTimes = false, string statusDoTorneio = "Finalizado",
        bool campeaoJaCarimbado = true)
    {
        var torneio = new Torneio
        {
            Nome = "Aberto de Teste",
            Codigo = "CAMP1",
            Status = statusDoTorneio,
            Formato = FormatoDoTorneio.Padrao,
            Restrito = restrito,
            DataInicio = new DateTime(2026, 8, 15),
        };
        var categoria = new Categoria { Nome = nomeCategoria, Codigo = "C1", Torneio = torneio, DeTimes = deTimes };

        var jogadores = Enumerable.Range(1, 12)
            .Select(i => new Jogador
            {
                Nome = $"Jogador {i:00}",
                Cpf = $"777000000{i:02}",
                Padelimetro = nivelInicial,
                JogosDePadelimetro = nivelInicial == null ? 0 : 12,
            })
            .ToArray();
        ctx.Jogadores.AddRange(jogadores);

        Dupla NovaDupla(int i1, int i2, string ultimaFase) =>
            new() { Categoria = categoria, Jogador1 = jogadores[i1], Jogador2 = jogadores[i2], UltimaFase = ultimaFase };

        var a = NovaDupla(0, 1, campeaoJaCarimbado ? "Campeao" : "Grupos");
        var b = NovaDupla(2, 3, "Final");
        var c = NovaDupla(4, 5, "Semifinal");
        var d = NovaDupla(6, 7, "Semifinal");
        var e = NovaDupla(8, 9, "Grupos");
        var f = NovaDupla(10, 11, "Grupos");
        ctx.Duplas.AddRange(a, b, c, d, e, f);
        ctx.SaveChanges();

        int seq = 0;
        Partida NovaPartida(Dupla d1, Dupla d2, string fase, int g1, int g2)
        {
            var p = new Partida
            {
                CategoriaId = categoria.Id,
                TorneioId = torneio.Id,
                Dupla1Id = d1.Id,
                Dupla2Id = d2.Id,
                Codigo = $"J{++seq:00}",
                Status = "Finalizada",
                Fase = fase,
                GamesDupla1 = g1,
                GamesDupla2 = g2,
                SetsDupla1 = g1 > g2 ? 1 : 0,
                SetsDupla2 = g2 > g1 ? 1 : 0,
                VencedorId = g1 > g2 ? d1.Id : d2.Id,
                PlacarMarcadoEm = new DateTime(2026, 8, 15, 9, 0, 0).AddHours(seq),
            };
            ctx.Partidas.Add(p);
            return p;
        }

        // Grupos (E e F só jogam aqui), semis e final — a linha do tempo inteira.
        NovaPartida(e, a, "Fase de Grupos", 2, 6);
        NovaPartida(f, b, "Fase de Grupos", 3, 6);
        NovaPartida(a, c, "Semifinal", 6, 2);
        NovaPartida(b, d, "Semifinal", 6, 3);
        var final = NovaPartida(a, b, "Final", 6, 4);
        ctx.SaveChanges();

        return new Cenario(torneio, categoria, final,
            new[] { jogadores[0], jogadores[1] }, new[] { jogadores[2], jogadores[3] },
            new[] { jogadores[4], jogadores[5], jogadores[6], jogadores[7] },
            new[] { jogadores[8], jogadores[9], jogadores[10], jogadores[11] });
    }

    [Fact]
    public async Task Campanha_move_campeao_estreia_e_chave_e_deixa_o_vice_quieto()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);

        await new PadelimetroService(ctx).AplicarCampanhaAsync(cenario.Categoria.Id);

        Assert.All(cenario.Campeoes, j => Assert.Equal(610, j.Padelimetro));
        Assert.All(cenario.Vices, j => Assert.Equal(600, j.Padelimetro));
        Assert.All(cenario.CairamNaEstreia, j => Assert.Equal(595, j.Padelimetro));
        Assert.All(cenario.FicaramNaChave, j => Assert.Equal(590, j.Padelimetro));

        // Campanha não é jogo: não conta pro K nem pra contagem de jogos.
        Assert.All(ctx.Jogadores, j => Assert.Equal(12, j.JogosDePadelimetro));

        // Extrato: linha de campanha tem categoria e NÃO tem partida — 2 do campeão,
        // 4 da estreia, 4 da chave; vice sem linha.
        var linhas = ctx.HistoricosDePadelimetro.Where(h => h.CategoriaId != null).ToList();
        Assert.Equal(10, linhas.Count);
        Assert.All(linhas, h => Assert.Null(h.PartidaId));
        Assert.All(linhas, h => Assert.Equal(cenario.Categoria.Id, h.CategoriaId));
        Assert.DoesNotContain(linhas, h => cenario.Vices.Any(v => v.Id == h.JogadorId));
    }

    [Fact]
    public async Task Aplicar_duas_vezes_nao_dobra_a_campanha()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);
        var service = new PadelimetroService(ctx);

        await service.AplicarCampanhaAsync(cenario.Categoria.Id);
        await service.AplicarCampanhaAsync(cenario.Categoria.Id);

        Assert.All(cenario.Campeoes, j => Assert.Equal(610, j.Padelimetro));
        Assert.Equal(10, ctx.HistoricosDePadelimetro.Count(h => h.CategoriaId != null));
    }

    [Fact]
    public async Task Quem_so_levou_wo_nao_entrou_em_quadra_e_nao_leva_ajuste()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);

        // O único jogo da dupla F vira W.O.: eles se inscreveram e não jogaram nada.
        var deF = ctx.Partidas.Single(p => p.Codigo == "J02");
        deF.MotivoDoEncerramento = EncerramentoPorWo.Motivo;
        ctx.SaveChanges();

        await new PadelimetroService(ctx).AplicarCampanhaAsync(cenario.Categoria.Id);

        // E (jogou) leva a pena; F (só W.O.) fica intocada.
        Assert.All(cenario.FicaramNaChave.Take(2), j => Assert.Equal(590, j.Padelimetro));
        Assert.All(cenario.FicaramNaChave.Skip(2), j => Assert.Equal(600, j.Padelimetro));
        Assert.Equal(8, ctx.HistoricosDePadelimetro.Count(h => h.CategoriaId != null));
    }

    [Fact]
    public async Task As_penas_do_servico_param_na_linha_de_descida()
    {
        using var ctx = TestInfra.NovoContexto();
        // Todo mundo a 505 — a 5 pontos da porta de descida da 4ª (500).
        var cenario = MontarCategoriaJogada(ctx, nivelInicial: 505);

        await new PadelimetroService(ctx).AplicarCampanhaAsync(cenario.Categoria.Id);

        Assert.All(cenario.FicaramNaChave, j => Assert.Equal(500, j.Padelimetro));
        Assert.All(cenario.CairamNaEstreia, j => Assert.Equal(500, j.Padelimetro));
        Assert.All(cenario.Campeoes, j => Assert.Equal(515, j.Padelimetro));
    }

    [Fact]
    public async Task Restrito_times_e_cancelado_nao_tem_campanha()
    {
        using var ctx1 = TestInfra.NovoContexto();
        var restrito = MontarCategoriaJogada(ctx1, restrito: true);
        await new PadelimetroService(ctx1).AplicarCampanhaAsync(restrito.Categoria.Id);
        Assert.Empty(ctx1.HistoricosDePadelimetro);
        Assert.All(ctx1.Jogadores, j => Assert.Equal(600, j.Padelimetro));

        using var ctx2 = TestInfra.NovoContexto();
        var deTimes = MontarCategoriaJogada(ctx2, deTimes: true);
        await new PadelimetroService(ctx2).AplicarCampanhaAsync(deTimes.Categoria.Id);
        Assert.Empty(ctx2.HistoricosDePadelimetro);

        using var ctx3 = TestInfra.NovoContexto();
        var cancelado = MontarCategoriaJogada(ctx3, statusDoTorneio: CancelamentoDoTorneio.Status);
        await new PadelimetroService(ctx3).AplicarCampanhaAsync(cancelado.Categoria.Id);
        Assert.Empty(ctx3.HistoricosDePadelimetro);
    }

    [Fact]
    public async Task Mista_nao_tem_campanha_mesmo_com_final_jogada()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx, nomeCategoria: "Mista B");

        await new PadelimetroService(ctx).AplicarCampanhaAsync(cenario.Categoria.Id);

        Assert.Empty(ctx.HistoricosDePadelimetro);
        Assert.All(ctx.Jogadores, j => Assert.Equal(600, j.Padelimetro));
    }

    [Fact]
    public async Task Encerrar_a_final_pelo_caminho_unico_aplica_jogo_e_campanha_e_nao_dobra()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx, statusDoTorneio: "Fase de Grupos",
            campeaoJaCarimbado: false);

        // O caminho real: a final acabou de ser finalizada e o encerramento único roda —
        // Padelímetro do jogo, coroação e campanha, tudo dele.
        var encerramento = TestInfra.NovoEncerramento(ctx);
        await encerramento.AplicarAsync(cenario.Final, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso(null, null));

        // Campeões: +12 do jogo (K20 × 1,2 do 6x4 × 0,5) e +10 da campanha.
        Assert.All(cenario.Campeoes, j => Assert.Equal(622, j.Padelimetro));
        // Vices: −12 do jogo, campanha não mexe.
        Assert.All(cenario.Vices, j => Assert.Equal(588, j.Padelimetro));
        Assert.All(cenario.CairamNaEstreia, j => Assert.Equal(595, j.Padelimetro));
        Assert.All(cenario.FicaramNaChave, j => Assert.Equal(590, j.Padelimetro));

        // O carimbo saiu do CoroarCampeao de verdade.
        Assert.Equal("Campeao", ctx.Duplas.Single(d => d.Id == cenario.Final.VencedorId).UltimaFase);

        // A fila offline reentrega o encerramento: nada dobra.
        await encerramento.AplicarAsync(cenario.Final, acabouDeTerminar: false,
            new EncerramentoDaPartida.LinksDoAviso(null, null));
        Assert.All(cenario.Campeoes, j => Assert.Equal(622, j.Padelimetro));
        Assert.Equal(10, ctx.HistoricosDePadelimetro.Count(h => h.CategoriaId != null));
    }

    [Fact]
    public async Task Replay_reaplica_a_campanha_no_mesmo_ponto_da_historia()
    {
        using var ctx = TestInfra.NovoContexto();
        // Ninguém tem nível: o replay semeia pelos jogos (entrada da 4ª = 600) e fecha a
        // campanha quando passa pela final.
        var cenario = MontarCategoriaJogada(ctx, nivelInicial: null);
        var service = new PadelimetroService(ctx);

        await service.RecalcularTudoAsync();

        // Quem ficou na chave e quem caiu na estreia levou a pena por cima do Elo.
        var linhasDeCampanha = ctx.HistoricosDePadelimetro.Where(h => h.CategoriaId != null).ToList();
        var idsComPena = linhasDeCampanha.Select(h => h.JogadorId).ToHashSet();
        Assert.All(cenario.FicaramNaChave, j => Assert.Contains(j.Id, idsComPena));
        Assert.All(cenario.CairamNaEstreia, j => Assert.Contains(j.Id, idsComPena));

        // Os campeões venceram 3 jogos em calibração (K40) e o Elo já os levou ACIMA da
        // linha de subida da 4ª (650) — a trava anti-farm segura o bônus. É o desenho:
        // o número deles já mandou os dois subirem.
        Assert.All(cenario.Campeoes, j => Assert.True(j.Padelimetro > 650));
        Assert.DoesNotContain(linhasDeCampanha, h => cenario.Campeoes.Any(c => c.Id == h.JogadorId));

        // A campanha é datada pela FINAL — senão o gráfico empilharia no dia do recálculo.
        Assert.All(linhasDeCampanha, h => Assert.Equal(cenario.Final.PlacarMarcadoEm, h.CriadoEm));

        // Rodar de novo dá EXATAMENTE no mesmo lugar (replay determinístico).
        var antes = ctx.Jogadores.ToDictionary(j => j.Id, j => j.Padelimetro);
        await service.RecalcularTudoAsync();
        Assert.All(ctx.Jogadores, j => Assert.Equal(antes[j.Id], j.Padelimetro));
    }

    [Fact]
    public async Task Reabrir_a_final_desfaz_a_campanha_junto_com_o_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx, statusDoTorneio: "Finalizado");

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000001" };
        ctx.Jogadores.Add(org);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = cenario.Torneio.Id, JogadorId = org.Id });
        ctx.SaveChanges();

        // O caminho real da final: primeiro o jogo move os 4, depois a campanha fecha.
        var service = new PadelimetroService(ctx);
        await service.AplicarAsync(cenario.Final.Id);
        await service.AplicarCampanhaAsync(cenario.Categoria.Id);
        Assert.All(cenario.Campeoes, j => Assert.Equal(622, j.Padelimetro)); // 600 +12 +10

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ReabrirPartida(cenario.Final.Id);

        // Tudo desandou na ordem inversa: campanha fora, jogo fora, nível e jogos de volta.
        Assert.All(cenario.Campeoes.Concat(cenario.Vices), j => Assert.Equal(600, j.Padelimetro));
        Assert.All(cenario.Campeoes, j => Assert.Equal(12, j.JogosDePadelimetro));
        Assert.All(cenario.CairamNaEstreia, j => Assert.Equal(600, j.Padelimetro));
        Assert.All(cenario.FicaramNaChave, j => Assert.Equal(600, j.Padelimetro));
        Assert.Empty(ctx.HistoricosDePadelimetro);

        // E, refeita a final, a campanha nasce de novo — o extrato limpo abriu a porta.
        // ...refazendo à mão o que o FinalizarPartida faria: placar, carimbo do vice e coroação.
        cenario.Final.Status = "Finalizada";
        cenario.Final.VencedorId = cenario.Final.Dupla1Id; // o reabrir tinha anulado
        ctx.Duplas.Single(d => d.Id == cenario.Final.Dupla1Id).UltimaFase = "Campeao";
        ctx.Duplas.Single(d => d.Id == cenario.Final.Dupla2Id).UltimaFase = "Final";
        ctx.SaveChanges();
        await service.AplicarCampanhaAsync(cenario.Categoria.Id);
        Assert.Equal(10, ctx.HistoricosDePadelimetro.Count(h => h.CategoriaId != null));
    }

    [Fact]
    public async Task Reabrir_a_final_preserva_o_que_veio_depois_da_campanha()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000002" };
        ctx.Jogadores.Add(org);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = cenario.Torneio.Id, JogadorId = org.Id });
        ctx.SaveChanges();

        var service = new PadelimetroService(ctx);
        await service.AplicarAsync(cenario.Final.Id);
        await service.AplicarCampanhaAsync(cenario.Categoria.Id);

        // Quem ficou na chave (600 − 10 = 590) joga a MISTA da noite e ganha +12: o
        // mesmo número, movido por outra categoria DEPOIS da campanha.
        var jogador = cenario.FicaramNaChave[0];
        Assert.Equal(590, jogador.Padelimetro);
        jogador.Padelimetro = 602;
        ctx.HistoricosDePadelimetro.Add(new HistoricoDePadelimetro
        {
            JogadorId = jogador.Id,
            PartidaId = 9099,
            NivelAntes = 590,
            Delta = 12,
            Motivo = "Vitória 6x2 na Mista",
            CriadoEm = cenario.Final.PlacarMarcadoEm!.Value.AddHours(3),
        });
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ReabrirPartida(cenario.Final.Id);

        // Desfazer a campanha devolve SÓ a pena (−10 de volta): a mista fica de pé.
        // Restaurar o nível absoluto de antes apagaria o +12 junto.
        Assert.Equal(612, jogador.Padelimetro);
    }

    [Fact]
    public async Task Jogador_em_duas_duplas_leva_a_melhor_campanha()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);

        // Dado torto: um jogador da dupla F ("Grupos", Id menor) também aparece numa
        // dupla nova que caiu na estreia ("Semifinal", Id maior). A melhor campanha —
        // a estreia, −5 — é a que vale; por Id, a pior venceria.
        var jogadorRepetido = cenario.FicaramNaChave[2];
        var duplaNova = new Dupla
        {
            CategoriaId = cenario.Categoria.Id,
            Jogador1Id = jogadorRepetido.Id,
            Jogador2Id = cenario.CairamNaEstreia[0].Id,
            UltimaFase = "Semifinal",
        };
        ctx.Duplas.Add(duplaNova);
        ctx.SaveChanges();
        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cenario.Categoria.Id,
            TorneioId = cenario.Torneio.Id,
            Dupla1Id = duplaNova.Id,
            Dupla2Id = ctx.Duplas.Single(d => d.UltimaFase == "Campeao").Id,
            Codigo = "J99",
            Status = "Finalizada",
            Fase = "Semifinal",
            GamesDupla1 = 2,
            GamesDupla2 = 6,
            SetsDupla1 = 0,
            SetsDupla2 = 1,
            VencedorId = ctx.Duplas.Single(d => d.UltimaFase == "Campeao").Id,
            PlacarMarcadoEm = new DateTime(2026, 8, 15, 8, 0, 0),
        });
        ctx.SaveChanges();

        await new PadelimetroService(ctx).AplicarCampanhaAsync(cenario.Categoria.Id);

        var linha = ctx.HistoricosDePadelimetro
            .Single(h => h.CategoriaId != null && h.JogadorId == jogadorRepetido.Id);
        Assert.Equal(-5, linha.Delta);
        Assert.Equal(595, jogadorRepetido.Padelimetro);
    }

    [Fact]
    public async Task Corrigir_o_placar_da_final_leva_a_campanha_pro_campeao_certo()
    {
        using var ctx = TestInfra.NovoContexto();
        var cenario = MontarCategoriaJogada(ctx);

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000003" };
        ctx.Jogadores.Add(org);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = cenario.Torneio.Id, JogadorId = org.Id });
        ctx.SaveChanges();

        var service = new PadelimetroService(ctx);
        await service.AplicarAsync(cenario.Final.Id);
        await service.AplicarCampanhaAsync(cenario.Categoria.Id);
        Assert.All(cenario.Campeoes, j => Assert.Equal(622, j.Padelimetro)); // 600 +12 +10

        // O placar da final estava AO CONTRÁRIO: quem venceu foi a dupla B, 6x4.
        // A correção troca o vencedor, o encerramento re-coroa — e a campanha precisa
        // migrar junto, senão o EX-campeão fica com o "+10" no extrato.
        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ControlePlacar(cenario.Final.Id, "Finalizada", 4, 6, null, null);

        // Os carimbos trocaram de dono...
        Assert.Equal("Campeao", ctx.Duplas.Single(d => d.Id == cenario.Final.Dupla2Id).UltimaFase);
        Assert.Equal("Final", ctx.Duplas.Single(d => d.Id == cenario.Final.Dupla1Id).UltimaFase);

        // ...e a campanha foi desfeita (subtração de delta) e reaplicada com eles:
        // A perde o bônus e fica só com o +12 do jogo invertido (que o extrato não
        // reaplica — é a regra de sempre, o replay acerta); B ganha o bônus.
        Assert.All(cenario.Campeoes, j => Assert.Equal(612, j.Padelimetro));
        Assert.All(cenario.Vices, j => Assert.Equal(598, j.Padelimetro)); // 588 + 10
        Assert.All(cenario.CairamNaEstreia, j => Assert.Equal(595, j.Padelimetro));
        Assert.All(cenario.FicaramNaChave, j => Assert.Equal(590, j.Padelimetro));

        var linhasDeCampeao = ctx.HistoricosDePadelimetro
            .Where(h => h.CategoriaId != null && h.Motivo.Contains("Campeão"))
            .Select(h => h.JogadorId).ToList();
        Assert.All(cenario.Vices, j => Assert.Contains(j.Id, linhasDeCampeao));
        Assert.All(cenario.Campeoes, j => Assert.DoesNotContain(j.Id, linhasDeCampeao));
    }

    [Fact]
    public async Task Linha_de_campanha_nao_vira_jogo_no_movimento_da_aba()
    {
        using var ctx = TestInfra.NovoContexto();
        var corte = new DateTime(2026, 8, 10);

        // Zulmira: 610 hoje = 600 antes do corte + campanha de +10 depois dele, com 12
        // jogos TODOS de antes. Ana: 600 com 11 jogos, parada. Antes do corte a ordem era
        // Zulmira (600, 12 jogos) na frente de Ana (600, 11) pelo desempate de jogos — e
        // se a linha de campanha contasse como jogo, o "antes" da Zulmira viraria 11,
        // empataria e o nome entregaria a ponta pra Ana, inventando movimento nos dois.
        var zulmira = new Jogador { Nome = "Zulmira", Cpf = "66600000001", Padelimetro = 610, JogosDePadelimetro = 12 };
        var ana = new Jogador { Nome = "Ana", Cpf = "66600000002", Padelimetro = 600, JogosDePadelimetro = 11 };
        ctx.Jogadores.AddRange(zulmira, ana);
        ctx.SaveChanges();

        ctx.HistoricosDePadelimetro.AddRange(
            new HistoricoDePadelimetro { JogadorId = zulmira.Id, PartidaId = 9001, NivelAntes = 590, Delta = 10, Motivo = "Vitória", CriadoEm = corte.AddDays(-1) },
            new HistoricoDePadelimetro { JogadorId = ana.Id, PartidaId = 9002, NivelAntes = 590, Delta = 10, Motivo = "Vitória", CriadoEm = corte.AddDays(-1) },
            new HistoricoDePadelimetro { JogadorId = zulmira.Id, PartidaId = null, CategoriaId = 501, NivelAntes = 600, Delta = 10, Motivo = "Campeã da 4ª — bônus de campanha", CriadoEm = corte.AddDays(2) });
        ctx.SaveChanges();

        var linhas = new List<PadelimetroLinhaVM>
        {
            new() { Jogador = zulmira, Pdz = 610, Jogos = 12 },
            new() { Jogador = ana, Pdz = 600, Jogos = 11 },
        };
        await new PadelimetroService(ctx).AplicarMovimentoAsync(linhas, corte);

        Assert.Equal(0, linhas[0].Movimento);
        Assert.Equal(0, linhas[1].Movimento);
    }
}
