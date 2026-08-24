using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;

namespace Padelizou.Tests;

// O GATE MECÂNICO DA REGRA 0 (21/08/2026): "ação que grava dado precisa de [HttpPost] +
// [Authorize] + checagem de dono/organizador".
//
// A parte da checagem de dono depende de julgamento e continua sendo trabalho dos testes de
// cada área. Mas a parte do [Authorize] é MECÂNICA — dá pra varrer o assembly inteiro e
// provar. Até hoje ela era sustentada só por disciplina: os dois buracos de 26/07 foram
// exatamente POSTs que gravavam sem exigir login, e nada no build reclamou.
//
// Não é analisador Roslyn de propósito. TorneiosController é uma classe partial espalhada por
// 17 arquivos, e [Authorize] vive tanto na classe quanto na ação — um analisador sintático
// precisaria do modelo semântico pra enxergar um atributo declarado noutro arquivo. Reflexão
// vê o conjunto efetivo de graça, e a suíte já é gate obrigatório pra commitar.
//
// ⚠️ Este teste responde "dá pra chamar sem login?", NÃO "quem chama pode fazer isso?".
// Um POST com [Authorize] e sem checagem de organizador passa aqui e continua sendo o buraco
// de 26/07. O gate fecha uma das três condições da Regra 0, não as três.
public class GateDeAutorizacaoDosPostsTests
{
    // A superfície pública do site, endpoint a endpoint, com o motivo de cada um. Nada entra
    // aqui sem uma linha dizendo por quê — uma exceção sem justificativa é o começo do
    // caminho de volta pro estado em que nada era verificado.
    private static readonly Dictionary<string, string> PostsPublicosPorDesenho = new()
    {
        ["AuthController.Login"] = "a porta de entrada — exigir login pra logar não fecha",
        ["AuthController.Cadastro"] = "quem se cadastra ainda não tem conta",
        ["AuthController.EsqueciSenha"] = "quem esqueceu a senha não consegue passar pelo login",
        ["AuthController.RedefinirSenha"] = "chamado pelo link do e-mail, protegido por token de uso único",
        ["AuthController.ReportarProblema"] = "canal de contato — funciona logado ou não, com [ValidateAntiForgeryToken]",
        ["AcessoAntecipadoController.Entrar"] = "é o próprio portão; a defesa aqui é a trava por IP (TravaDeEntrada), não login",
        ["PagamentosController.Webhook"] = "quem chama é o Asaas, sem cookie — a defesa é TokenValido()",
    };

    private static IEnumerable<(Type Tipo, MethodInfo Metodo)> AcoesDePost()
    {
        var assembly = typeof(TorneiosController).Assembly;

        foreach (var tipo in assembly.GetTypes()
                     .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
                     .OrderBy(t => t.Name))
        {
            var metodos = tipo
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.GetCustomAttribute<NonActionAttribute>() is null)
                .Where(EhPost)
                .OrderBy(m => m.Name);

            foreach (var metodo in metodos)
                yield return (tipo, metodo);
        }
    }

    private static bool EhPost(MethodInfo metodo) =>
        metodo.GetCustomAttributes<HttpPostAttribute>(inherit: true).Any()
        || metodo.GetCustomAttributes<AcceptVerbsAttribute>(inherit: true)
            .Any(a => a.HttpMethods.Any(h => string.Equals(h, "POST", StringComparison.OrdinalIgnoreCase)));

    // Espelha o runtime, e não a leitura ingênua do código: QUALQUER [AllowAnonymous] no
    // endpoint — no método OU na classe — faz o AuthorizationMiddleware pular a política,
    // mesmo que o método tenha [Authorize] logo acima. Ler só o atributo da ação daria
    // "protegido" pra um endpoint que na prática atende qualquer um.
    private static bool ExigeLogin(Type tipo, MethodInfo metodo)
    {
        if (metodo.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()) return false;
        if (tipo.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()) return false;

        return metodo.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
               || tipo.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }

    private static List<string> PostsSemLogin() =>
        AcoesDePost()
            .Where(a => !ExigeLogin(a.Tipo, a.Metodo))
            .Select(a => $"{a.Tipo.Name}.{a.Metodo.Name}")
            .Distinct()
            .OrderBy(n => n)
            .ToList();

    [Fact]
    public void NenhumPostNovoAtendeSemLogin()
    {
        var novos = PostsSemLogin()
            .Where(n => !PostsPublicosPorDesenho.ContainsKey(n))
            .ToList();

        Assert.True(novos.Count == 0,
            "POST que grava sem exigir login (Regra 0). Ou põe [Authorize] + a checagem de "
            + "dono/organizador, ou declara em PostsPublicosPorDesenho com o motivo:\n  "
            + string.Join("\n  ", novos));
    }

    // A lista de exceções tem que encolher sozinha. Sem esta guarda, um endpoint que ganhou
    // [Authorize] depois deixaria pra trás uma linha dizendo que ele é público — e a próxima
    // pessoa a ler acreditaria nela.
    [Fact]
    public void ExcecaoQueDeixouDeSerNecessariaSaiDaLista()
    {
        var semLogin = PostsSemLogin().ToHashSet();
        var obsoletas = PostsPublicosPorDesenho.Keys
            .Where(n => !semLogin.Contains(n))
            .OrderBy(n => n)
            .ToList();

        Assert.True(obsoletas.Count == 0,
            "Exceção declarada que não é mais exceção — o endpoint já exige login, ou foi "
            + "renomeado/removido. Tire de PostsPublicosPorDesenho:\n  "
            + string.Join("\n  ", obsoletas));
    }

    // Se a varredura parar de achar POST — controller renomeado, filtro quebrado por uma
    // mudança de API — os dois testes acima passam VAZIOS e param de proteger qualquer coisa,
    // em silêncio. Este é o canário: um gate que não vê nada não é um gate verde.
    [Fact]
    public void AVarreduraEnxergaOsControllers()
    {
        Assert.True(AcoesDePost().Count() > 200,
            $"A varredura só achou {AcoesDePost().Count()} POSTs — eram 281 em 21/08/2026. "
            + "O gate provavelmente parou de enxergar os controllers.");
    }
}
