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
    // Torneio de uma sede só devolve o nome da quadra e mais nada: repetir o clube em cada linha
    // da lista seria copiar o cabeçalho da página dezenas de vezes.
    //
    // Nome de quadra que não está no cadastro devolve só o nome. Acontece de verdade —
    // `Partida.NomeQuadra` é texto solto e a Mesa de Controle escreve nele à mão — e a saída
    // honesta é essa: escrever um clube chutado seria pior que não escrever nenhum, porque é ele
    // que decide pra que prédio a pessoa dirige.
    public static string? Etiqueta(SedesDoTorneio? sedes, string? nomeQuadra)
    {
        var quadra = (nomeQuadra ?? "").Trim();
        if (quadra.Length == 0) return null;

        if (sedes is not { MaisDeUmClube: true }) return quadra;

        return sedes.NomeDoClubeDaQuadra(quadra) is { } clube ? $"{clube} · {quadra}" : quadra;
    }

    // A mesma etiqueta pro calendário e pros avisos, onde o separador tem que ser texto comum:
    // o "·" some ou vira caixinha em app de e-mail antigo e no LOCATION do .ics.
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
