namespace Padelizou.Services;

// Que fases podem dividir o mesmo horário.
//
// Regra do organizador (05/08/2026): **finais podem ser juntas** — várias categorias
// decidindo ao mesmo tempo é o fecho normal de um interno, e forçar uma a esperar a outra só
// atrasa o encerramento. O que NÃO pode é **final e semifinal no mesmo horário**.
//
// Dentro de uma categoria isso é literalmente impossível: os dois finalistas saem da
// semifinal, e quem garante é a folga entre fases (GradeDeJogos.AberturaDaProximaFase).
// Entre categorias diferentes o motivo é o mesmo por outro caminho — num torneio com CHAVE
// DIRETA as mesmas pessoas disputam duas competições, então quem está na semifinal do
// Mata-Mata Geral pode ser exatamente quem tem que jogar a final da 6ª Masculina.
//
// ⚠️ Por que a regra fala de FASE e não de gente: a grade de verdade compara PESSOA e pegaria
// o choque (GradeDeJogos.Encaixar). A PREVISÃO não consegue — ela fala em "Vencedor Quartas
// de Final 1" e não sabe quem vai chegar. A fase é o que ela tem em mãos, e a regra vale nas
// duas pra tela e grade não divergirem.
//
// Deliberadamente só este par. Semifinal com quartas, ou final com jogo de grupo, são
// combinações normais num torneio de várias categorias — proibir mais espalharia a grade e
// empurraria o encerramento sem que ninguém tivesse pedido.
public static class FasesNoMesmoHorario
{
    private const string Semifinal = "Semifinal";
    private const string Final = "Final";

    // A fase nova cabe num horário que já tem estas outras?
    public static bool Cabe(string? fase, IEnumerable<string?> jaNoHorario) =>
        !jaNoHorario.Any(outra => Conflitam(fase, outra));

    public static bool Conflitam(string? a, string? b) =>
        (a == Final && b == Semifinal) || (a == Semifinal && b == Final);
}
