using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Pacote "nós registramos os resultados para você": o organizador contrata o Padelizou pra
// mandar gente lançar os jogos durante o torneio.
//
// A decisão que molda tudo é que isso é SOLICITAÇÃO, não compra — o botão diz "verificar
// disponibilidade" porque pode não haver ninguém livre naquela data e naquela cidade.
public class RegistroDeResultadosTests
{
    private static readonly DateTime Hoje = new(2026, 7, 28);

    // ── Quantas pessoas o torneio pede ────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 2, 1)]    // 1 quadra -> 1 pessoa
    [InlineData(2, 2, 1)]    // 2 quadras cabem numa pessoa só
    [InlineData(3, 2, 2)]    // 3 quadras já pedem 2 (arredonda pra cima)
    [InlineData(4, 2, 2)]
    [InlineData(8, 2, 4)]
    [InlineData(6, 3, 2)]    // com 3 quadras por pessoa a conta muda
    public void Uma_pessoa_por_par_de_quadras_arredondando_pra_cima(int quadras, int porPessoa, int esperado)
    {
        Assert.Equal(esperado, RegistroDeResultados.PessoasSugeridas(quadras, porPessoa));
    }

    [Fact]
    public void Numero_torto_de_quadra_nao_gera_equipe_de_zero_pessoa()
    {
        // Torneio salvo com 0 quadras existe (o campo já nasceu vazio em telas antigas).
        // Sugerir "0 pessoas" viraria um pedido que ninguém sabe atender.
        Assert.Equal(1, RegistroDeResultados.PessoasSugeridas(0, 2));
        Assert.Equal(1, RegistroDeResultados.PessoasSugeridas(-5, 2));
        Assert.Equal(1, RegistroDeResultados.PessoasSugeridas(1, 0));
    }

    // ── Quantos dias ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Torneio_de_um_dia_conta_um_dia()
    {
        var dia = new DateTime(2026, 9, 5);

        Assert.Equal(1, RegistroDeResultados.DiasDoTorneio(dia, null));
        Assert.Equal(1, RegistroDeResultados.DiasDoTorneio(dia, dia));
        Assert.Equal(1, RegistroDeResultados.DiasDoTorneio(null, null));
    }

    [Fact]
    public void Sexta_a_domingo_sao_TRES_dias_e_nao_dois()
    {
        // A conta ingênua (fim - início) daria 2 e a equipe faltaria no domingo.
        var dias = RegistroDeResultados.DiasDoTorneio(new DateTime(2026, 9, 4), new DateTime(2026, 9, 6));

        Assert.Equal(3, dias);
    }

    [Fact]
    public void Data_fim_antes_do_inicio_nao_gera_dia_negativo()
    {
        var dias = RegistroDeResultados.DiasDoTorneio(new DateTime(2026, 9, 6), new DateTime(2026, 9, 4));

        Assert.Equal(1, dias);
    }

    // ── Dinheiro: o preço é PERCENTUAL das inscrições, o custo continua por JOGO ─────

    [Fact]
    public void Jogos_do_torneio_e_a_soma_das_categorias()
    {
        // 4 categorias de 12 duplas: 4 × 19 = 76 jogos.
        Assert.Equal(76, RegistroDeResultados.JogosPrevistos(new[] { 12, 12, 12, 12 }));
    }

    [Fact]
    public void Categoria_sem_gente_nao_soma_jogo()
    {
        // Categoria criada e vazia é comum (o organizador abre 6 e enchem 4).
        Assert.Equal(RegistroDeResultados.JogosPrevistos(new[] { 12 }),
                     RegistroDeResultados.JogosPrevistos(new[] { 12, 0, 1 }));
    }

    [Fact]
    public void Custo_e_dez_reais_por_jogo_independente_dos_dias()
    {
        // O custo não mudou com a régua nova de preço: quem registra ganha por jogo, e um
        // Americano de UM dia pode ter mais jogos que um torneio de duplas de três.
        Assert.Equal(760m, RegistroDeResultados.CustoEstimado(76, 10m));
        Assert.Equal(0m, RegistroDeResultados.CustoEstimado(0, 10m));
    }

    [Fact]
    public void Preco_e_percentual_das_inscricoes_quando_o_torneio_e_grande()
    {
        // 120 pessoas × R$ 150 = R$ 18.000 de inscrições; 5% dá R$ 900, bem acima do mínimo.
        Assert.Equal(900m, RegistroDeResultados.PrecoSugerido(120, 150m, 5m, 500m));
    }

    [Fact]
    public void Torneio_pequeno_ou_gratuito_paga_o_minimo()
    {
        // 32 pessoas × R$ 100 × 5% = R$ 160, mas mandar alguém passar o dia custa o dia
        // inteiro. Sem o mínimo, torneio pequeno — e o gratuito, cujo percentual dá ZERO —
        // sairia no prejuízo.
        Assert.Equal(500m, RegistroDeResultados.PrecoSugerido(32, 100m, 5m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugerido(0, 150m, 5m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugerido(64, 0m, 5m, 500m));
    }

    [Fact]
    public void Da_pra_saber_onde_o_minimo_para_de_mandar()
    {
        // Quem responde precisa saber que abaixo disso todo torneio paga igual — senão acha
        // que a conta está errada quando dois pedidos diferentes dão o mesmo valor.
        var corte = RegistroDeResultados.InscricoesParaSairDoMinimo(5m, 500m);

        Assert.Equal(10_000m, corte);
        // No corte exato o percentual EMPATA com o mínimo; um degrau acima, passa.
        Assert.Equal(500m, RegistroDeResultados.PrecoSugerido(100, 100m, 5m, 500m));
        Assert.True(RegistroDeResultados.PrecoSugerido(101, 100m, 5m, 500m) > 500m);
    }

    [Fact]
    public void Percentual_e_custo_andam_soltos_e_a_conta_do_admin_precisa_mostrar_isso()
    {
        // O risco aceito da régua nova (20/08/2026): o preço segue as inscrições e o custo
        // segue os jogos. Inscrição barata com muitos jogos fica ABAIXO do custo — é o
        // painel do admin (que vê custo e sobra) quem segura, ajustando o valor na mão.
        var preco = RegistroDeResultados.PrecoSugerido(64, 50m, 5m, 500m);   // R$ 3.200 → mínimo
        var custo = RegistroDeResultados.CustoEstimado(60, 10m);             // 60 jogos

        Assert.Equal(500m, preco);
        Assert.True(custo > preco);
    }

    [Fact]
    public void Pedido_antigo_cotado_por_jogo_continua_valendo_o_que_leu()
    {
        // A cotação congela no pedido: quem pediu na regra do R$ 12 por jogo (antes de
        // 20/08/2026) não é recalculado pela régua nova.
        Assert.Equal(912m, RegistroDeResultados.PrecoSugeridoPorJogo(76, 12m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugeridoPorJogo(20, 12m, 500m));
    }

    // ── Quem pode pedir ───────────────────────────────────────────────────────────────

    [Fact]
    public void Com_antecedencia_e_servico_ligado_o_pedido_passa()
    {
        Assert.Null(RegistroDeResultados.ProblemaParaSolicitar(
            servicoHabilitado: true, jaTemSolicitacaoAberta: false,
            dataInicio: Hoje.AddDays(30), hoje: Hoje, antecedenciaMinimaDias: 7));
    }

    [Fact]
    public void Servico_desligado_recusa()
    {
        // Existe pra poder sumir com a oferta num fim de semana em que a equipe toda já está
        // ocupada — melhor não receber o pedido do que responder "não" pra todo mundo.
        var problema = RegistroDeResultados.ProblemaParaSolicitar(false, false, Hoje.AddDays(30), Hoje, 7);

        Assert.NotNull(problema);
        Assert.Contains("indisponível", problema!);
    }

    [Fact]
    public void Nao_aceita_dois_pedidos_abertos_pro_mesmo_torneio()
    {
        var problema = RegistroDeResultados.ProblemaParaSolicitar(true, jaTemSolicitacaoAberta: true,
            Hoje.AddDays(30), Hoje, 7);

        Assert.NotNull(problema);
        Assert.Contains("em aberto", problema!);
    }

    [Fact]
    public void Torneio_sem_data_nao_da_pra_pedir_equipe()
    {
        var problema = RegistroDeResultados.ProblemaParaSolicitar(true, false, dataInicio: null, Hoje, 7);

        Assert.NotNull(problema);
        Assert.Contains("data de início", problema!);
    }

    [Fact]
    public void Torneio_pra_depois_de_amanha_nao_da_tempo_de_montar_equipe()
    {
        // Prometer que dá e falhar na véspera é pior que dizer não agora: o organizador
        // ainda tem tempo de arrumar alguém por conta própria.
        var problema = RegistroDeResultados.ProblemaParaSolicitar(true, false, Hoje.AddDays(2), Hoje, 7);

        Assert.NotNull(problema);
        Assert.Contains("7 dias", problema!);
    }

    [Fact]
    public void No_limite_exato_da_antecedencia_ainda_passa()
    {
        Assert.Null(RegistroDeResultados.ProblemaParaSolicitar(true, false, Hoje.AddDays(7), Hoje, 7));
    }

    // ── Responder ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void So_pedido_em_aberto_pode_ser_respondido()
    {
        Assert.Null(RegistroDeResultados.ProblemaParaResponder(SolicitacaoRegistroResultados.Solicitada));
    }

    [Theory]
    [InlineData(SolicitacaoRegistroResultados.Confirmada)]
    [InlineData(SolicitacaoRegistroResultados.SemDisponibilidade)]
    [InlineData(SolicitacaoRegistroResultados.Cancelada)]
    [InlineData(SolicitacaoRegistroResultados.Concluida)]
    public void Responder_duas_vezes_deixaria_duas_versoes_do_combinado(string status)
    {
        // Sem esta trava, o organizador veria "confirmado por R$ 250" e depois "sem
        // disponibilidade" — sem saber qual vale.
        Assert.NotNull(RegistroDeResultados.ProblemaParaResponder(status));
    }

    // ── Cancelar ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SolicitacaoRegistroResultados.Solicitada)]
    [InlineData(SolicitacaoRegistroResultados.Confirmada)]
    public void Da_pra_cancelar_enquanto_o_servico_nao_aconteceu(string status)
    {
        Assert.Null(RegistroDeResultados.ProblemaParaCancelar(status));
    }

    [Theory]
    [InlineData(SolicitacaoRegistroResultados.Concluida)]
    [InlineData(SolicitacaoRegistroResultados.Cancelada)]
    [InlineData(SolicitacaoRegistroResultados.SemDisponibilidade)]
    public void Nao_da_pra_cancelar_o_que_ja_acabou(string status)
    {
        Assert.NotNull(RegistroDeResultados.ProblemaParaCancelar(status));
    }
}
