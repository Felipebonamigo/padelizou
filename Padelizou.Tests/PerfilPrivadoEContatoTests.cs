using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// O QUE A CHAVE "PERFIL PRIVADO" ESCONDE — e o que ela NUNCA escondeu.
//
// Duas coisas diferentes moravam na mesma chave. O perfil inteiro sumia pra quem só queria
// esconder o telefone, e a frase da tela de preferências prometia exatamente isso ("quem
// visitar seu perfil só vê sua foto e seu nome").
//
// A régua é do Felipe (10/08/2026): "todos os dados públicos aparecem mesmo para quem tem
// perfil privado — se foi campeão, pontos do ranking, etc.; o perfil privado é para evitar
// ver o Instagram, o WhatsApp da pessoa". Resultado de padel já está no ranking, na chave do
// torneio e no histórico do parceiro: escondê-lo no perfil não esconderia nada de ninguém.
//
// ⚠️ O que continua fechando o perfil INTEIRO é a CONTA EXCLUÍDA (LGPD) — e ela liga o
// `PerfilPrivado` junto (ver Services/ExclusaoDeConta), que foi como as duas regras se
// misturaram. Por isso o bloqueio olha `ExcluidoEm`, não a chave que a pessoa aperta.
public class PerfilPrivadoEContatoTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Quem Visita", Cpf = "1" },
            new Jogador
            {
                Id = 2, Nome = "Marina Reservada", Cpf = "2",
                PerfilPrivado = true, Instagram = "marina.padel", Celular = "51999990000",
            },
            new Jogador
            {
                Id = 3, Nome = "Jogador removido", Cpf = "3",
                PerfilPrivado = true, ExcluidoEm = new DateTime(2026, 8, 1),
            });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<JogadoresController> PerfilAsync(DbPadelContext ctx, int deQuem, int? logadoComo)
    {
        var controller = TestInfra.NovoJogadoresController(ctx, logadoComo);
        Assert.IsType<ViewResult>(await controller.Perfil(deQuem));
        return controller;
    }

    [Fact]
    public async Task Perfil_privado_esconde_o_contato_pra_quem_visita()
    {
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 1);

        Assert.True(controller.ViewBag.ContatoEscondido);
    }

    [Fact]
    public async Task Perfil_privado_NAO_esconde_o_resto_do_perfil()
    {
        // ⚠️ O teste que segura a régua: o perfil não pode mais parar antes de calcular. Se
        // alguém reintroduzir o retorno antecipado, `Pontos` volta a ser nulo aqui.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 1);

        // Nulo, não `false`: o ViewBag só ganha essa chave quando o perfil FECHA.
        Assert.Null(controller.ViewBag.PerfilBloqueado);
        Assert.NotNull(controller.ViewBag.Pontos);
        Assert.NotNull(controller.ViewBag.Titulos);
        Assert.NotNull(controller.ViewBag.Conquistas);
        // Os números da rede também: seguidores são tão públicos quanto os pontos.
        Assert.NotNull(controller.ViewBag.QuantosSeguidores);
    }

    [Fact]
    public async Task O_DONO_continua_vendo_o_proprio_contato()
    {
        // A chave é sobre quem VISITA. Esconder do dono seria esconder dele o que ele mesmo
        // cadastrou — e ele não teria como conferir se está certo.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 2);

        Assert.False(controller.ViewBag.ContatoEscondido);
    }

    [Fact]
    public async Task Conta_excluida_continua_com_o_perfil_FECHADO()
    {
        // Aqui o sumiço é o ponto: a pessoa pediu pra sair e deixou de ser identificável.
        var controller = await PerfilAsync(Cenario(), deQuem: 3, logadoComo: 1);

        Assert.True(controller.ViewBag.PerfilBloqueado);
        // E o perfil PARA antes de calcular qualquer coisa — este é o sinal de que parou.
        Assert.Null(controller.ViewBag.Pontos);
    }

    [Fact]
    public async Task Visitante_deslogado_tambem_nao_ve_o_contato()
    {
        // Deslogado é o caso que mais importa: é o link que circula em grupo de WhatsApp.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: null);

        Assert.True(controller.ViewBag.ContatoEscondido);
        Assert.NotNull(controller.ViewBag.Pontos);
    }
}
