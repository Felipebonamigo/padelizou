using Padelizou.Services;

namespace Padelizou.Tests;

// O SORTEADOR DE DUPLAS DA PANELINHA (Felipe, 18/08/2026: "para que as duplas não sejam
// sempre as mesmas").
//
// Duas regras que puxam pra lados opostos, e a ordem entre elas é o teste: o LADO manda
// (dupla de padel tem um na direita e um na esquerda, e isso é físico), e dentro do que o
// lado permite é que se sorteia evitando parceria repetida.
public class SorteioDeDuplasTests
{
    // `confirmou: true` por padrão porque estes testes são sobre LADO e PARCERIA, não sobre
    // presença — e `Confirmou` não tem default no record (ver SorteioDeDuplas.Candidato) de
    // propósito: o compilador obriga cada chamador a decidir em vez de herdar uma mentira.
    private static SorteioDeDuplas.Candidato Direita(int id, bool confirmou = true) => new(id, $"Destro {id}", LadoNaQuadra.Direita, confirmou);
    private static SorteioDeDuplas.Candidato Esquerda(int id, bool confirmou = true) => new(id, $"Canhoto {id}", LadoNaQuadra.Esquerda, confirmou);
    private static SorteioDeDuplas.Candidato Ambos(int id, bool confirmou = true) => new(id, $"Coringa {id}", LadoNaQuadra.Ambos, confirmou);

    private static readonly Dictionary<(int, int), int> SemHistorico = new();

    private static SorteioDeDuplas.Resultado Sortear(
        IReadOnlyList<SorteioDeDuplas.Candidato> gente,
        IReadOnlyDictionary<(int, int), int>? historico = null,
        int semente = 42,
        bool respeitarLado = true) =>
        SorteioDeDuplas.Sortear(gente, historico ?? SemHistorico, new Random(semente), respeitarLado);

    [Fact]
    public void Com_lados_certinhos_cada_um_joga_no_lado_dele()
    {
        var r = Sortear([Direita(1), Direita(2), Esquerda(3), Esquerda(4)]);

        Assert.Equal(2, r.Duplas.Count);
        Assert.All(r.Duplas, d =>
        {
            Assert.Contains(d.Direita.JogadorId, new[] { 1, 2 });
            Assert.Contains(d.Esquerda.JogadorId, new[] { 3, 4 });
            Assert.False(d.Direita.ForaDoLadoDele);
            Assert.False(d.Esquerda.ForaDoLadoDele);
        });
        Assert.Empty(r.Avisos);
    }

    // Quem joga dos dois lados é o coringa: tapa o buraco SEM contar como deslocado, porque
    // jogar dos dois lados é o lado dele.
    [Fact]
    public void Quem_joga_dos_dois_lados_tapa_o_buraco_sem_virar_deslocado()
    {
        var r = Sortear([Direita(1), Direita(2), Ambos(3), Ambos(4)]);

        Assert.Equal(2, r.Duplas.Count);
        Assert.All(r.Duplas, d => Assert.Contains(d.Direita.JogadorId, new[] { 1, 2 }));
        Assert.All(r.Duplas, d => Assert.Contains(d.Esquerda.JogadorId, new[] { 3, 4 }));
        Assert.All(r.Duplas, d => Assert.False(d.Direita.ForaDoLadoDele || d.Esquerda.ForaDoLadoDele));
        Assert.Empty(r.Avisos);
    }

    // O caso mais comum de panelinha de verdade: só destro. Não se recusa a sortear — joga-se
    // assim mesmo e a tela diz o que aconteceu.
    [Fact]
    public void So_destros_avisa_que_faltou_esquerda_e_sorteia_assim_mesmo()
    {
        var r = Sortear([Direita(1), Direita(2), Direita(3), Direita(4)]);

        Assert.Equal(2, r.Duplas.Count);
        Assert.Contains(r.Avisos, a => a.Contains("ESQUERDA"));
        Assert.Equal(2, r.Duplas.Count(d => d.Esquerda.ForaDoLadoDele));
        Assert.All(r.Duplas, d => Assert.False(d.Direita.ForaDoLadoDele));
    }

    [Fact]
    public void So_canhotos_avisa_que_faltou_direita()
    {
        var r = Sortear([Esquerda(1), Esquerda(2), Esquerda(3), Esquerda(4)]);

        Assert.Contains(r.Avisos, a => a.Contains("DIREITA"));
        Assert.Equal(2, r.Duplas.Count(d => d.Direita.ForaDoLadoDele));
    }

    // Desloca só o necessário: com 3 destros e 1 canhoto, UM destro vai pra esquerda, não dois.
    [Fact]
    public void Desloca_so_quem_for_inevitavel()
    {
        var r = Sortear([Direita(1), Direita(2), Direita(3), Esquerda(4)]);

        Assert.Equal(2, r.Duplas.Count);
        Assert.Equal(1, r.Duplas.Count(d => d.Esquerda.ForaDoLadoDele));
        Assert.Contains(r.Avisos, a => a.Contains("1 jogador"));
    }

    [Fact]
    public void Impar_deixa_alguem_de_fora_e_diz_quem()
    {
        var r = Sortear([Direita(1), Direita(2), Esquerda(3), Esquerda(4), Ambos(5)]);

        Assert.Equal(2, r.Duplas.Count);
        Assert.Single(r.Sobraram);
        Assert.Contains(r.Avisos, a => a.Contains("ímpar"));
    }

    // ⚠️ A INVARIANTE QUE NÃO PODE QUEBRAR NUNCA: ninguém joga em duas duplas ao mesmo tempo,
    // e ninguém some. É o defeito que mais custaria caro na quadra e o que menos aparece numa
    // conferência visual — a tela mostraria duplas bonitas com um nome repetido no meio.
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    public void Ninguem_joga_duas_vezes_e_ninguem_some(int quantos)
    {
        var gente = Enumerable.Range(1, quantos)
            .Select(i => i % 3 == 0 ? Esquerda(i) : i % 3 == 1 ? Direita(i) : Ambos(i))
            .ToList();

        var r = Sortear(gente, semente: quantos);

        var usados = r.Duplas.SelectMany(d => new[] { d.Direita.JogadorId, d.Esquerda.JogadorId }).ToList();

        Assert.Equal(usados.Count, usados.Distinct().Count());
        Assert.Equal(quantos / 2 * 2, usados.Count);
        Assert.Equal(quantos % 2, r.Sobraram.Count);
    }

    // ⚠️ O TESTE QUE MATOU A PRIMEIRA IMPLEMENTAÇÃO. Ela era gulosa (cada destro pegava o
    // parceiro com quem menos tinha jogado) e podia se encurralar: casando D2+E2 primeiro —
    // escolha de custo zero, "ótima" naquele passo — sobrava exatamente D1+E1, a única dupla
    // que se queria evitar. Ótimo local, péssimo total.
    [Fact]
    public void Nao_repete_a_parceria_que_ja_jogou_junto()
    {
        var historico = new Dictionary<(int, int), int> { [SorteioDeDuplas.Chave(1, 3)] = 5 };

        // Roda com várias sementes: uma implementação que só ACERTA por sorte não passa.
        for (int semente = 1; semente <= 25; semente++)
        {
            var r = Sortear([Direita(1), Direita(2), Esquerda(3), Esquerda(4)], historico, semente);

            var repetiu = r.Duplas.Any(d => d.Direita.JogadorId == 1 && d.Esquerda.JogadorId == 3);
            Assert.False(repetiu, $"semente {semente}: juntou de novo a dupla que já jogou 5x");
        }
    }

    // Quando NÃO dá pra escapar, o sorteio não trava — só escolhe o menor estrago.
    [Fact]
    public void Com_todo_mundo_ja_tendo_jogado_junto_ainda_assim_sorteia()
    {
        var historico = new Dictionary<(int, int), int>
        {
            [SorteioDeDuplas.Chave(1, 3)] = 2,
            [SorteioDeDuplas.Chave(1, 4)] = 2,
            [SorteioDeDuplas.Chave(2, 3)] = 2,
            [SorteioDeDuplas.Chave(2, 4)] = 2,
        };

        var r = Sortear([Direita(1), Direita(2), Esquerda(3), Esquerda(4)], historico);

        Assert.Equal(2, r.Duplas.Count);
        Assert.All(r.Duplas, d => Assert.Equal(2, d.VezesQueJaJogaramJuntos));
    }

    // A razão de existir do botão: clicar de novo tem que poder dar outra coisa. Sem isto o
    // "sorteio" seria uma ordenação — e as duplas continuariam sempre as mesmas.
    [Fact]
    public void Sementes_diferentes_dao_duplas_diferentes()
    {
        var gente = Enumerable.Range(1, 8).Select(id => Ambos(id)).ToList();

        var resultados = Enumerable.Range(1, 12)
            .Select(s => string.Join("|", Sortear(gente, semente: s)
                .Duplas.Select(d => $"{d.Direita.JogadorId}-{d.Esquerda.JogadorId}")))
            .Distinct()
            .Count();

        Assert.True(resultados > 1, "o sorteio devolveu sempre a mesma formação — não está sorteando");
    }

    [Fact]
    public void Sem_respeitar_lado_ninguem_e_marcado_como_deslocado()
    {
        var r = Sortear([Direita(1), Direita(2), Direita(3), Direita(4)], respeitarLado: false);

        Assert.Equal(2, r.Duplas.Count);
        Assert.All(r.Duplas, d => Assert.False(d.Direita.ForaDoLadoDele || d.Esquerda.ForaDoLadoDele));
        Assert.DoesNotContain(r.Avisos, a => a.Contains("ESQUERDA"));
        Assert.Contains(r.Avisos, a => a.Contains("SEM olhar o lado"));
    }

    [Fact]
    public void Com_menos_de_dois_nao_da_pra_formar_dupla()
    {
        var r = Sortear([Direita(1)]);

        Assert.Empty(r.Duplas);
        Assert.Contains(r.Avisos, a => a.Contains("pelo menos 2"));
    }

    // ⚠️ O CONTRATO COM A TELA. O resultado não é gravado em tabela nenhuma: o controller
    // serializa em JSON, guarda no TempData e a view desserializa do outro lado do redirect.
    // Se essa ida e volta não funcionar, o botão fica MUDO — sorteia, redireciona e a tela não
    // mostra nada, sem erro em lugar nenhum. Nenhum teste de regra pegaria isso, porque a
    // regra está certa; quem quebra é o transporte (`record` posicional com propriedades
    // `IReadOnlyList`, que é exatamente o formato que costuma voltar vazio).
    [Fact]
    public void O_resultado_sobrevive_a_ida_e_volta_em_json()
    {
        var original = Sortear([Direita(1), Direita(2), Direita(3), Esquerda(4)]);

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var voltou = System.Text.Json.JsonSerializer.Deserialize<SorteioDeDuplas.Resultado>(json);

        Assert.NotNull(voltou);
        Assert.Equal(original.Duplas.Count, voltou!.Duplas.Count);
        Assert.Equal(original.Avisos, voltou.Avisos);
        Assert.Equal(original.Duplas[0].Direita.Nome, voltou.Duplas[0].Direita.Nome);
        Assert.Equal(original.Duplas[0].Direita.JogadorId, voltou.Duplas[0].Direita.JogadorId);
        // O marcador de deslocado é o que a tela pinta de amarelo — se ele voltasse `false`,
        // o aviso pessoa-a-pessoa sumiria e ninguém notaria.
        Assert.Equal(original.Duplas.Count(d => d.Esquerda.ForaDoLadoDele),
                     voltou.Duplas.Count(d => d.Esquerda.ForaDoLadoDele));
    }
}
