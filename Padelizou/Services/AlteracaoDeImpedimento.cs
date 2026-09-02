using Padelizou.Models;

namespace Padelizou.Services;

// Os quatro turnos como UMA escolha, e não como quatro booleanos soltos.
//
// O modelo guarda quatro `bool` por motivo histórico, mas a regra real é que só um deles pode
// ser true (ver Services/ImpedimentoUnico). Um enum é o formato honesto pra tela e pro POST:
// "qual turno?" tem uma resposta, e quatro caixas de seleção convidam a marcar duas.
public enum TurnoDoImpedimento
{
    Nenhum,
    QuintaNoite,
    SextaNoite,
    SabadoManha,
    SabadoTarde,
}

// Trocar o impedimento depois de já estar inscrito.
//
// 🗣️ Pedido do Felipe, 02/09/2026: "permita a pessoa alterar o impedimento, até o fechamento
// das inscrições". Até aqui o turno era escolhido uma vez, na inscrição, e não tinha volta —
// quem descobria o compromisso depois só resolvia falando com o organizador.
//
// ⚠️ ISTO ENCOSTA EM DINHEIRO, e é por isso que a régua veio dele, não de mim: o torneio cobra
// `TaxaPorImpedimento` por janela marcada, e o `ValorInscricao` é CONGELADO quando a inscrição
// nasce. A régua, nas palavras dele: "se já tem outro impedimento, mantém o mesmo custo; se
// não, avisa que é cobrado e o valor que é adicionado".
//
// Ela cai redonda porque a quantidade só vive em 0 ou 1: trocar é de graça, marcar cobra,
// tirar devolve ao valor devido.
public static class AlteracaoDeImpedimento
{
    // O turno que a inscrição tem HOJE. Um lugar só pra ler os quatro booleanos — espalhar
    // essa leitura é como as telas passam a discordar sobre a mesma dupla.
    public static TurnoDoImpedimento TurnoAtual(Dupla dupla)
    {
        if (dupla.ImpedimentoQuintaNoite) return TurnoDoImpedimento.QuintaNoite;
        if (dupla.ImpedimentoSextaNoite) return TurnoDoImpedimento.SextaNoite;
        if (dupla.ImpedimentoSabadoManha) return TurnoDoImpedimento.SabadoManha;
        if (dupla.ImpedimentoSabadoTarde) return TurnoDoImpedimento.SabadoTarde;
        return TurnoDoImpedimento.Nenhum;
    }

    // Quanto o valor da inscrição MUDA se o turno virar `novo`. Positivo = a dupla passa a
    // dever mais; negativo = passa a dever menos; zero = troca, ou torneio que não cobra.
    public static decimal QuantoMudaOValor(Dupla dupla, Torneio torneio, TurnoDoImpedimento novo)
    {
        int antes = TurnoAtual(dupla) == TurnoDoImpedimento.Nenhum ? 0 : 1;
        int depois = novo == TurnoDoImpedimento.Nenhum ? 0 : 1;

        return (depois - antes) * torneio.TaxaPorImpedimento;
    }

    // Devolve o motivo da recusa, ou null quando pode alterar.
    //
    // `novo` é opcional porque a tela pergunta duas coisas em momentos diferentes: "dá pra
    // mexer nesta inscrição?" (pra decidir se mostra o formulário) e "dá pra gravar ESTA
    // troca?" (no POST). Sem o turno, responde só a primeira.
    public static string? MotivoParaNaoAlterar(Dupla? dupla, Torneio? torneio, int quemPede,
        TurnoDoImpedimento? novo = null)
    {
        if (dupla == null || torneio == null) return "Não encontrei essa inscrição.";

        // Time não passa por aqui: o Jogador1Id dele é o organizador que o cadastrou, e sem
        // esta linha o organizador mexeria no impedimento de um time pela porta do jogador.
        if (dupla.EhTime) return "Times são gerenciados pelo organizador na tela de times.";

        if (dupla.Jogador1Id != quemPede && dupla.Jogador2Id != quemPede)
            return "Essa inscrição não é sua.";

        // O limite que o Felipe pediu, e ele tem motivo de grade: depois do sorteio os jogos
        // já estão marcados e a janela nova obrigaria a remontar tudo.
        if (torneio.Status != "Inscrições Abertas")
            return "As inscrições já foram encerradas — fale com o organizador pra mudar o impedimento.";

        // ⚠️ INSCRIÇÃO PAGA só aceita alteração que NÃO mexe no valor — ou seja, a troca de um
        // turno por outro, que é justamente a mais comum ("não posso mais na sexta, posso no
        // sábado"). Marcar um impedimento novo criaria cobrança extra; tirar criaria
        // devolução, e devolução aqui é o botão de estorno do organizador, na mão (ESTORNO.md).
        // Fingir que a tela resolve isso sozinha é como o dinheiro fica pendurado sem ninguém
        // saber.
        if (novo is { } turno && dupla.Pago && QuantoMudaOValor(dupla, torneio, turno) != 0)
        {
            return "Essa inscrição já está paga: mudar isso mexeria no valor. "
                 + "Fale com o organizador — ele acerta o pagamento e a mudança junto.";
        }

        return null;
    }

    // Grava a troca. Não decide nada: quem decide é o MotivoParaNaoAlterar, que o chamador já
    // consultou.
    public static void Aplicar(Dupla dupla, Torneio torneio, TurnoDoImpedimento novo,
        int quemAlterou, DateTime agora)
    {
        var diferenca = QuantoMudaOValor(dupla, torneio, novo);

        dupla.ImpedimentoQuintaNoite = novo == TurnoDoImpedimento.QuintaNoite;
        dupla.ImpedimentoSextaNoite = novo == TurnoDoImpedimento.SextaNoite;
        dupla.ImpedimentoSabadoManha = novo == TurnoDoImpedimento.SabadoManha;
        dupla.ImpedimentoSabadoTarde = novo == TurnoDoImpedimento.SabadoTarde;

        // ⚠️ Nulo continua nulo. Inscrição anterior à coluna `ValorInscricao` não tem valor
        // congelado de propósito (ver Models/Dupla): inventar um número aqui seria adivinhar o
        // preço que valia no dia em que ela nasceu.
        if (dupla.ValorInscricao is decimal valor) dupla.ValorInscricao = valor + diferenca;

        // 🗣️ "deixe registrado quem marcou o impedimento". Sem isto, a dupla que chega no dia
        // reclamando do horário não tem como saber qual dos dois mexeu — e o organizador,
        // menos ainda.
        dupla.ImpedimentoAlteradoPorId = quemAlterou;
        dupla.ImpedimentoAlteradoEm = agora;
    }

    // O rótulo que a pessoa lê. Fica aqui, e não na view, porque a lista do sorteio e a tela
    // da inscrição precisam dizer a MESMA coisa sobre o mesmo turno.
    public static string Rotulo(TurnoDoImpedimento turno) => turno switch
    {
        TurnoDoImpedimento.QuintaNoite => "Quinta à noite",
        TurnoDoImpedimento.SextaNoite => "Sexta à noite",
        TurnoDoImpedimento.SabadoManha => "Sábado de manhã",
        TurnoDoImpedimento.SabadoTarde => "Sábado à tarde",
        _ => "Sem impedimento",
    };
}
