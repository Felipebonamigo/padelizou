using Padelizou.Services;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// "Vencedor Primeira Rodada 1" vira LINK pro jogo citado. Numa lista de 80 linhas, o número
// tornava a referência rastreável mas ainda obrigava a rolar a página inteira atrás dela.
public class AncoraDoJogoTests
{
    [Fact]
    public void O_id_e_o_mesmo_pra_linha_e_pro_link()
    {
        // A garantia que sustenta a feature: os dois lados chamam a MESMA função. Se algum dia
        // uma view montar o id "na mão", o link vira um clique que não vai a lugar nenhum —
        // silenciosamente, que é o pior jeito de quebrar.
        var daLinha = AncoraDoJogo.Id("6ª Categoria Masculina", "Quartas de Final", 2);
        var doLink = AncoraDoJogo.Id("6ª Categoria Masculina", "Quartas de Final", 2);

        Assert.Equal(daLinha, doLink);
    }

    [Fact]
    public void Id_sai_sem_acento_espaco_nem_simbolo()
    {
        // Vai em href="#..." e em querySelector: caractere fora do ASCII deixa os dois frágeis.
        // O "ª" cai fora — ele é LETRA pra Unicode e a normalização não o decompõe, então
        // `char.IsLetterOrDigit` o deixava passar (foi como este teste nasceu).
        var id = AncoraDoJogo.Id("6ª Categoria Masculina", "Quartas de Final", 2);

        Assert.Equal("jogo-6-categoria-masculina-quartas-de-final-2", id);
        Assert.Matches("^[a-z0-9-]+$", id);
    }

    [Fact]
    public void Jogos_diferentes_nunca_compartilham_o_id()
    {
        var ids = new[]
        {
            AncoraDoJogo.Id("6ª Masculina", "Semifinal", 1),
            AncoraDoJogo.Id("6ª Masculina", "Semifinal", 2),
            AncoraDoJogo.Id("6ª Masculina", "Final", 1),
            AncoraDoJogo.Id("6ª Feminina", "Semifinal", 1),
            AncoraDoJogo.Id("Mata-Mata Geral", "Semifinal", 1),
        };

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Categoria_sem_nome_ainda_devolve_id_valido()
    {
        // Torneio de categoria única pode chegar aqui sem nome; id vazio quebraria o href.
        var id = AncoraDoJogo.Id("", "Final", 1);

        Assert.Matches("^[a-z0-9-]+$", id);
    }

    [Fact]
    public void O_lado_que_vem_de_um_jogo_carrega_a_procedencia_desmontada()
    {
        // Sem isto a tela teria que ADIVINHAR a fase e o número lendo o texto do rótulo —
        // e "Vencedor Quartas de Final 1" só é fácil de quebrar até alguém mudar o texto.
        var quartas = Enumerable.Range(1, 4)
            .Select(i => new PartidaDaChave(i, "Quartas de Final", $"A{i}", $"B{i}",
                                            new DateTime(2026, 8, 5, 21, 0, 0)))
            .ToList();

        var jogos = ProximasFasesDaChave.Agendar(
            new[] { ProximasFasesDaChave.Montar(quartas, Array.Empty<string>(), "6ª Masculina") },
            new ConfiguracaoDaGrade(11, 5, Array.Empty<string>(),
                                    new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0)));

        var semi = jogos.First(j => j.Fase == "Semifinal");

        Assert.Equal("Quartas de Final", semi.Lado1.DeQualFase);
        Assert.Equal(1, semi.Lado1.DeQualNumero);
        Assert.Equal("Quartas de Final", semi.Lado2.DeQualFase);
        Assert.Equal(4, semi.Lado2.DeQualNumero);   // primeiro cruza com último
    }

    [Fact]
    public void Colocacao_de_grupo_e_bye_nao_apontam_pra_jogo_nenhum()
    {
        // "2º do Grupo C" e o nome de quem passou direto não são jogos — virar link levaria
        // pro vazio.
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B", "Grupo C" }, 2,
            new DateTime(2026, 8, 5, 21, 0, 0), "6ª Feminina");

        var jogos = ProximasFasesDaChave.Agendar(new[] { cadeia },
            new ConfiguracaoDaGrade(11, 5, Array.Empty<string>(),
                                    new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0)));

        var primeiraRodada = jogos.First();

        Assert.Null(primeiraRodada.Lado1.DeQualFase);
        Assert.Null(primeiraRodada.Lado1.DeQualNumero);
        Assert.StartsWith("1º do Grupo", primeiraRodada.Lado1.Rotulo);
    }
}
