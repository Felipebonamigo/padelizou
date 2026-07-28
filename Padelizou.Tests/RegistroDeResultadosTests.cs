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

    // ── Nosso custo ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Custo_e_pessoas_vezes_dias_vezes_o_que_pagamos_a_cada_um()
    {
        // 4 quadras num fim de semana de 3 dias: 2 pessoas × 3 dias × R$ 10 = R$ 60.
        var pessoas = RegistroDeResultados.PessoasSugeridas(4, 2);
        var dias = RegistroDeResultados.DiasDoTorneio(new DateTime(2026, 9, 4), new DateTime(2026, 9, 6));

        Assert.Equal(60m, RegistroDeResultados.CustoEstimado(pessoas, dias, 10m));
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
