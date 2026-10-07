using Padelizou.Services;

namespace Padelizou.Tests;

// "CLICO EM SALVAR E NÃO DÁ EM NADA. SÓ APAGA OS DADOS QUE COLOQUEI" — 30/09/2026.
//
// 🗣️ Um jogador da panelinha Sub 90, pelo WhatsApp: *"Opa to trancado nesse aviso e não consigo
// lançar os jogos do grupo... me dá um help valeu"* · *"Eu não consigo lançar mais resultados"* ·
// *"To preenchendo lá clico em salvar e não dá em nada"* · *"Só apaga os dados que coloquei"*.
// No print, um aviso amarelo na tela da SEMANA: "Jogo 1: O placar diz que a OUTRA dupla venceu."
//
// 🕳️ TRÊS DEFEITOS SOMADOS, e é a soma que vira "trancado":
//
//  1. A recusa do lançamento é `TempData["Erro"]` + `RedirectToAction` pro próprio formulário —
//     e `RegistrarJogo.cshtml` e `EditarJogo.cshtml` NUNCA desenharam essa chave. O servidor
//     recusa, a tela volta igualzinha e sem uma palavra: é o "não dá em nada", literal. É o
//     mesmo buraco que a lista de jogos tinha (ver AcaoDoCartaoAoVivoTests), na tela vizinha.
//
//  2. `TempData` só morre na LEITURA. Como o formulário não lê, o recado atravessa requisições
//     e estoura na próxima tela que lê a chave — a da semana. Por isso o aviso parece um
//     bloqueio preso à semana, e não a resposta de um salvar que já passou.
//
//  3. Quem marcou "Empatou" com placar desigual recebia justamente *"O placar diz que a OUTRA
//     dupla venceu"* — sem ter marcado dupla nenhuma. O ternário de `MotivoParaNaoSalvar` só
//     tratava o empate do lado do PLACAR, nunca o do vencedor.
//
// ⚠️ O que o jogador digitou continua se perdendo no redirect — isso é decisão de atomicidade
// já documentada em GruposController, e está tratada à parte. Estes testes travam o que era
// defeito puro: recusa invisível e recusa que mente.
public class PlacarRecusadoNaPanelinhaTests
{
    [Fact]
    public void As_telas_de_lancar_e_corrigir_jogo_tem_onde_mostrar_o_erro_do_servidor()
    {
        // As duas recusam por TempData e voltam pra si mesmas: sem este trecho, as duas ficam
        // mudas. São 7 saídas de recusa nos dois POSTs — nenhuma delas tem outro canal.
        foreach (var arquivo in new[] { "RegistrarJogo.cshtml", "EditarJogo.cshtml" })
        {
            var tela = Tela(arquivo);
            Assert.Contains("TempData[\"Erro\"]", tela);
        }
    }

    [Fact]
    public void Quem_marcou_empate_com_placar_desigual_ouve_falar_de_empate()
    {
        // 6 x 3 não é empate; o recado tem que dizer ISSO, e não acusar a pessoa de ter marcado
        // a outra dupla. Mensagem que descreve o erro errado é pior que mensagem nenhuma: manda
        // a pessoa procurar o defeito onde ele não está.
        var motivo = ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Empate, 6, 3);

        Assert.NotNull(motivo);
        Assert.DoesNotContain("OUTRA dupla", motivo);
        Assert.Contains("empat", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_recado_de_placar_contra_vencedor_continua_igual()
    {
        // O caso de sempre não pode mudar de texto por causa do conserto do empate.
        var motivo = ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, 3, 6);

        Assert.Equal("O placar diz que a OUTRA dupla venceu. Ajuste o placar ou o vencedor.", motivo);
    }

    private static string Tela(string nome)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a pasta Views.");

        return File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Views", "Grupos", nome));
    }
}
