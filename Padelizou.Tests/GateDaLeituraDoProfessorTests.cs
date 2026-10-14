using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using padelizou.Controllers;
using Padelizou.Filters;
using Xunit;

namespace Padelizou.Tests;

// O GATE DA LEITURA (14/10/2026). Irmão do GateDoBloqueioDoProfessorTests, e pelo mesmo motivo:
// as telas do professor e as do ALUNO moram nos mesmos controllers. Fechar o POST na classe
// funciona porque quem fica de fora se DECLARA; na leitura a regra de classe fecharia a vitrine
// e as telas de aluno junto. Então aqui é o contrário — a tela do professor se marca
// `[AgendaDoProfessor]` — e este teste OBRIGA a classificar: GET novo sem classificação reprova.
//
// ⚠️ Sem isto, a lista de "o que fecha" seria a lista escrita à mão que envelhece calada: a 20ª
// tela do professor nasceria aberta e nada reclamaria.
public class GateDaLeituraDoProfessorTests
{
    private static readonly Type[] AreaDoProfessor =
    {
        typeof(AulasController), typeof(JogoAulaController), typeof(ProfessoresController),
    };

    // Telas que ficam ABERTAS pro professor bloqueado, e por quê.
    private static readonly Dictionary<string, string> AbertasPorDesenho = new()
    {
        ["AulasController.Solicitar"] = "é o ALUNO marcando aula",
        ["AulasController.ObterOfertas"] = "o ALUNO escolhendo entre as ofertas do professor",
        ["AulasController.SolicitacaoEnviada"] = "o recibo do ALUNO",
        ["AulasController.MinhasAulas"] = "as aulas em que ele é ALUNO",
        ["AulasController.MinhaEvolucao"] = "a evolução dele como ALUNO",
        ["AulasController.ConfirmarPorEmail"] =
            "link de e-mail sem login: o filtro não sabe quem é; a própria ação confere o professor da aula",
        ["JogoAulaController.Index"] = "a lista pública de turmas abertas",
        ["JogoAulaController.Detalhes"] = "a turma aberta, vista pelo aluno",
        ["ProfessoresController.Index"] = "a vitrine pública",
        ["ProfessoresController.Comecar"] = "página pública pra quem ainda não joga",
        ["ProfessoresController.Perfil"] = "o perfil público do professor",
    };

    private static IEnumerable<MethodInfo> Leituras() =>
        AreaDoProfessor
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetCustomAttribute<NonActionAttribute>() == null)
            .Where(m => !m.GetCustomAttributes<HttpPostAttribute>(inherit: true).Any());

    private static string Nome(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

    [Fact]
    public void Toda_leitura_do_professor_foi_classificada()
    {
        var soltas = Leituras()
            .Where(m => m.GetCustomAttribute<AgendaDoProfessorAttribute>() == null)
            .Select(Nome)
            .Where(n => !AbertasPorDesenho.ContainsKey(n))
            .Distinct()
            .ToList();

        Assert.True(soltas.Count == 0,
            "Estas telas não dizem se fecham pro professor bloqueado — marque [AgendaDoProfessor] "
            + "ou escreva o motivo em AbertasPorDesenho:\n  " + string.Join("\n  ", soltas));
    }

    [Fact]
    public void A_lista_de_abertas_nao_guarda_tela_que_nao_existe_mais()
    {
        var vivas = Leituras().Select(Nome).ToHashSet();
        var orfas = AbertasPorDesenho.Keys.Where(k => !vivas.Contains(k)).ToList();

        Assert.True(orfas.Count == 0, "Exceções sem tela:\n  " + string.Join("\n  ", orfas));
    }

    [Fact]
    public void Tela_marcada_nao_pode_estar_tambem_na_lista_de_abertas()
    {
        var duplas = Leituras()
            .Where(m => m.GetCustomAttribute<AgendaDoProfessorAttribute>() != null)
            .Select(Nome)
            .Where(AbertasPorDesenho.ContainsKey)
            .ToList();

        Assert.True(duplas.Count == 0, "Marcadas E abertas:\n  " + string.Join("\n  ", duplas));
    }

    // O controle: as telas que o Felipe pediu pra fechar estão mesmo marcadas.
    [Theory]
    [InlineData(typeof(AulasController), "MinhaAgenda")]
    [InlineData(typeof(AulasController), "Dashboard")]
    [InlineData(typeof(AulasController), "MeusAlunos")]
    [InlineData(typeof(AulasController), "Financeiro")]
    [InlineData(typeof(AulasController), "Faturamento")]
    [InlineData(typeof(AulasController), "MeusHorarios")]
    [InlineData(typeof(JogoAulaController), "Publicar")]
    public void As_telas_da_agenda_estao_marcadas(Type controller, string acao)
    {
        var gets = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == acao && !m.GetCustomAttributes<HttpPostAttribute>(inherit: true).Any())
            .ToList();

        Assert.NotEmpty(gets);
        Assert.All(gets, m => Assert.NotNull(m.GetCustomAttribute<AgendaDoProfessorAttribute>()));
    }
}
