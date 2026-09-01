using Padelizou.ViewModels;
using System.Globalization;

namespace Padelizou.Services;

// O texto que o professor manda pra cobrar as aulas em aberto de um aluno — o botão do
// WhatsApp da lista "Quem está devendo", no Financeiro.
//
// 🗣️ Pedido do Felipe, 01/09/2026: *"caso o professor tenha um aluno com varias aulas em
// atraso, quando ele clicar para cobrar, caso o aluno tenha mais de uma aula devendo,
// perguntar se ele quer que envie todas as cobranças desse aluno para o whats detalhadamente"*.
//
// Até aqui existia UMA frase pra qualquer devedor: "das 7 aula(s) em aberto, total de
// R$ 700,00". Quem deve sete aulas espalhadas por três meses não tem como conferir isso — e a
// resposta padrão do aluno ("que aulas?") devolve o professor pra agenda, dia a dia, que é
// justamente o trabalho que a lista de devedores existe pra poupar.
//
// ⚠️ QUEM MANDA É O PROFESSOR, num toque, pelo WhatsApp DELE (link `wa.me`, que abre a conversa
// com o texto no campo e ainda dá pra editar). Nada aqui passa pelo chip do Padelizou: é a
// mesma decisão escrita em Services/ConviteDaAulaMarcada, e é o que mantém cobrança — a
// mensagem que mais gera bloqueio — longe do número que o projeto já viu ser restringido.
//
// Função pura, sem EF, no molde de RecebimentoDaAula: o texto é a parte que erra calado
// (total que não bate com a linha, data fora de ordem, falta escrita como aula dada), e aqui
// ele é testável sem montar banco nem abrir navegador.
public static class CobrancaDasAulasEmAberto
{
    // pt-BR fixo, e não a cultura da thread: é o MESMO formato que a tela usa ao lado do nome
    // (@Model...ToString("C", ptBr)). Dois jeitos de escrever o mesmo valor na mesma página é
    // como o professor conclui que a conta está errada.
    private static readonly CultureInfo Cultura = new("pt-BR");

    // ⚠️ ATALHO DELIBERADO: o texto viaja DENTRO da URL do wa.me. Cinquenta linhas viram uma
    // URL de vários KB, que é onde navegador e app começam a truncar — e mensagem truncada
    // mente sem avisar. Acima do teto a lista corta e DIZ que cortou; o total continua sendo o
    // da dívida inteira. A saída do professor é o filtro de período da própria tela (semana /
    // mês), que encurta a lista sem esconder nada.
    public const int MaximoDeLinhas = 20;

    // A pergunta só existe com mais de uma aula: com uma só, "detalhar" é repetir a linha que o
    // resumo já tem, e um clique a mais por nada é como se ensina alguém a ignorar o aviso.
    //
    // Olha a lista, e não o contador: sem linha nenhuma a mensagem detalhada sairia com um
    // buraco no meio, e cair no resumo é o pior caso aceitável.
    public static bool CabeDetalhe(DevedorVM devedor) => devedor.Aulas.Count > 1;

    // A frase curta de sempre. Continua sendo o "Cancelar" da pergunta — quem só quer avisar
    // não deve ser obrigado a mandar o extrato.
    public static string Resumo(DevedorVM devedor) =>
        $"{Saudacao(devedor.Nome)} Passando pra lembrar das {devedor.AulasEmAberto} aula(s) em aberto, "
      + $"total de {Reais(devedor.Valor)}. Abraço!";

    // O extrato: uma linha por aula, e o total no fim.
    public static string Detalhada(DevedorVM devedor)
    {
        // Ordena aqui, e não confia na tela: a lista chega ordenada hoje, mas cobrança com as
        // datas embaralhadas é lida como erro de conta — e quem lê é o aluno, não nós.
        var emOrdem = devedor.Aulas.OrderBy(a => a.DataHora).ToList();

        var linhas = emOrdem.Take(MaximoDeLinhas).Select(Linha).ToList();
        var deFora = emOrdem.Count - linhas.Count;
        if (deFora > 0) linhas.Add($"• …e mais {deFora} aula(s)");

        return $"{Saudacao(devedor.Nome)} Passando pra lembrar das {devedor.AulasEmAberto} aula(s) em aberto:\n\n"
             + string.Join("\n", linhas)
             + $"\n\nTotal: {Reais(devedor.Valor)}\n\n"
             + "Qualquer coisa é só me chamar. Abraço!";
    }

    // "• 04/08 (ter) 19:00 · R$ 110,00". O dia da semana entra porque é assim que o aluno
    // guarda a aula dele ("a de terça"), e é o que faz ele reconhecer a linha sem abrir
    // calendário.
    private static string Linha(AulaEmAbertoVM aula) =>
        $"• {aula.DataHora.ToString("dd/MM", Cultura)} ({PeriodoAgenda.DiaCurto(aula.DataHora.DayOfWeek)}) "
      + $"{aula.DataHora.ToString("HH:mm", Cultura)} · {Reais(aula.Preco)}{Marcador(aula.Status)}";

    // ⚠️ A aula que não aconteceu PRECISA se identificar. Sem o marcador, o aluno lê a data,
    // lembra que não teve aula naquele dia, e a cobrança inteira perde a credibilidade por
    // causa de uma linha. São dois casos diferentes, e o aluno lê a diferença:
    //   • falta cobrada — o dia foi perdido (PoliticaAula.Faltou + CobrarMesmoFaltando);
    //   • fila de reposição — cobrada, mas a aula continua de pé pra ser remarcada.
    private static string Marcador(string status) => status switch
    {
        PoliticaAula.Faltou => " (falta)",
        PoliticaAula.ARecuperar => " (a repor)",
        _ => "",
    };

    // O primeiro nome é como o professor chama o aluno; o nome de cadastro inteiro soa
    // cobrança de banco. Nome em branco (o "Aluno avulso" sem nada anotado) não pode virar
    // "Oi !" — que é o tipo de coisa que se manda sem ver e o aluno vê.
    private static string Saudacao(string? nome)
    {
        var primeiro = (nome ?? "").Trim().Split(' ')[0];
        return primeiro.Length == 0 ? "Oi!" : $"Oi {primeiro}!";
    }

    private static string Reais(decimal valor) => valor.ToString("C", Cultura);
}
