using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// ELOGIO, COMENTÁRIO E SEGUIDOR NOVO CAEM NA CAIXA DE ENTRADA.
//
// Até aqui as três coisas aconteciam em silêncio: alguém elogiava, comentava ou passava a
// seguir, e a pessoa só descobria se voltasse ao próprio perfil por acaso.
//
// ⚠️ Os três avisam só no NOVO. Trocar o elogio, editar o comentário e re-seguir depois de
// deixar de seguir são as três formas óbvias de transformar isto em cutucão repetido — e
// quem paga é sempre a mesma pessoa, a que recebe.
public class AvisosDoPerfilTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "CARLOS EDUARDO DA SILVA", Apelido = "Cadu", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Quem Recebe", Cpf = "2" });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Elogio_novo_avisa_quem_recebeu_com_o_NOME_de_quem_deu()
    {
        // Aviso sem nome não serve pra nada: "alguém te elogiou" só gera a pergunta "quem?".
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).DarElogio(2, "BoaBandeja");

        await push.Received(1).EnviarParaJogadorAsync(
            2,
            Arg.Is<string>(t => t.Contains("elogio")),
            Arg.Is<string>(c => c.Contains("Cadu") && c.Contains("Boa Bandeja")),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Trocar_o_elogio_NAO_avisa_de_novo()
    {
        // É a MESMA pessoa mexendo no MESMO elogio. Avisar a cada troca contaria a mesma
        // coisa três vezes e deixaria a caixa de entrada de alguém na mão de quem fica
        // clicando.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).DarElogio(2, "BoaBandeja");
        await TestInfra.NovoJogadoresController(ctx, 1, push).DarElogio(2, "Garra");

        await push.Received(1).EnviarParaJogadorAsync(
            2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Comentario_novo_avisa_com_o_texto_junto()
    {
        // O trecho vai no corpo porque um aviso que diz só "comentaram no seu perfil" obriga
        // a abrir o site pra saber se é bom ou ruim.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).ComentarPerfil(2, "Jogaço ontem, parceiro!");

        await push.Received(1).EnviarParaJogadorAsync(
            2,
            Arg.Is<string>(t => t.Contains("comentário")),
            Arg.Is<string>(c => c.Contains("Cadu") && c.Contains("Jogaço ontem")),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Editar_o_comentario_NAO_avisa_de_novo()
    {
        // Corrigir um acento não é notícia pro dono do perfil.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).ComentarPerfil(2, "Jogou muito");
        await TestInfra.NovoJogadoresController(ctx, 1, push).ComentarPerfil(2, "Jogou muito!");

        await push.Received(1).EnviarParaJogadorAsync(
            2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Comentario_RECUSADO_nao_avisa_ninguem()
    {
        // Palavrão barrado pelo filtro não grava nada — e um aviso dizendo que comentaram no
        // perfil de alguém, sem comentário nenhum lá, é pior que o silêncio.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).ComentarPerfil(2, "   ");

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    [Fact]
    public async Task Seguidor_novo_avisa_quem_passou_a_ser_seguido()
    {
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).Seguir(2);

        await push.Received(1).EnviarParaJogadorAsync(
            2,
            Arg.Is<string>(t => t.Contains("seguidor")),
            Arg.Is<string>(c => c.Contains("Cadu")),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Seguir_de_novo_quem_ja_sigo_nao_avisa()
    {
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).Seguir(2);
        await TestInfra.NovoJogadoresController(ctx, 1, push).Seguir(2);

        await push.Received(1).EnviarParaJogadorAsync(
            2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Deixar_de_seguir_NAO_avisa()
    {
        // A única das quatro ações que a outra pessoa preferia não saber.
        var ctx = Cenario();
        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = 1, SeguidoId = 2 });
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoJogadoresController(ctx, 1, push).DeixarDeSeguir(2);

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    [Fact]
    public async Task Os_tres_avisos_saem_SEM_email()
    {
        // ⚠️ São bilhetes sociais: bons de ver, mas ninguém age por causa deles. E-mail a cada
        // elogio é o que faz a pessoa marcar o remetente como lixo — e aí ela perde junto o
        // aviso de que a chave do torneio saiu.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        var controller = TestInfra.NovoJogadoresController(ctx, 1, push);
        await controller.DarElogio(2, "Garra");
        await controller.ComentarPerfil(2, "Muito bom");
        await controller.Seguir(2);

        await push.Received(3).EnviarParaJogadorAsync(
            2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Ninguem_se_avisa_sozinho()
    {
        // As três ações já eram barradas pra si mesmo; o teste existe pra que o aviso não
        // reabra o caminho por fora da guarda.
        var ctx = Cenario();
        var push = Substitute.For<IPushNotificationService>();

        var controller = TestInfra.NovoJogadoresController(ctx, 1, push);
        await controller.DarElogio(1, "Garra");
        await controller.ComentarPerfil(1, "Eu sou demais");
        await controller.Seguir(1);

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }
}
