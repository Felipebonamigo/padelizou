using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A regra que sustenta o Programa de Parceiros: de quem é o contato. Ela vale dinheiro (30% da
// primeira venda + 10% por 12 meses) e é aplicada entre amigos do Felipe — se ela falhar em
// silêncio, o prejuízo é a amizade, não o build.
public class LeadsComerciaisTests
{
    private static LeadComercial Lead(int parceiroId, DateTime registradoEm,
        string status = LeadsComerciais.Aberto, string telefone = "51984567890") =>
        new()
        {
            ParceiroId = parceiroId,
            Contato = "Grupo do teste",
            Telefone = telefone,
            Tipo = "Torneio",
            RegistradoEm = registradoEm,
            Status = status,
        };

    private static string Nome(int id) => $"Parceiro {id}";

    [Fact]
    public void Telefone_livre_deixa_registrar()
    {
        var problema = LeadsComerciais.ProblemaParaRegistrar(
            "Loberos", "(51) 98456-7890", "Torneio",
            Array.Empty<LeadComercial>(), DateTime.Now, Nome);

        Assert.Null(problema);
    }

    [Fact]
    public void Mascara_diferente_no_mesmo_numero_nao_burla_a_duplicata()
    {
        // O ponto inteiro da tela. Sem normalizar, "(51) 98456-7890" e "51984567890" seriam
        // dois contatos diferentes e dois parceiros teriam direito à mesma comissão.
        var agora = new DateTime(2026, 8, 11);
        var jaRegistrado = new[] { Lead(parceiroId: 7, agora.AddDays(-10)) };

        var problema = LeadsComerciais.ProblemaParaRegistrar(
            "Los Loberos", "(51) 98456-7890", "Torneio", jaRegistrado, agora, Nome);

        Assert.NotNull(problema);
        Assert.Contains("Parceiro 7", problema);
    }

    [Fact]
    public void A_recusa_diz_quem_segura_e_ate_quando()
    {
        // "Já existe" obrigaria o Felipe a abrir o banco pra responder a única pergunta que o
        // parceiro faz em seguida no WhatsApp.
        var agora = new DateTime(2026, 8, 11);
        var jaRegistrado = new[] { Lead(parceiroId: 3, new DateTime(2026, 8, 1)) };

        var problema = LeadsComerciais.ProblemaParaRegistrar(
            "Chakra", "51984567890", "Clube", jaRegistrado, agora, Nome);

        Assert.NotNull(problema);
        Assert.Contains("Parceiro 3", problema);
        Assert.Contains("01/08/2026", problema);
        Assert.Contains("30/10/2026", problema);   // 1º de agosto + 90 dias
    }

    [Fact]
    public void Passados_os_90_dias_o_contato_volta_a_ficar_livre()
    {
        var agora = new DateTime(2026, 8, 11);
        var velho = new[] { Lead(parceiroId: 7, agora.AddDays(-91)) };

        Assert.True(LeadsComerciais.EstaVencido(velho[0], agora));
        Assert.Equal(LeadsComerciais.Vencido, LeadsComerciais.Situacao(velho[0], agora));
        Assert.Null(LeadsComerciais.ProblemaParaRegistrar(
            "Loberos", "51984567890", "Torneio", velho, agora, Nome));
    }

    [Fact]
    public void No_ultimo_dia_do_prazo_o_contato_ainda_e_de_quem_registrou()
    {
        // A borda importa: errar pra menos aqui entrega a comissão de alguém pra outra pessoa
        // um dia antes da hora.
        var agora = new DateTime(2026, 8, 11);
        var noLimite = new[] { Lead(parceiroId: 7, agora.AddDays(-90)) };

        Assert.False(LeadsComerciais.EstaVencido(noLimite[0], agora));
        Assert.NotNull(LeadsComerciais.ProblemaParaRegistrar(
            "Loberos", "51984567890", "Torneio", noLimite, agora, Nome));
    }

    [Fact]
    public void Cliente_ja_fechado_nao_volta_a_ser_registravel_nunca()
    {
        // O prazo de 12 meses do contrato limita quanto o cliente RENDE, não de quem ele é.
        // Sem esta trava, um cliente fechado em março voltaria a ser registrável em junho e
        // outro parceiro ganharia a comissão de uma venda que não fez.
        var agora = new DateTime(2027, 8, 11);
        var ganho = Lead(parceiroId: 4, new DateTime(2026, 1, 10), LeadsComerciais.Ganho);
        ganho.FechadoEm = new DateTime(2026, 2, 1);

        var problema = LeadsComerciais.ProblemaParaRegistrar(
            "Er Padel", "51984567890", "Clube", new[] { ganho }, agora, Nome);

        Assert.NotNull(problema);
        Assert.Contains("já é cliente", problema);
        Assert.Contains("Parceiro 4", problema);
    }

    [Fact]
    public void Perdido_libera_o_telefone_na_hora()
    {
        var agora = new DateTime(2026, 8, 11);
        var perdido = new[] { Lead(parceiroId: 4, agora.AddDays(-5), LeadsComerciais.Perdido) };

        Assert.Null(LeadsComerciais.QuemSegura(perdido, agora));
        Assert.Null(LeadsComerciais.ProblemaParaRegistrar(
            "Golden Point", "51984567890", "Clube", perdido, agora, Nome));
    }

    [Fact]
    public void Entre_dois_abertos_quem_registrou_primeiro_leva()
    {
        var agora = new DateTime(2026, 8, 11);
        var leads = new[]
        {
            Lead(parceiroId: 9, agora.AddDays(-3)),
            Lead(parceiroId: 2, agora.AddDays(-30)),
        };

        Assert.Equal(2, LeadsComerciais.QuemSegura(leads, agora)!.ParceiroId);
    }

    [Fact]
    public void Ganho_vence_aberto_mesmo_tendo_sido_registrado_depois()
    {
        // Um contato que já virou cliente tem dono, ponto — mesmo que outro parceiro tenha um
        // registro aberto no mesmo telefone.
        var agora = new DateTime(2026, 8, 11);
        var ganho = Lead(parceiroId: 5, agora.AddDays(-2), LeadsComerciais.Ganho);
        var leads = new[] { Lead(parceiroId: 1, agora.AddDays(-60)), ganho };

        Assert.Equal(5, LeadsComerciais.QuemSegura(leads, agora)!.ParceiroId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("98456789")]        // sem DDD
    [InlineData("519845678901")]    // um dígito a mais
    public void Telefone_que_nao_serve_de_chave_e_recusado(string telefone)
    {
        var problema = LeadsComerciais.ProblemaParaRegistrar(
            "Alguém", telefone, "Torneio", Array.Empty<LeadComercial>(), DateTime.Now, Nome);

        Assert.NotNull(problema);
        Assert.Contains("DDD", problema);
    }

    [Fact]
    public void Contato_sem_nome_ou_com_tipo_invalido_e_recusado()
    {
        Assert.NotNull(LeadsComerciais.ProblemaParaRegistrar(
            "   ", "51984567890", "Torneio", Array.Empty<LeadComercial>(), DateTime.Now, Nome));

        Assert.NotNull(LeadsComerciais.ProblemaParaRegistrar(
            "Alguém", "51984567890", "Padaria", Array.Empty<LeadComercial>(), DateTime.Now, Nome));
    }

    [Fact]
    public void Os_tres_tipos_do_contrato_sao_aceitos()
    {
        // Se um tipo sumir daqui, a tela para de aceitar um produto inteiro em silêncio.
        Assert.Equal(new[] { "Torneio", "Professor", "Clube" }, LeadsComerciais.Tipos);

        foreach (var tipo in LeadsComerciais.Tipos)
        {
            Assert.Null(LeadsComerciais.ProblemaParaRegistrar(
                "Alguém", "51984567890", tipo, Array.Empty<LeadComercial>(), DateTime.Now, Nome));
        }
    }
}
