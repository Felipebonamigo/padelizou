using Padelizou.Services;

namespace Padelizou.Tests;

// O PESO POR TAMANHO DA CATEGORIA (10/08/2026) — espec em RANKING.md, Trilha B.
//
// A régua: todo mundo leva `pontos da fase × peso`, e o peso é 1,0 com 5 duplas, +0,1 por
// dupla, sem teto. Mas o ponto só existe DEPOIS que o torneio começou.
public class PontosDoTorneioTests
{
    // O status de um torneio que já rolou. A conta só paga a partir daqui — e escrever isso
    // uma vez evita que cada teste repita a string e um deles fique com a régua velha.
    private const string Comecou = "Fase de Grupos";

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

    // ── O ponto só nasce quando a bola rola ───────────────────────────────────────────
    [Theory]
    [InlineData(PortaDaInscricao.Aberta)]      // ninguém jogou: inscrição ainda aberta
    [InlineData(PortaDaInscricao.Fechada)]     // "Chaves em Sorteio": fechou, não sorteou
    [InlineData(CancelamentoDoTorneio.Status)] // cancelado: o evento não aconteceu
    public void Torneio_que_nao_comecou_nao_paga_nada(string status)
    {
        // ⚠️ Pedido do Felipe (10/08/2026): "só dê esses pontos quando o torneio começar".
        // Era buraco de verdade — `Dupla.UltimaFase` NASCE valendo "Grupos", então a
        // inscrição sozinha já pagava. Com a participação multiplicando, inscrever-se na
        // categoria mais cheia que existisse viraria a maneira mais barata de subir.
        Assert.Equal(0, PontosDoTorneio.Pontos("Grupos", 20, status));
        Assert.Equal(0, PontosDoTorneio.Pontos("Campeao", 20, status));
        Assert.False(PontosDoTorneio.TorneioJaComecou(status));
    }

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Finalizado")]
    [InlineData(null)]  // torneio não carregado numa consulta sem Include — ver ContaNoRanking
    public void Torneio_em_andamento_ou_encerrado_paga(string? status)
    {
        // ⚠️ `null` paga de propósito, pela MESMA razão já escrita em `ContaNoRanking`: sumir
        // do ranking por causa de um `Include` esquecido é pior do que qualquer das regras.
        Assert.True(PontosDoTorneio.TorneioJaComecou(status));
        Assert.Equal(100, PontosDoTorneio.Pontos("Campeao", 5, status));
    }

    [Fact]
    public void Cancelar_um_torneio_ja_sorteado_apaga_os_pontos_dele()
    {
        // O torneio pode ser cancelado DEPOIS de sortear, e aí os pontos que existiam somem.
        // É o comportamento certo (o evento não aconteceu) e é o único caminho em que o total
        // de alguém diminui sem ninguém ter mexido na regra — por isso está escrito.
        Assert.Equal(250, PontosDoTorneio.Pontos("Campeao", 20, Comecou));
        Assert.Equal(0, PontosDoTorneio.Pontos("Campeao", 20, CancelamentoDoTorneio.Status));
    }

    // ── A referência: com 5 duplas, nada muda ─────────────────────────────────────────
    [Theory]
    [InlineData("Campeao", 100)]
    [InlineData("Final", 60)]
    [InlineData("Semifinal", 35)]
    [InlineData("Quartas de Final", 20)]
    [InlineData("Grupos", 10)]
    public void Com_cinco_duplas_os_pontos_sao_os_de_sempre(string fase, int esperado)
    {
        // É o ponto de calibração da régua: no torneio pequeno de sempre os números não
        // mudam, e o ranking que já existe quase não se mexe. Se este teste ficar vermelho,
        // alguém mexeu na escala inteira sem perceber.
        Assert.Equal(esperado, PontosDoTorneio.Pontos(fase, duplasNaCategoria: 5, Comecou));
    }

    // ── Torneio maior paga mais ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(8, 130)]
    [InlineData(12, 170)]
    [InlineData(16, 210)]
    [InlineData(20, 250)]
    [InlineData(26, 310)]
    public void Campeao_de_categoria_grande_leva_mais(int duplas, int esperado)
        => Assert.Equal(esperado, PontosDoTorneio.Pontos("Campeao", duplas, Comecou));

    [Fact]
    public void Chegar_menos_longe_num_funil_maior_pode_valer_mais()
    {
        // A leitura de justiça que a régua promete, e o motivo de o peso existir: semifinal
        // numa categoria de 20 duplas (88) vale mais que o TÍTULO de uma de 3 (80).
        Assert.Equal(88, PontosDoTorneio.Pontos("Semifinal", 20, Comecou));
        Assert.Equal(80, PontosDoTorneio.Pontos("Campeao", 3, Comecou));
        Assert.True(PontosDoTorneio.Pontos("Semifinal", 20, Comecou)
                  > PontosDoTorneio.Pontos("Campeao", 3, Comecou));

        // E o caso extremo da régua nova: CAIR NO GRUPO de uma categoria de 26 duplas (31)
        // vale mais que ser semifinalista numa de 3 (28) — atravessar aquele campo era mais
        // difícil do que o torneio de 3 duplas inteiro.
        Assert.Equal(31, PontosDoTorneio.Pontos("Grupos", 26, Comecou));
        Assert.Equal(28, PontosDoTorneio.Pontos("Semifinal", 3, Comecou));
        Assert.True(PontosDoTorneio.Pontos("Grupos", 26, Comecou)
                  > PontosDoTorneio.Pontos("Semifinal", 3, Comecou));
    }

    // ── A participação TAMBÉM multiplica ──────────────────────────────────────────────
    [Theory]
    [InlineData(3, 8)]
    [InlineData(5, 10)]
    [InlineData(8, 13)]
    [InlineData(20, 25)]
    [InlineData(40, 45)]
    public void Cair_no_grupo_de_um_torneio_maior_paga_mais(int duplas, int esperado)
    {
        // ⚠️ Decisão do Felipe (10/08/2026), revendo a primeira versão da espec, em que os 10
        // eram fixos: "tem q dar mais pontos tambem para quem é eliminado na chave de um
        // torneio mais forte". É a lógica do circuito profissional — perder na estreia de um
        // Slam paga mais que vencer rodada de torneio pequeno.
        Assert.Equal(esperado, PontosDoTorneio.Pontos("Grupos", duplas, Comecou));
        Assert.Equal(esperado, PontosDoTorneio.Pontos("Grupo A", duplas, Comecou));
        Assert.Equal(esperado, PontosDoTorneio.Pontos(null, duplas, Comecou));
    }

    [Fact]
    public void A_participacao_nunca_precisa_de_arredondamento()
    {
        // 10 × (0,5 + d/10) = 5 + d, exato. Não é curiosidade: é o que garante que dois
        // jogadores eliminados na mesma categoria jamais saiam com números diferentes.
        foreach (int duplas in new[] { 3, 7, 11, 19, 33, 64 })
            Assert.Equal(5 + duplas, PontosDoTorneio.Pontos("Grupos", duplas, Comecou));
    }

    [Fact]
    public void Sair_do_grupo_numa_categoria_grande_continua_sendo_um_degrau()
    {
        // O degrau sobreviveu à mudança, e era a preocupação de quem defendia o 10 fixo:
        // numa categoria de 20 duplas, cair no grupo = 25 e passar pros 16-avos = 30.
        Assert.Equal(25, PontosDoTorneio.Pontos("Grupos", 20, Comecou));
        Assert.Equal(30, PontosDoTorneio.Pontos(ChaveamentoMataMata.PrimeiraRodada, 20, Comecou));
        Assert.True(PontosDoTorneio.Pontos(ChaveamentoMataMata.PrimeiraRodada, 20, Comecou)
                  > PontosDoTorneio.Pontos("Grupos", 20, Comecou));
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
        // Monotonia: grupos < 16-avos < Oitavas < Quartas < Semi < Vice < Campeão. Vale a
        // mesma categoria, então basta comparar as bases.
        var escadaDoPiorProMelhor = new[]
        {
            "Grupos", ChaveamentoMataMata.PrimeiraRodada, "Oitavas de Final",
            "Quartas de Final", "Semifinal", "Final", "Campeao",
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
        // 64) não quebraria nada: a fase simplesmente cairia em "participou" — o campeão
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
    [InlineData(1, 6)]
    [InlineData(2, 7)]
    public void Categoria_com_menos_de_tres_duplas_paga_todo_mundo_igual(int duplas, int esperado)
    {
        // Com 1 dupla o "campeão" não jogou nada; com 2, ganhou um jogo só. É resultado
        // fabricável em cinco minutos — a mesma porta que o piso de 8 fecha no Americano.
        //
        // ⚠️ O piso NÃO apaga o ponto de quem jogou: a campanha desaba pra participação, e
        // campeão, vice e eliminado saem com o MESMO número. Zerar puniria quem se inscreveu
        // num torneio que ficou vazio — coisa que ele não controla.
        Assert.Equal(esperado, PontosDoTorneio.Pontos("Campeao", duplas, Comecou));
        Assert.Equal(esperado, PontosDoTorneio.Pontos("Final", duplas, Comecou));
        Assert.Equal(esperado, PontosDoTorneio.Pontos("Grupos", duplas, Comecou));
    }

    [Fact]
    public void Com_tres_duplas_a_campanha_ja_vale()
    {
        Assert.Equal(80, PontosDoTorneio.Pontos("Campeao", 3, Comecou));
        Assert.Equal(8, PontosDoTorneio.Pontos("Grupos", 3, Comecou));
    }

    // ── Arredondamento ────────────────────────────────────────────────────────────────
    [Fact]
    public void Meio_ponto_arredonda_pra_cima_e_nao_pro_par()
    {
        // Semifinal (35) × peso 1,3 = 45,5. Com o `ToEven` padrão do .NET viraria 46 aqui e
        // 44 num caso vizinho — dois jogadores com a MESMA conta receberiam números
        // diferentes conforme a paridade.
        Assert.Equal(46, PontosDoTorneio.Pontos("Semifinal", 8, Comecou));    // 45,5
        Assert.Equal(32, PontosDoTorneio.Pontos("Oitavas de Final", 16, Comecou));  // 31,5
        Assert.Equal(38, PontosDoTorneio.Pontos("Oitavas de Final", 20, Comecou));  // 37,5
    }

    [Fact]
    public void Fase_desconhecida_cai_na_participacao_em_vez_de_estourar()
    {
        // Defensivo de propósito: fase vinda de dado velho não pode derrubar o ranking.
        Assert.Equal(25, PontosDoTorneio.Pontos("Fase Que Nao Existe", 20, Comecou));
        Assert.False(PontosDoTorneio.ValeCampanha(""));
    }
}
