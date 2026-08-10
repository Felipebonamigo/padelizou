using Padelizou.Services;

namespace Padelizou.Tests;

// O PESO POR TAMANHO DA CATEGORIA (10/08/2026) — espec em RANKING.md, Trilha B.
//
// A régua: quem cai na fase de grupos leva 10, sempre. Quem sobrevive à chave leva
// `pontos da fase × peso`, e o peso é 1,0 com 5 duplas, +0,1 por dupla, sem teto.
public class PontosDoTorneioTests
{
    // ── O peso ────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(3, 0.8)]
    [InlineData(5, 1.0)]    // a referência
    [InlineData(8, 1.3)]
    [InlineData(12, 1.7)]
    [InlineData(16, 2.1)]
    [InlineData(20, 2.5)]
    [InlineData(25, 3.0)]
    [InlineData(26, 3.1)]
    public void Peso_e_um_com_cinco_duplas_e_sobe_um_decimo_por_dupla(int duplas, double esperado)
        => Assert.Equal((decimal)esperado, PontosDoTorneio.Peso(duplas));

    [Fact]
    public void O_peso_nao_tem_teto()
    {
        // Decisão do Felipe (10/08/2026). Um teto criaria uma zona plana onde 25 e 40 duplas
        // valem igual — exatamente a injustiça que o peso existe pra consertar.
        Assert.Equal(4.5m, PontosDoTorneio.Peso(40));
        Assert.True(PontosDoTorneio.Peso(60) > PontosDoTorneio.Peso(40));
    }

    // ── A referência: com 5 duplas, nada muda ─────────────────────────────────────────
    [Theory]
    [InlineData("Campeao", 100)]
    [InlineData("Final", 60)]
    [InlineData("Semifinal", 35)]
    [InlineData("Quartas de Final", 20)]
    public void Com_cinco_duplas_os_pontos_sao_os_de_sempre(string fase, int esperado)
    {
        // É o ponto de calibração da régua: no torneio pequeno de sempre os números não
        // mudam, e o ranking que já existe quase não se mexe. Se este teste ficar vermelho,
        // alguém mexeu na escala inteira sem perceber.
        Assert.Equal(esperado, PontosDoTorneio.Pontos(fase, duplasNaCategoria: 5));
    }

    // ── Torneio maior paga mais ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(8, 130)]
    [InlineData(12, 170)]
    [InlineData(16, 210)]
    [InlineData(20, 250)]
    [InlineData(26, 310)]
    public void Campeao_de_categoria_grande_leva_mais(int duplas, int esperado)
        => Assert.Equal(esperado, PontosDoTorneio.Pontos("Campeao", duplas));

    [Fact]
    public void Chegar_menos_longe_num_funil_maior_pode_valer_mais()
    {
        // A leitura de justiça que a régua promete, e o motivo de o peso existir: semifinal
        // numa categoria de 20 duplas (88) vale mais que o TÍTULO de uma de 3 (80).
        Assert.Equal(88, PontosDoTorneio.Pontos("Semifinal", 20));
        Assert.Equal(80, PontosDoTorneio.Pontos("Campeao", 3));
        Assert.True(PontosDoTorneio.Pontos("Semifinal", 20) > PontosDoTorneio.Pontos("Campeao", 3));

        // E sair do grupo numa de 20 (30) vale mais que ser semifinalista numa de 3 (28).
        Assert.True(PontosDoTorneio.Pontos(ChaveamentoMataMata.PrimeiraRodada, 20)
                  > PontosDoTorneio.Pontos("Semifinal", 3));
    }

    // ── A participação NÃO multiplica ─────────────────────────────────────────────────
    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(40)]
    public void Cair_no_grupo_vale_dez_em_qualquer_tamanho(int duplas)
    {
        // Decisão do Felipe: os 10 são o ponto DA INSCRIÇÃO. Multiplicá-los premiaria
        // aparecer num torneio grande e perder tudo.
        Assert.Equal(10, PontosDoTorneio.Pontos("Grupos", duplas));
        Assert.Equal(10, PontosDoTorneio.Pontos("Grupo A", duplas));
        Assert.Equal(10, PontosDoTorneio.Pontos(null, duplas));
    }

    [Fact]
    public void Sair_do_grupo_numa_categoria_grande_e_um_degrau_de_verdade()
    {
        // Numa categoria de 20 duplas: cair no grupo = 10, passar pros 16-avos = 30.
        Assert.Equal(10, PontosDoTorneio.Pontos("Grupos", 20));
        Assert.Equal(30, PontosDoTorneio.Pontos(ChaveamentoMataMata.PrimeiraRodada, 20));
    }

    // ── As duas fases novas ───────────────────────────────────────────────────────────
    [Fact]
    public void Oitavas_e_dezesseis_avos_passaram_a_valer_campanha()
    {
        // Antes de 10/08/2026 as duas caíam em "participou 10": quem sobrevivia aos grupos
        // de uma categoria grande pontuava igual a quem perdeu tudo no grupo.
        Assert.True(PontosDoTorneio.ValeCampanha("Oitavas de Final"));
        Assert.True(PontosDoTorneio.ValeCampanha(ChaveamentoMataMata.PrimeiraRodada));
        Assert.True(PontosDoTorneio.PontosBase("Oitavas de Final") > PontosDoTorneio.PontosDeParticipacao);
        Assert.True(PontosDoTorneio.PontosBase(ChaveamentoMataMata.PrimeiraRodada) > PontosDoTorneio.PontosDeParticipacao);
    }

    [Fact]
    public void A_escada_nunca_paga_mais_por_chegar_menos_longe()
    {
        // Monotonia: 16-avos < Oitavas < Quartas < Semi < Vice < Campeão. Vale a mesma
        // categoria, então basta comparar as bases.
        var escadaDoPiorProMelhor = new[]
        {
            ChaveamentoMataMata.PrimeiraRodada, "Oitavas de Final", "Quartas de Final",
            "Semifinal", "Final", "Campeao",
        };

        for (int i = 1; i < escadaDoPiorProMelhor.Length; i++)
        {
            Assert.True(
                PontosDoTorneio.PontosBase(escadaDoPiorProMelhor[i])
                > PontosDoTorneio.PontosBase(escadaDoPiorProMelhor[i - 1]),
                $"'{escadaDoPiorProMelhor[i]}' deveria valer mais que '{escadaDoPiorProMelhor[i - 1]}'");
        }
    }

    [Fact]
    public void Toda_fase_que_o_chaveamento_gera_vale_campanha()
    {
        // ⚠️ O GUARDA MAIS IMPORTANTE DESTE ARQUIVO. A escada casa a fase pelo NOME, e o nome
        // nasce em `ChaveamentoMataMata.NomeFase`. Renomear uma fase lá (ou criar o quadro de
        // 64) não quebraria nada: a fase simplesmente cairia em "participou 10" — o campeão
        // continuaria pontuando e só quem parou no meio perderia ponto, calado.
        foreach (int quadro in new[] { 32, 16, 8, 4, 2 })
        {
            var fase = ChaveamentoMataMata.NomeFase(quadro);
            Assert.True(PontosDoTorneio.ValeCampanha(fase),
                $"A fase '{fase}' (quadro de {quadro}) não está na escada de pontos.");
        }
    }

    // ── O piso ────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Categoria_com_menos_de_tres_duplas_so_da_o_ponto_da_inscricao(int duplas)
    {
        // Com 1 dupla o "campeão" não jogou nada; com 2, ganhou um jogo só. É resultado
        // fabricável em cinco minutos — a mesma porta que o piso de 8 fecha no Americano.
        Assert.Equal(10, PontosDoTorneio.Pontos("Campeao", duplas));
        Assert.Equal(10, PontosDoTorneio.Pontos("Final", duplas));
    }

    [Fact]
    public void Com_tres_duplas_a_campanha_ja_vale()
        => Assert.Equal(80, PontosDoTorneio.Pontos("Campeao", 3));

    // ── Arredondamento ────────────────────────────────────────────────────────────────
    [Fact]
    public void Meio_ponto_arredonda_pra_cima_e_nao_pro_par()
    {
        // Semifinal (35) × peso 1,3 = 45,5. Com o `ToEven` padrão do .NET viraria 46 aqui e
        // 44 num caso vizinho — dois jogadores com a MESMA conta receberiam números
        // diferentes conforme a paridade.
        Assert.Equal(46, PontosDoTorneio.Pontos("Semifinal", 8));    // 45,5
        Assert.Equal(32, PontosDoTorneio.Pontos("Oitavas de Final", 16));  // 31,5
        Assert.Equal(38, PontosDoTorneio.Pontos("Oitavas de Final", 20));  // 37,5
    }

    [Fact]
    public void Fase_desconhecida_cai_na_participacao_em_vez_de_estourar()
    {
        // Defensivo de propósito: fase vinda de dado velho não pode derrubar o ranking.
        Assert.Equal(10, PontosDoTorneio.Pontos("Fase Que Nao Existe", 20));
        Assert.False(PontosDoTorneio.ValeCampanha(""));
    }
}
