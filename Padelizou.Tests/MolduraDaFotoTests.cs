using NSubstitute;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 13/08/2026 — MOLDURAS DA FOTO: cada conquista destrava uma, o jogador escolhe no perfil.
//
// Os dois conjuntos (conquistas e molduras) são gêmeos POR CONTRATO, e o teste é o cartório:
// conquista sem moldura é promessa quebrada ("cada conquista destrava uma moldura" — e a sua
// não); moldura sem conquista é enfeite órfão que ninguém consegue destravar. Os dois erros
// nascem do mesmo jeito — alguém adiciona numa lista e esquece da outra.
public class MolduraDaFotoTests
{
    // Todas as conquistas do catálogo, todas destravadas — o universo completo de códigos.
    private static List<ConquistaVM> TodasConquistadas() =>
        CatalogoConquistas.Montar(new DadosParaConquistas(
            JogouAlgumaVez: true, JogosSemanais: 99, EhOrganizador: true, TemTime: true,
            EhProfessor: true, Titulos: 10, Finais: 10, TotalTorneios: 99, Vitorias: 200,
            ElogiosRecebidos: 99, AulasComoAluno: 99, VezesMvp: 1, ClubesDiferentes: 9));

    private static List<ConquistaVM> NenhumaConquistada() =>
        CatalogoConquistas.Montar(new DadosParaConquistas(
            false, 0, false, false, false, 0, 0, 0, 0, 0, 0));

    // As DE FÁBRICA (Livre) ficam fora da sincronia de propósito: elas existem justamente
    // pra quem não tem conquista nenhuma. O contrato passa a ser: toda conquista tem moldura,
    // e toda moldura NÃO-livre tem conquista.
    [Fact]
    public void Toda_conquista_tem_moldura_e_toda_moldura_de_conquista_tem_conquista()
    {
        var conquistas = TodasConquistadas().Select(c => c.Codigo).ToHashSet();
        var molduras = CatalogoMolduras.Todas.Where(m => !m.Livre).Select(m => m.Codigo).ToHashSet();

        var conquistaSemMoldura = conquistas.Except(molduras).ToList();
        var molduraSemConquista = molduras.Except(conquistas).ToList();

        Assert.True(conquistaSemMoldura.Count == 0,
            "Conquista sem moldura (a promessa é 'cada conquista destrava uma'): "
            + string.Join(", ", conquistaSemMoldura));
        Assert.True(molduraSemConquista.Count == 0,
            "Moldura órfã — não há conquista que a destrave: " + string.Join(", ", molduraSemConquista));
    }

    // ⚠️ E uma DE FÁBRICA não pode usar código de conquista: no dia em que uma conquista
    // "Gatinha" nascesse, a moldura livre viraria travada (ou vice-versa) sem ninguém decidir.
    [Fact]
    public void Moldura_de_fabrica_nao_reusa_codigo_de_conquista()
    {
        var conquistas = TodasConquistadas().Select(c => c.Codigo).ToHashSet();
        var colisoes = CatalogoMolduras.Todas.Where(m => m.Livre && conquistas.Contains(m.Codigo)).ToList();

        Assert.True(colisoes.Count == 0,
            "Moldura de fábrica com código de conquista: " + string.Join(", ", colisoes.Select(m => m.Codigo)));
    }

    // ⚠️ O COFRE DAS DIVERTIDAS (decisão do Felipe, 14/08/2026: molduras por conquista; as
    // enfeitadas viram produto pago no futuro). O padrão do parâmetro é FECHADO — quem
    // esquecer de passar a chave recebe a recusa, nunca a liberação.
    [Fact]
    public void Divertida_guardada_e_recusada_mesmo_com_todas_as_conquistas()
    {
        foreach (var livre in CatalogoMolduras.Todas.Where(m => m.Livre))
        {
            var motivo = CatalogoMolduras.MotivoParaNaoUsar(livre.Codigo, TodasConquistadas());

            Assert.NotNull(motivo);
            Assert.Contains("ainda não está disponível", motivo);
        }
    }

    // E quando o Felipe liberar, elas passam sem exigir conquista nenhuma — é a razão de
    // existirem (jogador novo, zero conquista, já tem o que vestir).
    [Fact]
    public void Divertida_LIBERADA_dispensa_conquista()
    {
        foreach (var livre in CatalogoMolduras.Todas.Where(m => m.Livre))
        {
            Assert.Null(CatalogoMolduras.MotivoParaNaoUsar(
                livre.Codigo, NenhumaConquistada(), divertidasLiberadas: true));
        }
    }

    // O cofre não pode vazar pelo outro lado: moldura de CONQUISTA não vira livre porque o
    // interruptor das divertidas ligou.
    [Fact]
    public void Liberar_as_divertidas_nao_solta_as_de_conquista()
    {
        var motivo = CatalogoMolduras.MotivoParaNaoUsar(
            "Decacampeao", NenhumaConquistada(), divertidasLiberadas: true);

        Assert.NotNull(motivo);
        Assert.Contains("ainda não destravou", motivo);
    }

    // ⚠️ Moldura no catálogo sem CSS é INVISÍVEL — a pessoa escolhe e nada muda, sem erro em
    // lugar nenhum. O teste lê o site.css de verdade, como o PwaArquivosTests lê o sw.js.
    [Fact]
    public void Toda_moldura_do_catalogo_tem_o_desenho_no_site_css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei o site.css.");

        var css = File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "wwwroot", "css", "site.css"));

        var semDesenho = CatalogoMolduras.Todas
            .Where(m => !Regex.IsMatch(css, $@"\.pdz-m-{Regex.Escape(m.Codigo)}\b"))
            .Select(m => m.Codigo)
            .ToList();

        Assert.True(semDesenho.Count == 0,
            "Moldura sem CSS — quem escolher não vê nada acontecer: " + string.Join(", ", semDesenho));
    }

    // ===================== A TRAVA DE USO =====================

    [Fact]
    public void Tirar_a_moldura_pode_sempre()
    {
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar(null, NenhumaConquistada()));
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("", NenhumaConquistada()));
    }

    [Fact]
    public void Moldura_de_conquista_destravada_pode()
    {
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("Estreia", TodasConquistadas()));
        Assert.Null(CatalogoMolduras.MotivoParaNaoUsar("Decacampeao", TodasConquistadas()));
    }

    // ⚠️ A LISTA DA TELA NÃO É A TRAVA: a tela cinza a moldura bloqueada, mas o POST chega com
    // qualquer string. Sem esta recusa, um formulário montado à mão vestiria a moldura de
    // Decacampeão em quem nunca jogou.
    [Fact]
    public void Moldura_de_conquista_NAO_destravada_e_recusada()
    {
        var motivo = CatalogoMolduras.MotivoParaNaoUsar("Decacampeao", NenhumaConquistada());

        Assert.NotNull(motivo);
        Assert.Contains("ainda não destravou", motivo);
    }

    [Fact]
    public void Codigo_inventado_e_recusado_sem_virar_classe_css()
    {
        Assert.NotNull(CatalogoMolduras.MotivoParaNaoUsar("HackDoRecreio", TodasConquistadas()));

        // E mesmo que um valor inventado chegue ao banco, ele nunca vira classe: o CSS da
        // moldura sai do catálogo, não do texto gravado.
        Assert.Equal("", CatalogoMolduras.ClasseCss("HackDoRecreio"));
        Assert.Equal("", CatalogoMolduras.ClasseCss(null));
        Assert.Equal("pdz-m-Campeao", CatalogoMolduras.ClasseCss("Campeao"));
    }

    // ===================== "NÃO QUERO VER MOLDURA NENHUMA" =====================
    //
    // 14/08/2026, pedido do Felipe. A preferência é de quem OLHA (não do dono da foto) e mora
    // no aparelho, ao lado do tema: um atributo no <html> desliga o sistema inteiro com uma
    // regra de CSS — inclusive as fotos que ainda não passam pelo parcial.

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    // ⚠️ ESQUECER UMA DAS TRÊS CAMADAS É O BUG SILENCIOSO DESTA FEATURE: sem o `::before` a
    // coroa fica FLUTUANDO sobre uma foto sem anel — pior do que a moldura inteira.
    [Fact]
    public void Desligar_as_molduras_zera_anel_verniz_E_ornamento()
    {
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

        var bloco = Regex.Match(css,
            @":root\[data-molduras=""off""\].*?(?=\n/\* ──|\n\.pdz-m-)", RegexOptions.Singleline);
        Assert.True(bloco.Success, "Sumiu a regra que desliga as molduras (:root[data-molduras=\"off\"]).");

        var regra = bloco.Value;
        Assert.Contains("padding: 0", regra);          // o anel
        Assert.Contains("background: none", regra);    // a cor da moldura
        Assert.Contains("::before", regra);            // o ornamento que transborda
        Assert.Contains("::after", regra);             // o verniz
        Assert.Contains("animation: none", regra);     // a Lenda para de girar
    }

    // O <head> lê a chave antes do primeiro pixel; a tela de Molduras escreve nela. Nomes
    // diferentes = o interruptor liga e a próxima página volta a mostrar tudo, sem erro nenhum.
    [Fact]
    public void A_chave_do_aparelho_tem_o_mesmo_nome_nos_dois_lugares()
    {
        var raiz = RaizDoRepo();
        var layout = File.ReadAllText(Path.Combine(raiz, "Padelizou", "Views", "Shared", "_Layout.cshtml"));
        var tela = File.ReadAllText(Path.Combine(raiz, "Padelizou", "Views", "Molduras", "Index.cshtml"));

        Assert.Contains("'pdz-molduras'", layout);
        Assert.Contains("'pdz-molduras'", tela);

        // E o atributo que o CSS espera é o mesmo que o <head> escreve.
        Assert.Contains("data-molduras", layout);
        Assert.Contains("data-molduras", tela);
    }

    // ===================== O INTERRUPTOR =====================
    //
    // 14/08/2026: o Felipe pediu pra SEGURAR as molduras enquanto decide o modelo (conquista ×
    // paga) — e o pedido chegou com tudo já publicado. O portão é o que transforma "está no
    // ar" em "ninguém vê": desligado, a tela devolve 404 pra jogador comum. Ligar é uma linha
    // no systemd, não um deploy.

    private static PortaDasMolduras PortaCom(bool habilitado, bool divertidas = false) =>
        new(Microsoft.Extensions.Options.Options.Create(
            new MoldurasSettings { Habilitado = habilitado, DivertidasLiberadas = divertidas }));

    private static System.Security.Claims.ClaimsPrincipal Quem(bool admin)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
        };
        if (admin) claims.Add(new("IsAdmin", "true"));
        return new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(claims, "Teste"));
    }

    // ⚠️ ESTE É O BURACO QUE PRODUÇÃO MOSTROU (14/08/2026): o portão fechava a PORTA e não a
    // VITRINE. A tela dava 404, e a moldura de quem tinha escolhido antes do interruptor
    // continuava DESENHADA em toda tela — a consulta em prod achou 1 jogador assim.
    //
    // A porta e o desenho têm que responder à mesma pergunta, e é isso que estes testes fixam.
    [Fact]
    public void Portao_fechado_esconde_a_moldura_de_quem_JA_escolheu()
    {
        var fechada = PortaCom(habilitado: false);

        Assert.True(fechada.Fechada(Quem(admin: false)));

        // O admin continua vendo — é assim que o Felipe avalia ao vivo antes de decidir.
        Assert.False(fechada.Fechada(Quem(admin: true)));

        // Ligado, todo mundo vê.
        Assert.False(PortaCom(habilitado: true).Fechada(Quem(admin: false)));

        // Visitante deslogado (o parcial desenha foto em tela pública) não pode explodir nem
        // virar admin por acidente: sem claim, a porta está fechada.
        Assert.True(fechada.Fechada(null));
    }

    // O teste acima prova a REGRA; este prova que quem DESENHA pergunta por ela. Sem isto, a
    // regra continuaria certa e sozinha — que foi exatamente o estado que chegou em produção.
    [Fact]
    public void O_parcial_da_foto_consulta_o_portao_antes_de_montar_a_classe()
    {
        var parcial = File.ReadAllText(Path.Combine(
            RaizDoRepo(), "Padelizou", "Views", "Shared", "_FotoDoJogador.cshtml"));

        Assert.Contains("PortaDasMolduras", parcial);
        Assert.Contains("Fechada(User)", parcial);
    }

    // ⚠️ `@inject` numa view só quebra em RUNTIME, na hora de desenhar: serviço não registrado
    // derruba toda tela que mostra foto, com o app subindo saudável e o healthz em 200. Como
    // o parcial passou a injetar o portão, o registro virou parte do contrato.
    [Fact]
    public void O_portao_esta_registrado_no_container()
    {
        var program = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Program.cs"));

        Assert.Contains("AddSingleton<PortaDasMolduras>", program);
        Assert.Contains("Configure<MoldurasSettings>", program);
    }

    // E o parcial precisa continuar sendo o ÚNICO que desenha moldura: uma view que monte a
    // classe pdz-m- por conta própria escapa do portão sem ninguém perceber.
    [Fact]
    public void Nenhuma_outra_view_desenha_moldura_por_fora_do_parcial()
    {
        var views = Path.Combine(RaizDoRepo(), "Padelizou", "Views");

        var forasteiras = Directory.GetFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .Where(a => !a.EndsWith("_FotoDoJogador.cshtml"))
            .Where(a => Regex.IsMatch(File.ReadAllText(a), @"pdz-m-[a-zA-Z]"))
            .Select(a => Path.GetFileName(a))
            .ToList();

        Assert.True(forasteiras.Count == 0,
            "View desenhando moldura sem passar pelo portão: " + string.Join(", ", forasteiras));
    }

    private static padelizou.Controllers.MoldurasController ControllerMolduras(
        Padelizou.Models.DbPadelContext ctx, int euId, bool habilitado, bool admin = false)
    {
        var estatisticas = NSubstitute.Substitute.For<IEstatisticasService>();
        estatisticas.ObterConquistasAsync(euId).Returns(NenhumaConquistada());

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, euId.ToString()),
        };
        if (admin) claims.Add(new("IsAdmin", "true"));

        var controller = new padelizou.Controllers.MoldurasController(
            ctx, estatisticas, PortaCom(habilitado))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(claims, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext,
            NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Com_o_interruptor_DESLIGADO_a_tela_nao_existe_pra_jogador_comum()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = new Padelizou.Models.Jogador { Nome = "Comum", Cpf = "44400000001", Login = "comum" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        var controller = ControllerMolduras(ctx, jogador.Id, habilitado: false);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.Index());
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.Escolher("Gatinha"));

        // E o POST barrado não gravou nada — 404 de fachada com efeito por baixo seria pior.
        Assert.Null((await ctx.Jogadores.FindAsync(jogador.Id))!.MolduraEscolhida);
    }

    // O admin entra mesmo desligado — é assim que o Felipe avalia ao vivo antes de decidir.
    [Fact]
    public async Task Admin_entra_mesmo_com_o_interruptor_desligado()
    {
        using var ctx = TestInfra.NovoContexto();
        var admin = new Padelizou.Models.Jogador { Nome = "Admin", Cpf = "44400000002", Login = "adm" };
        ctx.Jogadores.Add(admin);
        await ctx.SaveChangesAsync();

        var controller = ControllerMolduras(ctx, admin.Id, habilitado: false, admin: true);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.Index());
    }

    [Fact]
    public async Task Com_o_interruptor_LIGADO_o_jogador_comum_entra()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = new Padelizou.Models.Jogador { Nome = "Comum", Cpf = "44400000003", Login = "comum3" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        var controller = ControllerMolduras(ctx, jogador.Id, habilitado: true);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.Index());
    }

    // O VM do parcial: tamanho zero significa "quem dimensiona é a classe" (o chip compacto) —
    // nesse caso o parcial não pode emitir o --foto inline, porque inline vence classe.
    [Fact]
    public void O_VM_carrega_moldura_e_classe_do_jogador()
    {
        var jogador = new Padelizou.Models.Jogador
        {
            Nome = "Teste", FotoPerfil = "/img/x.jpg", MolduraEscolhida = "Campeao",
        };

        var vm = FotoDoJogadorVM.De(jogador, 0, "pdz-chip-avatar");

        Assert.Equal("Campeao", vm.Moldura);
        Assert.Equal(0, vm.Tamanho);
        Assert.Equal("pdz-chip-avatar", vm.Classe);

        // Jogador nulo (dupla sem parceiro) não pode derrubar a tela.
        var vazio = FotoDoJogadorVM.De(null, 48);
        Assert.Null(vazio.FotoUrl);
        Assert.Null(vazio.Moldura);
    }
}
