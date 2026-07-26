using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Raquete Livre é rodízio: marca-se a hora de começar e muitas vezes não se sabe a de
// acabar. O fim era obrigatório, o que forçava o clube a inventar um horário.
public class RaqueteLivreTests
{
    private static readonly DateTime Agora = new(2026, 7, 26, 20, 0, 0);

    private static AvisoRaqueteLivre Sessao(DateTime inicio, DateTime? fim, string status = "Ativo") => new()
    {
        ClubeId = 1,
        CriadorId = 1,
        DataHoraInicio = inicio,
        DataHoraFim = fim,
        Status = status,
    };

    private static List<AvisoRaqueteLivre> EmCartaz(params AvisoRaqueteLivre[] sessoes)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.AvisosRaqueteLivre.AddRange(sessoes);
        ctx.SaveChanges();

        return SessaoRaqueteLivre.EmCartaz(ctx.AvisosRaqueteLivre, Agora).ToList();
    }

    [Fact]
    public void Sessao_sem_hora_de_fim_continua_na_lista_depois_de_comecar()
    {
        // Começou às 19h e não tem previsão de acabar: às 20h ainda está rolando.
        var aberta = Sessao(Agora.AddHours(-1), null);

        Assert.Single(EmCartaz(aberta));
    }

    [Fact]
    public void Sessao_sem_hora_de_fim_some_quando_a_duracao_presumida_passa()
    {
        var ontem = Sessao(Agora - SessaoRaqueteLivre.DuracaoPresumida.Add(TimeSpan.FromMinutes(1)), null);

        Assert.Empty(EmCartaz(ontem));
    }

    [Fact]
    public void Sessao_com_hora_de_fim_continua_valendo_a_regra_antiga()
    {
        var acabando = Sessao(Agora.AddHours(-2), Agora.AddHours(1));
        var acabada = Sessao(Agora.AddHours(-4), Agora.AddHours(-1));

        var lista = EmCartaz(acabando, acabada);

        Assert.Single(lista);
        Assert.Equal(acabando.DataHoraInicio, lista[0].DataHoraInicio);
    }

    [Fact]
    public void Sessao_futura_aparece_com_ou_sem_hora_de_fim()
    {
        var lista = EmCartaz(
            Sessao(Agora.AddDays(2), null),
            Sessao(Agora.AddDays(3), Agora.AddDays(3).AddHours(3)));

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public void Sessao_cancelada_nunca_aparece()
    {
        var cancelada = Sessao(Agora.AddDays(1), null, status: "Cancelado");

        Assert.Empty(EmCartaz(cancelada));
    }

    [Fact]
    public void Horario_escrito_muda_conforme_tenha_ou_nao_previsao_de_fim()
    {
        var comFim = Sessao(new DateTime(2026, 7, 26, 19, 0, 0), new DateTime(2026, 7, 26, 22, 0, 0));
        var semFim = Sessao(new DateTime(2026, 7, 26, 19, 0, 0), null);

        Assert.Equal("19:00 às 22:00", SessaoRaqueteLivre.DescreverHorario(comFim));
        Assert.Equal("a partir das 19:00", SessaoRaqueteLivre.DescreverHorario(semFim));

        Assert.False(SessaoRaqueteLivre.SemHoraPraAcabar(comFim));
        Assert.True(SessaoRaqueteLivre.SemHoraPraAcabar(semFim));
    }
}
