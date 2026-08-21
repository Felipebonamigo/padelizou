using System.Text.RegularExpressions;
using Padelizou.Services;

namespace Padelizou.Tests;

// 21/08/2026 — O TETO DE GAMES CHEGA NA TELA, pedido do Felipe: "está permitindo marcar mais do
// que 9 em um torneio que é até 9".
//
// O `max` do campo de placar ao vivo era `99` cravado, e o "+" do card também. O servidor
// SEMPRE cortou o número no salvar (FormatoDaPartida.PlacarValido), então nunca entrou lixo no
// banco — mas a tela passava vários segundos exibindo 12x0 num jogo até 9, e é justamente pela
// tela que o organizador decide se o jogo acabou.
//
// ⚠️ O QUE ESTES TESTES PROTEGEM NÃO É O NÚMERO, É O LUGAR ONDE ELE É CALCULADO. A régua tem
// soma × "até", o desempate do "vencer por dois" e teto POR LADO. Reescrever isso em JavaScript
// é exatamente como o `limiteGames: 9` cravado no JS sobreviveu tanto tempo — o comentário do
// próprio FormatoDaPartida diz isso. O servidor calcula; a tela obedece.
public class TetoDeGamesNaTelaTests
{
    private static readonly FormatoDaPartida.Formato Ate9 = new(Sets: 1, Games: 9);
    private static readonly FormatoDaPartida.Formato Ate4 = new(Sets: 1, Games: 4);

    [Fact]
    public void Num_jogo_ate_9_o_teto_dos_dois_lados_e_9()
    {
        Assert.Equal(9, FormatoDaPartida.TetoDoLado(Ate9, 0, 0));
        Assert.Equal(9, FormatoDaPartida.TetoDoLado(Ate9, 8, 8));
    }

    // O "vencer por dois" do padel: limite PAR empatado na penúltima estende. É por isso que o
    // teto precisa voltar a cada resposta do servidor, e não ser calculado uma vez só.
    [Fact]
    public void Num_jogo_ate_4_o_teto_sobe_pra_5_depois_do_empate_em_3x3()
    {
        Assert.Equal(4, FormatoDaPartida.TetoDoLado(Ate4, 2, 3));
        Assert.Equal(5, FormatoDaPartida.TetoDoLado(Ate4, 3, 3));
    }

    // Na SOMA o teto é por lado e depende do outro: numa soma de 7 com o adversário em 3, eu só
    // chego a 4. Um `max` simétrico deixaria marcar 7x3.
    [Fact]
    public void Na_soma_o_teto_de_cada_lado_desconta_o_que_o_outro_ja_tem()
    {
        var soma7 = new FormatoDaPartida.Formato(1, 7, ContagemDeGamesDoTorneio.Soma);

        Assert.Equal(4, FormatoDaPartida.TetoDoLado(soma7, 0, 3));
        Assert.Equal(7, FormatoDaPartida.TetoDoLado(soma7, 0, 0));
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // AS GUARDAS DE ARQUIVO — o que teste de C# não alcança.
    //
    // ⚠️ O defeito aqui é 100% de tela: com o `max` errado o servidor continua salvando certo,
    // nenhum teste de controller fica vermelho, e o único sintoma é o organizador vendo um
    // número que não existe. A única rede é ler o arquivo.
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void O_campo_de_placar_ao_vivo_NAO_tem_teto_cravado_na_view()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        // O `max` sai do teto que o servidor calculou, nunca de um número escrito à mão.
        Assert.Contains("max=\"@tetosDoJogo.Lado1\"", view);
        Assert.Contains("max=\"@tetosDoJogo.Lado2\"", view);
        Assert.DoesNotContain("max=\"99\"", view);
    }

    [Fact]
    public void O_botao_de_mais_obedece_o_max_do_campo_e_nao_recalcula_a_regra()
    {
        var js = LerDaWeb("wwwroot", "js", "placar-ao-vivo.js");

        // Lê o teto do campo…
        Assert.Contains("campo.getAttribute(\"max\")", js);
        // …e o mantém em dia a cada resposta do servidor, senão trava no game do desempate.
        Assert.Contains("setAttribute(\"max\"", js);

        // ⚠️ O 99 cravado não pode voltar como LIMITE. Ele sobrevive só como reserva de
        // "o servidor não disse nada" — nunca comparado direto contra o valor digitado.
        Assert.DoesNotContain("novo > 99", js);
    }

    // Mesmo achador dos outros testes de guarda: os testes rodam a partir de bin/.
    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
