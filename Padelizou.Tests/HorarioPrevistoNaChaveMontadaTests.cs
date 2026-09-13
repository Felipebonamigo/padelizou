namespace Padelizou.Tests;

// A CHAVE DE VERDADE TAMBÉM MOSTRA A HORA DAS FASES QUE AINDA NÃO ACONTECERAM.
//
// 🗣️ Felipe, 12/09/2026, com o print da 4ª Masculina em quadra: *"esse a definir nao é uma
// verdade, ele ja tem horario previsto"*.
//
// 🕳️ ERA UM ATALHO DELIBERADO, ESCRITO NO PRÓPRIO ARQUIVO — e é assim que ele deve ser lido:
// *"a vaga FUTURA da chave de verdade continua dizendo 'a definir' em vez da hora prevista (o
// `null` no lugar dos previstos) — casar ViewBag.ProjecaoCompleta com a numeração global do
// quadro é outra tarefa"*. A tarefa é esta. Antes de a primeira rodada nascer, a PRÉVIA mostrava
// "12/09 23:00 · Arena Nclass" nas quartas; no instante em que ela nasceu, o mesmo partial
// passou a receber `null` e as mesmas vagas viraram "a definir". A informação existia e já tinha
// sido mostrada ao jogador na véspera.
//
// ⚠️ O CASAMENTO É POR (FASE, NÚMERO DENTRO DA FASE) e entregue por número GLOBAL do quadro —
// nunca por posição na lista. É a armadilha de 10/09 (QuadroDaChaveCasaPorNumeroTests):
// `ProjetarProximasFasesAsync` termina com `OrderBy(j => j.Horario)`, e uma reserva fora de
// ordem faz a Semifinal 2 chegar na frente da 1.
public class HorarioPrevistoNaChaveMontadaTests
{
    // A pasta do projeto, subindo do binário de teste — mesma busca de
    // QuadroDaChaveCasaPorNumeroTests. (`TestInfra.PastaDasFontesDeVerdade` é a das FONTES
    // tipográficas: nome parecido, pasta outra.)
    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto a partir de " + AppContext.BaseDirectory);
    }

    private static string Details() =>
        TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml")));

    // O trecho do `else` — a chamada do partial com a chave JÁ MONTADA. É a segunda das duas
    // (a primeira é a da prévia), e é só ela que interessa aqui.
    private static string ChamadaDaChaveDeVerdade()
    {
        var fonte = Details();
        int primeira = fonte.IndexOf("<partial name=\"_ChaveDoMataMata\"", StringComparison.Ordinal);
        Assert.True(primeira >= 0, "Não achei nenhuma chamada do quadro da chave.");

        int segunda = fonte.IndexOf("<partial name=\"_ChaveDoMataMata\"", primeira + 1, StringComparison.Ordinal);
        Assert.True(segunda > primeira,
            "Só achei UMA chamada do quadro: a prévia e a chave de verdade deviam ser duas.");

        return fonte.Substring(segunda, Math.Min(600, fonte.Length - segunda));
    }

    [Fact]
    public void A_chave_de_verdade_nao_recebe_mais_null_no_lugar_dos_previstos()
    {
        var chamada = ChamadaDaChaveDeVerdade();

        // O `null` ERA o atalho. Enquanto ele estiver aí, toda vaga futura diz "a definir".
        Assert.DoesNotContain(">?)null", chamada, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chave_de_verdade_recebe_um_mapa_de_previstos()
    {
        Assert.Contains("previstosDaChave", ChamadaDaChaveDeVerdade(), StringComparison.Ordinal);
    }

    [Fact]
    public void O_mapa_da_chave_de_verdade_casa_por_numero_dentro_da_fase()
    {
        var fonte = Details();
        int mapa = fonte.IndexOf("var previstosDaChave", StringComparison.Ordinal);
        Assert.True(mapa >= 0, "Não achei a montagem do mapa de previstos da chave de verdade.");

        int fim = fonte.IndexOf("<partial name=\"_ChaveDoMataMata\"", mapa, StringComparison.Ordinal);
        Assert.True(fim > mapa, "Não achei o partial que recebe o mapa.");

        var trecho = fonte[mapa..fim];
        Assert.Contains("Numero == i + 1", trecho, StringComparison.Ordinal);
        Assert.DoesNotContain("daFase[i]", trecho, StringComparison.Ordinal);
    }

    [Fact]
    public void O_atalho_deliberado_saiu_junto_com_o_atalho()
    {
        // Comentário que descreve um atalho que não existe mais é pior que comentário nenhum:
        // a próxima sessão lê "continua dizendo a definir" e vai procurar um defeito que já
        // foi consertado. Sai com o atalho, no mesmo commit.
        var comComentarios = File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        Assert.DoesNotContain("continua dizendo \"a definir\" em vez da hora prevista",
            comComentarios, StringComparison.Ordinal);
    }
}
