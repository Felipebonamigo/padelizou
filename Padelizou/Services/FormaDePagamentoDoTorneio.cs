namespace Padelizou.Services;

// As três formas de receber a inscrição de um torneio — e a pergunta "ainda dá pra trocar?".
//
// Até 08/08/2026 a forma escolhida na criação era DEFINITIVA: ela aparecia num rádio da tela
// de criar e em lugar nenhum da tela de gerenciar. Quem clicasse errado — ou quem só depois
// conectasse a conta de recebimento, ou combinasse outro arranjo com o clube — tinha uma
// saída só: apagar o torneio e criar de novo, perdendo o link já compartilhado, a capa, as
// categorias, as quadras e os horários. Pedido do Felipe preparando o torneio do Nata Padel.
//
// ⚠️ A trava certa é INSCRITO, e não "torneio recém-criado" nem "inscrições ainda abertas".
// A forma de recebimento é as duas coisas ao mesmo tempo: é o que o jogador LEU antes de se
// inscrever ("pago no site" x "combino o Pix com o organizador") e é o que fixa o split no
// momento em que a cobrança nasce. Trocar com gente dentro mexeria nas duas pontas de quem
// já entrou — no caminho pior (site → por fora), o torneio passaria a dizer "acerte com o
// organizador" pra quem já pagou pelo site e está com a vaga confirmada.
//
// Com zero inscrito não há nada disso: não existe cobrança criada, não existe vaga vendida e
// a taxa do "por fora" (TaxaDoTorneioExterno) tem base zero. Trocar é grátis, e é justamente
// aí que o organizador ainda está montando o torneio.
public static class FormaDePagamentoDoTorneio
{
    // Os nomes gravados em Torneio.FormaPagamento. Estavam digitados à mão em oito arquivos
    // (modelo, cobrança, taxas, duas views, controller); aqui viram um lugar só — a mesma
    // história de FormatoDoTorneio, que virou serviço depois de o nome do formato aparecer
    // solto numa dúzia de arquivos.
    public const string Externo = "Externo";
    public const string SoPix = "OnlinePix";
    public const string TodasAsFormas = "OnlineTodas";

    // POST montado à mão pode mandar qualquer texto. Uma forma desconhecida não estoura nada
    // — e é por isso que é perigosa: CobraPeloSite devolveria false (o torneio viraria "por
    // fora" calado) e PercentualDoTorneio cairia no default de 15%. O organizador só
    // descobriria ao procurar o dinheiro.
    public static bool Existe(string? forma) =>
        forma is Externo or SoPix or TodasAsFormas;

    // A régua de "o dinheiro passa pelo Padelizou". Torneio.CobraPeloSite delega pra cá: eram
    // duas escritas da mesma regra (lá, um StartsWith("Online")), e uma forma nova entraria
    // numa sem entrar na outra.
    public static bool EhPeloSite(string? forma) =>
        forma is SoPix or TodasAsFormas;

    public static bool PodeTrocar(bool temAlguemInscrito) => !temAlguemInscrito;

    // O motivo escrito pra quem organiza padel, não pra quem lê log. Fica aqui, e não na
    // view, porque a tela e a recusa do servidor precisam dizer a MESMA coisa: a tela some
    // com os rádios, mas quem manda o formulário na mão bate nesta frase.
    public const string PorqueTravou =
        "A forma de recebimento não muda depois que alguém já se inscreveu — foi ela que o "
        + "jogador leu quando entrou, e é ela que define como a inscrição dele foi cobrada.";
}
