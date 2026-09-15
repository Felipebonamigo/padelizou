using Microsoft.EntityFrameworkCore;
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
        // montada à mão. Games prendem no teto de 9.
        //
        // ⚠️ NEGATIVO NÃO É MAIS ZERO (12/09/2026): ele passou a significar "não toquei neste
        // lado" (ver `Lado_nao_tocado_fica_como_esta`), e o lado fica com o que está gravado.
        // Como cinto de segurança isso é MAIS forte, não menos: um POST montado à mão com -1
        // agora não apaga placar nenhum, onde antes zerava o lado.
        var partida = NovaAoVivo();

        var r = PlacarDaMesa.Aplicar(partida, 50, -3, -1, 2, Meiodia);

        Assert.True(r.Aplicado);
        Assert.Equal(9, partida.GamesDupla1);
        Assert.Equal(1, partida.GamesDupla2);   // intocado, como estava
        Assert.Equal(0, partida.SetsDupla1);    // idem
        Assert.Equal(2, partida.SetsDupla2);
    }

    // ═══ DOIS APARELHOS NA MESMA MESA ═══════════════════════════════════════════════════════

    [Fact]
    public void Lado_nao_tocado_fica_como_esta()
    {
        // 🗣️ Felipe: *"quando um de um lado marcava e o outro junto as vezes, um deles nao
        // pegava"*. Na Mesa vale o mesmo que na lista AO VIVO: a fila mandava o placar INTEIRO
        // em todo toque, então o aparelho do vizinho reescrevia o lado que ninguém tinha
        // tocado com o número que a tela DELE tinha.
        //
        // -1 = "não toquei neste": o lado fica com o que está gravado.
        var partida = NovaAoVivo();
        partida.SetsDupla1 = 1;

        var r = PlacarDaMesa.Aplicar(partida, 5, -1, -1, -1, Meiodia);

        Assert.True(r.Aplicado);
        Assert.Equal(5, partida.GamesDupla1);
        Assert.Equal(1, partida.GamesDupla2);   // o lado 2 não foi tocado
        Assert.Equal(1, partida.SetsDupla1);    // nem os sets
        Assert.Equal(0, partida.SetsDupla2);
    }

    [Fact]
    public void Um_marcador_de_cada_lado_nao_apaga_o_trabalho_do_outro()
    {
        var partida = NovaAoVivo();

        // A toca no + da dupla 1; B, com a tela de antes, toca no + da dupla 2 logo depois.
        PlacarDaMesa.Aplicar(partida, 3, -1, -1, -1, Meiodia);
        PlacarDaMesa.Aplicar(partida, -1, 2, -1, -1, Meiodia.AddSeconds(2));

        Assert.Equal(3, partida.GamesDupla1);
        Assert.Equal(2, partida.GamesDupla2);
    }

    [Fact]
    public void Lado_negativo_nao_vira_zero()
    {
        // O -1 é "não toquei", e não "apague" — sem a leitura certa ele cairia no clamp e
        // zeraria o placar da quadra.
        var partida = NovaAoVivo();
        partida.GamesDupla1 = 7;
        partida.GamesDupla2 = 4;

        PlacarDaMesa.Aplicar(partida, -1, -1, -1, -1, Meiodia);

        Assert.Equal(7, partida.GamesDupla1);
        Assert.Equal(4, partida.GamesDupla2);
    }

    // ═══ O RELÓGIO DO APARELHO NÃO MANDA MAIS ═══════════════════════════════════════════════

    [Fact]
    public async Task O_relogio_adiantado_de_um_aparelho_nao_trava_o_outro()
    {
        // 🕳️ A ordem entre dois placares saía do relógio de CADA aparelho (epoch do
        // `Date.now()`), e relógio de celular erra. Um aparelho adiantado carimbava a partida
        // com uma hora no futuro e **todo toque do outro era recusado a partir dali** — a Mesa
        // adotava o placar do servidor, esvaziava a fila e mostrava a tarja VERDE.
        //
        // ✅ Agora o aparelho manda a IDADE do toque ("isto foi marcado há 5 segundos"), medida
        // com o próprio relógio dele, e quem ancora é o relógio do SERVIDOR. O erro absoluto se
        // cancela: os dois aparelhos passam a ser comparáveis.
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        // Aparelho A, relógio DEZ MINUTOS adiantado, marcando um game de 5 segundos atrás.
        await controller.SincronizarPlacar(jogo.Id, 5, -1, -1, -1,
            marcadoEm: DateTimeOffset.Now.AddMinutes(10).ToUnixTimeMilliseconds(), idadeMs: 5000);

        // Aparelho B, relógio certo, marcando AGORA.
        await controller.SincronizarPlacar(jogo.Id, -1, 4, -1, -1,
            marcadoEm: DateTimeOffset.Now.ToUnixTimeMilliseconds(), idadeMs: 100);

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(5, depois!.GamesDupla1);   // o de A entrou
        Assert.Equal(4, depois.GamesDupla2);    // e o de B TAMBÉM, apesar do relógio de A
    }

    [Fact]
    public async Task Sem_a_idade_o_relogio_do_aparelho_continua_valendo()
    {
        // Fila gravada ANTES deste deploy não tem idade: ela manda o epoch de sempre, e o
        // servidor continua lendo por ele. A idade é acréscimo, não troca de contrato.
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).SincronizarPlacar(
            jogo.Id, 6, 2, 0, 0, marcadoEm: DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(6, depois!.GamesDupla1);
        Assert.Equal(2, depois.GamesDupla2);
    }
}
