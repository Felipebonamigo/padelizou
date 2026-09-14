using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — O "FINALIZAR" RECARREGAVA A PÁGINA, E RECARGA REINICIA TODO <iframe> DA TELA.
//
// 🗣️ Felipe: *"apenas queria q o video nao travasse, nao mude o layout"*.
//
// 🕳️ "Finalizar" e "Voltar pra agendado" eram POST comum. Encerrar o jogo da Quadra 1 parava o
// vídeo de quem estava assistindo à Quadra 2 — duas coisas sem nenhuma relação. O
// js/placar-ao-vivo.js e o js/saque-ao-vivo.js já tinham tirado essa recarga do −/+ e da bolinha
// do saque; estes dois botões ficaram de fora, e são os que o organizador mais aperta.
//
// O COMPORTAMENTO tem trava de verdade em dois conferidores que rodam o JS num DOM falso:
// `conferir-acao-do-cartao-ao-vivo.js` (o POST, a confirmação, o erro, a rede caída) e
// `conferir-abas-que-ficam.js` (o remendo da resposta).
//
// 🔑 O QUE ESTE ARQUIVO GUARDA É O CONTRATO ENTRE O RAZOR E O JS, e cada peça dele já é uma
// falha calada esperando: sem a classe, o botão volta a recarregar e ninguém percebe (só o vídeo
// para); sem o script na view, idem; sem o `#pdzAvisoDaAcao`, o motivo da recusa do servidor
// morre dentro da resposta do fetch — TempData é de uma leitura só.
public class AcaoDoCartaoAoVivoTests
{
    [Fact]
    public void Os_dois_botoes_que_gravam_e_voltam_pra_lista_levam_a_etiqueta_do_JS()
    {
        var cartoes = Cartoes();

        // ⚠️ DENTRO DA TAG <form>, e não no arquivo inteiro: o comentário logo acima cita a
        // classe de propósito, e contar menção deixaria o teste verde com a etiqueta removida
        // da tag — que é exatamente o defeito.
        var formularios = Regex.Matches(cartoes, "<form[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .Where(f => f.Contains("FinalizarPartida") || f.Contains("VoltarParaAgendado"))
            .ToList();

        Assert.Equal(2, formularios.Count);
        foreach (var form in formularios)
        {
            Assert.Contains("pdz-live-acao", form);
        }
    }

    [Fact]
    public void As_duas_telas_da_lista_de_jogos_carregam_o_script()
    {
        // A lista de jogos é o MESMO parcial em duas páginas. Carregar o script só numa deixaria
        // a outra recarregando, e o defeito voltaria pela metade — do jeito mais difícil de ver.
        Assert.Contains("js/acao-do-cartao-ao-vivo.js", Details());
        Assert.Contains("js/acao-do-cartao-ao-vivo.js", Jogos());
    }

    [Fact]
    public void As_duas_telas_tem_onde_mostrar_o_erro_do_servidor()
    {
        // 🕳️ A `jogos.cshtml` NUNCA desenhou `TempData["Erro"]` — o organizador apertava
        // Finalizar, o servidor recusava e a tela voltava igualzinha, sem uma palavra. Com o
        // fetch fica pior: o TempData é de UMA LEITURA SÓ e a resposta o consome, então o motivo
        // morre ali dentro se não houver onde copiá-lo.
        foreach (var view in new[] { Details(), Jogos() })
        {
            Assert.Contains("id=\"pdzAvisoDaAcao\"", view);
            Assert.Contains("TempData[\"Erro\"]", view);
        }
    }

    [Fact]
    public void O_JS_procura_exatamente_o_que_o_Razor_escreve()
    {
        var js = Js();

        // Este é o gate nos dois sentidos: renomear de um lado só compila, não quebra teste
        // nenhum e o botão volta a recarregar, calado.
        Assert.Contains(".pdz-live-acao", js);
        Assert.Contains("#pdzAvisoDaAcao", js);

        // E a régua que separa "finalizou" de "a sessão caiu e isto é a tela de login": o fetch
        // SEGUE o 302 e entrega 200 com o HTML do login. Sem esta checagem, "não finalizou nada"
        // apareceria como finalizado.
        Assert.Contains("jogosTabsContent", Atualizador());
    }

    private static string Cartoes() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string Jogos() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "jogos.cshtml"));

    private static string Js() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "acao-do-cartao-ao-vivo.js"));

    private static string Atualizador() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "jogos-ao-vivo-atualiza.js"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
