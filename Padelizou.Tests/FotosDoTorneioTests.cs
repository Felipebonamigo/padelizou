using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS FOTOS DO TORNEIO (pedido do Felipe, 11/08/2026): o organizador cola o endereço de onde as
// fotos foram publicadas e a página do torneio ganha um botão pra elas.
//
// ⚠️ Este campo é o VIZINHO do link do grupo no WhatsApp, e as duas regras são OPOSTAS —
// é por isso que os testes existem separados em vez de virarem um só:
//
//   grupo  → host conhecido (chat.whatsapp.com), e o link é SEGREDO de quem está inscrito;
//   fotos  → host QUALQUER (Drive, Google Fotos, Instagram, site do fotógrafo), e o link é
//            PÚBLICO de propósito.
//
// Como não dá pra ter lista de hosts permitidos sem recusar álbum legítimo, a única trava que
// sobra aqui é o ESQUEMA. Se ela cair, o campo vira uma porta pra "javascript:" rodando na
// sessão de quem clicar, dentro da página do torneio — e a tela continua abrindo normalmente,
// que é o que torna esse defeito mudo.
public class FotosDoTorneioTests
{
    // ---- O endereço ----

    [Fact]
    public void O_link_colado_do_album_passa_inteiro()
    {
        const string album = "https://photos.app.goo.gl/H7kP2qXzYaB3cD4eF5";

        Assert.Null(FotosDoTorneio.ProblemaCom(album));
        Assert.Equal(album, FotosDoTorneio.Normalizar(album));
    }

    [Fact]
    public void Digitado_sem_https_o_link_e_completado_em_vez_de_recusado()
    {
        Assert.Equal("https://photos.app.goo.gl/ABC123",
            FotosDoTorneio.Normalizar(" photos.app.goo.gl/ABC123 "));
        Assert.Null(FotosDoTorneio.ProblemaCom("photos.app.goo.gl/ABC123"));
    }

    [Fact]
    public void http_continua_http_aqui_ao_contrario_do_link_do_grupo()
    {
        // A diferença deliberada. No grupo o destino é um só e sabidamente atende em https, e
        // promover o endereço economiza um salto. Aqui o destino é o servidor que o fotógrafo
        // tiver: forçar https num site que só fala http quebraria um link que funcionava.
        Assert.Equal("http://fotos-do-ze.com.br/torneio",
            FotosDoTorneio.Normalizar("http://fotos-do-ze.com.br/torneio"));
        Assert.Null(FotosDoTorneio.ProblemaCom("http://fotos-do-ze.com.br/torneio"));
    }

    [Theory]
    // O álbum pode estar em qualquer lugar, e é isso que impede uma lista de permitidos.
    [InlineData("https://photos.app.goo.gl/ABC123")]
    [InlineData("https://drive.google.com/drive/folders/1a2b3c")]
    [InlineData("https://www.instagram.com/p/Cx1y2z3/")]
    [InlineData("https://www.dropbox.com/scl/fo/abc/fotos")]
    [InlineData("https://fotos-do-ze.com.br/nata-padel-tour-2026")]
    public void Album_em_qualquer_host_e_aceito(string endereco)
    {
        Assert.True(FotosDoTorneio.EhEnderecoDeAlbum(endereco));
        Assert.Null(FotosDoTorneio.ProblemaCom(endereco));
    }

    [Theory]
    // ⚠️ O TESTE CENTRAL. Os três são URIs absolutas VÁLIDAS — passam no Uri.TryCreate sem
    // reclamar. O que separa cada um deles de um álbum é só o esquema, e é só isso que a
    // conferência tem pra segurar aqui.
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///C:/Windows/System32")]
    // "https://" sozinho monta uma URI que não leva a lugar nenhum.
    [InlineData("https://")]
    public void O_que_nao_e_endereco_de_pagina_e_recusado_com_o_motivo(string endereco)
    {
        Assert.False(FotosDoTorneio.EhEnderecoDeAlbum(endereco));
        Assert.NotNull(FotosDoTorneio.ProblemaCom(endereco));
    }

    [Fact]
    public void Texto_gigante_e_recusado()
    {
        // A tela limita em 300, mas o POST vem do navegador e o navegador vem de quem quiser.
        var enorme = "https://fotos.com.br/" + new string('a', FotosDoTorneio.MaximoDeCaracteres);

        Assert.False(FotosDoTorneio.EhEnderecoDeAlbum(enorme));
        Assert.NotNull(FotosDoTorneio.ProblemaCom(enorme));
    }

    [Fact]
    public void Campo_em_branco_nao_e_problema_nenhum()
    {
        // Torneio sem álbum publicado é o caso normal: simplesmente não há botão.
        Assert.Null(FotosDoTorneio.ProblemaCom(null));
        Assert.Null(FotosDoTorneio.ProblemaCom("   "));
        Assert.Null(FotosDoTorneio.Normalizar("   "));
    }

    // ---- Quem vê ----

    [Fact]
    public void O_link_das_fotos_e_publico_e_isso_e_a_decisao_nao_um_esquecimento()
    {
        // O oposto exato do grupo no WhatsApp, que só sai pra quem está inscrito. Álbum de
        // torneio é divulgação: quem NÃO jogou ver a foto é justamente o que puxa gente pro
        // próximo. Se algum dia isto virar segredo, é este teste que tem que cair junto.
        var torneio = new Torneio
        {
            Nome = "T",
            Codigo = "T1",
            LinkDasFotos = "https://photos.app.goo.gl/ABC123"
        };

        Assert.Equal("https://photos.app.goo.gl/ABC123", FotosDoTorneio.LinkPara(torneio));
        Assert.True(FotosDoTorneio.Configurado(torneio));
    }

    [Fact]
    public void Endereco_estranho_guardado_no_banco_nao_vira_botao()
    {
        // A segunda tranca: o que está guardado é conferido DE NOVO na hora de mostrar, e não
        // só na hora de salvar. Linha antiga, importação e correção na mão no banco não
        // passaram pela tela.
        var torneio = new Torneio { Nome = "T", Codigo = "T1", LinkDasFotos = "javascript:alert(1)" };

        Assert.Null(FotosDoTorneio.LinkPara(torneio));
        Assert.False(FotosDoTorneio.Configurado(torneio));
    }

    // ---- Salvar o link ----

    [Fact]
    public async Task O_organizador_salva_o_album_e_ele_fica_guardado_com_https()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");

        await EditarComFotosAsync(ctx, torneio, organizador.Id, "photos.app.goo.gl/ABC123");

        Assert.Equal("https://photos.app.goo.gl/ABC123",
            (await ctx.Torneios.FindAsync(torneio.Id))!.LinkDasFotos);
    }

    [Fact]
    public async Task Endereco_invalido_e_recusado_e_o_que_estava_gravado_fica()
    {
        // A recusa vem ANTES de gravar qualquer coisa: senão o torneio ficaria salvo pela
        // metade, com o resto dos campos já alterados.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.LinkDasFotos = "https://photos.app.goo.gl/ANTIGO1";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await EditarComFotosAsync(ctx, torneio, organizador.Id, "javascript:alert(1)", controller);

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("https://photos.app.goo.gl/ANTIGO1",
            (await ctx.Torneios.FindAsync(torneio.Id))!.LinkDasFotos);
    }

    [Fact]
    public async Task Campo_apagado_tira_o_botao()
    {
        // É como o organizador desfaz um álbum que saiu do ar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.LinkDasFotos = "https://photos.app.goo.gl/ABC123";
        await ctx.SaveChangesAsync();

        await EditarComFotosAsync(ctx, torneio, organizador.Id, "");

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.LinkDasFotos);
    }

    // ---- A ligação com a tela ----

    [Fact]
    public void A_pagina_do_torneio_pergunta_ao_servico_em_vez_de_ler_o_campo_num_href()
    {
        // Escrever href="@Model.LinkDasFotos" funciona e fica bonito na tela do organizador que
        // testou com um álbum de verdade — e entrega o href sem conferência nenhuma pra quem
        // tiver gravado outra coisa no campo.
        var tela = SemComentarioRazor(File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml")));

        Assert.DoesNotContain("href=\"@Model.LinkDasFotos", tela);
        Assert.Contains("FotosDoTorneio.LinkPara(Model)", tela);

        // O campo aparece cru num lugar só: o formulário da gestão, que já é fechado.
        Assert.Single(Regex.Matches(tela, @"Model\.LinkDasFotos"));
    }

    // ---- Bastidores ----

    private static Task<IActionResult> EditarComFotosAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, string? link,
        TorneiosController? controller = null)
        => (controller ?? TestInfra.NovoTorneiosController(ctx, organizadorId)).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            linkDasFotos: link);

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
