using Padelizou.Models;

namespace Padelizou.Services;

// QUEM VAI NO JOGO DA SEMANA — o vocabulário num lugar só.
//
// Pedido do Felipe (21/08/2026): "não seja necessário a pessoa aceitar, ela já fica como se
// estivesse no grupo, mas permita a pessoa recusar e sair, ai depois disso, somente aceitando".
// Panelinha é hábito: quem é da terça vai na terça, e cobrar um clique toda semana pra
// confirmar o que já está combinado é burocracia que ninguém cumpre — a lista ficava vazia e o
// jogo acontecia mesmo assim.
//
// ⚠️ O QUE ISSO CUSTA, E POR ISSO EXISTEM DOIS VALORES E NÃO UM: antes da presunção,
// "8 confirmados" queria dizer "8 pessoas disseram sim". Depois dela, passaria a querer dizer
// "8 pessoas não disseram não" — e é com esse número que alguém RESERVA QUADRA. Se `Presumido`
// e `Confirmado` fossem a mesma coisa no banco, essa diferença sumiria pra sempre no primeiro
// dia, e em um mês 100% da lista seria presumida sem ninguém saber. Dois valores mantêm as duas
// perguntas respondíveis: "quantos estão na lista" e "quantos apertaram o botão".
//
// ⚠️ QUEM PERGUNTA "QUEM VAI" USA `EstaNaLista`, NUNCA `== "Confirmado"`. Um leitor esquecido
// não quebra nada — ele só passa a contar gente de menos, calado. É por isso que existe um
// teste varrendo o fonte atrás de comparação com string crua.
public static class PresencaNaSessao
{
    // ⚠️ `const`, e não `static readonly`: entra literal na árvore de expressão e portanto
    // funciona dentro de subquery traduzida pelo EF (HomeController). `static readonly` viraria
    // acesso a campo e o Npgsql não traduz.
    public const string Presumido = "Presumido";
    public const string Confirmado = "Confirmado";
    public const string NaoVai = "NaoVai";
    public const string Pendente = "Pendente";

    // Está contando como quem vai? Presumido e Confirmado contam; Pendente e NaoVai, não.
    public static bool EstaNaLista(string? status) => status is Confirmado or Presumido;

    public static bool EstaNaLista(ConfirmacaoSessao c) => EstaNaLista(c.Status);

    // Com que status a linha do MEMBRO nasce.
    //
    // ⚠️ A RÉGUA É A DATA DA SESSÃO, não "a sessão é nova". Existem dois caminhos de criação
    // (sessão nova e reconciliação de quem entrou depois), e a tela da Semana tem botão de
    // "semana anterior" que manda data arbitrária: sem esta guarda, clicar duas vezes pra trás
    // escreveria "o grupo inteiro estava lá" num jogo de julho que ninguém jogou. O passado não
    // se presume — só o futuro.
    public static string StatusDeNascimento(DateTime dataDaSessao, DateTime agora) =>
        dataDaSessao > agora ? Presumido : Pendente;

    // ⚠️ `NaoVai` COM `RespondidoEm` NULO É "O ADMIN ME TIROU", não "eu avisei que não vou".
    // O par era inalcançável até 21/08/2026 (só `ConfirmarPresenca` escrevia NaoVai, e ele
    // sempre carimbava a data), e é ele que impede a tela de pôr na boca de alguém uma frase
    // que essa pessoa não disse.
    public static bool TiradoPeloAdmin(ConfirmacaoSessao c) =>
        c.Status == NaoVai && c.RespondidoEm == null;

    // O selo ao lado do nome, na lista de quem vai. Um só por pessoa.
    public static string Selo(ConfirmacaoSessao c) =>
        c.Status == Confirmado ? "confirmou" : "no automático";
}
