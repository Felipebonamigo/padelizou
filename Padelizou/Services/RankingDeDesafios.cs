using Padelizou.Models;

namespace Padelizou.Services;

// Uma linha do ranking de desafios — serve pras duas tabelas (duplas e jogadores).
//
// `Chave` é o que identifica a linha: a chave canônica da dupla (`ChaveDaDupla`) numa, o id do
// jogador na outra. Existe pra ordenação ser TOTAL e pra tela saber destacar "essa aqui é você".
public sealed record LinhaDoRankingDeDesafios(
    int Posicao,
    string Chave,
    string Nome,
    IReadOnlyList<int> JogadorIds,
    int Desafios,
    int Vitorias,
    int Pontos)
{
    public int Derrotas => Math.Max(0, Desafios - Vitorias);

    // Sem jogo não existe aproveitamento — e uma linha do ranking sempre tem pelo menos um.
    public int Aproveitamento => Desafios == 0
        ? 0
        : (int)Math.Round(100.0 * Vitorias / Desafios, MidpointRounding.AwayFromZero);
}

// O que o perfil mostra numa linha só.
public sealed record ResumoDeDesafios(int Desafios, int Vitorias, int Pontos)
{
    public int Derrotas => Math.Max(0, Desafios - Vitorias);

    public int Aproveitamento => Desafios == 0
        ? 0
        : (int)Math.Round(100.0 * Vitorias / Desafios, MidpointRounding.AwayFromZero);

    public bool Vazio => Desafios == 0;
}

// O RANKING DE DESAFIOS (fase 2 do DESAFIOS.md).
//
// ⚠️ SÃO DUAS TABELAS, E ELAS NUNCA SE SOMAM. "Duplas" é o par fixo; "Jogadores" é o que a
// pessoa fez com QUALQUER parceiro. São recortes diferentes das MESMAS partidas — somar as duas
// conta a mesma pessoa duas vezes, que foi exatamente o erro do Ranking Americano (individual ×
// duplas). A tela diz isso com todas as letras, e é por isso que não existe um método aqui que
// devolva "o ranking" no singular.
//
// ⚠️ Este arquivo NÃO calcula ponto. Ponto é congelado na linha do desafio no fechamento (ver
// FechamentoDoDesafio) — aqui só se SOMA o que já está gravado. Recalcular na leitura faria
// mexer na fórmula em novembro reescrever o ranking de agosto, em silêncio.
public static class RankingDeDesafios
{
    // Igual ao ranking anual do RANKING.md: a campanha expira, e é isso que mata o "campeão
    // eterno" e dá motivo pra voltar todo ano.
    public const int MesesQueContam = 12;

    // O QUE CONTA — num lugar só. São três telas lendo isto (o ranking, o retrospecto do mural e
    // a linha do perfil), e três cópias desta cláusula divergiriam no dia em que só uma soubesse
    // de um estado novo.
    //
    // Só `Confirmado`: placar em disputa não é resultado, e jogo em que ninguém apareceu não
    // mediu nada (é a mesma régua do W.O. no Padelímetro).
    public static IQueryable<Desafio> QueContam(IQueryable<Desafio> desafios, DateTime agora)
    {
        // O corte sai da consulta calculado: `agora.AddMonths(...)` dentro do Where viraria uma
        // chamada que o provedor tenta traduzir pra SQL.
        var corte = agora.AddMonths(-MesesQueContam);

        return desafios.Where(d => d.Status == Desafio.Confirmado
            && d.ConfirmadoEm != null
            && d.ConfirmadoEm >= corte);
    }

    public static List<LinhaDoRankingDeDesafios> PorDupla(
        IEnumerable<Desafio> confirmados, IReadOnlyDictionary<int, Jogador> pessoas)
    {
        var acumulado = new Dictionary<string, Acumulador>();

        foreach (var d in confirmados)
        {
            var vencedor = EstadoDoDesafio.LadoVencedor(d);

            Somar(acumulado,
                ChaveDaDupla.De(d.DesafianteJogador1Id, d.DesafianteJogador2Id),
                new[] { d.DesafianteJogador1Id, d.DesafianteJogador2Id },
                venceu: vencedor == 1, pontos: d.PontosDesafiante ?? 0);

            Somar(acumulado,
                ChaveDaDupla.De(d.DesafiadoJogador1Id, d.DesafiadoJogador2Id),
                new[] { d.DesafiadoJogador1Id, d.DesafiadoJogador2Id },
                venceu: vencedor == 2, pontos: d.PontosDesafiado ?? 0);
        }

        return Ordenar(acumulado, ids => DuplaNaTela.Nome(Pessoa(pessoas, ids[0]), Pessoa(pessoas, ids[1])));
    }

    public static List<LinhaDoRankingDeDesafios> PorJogador(
        IEnumerable<Desafio> confirmados, IReadOnlyDictionary<int, Jogador> pessoas)
    {
        var acumulado = new Dictionary<string, Acumulador>();

        foreach (var d in confirmados)
        {
            var vencedor = EstadoDoDesafio.LadoVencedor(d);

            // Os dois do mesmo lado levam os pontos DAQUELE lado. É o mesmo jogo contado uma vez
            // pra cada pessoa que o jogou — e é por isso que esta tabela não pode ser somada à de
            // duplas: as duas leem as mesmas partidas.
            foreach (var id in d.LadoDesafiante)
                Somar(acumulado, id.ToString(), new[] { id }, vencedor == 1, d.PontosDesafiante ?? 0);

            foreach (var id in d.LadoDesafiado)
                Somar(acumulado, id.ToString(), new[] { id }, vencedor == 2, d.PontosDesafiado ?? 0);
        }

        return Ordenar(acumulado, ids => NomeBonito.ComApelido(
            Pessoa(pessoas, ids[0])?.Nome, Pessoa(pessoas, ids[0])?.Apelido));
    }

    // A linha do perfil: o que ESTA pessoa fez, com qualquer parceiro.
    public static ResumoDeDesafios DoJogador(IEnumerable<Desafio> confirmados, int jogadorId)
    {
        int desafios = 0, vitorias = 0, pontos = 0;

        foreach (var d in confirmados)
        {
            var souDesafiante = d.LadoDesafiante.Contains(jogadorId);
            var souDesafiado = d.LadoDesafiado.Contains(jogadorId);
            if (!souDesafiante && !souDesafiado) continue;

            var vencedor = EstadoDoDesafio.LadoVencedor(d);

            desafios++;
            if ((souDesafiante && vencedor == 1) || (souDesafiado && vencedor == 2)) vitorias++;
            pontos += (souDesafiante ? d.PontosDesafiante : d.PontosDesafiado) ?? 0;
        }

        return new ResumoDeDesafios(desafios, vitorias, pontos);
    }

    // ── Miudezas ──────────────────────────────────────────────────────────────────────

    private sealed class Acumulador
    {
        public int[] JogadorIds = Array.Empty<int>();
        public int Desafios;
        public int Vitorias;
        public int Pontos;
    }

    private static void Somar(Dictionary<string, Acumulador> acumulado, string chave,
        int[] jogadorIds, bool venceu, int pontos)
    {
        if (!acumulado.TryGetValue(chave, out var atual))
        {
            // A ordem dos ids fica ESTÁVEL (menor primeiro) pra que o nome da dupla não mude
            // conforme quem desafiou quem no primeiro jogo da lista.
            atual = new Acumulador { JogadorIds = jogadorIds.OrderBy(i => i).ToArray() };
            acumulado[chave] = atual;
        }

        atual.Desafios++;
        if (venceu) atual.Vitorias++;
        atual.Pontos += pontos;
    }

    // ⚠️ A ordenação é TOTAL de propósito. Empatar em pontos é comum num ranking novo, e um
    // critério que não desempata faz a tabela trocar de ordem entre dois carregamentos da mesma
    // página — o tipo de coisa que ninguém reporta como bug e todo mundo desconfia. A `Chave` é
    // única por linha e fecha o desempate.
    private static List<LinhaDoRankingDeDesafios> Ordenar(
        Dictionary<string, Acumulador> acumulado, Func<int[], string> nomeDe)
    {
        return acumulado
            .OrderByDescending(x => x.Value.Pontos)
            .ThenByDescending(x => x.Value.Vitorias)
            .ThenByDescending(x => x.Value.Desafios)
            .ThenBy(x => nomeDe(x.Value.JogadorIds), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select((x, i) => new LinhaDoRankingDeDesafios(
                Posicao: i + 1,
                Chave: x.Key,
                Nome: nomeDe(x.Value.JogadorIds),
                JogadorIds: x.Value.JogadorIds,
                Desafios: x.Value.Desafios,
                Vitorias: x.Value.Vitorias,
                Pontos: x.Value.Pontos))
            .ToList();
    }

    private static Jogador? Pessoa(IReadOnlyDictionary<int, Jogador> pessoas, int id) =>
        pessoas.TryGetValue(id, out var p) ? p : null;
}
