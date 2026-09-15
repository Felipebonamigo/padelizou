using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// A COLUNA DAS HORAS NÃO VAI EMBORA QUANDO A GRADE ROLA PRO LADO.
//
// 🐛 O DEFEITO QUE ESTE ARQUIVO PRENDE (professor Gabriel, 14/09/2026): "tu vai rolando pro
// lado e some os horarios · faz com a planilha pra continuar os horarios ali do lado".
// A grade de Dia/Semana rola inteira dentro do card (ver GradeDaAgendaRolaInteiraTests) — e a
// coluna das horas rolava junto. No celular, arrastar pra ver quinta e sexta levava embora o
// ÚNICO lugar da tela que diz que horas são aquelas faixas: as aulas continuavam ali,
// empilhadas, sem nenhuma hora ao lado. É a primeira coluna congelada da planilha.
//
// ⚠️ É TESTE DE FONTE, pelo mesmo motivo do GradeDaAgendaRolaInteiraTests: não há suíte de
// CSS aqui, e a alternativa era não travar nada. O que ele prende é a INVARIANTE (a coluna
// fica parada, opaca e por cima), não a aparência.
public class ColunaDasHorasNaoSomeAoRolarTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Aulas", "MinhaAgenda.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("MinhaAgenda.cshtml não encontrado a partir do bin.");
    }

    // Todas as declarações de `.pdz-grade-gutter` juntas — a classe tem a regra principal e o
    // ajuste de celular (`flex-basis: 44px`), e a invariante vale sobre o conjunto.
    private static string Gutter()
    {
        var regras = Regex.Matches(Fonte(), @"\.pdz-grade-gutter\s*\{([^}]*)\}")
                          .Select(m => m.Groups[1].Value);
        var junto = string.Concat(regras);
        Assert.False(string.IsNullOrWhiteSpace(junto), "regra .pdz-grade-gutter não encontrada");
        return junto;
    }

    [Fact]
    public void A_coluna_das_horas_fica_PARADA_enquanto_a_grade_rola()
    {
        var gutter = Gutter();

        Assert.Matches(new Regex(@"position:\s*sticky"), gutter);
        // Sem o `left`, o `sticky` não gruda em lugar nenhum — fica igual ao `relative` antigo.
        Assert.Matches(new Regex(@"left:\s*0"), gutter);
    }

    // As aulas passam POR BAIXO da coluna parada. Sem fundo, os cards coloridos atravessam os
    // números; sem z-index acima do `.pdz-evento`, eles passam por CIMA e escondem a hora.
    [Fact]
    public void As_aulas_passam_por_BAIXO_da_coluna_das_horas()
    {
        var gutter = Gutter();
        var evento = Regex.Match(Fonte(), @"\.pdz-evento\s*\{([^}]*)\}");
        Assert.True(evento.Success, "regra .pdz-evento não encontrada");

        var doGutter = Regex.Match(gutter, @"z-index:\s*(\d+)");
        var doEvento = Regex.Match(evento.Groups[1].Value, @"z-index:\s*(\d+)");
        Assert.True(doGutter.Success, "a coluna das horas precisa de z-index pra ficar acima das aulas");
        Assert.True(doEvento.Success, "a aula precisa de z-index — é ele que o da coluna tem que vencer");

        Assert.True(int.Parse(doGutter.Groups[1].Value) > int.Parse(doEvento.Groups[1].Value),
            "a coluna das horas tem que pintar ACIMA da aula que passa por baixo dela");
    }

    // O fundo opaco vem do token do card, não de um `#fff` cravado: com `data-bs-theme="dark"`
    // a coluna viraria uma faixa branca no meio de um card #1a2338.
    [Fact]
    public void O_fundo_opaco_da_coluna_segue_o_tema()
    {
        var gutter = Gutter();

        Assert.Matches(new Regex(@"background:\s*var\(--pdz-surface"), gutter);
    }
}
