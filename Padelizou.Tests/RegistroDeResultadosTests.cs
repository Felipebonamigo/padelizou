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

    // ── Dinheiro: o preço é POR JOGO, e o custo também ───────────────────────────────

    [Fact]
    public void O_preco_do_servico_e_doze_reais_por_jogo()
    {
        // Decisão do Felipe (23/09/2026): o pacote volta a ser cobrado POR JOGO, no lugar dos
        // 10% das inscrições. Com a inscrição média em R$ 150, os 10% davam R$ 15 por pessoa
        // contra ~R$ 8 por jogo — e, mais que o número, o percentual não acompanha o CUSTO,
        // que sempre foi por jogo. R$ 12 é também o que o material de venda
        // (ORGANIZADOR-TORNEIO.html) nunca deixou de prometer.
        var config = new RegistroResultadosSettings();

        Assert.Equal(12m, config.PrecoPorJogo);
        Assert.Equal(10m, config.CustoPorJogo);   // a margem é de R$ 2 por jogo
        Assert.Equal(500m, config.ValorMinimo);
    }

    [Fact]
    public void Preco_e_o_numero_de_jogos_vezes_o_preco_do_jogo()
    {
        // 4 categorias de 16 duplas = 84 jogos × R$ 12.
        Assert.Equal(1_008m, RegistroDeResultados.PrecoSugeridoPorJogo(84, 12m, 500m));
    }

    [Fact]
    public void Torneio_pequeno_ou_gratuito_paga_o_minimo()
    {
        // Mandar alguém passar o dia custa o dia inteiro, tendo 10 ou 40 jogos. O mínimo é
        // também o amortecedor de distância — clube longe encarece e quem responde ajusta.
        Assert.Equal(500m, RegistroDeResultados.PrecoSugeridoPorJogo(20, 12m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugeridoPorJogo(0, 12m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugeridoPorJogo(-5, 12m, 500m));
    }

    [Fact]
    public void Da_pra_saber_a_partir_de_quantos_jogos_o_minimo_para_de_mandar()
    {
        // Quem responde precisa saber que abaixo disso todo torneio paga igual — senão acha
        // que a conta quebrou quando dois pedidos de tamanhos diferentes dão o mesmo valor.
        var corte = RegistroDeResultados.JogosParaSairDoMinimo(12m, 500m);

        Assert.Equal(42, corte);
        Assert.True(RegistroDeResultados.PrecoSugeridoPorJogo(corte, 12m, 500m) > 500m);
        Assert.Equal(500m, RegistroDeResultados.PrecoSugeridoPorJogo(corte - 1, 12m, 500m));
    }

    [Fact]
    public void Preco_e_custo_agora_andam_na_MESMA_unidade()
    {
        // O risco da régua percentual (20/08 a 23/09/2026) era este: o preço seguia as
        // INSCRIÇÕES e o custo seguia os JOGOS, então inscrição barata com muitos jogos
        // ficava abaixo do custo — o teste antigo daqui documentava isso como risco aceito.
        // Com os dois por jogo a sobra é R$ 2 por jogo em QUALQUER torneio.
        const int jogos = 390;                                        // 10 categorias de 24 duplas
        var preco = RegistroDeResultados.PrecoSugeridoPorJogo(jogos, 12m, 500m);
        var custo = RegistroDeResultados.CustoEstimado(jogos, 10m);

        Assert.Equal(4_680m, preco);
        Assert.Equal(3_900m, custo);
        Assert.Equal(2m * jogos, preco - custo);

        // O mesmo torneio pela régua velha, com inscrição barata (480 pessoas × R$ 75):
        // 10% dão R$ 3.600 contra R$ 3.900 de custo — R$ 300 de PREJUÍZO.
        Assert.True(RegistroDeResultados.PrecoSugerido(480, 75m, 10m, 500m) < custo);
    }

    [Fact]
    public void Pedido_cotado_em_percentual_continua_no_percentual()
    {
        // A cotação congela no pedido (PercentualCotado, regra de 20/08/2026): quem pediu na
        // régua dos 5% ou dos 10% não passa a pagar por jogo por causa desta mudança. É a
        // mesma promessa que protegeu quem tinha pedido por jogo quando o percentual entrou.
        Assert.Equal(900m, RegistroDeResultados.PrecoSugerido(120, 150m, 5m, 500m));
        Assert.Equal(1_800m, RegistroDeResultados.PrecoSugerido(120, 150m, 10m, 500m));
        Assert.Equal(500m, RegistroDeResultados.PrecoSugerido(32, 100m, 5m, 500m));
    }

    [Fact]
    public void Custo_e_dez_reais_por_jogo_independente_dos_dias()
    {
        // Quem registra ganha por jogo lançado, e um Americano de UM dia pode ter mais jogos
        // que um torneio de duplas de três.
        Assert.Equal(760m, RegistroDeResultados.CustoEstimado(76, 10m));
        Assert.Equal(0m, RegistroDeResultados.CustoEstimado(0, 10m));
    }

    // ── Quantos jogos o torneio tem — desde 23/09/2026 isto é PREÇO, não só custo ────

    [Fact]
    public void Jogos_do_torneio_e_a_soma_das_categorias()
    {
        // 4 categorias de 12 duplas: 4 × 19 = 76 jogos.
        Assert.Equal(76, RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Padrao, new[]
        {
            new CategoriaParaContar(false, 12), new CategoriaParaContar(false, 12),
            new CategoriaParaContar(false, 12), new CategoriaParaContar(false, 12),
        }));
    }

    [Fact]
    public void Categoria_sem_gente_nao_soma_jogo()
    {
        // Categoria criada e vazia é comum (o organizador abre 6 e enchem 4). E uma dupla
        // sozinha não joga contra ninguém.
        Assert.Equal(
            RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Padrao,
                new[] { new CategoriaParaContar(false, 12) }),
            RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Padrao, new[]
            {
                new CategoriaParaContar(false, 12), new CategoriaParaContar(false, 0),
                new CategoriaParaContar(false, 1),
            }));
    }

    [Fact]
    public void Chave_direta_e_mata_mata_puro_e_nao_fase_de_grupos()
    {
        // 24 duplas em chave direta são 23 jogos — cada um elimina uma até sobrar o campeão.
        // Contar pela régua dos grupos daria 39, e desde que o preço é por jogo isso não é
        // uma previsão inflada na tela: são R$ 192 cobrados a mais do organizador. A régua
        // já existia no preview das chaves (TorneiosController.Chaves.PrevisaoDaGrade).
        Assert.Equal(23, RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Padrao,
            new[] { new CategoriaParaContar(true, 24) }));
    }

    [Fact]
    public void Americano_individual_nao_conta_ZERO_jogo()
    {
        // A conta olhava só DUPLAS, e o Americano individual inscreve pessoa a pessoa: o
        // torneio inteiro valia zero jogo. Com preço por jogo, zero vira "paga o mínimo"
        // justamente no formato que tem MAIS jogo por pessoa.
        // 16 pessoas num grupo só: cada um com cada um = 60 partidas.
        Assert.Equal(60, RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Americano,
            new[] { new CategoriaParaContar(false, 16) }));
    }

    [Fact]
    public void Americano_de_duplas_e_todos_contra_todos()
    {
        // 8 duplas: 8 × 7 / 2 = 28 jogos. Pela régua do Padrão daria 15 (grupos + mata-mata),
        // e o formato não tem mata-mata nenhum.
        Assert.Equal(28, RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.AmericanoDeDuplas,
            new[] { new CategoriaParaContar(false, 8) }));
    }

    [Fact]
    public void Numero_que_nao_fecha_no_americano_nao_zera_a_conta()
    {
        // 6 pessoas não fecham "cada um com cada um" (DivisaoDoAmericano.Aceita = false) — o
        // organizador ainda vai ajustar o número. Responder ZERO aqui seria cotar o mínimo
        // num torneio que pode ter 40 jogos; a aproximação erra menos que o zero.
        Assert.True(RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Americano,
            new[] { new CategoriaParaContar(false, 6) }) > 0);

        // Abaixo de 4 pessoas não há Americano nenhum: nem uma quadra fecha.
        Assert.Equal(0, RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Americano,
            new[] { new CategoriaParaContar(false, 3) }));
    }

    [Fact]
    public void Torneio_antigo_sem_formato_conta_como_Padrao()
    {
        // Formato nulo é o torneio gravado antes de a coluna existir — ele é Padrão por
        // natureza (o Americano veio depois), e é a mesma leitura do FormatoDoTorneio.
        Assert.Equal(RegistroDeResultados.JogosPrevistos(FormatoDoTorneio.Padrao,
                         new[] { new CategoriaParaContar(false, 12) }),
                     RegistroDeResultados.JogosPrevistos(null,
                         new[] { new CategoriaParaContar(false, 12) }));
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
