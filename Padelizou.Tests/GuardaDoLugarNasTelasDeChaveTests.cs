using System.IO;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — A GUARDA DO LUGAR É A ETIQUETA, NÃO A QUADRA — também nos cards de grupo e na chave.
//
// Achado pela revisão adversarial: as três telas ganharam `LugarDoJogo.Etiqueta(sedes, NomeQuadra,
// CategoriaId, ClubeId)` nesta semana, mas mantiveram por fora o `@if (!IsNullOrEmpty(NomeQuadra))`.
// No Er a quadra é sempre nula, então o `if` nunca entra e o clube carimbado — o motivo de o
// ClubeId existir — não aparece: o card do grupo diz "12/09 08:00" e nada de Radar, e a semifinal
// nascida pelo robô aparece na chave sem prédio. É o mesmo defeito que `_JogoEmLinha` corrigiu em
// 09/09 ("A guarda é a ETIQUETA, e não NomeQuadra"), reintroduzido nas telas vizinhas.
//
// Teste de FONTE: a suíte não renderiza Razor.
public class GuardaDoLugarNasTelasDeChaveTests
{
    [Theory]
    [InlineData("Torneios/Details.cshtml", "pdz-grupo-jogo-quadra")]
    [InlineData("Torneios/_ChaveDoMataMata.cshtml", "pdz-chave-quadra")]
    [InlineData("Torneios/_ChaveVaga.cshtml", "pdz-chave-quadra")]
    // ⚠️ A PRÉVIA SAIU DA LISTA em 12/09/2026 porque saiu o ARQUIVO: ela passou a ser desenhada
    // pelo mesmo `_ChaveDoMataMata` acima (a aba não muda mais de cara quando o mata-mata
    // nasce), e lá o cartão calcula o lugar UMA vez pros dois casos — a linha do jogo real e a
    // do previsto. A régua da etiqueta continua guardada, num lugar só.
    // A QUARTA TELA, ESQUECIDA NA PRIMEIRA VOLTA (10/09/2026): o cartão da PRÉVIA do mata-mata, no
    // mesmo Details.cshtml. 🗣️ Felipe, num print do quadro do 2ª Etapa ER PADEL TOUR: *"quartas de
    // final ta sem clube"* — a hora das quartas foi digitada na mão, a reserva nasce sem quadra
    // (TorneiosController.DefinirHorario) e o `if (!IsNullOrEmpty(previsto.Quadra))` calava o
    // cartão inteiro. Ao lado, os cards de grupo diziam "Radar" e "Er Padel" pelo carimbo.
    public void O_lugar_do_jogo_e_guardado_pela_etiqueta_e_nao_pela_quadra(string view, string classe)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view));
        int span = fonte.IndexOf($"class=\"{classe}\"", StringComparison.Ordinal);
        Assert.True(span >= 0, $"Não achei o span .{classe} em {view}.");

        var antes = fonte[Math.Max(0, span - 300)..span];
        Assert.DoesNotContain("IsNullOrEmpty(", antes);
        Assert.Contains("LugarDoJogo.Etiqueta(", antes);
        Assert.Contains("is { }", antes);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
