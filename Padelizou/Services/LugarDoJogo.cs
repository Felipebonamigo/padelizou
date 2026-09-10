using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Padelizou.Services;

// ONDE É O JOGO, em uma linha — a etiqueta que as telas mostram junto do horário.
//
// Até 21/08/2026 a resposta era só o nome da quadra, e bastava: o torneio acontecia num lugar
// só, e o cabeçalho da página já dizia qual. Com o torneio em mais de um clube
// (Services/SedesDoTorneio) parou de bastar — "Quadra 2 · ao vivo" manda quem está no clube A
// procurar a quadra 2 DALI, e o jogo é do outro lado da cidade.
//
// ⚠️ A régua mora aqui porque são NOVE telas mostrando a mesma etiqueta (a linha de jogo, o
// jogo que vem, as duas vagas de chave, o ao vivo, o card do grupo, a home, o placar e a Mesa).
// Nove cópias de um `if` é como se escreve a tela que mostra o clube certo ao lado da que mostra
// o errado — e a que fica pra trás é sempre a que ninguém lembra que existe.
public static class LugarDoJogo
{
    // A etiqueta pronta, ou null quando não há nada a dizer.
    //
    // ⚠️ O LOCAL ENTRA SEMPRE desde 09/09/2026, e isso INVERTE a decisão de 21/08: até aqui o
    // torneio de uma sede só devolvia o nome da quadra e mais nada, "porque repetir o clube em
    // cada linha seria copiar o cabeçalho da página dezenas de vezes".
    //
    // 🗣️ Felipe, num print da lista de jogos do 2º Etapa ER Padel Tour: *"falta aparecer qual o
    // local e quadra aqui na lista de jogos"*, e a referência que ele mandou é o que o Er já
    // publica na 1ª Etapa (`17/07 Sex 18:00 - Er Padel - Quadra: .Loja 7`). Perguntado sobre
    // exatamente o custo de repetir o clube, escolheu o outro lado: *"Sempre: Er Padel · Quadra
    // 2"*. O que mudou desde 21/08 é que o torneio de duas sedes deixou de ser hipótese — o Er
    // aluga o Radar —, e uma etiqueta que muda de forma conforme o torneio ensina o jogador a
    // não confiar nela.
    //
    // JOGO SEM QUADRA devolve o local sozinho, em vez de nada. Quadra vazia não é defeito:
    // 🗣️ *"nao tem quadra definida, apenas o clube, por que é por ordem de chegada (por ter
    // checkin)"* — quem decide a quadra é o balcão, na hora. O que a linha não pode é ficar
    // muda sobre o prédio, que é o que ela fazia nos 97 jogos do print.
    // `categoriaId` entrou em 10/09/2026 e só é lido quando NÃO há quadra: a quadra é o dado mais
    // específico e continua mandando. Sem ela, num torneio de dois clubes, é a categoria que
    // pode dizer o clube — ver SedesDoTorneio.NomeDoClubeDaCategoria. Opcional porque nem toda
    // tela tem a categoria na mão; sem ela o comportamento é o de antes.
    public static string? Etiqueta(SedesDoTorneio? sedes, string? nomeQuadra, int? categoriaId = null)
    {
        var quadra = (nomeQuadra ?? "").Trim();
        var clube = ClubeNaEtiqueta(sedes, quadra);

        if (quadra.Length == 0)
            return clube ?? (sedes != null && categoriaId is { } cat ? sedes.NomeDoClubeDaCategoria(cat) : null);

        return clube == null ? quadra : $"{clube} · {quadra}";
    }

    // Que clube escrever ao lado da quadra — e a resposta depende de quantos o torneio tem.
    //
    // Com UM clube, toda quadra é dele, inclusive a que a Mesa de Controle escreveu à mão: não
    // existe segundo prédio pra errar, então até o jogo sem quadra nenhuma sabe onde é.
    //
    // Com DOIS, só a quadra do CADASTRO tem clube. Nome de quadra que não está lá devolve null e
    // a tela mostra só o nome — `Partida.NomeQuadra` é texto solto, e escrever um clube chutado
    // seria pior que não escrever nenhum, porque é ele que decide pra que prédio a pessoa dirige.
    // Pelo mesmo motivo, jogo SEM quadra num torneio de duas sedes não recebe local nenhum:
    // nada no banco diz em qual dos dois ele é.
    private static string? ClubeNaEtiqueta(SedesDoTorneio? sedes, string quadra)
    {
        if (sedes == null) return null;

        return sedes.MaisDeUmClube ? sedes.NomeDoClubeDaQuadra(quadra) : sedes.NomeDoClubePrincipal;
    }

    // A mesma etiqueta pro calendário e pros avisos, onde o separador tem que ser texto comum:
    // o "·" some ou vira caixinha em app de e-mail antigo e no LOCATION do .ics.
    //
    // ⚠️ ELE NÃO SEGUIU A ETIQUETA em 09/09/2026, e a diferença é de PERGUNTA, não de descuido.
    // Aqui o texto entra DENTRO de frase — "A {onde} vagou — seu jogo é o próximo"
    // (Services/AvisosDoDiaDeJogo, que sai por push, e-mail e WhatsApp de uma vez) —, e ali a
    // pergunta é "que QUADRA vagou?". Um aviso dizendo "A Er Padel vagou" manda a pessoa se
    // levantar sem dizer pra onde. O .ics também já recebe o local do torneio por fora
    // (AgendaController.LocalDaPartida). Por isso jogo sem quadra continua devolvendo null.
    public static string? EmTextoCorrido(SedesDoTorneio? sedes, string? nomeQuadra)
    {
        var quadra = (nomeQuadra ?? "").Trim();
        if (quadra.Length == 0) return null;

        if (sedes is not { MaisDeUmClube: true }) return quadra;

        return sedes.NomeDoClubeDaQuadra(quadra) is { } clube ? $"{quadra} — {clube}" : quadra;
    }

    // ── Como as sedes chegam nas telas ────────────────────────────────────────────────────
    // Por `ViewData`, e não pelo modelo de cada view, porque quem mostra a etiqueta são
    // PARCIAIS COMPARTILHADAS (`_JogoEmLinha` e companhia), usadas por telas com modelos
    // completamente diferentes — a página do torneio, a home, a agenda, o painel do organizador.
    // Enfiar as sedes em cada um desses modelos seria mexer em oito view models pra carregar o
    // mesmo dado, e a parcial que ficasse de fora mostraria a quadra sem o clube, calada.
    //
    // Ausente = torneio de uma sede só, e a etiqueta volta a ser só o nome da quadra. É o padrão
    // e é o certo: tela que não sabe de sede não inventa nenhuma.
    public const string ChaveNaTela = "SedesDoTorneio";

    public static SedesDoTorneio? Sedes(this ViewDataDictionary viewData) =>
        viewData[ChaveNaTela] as SedesDoTorneio;
}
