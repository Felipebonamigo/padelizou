using Xunit;

namespace Padelizou.Tests;

// QUEM PODE FALAR PELO WHATSAPP É UMA LISTA FECHADA, E ELA MORA AQUI.
//
// 🕳️ POR QUE UMA LISTA, e não um teste por família: em 21/08/2026 a família de torneio saiu do
// canal, e em 01/09 um aviso NOVO de torneio entrou de volta sozinho — passava nos três
// critérios, o volume era ridículo, e nada no código reclamou. Nasceu daí o
// `TorneioNaoVaiProWhatsAppTests`, que fecha aquela porta. Mas ele fecha UMA porta: qualquer
// controller novo continua podendo entrar no canal sem ninguém decidir isso.
//
// 🔑 Este arquivo inverte a pergunta. Em vez de listar quem NÃO pode, lista quem PODE — então
// um arquivo novo pedindo o canal quebra o teste por existir, que é o único jeito de a decisão
// continuar sendo do Felipe depois que todo mundo esquecer desta conversa.
//
// 🗣️ 18/09/2026, o Felipe sobre os desafios: *"na parte de desafios, está enviando whats, não é
// para enviar whats, esse pode ser só notificação"*. Os desafios saíram do canal nesse dia — e
// com eles caiu o último aviso disparado por um jogador contra OUTRO jogador. O que sobrou é
// aula (professor ↔ aluno, relação contratada) e pagamento pendente (a pessoa deve dinheiro e
// perde a vaga).
//
// ⚠️ Este teste NÃO julga se um aviso merece o canal — ele diz que a decisão é do Felipe, e não
// de quem está escrevendo o próximo aviso com pressa. Ficou vermelho? A conversa é com ele, e o
// caminho é acrescentar a linha aqui de propósito, nunca contornar.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente (a mesma do teste de torneio): o alcance é um
// argumento passado em dezenas de pontos de chamada, e não há como perguntar "quem manda
// WhatsApp?" em tempo de execução sem disparar de verdade.
public class QuemFalaPeloWhatsAppTests
{
    // A LISTA. Cada linha é uma decisão do Felipe registrada no WHATSAPP.md, seção "Quem ainda
    // fala pelo WhatsApp" — e a régua pra entrar são TRÊS coisas ao mesmo tempo: pessoal,
    // urgente e acionável. Duas de três não bastam.
    private static readonly string[] PodemPedirOCanal =
    {
        // Aulas: professor ↔ aluno, relação contratada, e quem não vê vai à quadra à toa.
        "AulasController.Agenda.cs",   // reposição marcada · aula apagada pelo professor
        "AulasController.Aluno.cs",    // aluno pediu aula · aluno desmarcou
        "EdicaoDeAula.cs",             // mudou horário ou local (preço sozinho NÃO vai)

        // Inscrição: vence, custa a vaga e tem dinheiro no meio.
        "PagamentoExpiradoBackgroundService.cs",
    };

    // O enum se declara com o próprio nome, e o VolumeDoWhatsApp cita o valor em comentário.
    // Nenhum dos dois é ponto de chamada.
    private const string ArquivoDoEnum = "AlcanceDoAviso.cs";

    [Fact]
    public void So_a_lista_fechada_pede_o_canal_de_WhatsApp()
    {
        var arquivos = Directory.GetFiles(PastaDoProjeto(), "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        // Se a varredura vier vazia, o teste passa a não provar nada — foi assim que um guarda
        // de arquivo já ficou verde depois de o arquivo ser renomeado.
        Assert.NotEmpty(arquivos);

        var intrusos = arquivos
            .Where(a => Path.GetFileName(a) != ArquivoDoEnum)
            .Where(a => !PodemPedirOCanal.Contains(Path.GetFileName(a)))
            .Where(a => PedeOCanal(File.ReadAllLines(a)))
            .Select(a => Path.GetFileName(a))
            .OrderBy(n => n)
            .ToList();

        Assert.True(intrusos.Count == 0,
            "Estes arquivos pedem o canal de WhatsApp e não estão na lista: "
            + string.Join(", ", intrusos)
            + ". Quem entra no canal é decisão do Felipe (WHATSAPP.md, \"Quem ainda fala pelo "
            + "WhatsApp\") — se este aviso é exceção, a linha entra na lista deste teste junto "
            + "com a conversa que a autorizou.");
    }

    [Fact]
    public void E_a_lista_nao_tem_linha_morta()
    {
        // O outro lado do guarda: uma entrada que não corresponde mais a nenhum arquivo deixa a
        // lista mentindo sobre o alcance do canal, e a próxima pessoa a lê como autorização.
        var nomes = Directory.GetFiles(PastaDoProjeto(), "*.cs", SearchOption.AllDirectories)
            .Where(a => PedeOCanal(File.ReadAllLines(a)))
            .Select(Path.GetFileName)
            .ToHashSet();

        var mortas = PodemPedirOCanal.Where(p => !nomes.Contains(p)).ToList();

        Assert.True(mortas.Count == 0,
            "A lista autoriza arquivos que não pedem mais o canal: " + string.Join(", ", mortas)
            + ". Saiu do canal? Sai da lista também.");
    }

    // Só ponto de chamada de verdade: linha comentada não conta (o WHATSAPP.md e o
    // VolumeDoWhatsApp citam o valor em prosa pra explicar a régua).
    private static bool PedeOCanal(string[] linhas) =>
        linhas.Any(l => l.Contains("AlcanceDoAviso.AppEWhatsApp")
                     && !l.TrimStart().StartsWith("//"));

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Pasta do projeto não encontrada a partir do bin.");
    }
}
