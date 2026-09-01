using Xunit;

namespace Padelizou.Tests;

// A FAMÍLIA DE TORNEIO NÃO USA O CANAL DE WHATSAPP. NENHUM AVISO DELA.
//
// Decisão do Felipe em 21/08/2026, e o motivo é aritmético: um torneio de 100 participantes
// gerava ~450 mensagens num dia — "as chaves saíram" ia 100 de uma vez, em texto quase
// idêntico. É o padrão exato que restringiu o número em 04/08. Saíram as chaves, o "seu jogo
// é o próximo", o cancelamento e a vaga na lista de espera; o canal ficou com aula, desafio e
// pagamento pendente, que são de UM pra UM e disparados por gesto humano.
//
// 🕳️ POR QUE ESTE ARQUIVO EXISTE, e não bastava a decisão estar escrita: em 01/09/2026 um
// aviso novo de torneio — "saiu uma inscrição PAGA", pro organizador — nasceu com
// `AlcanceDoAviso.AppEWhatsApp` e entrou em produção. Ele passava nos três critérios do
// canal (pessoal, urgente, acionável) e o volume era ridículo, então nada no código reclamou.
// A porta que o Felipe tinha fechado dez dias antes voltou a abrir sozinha, e só apareceu
// porque alguém foi contar à mão o que ainda usava o canal.
//
// ⚠️ Este teste NÃO julga se o aviso merece o WhatsApp — ele diz que a decisão é do Felipe, e
// não de quem está escrevendo o próximo aviso com pressa. Ficou vermelho? A conversa é com
// ele, e o caminho é apagar esta linha de propósito, não contornar.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente: o alcance é um argumento passado em ~30
// pontos de chamada, e não há como perguntar "quem manda WhatsApp?" em tempo de execução sem
// disparar de verdade.
public class TorneioNaoVaiProWhatsAppTests
{
    private static string PastaDosControllers()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Controllers");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Pasta Controllers não encontrada a partir do bin.");
    }

    [Fact]
    public void Nenhum_aviso_de_torneio_pede_o_canal_de_WhatsApp()
    {
        var arquivos = Directory.GetFiles(PastaDosControllers(), "TorneiosController*.cs");

        // Se este `Assert` cair pra zero arquivo, o teste passa a não provar nada — foi assim
        // que um guarda de arquivo já ficou verde depois de o arquivo ser renomeado.
        Assert.NotEmpty(arquivos);

        var culpados = arquivos
            .Where(a => File.ReadAllText(a).Contains("AlcanceDoAviso.AppEWhatsApp"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(culpados.Count == 0,
            "Aviso de torneio pedindo o canal de WhatsApp em: " + string.Join(", ", culpados)
            + ". A família de torneio saiu do canal em 21/08/2026 por decisão do Felipe — "
            + "se este caso é exceção, é ele quem decide, e este teste sai junto.");
    }
}
