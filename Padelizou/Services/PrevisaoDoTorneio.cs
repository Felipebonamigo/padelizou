namespace Padelizou.Services;

// Responde a pergunta que o organizador faz antes de anunciar o torneio: "cabe?"
//
// Ele escolhe o dia de começar, a hora de abrir, o teto do dia, o número de quadras e a
// duração do jogo. Nenhum desses números diz sozinho quando o torneio ACABA — isso depende
// de quantos jogos existem, e quantos jogos existem depende de quantas duplas entram.
// Sem essa conta o organizador só descobre no sábado à noite que o domingo não fecha.
public static class PrevisaoDoTorneio
{
    // Quantos grupos e quantos jogos de grupo saem de N duplas.
    //
    // Espelha a regra do GerarChaves: grupos de 3, e quando o total não é múltiplo de 3 os
    // melhores rankeados resolvem em grupo(s) de 2.
    //   resto 1 (ex. 13) → 2 grupos de 2 + o resto em grupos de 3
    //   resto 2 (ex. 14) → 1 grupo de 2  + o resto em grupos de 3
    public static (int Grupos, int Jogos) FaseDeGrupos(int duplas)
    {
        if (duplas < 2) return (0, 0);
        if (duplas < 3) return (1, 1);   // 2 duplas: chave direta

        int gruposDeDois = (duplas % 3) switch { 1 => 2, 2 => 1, _ => 0 };
        int emGruposDeTres = duplas - gruposDeDois * 2;
        int gruposDeTres = emGruposDeTres / 3;

        // Todos contra todos: grupo de 2 dá 1 jogo, grupo de 3 dá 3.
        return (gruposDeDois + gruposDeTres, gruposDeDois * 1 + gruposDeTres * 3);
    }

    // Quantos jogos o mata-mata inteiro tem, do primeiro cruzamento à final.
    //
    // Mesma conta do ChaveamentoMataMata: classificam 2 por grupo, e o quadro é a maior
    // potência de 2 que cabe nesses classificados. Um quadro de N duplas tem N-1 jogos —
    // cada jogo elimina exatamente uma, e sobra o campeão.
    public static int MataMata(int grupos)
    {
        if (grupos <= 0) return 0;
        if (grupos == 1) return 1;   // um grupo só fecha com a final direta 1º x 2º

        return MaiorPotenciaDe2Ate(grupos * 2) - 1;
    }

    private static int MaiorPotenciaDe2Ate(int teto)
    {
        int p = 1;
        while (p * 2 <= teto) p *= 2;
        return p;
    }

    // O torneio inteiro, de uma categoria: grupos + mata-mata.
    public static int TotalDeJogos(int duplas)
    {
        var (grupos, jogos) = FaseDeGrupos(duplas);
        return jogos + MataMata(grupos);
    }

    // Quando o ÚLTIMO jogo começa, dada a grade. Null se não há jogo nenhum.
    public static DateTime? UltimoJogo(
        DateTime inicio, TimeSpan ultimoInicioDoDia, TimeSpan aberturaDiasSeguintes,
        int quadras, int duracaoMinutos, int totalDeJogos)
    {
        if (totalDeJogos <= 0) return null;

        // A grade é preguiçosa: só materializa o último.
        return GradeDeJogos
            .Horarios(inicio, ultimoInicioDoDia, quadras, duracaoMinutos, totalDeJogos, aberturaDiasSeguintes)
            .Last();
    }

    // Quantos dias de quadra o torneio ocupa (o de abertura conta como 1).
    public static int DiasOcupados(DateTime inicio, DateTime ultimoJogo) =>
        (ultimoJogo.Date - inicio.Date).Days + 1;
}
