using Padelizou.Services;

namespace Padelizou.Tests;

// A ARITMÉTICA DA ABA DE PLANEJAMENTO DE QUADRAS (09/09/2026).
//
// 🗣️ Pedido do Felipe, pelo Er: "estão pensando e provavelmente irão alocar mais quadras, por
// que o ER só tem 2, então eles querem uma aba (…) que possam ver quantos horários eles teriam
// que locar de quadra, extra, para fechar os jogos da chave (…) uma previsão de quantos jogos
// precisaria colocar lá na sexta, e no sábado".
//
// A conta que já existia (Services/PrevisaoDoTorneio) responde "onde a grade TERMINA". A que
// faltava é a de CAPACIDADE: quantas vagas cada dia tem, quantas sobram, e — quando não cabe —
// quantas quadras ou quantos horários alugar. São perguntas diferentes: a primeira empurra o
// torneio pra frente até caber, a segunda trava o calendário e pergunta o que falta.
//
// ⚠️ O QUE ESTES TESTES GUARDAM ANTES DE TUDO É A NÃO-DIVERGÊNCIA. Quem diz o formato de um dia
// (quantas rodadas, a que horas começa a última) continua sendo o MOTOR do sorteio —
// GradeDeJogos.RodadasPorDia / UltimoInicioDoDia. Se o planejamento ganhasse a própria fórmula,
// ele prometeria uma grade que o sorteio não entrega: é exatamente o erro que a tela de criação
// cometeu em JavaScript e que PrevisaoDoTorneio existe pra ter matado.
public class PlanejamentoDeQuadrasTests
{
    // O torneio do Er na forma exata dele: 87 jogos, 2 quadras, sexta 11/09 abrindo 18h,
    // sábado e domingo abrindo 8h, último jogo começando 23h50, partida de 50 min.
    private static PlanejamentoDeQuadras.Plano DoEr(
        string? limitesPorDia = null, DateTime? ate = null, int quadras = 2, int jogos = 87)
        => PlanejamentoDeQuadras.Montar(
            inicio: new DateTime(2026, 9, 11, 18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(23, 50, 0),
            quadras: quadras,
            duracaoMinutos: 50,
            totalDeJogos: jogos,
            ate: ate,
            limitesPorDia: PlanejamentoDeQuadras.LerLimites(limitesPorDia));

    // A PERGUNTA LITERAL DO PEDIDO: quantos jogos na sexta, quantos no sábado.
    //
    // Sexta abre 18h e o último começa 23h50 → 8 rodadas × 2 quadras = 16 jogos. Sábado abre
    // 8h → 20 rodadas × 2 = 40. Sobram 31 pro domingo, que teria vaga pra 40.
    [Fact]
    public void Diz_quantos_jogos_cabem_em_cada_dia_do_fim_de_semana()
    {
        var plano = DoEr(ate: new DateTime(2026, 9, 13));

        Assert.Equal(3, plano.Dias.Count);

        Assert.Equal(new DateTime(2026, 9, 11), plano.Dias[0].Data);
        Assert.Equal(8, plano.Dias[0].Rodadas);
        Assert.Equal(16, plano.Dias[0].Vagas);
        Assert.Equal(16, plano.Dias[0].Jogos);

        // ⚠️ DIA CHEIO, E É POR ISSO QUE ELE ESTÁ AQUI: com 16 jogos em 2 quadras a última
        // rodada é a 8ª (23h50), não a 9ª. Num dia de nº ÍMPAR de jogos a divisão inteira e o
        // arredondamento pra cima dão o MESMO resultado — foi só com o dia cheio que a conta
        // errada apareceu, marcando jogo às 00h40 num dia que fecha 23h50.
        Assert.Equal(new TimeSpan(23, 50, 0), plano.Dias[0].UltimoJogoComeca);

        Assert.Equal(20, plano.Dias[1].Rodadas);
        Assert.Equal(40, plano.Dias[1].Vagas);
        Assert.Equal(40, plano.Dias[1].Jogos);

        // O domingo NÃO enche: é o dia que sobra folga, e é essa folga que o organizador
        // negocia quando decide a que horas quer ir embora.
        Assert.Equal(40, plano.Dias[2].Vagas);
        Assert.Equal(31, plano.Dias[2].Jogos);

        // ⚠️ DUAS HORAS DIFERENTES, e confundi-las é prometer quadra parada como quadra
        // cheia: `UltimoComeca` é o formato do DIA (até quando ele aceitaria jogo) e
        // `UltimoJogoComeca` é quando o último jogo DE VERDADE entra. Num dia que não enche,
        // elas são horas distintas — e é a segunda que responde "a que horas eu vou embora".
        Assert.Equal(new TimeSpan(23, 50, 0), plano.Dias[2].UltimoComeca);
        Assert.Equal(new TimeSpan(20, 30, 0), plano.Dias[2].UltimoJogoComeca);

        Assert.Equal(0, plano.Faltam);
        Assert.Equal(96, plano.Vagas);
        Assert.Equal(9, plano.Sobram);
    }

    // O CORAÇÃO DO PEDIDO: "quantos horários eles teriam que locar de quadra, extra".
    //
    // Ninguém joga até meia-noite de domingo. Com o domingo fechando às 14h, o dia cai de 20
    // rodadas pra 8 (o último jogo começa 13h50) e 15 jogos ficam sem lugar.
    //
    // As duas saídas são a MESMA conta vista de dois lados: 15 vagas faltando são 15 horários
    // de UMA quadra — ou uma quadra a mais no fim de semana inteiro, já que 36 rodadas × 3
    // quadras = 108 vagas pros 87 jogos.
    [Fact]
    public void Com_o_domingo_curto_diz_quantas_quadras_e_quantos_horarios_faltam()
    {
        var plano = DoEr(limitesPorDia: "2026-09-13=14:00", ate: new DateTime(2026, 9, 13));

        Assert.Equal(8, plano.Dias[2].Rodadas);
        Assert.Equal(16, plano.Dias[2].Vagas);
        Assert.Equal(new TimeSpan(13, 50, 0), plano.Dias[2].UltimoComeca);

        Assert.Equal(72, plano.Vagas);
        Assert.Equal(15, plano.Faltam);
        Assert.Equal(15, plano.HorariosExtras);
        Assert.Equal(3, plano.QuadrasNecessarias);

        // 15 horários de 50 min é o que o organizador leva pra negociar a quadra alugada.
        Assert.Equal(750, plano.MinutosExtras);
    }

    // ⚠️ A GUARDA CONTRA A SEGUNDA FONTE DA VERDADE. Sem limite por dia, o planejamento e o
    // motor do sorteio estão respondendo a MESMA pergunta — e têm que dar a mesma resposta,
    // até o minuto. No dia em que divergirem, a aba promete uma coisa e a grade entrega outra.
    [Fact]
    public void Sem_limite_por_dia_o_plano_termina_onde_o_sorteio_terminaria()
    {
        var plano = DoEr();

        var peloMotor = PrevisaoDoTorneio.UltimoJogo(
            new DateTime(2026, 9, 11, 18, 0, 0), new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0),
            quadras: 2, duracaoMinutos: 50, totalDeJogos: 87);

        Assert.Equal(peloMotor, plano.UltimoJogoComeca);
        Assert.Equal(new DateTime(2026, 9, 13, 20, 30, 0), plano.UltimoJogoComeca);
        Assert.Equal(new DateTime(2026, 9, 13, 21, 20, 0), plano.UltimoJogoTermina);
    }

    // A FOLGA QUE O ENCAIXE PEDE não é enfeite: sem vaga sobrando, o sorteio não tem pra onde
    // desviar de um impedimento e cai no último recurso (ver GradeDeJogos.Encaixar). O
    // planejamento tem que mostrar isso ANTES, e com o mesmo número que o motor usa — não com
    // um "3 rodadas" copiado à mão pra cá.
    [Fact]
    public void A_folga_cobrada_e_a_mesma_que_o_motor_do_sorteio_pede()
    {
        var apertado = DoEr(limitesPorDia: "2026-09-13=14:00", ate: new DateTime(2026, 9, 13));
        var folgado = DoEr(ate: new DateTime(2026, 9, 13));

        Assert.Equal(GradeDeJogos.MargemDeHorarios(2), apertado.Margem);
        Assert.False(apertado.FolgaSuficiente);   // 0 vagas sobrando, e o motor pede 6
        Assert.True(folgado.FolgaSuficiente);     // 9 sobrando
    }

    // Configuração torta (o dia "fecha" antes de abrir) não vira grade inventada — é a mesma
    // guarda que GradeDeJogos.RodadasPorDia já faz, e existe pra nunca entrar em laço.
    [Fact]
    public void Dia_que_fecha_antes_de_abrir_nao_vira_grade()
    {
        var plano = PlanejamentoDeQuadras.Montar(
            inicio: new DateTime(2026, 9, 11, 18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(7, 0, 0),
            quadras: 2, duracaoMinutos: 50, totalDeJogos: 87,
            ate: new DateTime(2026, 9, 13),
            limitesPorDia: PlanejamentoDeQuadras.LerLimites(null));

        Assert.True(plano.DiaSemHoraPraAcabar);
        Assert.Equal(0, plano.Vagas);

        // ⚠️ OS DIAS CONTINUAM NA LISTA, zerados. É o que sustenta a linha da tela onde ele
        // digita a hora de fechar: um dia que some da tabela é um dia que ele não consegue
        // mais reabrir.
        Assert.Equal(3, plano.Dias.Count);
        Assert.All(plano.Dias, d => Assert.Equal(0, d.Rodadas));
        Assert.All(plano.Dias, d => Assert.Null(d.UltimoJogoComeca));
    }

    // Sem prazo não existe "faltar": a grade só empurra o torneio pra frente até caber. Dizer
    // "faltam 15 horários" aí seria inventar uma cobrança que ninguém fez.
    [Fact]
    public void Sem_prazo_marcado_nada_falta_porque_a_grade_so_avanca()
    {
        var plano = DoEr(limitesPorDia: "2026-09-13=14:00");

        Assert.False(plano.PrazoDefinido);
        Assert.Equal(0, plano.Faltam);
        Assert.Equal(87, plano.Alocados);
        Assert.Equal(2, plano.QuadrasNecessarias);   // as que ele já tem: nada a alugar
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lixo")]
    [InlineData("2026-13-45=99:99")]
    public void Limite_por_dia_ilegivel_nao_derruba_a_tela(string? texto)
    {
        Assert.Empty(PlanejamentoDeQuadras.LerLimites(texto));
    }

    [Fact]
    public void Limite_por_dia_le_varios_dias_de_uma_vez()
    {
        var limites = PlanejamentoDeQuadras.LerLimites("2026-09-11=21:00;2026-09-13=14:00");

        Assert.Equal(2, limites.Count);
        Assert.Equal(new TimeSpan(21, 0, 0), limites[new DateTime(2026, 9, 11)]);
        Assert.Equal(new TimeSpan(14, 0, 0), limites[new DateTime(2026, 9, 13)]);
    }

    // ── A QUADRA ALUGADA POR ALGUMAS HORAS ────────────────────────────────────────────────
    //
    // 🗣️ Felipe: "adicionar aqui nessa tela uma ou mais quadras, para calcular corretamente
    // (…) quais horarios elas irão receber (de que horas até que horas, cada quadra)".
    //
    // ⚠️ A CAPACIDADE DEIXA DE SER `rodadas × quadras`. Uma quadra alugada das 8h às 14h de
    // sábado rende só as rodadas dessa janela — 8 (8h, 8h50 … 13h50) —, e nada nos outros
    // dias. Contá-la como quadra inteira prometeria 40 vagas onde existem 8: é a conta que
    // faria o organizador alugar de menos e descobrir na sexta à noite.
    //
    // ⚠️ E A REGRA DA JANELA NÃO É COPIADA PRA CÁ: quem responde "esta quadra está aberta às
    // 13h50?" é o mesmo SedesDoTorneio que o sorteio consulta. Meio aberta ([De, Ate)): o
    // jogo que COMEÇA às 14h já está fora.
    private static PlanejamentoDeQuadras.Plano DoErComQuadraAlugada(DateTime? ate = null)
        => PlanejamentoDeQuadras.Montar(
            inicio: new DateTime(2026, 9, 11, 18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(23, 50, 0),
            quadras: new[]
            {
                new Padelizou.Models.Quadra { Nome = "Quadra A" },
                new Padelizou.Models.Quadra { Nome = "Quadra B" },
                new Padelizou.Models.Quadra
                {
                    Nome = "Alugada",
                    DisponivelDe = new DateTime(2026, 9, 12, 8, 0, 0),
                    DisponivelAte = new DateTime(2026, 9, 12, 14, 0, 0),
                },
            },
            duracaoMinutos: 50,
            totalDeJogos: 87,
            ate: ate,
            limitesPorDia: PlanejamentoDeQuadras.LerLimites(null));

    [Fact]
    public void Quadra_alugada_so_rende_as_rodadas_da_janela_dela()
    {
        var plano = DoErComQuadraAlugada(ate: new DateTime(2026, 9, 13));

        Assert.Equal(3, plano.Quadras);

        // Sexta: a alugada está fechada (a janela é de sábado) → 8 rodadas × 2 = 16.
        Assert.Equal(16, plano.Dias[0].Vagas);

        // Sábado: 20 rodadas × 2 quadras de sempre + 8 rodadas da alugada = 48.
        Assert.Equal(48, plano.Dias[1].Vagas);

        // Domingo: de novo só as duas → 40.
        Assert.Equal(40, plano.Dias[2].Vagas);

        Assert.Equal(104, plano.Vagas);
        Assert.Equal(0, plano.Faltam);
    }

    // Os jogos ENTRAM na quadra alugada enquanto ela está aberta — e é isso que adianta o
    // fim do torneio: com 3 vagas por rodada de manhã, o sábado absorve mais, e o domingo
    // termina mais cedo do que sem ela.
    [Fact]
    public void Com_a_quadra_alugada_o_torneio_termina_mais_cedo()
    {
        var sem = DoEr();
        var com = DoErComQuadraAlugada();

        Assert.Equal(48, com.Dias[1].Jogos);
        Assert.Equal(87 - 16 - 48, com.Dias[2].Jogos);   // 23 no domingo
        Assert.True(com.UltimoJogoComeca < sem.UltimoJogoComeca);

        // 23 jogos em 2 quadras = 12 rodadas → a 12ª começa 8h + 11×50min = 17h10.
        Assert.Equal(new DateTime(2026, 9, 13, 17, 10, 0), com.UltimoJogoComeca);
    }

    // ⚠️ A GUARDA DE NÃO-DIVERGÊNCIA CONTINUA VALENDO com a lista de quadras: sem janela
    // nenhuma, três quadras de lista e "3" de número são a MESMA grade, até o minuto.
    [Fact]
    public void Lista_de_quadras_sem_janela_e_o_mesmo_que_o_numero()
    {
        var porNumero = DoEr(quadras: 3);
        var porLista = PlanejamentoDeQuadras.Montar(
            inicio: new DateTime(2026, 9, 11, 18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(23, 50, 0),
            quadras: new[]
            {
                new Padelizou.Models.Quadra { Nome = "A" },
                new Padelizou.Models.Quadra { Nome = "B" },
                new Padelizou.Models.Quadra { Nome = "C" },
            },
            duracaoMinutos: 50, totalDeJogos: 87, ate: null,
            limitesPorDia: PlanejamentoDeQuadras.LerLimites(null));

        Assert.Equal(porNumero.Vagas, porLista.Vagas);
        Assert.Equal(porNumero.UltimoJogoComeca, porLista.UltimoJogoComeca);
        Assert.Equal(porNumero.Dias.Select(d => d.Jogos), porLista.Dias.Select(d => d.Jogos));
    }
}
