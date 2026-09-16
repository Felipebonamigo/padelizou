using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O BOTÃO DA ARTE PRO STORY, DENTRO DO TORNEIO (14/09/2026).
//
// ⚠️ AS AÇÕES MORAM NO `TorneiosController`, E NÃO NO `CartoesController` — e não é arrumação
// de arquivo, é a régua de autorização. Quem pode gerar a arte é organizador OU marcador, e
// isso é `PodeOperarODiaDeJogoAsync`, que é PRIVADO do TorneiosController. Copiar a régua pro
// controller dos cards criaria o QUARTO lugar de uma checagem que o CLAUDE.md já diz que
// precisa andar junto em três — e uma dessincronia dessas quebrou a Mesa de Controle em 31/07.
// Precedente exato: o CartaoDoPlacarAoVivo mora no TorneiosController.Placar.cs pelo mesmo
// motivo.
//
// ⚠️ E A ARTE É DA FAMÍLIA FECHADA (ver o cabeçalho do CartoesController): ela imprime o @ de
// gente que não escolheu aparecer, então exige login, não declara `og:image` e responde com
// `Cache-Control: private`.
public class ArteDoJogoNoTorneioTests
{
    // ── O @ do organizador, campo novo do torneio ────────────────────────────────────────

    [Fact]
    public async Task O_organizador_salva_o_arroba_do_clube_e_ele_fica_guardado_sem_o_arroba()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");

        await EditarComInstagramAsync(ctx, torneio, organizador.Id, "@ER.Padel");

        Assert.Equal("er.padel", (await ctx.Torneios.FindAsync(torneio.Id))!.InstagramDoOrganizador);
    }

    [Fact]
    public async Task Campo_apagado_tira_o_arroba_da_arte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.InstagramDoOrganizador = "er.padel";
        await ctx.SaveChangesAsync();

        await EditarComInstagramAsync(ctx, torneio, organizador.Id, "");

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.InstagramDoOrganizador);
    }

    // ⚠️ Recusa em vez de apagar em silêncio. Sem isto, um erro de digitação ("er padel", com
    // espaço) normalizaria pra nulo e APAGARIA o @ que estava certo — e a tela não diria nada.
    // Mesma regra do vizinho de formulário, o link das fotos.
    [Fact]
    public async Task Arroba_invalido_e_recusado_e_o_que_estava_gravado_fica()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.InstagramDoOrganizador = "er.padel";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await EditarComInstagramAsync(ctx, torneio, organizador.Id, "er padel", controller);

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("er.padel", (await ctx.Torneios.FindAsync(torneio.Id))!.InstagramDoOrganizador);
    }

    // O circuito que repete toda etapa tem o MESMO clube e o mesmo organizador: o @ é estrutura,
    // não coisa da edição que passou. (Um teste de reflexão já recusa propriedade nova do
    // Torneio que não escolheu lado — este diz QUAL lado, e por quê.)
    [Fact]
    public void O_arroba_do_organizador_viaja_pra_edicao_nova_do_torneio()
    {
        Assert.Contains(nameof(Torneio.InstagramDoOrganizador), DuplicacaoDeTorneio.Copiadas);
    }

    // ── Quem pode gerar a arte ───────────────────────────────────────────────────────────

    [Fact]
    public async Task O_organizador_ve_a_lista_de_jogos_pra_gerar_arte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var r = await TestInfra.NovoTorneiosController(ctx, organizador.Id).ArtesDosJogos(torneio.Id, Fontes());

        var view = Assert.IsType<ViewResult>(r);
        var jogos = Assert.IsAssignableFrom<IReadOnlyList<Partida>>(view.Model);
        Assert.Single(jogos);
    }

    // O marcador é quem está na quadra com o celular na hora da foto — é o caso de uso, não
    // uma concessão.
    [Fact]
    public async Task O_marcador_do_torneio_tambem_gera_a_arte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var marcador = new Jogador { Nome = "Marcador da Mesa", Cpf = "11122233344" };
        ctx.Jogadores.Add(marcador);
        await ctx.SaveChangesAsync();
        ctx.TorneioMarcadores.Add(new TorneioMarcador { TorneioId = torneio.Id, JogadorId = marcador.Id });
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, marcador.Id).ArtesDosJogos(torneio.Id, Fontes());

        Assert.IsType<ViewResult>(r);
    }

    [Fact]
    public async Task Quem_nao_organiza_nem_marca_nao_ve_a_lista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var estranho = new Jogador { Nome = "Curioso", Cpf = "55566677788" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, estranho.Id).ArtesDosJogos(torneio.Id, Fontes());

        Assert.IsType<ForbidResult>(r);
    }

    [Fact]
    public async Task A_imagem_da_arte_tambem_e_fechada_pra_quem_nao_opera_o_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        var partida = await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var estranho = new Jogador { Nome = "Curioso", Cpf = "55566677788" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .ArteDoJogoImagem(partida.Id, Fontes());

        Assert.IsType<ForbidResult>(r);
    }

    [Fact]
    public async Task Quem_nao_opera_o_torneio_nao_gera_a_arte_nem_mandando_a_foto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        var partida = await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var estranho = new Jogador { Nome = "Curioso", Cpf = "55566677788" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .ArteDoJogoImagem(partida.Id, foto: null, Fontes());

        Assert.IsType<ForbidResult>(r);
    }

    // ── A arte que sai ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_arte_sai_em_PNG_e_o_cache_e_PRIVADO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        var partida = await JogoAsync(ctx, torneio, categoria, "Semifinal");

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var r = await controller.ArteDoJogoImagem(partida.Id, Fontes());

        var arquivo = Assert.IsType<FileContentResult>(r);
        Assert.Equal("image/png", arquivo.ContentType);
        Assert.True(arquivo.FileContents.Length > 0);

        // ⚠️ `private`, não `public`. A arte carrega o @ dos quatro jogadores: resposta
        // pública autoriza qualquer cache no caminho a devolvê-la a OUTRA pessoa.
        Assert.Contains("private", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task O_arroba_do_torneio_chega_na_arte_e_o_dos_jogadores_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Mata-Mata");
        torneio.InstagramDoOrganizador = "er.padel";
        torneio.Nome = "ER PADEL TOUR";
        await ctx.SaveChangesAsync();

        var d1 = await DuplaAsync(ctx, categoria, ("Felipe Bonamigo", "felipebonamigo"), ("Guilherme Bagesteiro", "guilhermebagesteiro"));
        var d2 = await DuplaAsync(ctx, categoria, ("Lucas Foka", "fokalucas"), ("Alexandre Costa", "xandicosta.13"));
        var partida = await JogoAsync(ctx, torneio, categoria, "Semifinal", d1, d2, numeroNaFase: 2);

        var jogo = await JogosParaArte.DoJogoAsync(ctx, partida.Id);

        Assert.NotNull(jogo);
        Assert.Equal("@er.padel", jogo!.ArrobaDoOrganizador);
        Assert.Equal("Semifinal 2", jogo.Fase);
        Assert.Equal("ER PADEL TOUR", jogo.Torneio);
        Assert.Equal(new[] { "@felipebonamigo", "@guilhermebagesteiro" }, jogo.Dupla1.Marcacoes.Select(m => m.Texto));
        Assert.Equal(new[] { "@fokalucas", "@xandicosta.13" }, jogo.Dupla2.Marcacoes.Select(m => m.Texto));
    }

    [Fact]
    public async Task Torneio_sem_arroba_cadastrado_gera_arte_sem_a_linha_do_organizador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Mata-Mata");
        var partida = await JogoAsync(ctx, torneio, categoria, "Final");

        var jogo = await JogosParaArte.DoJogoAsync(ctx, partida.Id);

        Assert.NotNull(jogo);
        Assert.Null(jogo!.ArrobaDoOrganizador);
    }

    // ── A ligação com as telas ───────────────────────────────────────────────────────────

    [Fact]
    public void As_ferramentas_do_organizador_levam_pra_lista_de_artes()
    {
        var tela = TestInfra.SemComentarios(File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_FerramentasDoOrganizador.cshtml")));

        Assert.Contains("ArtesDosJogos", tela);
    }

    [Fact]
    public void O_campo_do_arroba_aparece_so_no_formulario_da_gestao()
    {
        var tela = TestInfra.SemComentarios(File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml")));

        Assert.Contains("instagramDoOrganizador", tela);
        Assert.Single(Regex.Matches(tela, @"Model\.InstagramDoOrganizador"));
    }

    // ── Bastidores ───────────────────────────────────────────────────────────────────────

    private static FonteDoCartao Fontes() => new(PastaDasFontes());

    private static Task<IActionResult> EditarComInstagramAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, string? arroba,
        TorneiosController? controller = null)
        => (controller ?? TestInfra.NovoTorneiosController(ctx, organizadorId)).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            instagramDoOrganizador: arroba);

    private static async Task<Dupla> DuplaAsync(
        DbPadelContext ctx, Categoria categoria,
        (string Nome, string? Arroba) um, (string Nome, string? Arroba) outro)
    {
        // ⚠️ `SenhaHash` é o que diz "esta pessoa tem conta": sem ele o Jogador é PRÉ-CADASTRO
        // (Jogador.EhPreCadastro), e a régua da arte não marca o @ de quem nunca entrou.
        var j1 = new Jogador { Nome = um.Nome, Cpf = Guid.NewGuid().ToString("N")[..11], Instagram = um.Arroba, SenhaHash = "hash-de-teste" };
        var j2 = new Jogador { Nome = outro.Nome, Cpf = Guid.NewGuid().ToString("N")[..11], Instagram = outro.Arroba, SenhaHash = "hash-de-teste" };
        ctx.Jogadores.AddRange(j1, j2);
        await ctx.SaveChangesAsync();

        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return dupla;
    }

    private static async Task<Partida> JogoAsync(
        DbPadelContext ctx, Torneio torneio, Categoria categoria, string fase,
        Dupla? d1 = null, Dupla? d2 = null, int? numeroNaFase = null)
    {
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToListAsync();
        d1 ??= duplas.ElementAtOrDefault(0);
        d2 ??= duplas.ElementAtOrDefault(1);
        Assert.NotNull(d1);
        Assert.NotNull(d2);

        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = $"{fase[..3]}{d1!.Id}x{d2!.Id}",
            Status = "Agendada",
            Fase = fase,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            NumeroNaFase = numeroNaFase,
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();
        return partida;
    }

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("wwwroot/fonts não encontrado a partir do bin.");
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            {
                return Path.Combine(dir.FullName, "Padelizou");
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("A pasta do projeto não foi encontrada a partir do bin.");
    }
}
