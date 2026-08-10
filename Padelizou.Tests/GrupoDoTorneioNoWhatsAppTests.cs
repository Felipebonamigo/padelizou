using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O GRUPO DO TORNEIO NO WHATSAPP (pedido do Felipe, 10/08/2026): o organizador cola o convite
// e quem está inscrito ganha um botão pra entrar.
//
// Os dois defeitos que estes testes existem pra impedir são os dois MUDOS — a tela continua
// abrindo nos dois casos:
//
//   1. o link vazando pra quem não está no torneio. A página do torneio abre SEM LOGIN, e
//      convite de grupo é chave de porta: quem tem, entra, e o organizador só descobre o
//      estranho depois de ele já estar lá dentro;
//   2. o botão do Padelizou apontando pra fora do WhatsApp — que é o que um campo de texto
//      sem conferência permitiria (inclusive "javascript:" rodando na sessão de quem clica).
public class GrupoDoTorneioNoWhatsAppTests
{
    // ---- O endereço ----

    [Fact]
    public void O_link_colado_do_whatsapp_passa_inteiro()
    {
        const string convite = "https://chat.whatsapp.com/H7kP2qXzYaB3cD4eF5";

        Assert.Null(GrupoDoTorneioNoWhatsApp.ProblemaCom(convite));
        Assert.Equal(convite, GrupoDoTorneioNoWhatsApp.Normalizar(convite));
    }

    [Fact]
    public void Digitado_sem_https_o_link_e_completado_em_vez_de_recusado()
    {
        // Colado do app o link vem inteiro, mas digitado à mão vem assim — e recusar isso
        // seria recusar um convite bom por causa de seis caracteres.
        Assert.Equal("https://chat.whatsapp.com/ABC123",
            GrupoDoTorneioNoWhatsApp.Normalizar(" chat.whatsapp.com/ABC123 "));
        Assert.Null(GrupoDoTorneioNoWhatsApp.ProblemaCom("chat.whatsapp.com/ABC123"));
    }

    [Fact]
    public void http_vira_https()
    {
        Assert.Equal("https://chat.whatsapp.com/ABC123",
            GrupoDoTorneioNoWhatsApp.Normalizar("http://chat.whatsapp.com/ABC123"));
    }

    [Theory]
    // O disfarce mais barato que existe: o host de verdade é "pegadinha.com.br".
    [InlineData("https://chat.whatsapp.com.pegadinha.com.br/ABC123")]
    // Aqui o "chat.whatsapp.com" é só um pedaço do caminho.
    [InlineData("https://pegadinha.com.br/chat.whatsapp.com/ABC123")]
    // E aqui ele é o usuário, não o servidor: o clique iria pra "pegadinha.com.br".
    [InlineData("https://chat.whatsapp.com@pegadinha.com.br/ABC123")]
    // Endereço nenhum: código rodando na página do torneio, na sessão de quem clicou.
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    // O site do WhatsApp não é convite pra grupo nenhum — falta o código que diz QUAL grupo.
    [InlineData("https://chat.whatsapp.com/")]
    // wa.me abre conversa com UMA pessoa, não o grupo do torneio.
    [InlineData("https://wa.me/5551999999999")]
    public void O_que_nao_e_convite_de_grupo_e_recusado_com_o_motivo(string endereco)
    {
        Assert.False(GrupoDoTorneioNoWhatsApp.EhConviteDeGrupo(endereco));

        var problema = GrupoDoTorneioNoWhatsApp.ProblemaCom(endereco);
        Assert.NotNull(problema);
        Assert.Contains("chat.whatsapp.com", problema);
    }

    [Fact]
    public void Campo_em_branco_nao_e_problema_nenhum()
    {
        // Torneio sem grupo é o caso normal: simplesmente não há botão.
        Assert.Null(GrupoDoTorneioNoWhatsApp.ProblemaCom(null));
        Assert.Null(GrupoDoTorneioNoWhatsApp.ProblemaCom("   "));
        Assert.Null(GrupoDoTorneioNoWhatsApp.Normalizar("   "));
    }

    // ---- O portão ----

    [Fact]
    public void Quem_nao_esta_no_torneio_nao_recebe_o_link()
    {
        // ⚠️ O teste central. A página do torneio é pública: se o link sair daqui pra quem
        // não está inscrito, ele está publicado — e não há como despublicar um convite.
        var torneio = new Torneio { Nome = "T", Codigo = "T1", LinkGrupoWhatsApp = "https://chat.whatsapp.com/ABC123" };

        Assert.Null(GrupoDoTorneioNoWhatsApp.LinkPara(torneio, estaNoTorneio: false));
        Assert.NotNull(GrupoDoTorneioNoWhatsApp.LinkPara(torneio, estaNoTorneio: true));
    }

    [Fact]
    public void Endereco_estranho_guardado_no_banco_nao_vira_botao_nem_pra_inscrito()
    {
        // A segunda tranca: o que está guardado é conferido DE NOVO na hora de mostrar.
        // Linha antiga, importação e correção na mão no banco não passaram pela tela.
        var torneio = new Torneio { Nome = "T", Codigo = "T1", LinkGrupoWhatsApp = "javascript:alert(1)" };

        Assert.Null(GrupoDoTorneioNoWhatsApp.LinkPara(torneio, estaNoTorneio: true));
        Assert.False(GrupoDoTorneioNoWhatsApp.Configurado(torneio));
    }

    // ---- Quem a página reconhece como "está no torneio" ----

    [Fact]
    public async Task Inscrito_ve_o_grupo_e_quem_so_passou_na_pagina_nao_ve()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.LinkGrupoWhatsApp = "https://chat.whatsapp.com/ABC123";
        await ctx.SaveChangesAsync();

        var inscrito = ctx.Duplas.First(d => d.CategoriaId == categoria.Id).Jogador1Id;
        Assert.True(await PodeVerAsync(ctx, torneio.Id, inscrito));

        var curioso = new Jogador { Nome = "Curioso", Cpf = "22255588896" };
        ctx.Jogadores.Add(curioso);
        await ctx.SaveChangesAsync();
        Assert.False(await PodeVerAsync(ctx, torneio.Id, curioso.Id));

        // E nem logado a pessoa precisa estar pra abrir a página do torneio.
        Assert.False(await PodeVerAsync(ctx, torneio.Id, jogadorId: 0));
    }

    [Fact]
    public async Task O_organizador_ve_o_grupo_mesmo_sem_estar_inscrito()
    {
        // Ele precisa conferir se o link que colou funciona — e é ele quem cria o grupo.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        Assert.True(await PodeVerAsync(ctx, torneio.Id, organizador.Id));
    }

    [Fact]
    public async Task Quem_esta_na_lista_de_espera_tambem_ve()
    {
        // Em outros lugares do sistema quem espera NÃO está inscrito (não ocupa vaga, não
        // entra no sorteio). Aqui a pergunta é outra: é no grupo que a vaga que abre é
        // anunciada, e deixar a fila de fora é deixar de fora justamente quem depende dele.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        dupla.EmListaDeEspera = true;
        await ctx.SaveChangesAsync();

        Assert.True(await PodeVerAsync(ctx, torneio.Id, dupla.Jogador1Id));
    }

    [Fact]
    public async Task No_americano_a_inscricao_individual_tambem_conta()
    {
        // O Americano não tem Dupla nenhuma: quem olhasse só a tabela de duplas concluiria
        // que ninguém está inscrito, e o torneio inteiro ficaria sem botão.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.Formato = "Americano";

        var jogador = new Jogador { Nome = "Americano", Cpf = "22255588896" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = jogador.Id });
        await ctx.SaveChangesAsync();

        Assert.True(await PodeVerAsync(ctx, torneio.Id, jogador.Id));
    }

    // ---- Salvar o link ----

    [Fact]
    public async Task O_organizador_salva_o_convite_e_ele_fica_guardado_com_https()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");

        await EditarComLinkAsync(ctx, torneio, organizador.Id, "chat.whatsapp.com/ABC123");

        Assert.Equal("https://chat.whatsapp.com/ABC123",
            (await ctx.Torneios.FindAsync(torneio.Id))!.LinkGrupoWhatsApp);
    }

    [Fact]
    public async Task Endereco_que_nao_e_do_whatsapp_e_recusado_e_o_que_estava_gravado_fica()
    {
        // A recusa vem ANTES de gravar qualquer coisa: senão o torneio ficaria salvo pela
        // metade, com o resto dos campos já alterados — a mesma regra da chave de acesso.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.LinkGrupoWhatsApp = "https://chat.whatsapp.com/ANTIGO1";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await EditarComLinkAsync(ctx, torneio, organizador.Id, "https://pegadinha.com.br/entra", controller);

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("https://chat.whatsapp.com/ANTIGO1",
            (await ctx.Torneios.FindAsync(torneio.Id))!.LinkGrupoWhatsApp);
    }

    [Fact]
    public async Task Campo_apagado_tira_o_botao()
    {
        // É como o organizador desfaz um grupo que acabou. Quem já entrou continua lá — isso
        // é do WhatsApp, não nosso — mas ninguém novo entra por aqui.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.LinkGrupoWhatsApp = "https://chat.whatsapp.com/ABC123";
        await ctx.SaveChangesAsync();

        await EditarComLinkAsync(ctx, torneio, organizador.Id, "");

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.LinkGrupoWhatsApp);
    }

    // ---- A ligação com a tela ----

    [Fact]
    public void A_pagina_do_torneio_nunca_escreve_o_link_direto_num_href()
    {
        // Este é o teste que impede o portão de ser contornado sem ninguém notar: escrever
        // href="@Model.LinkGrupoWhatsApp" funciona, fica bonito na tela do organizador que
        // testou — e publica o convite pra qualquer um que abra a página.
        var tela = SemComentarioRazor(File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml")));

        Assert.DoesNotContain("href=\"@Model.LinkGrupoWhatsApp", tela);
        Assert.Contains("GrupoDoTorneioNoWhatsApp.LinkPara(Model, ViewBag.PodeVerOGrupoDoWhats == true)", tela);

        // O campo aparece preenchido num lugar só: o formulário da gestão, que já é fechado.
        Assert.Single(Regex.Matches(tela, @"Model\.LinkGrupoWhatsApp"));
    }

    // ---- Bastidores ----

    // Roda o Details de verdade e devolve o que a view vai perguntar.
    private static async Task<bool> PodeVerAsync(DbPadelContext ctx, int torneioId, int jogadorId)
    {
        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, jogadorId)
                .Details(torneioId, timeFiltroId: null, categoriaFiltroIds: null));

        return view.ViewData["PodeVerOGrupoDoWhats"] as bool? == true;
    }

    private static Task<IActionResult> EditarComLinkAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, string? link,
        TorneiosController? controller = null)
        => (controller ?? TestInfra.NovoTorneiosController(ctx, organizadorId)).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            linkGrupoWhatsApp: link);

    // Comentário Razor não chega ao navegador — e os desta tela citam os próprios trechos que
    // o teste procura. Sem o corte, a explicação passaria por implementação.
    private static string SemComentarioRazor(string texto) =>
        Regex.Replace(texto, @"@\*.*?\*@", "", RegexOptions.Singleline);

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

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
