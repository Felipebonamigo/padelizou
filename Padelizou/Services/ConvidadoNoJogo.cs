using Padelizou.Models;

namespace Padelizou.Services;

// O CONVIDADO SEM NOME DA PANELINHA — a vaga de quem jogou e o sistema não sabe quem é.
//
// Nasceu de um pedido do Felipe (20/08/2026): pra alguém de fora entrar num placar era preciso
// que a pessoa JÁ TIVESSE CONTA e fosse convidada pela tela Convidar. Quem lança o jogo está em
// pé ao lado da quadra às 22h com o jogo recém-acabado — e o primo do Rafael que tapou o buraco
// não tem conta nenhuma. Sem esta vaga, o resultado da noite simplesmente não é lançado.
//
// ⚠️ NO BANCO ELE É NULO (JogoSemanal.Dupla1Jogador1Id e as outras três). NO FORMULÁRIO ele é
// -1, e essa diferença NÃO é preciosismo. Os parâmetros dos dois POSTs continuam `int`: campo
// faltando ou com lixo vira ZERO pelo binder e é RECUSADO pelo porteiro, como já é hoje. Se o
// parâmetro virasse `int?`, "veio um convidado" e "o formulário chegou truncado" seriam o MESMO
// POST, byte a byte — e a única coisa separando os dois seria o `disabled` de um botão em
// JavaScript. O sentinela explícito mantém o fail-closed que já existe.
//
// ⚠️ E ELE NÃO É O MESMO "CONVIDADO" DA TELA DE CONVIDAR. Aquele é o avulso COM CONTA
// (ConfirmacaoSessao.Avulso): aparece em lista, é rastreável, tem histórico. Este aqui não
// aparece em lista nenhuma porque não é ninguém. As duas portas convivem de propósito — ver o
// comentário do link em Views/Grupos/RegistrarJogo.cshtml.
public static class ConvidadoNoJogo
{
    // O valor que viaja no formulário. Negativo porque nenhum Jogador.Id é negativo, e porque o
    // ZERO já tem dono: é o que o binder produz pra campo vazio, e continua sendo recusado.
    public const int NoFio = -1;

    // ⚠️ TETO LISO (2 no jogo inteiro), e não "1 por dupla". Três anônimos viram troféu
    // unilateral: um membro "vence sozinho", leva 3 pontos no ranking interno, repetível, e não
    // sobra ninguém pra desmentir. Com 2 sobram sempre DOIS identificados que estavam na quadra.
    // Liso também porque é o único formato que cabe num número — "1 por dupla" é uma FORMA, e o
    // JavaScript teria que reescrevê-la; regra duplicada é o bug histórico nº 1 deste projeto.
    public const int MaximoPorJogo = 2;

    public const string Rotulo = "Convidado";

    public static int? DoFio(int idDoFormulario) => idDoFormulario == NoFio ? null : idDoFormulario;

    public static int ParaOFio(int? id) => id ?? NoFio;

    public static int Quantos(int? a, int? b, int? c, int? d) =>
        (a == null ? 1 : 0) + (b == null ? 1 : 0) + (c == null ? 1 : 0) + (d == null ? 1 : 0);

    // ⚠️ O TETO É CONFERIDO EM DOIS POSTs — registrar e corrigir. Ensinar só um faz o Corrigir
    // recusar o jogo que o Registrar acabou de salvar, e é assim que a regra duplicada morde.
    // O JavaScript recebe o número deste mesmo lugar, pelo payload da view, em vez de repeti-lo.
    public static string? MotivoParaNaoSalvar(int? d1j1, int? d1j2, int? d2j1, int? d2j2) =>
        Quantos(d1j1, d1j2, d2j1, d2j2) > MaximoPorJogo
            ? $"Dá pra marcar no máximo {MaximoPorJogo} convidados num jogo — os outros lugares "
              + "precisam ser gente da panelinha, senão o placar não tem quem confirme."
            : null;

    // O nome pra tela. Recebe o ID **e** a navegação, de propósito.
    //
    // ⚠️ NAVEGAÇÃO NULA COM ID PREENCHIDO NÃO É CONVIDADO — é `.Include()` esquecido na query, e
    // engolir isso como "Convidado" imprimiria a palavra por cima do nome de um membro real, sem
    // erro e sem log. Hoje esse caso derruba a página (NullReferenceException em
    // Views/Grupos/Detalhes.cshtml) e ele TEM que continuar derrubando: um `?? "Convidado"` solto
    // transformaria todo Include esquecido daqui pra frente num convidado silencioso.
    public static string NomeNaTela(int? id, Jogador? jogador) =>
        Quem(id, jogador) is Jogador pessoa ? pessoa.ComoChamar : Rotulo;

    // O mesmo, mas SÓ COM O APELIDO (nome curto pra quem não tem) — a forma que a lista de
    // jogos da panelinha usa desde 26/08/2026, a pedido do Felipe: "pode ser só o apelido".
    // O porquê de existirem as duas formas está em NomeBonito.ApelidoOuCurto.
    public static string ApelidoNaTela(int? id, Jogador? jogador) =>
        Quem(id, jogador) is Jogador pessoa ? NomeBonito.ApelidoOuCurto(pessoa.Nome, pessoa.Apelido) : Rotulo;

    // A guarda mora AQUI, num lugar só: as duas formas acima precisam dela igual, e uma cópia
    // que esquecesse o `throw` transformaria todo Include esquecido num convidado silencioso.
    // Devolve null quando é a vaga do convidado de verdade (id nulo).
    private static Jogador? Quem(int? id, Jogador? jogador)
    {
        if (id == null) return null;
        if (jogador == null) throw new InvalidOperationException(
            $"JogoSemanal aponta pro jogador {id}, mas a navegação veio nula: falta um .Include() na query.");
        return jogador;
    }
}
