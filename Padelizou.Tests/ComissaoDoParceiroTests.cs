using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A conta do Programa de Parceiros: 30% da primeira venda, 10% do que vier depois, por 12
// meses. Errar aqui não quebra tela nenhuma — devolve um número plausível e errado, que vai
// virar Pix pra uma pessoa de fora da empresa.
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
        // ⚠️ A armadilha central: 32 duplas são 32 cobranças e uma edição só. Contar por
        // pagamento pagaria 30% da 1ª dupla e 10% das outras 31 — 20% a menos do combinado.
        var pagos = Enumerable.Range(0, 32)
            .Select(i => Inscricao(7, torneioId: 1, comissao: 15m, Estreia.AddMinutes(i)))
            .ToList();

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        // 32 × R$ 15 = R$ 480 de comissão; 30% de tudo = R$ 144.
        Assert.Equal(144m, conta.Total);
        Assert.All(conta.Parcelas, p => Assert.Equal(30m, p.Percentual));
    }

    [Fact]
    public void A_segunda_edicao_cai_pra_10_por_cento()
    {
        var pagos = new[]
        {
            Inscricao(7, torneioId: 1, comissao: 480m, Estreia),
            Inscricao(7, torneioId: 2, comissao: 480m, Estreia.AddMonths(3)),
        };

        var conta = ComissaoDoParceiro.Calcular("Torneio", pagos);

        Assert.Equal(144m + 48m, conta.Total);
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

        Assert.Equal(30m, doDoze.Percentual);
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
        Assert.Equal(30m + 10m, conta.Total);
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
    public void Professor_ganha_os_50_reais_de_estreia_uma_vez_so()
    {
        // 10% de R$ 49,90 seriam R$ 4,99 pelo trabalho inteiro de trazer um cliente novo —
        // por isso o bônus de estreia é fixo, e não percentual.
        var pagos = new[]
        {
            Mensalidade(5, 49.90m, Estreia),
            Mensalidade(5, 49.90m, Estreia.AddMonths(1)),
        };

        var conta = ComissaoDoParceiro.Calcular("Professor", pagos);

        // (49,90 × 10% + 50) + (49,90 × 10%) = 54,99 + 4,99
        Assert.Equal(59.98m, conta.Total);
        Assert.Contains("estreia", conta.Parcelas[0].Motivo);
        Assert.Equal("recorrente", conta.Parcelas[1].Motivo);
    }

    [Fact]
    public void A_aula_do_professor_tambem_rende_10_por_cento()
    {
        // "10% de tudo que ele gerar" inclui a taxa das aulas, não só a mensalidade.
        var aula = new Pagamento
        {
            Tipo = "Aula",
            JogadorId = 999,
            RecebedorId = 5,
            Valor = 100m,
            Comissao = 10m,
            Status = "Confirmado",
            ConfirmadoEm = Estreia,
        };

        var conta = ComissaoDoParceiro.Calcular("Professor", new[] { aula });

        Assert.Equal(1m, conta.Total);
    }

    // ── Clube: a régua existe, o plano ainda não ──────────────────────────────────────────

    [Fact]
    public void Clube_hoje_so_rende_recorrente_porque_nao_existe_mensalidade_de_clube()
    {
        // A régua diz "1ª mensalidade cheia", mas não há plano de clube no código (o preço nem
        // foi fechado). Este teste é o marcador: quando a mensalidade nascer, o tipo dela entra
        // em TiposDeMensalidade e este teste passa a falhar de propósito.
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

        Assert.Equal(0.80m, conta.Total);
        Assert.Equal("recorrente", conta.Parcelas[0].Motivo);
        Assert.DoesNotContain("AssinaturaClube", ComissaoDoParceiro.TiposDeMensalidade);
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
        Assert.Equal(15m, ComissaoDoParceiro.Calcular("Torneio", new[] { meio }).Total);
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
        Assert.Equal(30m, ComissaoDoParceiro.PercentualDaPrimeiraVenda);
        Assert.Equal(10m, ComissaoDoParceiro.PercentualRecorrente);
        Assert.Equal(50m, ComissaoDoParceiro.BonusDeEstreiaDoProfessor);
        Assert.Equal(12, ComissaoDoParceiro.MesesDeComissao);
    }
}
