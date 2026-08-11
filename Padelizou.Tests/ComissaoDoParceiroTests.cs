using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A conta do Programa de Parceiros: 20% da primeira venda, 10% do que vier depois, por 12
// meses. Errar aqui não quebra tela nenhuma — devolve um número plausível e errado, que vai
// virar Pix pra uma pessoa de fora da empresa.
//
// ⚠️ Os tamanhos daqui são os REAIS (45 a 110 duplas, informado pelo Felipe em 11/08/2026).
// A régua já tinha sido calibrada uma vez em cima de um torneio de 32 duplas, que quase não
// existe — e a conclusão sobre o percentual saiu errada por causa disso.
public class ComissaoDoParceiroTests
{
    private static readonly DateTime Estreia = new(2026, 3, 10, 10, 0, 0);

    // Inscrição de torneio: quem nos interessa é o RECEBEDOR (o organizador). O JogadorId é
    // o jogador que se inscreveu.
    private static Pagamento Inscricao(int organizadorId, int torneioId, decimal comissao,
        DateTime confirmadoEm, decimal valor = 1000m, int jogadorId = 999) =>
        new()
        {
            Tipo = "TorneioDupla",
            TorneioId = torneioId,
            JogadorId = jogadorId,
            RecebedorId = organizadorId,
            Valor = valor,
            Comissao = comissao,
            Status = "Confirmado",
            ConfirmadoEm = confirmadoEm,
        };

    // Taxa de aula: o professor é o RECEBEDOR. NÃO é da frente do parceiro desde 11/08/2026.
    private static Pagamento Aula(int professorId, decimal comissao, DateTime confirmadoEm) =>
        new()
        {
            Tipo = "Aula",
            JogadorId = 999,
            RecebedorId = professorId,
            Valor = 100m,
            Comissao = comissao,
            Status = "Confirmado",
            ConfirmadoEm = confirmadoEm,
        };

    // Mensalidade: RecebedorId NULO (é 100% nosso) e quem paga É o cliente.
    private static Pagamento Mensalidade(int professorId, decimal valor, DateTime confirmadoEm) =>
        new()
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = professorId,
            RecebedorId = null,
            Valor = valor,
            Comissao = valor,
            Status = "Confirmado",
            ConfirmadoEm = confirmadoEm,
        };

    // ── De quem é o cliente ───────────────────────────────────────────────────────────────

    [Fact]
    public void Na_inscricao_o_cliente_e_o_recebedor_e_nao_quem_pagou()
    {
        // Atribuir pelo pagador daria a comissão do parceiro por causa de um cliente do
        // cliente: cada jogador inscrito viraria uma "indicação".
        var p = Inscricao(organizadorId: 7, torneioId: 1, comissao: 100m, Estreia, jogadorId: 42);

        Assert.Equal(7, ComissaoDoParceiro.ClienteDoPagamento(p));
    }

    [Fact]
    public void Na_mensalidade_o_cliente_e_quem_pagou_porque_nao_ha_recebedor()
    {
        // Mensalidade e taxa do externo são 100% nossas: RecebedorId é nulo de propósito.
        // Buscar só por recebedor perderia essas duas frentes inteiras, em silêncio.
        var p = Mensalidade(professorId: 5, valor: 49.90m, Estreia);

        Assert.Null(p.RecebedorId);
        Assert.Equal(5, ComissaoDoParceiro.ClienteDoPagamento(p));
    }

    // ── Torneio: 30% na estreia, 10% depois ───────────────────────────────────────────────

    [Fact]
    public void Um_torneio_e_UMA_edicao_mesmo_com_dezenas_de_inscricoes()
    {
        // ⚠️ A armadilha central: 45 duplas são 45 cobranças e uma edição só. Contar por
        // pagamento pagaria 20% da 1ª dupla e 10% das outras 44 — quase metade a menos do
        // combinado.
        var pagos = Enumerable.Range(0, 45)
            .Select(i => Inscricao(7, torneioId: 1, comissao: 15m, Estreia.AddMinutes(i)))
            .ToList();

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        // 45 × R$ 15 = R$ 675 de comissão; 20% de tudo = R$ 135.
        Assert.Equal(135m, conta.Total);
        Assert.All(conta.Parcelas, p => Assert.Equal(20m, p.Percentual));
    }

    [Fact]
    public void A_segunda_edicao_cai_pra_10_por_cento()
    {
        // Duas edições de 60 duplas: comissão de R$ 900 cada.
        var pagos = new[]
        {
            Inscricao(7, torneioId: 1, comissao: 900m, Estreia),
            Inscricao(7, torneioId: 2, comissao: 900m, Estreia.AddMonths(3)),
        };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        Assert.Equal(180m + 90m, conta.Total);
        Assert.Equal("1ª edição", conta.Parcelas[0].Motivo);
        Assert.Equal("edição seguinte", conta.Parcelas[1].Motivo);
    }

    [Fact]
    public void A_estreia_e_o_torneio_do_primeiro_pagamento_e_nao_o_menor_id()
    {
        // O torneio 9 foi criado antes, mas quem pagou primeiro foi o 12: a estreia é a
        // primeira VENDA, não o cadastro mais antigo.
        var pagos = new[]
        {
            Inscricao(7, torneioId: 9, comissao: 100m, Estreia.AddMonths(2)),
            Inscricao(7, torneioId: 12, comissao: 100m, Estreia),
        };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);
        var doDoze = conta.Parcelas.Single(p => p.Pagamento.TorneioId == 12);

        Assert.Equal(20m, doDoze.Percentual);
    }

    // ── A janela de 12 meses ──────────────────────────────────────────────────────────────

    [Fact]
    public void Passados_12_meses_o_cliente_para_de_render()
    {
        // A decisão de 11/08/2026: nada e vitalicio. Sem este corte, a margem de um cliente
        // que a plataforma sustenta no ano 3 continuaria saindo pra quem vendeu uma vez.
        var pagos = new[]
        {
            Inscricao(7, torneioId: 1, comissao: 100m, Estreia),
            Inscricao(7, torneioId: 2, comissao: 100m, Estreia.AddMonths(11)),
            Inscricao(7, torneioId: 3, comissao: 100m, Estreia.AddMonths(13)),   // fora
        };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        Assert.Equal(2, conta.Parcelas.Count);
        Assert.Equal(20m + 10m, conta.Total);
        Assert.Equal(Estreia.AddMonths(12), conta.FimDaJanela);
    }

    [Fact]
    public void A_janela_conta_do_primeiro_PAGAMENTO_e_nao_do_fechamento_do_lead()
    {
        // Quem marca o lead como ganho é o Felipe, quando lembra. Amarrar o relógio do
        // dinheiro a um clique manual faria o parceiro ganhar ou perder meses pela agenda de
        // outra pessoa.
        var pagos = new[] { Inscricao(7, torneioId: 1, comissao: 100m, Estreia) };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        Assert.Equal(Estreia, conta.PrimeiroPagamento);
    }

    [Fact]
    public void Cliente_que_nunca_pagou_nao_rende_nem_abre_janela()
    {
        var conta = ComissaoDoParceiro.Calcular("Torneio", Array.Empty<Pagamento>());

        Assert.Equal(0m, conta.Total);
        Assert.Null(conta.PrimeiroPagamento);
        Assert.False(conta.AindaRende(Estreia));
        Assert.Equal(0, conta.DiasQueRestam(Estreia));
    }

    // ── Professor ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Professor_paga_20_por_cento_na_primeira_mensalidade_e_10_nas_seguintes()
    {
        var pagos = new[]
        {
            Mensalidade(5, 49.90m, Estreia),
            Mensalidade(5, 49.90m, Estreia.AddMonths(1)),
        };

        var conta = ComissaoDoParceiro.Calcular("Professor", pagos);

        // (49,90 × 20%) + (49,90 × 10%) = 9,98 + 4,99
        Assert.Equal(14.97m, conta.Total);
        Assert.Equal(20m, conta.Parcelas[0].Percentual);
        Assert.Equal("1ª mensalidade", conta.Parcelas[0].Motivo);
        Assert.Equal("mensalidade seguinte", conta.Parcelas[1].Motivo);
    }

    [Fact]
    public void O_plano_ANUAL_paga_uma_entrada_bem_maior()
    {
        // R$ 499,90 de uma vez: a entrada acompanha o tamanho da venda, sem regra especial.
        var conta = ComissaoDoParceiro.Calcular("Professor", new[] { Mensalidade(5, 499.90m, Estreia) });

        Assert.Equal(99.98m, conta.Total);
    }

    [Fact]
    public void Aula_paga_ANTES_de_assinar_nao_consome_a_entrada()
    {
        // O professor pode estar no Avulso e pagar taxa de aula antes de assinar. A aula fica
        // fora da conta, e a ENTRADA continua sendo a mensalidade — se a aula entrasse como
        // "primeira cobrança", a entrada valeria R$ 2 em vez de R$ 9,98.
        var aula = Aula(professorId: 5, comissao: 10m, Estreia);
        var assinatura = Mensalidade(5, 49.90m, Estreia.AddMonths(1));

        var conta = ComissaoDoParceiro.Calcular("Professor", new[] { aula, assinatura });

        var unica = Assert.Single(conta.Parcelas);
        Assert.Equal("1ª mensalidade", unica.Motivo);
        Assert.Equal(9.98m, unica.Valor);

        // ⚠️ E a JANELA também começa na mensalidade, não na aula ignorada: contar dali
        // encurtaria os 12 meses do parceiro por causa de um pagamento que não é dele.
        Assert.Equal(Estreia.AddMonths(1), conta.PrimeiroPagamento);
    }

    [Fact]
    public void Professor_que_nunca_assina_nao_rende_NADA()
    {
        // Consequência assumida: quem fica no Avulso pra sempre não paga mensalidade, e a taxa
        // das aulas dele não é do parceiro. Zero, e a tela mostra zero.
        var conta = ComissaoDoParceiro.Calcular("Professor", new[] { Aula(5, 10m, Estreia) });

        Assert.Equal(0m, conta.Total);
        Assert.Null(conta.PrimeiroPagamento);
    }

    [Fact]
    public void A_taxa_das_AULAS_nao_e_do_parceiro()
    {
        // ⚠️ Decisão do Felipe: no professor a frente é SÓ A MENSALIDADE. O que o parceiro
        // vendeu foi a assinatura, não o movimento de alunos do professor.
        var aula = Aula(professorId: 5, comissao: 10m, Estreia);

        Assert.False(ComissaoDoParceiro.EhDaFrente("Professor", aula));
        Assert.Equal(0m, ComissaoDoParceiro.Calcular("Professor", new[] { aula }).Total);
    }

    // ── Clube: a régua existe, o plano ainda não ──────────────────────────────────────────

    [Fact]
    public void Clube_hoje_rende_ZERO_e_isso_e_a_verdade()
    {
        // ⚠️ A frente do clube é só a mensalidade — e **a mensalidade de clube não existe no
        // código** (o preço nem foi fechado). A reserva de quadra não é do parceiro. Logo,
        // lead de clube fechado hoje rende ZERO: não há o que comissionar.
        //
        // Este teste é o marcador. Quando o plano de clube nascer, o tipo dele entra em
        // TiposDoClube e este teste falha de propósito.
        var reserva = new Pagamento
        {
            Tipo = "Jogo",
            JogadorId = 999,
            RecebedorId = 3,
            Valor = 80m,
            Comissao = 8m,
            Status = "Confirmado",
            ConfirmadoEm = Estreia,
        };

        var conta = ComissaoDoParceiro.Calcular("Clube", new[] { reserva });

        Assert.Equal(0m, conta.Total);
        Assert.Empty(ComissaoDoParceiro.TiposDoClube);
    }

    // ── "Apenas do que ele vendeu" ────────────────────────────────────────────────────────

    [Fact]
    public void Quem_vendeu_TORNEIO_nao_ganha_da_mensalidade_de_professor_do_mesmo_cliente()
    {
        // ⚠️ A regra do Felipe: a comissão é da FRENTE que o parceiro vendeu. O organizador
        // que também é professor pode ter sido trazido pra aula por outra pessoa — ou por
        // ninguém. Sem este filtro, uma venda de torneio rendia sobre tudo que aquela pessoa
        // gerasse no sistema inteiro.
        var pagos = new Pagamento[]
        {
            Inscricao(7, torneioId: 1, comissao: 900m, Estreia),
            Mensalidade(7, 49.90m, Estreia.AddMonths(1)),
        };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        Assert.Equal(180m, conta.Total);                 // só os 20% da 1ª edição
        Assert.Single(conta.Parcelas);
    }

    [Fact]
    public void Quem_vendeu_PROFESSOR_nao_ganha_das_inscricoes_de_torneio_do_mesmo_cliente()
    {
        var pagos = new Pagamento[]
        {
            Mensalidade(7, 49.90m, Estreia),
            Inscricao(7, torneioId: 1, comissao: 900m, Estreia.AddMonths(1)),
        };

        var conta = ComissaoDoParceiro.Calcular("Professor", pagos);

        Assert.Equal(9.98m, conta.Total);                // só os 20% da 1ª mensalidade
        Assert.Single(conta.Parcelas);
    }

    [Fact]
    public void A_taxa_do_torneio_externo_conta_como_torneio()
    {
        // Ela é 100% nossa, o cliente é quem paga, e carrega o TorneioId — é por isso que a
        // frente é reconhecida pelo id, e não por lista de tipos.
        var taxa = new Pagamento
        {
            Tipo = "TaxaTorneio", TorneioId = 4, JogadorId = 7, RecebedorId = null,
            Valor = 450m, Comissao = 450m, Status = "Confirmado", ConfirmadoEm = Estreia,
        };

        Assert.True(ComissaoDoParceiro.EhDaFrente("Torneio", taxa));
        Assert.Equal(90m, ComissaoDoParceiro.Calcular("Torneio", new[] { taxa }).Total);
    }

    [Fact]
    public void Reserva_de_quadra_nao_e_de_parceiro_nenhum()
    {
        // Nem do clube, nem do professor, nem do torneio: o parceiro vendeu a assinatura, e
        // não o movimento de quem usa a quadra.
        var reserva = new Pagamento { Tipo = "Jogo", JogadorId = 999, RecebedorId = 3, Valor = 80m, Comissao = 8m };

        Assert.False(ComissaoDoParceiro.EhDaFrente("Clube", reserva));
        Assert.False(ComissaoDoParceiro.EhDaFrente("Professor", reserva));
        Assert.False(ComissaoDoParceiro.EhDaFrente("Torneio", reserva));
    }

    // ── Estorno ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Estorno_total_zera_a_comissao_daquele_pagamento()
    {
        var estornado = Inscricao(7, torneioId: 1, comissao: 100m, Estreia, valor: 1000m);
        estornado.Status = "Estornado";

        Assert.Equal(0m, ComissaoDoParceiro.ComissaoLiquida(estornado));
        Assert.Equal(0m, ComissaoDoParceiro.Calcular("Torneio", new[] { estornado }).Total);
    }

    [Fact]
    public void Estorno_parcial_reduz_na_proporcao_do_que_voltou()
    {
        // Metade voltou: metade da comissão deixa de existir, e a parte do parceiro cai junto.
        // Sem isto, o parceiro seria pago por uma venda que foi desfeita pela metade.
        var meio = Inscricao(7, torneioId: 1, comissao: 100m, Estreia, valor: 1000m);
        meio.ValorEstornado = 500m;

        Assert.Equal(50m, ComissaoDoParceiro.ComissaoLiquida(meio));
        Assert.Equal(10m, ComissaoDoParceiro.Calcular("Torneio", new[] { meio }).Total);
    }

    [Fact]
    public void Pagamento_pendente_nao_conta_como_venda()
    {
        var pendente = Inscricao(7, torneioId: 1, comissao: 100m, Estreia);
        pendente.Status = "Pendente";

        Assert.Equal(0m, ComissaoDoParceiro.Calcular("Torneio", new[] { pendente }).Total);
    }

    // ── O mês corrente ────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_mes_corrente_e_separado_porque_ainda_pode_crescer()
    {
        var agora = new DateTime(2026, 3, 20);
        var conta = ComissaoDoParceiro.Calcular("Torneio", new[]
        {
            Inscricao(7, torneioId: 1, comissao: 100m, new DateTime(2026, 2, 5)),
            Inscricao(7, torneioId: 2, comissao: 100m, new DateTime(2026, 3, 15)),
        });

        var doMes = conta.Parcelas.Where(p => ComissaoDoParceiro.EhDoMesCorrente(p, agora)).ToList();

        Assert.Single(doMes);
        Assert.Equal(2, doMes[0].Pagamento.TorneioId);
    }

    // ── As constantes são o contrato ──────────────────────────────────────────────────────

    [Fact]
    public void A_regua_e_a_que_esta_no_contrato()
    {
        // Se alguém mexer nestes números sem mexer no PARCEIROS.md, o sistema passa a pagar
        // uma coisa e o contrato a prometer outra.
        Assert.Equal(20m, ComissaoDoParceiro.PercentualDaPrimeiraVenda);
        Assert.Equal(10m, ComissaoDoParceiro.PercentualRecorrente);
        Assert.Equal(12, ComissaoDoParceiro.MesesDeComissao);
    }
}
