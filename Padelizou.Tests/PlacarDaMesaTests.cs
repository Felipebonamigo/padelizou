using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A Mesa de Controle funciona sem internet, e a fila do aparelho manda o PLACAR INTEIRO
// (não o "+1" — incremento reentregue dobraria o game). Estes testes prendem a regra de
// aceitação do servidor: o placar marcado por último NA QUADRA vence, sempre.
public class PlacarDaMesaTests
{
    private static readonly DateTime Meiodia = new(2026, 7, 29, 12, 0, 0);

    private static Partida NovaAoVivo() => new()
    {
        Id = 1, Codigo = "T", Status = "AoVivo",
        GamesDupla1 = 2, GamesDupla2 = 1, SetsDupla1 = 0, SetsDupla2 = 0,
    };

    [Fact]
    public void Placar_novo_e_aplicado_e_carimbado()
    {
        var partida = NovaAoVivo();

        var r = PlacarDaMesa.Aplicar(partida, 5, 3, 1, 0, Meiodia);

        Assert.True(r.Aplicado);
        Assert.Equal(5, partida.GamesDupla1);
        Assert.Equal(3, partida.GamesDupla2);
        Assert.Equal(1, partida.SetsDupla1);
        Assert.Equal(Meiodia, partida.PlacarMarcadoEm);
        Assert.True(partida.SendoTransmitida);
    }

    [Fact]
    public void Reenviar_o_mesmo_placar_da_no_mesmo_lugar()
    {
        // É a propriedade que torna a fila offline segura: a rede cai entre o servidor
        // aplicar e o aparelho confirmar, o aparelho reenvia, e NADA dobra.
        var partida = NovaAoVivo();

        PlacarDaMesa.Aplicar(partida, 5, 3, 1, 0, Meiodia);
        var segunda = PlacarDaMesa.Aplicar(partida, 5, 3, 1, 0, Meiodia);

        Assert.False(segunda.Aplicado);           // "já existe um placar mais novo (ou igual)"
        Assert.Equal(5, partida.GamesDupla1);     // e o placar continua exatamente o mesmo
        Assert.Equal(3, partida.GamesDupla2);
    }

    [Fact]
    public void Placar_velho_preso_na_fila_nao_atropela_o_mais_novo()
    {
        // Aparelho A ficou sem sinal com "3x1" na fila; aparelho B (ou o próprio A, depois)
        // já marcou "5x3". Quando o A volta, o 3x1 chega ATRASADO — e tem que ser ignorado.
        var partida = NovaAoVivo();
        PlacarDaMesa.Aplicar(partida, 5, 3, 1, 0, Meiodia);

        var atrasado = PlacarDaMesa.Aplicar(partida, 3, 1, 0, 0, Meiodia.AddMinutes(-10));

        Assert.False(atrasado.Aplicado);
        Assert.Equal(5, partida.GamesDupla1);
    }

    [Fact]
    public void Placar_mais_novo_de_outro_aparelho_passa_por_cima()
    {
        var partida = NovaAoVivo();
        PlacarDaMesa.Aplicar(partida, 5, 3, 1, 0, Meiodia);

        var maisNovo = PlacarDaMesa.Aplicar(partida, 6, 3, 1, 0, Meiodia.AddSeconds(30));

        Assert.True(maisNovo.Aplicado);
        Assert.Equal(6, partida.GamesDupla1);
    }

    [Fact]
    public void Partida_finalizada_nao_aceita_placar_da_fila()
    {
        // Finalizar dispara mata-mata, carimba fase e avisa gente. Um placar velho preso
        // num celular não pode reabrir nada disso — correção de jogo encerrado é outra tela.
        var partida = NovaAoVivo();
        partida.Status = "Finalizada";

        var r = PlacarDaMesa.Aplicar(partida, 9, 0, 2, 0, Meiodia);

        Assert.False(r.Aplicado);
        Assert.Equal(2, partida.GamesDupla1);   // intocado
    }

    [Fact]
    public void Placar_impossivel_e_domado_pras_bordas()
    {
        // O aparelho já trava isso na tela; aqui é o cinto de segurança contra requisição
        // montada à mão. Games prendem em 0..9, sets em 0 pra cima.
        var partida = NovaAoVivo();

        var r = PlacarDaMesa.Aplicar(partida, 50, -3, -1, 2, Meiodia);

        Assert.True(r.Aplicado);
        Assert.Equal(9, partida.GamesDupla1);
        Assert.Equal(0, partida.GamesDupla2);
        Assert.Equal(0, partida.SetsDupla1);
        Assert.Equal(2, partida.SetsDupla2);
    }
}
