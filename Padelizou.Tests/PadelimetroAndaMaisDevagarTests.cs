using Padelizou.Services;

namespace Padelizou.Tests;

// 09/11/2026 — O NÚMERO ANDA MAIS DEVAGAR.
//
// 🗣️ Felipe: "demora mais para as pessoas trocarem de categoria, não é tão rápido, às vezes
// ficam ano na mesma". A ideia inicial era a escala 0–10.000 — descartada porque o Elo é
// proporcional: tudo ×10 dá exatamente a mesma velocidade. O que dá ritmo é o K. Escolha dele:
// "K 24/12, campanha ±5, sem 10.000".
//
// 📊 Simulação (5 jogos por torneio, jogador claramente de 3ª jogando a 4ª): com K 40/20 o
// rótulo virava 3ª em 2 a 5 torneios; com K 24/12, em 6 a 11 — perto de um ano.
public class PadelimetroAndaMaisDevagarTests
{
    private const string Quarta = "4ª Categoria Masculina";   // 550–649

    [Fact]
    public void K_e_24_na_calibracao_e_12_depois()
    {
        Assert.Equal(24, Padelimetro.K(0));
        Assert.Equal(24, Padelimetro.K(9));
        Assert.Equal(12, Padelimetro.K(10));
        Assert.Equal(12, Padelimetro.K(200));
    }

    [Fact]
    public void Vitoria_entre_iguais_vale_metade_do_que_valia()
    {
        // Estável, 6x3 (fator 1,3), contra dupla igual: 12 × 1,3 × 0,5 = 7,8 → 8 (era 13).
        Assert.Equal(8, Padelimetro.Variacao(Padelimetro.K(10), Padelimetro.FatorDeGames(6, 3), venceu: true, 0.5));
        Assert.Equal(-8, Padelimetro.Variacao(Padelimetro.K(10), Padelimetro.FatorDeGames(3, 6), venceu: false, 0.5));
    }

    [Fact]
    public void Campeao_ganha_5()
    {
        var ajuste = CampanhaNoPadelimetro.Ajuste(600, CampeoesDoTorneio.FaseDeCampeao, "Quartas", Quarta);
        Assert.Equal(5, ajuste?.Delta);
    }

    [Fact]
    public void Quem_nao_sai_da_chave_perde_5()
    {
        var ajuste = CampanhaNoPadelimetro.Ajuste(600, CampanhaNoPadelimetro.FicouNaChave, "Quartas", Quarta);
        Assert.Equal(-5, ajuste?.Delta);
    }

    [Fact]
    public void Quem_cai_na_estreia_do_mata_mata_perde_3___metade_de_novo_arredondada()
    {
        // Era −5 (metade da chave). Cortado pela metade junto com o resto: −2,5 → −3, pra
        // continuar MENOR que a pena da chave e não sumir.
        var ajuste = CampanhaNoPadelimetro.Ajuste(600, "Quartas", "Quartas", Quarta);
        Assert.Equal(-3, ajuste?.Delta);
    }
}
