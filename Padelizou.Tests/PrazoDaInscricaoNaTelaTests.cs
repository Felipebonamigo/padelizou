using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "ATÉ QUANDO VÃO AS INSCRIÇÕES" — NO CARD DA LISTA, NÃO SÓ NA PÁGINA DO TORNEIO.
//
// 🗣️ Pedido do Felipe, 04/09/2026, olhando a lista de torneios: *"acho que é bom colocar até
// quando vai as inscrições de cada torneio também"*.
//
// O campo `PrevisaoEncerramentoInscricoes` JÁ EXISTIA e já estava preenchido em 3 dos 4
// torneios abertos de produção — ele só não aparecia na lista, que é onde a pessoa decide se
// clica. A frase mora aqui, e não nas views, pelo mesmo motivo que fez `DataDoTorneioNaTela`
// nascer: são DUAS telas mostrando a mesma coisa, e duas cópias divergem.
public class PrazoDaInscricaoNaTelaTests
{
    private static readonly DateTime Hoje = new(2026, 9, 4);

    private static Torneio Torneio(DateTime? prazo, string status = "Inscrições Abertas") => new()
    {
        Id = 1, Nome = "2ª Etapa ER PADEL TOUR (EPT)", Codigo = "EPT2",
        Status = status, PrevisaoEncerramentoInscricoes = prazo,
    };

    // (Havia aqui um `Com_prazo_futuro_diz_a_data` esperando "08/09" para uma data a 4 dias —
    // que é exatamente o caso em que a frase conta os DIAS. Ele contradizia o
    // `Faltando_poucos_dias_a_frase_conta_os_dias` logo abaixo sobre a MESMA entrada, e o caso
    // que ele queria cobrir já é o `Prazo_distante_nao_conta_dias`. Foi removido em vez de
    // remendado: dois testes brigando pela mesma entrada é um deles mentindo.)

    [Fact]
    public void Sem_prazo_preenchido_nao_diz_nada()
    {
        // É o caso do "Americano das gurias - 2ª edição" hoje em produção. Card sem a linha é
        // melhor do que card com "a definir": o organizador não prometeu data nenhuma.
        Assert.Null(PrazoDaInscricaoNaTela.Frase(Torneio(null), Hoje));
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Chaves em Aprovação")]
    [InlineData("Fase de Grupos")]
    [InlineData("Finalizado")]
    [InlineData("Cancelado")]
    public void Torneio_que_nao_esta_aberto_nao_fala_de_prazo(string status)
    {
        // "Inscrições até 08/09" num torneio que já começou é informação morta ocupando a
        // linha — e em um FINALIZADO chega a confundir quem lê rápido.
        Assert.Null(PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 9, 8), status), Hoje));
    }

    [Fact]
    public void Prazo_que_JA_PASSOU_cala()
    {
        // ⚠️ O caso que importa e o menos óbvio: a previsão passou e o organizador NÃO fechou
        // as inscrições — quem encerra é ele, no botão, e a data aqui é promessa, não gatilho.
        // A inscrição continua ABERTA, então dizer "inscrições até 08/09" no dia 09 desanima
        // quem ainda pode entrar. O certo é calar, não mentir nem contradizer o botão.
        var frase = PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 9, 1)), Hoje);

        Assert.Null(frase);
    }

    [Fact]
    public void No_ULTIMO_dia_ainda_fala()
    {
        // O dia do prazo ainda é dia de inscrever. Cortar aqui tiraria a frase justamente no
        // dia em que ela mais empurra alguém a clicar.
        var frase = PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 9, 4)), Hoje);

        Assert.NotNull(frase);
        Assert.Contains("hoje", frase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Faltando_poucos_dias_a_frase_conta_os_dias()
    {
        // No card a pessoa está DECIDINDO se clica. "Faltam 4 dias" move mais do que uma data
        // que ela precisa comparar de cabeça com o calendário.
        var frase = PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 9, 8)), Hoje);

        Assert.Contains("4 dias", frase);
    }

    [Fact]
    public void Amanha_e_falado_como_amanha()
    {
        var frase = PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 9, 5)), Hoje);

        Assert.Contains("amanhã", frase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prazo_distante_nao_conta_dias()
    {
        // "Faltam 61 dias" não é urgência, é ruído — a essa distância a data seca informa mais.
        var frase = PrazoDaInscricaoNaTela.Frase(
            Torneio(new DateTime(2026, 11, 8)), Hoje);

        Assert.Contains("08/11", frase);
        Assert.DoesNotContain("dias", frase);
    }
}
