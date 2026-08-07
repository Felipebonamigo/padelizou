namespace Padelizou.Services;

// Como se chama cada fase de um Torneio Americano — e como se lê de volta.
//
// A `Partida.Fase` é texto livre no banco, e é por ela que TODO o resto se orienta: o robô que
// fecha o torneio, a classificação, o formato da partida. Com a divisão em grupos passaram a
// existir três coisas diferentes ali dentro (rodada de grupo único, rodada de um grupo, rodada
// do grupo final), e montar ou interpretar esse texto na mão em cada lugar é a receita
// documentada dos piores defeitos deste projeto: uma cópia é corrigida, a outra não.
//
// Então tudo passa por aqui. Quem precisa saber "esta partida é de qual grupo?" pergunta; quem
// cria partida pede o nome.
//
// ⚠️ COMPATIBILIDADE: torneios sorteados antes de 06/08/2026 têm "Americano Rodada 3", sem
// grupo. Isso continua valendo e significa GRUPO ÚNICO — e o formato novo de grupo único é
// exatamente o mesmo texto, pra não criar duas grafias pra mesma coisa.
public static class FaseDoAmericano
{
    private const string Prefixo = "Americano";
    private const string MarcaDeGrupo = "Americano Grupo ";
    private const string MarcaDeFinal = "Americano Final Rodada ";

    // Rodada de um torneio sem divisão: "Americano Rodada 3".
    public static string RodadaUnica(int rodada) => $"{Prefixo} Rodada {rodada}";

    // Rodada de um grupo: "Americano Grupo B Rodada 3".
    public static string RodadaDeGrupo(string grupo, int rodada) =>
        $"{MarcaDeGrupo}{grupo} Rodada {rodada}";

    // Rodada do grupo que decide o título: "Americano Final Rodada 2".
    public static string RodadaDoGrupoFinal(int rodada) => $"{MarcaDeFinal}{rodada}";

    // O nome do grupo pela posição: 0 → "A", 1 → "B"... Depois de "Z" vira "A2", "B2" — feio,
    // mas um Americano com 27 grupos são 27 × 4 = 108 pessoas, e aí o nome do grupo é o menor
    // dos problemas. O que não pode é repetir nome.
    public static string NomeDoGrupo(int indice)
    {
        if (indice < 26) return ((char)('A' + indice)).ToString();
        return $"{(char)('A' + indice % 26)}{indice / 26 + 1}";
    }

    // É partida de Americano? É o gatilho do robô que fecha o torneio.
    public static bool EhDoAmericano(string? fase) =>
        fase != null && fase.StartsWith(Prefixo, StringComparison.Ordinal);

    // É do grupo que decide o título?
    public static bool EhDoGrupoFinal(string? fase) =>
        fase != null && fase.StartsWith(MarcaDeFinal, StringComparison.Ordinal);

    // De qual grupo é esta partida? null = torneio sem divisão (grupo único), que inclui os
    // torneios antigos.
    public static string? GrupoDe(string? fase)
    {
        if (fase == null || !fase.StartsWith(MarcaDeGrupo, StringComparison.Ordinal)) return null;

        var resto = fase[MarcaDeGrupo.Length..];
        int espaco = resto.IndexOf(' ');
        return espaco > 0 ? resto[..espaco] : null;
    }

    // As partidas da FASE DE GRUPOS — as que contam pra classificação que define quem passa.
    // O grupo final fica de fora de propósito: ele é outro torneio, com outra tabela.
    public static bool EhDaFaseDeGrupos(string? fase) =>
        EhDoAmericano(fase) && !EhDoGrupoFinal(fase);

    // Só as de um grupo específico. `grupo` nulo = torneio sem divisão.
    public static bool EhDoGrupo(string? fase, string? grupo) =>
        EhDaFaseDeGrupos(fase) && GrupoDe(fase) == grupo;
}
