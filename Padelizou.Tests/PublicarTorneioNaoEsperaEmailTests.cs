using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// PUBLICAR UM TORNEIO NÃO ESPERA E-MAIL NENHUM.
//
// ⚠️ Relato do Felipe em 07/08/2026, na véspera do lançamento: *"está demorando bastante para
// publicar o torneio"* — o botão ficou em "Publicando o torneio…" por minutos. A criação
// avisava quem tem `NotificarTorneiosAbertos` com um laço de SMTP DENTRO da requisição: 71
// jogadores em produção, uma conexão com o Gmail por pessoa, tudo antes de a tela voltar.
//
// E havia um agravante: a fila de avisos JÁ manda e-mail pra quem tem `NotificarEmail`. O laço
// inline era e-mail em DOBRO — foi assim que a cota diária do Gmail estourou no mesmo dia
// ("Daily user sending limit exceeded" no log de produção).
//
// A mesma armadilha já tinha derrubado o "finalizar jogo" em 05/08 (ver
// AvisoNaoSeguraOCliqueTests). Voltou porque a correção foi feita numa cópia da regra e não
// nas outras — a família de defeito mais cara deste projeto.
public class PublicarTorneioNaoEsperaEmailTests
{
    [Fact]
    public async Task Criar_torneio_enfileira_os_avisos_em_vez_de_abrir_SMTP()
    {
        using var ctx = TestInfra.NovoContexto();
        // `NotificarTorneiosAbertos` nasce LIGADO no modelo — desligado aqui só pra a conta
        // do teste ser exatamente os três interessados, sem o próprio organizador no meio.
        ctx.Jogadores.Add(new Jogador
        {
            Id = 1, Nome = "Organizador", Cpf = "1", NotificarTorneiosAbertos = false,
            IsOrganizadorTorneio = true,
        });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });

        // Quem aprova. Desde 07/08/2026 é ELE que recebe aviso na criação — o torneio entrou
        // na fila dele.
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Admin", Cpf = "2", IsAdminRaiz = true,
            NotificarTorneiosAbertos = false,
        });

        // Três pessoas querendo saber de torneio novo — em produção eram 71.
        for (int i = 0; i < 3; i++)
        {
            ctx.Jogadores.Add(new Jogador
            {
                // Id na mão: o organizador já ocupou o 1, e o gerador do banco em memória
                // recomeçaria do 1 pra quem entra sem Id.
                Id = 10 + i,
                Nome = $"Interessado {i}", Cpf = $"9990000000{i}",
                Email = $"interessado{i}@exemplo.com", NotificarEmail = true,
                NotificarTorneiosAbertos = true,
            });
        }
        await ctx.SaveChangesAsync();

        var email = Substitute.For<IEmailService>();
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var push = PushDeVerdade(ctx, fila, email);

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1, push: push);

        await controller.Create(new Torneio
        {
            Nome = "Copa Teste",
            ClubeId = 1,
            Status = "Inscrições Abertas",
            SetsFaseGrupos = 1,
            GamesFaseGrupos = 6,
            RestricaoCategoria = "Livre",
            FormaPagamento = "Externo",
        }, new[] { 3 }, null, null, null, null);

        // O torneio nasceu...
        Assert.Single(ctx.Torneios);

        // ...e o ÚNICO aviso enfileirado é o do admin, que precisa aprovar.
        //
        // ⚠️ Os três interessados NÃO são avisados aqui: desde 07/08/2026 o "novo torneio
        // aberto" saiu da criação e passou pra APROVAÇÃO. Avisar na criação entregaria à base
        // inteira justamente o torneio que ninguém olhou ainda.
        Assert.Equal(1, fila.Pendentes);
        Assert.True(fila.TentarLer(out var aviso));
        Assert.Equal(2, aviso!.JogadorId);
        Assert.Contains("aprova", aviso.Titulo, StringComparison.OrdinalIgnoreCase);

        // E NADA de rede aconteceu no caminho de quem clicou em "Publicar" — que é o que este
        // teste existe pra garantir desde o começo.
        await email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);
    }

    // Push de verdade (não dublê): é ele que decide enfileirar em vez de entregar, e é
    // justamente essa decisão que o teste quer ver acontecendo.
    private static PushNotificationService PushDeVerdade(
        DbPadelContext ctx, FilaDeAvisos fila, IEmailService email) =>
        new(ctx,
            Microsoft.Extensions.Options.Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            fila,
            email,
            Microsoft.Extensions.Options.Options.Create(new SiteSettings { Url = "https://padelizou.com.br" }),
            PorteiroDeTeste.Com(),
            NullLogger<PushNotificationService>.Instance);
}
