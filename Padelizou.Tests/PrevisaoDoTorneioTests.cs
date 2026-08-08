using Padelizou.Services;

namespace Padelizou.Tests;

// "Cabe?" é a pergunta que o organizador faz antes de anunciar o torneio, e que nenhum campo
// da tela responde sozinho. Estes testes prendem a previsão ao que o sorteio REALMENTE faz —
// se as duas contas divergirem, a tela promete uma coisa e a grade entrega outra.
public class PrevisaoDoTorneioTests
{
    [Theory]
    [InlineData(2, 1, 1)]    // 2 duplas: chave direta
    [InlineData(6, 2, 6)]    // 2 grupos de 3
    [InlineData(9, 3, 9)]
    [InlineData(12, 4, 12)]
    [InlineData(13, 5, 11)]  // resto 1: 2 grupos de 2 (1 jogo cada) + 3 de 3 (3 jogos cada)
    [InlineData(14, 5, 13)]  // resto 2: 1 grupo de 2 + 4 de 3
    [InlineData(16, 6, 14)]  // resto 1: 2 grupos de 2 + 4 de 3
    [InlineData(24, 8, 24)]
    public void Fase_de_grupos_espelha_a_regra_do_sorteio(int duplas, int grupos, int jogos)
    {
        var previsto = PrevisaoDoTorneio.FaseDeGrupos(duplas);

        Assert.Equal(grupos, previsto.Grupos);
        Assert.Equal(jogos, previsto.Jogos);
    }

    [Theory]
    [InlineData(1, 1)]    // 1 grupo: final direta
    [InlineData(2, 3)]    // quadro de 4: 2 semis + final
    [InlineData(3, 3)]    // 6 classificados, quadro de 4
    [InlineData(5, 7)]    // 10 classificados, quadro de 8: 4 quartas + 2 semis + final
    [InlineData(6, 7)]
    [InlineData(8, 15)]   // 16 classificados, quadro de 16
    public void Mata_mata_tem_um_jogo_a_menos_que_o_quadro(int grupos, int jogos)
    {
        Assert.Equal(jogos, PrevisaoDoTorneio.MataMata(grupos));
    }

    [Fact]
    public void Sem_duplas_nao_ha_torneio()
    {
        Assert.Equal((0, 0), PrevisaoDoTorneio.FaseDeGrupos(0));
        Assert.Equal((0, 0), PrevisaoDoTorneio.FaseDeGrupos(1));
        Assert.Equal(0, PrevisaoDoTorneio.MataMata(0));
        Assert.Equal(0, PrevisaoDoTorneio.TotalDeJogos(1));
    }

    // O caso que o Felipe descreveu: sexta 18h, sábado e domingo 8h, teto 23h50.
    [Fact]
    public void Torneio_de_fim_de_semana_termina_no_domingo()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(24);   // 8 grupos: 24 + 15 = 39

        Assert.Equal(39, jogos);

        var ultimo = PrevisaoDoTorneio.UltimoJogo(
            sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0),
            quadras: 3, duracaoMinutos: 50, totalDeJogos: jogos);

        // Sexta: 8 rodadas x 3 quadras = 24 jogos. Sábado abre 8h e leva os 15 restantes
        // em 5 rodadas — 8h às 11h20.
        Assert.Equal(new DateTime(2026, 8, 22, 11, 20, 0), ultimo);
        Assert.Equal(2, PrevisaoDoTorneio.DiasOcupados(sexta18h, ultimo!.Value));
    }

    [Fact]
    public void Uma_quadra_so_estica_o_torneio_por_mais_dias()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(24);

        var comTres = PrevisaoDoTorneio.UltimoJogo(sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 3, 50, jogos);
        var comUma = PrevisaoDoTorneio.UltimoJogo(sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 1, 50, jogos);

        // É o que o organizador precisa ver ANTES de anunciar: a mesma grade, com 1 quadra
        // em vez de 3, deixa de caber no fim de semana.
        Assert.True(comUma > comTres);
        Assert.Equal(2, PrevisaoDoTorneio.DiasOcupados(sexta18h, comTres!.Value));
        Assert.True(PrevisaoDoTorneio.DiasOcupados(sexta18h, comUma!.Value) >= 3);
    }

    [Fact]
    public void A_previsao_bate_com_a_grade_de_verdade()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(16);

        var previsto = PrevisaoDoTorneio.UltimoJogo(
            sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 2, 50, jogos);

        var grade = GradeDeJogos.Horarios(sexta18h, new TimeSpan(23, 50, 0), 2, 50, jogos,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        Assert.Equal(grade.Last(), previsto);
        Assert.Equal(jogos, grade.Count);
    }

    [Fact]
    public void Dias_ocupados_conta_o_dia_de_abertura()
    {
        var sexta = new DateTime(2026, 8, 21, 18, 0, 0);

        Assert.Equal(1, PrevisaoDoTorneio.DiasOcupados(sexta, sexta.AddHours(3)));
        Assert.Equal(3, PrevisaoDoTorneio.DiasOcupados(sexta, new DateTime(2026, 8, 23, 15, 0, 0)));
    }

    // ---- ParaATela: o que a tela de criação mostra --------------------------------------
    //
    // Estes existem por causa de uma regra DUPLICADA que foi removida em 08/08/2026: a tela
    // calculava tudo isto de novo, em JavaScript, e a cópia era invisível pra esta suíte.
    // Os testes abaixo comparam a resposta com os Services de origem em vez de com números
    // escritos à mão — assim eles quebram se alguém reimplementar a conta aqui dentro, que
    // é exatamente o erro que se está tentando impedir de voltar.

    private static PrevisaoDoTorneio.Tela Tela(string formato, int numero, DateTime? fim = null) =>
        PrevisaoDoTorneio.ParaATela(
            formato, numero, duracaoMinutos: 50, quadras: 3,
            abertura: new TimeSpan(18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            ultimoInicioDoDia: new TimeSpan(23, 0, 0),
            dataInicio: new DateTime(2026, 8, 21),
            dataFim: fim);

    [Fact]
    public void Oficial_usa_a_mesma_conta_de_grupos_e_mata_mata_do_sorteio()
    {
        var tela = Tela(FormatoDoTorneio.Padrao, 50);

        var (grupos, jogosDeGrupo) = PrevisaoDoTorneio.FaseDeGrupos(50);
        int esperado = jogosDeGrupo + PrevisaoDoTorneio.MataMata(grupos);

        Assert.True(tela.TemConta);
        Assert.True(tela.Fecha);
        Assert.Equal(esperado, tela.TotalDeJogos);
        Assert.Equal("50 duplas", tela.Resumo!.Inscritos);
        Assert.Contains($"{grupos} grupos", tela.Resumo.Meio);
    }

    [Fact]
    public void Americano_individual_usa_a_divisao_que_mais_mistura()
    {
        var tela = Tela(FormatoDoTorneio.Americano, 16);

        // A primeira da lista é a que mais mistura — a MESMA escolha que o sorteio faz.
        var divisao = DivisaoDoAmericano.Possiveis(16)[0];

        Assert.True(tela.Fecha);
        Assert.Equal(divisao.PartidasTotais, tela.TotalDeJogos);
        Assert.Equal("16 pessoas", tela.Resumo!.Inscritos);
        Assert.Contains($"{divisao.RodadasTotais} rodadas", tela.Resumo.Meio);
    }

    [Fact]
    public void Americano_de_duplas_usa_o_todos_contra_todos_comum()
    {
        var tela = Tela(FormatoDoTorneio.AmericanoDeDuplas, 8);

        Assert.True(tela.Fecha);
        Assert.Equal(RodadasAmericanoDeDuplas.Partidas(8), tela.TotalDeJogos);
        Assert.Contains($"{RodadasAmericanoDeDuplas.Rodadas(8)} rodadas", tela.Resumo!.Meio);
    }

    // ⚠️ O de duplas fecha em QUALQUER número, o individual não. Somar as duas réguas — ou
    // reaproveitar uma pra outra — faria a tela recusar um Americano de duplas que roda bem.
    [Fact]
    public void Numero_que_nao_fecha_no_individual_fecha_no_de_duplas()
    {
        var individual = Tela(FormatoDoTorneio.Americano, 6);
        var deDuplas = Tela(FormatoDoTorneio.AmericanoDeDuplas, 6);

        Assert.False(individual.Fecha);
        Assert.Null(individual.Resumo);
        // A saída, não só o problema: com 6 o caminho é 8 (ou 5).
        Assert.Contains("8", individual.PorQueNaoFecha!);

        Assert.True(deDuplas.Fecha);
        Assert.Equal(15, deDuplas.TotalDeJogos);
    }

    [Fact]
    public void Sem_data_ou_sem_numero_nao_ha_conta_a_mostrar()
    {
        Assert.False(Tela(FormatoDoTorneio.Padrao, 1).TemConta);       // abaixo do mínimo
        Assert.False(Tela(FormatoDoTorneio.Americano, 3).TemConta);    // o piso do Americano é 4

        var semData = PrevisaoDoTorneio.ParaATela(
            FormatoDoTorneio.Padrao, 16, 50, 3,
            new TimeSpan(18, 0, 0), new TimeSpan(8, 0, 0), new TimeSpan(23, 0, 0),
            dataInicio: null, dataFim: null);

        Assert.False(semData.TemConta);
        // ...mas o ritmo do dia NÃO depende da data: ele continua respondendo.
        Assert.True(semData.TemRitmo);
    }

    [Fact]
    public void O_prazo_se_mede_pelo_comeco_do_ultimo_jogo()
    {
        // Cabe folgado em 3 dias; não cabe se o prazo termina no mesmo dia da abertura.
        Assert.True(Tela(FormatoDoTorneio.Padrao, 50, fim: new DateTime(2026, 8, 23)).CabeNoPrazo);
        Assert.False(Tela(FormatoDoTorneio.Padrao, 50, fim: new DateTime(2026, 8, 21)).CabeNoPrazo);

        // Sem data de fim não há promessa a cobrar — nem verde nem vermelho.
        Assert.Null(Tela(FormatoDoTorneio.Padrao, 50).CabeNoPrazo);
    }

    [Fact]
    public void O_ritmo_do_dia_sai_da_grade_e_nao_de_uma_conta_propria()
    {
        var tela = Tela(FormatoDoTorneio.Padrao, 16);
        var abertura = new TimeSpan(18, 0, 0);
        var teto = new TimeSpan(23, 0, 0);

        Assert.Equal(GradeDeJogos.RodadasPorDia(abertura, teto, 50), tela.PrimeiroDia!.Rodadas);
        Assert.Equal("23:00", tela.PrimeiroDia.UltimoComeca);
        Assert.Equal("23:50", tela.PrimeiroDia.UltimoTermina);
        // Quantos jogos cabem no dia = rodadas × quadras.
        Assert.Equal(tela.PrimeiroDia.Rodadas * 3, tela.PrimeiroDia.Jogos);
        Assert.False(tela.DiasSeguintesIguais);
    }

    [Fact]
    public void Dia_sem_hora_pra_acabar_nao_inventa_ritmo()
    {
        var tela = PrevisaoDoTorneio.ParaATela(
            FormatoDoTorneio.Padrao, 16, 50, 3,
            abertura: new TimeSpan(8, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            ultimoInicioDoDia: new TimeSpan(8, 0, 0),   // teto == abertura: dia aberto
            dataInicio: new DateTime(2026, 8, 21), dataFim: null);

        Assert.True(tela.DiaSemHoraPraAcabar);
        Assert.Null(tela.PrimeiroDia);
    }

    // ⚠️ O campo da tela tem max="256", mas requisição montada à mão não passa pelo
    // formulário. Sem o teto, um número absurdo faz a divisão do Americano varrer milhões de
    // combinações e a grade materializar bilhões de horários — a requisição pendura.
    [Fact]
    public void Numero_absurdo_e_cortado_no_teto()
    {
        var tela = Tela(FormatoDoTorneio.Padrao, 5_000_000);

        Assert.True(tela.TemConta);
        Assert.Equal(Tela(FormatoDoTorneio.Padrao, PrevisaoDoTorneio.MaximoDeInscritos).TotalDeJogos,
            tela.TotalDeJogos);
    }
}
