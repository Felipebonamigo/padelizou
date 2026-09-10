using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// O MODAL "TROCAR COM QUAL JOGO?" TEM QUE DIZER O CLUBE DO SLOT (10/09/2026).
//
// Desde o PR #120 o clube viaja com o horário (TrocaDeHorario.Trocar troca ClubeId). No Er, "por
// ordem", toda Partida nasce sem quadra e com ClubeId carimbado — a linha da lista mostra "Radar"
// pelo carimbo (_JogoEmLinha), mas a opção do jogo REAL no modal só escrevia lugar quando havia
// NomeQuadra, e sem CategoriaId/ClubeId. Resultado: o organizador escolhe o slot de destino às
// cegas, e a troca leva o jogo pro Radar sem ninguém ver.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A guarda tem que ser a ETIQUETA,
// igual à linha — nunca `IsNullOrEmpty(candidato.NomeQuadra)`.
public class OpcaoDoModalDeTrocaDizOClubeTests
{
    [Fact]
    public void A_opcao_do_jogo_real_pede_a_etiqueta_com_categoria_e_clube_e_nao_se_esconde_atras_da_quadra()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        // Só o trecho do select do modal de horário, pra não casar com o modal de quadra.
        var inicio = fonte.IndexOf("id=\"trocaJogoB\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "o select trocaJogoB sumiu da view");
        var fim = fonte.IndexOf("</select>", inicio, StringComparison.Ordinal);
        var select = fonte.Substring(inicio, fim - inicio);

        Assert.DoesNotContain("IsNullOrEmpty(candidato.NomeQuadra)", select);
        Assert.Matches(
            new Regex(@"LugarDoJogo\.Etiqueta\(\s*ViewData\.Sedes\(\),\s*candidato\.NomeQuadra,\s*candidato\.CategoriaId,\s*candidato\.ClubeId\s*\)"),
            select);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
