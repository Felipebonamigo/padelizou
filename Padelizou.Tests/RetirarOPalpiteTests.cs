using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — RETIRAR O PALPITE. 🗣️ Felipe: *"tambem permita retirar o palpite colocado"*.
//
// 🕳️ Dava pra TROCAR de dupla e pra trocar a ficha de placar, mas não pra sair: uma vez tocado
// o nome, aquele palpite ficava na barra e no ranking pra sempre. Quem tocou sem querer — e o
// alvo tem 48px, no meio de uma lista de 97 jogos — não tinha caminho de volta.
//
// A régua de QUANDO é a mesma do palpitar, e por isso não precisa ser inventada: 🗣️ *"todo
// jogo pode ser palpitado até começar"*. Começou, o palpite está valendo — retirar ali seria
// desistir da aposta vendo o primeiro game.
public class RetirarOPalpiteTests
{
    [Fact]
    public async Task Retirar_apaga_SO_o_meu_palpite()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var eu = await NovoTorcedorAsync(ctx, "Eu", "55550000001");
        var outro = await NovoTorcedorAsync(ctx, "Outro", "55550000002");

        await servico.RegistrarVotoAsync(partida.Id, eu.Id, duplas[0].Id, 6, 4);
        await servico.RegistrarVotoAsync(partida.Id, outro.Id, duplas[0].Id);

        var resumo = await servico.RetirarPalpiteAsync(partida.Id, eu.Id);

        // ⚠️ A CHECAGEM DE DONO É ESTRUTURAL: o serviço só acha a linha por (partida, jogador),
        // e o jogador vem da claim. Não há como pedir a retirada do palpite de outra pessoa.
        Assert.Null(resumo.MeuVotoDuplaId);
        Assert.Equal(1, resumo.TotalVotos);
        Assert.Equal(outro.Id, ctx.PalpitesPartida.Single().JogadorId);
    }

    [Fact]
    public async Task Retirar_leva_o_PLACAR_junto_e_a_galera_para_de_cravar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var eu = await NovoTorcedorAsync(ctx, "Eu", "55550000003");
        await servico.RegistrarVotoAsync(partida.Id, eu.Id, duplas[0].Id, 6, 4);

        var resumo = await servico.RetirarPalpiteAsync(partida.Id, eu.Id);

        // O placar mora na MESMA linha do voto — some junto, senão a frase "a galera crava 6x4"
        // continuaria contando um palpite que não existe mais.
        Assert.False(resumo.PalpiteiOPlacar);
        Assert.Equal(0, resumo.PalpitesComPlacar);
        Assert.False(resumo.TemPlacarMaisPalpitado);
    }

    [Fact]
    public async Task Depois_que_o_jogo_COMECOU_nao_da_mais_pra_retirar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var eu = await NovoTorcedorAsync(ctx, "Eu", "55550000004");
        await servico.RegistrarVotoAsync(partida.Id, eu.Id, duplas[0].Id);

        partida.Status = "AoVivo";
        await ctx.SaveChangesAsync();

        // Mesma frase e mesma régua do palpitar: quem entrou em quadra fechou a aposta.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RetirarPalpiteAsync(partida.Id, eu.Id));

        Assert.Single(ctx.PalpitesPartida.ToList());
    }

    [Fact]
    public async Task Retirar_sem_ter_palpitado_nao_estoura()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var ninguem = await NovoTorcedorAsync(ctx, "Nunca Palpitou", "55550000005");

        // ⚠️ IDEMPOTENTE de propósito: o toque duplo no "retirar" manda dois POSTs, e o segundo
        // chega quando a linha já não existe. Estourar ali viraria um alerta vermelho na cara de
        // quem conseguiu exatamente o que queria — é a mesma lição da corrida do clique duplo
        // no votar (PalpiteEmDobroTests).
        var resumo = await servico.RetirarPalpiteAsync(partida.Id, ninguem.Id);

        Assert.Null(resumo.MeuVotoDuplaId);
        Assert.Equal(0, resumo.TotalVotos);
    }

    // ─────────────────────────── O QUE SÓ EXISTE NA TELA ───────────────────────────

    [Fact]
    public void O_botao_de_retirar_existe_nas_duas_apresentacoes_e_so_pra_quem_palpitou()
    {
        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_Palpitometro.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);

            var inicio = fonte.IndexOf("retirarPalpite(", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei o botão de retirar o palpite em {arquivo}.");

            // A marca que o JS usa pra esconder o botão sozinho depois da retirada — sem ela,
            // o "retirar" ficaria na tela oferecendo desfazer o que já foi desfeito.
            Assert.Contains("pdz-retirar-palpite", fonte);
        }
    }

    [Fact]
    public void A_tela_esconde_o_retirar_de_quem_nao_tem_mais_palpite()
    {
        var js = Ler("wwwroot", "js", "palpitometro.js");

        Assert.Contains("pdz-retirar-palpite", js);
        Assert.Contains("/Partidas/RetirarPalpite", js);
    }

    // ─────────────────────────── INFRA ───────────────────────────

    private static async Task<(Partida partida, List<Dupla> duplas)> MontarJogoAgendadoAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = 6;
        torneio.SetsFaseGrupos = 1;

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
