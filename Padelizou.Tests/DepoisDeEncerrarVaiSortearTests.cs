using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;

namespace Padelizou.Tests;

// ENCERRAR INSCRIÇÕES CAI NUMA TELA VAZIA (07/09/2026).
//
// Relato do Felipe, no torneio do Er: *"ao clicar em encerrar inscrições, vai para essa tela,
// devia ir para uma tela para gerar chaves"*.
//
// O que acontece: encerrar troca o status pra "Chaves em Sorteio", e é isso que faz
// `torneioComecou` virar true na Details. Aí a aba **Inscritos** perde o `active` e a aba
// **Jogos** — que tem `active` fixo no HTML — assume. O organizador acabou de fechar as
// inscrições e cai em "Nenhum jogo agendado", que é a única tela da página que ainda não tem
// nada pra mostrar. O botão de sortear existe e está a um clique dali, na aba "Gerenciar
// Torneio", mas nada na tela diz isso.
//
// A Details já sabe abrir aba pela hash da URL (o script no fim da view), então o conserto é
// o redirect apontar pra `#admin` em vez de largar o organizador na aba padrão.
public class DepoisDeEncerrarVaiSortearTests
{
    [Fact]
    public async Task Encerrar_inscricoes_leva_o_organizador_pro_painel_de_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Inscrições Abertas");
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var resposta = await controller.EncerrarInscricoes(torneio.Id);

        Assert.Equal(PortaDaInscricao.Fechada, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Details", redirect.ActionName);

        // ⚠️ A hash é o que decide em QUAL aba a página abre. Sem ela o organizador cai na aba
        // Jogos, que neste exato momento está vazia por definição — o sorteio ainda não rodou.
        Assert.Equal("admin", redirect.Fragment);
    }

    [Fact]
    public async Task Quem_encerrou_e_avisado_de_que_o_proximo_passo_e_sortear()
    {
        // Encerrar não dava retorno nenhum: a página recarregava e o organizador ficava sem
        // saber se o clique pegou. O recado precisa dizer o PRÓXIMO PASSO, não repetir o que
        // ele acabou de fazer.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Inscrições Abertas");
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.EncerrarInscricoes(torneio.Id);

        var recado = controller.TempData["Sucesso"] as string;
        Assert.False(string.IsNullOrWhiteSpace(recado));
        Assert.Contains("sorte", recado!, StringComparison.OrdinalIgnoreCase);
    }
}
