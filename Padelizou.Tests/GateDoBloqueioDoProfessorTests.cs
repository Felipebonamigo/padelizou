using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using padelizou.Controllers;
using Padelizou.Filters;

namespace Padelizou.Tests;

// O GATE MECÂNICO DO BLOQUEIO (item 2, 22/09/2026). Irmão do GateDeAutorizacaoDosPostsTests e
// pelo mesmo motivo: são 51 POSTs do professor espalhados por 11 arquivos parciais, e uma lista
// escrita à mão de "quais bloqueiam" envelhece calada — o 52º nasceria furado e nada reclamaria.
//
// A defesa é `[ExigePlanoAtivo]` NA CLASSE: endpoint novo já nasce coberto. O que este teste
// guarda são as duas bordas que a classe não guarda sozinha — a classe perder o atributo, e um
// opt-out entrar sem motivo escrito.
//
// ⚠️ ESTE TESTE RESPONDE "o endpoint está sob o bloqueio?", E NÃO "o bloqueio está certo?".
// A régua de quem bloqueia é BloqueioDoProfessorTests; a de quem é dono de cada recurso continua
// sendo trabalho do teste de cada área, como manda a Regra 0.
//
// ⚠️ E ELE NÃO PEGA UM CONTROLLER NOVO de área de professor que nasça sem o atributo — isso é
// ato deliberado de arquitetura, não descuido de endpoint. O risco real é endpoint, e é esse que
// está fechado.
public class GateDoBloqueioDoProfessorTests
{
    // Os controllers em que o professor MEXE NAS COISAS dele.
    private static readonly Type[] AreaDoProfessor =
    {
        typeof(AulasController),        // agenda, alunos, locais, horários, financeiro, faturas
        typeof(JogoAulaController),     // publicar turma aberta
        typeof(ProfessoresController),  // a vitrine dele
    };

    // Quem fica FORA do bloqueio, e por quê. Nada entra aqui sem uma linha — exceção sem
    // justificativa é o começo do caminho de volta pro estado em que nada era verificado.
    private static readonly Dictionary<string, string> ForaDoBloqueioPorDesenho = new()
    {
        ["AulasController.Solicitar"] =
            "é o ALUNO marcando aula. Professor bloqueado que também é aluno de alguém não perde o direito de marcar a aula DELE",
        ["AulasController.CancelarComoAluno"] =
            "o mesmo, do outro lado: desmarcar a aula em que ELE é o aluno",
        ["JogoAulaController.Inscrever"] =
            "é o ALUNO se inscrevendo na turma aberta de outra pessoa",
        ["JogoAulaController.CancelarInscricao"] =
            "o mesmo, desfazendo",
        ["ProfessoresController.Avaliar"] =
            "é o ALUNO avaliando o professor — nota e depoimento nunca foram ação de quem dá aula",
    };

    [Fact]
    public void Os_controllers_do_professor_estao_sob_o_bloqueio()
    {
        foreach (var tipo in AreaDoProfessor)
        {
            Assert.True(
                tipo.GetCustomAttributes<ExigePlanoAtivoAttribute>(inherit: true).Any(),
                $"{tipo.Name} perdeu o [ExigePlanoAtivo] — todos os POSTs dele voltaram a atender "
                + "professor com o plano vencido, sem nada no build reclamando.");
        }
    }

    [Fact]
    public void Todo_opt_out_tem_motivo_escrito()
    {
        var semJustificativa = OptOuts()
            .Select(Nome)
            .Where(n => !ForaDoBloqueioPorDesenho.ContainsKey(n))
            .ToList();

        Assert.True(semJustificativa.Count == 0,
            "Estes endpoints saíram do bloqueio sem uma linha dizendo por quê — escreva o motivo "
            + "em ForaDoBloqueioPorDesenho, ou tire o [SemBloqueioDeProfessor]:\n  "
            + string.Join("\n  ", semJustificativa));
    }

    [Fact]
    public void A_lista_de_excecoes_nao_guarda_endpoint_que_nao_existe_mais()
    {
        // ⚠️ A outra ponta, e a que apodrece sozinha: endpoint renomeado ou apagado deixaria a
        // linha aqui pra sempre, e a próxima sessão leria uma isenção que não protege nada —
        // achando que o caso está resolvido quando ninguém sabe mais qual era.
        var vivos = OptOuts().Select(Nome).ToHashSet();
        var orfas = ForaDoBloqueioPorDesenho.Keys.Where(k => !vivos.Contains(k)).ToList();

        Assert.True(orfas.Count == 0,
            "Estas exceções não correspondem a endpoint nenhum:\n  " + string.Join("\n  ", orfas));
    }

    [Fact]
    public void A_porta_de_saida_NUNCA_fecha()
    {
        // ⚠️ O BLOQUEIO PRECISA TER SAÍDA. `Escolher` e `PagarMensalidade` são os dois únicos
        // jeitos de destravar — pô-los sob o filtro trancaria o professor do lado de fora com a
        // chave dentro, e nenhum dos nossos avisos teria pra onde mandar ele.
        var plano = typeof(PlanoProfessorController);

        Assert.False(plano.GetCustomAttributes<ExigePlanoAtivoAttribute>(inherit: true).Any(),
            "PlanoProfessorController ficou sob o bloqueio: o professor vencido não consegue mais "
            + "assinar nem pagar, e o bloqueio virou porta sem maçaneta.");

        foreach (var acao in new[] { "Escolher", "PagarMensalidade" })
            Assert.True(plano.GetMethod(acao) != null, $"PlanoProfessorController.{acao} sumiu.");
    }

    [Fact]
    public void O_bloqueio_cobre_a_agenda_o_cadastro_e_o_dinheiro_do_professor()
    {
        // O controle do gate: prova que o atributo de classe alcança MESMO os POSTs espalhados
        // pelos arquivos parciais, e nomeia um de cada área que o Felipe pediu pra fechar —
        // inclusive os de fechar mês e dar baixa, que ele escolheu bloquear de olhos abertos.
        foreach (var acao in new[] { "AdicionarManual", "Editar", "CriarLocal", "CriarHorario",
                                     "FecharMes", "MarcarRecebida", "ConfirmarSolicitacao" })
        {
            // ⚠️ Por NOME e não por GetMethod: várias destas ações têm o par GET/POST com o
            // mesmo nome, e GetMethod estoura com AmbiguousMatchException. Filtrar pelo verbo
            // é o que responde a pergunta certa — é o POST que grava.
            var posts = typeof(AulasController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == acao)
                .Where(m => m.GetCustomAttributes<HttpPostAttribute>(inherit: true).Any())
                .ToList();

            Assert.True(posts.Count > 0, $"AulasController.{acao} sumiu — o gate ficou cego pra ele.");
            Assert.All(posts, m => Assert.Null(m.GetCustomAttribute<SemBloqueioDeProfessorAttribute>()));
        }
    }

    private static IEnumerable<MethodInfo> OptOuts() =>
        AreaDoProfessor
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetCustomAttribute<SemBloqueioDeProfessorAttribute>() != null);

    private static string Nome(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";
}
