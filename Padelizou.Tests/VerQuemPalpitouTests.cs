using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — VER QUEM PALPITOU **E O QUE** CADA UM PALPITOU. 🗣️ Felipe, num print da lista de
// jogos do 2ª Etapa ER PADEL TOUR, com a frase "A galera crava 9 x 7 (1 de 3)" marcada:
// *"tambem permita clicar e ver quem colocou o palpitometro e qual o placar"*.
//
// 🕳️ O modal "quem votou em quem" já existia desde sempre — e mostrava só o NOME. O placar, que
// é a metade interessante ("quem foi que cravou 9x7?"), morria no banco: a barra dizia quantos,
// a frase dizia o consenso, e não havia tela nenhuma que ligasse um palpite a uma pessoa.
public class VerQuemPalpitouTests
{
    // ─────────────────────────── O QUE O SERVIÇO DEVOLVE ───────────────────────────

    [Fact]
    public async Task O_modal_diz_QUAL_PLACAR_cada_um_palpitou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var cravou = await NovoTorcedorAsync(ctx, "Cravou o Placar", "55540000001");
        var soOVencedor = await NovoTorcedorAsync(ctx, "So o Vencedor", "55540000002");

        await servico.RegistrarVotoAsync(partida.Id, cravou.Id, duplas[0].Id, 6, 4);
        await servico.RegistrarVotoAsync(partida.Id, soOVencedor.Id, duplas[0].Id);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        var comPlacar = votantes.VotantesDupla1.Single(v => v.Nome == "Cravou o Placar");
        Assert.Equal(6, comPlacar.PlacarVencedor);
        Assert.Equal(4, comPlacar.PlacarPerdedor);

        // ⚠️ Palpitar o placar é OPCIONAL e continua sendo — quem só disse quem vence aparece
        // sem placar nenhum, e não com um "0 x 0" inventado.
        var semPlacar = votantes.VotantesDupla1.Single(v => v.Nome == "So o Vencedor");
        Assert.Null(semPlacar.PlacarVencedor);
        Assert.Null(semPlacar.PlacarPerdedor);
    }

    [Fact]
    public async Task O_placar_de_quem_votou_na_DUPLA_2_nao_sai_invertido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor da Dois", "55540000003");

        // ⚠️ No BANCO o placar mora na orientação do JOGO (lado 1 = Dupla1), então quem aposta
        // "6 x 4 pra Dupla 2" grava 4 x 6. Na tela ele tem que ler 6 x 4: o modal lista a pessoa
        // DEBAIXO da dupla em que ela votou, e ali "4 x 6" diria que ela apostou na derrota de
        // quem escolheu. É a mesma orientação da ficha que ela tocou.
        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[1].Id, 4, 6);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        var linha = Assert.Single(votantes.VotantesDupla2);
        Assert.Equal(6, linha.PlacarVencedor);
        Assert.Equal(4, linha.PlacarPerdedor);
    }

    [Fact]
    public async Task Em_jogo_de_dois_SETS_o_modal_diz_que_o_palpite_e_em_sets()
    {
        using var ctx = TestInfra.NovoContexto();
        // Sets = 2 é o "melhor de 3" do formato (ver PlacaresPossiveis): 2x0 e 2x1.
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx, setsDosGrupos: 2);
        var servico = new PalpiteService(ctx);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor de Sets", "55540000004");
        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 2, 0);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        // "2 x 0" sem a moeda ao lado é um placar de games que nenhum jogo termina.
        var linha = Assert.Single(votantes.VotantesDupla1);
        Assert.Equal(2, linha.PlacarVencedor);
        Assert.True(linha.PlacarEmSets);
    }

    // ─────────────────────────── O QUE SÓ EXISTE NA TELA ───────────────────────────
    //
    // ⚠️ Teste de FONTE: a suíte não renderiza Razor nem executa o modal. O que se trava aqui é
    // que os pontos de clique existem e que o JS desenha o placar — o resto é comportamento e
    // está travado acima.

    [Fact]
    public void A_frase_do_consenso_ABRE_o_modal_nas_duas_apresentacoes()
    {
        // 🗣️ O print do Felipe marcou exatamente esta frase. Ela é o lugar mais natural pra
        // perguntar "quem cravou?", e até agora era texto morto.
        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_Palpitrometro.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);

            var inicio = fonte.IndexOf("pdz-palpite-consenso", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei a frase do consenso em {arquivo}.");

            var trecho = fonte[inicio..Math.Min(fonte.Length, inicio + 900)];
            Assert.Contains("verVotos(", trecho);
        }
    }

    [Fact]
    public void O_modal_desenha_o_placar_de_cada_votante()
    {
        var js = Ler("wwwroot", "js", "palpitrometro.js");

        // Os nomes vêm do JSON do /Partidas/VerVotos, em camelCase.
        Assert.Contains("placarVencedor", js);
        Assert.Contains("placarPerdedor", js);
    }

    // ─────────────────────────── INFRA ───────────────────────────

    private static async Task<(Partida partida, List<Dupla> duplas)> MontarJogoAgendadoAsync(
        DbPadelContext ctx, int gamesDosGrupos = 6, int setsDosGrupos = 1)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = gamesDosGrupos;
        torneio.SetsFaseGrupos = setsDosGrupos;

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
        await ctx.SaveChangesAsync();

        return (partida, duplas);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static string Ler(params string[] caminho) =>
        File.ReadAllText(Path.Combine(new[] { PastaDoProjeto() }.Concat(caminho).ToArray()));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
