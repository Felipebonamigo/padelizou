using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Padelizou.Tests;

// CSRF: um site qualquer faz o navegador de quem está logado aqui enviar um formulário sem a
// pessoa saber. A defesa é o carimbo antifalsificação, ligado GLOBALMENTE no Program.cs — então
// não há o que testar por ação: todas estão cobertas por construção.
//
// O que sobra pra vigiar é o outro lado: a lista de EXCEÇÕES. `[IgnoreAntiforgeryToken]` é o
// atributo que alguém colará numa ação pra calar um erro que não entendeu, e o buraco volta em
// silêncio — sem teste vermelho, sem log, sem nada visível na tela. Aqui a exceção passa a custar
// duas edições em arquivos diferentes, e a segunda obriga a escrever por quê.
public class ProtecaoAntifalsificacaoTests
{
    // Quem pode ficar de fora, e a razão. Mudar esta lista é decisão de segurança, não ajuste.
    private static readonly Dictionary<string, string> IsencoesPermitidas = new()
    {
        ["PagamentosController.Webhook"] =
            "Quem chama é o meio de pagamento, de fora, sem cookie e sem página nossa — " +
            "não existe carimbo pra ele mandar. Protegido pelo próprio token no lugar disso.",

        // As duas telas de erro, pelo mesmo motivo: quem as chama não é um cliente, é o
        // FRAMEWORK reexecutando o pipeline — e o re-execute preserva o método da requisição
        // original. Um POST que dá 404 vira um POST /Home/NaoEncontrado; um POST que estoura
        // vira um POST /Home/Error. Com o carimbo valendo ali, o filtro recusa a reexecução e o
        // 400 dele substitui o status de verdade na resposta ao cliente.
        ["HomeController.NaoEncontrado"] =
            "Reexecutada pelo UseStatusCodePagesWithReExecute mantendo o método. Não grava " +
            "nada e não devolve dado de ninguém — só desenha a tela e repete o status original.",

        ["HomeController.Error"] =
            "Reexecutada pelo UseExceptionHandler mantendo o método. Mesma tela sem estado: " +
            "isentar não abre porta nenhuma, e sem isentar todo erro de POST chega como 400.",
    };

    private static IEnumerable<(string Nome, MethodInfo Metodo)> AcoesIsentas()
    {
        var assembly = typeof(Padelizou.Services.VigiaDoBackup).Assembly;

        return assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => (Nome: $"{t.Name}.{m.Name}", Metodo: m)))
            // O atributo na CLASSE isentaria o controller inteiro de uma vez — pega os dois casos.
            .Where(x => x.Metodo.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null
                     || x.Metodo.DeclaringType!.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null);
    }

    [Fact]
    public void Nenhuma_acao_nova_escapa_do_carimbo_sem_estar_na_lista()
    {
        var isentasDeVerdade = AcoesIsentas().Select(x => x.Nome).Distinct().OrderBy(n => n).ToList();
        var previstas = IsencoesPermitidas.Keys.OrderBy(n => n).ToList();

        var naoPrevistas = isentasDeVerdade.Except(previstas).ToList();

        Assert.True(naoPrevistas.Count == 0,
            "Ação(ões) com [IgnoreAntiforgeryToken] fora da lista de exceções: " +
            string.Join(", ", naoPrevistas) +
            ". Se a isenção é mesmo necessária, some à lista em ProtecaoAntifalsificacaoTests " +
            "explicando por quê. Se não é, tire o atributo — o padrão é ser protegido.");
    }

    [Fact]
    public void A_lista_de_excecoes_nao_guarda_isencao_que_ja_saiu_do_codigo()
    {
        // Exceção que sobra na lista depois de a ação mudar de nome vira permissão órfã: um dia
        // alguém cria um método com aquele nome e ele nasce isento sem ninguém decidir isso.
        var isentasDeVerdade = AcoesIsentas().Select(x => x.Nome).Distinct().ToList();

        var sobrando = IsencoesPermitidas.Keys.Except(isentasDeVerdade).ToList();

        Assert.True(sobrando.Count == 0,
            "A lista prevê isenção que não existe mais no código: " + string.Join(", ", sobrando));
    }

    [Fact]
    public void So_existem_dois_tipos_de_isencao_e_nenhum_deles_grava_nada()
    {
        // Prende o número. Crescer tem que ser uma decisão consciente, não o efeito colateral de
        // um teste que já estava verde e continuou verde.
        //
        // São TRÊS isenções de DOIS tipos, e nenhum deles é "ação que grava": (1) a porta que o
        // meio de pagamento chama de fora, sem cookie e sem carimbo possível; (2) as duas telas
        // de erro, que quem chama é o próprio framework reexecutando o pipeline. Uma quarta
        // isenção que não caiba nesses dois tipos é sinal de que alguém calou um erro de
        // carimbo numa ação que grava — que é exatamente o buraco que o filtro global fecha.
        Assert.Equal(3, IsencoesPermitidas.Count);
    }
}
