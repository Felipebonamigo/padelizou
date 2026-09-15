using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// A COLUNA "HORA" DO MAPA DE OCUPAÇÃO NÃO VAI EMBORA QUANDO A TABELA ROLA PRO LADO.
//
// 🐛 O DEFEITO QUE ESTE ARQUIVO PRENDE: é o mesmo de 15/09 na agenda do professor
// (ColunaDasHorasNaoSomeAoRolarTests), na outra planilha do sistema. Aqui são sete dias numa
// tabela dentro de `.table-responsive`, que rola de lado no celular: a coluna "Hora" — a única
// que diz A QUE HORA pertence a linha — saía da tela junto com os dias, e as células ocupadas
// viravam nomes sem horário nenhum.
//
// ⚠️ É TESTE DE FONTE, mesma escolha consciente dos outros dois arquivos de grade: não há
// suíte de CSS neste projeto, e a alternativa era não travar nada.
public class ColunaDaHoraNoMapaDeOcupacaoTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "ClubeGestao", "Ocupacao.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Ocupacao.cshtml não encontrado a partir do bin.");
    }

    // A regra da primeira coluna do mapa, seja qual for a ordem dos seletores.
    private static string RegraDaPrimeiraColuna()
    {
        var regra = Regex.Match(Fonte(), @"([^{}]*\.pdz-mapa[^{}]*:first-child[^{}]*)\{([^}]*)\}");
        Assert.True(regra.Success, "não há regra de CSS mirando a primeira coluna do .pdz-mapa");
        return regra.Groups[2].Value;
    }

    [Fact]
    public void A_coluna_da_hora_fica_PARADA_enquanto_o_mapa_rola()
    {
        var regra = RegraDaPrimeiraColuna();

        Assert.Matches(new Regex(@"position:\s*sticky"), regra);
        // Sem o `left`, o `sticky` não gruda em lugar nenhum.
        Assert.Matches(new Regex(@"left:\s*0"), regra);
    }

    // As células dos dias passam POR BAIXO da coluna parada — e o fundo dela é o MESMO do resto
    // da tabela (`--bs-table-bg`, que o Bootstrap já põe em toda célula). Um token diferente
    // aqui pintaria a coluna congelada de outra cor que o resto da tabela no tema escuro.
    [Fact]
    public void As_celulas_dos_dias_passam_por_BAIXO_da_coluna_da_hora()
    {
        var regra = RegraDaPrimeiraColuna();

        Assert.Matches(new Regex(@"background:\s*var\(--bs-table-bg"), regra);
        Assert.Matches(new Regex(@"z-index:\s*[1-9]"), regra);
    }

    // ⚠️ A REGRA MIRA `:first-child`, ENTÃO QUEM CONGELA É A POSIÇÃO, NÃO O CONTEÚDO. Se um dia
    // entrar uma coluna ANTES da hora, o congelamento muda de coluna sozinho e em silêncio —
    // este teste é o que quebra nesse dia.
    [Fact]
    public void A_primeira_coluna_da_tabela_e_a_da_HORA()
    {
        var fonte = Fonte();

        // No cabeçalho, o primeiro <th> da linha é o "Hora".
        Assert.Matches(new Regex(@"<tr[^>]*>\s*<th[^>]*>Hora</th>"), fonte);

        // No corpo, a primeira <td> da linha é a que escreve a hora.
        Assert.Matches(new Regex(@"<tr>\s*<td[^>]*>@h\.ToString"), fonte);
    }
}
