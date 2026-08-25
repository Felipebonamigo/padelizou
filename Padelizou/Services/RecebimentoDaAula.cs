using padelizou.Models;

namespace Padelizou.Services;

// "A aula aconteceu" e "o dinheiro entrou" viraram duas perguntas separadas em 25/08/2026
// (pedido do Felipe: "permita o professor colocar como aula concluída mas ainda não paga, tem
// alunos que pagam depois ou por mês"). A régua de quem deve o quê mora aqui, e não nos
// controllers, porque a MESMA pergunta é feita em quatro lugares que não podem discordar: a
// folha da agenda, o card "a cobrar" do Financeiro, a lista de devedores e a conta do mês.
//
// Função pura, sem EF: é o padrão de VagasDaSessao e PoliticaAula — dá pra testar a régua sem
// montar banco, e é o que faz os quatro lugares chamarem a mesma coisa em vez de reescrevê-la.
public static class RecebimentoDaAula
{
    public static bool EstaPaga(Aula aula) => aula.PagaEm != null;

    // A aula GEROU cobrança? São as duas mesmas portas do FechamentoDoMes — aconteceu, ou não
    // aconteceu e é cobrada assim mesmo —, mais dois cortes que existem só aqui:
    //
    // ⚠️ REPOSIÇÃO FICA DE FORA. Ela aconteceu, mas o dinheiro dela entrou no mês da aula
    // original — foi por isso que ela nasceu sem preço. Contá-la aqui poria na lista de
    // devedores uma aula que ninguém deve. Testado com preço > 0 também: professor que editou
    // o valor da reposição na mão não a transforma numa cobrança nova.
    //
    // ⚠️ AULA DE GRAÇA FICA DE FORA. R$ 0,00 nunca é dívida, e um devedor de R$ 0,00 na tela
    // é ruído que ensina o professor a ignorar a lista inteira.
    public static bool GerouCobranca(Aula aula) =>
        aula.RecuperaAulaId == null
        && aula.Preco > 0
        && (aula.Status == PoliticaAula.Realizada || aula.CobrarMesmoFaltando);

    // O que o professor ainda tem pra receber por esta aula.
    public static bool EstaAReceber(Aula aula) => GerouCobranca(aula) && !EstaPaga(aula);

    // O dinheiro DESTA aula entrou. Junto com EstaAReceber, PARTE em duas exatamente o que
    // gerou cobrança — é essa invariante que faz "Recebido" e "A cobrar" somarem o faturamento
    // do período sem sobrar nem faltar aula em lugar nenhum.
    public static bool FoiRecebida(Aula aula) => GerouCobranca(aula) && EstaPaga(aula);

    // Dá pra dar baixa nela? Mesma régua: só o que gerou cobrança pode ser recebido.
    public static bool PodeMarcar(Aula aula) => GerouCobranca(aula);

    // O motivo é escrito pro professor LER na tela, e cada caso tem o seu — a mensagem
    // genérica manda ele procurar o problema no lugar errado.
    public static string MotivoParaNaoMarcar(Aula aula) =>
        aula.RecuperaAulaId != null
            ? "Essa aula repõe outra — o valor dela já entrou na aula original."
            : aula.Preco <= 0
                ? "Essa aula é de R$ 0,00: não há o que receber."
                : "Essa aula ainda não gerou cobrança — só aula dada, ou falta marcada como cobrável.";

    // O card da turma na agenda mostra o preço SOMADO da sessão (ver AgendaDeTurma.Colapsar).
    // Então ele só pode dizer "paga" quando a soma inteira entrou: com dois de três alunos
    // pagos, o card afirmaria um valor que o professor não recebeu.
    //
    // A data é a do ÚLTIMO pagamento, que é quando a sessão terminou de ser quitada.
    public static DateTime? DaSessao(IEnumerable<Aula> linhas)
    {
        DateTime? ultima = null;

        foreach (var linha in linhas)
        {
            if (linha.PagaEm == null) return null;
            if (ultima == null || linha.PagaEm > ultima) ultima = linha.PagaEm;
        }

        return ultima;
    }
}
