using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — CLICAR NO NOME NA TABELA DE PALPITEIROS ABRE O QUE AQUELA PESSOA PALPITOU.
// 🗣️ Felipe, com o print da aba no celular: *"ai clicar no nome, permita ver os resultados q a
// pessoa colocou mas de um modo que nao quebre a tela"*.
//
// A conta está travada em PalpitesDoPalpiteiroTests e o desenho da lista em
// Padelizou.Tests/js/conferir-palpites-do-palpiteiro.js. O que sobra pra cá são as duas pontas
// que nenhum dos dois alcança: a PORTA (o endpoint que serve a lista, e quem ele recusa) e a
// LIGAÇÃO na tela (o nome virou botão no torneio, e continuou link do perfil no hub).
//
// ⚠️ POR QUE TESTE DE FONTE na segunda metade: a suíte não renderiza Razor. Um `@if` que some
// não tem como ser pego por teste de comportamento — e é justamente o tipo de coisa que some
// numa edição distraída da parcial, que serve TRÊS telas de dois controllers.
public class ClicarNoNomeAbreOsPalpitesTests
{
    // ─────────────────────────── A PORTA ───────────────────────────

    [Fact]
    public async Task O_endpoint_devolve_a_lista_daquela_pessoa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, partida, duplas) = await MontarAsync(ctx);

        var torcedor = new Jogador { Nome = "Torcedor Certeiro", Cpf = "55530000001" };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        await PalpitarAsync(ctx, partida.Id, torcedor.Id, duplas[0].Id, 6, 4);

        var resposta = await TestInfra.NovoTorneiosController(ctx, torcedor.Id)
            .PalpitesDoPalpiteiro(torneio.Id, torcedor.Id);

        var json = Assert.IsType<JsonResult>(resposta);
        var lista = Assert.IsType<PalpitesDoPalpiteiro>(json.Value);
        Assert.Equal(torcedor.Id, lista.JogadorId);
        Assert.Equal(PontosDoPalpite.Cravou, lista.Pontos);
        Assert.Single(lista.Linhas);
    }

    [Fact]
    public async Task Torneio_OCULTO_nao_entrega_o_palpite_de_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, partida, duplas) = await MontarAsync(ctx);

        var torcedor = new Jogador { Nome = "Torcedor Escondido", Cpf = "55530000002" };
        var curioso = new Jogador { Nome = "Curioso de Fora", Cpf = "55530000003" };
        ctx.Jogadores.AddRange(torcedor, curioso);
        await ctx.SaveChangesAsync();
        await PalpitarAsync(ctx, partida.Id, torcedor.Id, duplas[0].Id, 6, 4);

        torneio.Oculto = true;
        await ctx.SaveChangesAsync();

        // ⚠️ MESMA PORTA DO Details E DA PÁGINA DE PALPITEIROS. Deixar UMA porta de fora é como o
        // torneio escondido reaparece — aqui com o nome e o placar de quem palpitou nele.
        var resposta = await TestInfra.NovoTorneiosController(ctx, curioso.Id)
            .PalpitesDoPalpiteiro(torneio.Id, torcedor.Id);

        Assert.IsType<NotFoundResult>(resposta);
    }

    [Fact]
    public async Task Quem_nao_palpitou_neste_torneio_responde_404()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx);

        var estranho = new Jogador { Nome = "Nunca Palpitou", Cpf = "55530000004" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        // ⚠️ 404 e não um painel em branco: é o que o modal transforma na frase "esta pessoa não
        // tem palpite neste torneio", em vez de ficar em "Carregando..." pra sempre.
        var resposta = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .PalpitesDoPalpiteiro(torneio.Id, estranho.Id);

        Assert.IsType<NotFoundResult>(resposta);
    }

    // ─────────────────────────── A LIGAÇÃO NA TELA ───────────────────────────

    [Fact]
    public void No_TORNEIO_o_nome_e_um_BOTAO_que_abre_os_palpites()
    {
        var fonte = Parcial("_TabelaDePalpiteiros.cshtml");

        // O gate é o `TorneioId` do modelo, e não um interruptor de tela: é a pergunta "existe
        // um torneio aqui?" feita ao dado que a página já tem.
        var gate = fonte.IndexOf("@if (Model.TorneioId is int torneioId)", StringComparison.Ordinal);
        Assert.True(gate >= 0, "O nome clicável precisa estar atrás de um `Model.TorneioId is int`.");

        var chamada = fonte.IndexOf("verPalpitesDoPalpiteiro(", StringComparison.Ordinal);
        Assert.True(chamada > gate, "O botão que abre os palpites tem que estar DENTRO do gate do torneio.");

        // ⚠️ `<button>` e não `<a onclick>`: abrir o modal é ação, e ação em link leva quem
        // navega por teclado (e o leitor de tela) a um destino que não existe.
        Assert.Contains("<button type=\"button\"", fonte[gate..chamada]);
    }

    [Fact]
    public void No_HUB_o_nome_continua_indo_pro_PERFIL()
    {
        // ⚠️ Lá a tabela soma VÁRIOS torneios: não existe "os palpites dela neste torneio" pra
        // mostrar. O caminho antigo tem que sobreviver inteiro no `else`.
        var fonte = Parcial("_TabelaDePalpiteiros.cshtml");
        Assert.Contains("asp-controller=\"Jogadores\" asp-action=\"Perfil\"", fonte);

        var hub = Arquivo(Path.Combine("Views", "Jogadores", "Ranking.cshtml"));
        var chamada = hub.IndexOf("_TabelaDePalpiteiros", StringComparison.Ordinal);
        Assert.True(chamada >= 0, "O hub do Ranking precisa continuar usando a parcial da tabela.");
        Assert.DoesNotContain("TorneioId", hub[chamada..(chamada + 300)]);
    }

    [Fact]
    public void O_ranking_do_TORNEIO_passa_o_torneio_pra_tabela()
    {
        var fonte = Parcial("_RankingDePalpiteiros.cshtml");
        var chamada = fonte.IndexOf("_TabelaDePalpiteiros", StringComparison.Ordinal);
        Assert.True(chamada >= 0, "A parcial do ranking precisa continuar usando a tabela.");
        Assert.Contains("Model.TorneioId", fonte[chamada..]);
    }

    [Fact]
    public void O_modal_ROLA_POR_DENTRO_e_so_existe_onde_ha_torneio()
    {
        var tabela = Parcial("_TabelaDePalpiteiros.cshtml");
        var gate = tabela.IndexOf("@if (Model.TorneioId != null)", StringComparison.Ordinal);
        Assert.True(gate >= 0, "O modal precisa estar atrás de um gate pelo `TorneioId`.");
        Assert.Contains("_ModalPalpitesDoPalpiteiro", tabela[gate..]);

        // ⚠️ ESTE É O PEDIDO DO FELIPE — "de um modo que nao quebre a tela". Com 41 palpites,
        // uma lista que cresce dentro da tabela empurra a tabela inteira pra fora do celular;
        // `modal-dialog-scrollable` faz a lista rolar POR DENTRO, com o X sempre visível.
        var modal = Parcial("_ModalPalpitesDoPalpiteiro.cshtml");
        Assert.Contains("modal-dialog-scrollable", modal);

        // ⚠️ asp-append-version obrigatório: o Service Worker guarda script em cache, e sem a
        // versão o usuário fica preso no JS velho indefinidamente.
        Assert.Contains("~/js/palpites-do-palpiteiro.js\" asp-append-version=\"true\"", modal);

        // O caminho do perfil não some — ele desce pro rodapé do modal.
        Assert.Contains("pdzPalpitesPerfil", modal);
    }

    // ─────────────────────────── A INFRA DOS TESTES ───────────────────────────

    private static async Task<(Torneio torneio, Partida partida, List<Dupla> duplas)> MontarAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            VencedorId = duplas[0].Id,
            GamesDupla1 = 6,
            GamesDupla2 = 4,
            Status = "Finalizada",
            Fase = "Final",
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (torneio, partida, duplas);
    }

    private static async Task PalpitarAsync(DbPadelContext ctx, int partidaId, int jogadorId, int duplaId,
        int? games1, int? games2)
    {
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partidaId,
            JogadorId = jogadorId,
            DuplaEscolhidaId = duplaId,
            GamesDupla1 = games1,
            GamesDupla2 = games2,
        });
        await ctx.SaveChangesAsync();
    }

    private static string Parcial(string nome) => Arquivo(Path.Combine("Views", "Shared", nome));

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

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
