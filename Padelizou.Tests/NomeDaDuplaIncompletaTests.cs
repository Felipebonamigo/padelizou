using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O NOME DE UMA DUPLA COM A VAGA EM ABERTO, nos textos que ela agora alcança.
//
// Enquanto a dupla sem parceiro ficava FORA do sorteio, ela nunca aparecia em jogo nenhum — e
// por isso todo texto que junta "jogador1 e jogador2" foi escrito supondo dois nomes. Desde
// 09/09/2026 ela entra na chave, então esses textos passaram a ser alcançáveis com metade dos
// dados, e dois deles produziam frase quebrada:
//
//   • o push de fim de jogo: "Vocês venceram Paulo e  (6x2)" — o `e` pendurado no vazio;
//   • o "seu próximo jogo" da home: a concatenação é traduzida pra SQL, e no Postgres
//     `'Paulo' || ' e ' || NULL` é NULL — o adversário sumiria inteiro, não só o parceiro.
//     O EF InMemory da suíte concatena em C# e devolve "Paulo e ", então nem o defeito nem o
//     conserto apareceriam aqui sem este teste.
//
// ⚠️ O QUE ESTES TESTES DELIBERADAMENTE NÃO MUDAM: `NomeDaDupla` e `Dupla.NomeDeExibicao`.
// `Jogador2Id == null` significa DUAS coisas no sistema — inscrição com vaga aberta E campeão
// individual do Americano (EncerramentoDaPartida cria essa linha na coroação) —, e lá o texto
// de hoje ("só o nome") está certo pros dois. Mexer na régua central escreveria "e parceiro"
// no card de campeão do Americano.
public class NomeDaDuplaIncompletaTests
{
    [Fact]
    public async Task Push_de_fim_de_jogo_nao_deixa_o_e_pendurado_no_vazio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.Status = "Fase de Grupos";

        var solo = new Jogador { Nome = "Paulo Prass", Cpf = "11144477735" };
        ctx.Jogadores.Add(solo);
        await ctx.SaveChangesAsync();
        var incompleta = new Dupla { CategoriaId = categoria.Id, Jogador1Id = solo.Id, Jogador2Id = null };
        ctx.Duplas.Add(incompleta);
        await ctx.SaveChangesAsync();

        var fechada = await ctx.Duplas.FirstAsync(d => d.Jogador2Id != null);
        var partida = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = fechada.Id, Dupla2Id = incompleta.Id, Status = "Agendada",
            GamesDupla1 = 6, GamesDupla2 = 0, SetsDupla1 = 1, SetsDupla2 = 0,
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoTorneiosController(ctx, org.Id, push: push).FinalizarPartida(partida.Id);

        // O corpo do push cita a dupla perdedora. Com a vaga em aberto, ele precisa dizer só
        // o nome de quem existe — nunca "Paulo e " com o parceiro faltando.
        var corpos = push.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<string>()
            .ToList();

        Assert.DoesNotContain(corpos, c => c != null && c.Contains("Paulo Prass e "));
        Assert.DoesNotContain(corpos, c => c != null && c.Contains(" e  "));
    }
}
