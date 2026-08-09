using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 09/08/2026 — dois tipos de aviso pararam de mandar e-mail, por decisão do Felipe depois de a
// cota do Gmail estourar e derrubar 130 e-mails num dia (duas recuperações de senha entre eles).
//
// Saíram do e-mail: o RESULTADO DE PARTIDA (pros 4 que jogaram e pros seguidores deles) e o
// "alguém que você segue se inscreveu". Os dois são rajada — um jogo avisa 4 pessoas mais os
// seguidores de cada uma, e uma rodada inteira termina junto —, e nenhum dos dois pede ação:
// quem jogou estava na quadra e já sabe o placar.
//
// Push e caixa de entrada seguem valendo. O que morreu foi só o canal caro.
public class AvisoQueNaoGastaEmailTests
{
    [Fact]
    public async Task O_resultado_da_partida_nao_manda_email_pra_ninguem()
    {
        var (ctx, partida, torcedor, push) = CenarioDeFinalComTorcedor();

        await TestInfra.NovoEncerramento(ctx, push).AplicarAsync(
            partida, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso("/Torneios/Details/1", "/Torneios/Jogos/1"));

        // A guarda que importa: NADA daqui pode sair no alcance que inclui e-mail.
        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.SoApp);

        // 4 em quadra + 1 torcedor, todos ainda avisados — o corte é do canal, não do aviso.
        await push.Received(5).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);

        await push.Received(1).EnviarParaJogadorAsync(
            torcedor.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
    }

    // ⚠️ O gancho de seguidores existe em DUAS cópias (inscrição em dupla e individual), e o
    // comentário de cada uma diz isso. Consertar só uma é o defeito clássico deste projeto:
    // metade dos inscritos continuaria gastando cota, e por um caminho que ninguém reabre pra
    // conferir. Este teste lê o CÓDIGO-FONTE porque é a única forma de amarrar as duas de uma
    // vez sem montar o fluxo inteiro de inscrição duas vezes — mesmo recurso já usado em
    // AssistenteDoSistemaTests.
    [Theory]
    [InlineData("DuplasController.cs")]
    [InlineData("TorneiosController.cs")]
    public void As_duas_copias_do_aviso_de_seguidor_saem_sem_email(string arquivo)
    {
        var chamada = ChamadaDeAvisoDoGanchoDeSeguidores(arquivo);

        Assert.Contains("AlcanceDoAviso.AppSemEmail", chamada);
    }

    // Monta uma FINAL entre duas duplas, com um quinto jogador que segue um dos que jogam.
    // Final de propósito: é o caminho que aciona também o texto de campeão, e não há jogo
    // "Agendada" no cenário, então o "seu jogo é o próximo" (esse sim vai no WhatsApp) não
    // entra na contagem.
    private static (DbPadelContext ctx, Partida partida, Jogador torcedor, IPushNotificationService push)
        CenarioDeFinalComTorcedor()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, 2);

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var torcedor = new Jogador { Nome = "Torcedor", Cpf = "99900000098" };
        ctx.Jogadores.Add(torcedor);
        ctx.SaveChanges();

        ctx.SeguidoresJogador.Add(new SeguidorJogador
        {
            SeguidorId = torcedor.Id,
            SeguidoId = duplas[0].Jogador1Id,
        });

        var partida = new Partida
        {
            Codigo = "FIN1",
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Fase = "Final",
            Status = "Finalizada",
            VencedorId = duplas[0].Id,
            GamesDupla1 = 6,
            GamesDupla2 = 3,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        return (ctx, partida, torcedor, Substitute.For<IPushNotificationService>());
    }

    private static string ChamadaDeAvisoDoGanchoDeSeguidores(string arquivo)
    {
        var texto = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Controllers", arquivo));

        var gancho = texto.IndexOf("NotificarSeguidoresDeInscricaoAsync(int torneioId", StringComparison.Ordinal);
        Assert.True(gancho >= 0,
            $"{arquivo}: não achei o gancho de seguidores. Se ele foi renomeado, atualize este teste — "
            + "não o apague, é ele que impede uma das duas cópias de voltar a mandar e-mail.");

        var envio = texto.IndexOf("EnviarParaJogadorAsync(", gancho, StringComparison.Ordinal);
        Assert.True(envio >= 0, $"{arquivo}: o gancho de seguidores não avisa mais ninguém?");

        // Do nome da chamada até o fecha-parênteses dela: é aí que o alcance viaja.
        var fim = texto.IndexOf(");", envio, StringComparison.Ordinal);
        return texto[envio..fim];
    }

    // Sobe do diretório de saída dos testes até achar o projeto web.
    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Controllers");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Não achei a pasta do projeto subindo a partir de {AppContext.BaseDirectory}");
    }
}
