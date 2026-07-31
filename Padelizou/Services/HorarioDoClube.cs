namespace Padelizou.Services;

// Regras da grade de horários que o clube publica pra marcação.
//
// Ficam aqui, e não no controller, porque as mesmas três decisões aparecem em quatro lugares
// (criar um horário, editar um, aplicar em todos de uma vez, e o formulário da tela): o que
// conta como "de graça", quanto dura cada jogo e se essa duração cabe na janela publicada.
public static class HorarioDoClube
{
    // As durações que existem no padel de verdade. A tela oferece estas, mas um horário
    // antigo com outro valor continua valendo — por isso Opcoes recebe o valor atual.
    public static readonly int[] DuracoesComuns = { 30, 60, 90, 120 };

    public const int DuracaoMinima = 15;

    // Zero e nulo dizem a mesma coisa (quadra sem cobrança pelo app). Normalizar evita ter
    // dois jeitos de dizer "de graça" e um "R$ 0,00" aparecendo na tela do jogador.
    public static decimal? NormalizarPreco(decimal? preco) => preco > 0 ? preco : null;

    // As opções do seletor, já incluindo a duração atual quando ela é fora do comum —
    // senão editar o preço de um horário de 45 min o converteria pra 60 sem ninguém pedir.
    public static IEnumerable<int> Opcoes(int? duracaoAtual = null)
    {
        var lista = DuracoesComuns.ToList();
        if (duracaoAtual is int d && d > 0 && !lista.Contains(d)) lista.Add(d);
        return lista.OrderBy(x => x);
    }

    public static string Rotulo(int minutos) => minutos switch
    {
        60 => "1h",
        90 => "1h30",
        120 => "2h",
        30 => "30 min",
        _ => $"{minutos} min",
    };

    // Devolve o motivo da recusa, ou null quando serve.
    //
    // A janela menor que a duração é o caso que morde: o clube publica 19h–20h com jogo de
    // 1h30 e a tela do jogador simplesmente não oferece horário nenhum — sem erro, sem aviso,
    // parecendo que o clube está sem vaga. É o mesmo defeito que existia na agenda do
    // professor, e a causa é a mesma: ninguém conferiu se o jogo cabe no que foi publicado.
    public static string? ProblemaCom(TimeSpan inicio, TimeSpan fim, int duracaoMinutos)
    {
        if (duracaoMinutos < DuracaoMinima)
            return $"A duração precisa ser de pelo menos {DuracaoMinima} minutos.";

        if (fim <= inicio)
            return "O horário de fim precisa ser depois do início.";

        if ((fim - inicio).TotalMinutes < duracaoMinutos)
            return $"Não cabe: das {inicio:hh\\:mm} às {fim:hh\\:mm} não dá pra encaixar um jogo de {Rotulo(duracaoMinutos)}.";

        return null;
    }

    // ---- Montar a grade de uma vez ----
    // O clube novo tem 3 quadras × 7 dias: cadastrar regra por regra são 21 formulários
    // iguais, e é exatamente onde ele desiste no meio e fica invisível pro jogador.
    //
    // O clique no botão DIZ qual é a grade daquela janela agora: o que sobrepõe sai e a
    // regra nova entra. A primeira versão preservava o existente — mais segura no papel,
    // mas no primeiro uso real o Felipe clicou "gerar 1h30" pra TROCAR a grade de 1h e
    // recebeu "nada novo pra criar". A intenção de quem clica é substituir.
    //
    // Fora da janela nada é tocado: a noite nobre 18–23 sobrevive a um "gerar 07–18".
    // Pausada sobreposta também sai — deixá-la viraria lixo embaixo da grade nova.
    public static List<Models.HorarioMarcacaoDisponivel> QuaisSubstituir(
        IEnumerable<Models.HorarioMarcacaoDisponivel> existentesDaQuadraEDia,
        TimeSpan inicio, TimeSpan fim) =>
        existentesDaQuadraEDia.Where(h => h.HoraInicio < fim && inicio < h.HoraFim).ToList();
}
