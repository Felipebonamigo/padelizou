using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using padelizou.Models;   // namespace legado (minúsculo) — CategoriaPadrao mora aqui
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// PULAR UM PASSO DOS "PRIMEIROS PASSOS" — 14/09/2026.
//
// 🗣️ Feedback de usuário repassado pelo Felipe: *"nos primeiros passos, por exemplo se eu não
// quero seguir ninguém, posso dar um 'Skip' no item"*.
//
// 🕳️ Os 5 passos são derivados do dado e não tinham saída. O "Instale o app no celular" só
// conclui com `InstalouAppEm` ou uma PushSubscription — quem usa só no computador NUNCA
// concluía, e o cartão ficava na Home e no Perfil para sempre.
public class PularPassoDoOnboardingTests
{
    private static OnboardingController NovoController(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new OnboardingController(ctx)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }

    private static Jogador NovoJogador(DbPadelContext ctx, string nome = "Novato", string cpf = "1")
    {
        var j = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    private static void Pulou(DbPadelContext ctx, int jogadorId, PassoDoOnboarding passo)
    {
        ctx.PassosPuladosDoOnboarding.Add(new PassoPuladoDoOnboarding { JogadorId = jogadorId, Passo = passo });
        ctx.SaveChanges();
    }

    // ---------- a régua: nem todo passo tem saída ----------

    // ⚖️ Decisão do Felipe, perguntado: só "Siga outros jogadores" e "Instale o app" podem ser
    // pulados. Perfil, categoria e torneio são o que faz o app saber o que sugerir — e são os
    // três que o cartão existe pra cobrar.
    [Theory]
    [InlineData(PassoDoOnboarding.Seguir, true)]
    [InlineData(PassoDoOnboarding.InstalarApp, true)]
    [InlineData(PassoDoOnboarding.Perfil, false)]
    [InlineData(PassoDoOnboarding.Categoria, false)]
    [InlineData(PassoDoOnboarding.Torneio, false)]
    public void So_seguir_e_instalar_o_app_podem_ser_pulados(PassoDoOnboarding passo, bool esperado)
    {
        Assert.Equal(esperado, PulosDoOnboarding.PodeSerPulado(passo));
    }

    // ---------- o cartão ----------

    [Fact]
    public async Task Passo_pulado_sai_da_lista_e_o_total_cai()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);
        Pulou(ctx, j.Id, PassoDoOnboarding.Seguir);

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);

        Assert.Equal(4, vm.Total);
        Assert.DoesNotContain(vm.Passos, p => p.Chave == PassoDoOnboarding.Seguir);
    }

    // O percentual passa a ser sobre o que sobrou. Se o pulado continuasse no denominador, a
    // barra nunca fecharia — que é o defeito que estamos consertando.
    [Fact]
    public async Task O_percentual_conta_so_o_que_sobrou()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador
        {
            Nome = "Meio", Cpf = "1",
            FotoPerfil = "/uploads/foto.png", Cidade = "Caxias do Sul", LadoQuadra = "Direita",
        };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();

        // 5 passos, 1 concluído (o perfil) → 20%. Pulando um dos 4 pendentes: 1 de 4 → 25%.
        Assert.Equal(20, (await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id)).Percentual);

        Pulou(ctx, j.Id, PassoDoOnboarding.InstalarApp);

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);
        Assert.Equal(4, vm.Total);
        Assert.Equal(1, vm.Concluidos);
        Assert.Equal(25, vm.Percentual);
    }

    // O caso que motivou tudo: quem usa só no computador nunca conclui "Instale o app".
    [Fact]
    public async Task Pulando_o_ultimo_pendente_o_cartao_some()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador
        {
            Nome = "Só no computador", Cpf = "1",
            FotoPerfil = "/uploads/foto.png", Cidade = "Caxias do Sul", LadoQuadra = "Esquerda",
        };
        var outro = NovoJogador(ctx, "Outro", "2");
        ctx.Jogadores.Add(j);
        ctx.CategoriasPadrao.Add(new CategoriaPadrao { Nome = "2ª Masculina", Codigo = "2M", Tipo = "Masculina" });
        ctx.SaveChanges();

        var catPadrao = ctx.CategoriasPadrao.First();
        ctx.JogadorCategorias.Add(new JogadorCategoria { JogadorId = j.Id, CategoriaPadraoId = catPadrao.Id });

        var torneio = new Torneio { Nome = "T", Codigo = "T", Status = "Finalizado" };
        var cat = new Categoria { Nome = "2ª Masculina", Codigo = "TC", Torneio = torneio };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = j.Id, Jogador2Id = outro.Id });
        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = j.Id, SeguidoId = outro.Id });
        ctx.SaveChanges();

        // Perfil, categoria, seguir e torneio feitos; só o app falta e nunca vai vir.
        var antes = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);
        Assert.False(antes.Completo);

        Pulou(ctx, j.Id, PassoDoOnboarding.InstalarApp);

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);
        Assert.True(vm.Completo);
        Assert.Null(vm.Proximo);
    }

    // Pulando os dois puláveis com nada feito, sobram os 3 obrigatórios: o cartão CONTINUA.
    // É o que garante que "pular" não virou "sumir com o guia".
    [Fact]
    public async Task Pular_os_dois_pulaveis_nao_apaga_os_tres_obrigatorios()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);
        Pulou(ctx, j.Id, PassoDoOnboarding.Seguir);
        Pulou(ctx, j.Id, PassoDoOnboarding.InstalarApp);

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);

        Assert.Equal(3, vm.Total);
        Assert.False(vm.Completo);
        Assert.Equal(PassoDoOnboarding.Perfil, vm.Proximo!.Chave);
    }

    // Pular vence: o passo saiu por escolha dela, e ressuscitá-lo seria o app discordando do
    // que ela mandou — o cartão voltaria do nada depois de ela ter seguido alguém.
    [Fact]
    public async Task Pulado_que_depois_e_cumprido_continua_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);
        var outro = NovoJogador(ctx, "Outro", "2");
        Pulou(ctx, j.Id, PassoDoOnboarding.Seguir);

        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = j.Id, SeguidoId = outro.Id });
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);

        Assert.DoesNotContain(vm.Passos, p => p.Chave == PassoDoOnboarding.Seguir);
    }

    [Fact]
    public async Task O_pulo_de_um_jogador_nao_mexe_no_cartao_do_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var meu = NovoJogador(ctx, "Meu", "1");
        var alheio = NovoJogador(ctx, "Alheio", "2");
        Pulou(ctx, alheio.Id, PassoDoOnboarding.Seguir);

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(meu.Id);

        Assert.Equal(5, vm.Total);
        Assert.Contains(vm.Passos, p => p.Chave == PassoDoOnboarding.Seguir);
    }

    // ---------- o POST ----------

    [Fact]
    public async Task Pular_grava_para_o_jogador_do_claim()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);

        await NovoController(ctx, j.Id).Pular("Seguir");

        var pulado = Assert.Single(ctx.PassosPuladosDoOnboarding);
        Assert.Equal(j.Id, pulado.JogadorId);
        Assert.Equal(PassoDoOnboarding.Seguir, pulado.Passo);
    }

    // ⚠️ ESCONDER NÃO É FECHAR: o botão só existe nos dois passos puláveis, mas o POST é
    // montado à mão em três segundos. A régua tem que estar no servidor.
    [Fact]
    public async Task Pular_recusa_o_passo_que_nao_pode_ser_pulado()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);

        await NovoController(ctx, j.Id).Pular("Perfil");

        Assert.Empty(ctx.PassosPuladosDoOnboarding);
    }

    // ⚠️ Chave lixo não pode bindar em 0 (= Perfil) e pular o passo errado, calado. Por isso
    // `passo` chega como string e passa por Enum.TryParse, em vez de bindar como enum.
    [Fact]
    public async Task Pular_com_chave_desconhecida_nao_grava_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);

        await NovoController(ctx, j.Id).Pular("NaoExiste");

        Assert.Empty(ctx.PassosPuladosDoOnboarding);
    }

    // ⚠️ O toque duplo manda dois POSTs. A trava DE VERDADE é a chave composta; este teste
    // cobra que o segundo POST não estoure na cara de quem conseguiu o que queria.
    [Fact]
    public async Task Pular_duas_vezes_o_mesmo_passo_nao_duplica_nem_estoura()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);

        await NovoController(ctx, j.Id).Pular("Seguir");
        await NovoController(ctx, j.Id).Pular("Seguir");

        Assert.Single(ctx.PassosPuladosDoOnboarding);
    }

    [Fact]
    public async Task Mostrar_todos_devolve_a_lista_inteira()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);
        Pulou(ctx, j.Id, PassoDoOnboarding.Seguir);
        Pulou(ctx, j.Id, PassoDoOnboarding.InstalarApp);

        await NovoController(ctx, j.Id).MostrarTodos();

        Assert.Empty(ctx.PassosPuladosDoOnboarding);
        Assert.Equal(5, (await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id)).Total);
    }

    // ⚠️ "Mostrar de novo" é o botão do Preferências, e ele não pode virar faxina na conta
    // alheia: a linha é achada pelo jogador da claim, e não existe parâmetro por onde pedir
    // outro.
    [Fact]
    public async Task Mostrar_todos_nao_mexe_no_pulo_de_outro_jogador()
    {
        using var ctx = TestInfra.NovoContexto();
        var meu = NovoJogador(ctx, "Meu", "1");
        var alheio = NovoJogador(ctx, "Alheio", "2");
        Pulou(ctx, meu.Id, PassoDoOnboarding.Seguir);
        Pulou(ctx, alheio.Id, PassoDoOnboarding.Seguir);

        await NovoController(ctx, meu.Id).MostrarTodos();

        var sobrou = Assert.Single(ctx.PassosPuladosDoOnboarding);
        Assert.Equal(alheio.Id, sobrou.JogadorId);
    }

    // ⚠️ LGPD: "não quero seguir ninguém" e "não vou instalar o app" são escolhas DELA sobre o
    // próprio uso — a mesma natureza das preferências e do quem-ela-seguia que o ExcluirConta já
    // apaga, e mantê-las seria continuar perfilando quem pediu pra sair.
    [Fact]
    public async Task Quem_exclui_a_conta_nao_deixa_os_pulos_pra_tras()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "Saindo", Cpf = "1" };
        j.SenhaHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Jogador>().HashPassword(j, "segredo");
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        Pulou(ctx, j.Id, PassoDoOnboarding.Seguir);

        await TestInfra.NovoAuthController(ctx, j.Id).ExcluirConta("segredo", confirmo: true);

        Assert.NotNull(ctx.Jogadores.Find(j.Id)!.ExcluidoEm);
        Assert.Empty(ctx.PassosPuladosDoOnboarding);
    }

    // ⚠️ Lista branca, e não a URL que vier no formulário: o partial vive em duas telas e o
    // botão de repor numa terceira, então o destino precisa viajar no POST — e destino vindo
    // do formulário usado como URL é redirect aberto.
    [Theory]
    [InlineData("perfil", "Perfil", "Auth")]
    [InlineData("preferencias", "Preferencias", "Auth")]
    [InlineData(null, "Index", "Home")]
    [InlineData("https://sitedosoutros.com", "Index", "Home")]
    public async Task O_destino_da_volta_vem_de_uma_lista_branca(string? de, string acao, string controlador)
    {
        using var ctx = TestInfra.NovoContexto();
        var j = NovoJogador(ctx);

        var resultado = Assert.IsType<RedirectToActionResult>(
            await NovoController(ctx, j.Id).Pular("Seguir", de));

        Assert.Equal(acao, resultado.ActionName);
        Assert.Equal(controlador, resultado.ControllerName);
    }
}
