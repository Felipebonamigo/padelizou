namespace Padel.Core.Rodizio;

/// <summary>
/// Americano: a tabela inteira sai da semente antes do primeiro jogo, e cada par de jogadores é
/// dupla uma vez (<see cref="TabelaDoAmericano"/>). Mexicano: só a primeira rodada é montada de
/// antemão; as outras saem da classificação do momento (1º+4º contra 2º+3º em cada grupo de 4).
/// </summary>
public enum FormatoDoRodizio { Americano, Mexicano }

/// <summary>Como o Mexicano monta a primeira rodada, quando ainda não há classificação.</summary>
public enum PrimeiraRodadaDoMexicano
{
    /// <summary>Sorteio pela semente do evento.</summary>
    Sorteio,
    /// <summary>Pela força, como se ela fosse a classificação: 1º+4º contra 2º+3º dos 4 mais fortes, e assim por diante.</summary>
    PorForca,
}

/// <summary>
/// As regras de um rodízio. <see cref="PontosPorJogo"/> é o total de pontos CORRIDOS do jogo: ele
/// acaba quando a soma das duas duplas chega a esse número (24 pode terminar 13-11, e 12-12 é
/// empate). É a regra de clube mais comum — 16, 21, 24 e 32 são os totais usuais — e é a única
/// aceita aqui: jogo "por tempo" (acaba quando o relógio acaba, com qualquer soma) não entra.
/// <see cref="Rodadas"/> <c>null</c> é o ciclo padrão da tabela (<see cref="TabelaDoAmericano.RodadasPadrao"/>).
/// </summary>
public sealed record RegrasDoRodizio(
    FormatoDoRodizio Formato = FormatoDoRodizio.Americano,
    int PontosPorJogo = 24,
    int? Rodadas = null,
    PrimeiraRodadaDoMexicano PrimeiraRodada = PrimeiraRodadaDoMexicano.Sorteio)
{
    /// <summary>Menos de 4 pontos não é jogo: um saque de cada lado mal começa.</summary>
    public const int MinimoDePontos = 4;

    /// <summary>Teto de sanidade (o arquivo é fronteira de confiança); os clubes jogam de 16 a 32.</summary>
    public const int MaximoDePontos = 64;

    /// <summary>O motivo de as regras não servirem, ou <c>null</c>.</summary>
    internal static string? Problema(RegrasDoRodizio? regras)
    {
        if (regras is null) return "sem regras.";
        if (!Enum.IsDefined(regras.Formato)) return $"formato desconhecido: {regras.Formato}.";
        if (!Enum.IsDefined(regras.PrimeiraRodada)) return $"primeira rodada do Mexicano desconhecida: {regras.PrimeiraRodada}.";
        if (regras.PontosPorJogo is < MinimoDePontos or > MaximoDePontos)
            return $"o jogo vale de {MinimoDePontos} a {MaximoDePontos} pontos corridos; veio {regras.PontosPorJogo}.";
        if (regras.Rodadas is { } rodadas && rodadas is < 1 or > TabelaDoAmericano.MaximoDeRodadas)
            return $"o rodízio tem de 1 a {TabelaDoAmericano.MaximoDeRodadas} rodadas; veio {rodadas}.";
        return null;
    }
}
