using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Política de cancelamento, presença e avaliação do professor.
public class ProfessorTests
{
    private static Jogador NovoProfessor(int horasMinimas = 24, bool cobraFalta = false) => new()
    {
        Nome = "Prof",
        Cpf = "1",
        IsProfessor = true,
        HorasMinimasCancelamento = horasMinimas,
        CobraFaltaSemAviso = cobraFalta,
    };

    // ---------- Prazo ----------

    [Fact]
    public void Prazo_zero_libera_cancelamento_a_qualquer_hora()
    {
        var prof = NovoProfessor(horasMinimas: 0);
        var agora = new DateTime(2026, 7, 26, 10, 0, 0);

        // Faltando 5 minutos ainda está no prazo quando o professor não exige aviso.
        Assert.True(PoliticaAula.DentroDoPrazo(prof, agora.AddMinutes(5), agora));
    }

    [Fact]
    public void Prazo_de_24h_separa_dentro_e_fora()
    {
        var prof = NovoProfessor(horasMinimas: 24);
        var agora = new DateTime(2026, 7, 26, 10, 0, 0);

        Assert.True(PoliticaAula.DentroDoPrazo(prof, agora.AddHours(25), agora));
        Assert.True(PoliticaAula.DentroDoPrazo(prof, agora.AddHours(24), agora));  // no limite conta
        Assert.False(PoliticaAula.DentroDoPrazo(prof, agora.AddHours(23), agora));
    }

    // ---------- Cobrança ----------

    [Fact]
    public void So_cobra_quando_o_aluno_avisa_fora_do_prazo_e_o_professor_cobra()
    {
        var agora = new DateTime(2026, 7, 26, 10, 0, 0);
        var aulaLonge = new Aula { DataHora = agora.AddHours(48) };
        var aulaPerto = new Aula { DataHora = agora.AddHours(2) };

        var cobra = NovoProfessor(24, cobraFalta: true);
        var naoCobra = NovoProfessor(24, cobraFalta: false);

        Assert.True(PoliticaAula.DeveCobrar(cobra, aulaPerto, "Aluno", agora));   // fora do prazo
        Assert.False(PoliticaAula.DeveCobrar(cobra, aulaLonge, "Aluno", agora));  // dentro do prazo
        Assert.False(PoliticaAula.DeveCobrar(naoCobra, aulaPerto, "Aluno", agora)); // não cobra falta
    }

    [Fact]
    public void Professor_que_cancela_nunca_gera_cobranca()
    {
        var agora = new DateTime(2026, 7, 26, 10, 0, 0);
        var prof = NovoProfessor(24, cobraFalta: true);
        var aulaDaquiAPouco = new Aula { DataHora = agora.AddHours(1) };

        // A regra existe pra proteger a agenda do professor, não pra punir o aluno
        // quando quem desmarcou foi ele.
        Assert.False(PoliticaAula.DeveCobrar(prof, aulaDaquiAPouco, "Professor", agora));
    }

    // ---------- Texto pro aluno ----------

    [Fact]
    public void Texto_livre_do_professor_tem_prioridade_sobre_o_automatico()
    {
        var prof = NovoProfessor(24, cobraFalta: true);
        prof.PoliticaCancelamentoTexto = "  Desmarcou, remarcou. Sem estresse.  ";

        Assert.Equal("Desmarcou, remarcou. Sem estresse.", PoliticaAula.DescreverPolitica(prof));
    }

    [Fact]
    public void Sem_texto_livre_a_frase_muda_conforme_cobra_ou_nao()
    {
        var cobra = PoliticaAula.DescreverPolitica(NovoProfessor(24, cobraFalta: true));
        var naoCobra = PoliticaAula.DescreverPolitica(NovoProfessor(24, cobraFalta: false));
        var semPrazo = PoliticaAula.DescreverPolitica(NovoProfessor(0));

        Assert.Contains("podem ser cobrados", cobra);
        Assert.DoesNotContain("cobrados", naoCobra);
        Assert.Contains("qualquer momento", semPrazo);
    }

    // ---------- Avaliação ----------

    [Fact]
    public void Um_aluno_tem_uma_avaliacao_por_professor()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = NovoProfessor();
        var aluno = new Jogador { Nome = "Aluno", Cpf = "2" };
        ctx.Jogadores.AddRange(prof, aluno);
        ctx.SaveChanges();

        ctx.AvaliacoesProfessor.Add(new AvaliacaoProfessor { ProfessorId = prof.Id, AlunoId = aluno.Id, Nota = 5 });
        ctx.SaveChanges();

        // Reavaliar edita a linha existente — senão a média viraria "quem escreve mais, pesa mais".
        var existente = ctx.AvaliacoesProfessor.Single(a => a.ProfessorId == prof.Id && a.AlunoId == aluno.Id);
        existente.Nota = 3;
        existente.AtualizadoEm = DateTime.Now;
        ctx.SaveChanges();

        Assert.Single(ctx.AvaliacoesProfessor);
        Assert.Equal(3, ctx.AvaliacoesProfessor.Single().Nota);
    }

    [Fact]
    public void Status_de_aula_ativa_reconhece_pendente_e_confirmada()
    {
        Assert.True(PoliticaAula.ContaComoAtiva(PoliticaAula.Pendente));
        Assert.True(PoliticaAula.ContaComoAtiva(PoliticaAula.Confirmada));
        Assert.False(PoliticaAula.ContaComoAtiva(PoliticaAula.Cancelada));
        Assert.False(PoliticaAula.ContaComoAtiva(PoliticaAula.Realizada));
        Assert.False(PoliticaAula.ContaComoAtiva(PoliticaAula.Faltou));
    }
}
