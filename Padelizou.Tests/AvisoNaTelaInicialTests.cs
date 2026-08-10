using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Tests;

// O AVISO PRECISA SER VISTO AO ENTRAR (pedido do Felipe, 10/08/2026).
//
// A caixa de avisos é o único canal que não depende de entrega nenhuma: o push alcança 5
// aparelhos em 154, a cota do Gmail já estourou e derrubou 130 e-mails, e o WhatsApp depende
// de um chip pré-pago. Mesmo assim ela só era encontrada de UM jeito — abrindo o sanduíche e
// reparando na bolinha do sino. No celular, que é onde este produto é usado, o menu nasce
// fechado: quem entrava e não tocava em nada não descobria que a chave do torneio saiu.
public class AvisoNaTelaInicialTests
{
    private static HomeController NovoHome(DbPadelContext ctx, int? usuarioLogadoId = null)
    {
        var controller = new HomeController(ctx, new EstatisticasService(ctx));
        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    private static async Task<HomeVM> Abrir(DbPadelContext ctx, int? usuarioLogadoId = null)
    {
        var result = (ViewResult)await NovoHome(ctx, usuarioLogadoId).Index();
        return (HomeVM)result.Model!;
    }

    private static Jogador NovoJogador(DbPadelContext ctx, string nome, string cpf)
    {
        var j = new Jogador { Nome = nome, Login = nome.ToLower(), Cpf = cpf };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    private static AvisoDoJogador Avisar(DbPadelContext ctx, int jogadorId, string titulo,
        DateTime quando, DateTime? lidaEm = null)
    {
        var aviso = new AvisoDoJogador
        {
            JogadorId = jogadorId,
            Titulo = titulo,
            Corpo = "corpo do aviso",
            Url = "/Torneios",
            CriadoEm = quando,
            LidaEm = lidaEm,
        };
        ctx.AvisosDoJogador.Add(aviso);
        ctx.SaveChanges();
        return aviso;
    }

    [Fact]
    public async Task O_aviso_novo_aparece_na_home_com_o_mais_recente_em_cima()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Maickel", "77700000001");

        Avisar(ctx, eu.Id, "Chave saiu", DateTime.Now.AddHours(-30));
        Avisar(ctx, eu.Id, "Novo torneio aberto", DateTime.Now.AddHours(-21));

        var vm = await Abrir(ctx, eu.Id);

        Assert.Equal(2, vm.TotalAvisosNovos);
        Assert.Equal("Novo torneio aberto", vm.AvisosNovos[0].Titulo);
    }

    // Aviso lido já cumpriu o papel dele. Continuar na primeira tela transformaria a Home num
    // mural que nunca esvazia — e aviso que não some vira paisagem.
    [Fact]
    public async Task Aviso_ja_lido_nao_volta_pra_home()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Maickel", "77700000002");

        Avisar(ctx, eu.Id, "Antigo", DateTime.Now.AddDays(-2), lidaEm: DateTime.Now.AddDays(-1));

        var vm = await Abrir(ctx, eu.Id);

        Assert.Empty(vm.AvisosNovos);
        Assert.Equal(0, vm.TotalAvisosNovos);
    }

    // ⚠️ O teste que não pode ser apagado. Se a Home marcasse como lido só de desenhar o card,
    // o aviso sumiria da próxima visita — e o sino apagaria junto — com a pessoa nunca tendo
    // aberto nem lido nada. Quem lê é quem abre a caixa.
    [Fact]
    public async Task Abrir_a_home_nao_marca_o_aviso_como_lido()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Maickel", "77700000003");
        var aviso = Avisar(ctx, eu.Id, "Chave saiu", DateTime.Now.AddHours(-2));

        await Abrir(ctx, eu.Id);

        Assert.Null(ctx.AvisosDoJogador.Single(a => a.Id == aviso.Id).LidaEm);

        // E na segunda visita ele continua lá.
        var vm = await Abrir(ctx, eu.Id);
        Assert.Single(vm.AvisosNovos);
    }

    // O card mostra 3 e diz quantos faltam. Sem o total de verdade, "e mais N" mentiria — e
    // este projeto já teve um card prometendo "e mais 1 elogio" que nunca ia desenhar.
    [Fact]
    public async Task A_home_mostra_os_primeiros_e_conta_o_resto()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Maickel", "77700000004");

        for (int i = 0; i < 7; i++)
        {
            Avisar(ctx, eu.Id, "Aviso " + i, DateTime.Now.AddMinutes(-i));
        }

        var vm = await Abrir(ctx, eu.Id);

        Assert.Equal(3, vm.AvisosNovos.Count);
        Assert.Equal(7, vm.TotalAvisosNovos);
    }

    [Fact]
    public async Task Aviso_de_outra_pessoa_nao_aparece_no_meu()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Maickel", "77700000005");
        var outro = NovoJogador(ctx, "Rafael", "77700000006");

        Avisar(ctx, outro.Id, "Aviso do Rafael", DateTime.Now);

        var vm = await Abrir(ctx, eu.Id);

        Assert.Empty(vm.AvisosNovos);
    }

    [Fact]
    public async Task Visitante_nao_tem_caixa_de_avisos()
    {
        using var ctx = TestInfra.NovoContexto();

        var vm = await Abrir(ctx);

        Assert.Empty(vm.AvisosNovos);
        Assert.Equal(0, vm.TotalAvisosNovos);
    }

    // ---- O sino, no arquivo ----
    //
    // A barra é HTML do layout: não passa por controller, e o defeito seria mudo — o site
    // continua abrindo, e o sino volta a existir só pra quem abre o sanduíche.

    [Fact]
    public void O_sino_fica_fora_do_sanduiche()
    {
        var layout = Arquivo(Path.Combine("Views", "Shared", "_Layout.cshtml"));

        var sino = layout.IndexOf("naBarra = true", StringComparison.Ordinal);
        var sanduiche = layout.IndexOf("navbar-collapse collapse", StringComparison.Ordinal);

        Assert.True(sino >= 0, "O sino da barra sumiu do _Layout: sem ele o aviso volta a ficar "
            + "escondido atrás do ícone de três linhas no celular.");
        Assert.True(sino < sanduiche, "O sino da barra tem que ficar ANTES do #pdzNav — dentro do "
            + "collapse ele é exatamente o que estava escondido.");
    }

    // Os dois sinos ao mesmo tempo seriam duas portas pro mesmo lugar na mesma tela.
    [Fact]
    public void O_sino_do_menu_so_aparece_onde_o_menu_esta_aberto()
    {
        var doMenu = Arquivo(Path.Combine("Views", "Shared", "Components", "SinoDeAvisos", "Default.cshtml"));

        Assert.Contains("d-none d-xl-block", doMenu);
    }

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
