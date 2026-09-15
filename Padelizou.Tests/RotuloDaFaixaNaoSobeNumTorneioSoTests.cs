using Padelizou.Services;

namespace Padelizou.Tests;

// 14/09/2026 — O RÓTULO DA FAIXA NÃO PODE TROCAR COM UM TORNEIO SÓ.
//
// 🗣️ Felipe, vendo quatro "2ª" no Padelímetro de um torneio cuja categoria mais forte era a 3ª:
// *"a faixa está errada, essas pessoas estão com nível mais alto que estão jogando"* · *"foi
// apenas um torneio e estamos exigindo subir, acho que isso não pode ser assim"*.
//
// 🕳️ O rótulo era `DoNivel(pdz)` — a faixa CRUA do número, sem folga nenhuma. Depois do
// primeiro torneio da história do Padelímetro, 52 dos 128 jogadores (41%) apareceram numa
// faixa diferente da que jogaram: 22 acima, 30 abaixo. A causa é aritmética: a faixa tem 100
// de largura e o seed nasce no MEIO (3ª é 650–749, entra em 700), então bastam +50 — e o
// próprio RANKING.md diz que um fim de semana dominante rende "+60 a +100".
//
// 🔑 A régua nova (RANKING.md, "O RÓTULO da tela não é a trava"): em calibração o rótulo é a
// faixa da CATEGORIA QUE A PESSOA JOGA; passada a calibração, o número manda com folga de 50
// pros dois lados.
//
// ⚠️ Os casos deste arquivo são os NÚMEROS REAIS medidos na produção em 14/09/2026 — não são
// inventados. Foi assim que o defeito apareceu, e é assim que ele fica travado.
public class RotuloDaFaixaNaoSobeNumTorneioSoTests
{
    private const string Terceira = "3ª Categoria Masculina";   // 650–749, entra em 700
    private const string Quarta = "4ª Categoria Masculina";     // 550–649, entra em 600
    private const string SextaFem = "6ª Categoria Feminina";    // 60–149, entra em 120

    private static string Rotulo(int pdz, string? categoria, int jogos) =>
        FaixasDePadelimetro.FaixaExibida(pdz, categoria, jogos).Rotulo;

    [Fact]
    public void Arthur_Guex_ganhou_tudo_na_3a_e_continua_na_3a()
    {
        // O caso do print: 802 PDZ com 4 jogos, jogando a 3ª. Antes saía "2ª" — numa tabela de
        // um torneio que não TINHA 2ª (as categorias iam da 3ª à 7ª).
        Assert.Equal("3ª", Rotulo(802, Terceira, jogos: 4));
    }

    [Fact]
    public void Alexandre_Longhi_subiu_142_num_torneio_e_continua_na_4a()
    {
        // A maior subida medida: 600 → 742 em 5 jogos. Mais que UMA FAIXA INTEIRA de largura,
        // num fim de semana. Antes saía "3ª".
        Assert.Equal("4ª", Rotulo(742, Quarta, jogos: 5));
    }

    [Fact]
    public void E_quem_apanhou_tambem_nao_e_rebaixado_na_estreia()
    {
        // Arthur Prass: 700 → 635 em 2 jogos, jogando a 3ª. Antes saía "4ª" — rebaixado por
        // dois jogos. A régua de descida do RANKING.md nunca permitiu isso; a tela é que não
        // lia a folga.
        Assert.Equal("3ª", Rotulo(635, Terceira, jogos: 2));
    }

    [Fact]
    public void A_escada_FEMININA_continua_sendo_a_dela()
    {
        // Cristina Bassols: 120 → 227 em 4 jogos na 6ª Feminina. O mesmo 227 é outra faixa na
        // régua masculina — a escada vem da categoria, e isso não pode se perder no caminho.
        Assert.Equal("6ª", Rotulo(227, SextaFem, jogos: 4));
    }

    [Fact]
    public void Passada_a_CALIBRACAO_o_numero_volta_a_mandar___com_folga_dos_dois_lados()
    {
        // Com 10 jogos a calibração acabou. A 3ª é 650–749, então:
        const int dezJogos = 10;
        Assert.Equal("3ª", Rotulo(799, Terceira, dezJogos));   // teto+50 ainda é 3ª
        Assert.Equal("2ª", Rotulo(800, Terceira, dezJogos));   // passou da folga → sobe
        Assert.Equal("3ª", Rotulo(600, Terceira, dezJogos));   // piso−50 ainda é 3ª
        Assert.Equal("4ª", Rotulo(599, Terceira, dezJogos));   // passou da folga → desce
    }

    [Fact]
    public void Quem_nao_tem_categoria_na_escada_segue_com_a_faixa_CRUA()
    {
        // Só jogou mista/casal/lendas: não há âncora de onde partir, então vale o de sempre.
        // Sem isto, a correção apagaria o rótulo de quem só joga mista.
        Assert.Equal(FaixasDePadelimetro.DoNivel(802, feminina: false).Rotulo,
            Rotulo(802, "Mista A", jogos: 4));
        Assert.Equal(FaixasDePadelimetro.DoNivel(802, feminina: false).Rotulo,
            Rotulo(802, null, jogos: 4));
    }

    [Fact]
    public void O_FALTAM_X_mede_o_que_muda_o_rotulo___e_nao_a_faixa_crua()
    {
        // Um "faltam 8 pra subir" ao lado de um rótulo que não muda em 8 é a mesma mentira
        // pequena do selo de movimento. Em calibração não há o que dizer; depois dela, o que
        // falta é chegar na folga.
        Assert.Null(FaixasDePadelimetro.FaltaPraMudarDeFaixa(742, Quarta, jogos: 5));

        // 10 jogos, joga a 3ª (teto 749): o rótulo vira "2ª" em 800, então faltam 58 de 742.
        Assert.Equal(58, FaixasDePadelimetro.FaltaPraMudarDeFaixa(742, Terceira, jogos: 10));
    }

    [Fact]
    public void A_folga_do_ROTULO_nao_e_a_linha_de_subida_da_CAMPANHA()
    {
        // ⚠️ Cinto de segurança: `LinhaDeSubida` limita o BÔNUS DE CAMPANHA, que mexe no
        // NÚMERO. Se alguém "unificar" as duas constantes, o bônus passa a poder empurrar 50
        // pontos além do teto — mudando o PDZ de todo mundo e exigindo replay, o oposto do que
        // esta mudança se propôs a fazer.
        var terceira = FaixasDePadelimetro.DaCategoria(Terceira)!;
        Assert.Equal(terceira.Teto + 1, FaixasDePadelimetro.LinhaDeSubida(terceira));
        Assert.NotEqual(terceira.Teto + FaixasDePadelimetro.FolgaDoRotulo,
            FaixasDePadelimetro.LinhaDeSubida(terceira));
    }
}
