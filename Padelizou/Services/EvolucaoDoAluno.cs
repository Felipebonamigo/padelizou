namespace Padelizou.Services;

// "O aluno melhorou?" — comparando duas fichas do mesmo aluno.
//
// É a metade do pedido que a ficha sozinha não responde: o Felipe pediu que o aluno visse
// "tanto a avaliação total como depois a evolução". Uma ficha diz onde ele está; duas dizem
// pra onde ele foi, e é isso que faz o aluno voltar.
public static class EvolucaoDoAluno
{
    // Uma nota solta, do jeito que sai do banco, sem arrastar as entidades pra cá.
    public record NotaLida(int FundamentoId, string Fundamento, string Familia, string? Execucao, int? Acertos);

    public enum Rumo { Subiu, Igual, Caiu, Novo }

    public record Movimento(
        string Fundamento,
        string Familia,
        string? Antes,
        string? Agora,
        int? AcertosAntes,
        int? AcertosAgora,
        Rumo Rumo)
    {
        public string Seta => Rumo switch
        {
            Rumo.Subiu => "bi-arrow-up-right",
            Rumo.Caiu => "bi-arrow-down-right",
            Rumo.Novo => "bi-star-fill",
            _ => "bi-dash",
        };
    }

    // Compara a ficha nova com a anterior. Fundamento que só existe na nova é `Novo` — não é
    // "subiu": o aluno não melhorou nada ali, o professor é que passou a avaliar aquilo.
    //
    // Fundamento que estava na antiga e sumiu da nova NÃO entra: o professor avalia o que
    // trabalhou na aula, e a ausência não quer dizer que o aluno piorou. Chamar isso de queda
    // seria inventar uma notícia ruim a partir de silêncio.
    public static List<Movimento> Comparar(IReadOnlyList<NotaLida> anterior, IReadOnlyList<NotaLida> atual)
    {
        var antes = anterior.ToDictionary(n => n.FundamentoId);

        return atual.Select(agora =>
        {
            antes.TryGetValue(agora.FundamentoId, out var velho);

            var rumo = velho == null
                ? Rumo.Novo
                : Comparar(velho.Execucao, agora.Execucao, velho.Acertos, agora.Acertos);

            return new Movimento(agora.Fundamento, agora.Familia,
                velho?.Execucao, agora.Execucao, velho?.Acertos, agora.Acertos, rumo);
        }).ToList();
    }

    // A execução manda: é a régua que diz como o golpe sai. Os acertos servem de desempate
    // quando a letra não mudou — subir de 5 pra 8 acertos com a mesma execução É evolução, e
    // ignorar isso apagaria o progresso mais comum entre duas aulas seguidas.
    private static Rumo Comparar(string? execAntes, string? execAgora, int? acertosAntes, int? acertosAgora)
    {
        int a = EscalaDeAvaliacao.PesoDaExecucao(execAntes);
        int b = EscalaDeAvaliacao.PesoDaExecucao(execAgora);

        if (a >= 0 && b >= 0 && a != b) return b > a ? Rumo.Subiu : Rumo.Caiu;

        if (acertosAntes.HasValue && acertosAgora.HasValue && acertosAntes != acertosAgora)
            return acertosAgora > acertosAntes ? Rumo.Subiu : Rumo.Caiu;

        // Só passou a ter nota de execução onde antes não havia: é avaliação nova, não ganho.
        if (a < 0 && b >= 0 && !acertosAntes.HasValue) return Rumo.Novo;

        return Rumo.Igual;
    }

    // Um resumo em uma linha, pro topo da tela: "3 subiram · 1 caiu · 12 iguais".
    public static string Resumo(IReadOnlyList<Movimento> movimentos)
    {
        int subiram = movimentos.Count(m => m.Rumo == Rumo.Subiu);
        int cairam = movimentos.Count(m => m.Rumo == Rumo.Caiu);
        int novos = movimentos.Count(m => m.Rumo == Rumo.Novo);

        var partes = new List<string>();
        if (subiram > 0) partes.Add($"{subiram} {(subiram == 1 ? "subiu" : "subiram")}");
        if (cairam > 0) partes.Add($"{cairam} {(cairam == 1 ? "caiu" : "caíram")}");
        if (novos > 0) partes.Add($"{novos} {(novos == 1 ? "novo" : "novos")}");

        return partes.Count == 0 ? "Sem mudanças desde a ficha anterior" : string.Join(" · ", partes);
    }
}
