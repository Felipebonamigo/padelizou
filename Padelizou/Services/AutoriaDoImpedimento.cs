using Padelizou.Models;

namespace Padelizou.Services;

// DE QUEM FOI O IMPEDIMENTO: do jogador ou do organizador?
//
// 🗣️ Reclamação de usuário, 09/09/2026: "não consegue ver qual impedimento foi solicitado pelo
// usuário, e qual pelo organizador".
//
// O dado JÁ EXISTIA desde 02/09 — `Dupla.ImpedimentoAlteradoPorId` guarda quem mexeu por
// último. O que faltava era a PERGUNTA: a tela imprimia só "alterado em <data>", que responde
// QUANDO e não POR QUEM.
//
// ⚠️ A REGRA É "ESSA PESSOA ESTÁ NA DUPLA?", E NÃO "ESSA PESSOA ORGANIZA?". O organizador de um
// torneio joga o próprio torneio o tempo todo (é a regra no interno, não a exceção). Mexendo no
// impedimento da PRÓPRIA dupla, quem pediu foi o jogador — ele mesmo. Perguntar pelo papel
// responderia "organizador" e a tela mentiria justamente no caso mais comum.
//
// ⚠️ A CONCENTRAÇÃO NÃO PASSA POR AQUI. Ela é do organizador POR DEFINIÇÃO — o servidor não
// aceita de mais ninguém (TorneiosController.AlterarConcentracaoOrganizador) —, então a tela
// diz isso sem precisar de coluna nem de conta.
public enum AutoriaDoImpedimento
{
    // Não há impedimento marcado: não há autoria a mostrar. Escrever "pedido pelo jogador" ao
    // lado de "sem impedimento" seria informação inventada.
    Ninguem,
    Jogador,
    Organizador,
}

public static class AutoriaDoImpedimentoDaDupla
{
    public static AutoriaDoImpedimento De(Dupla dupla)
    {
        if (AlteracaoDeImpedimento.TurnoAtual(dupla) == TurnoDoImpedimento.Nenhum)
            return AutoriaDoImpedimento.Ninguem;

        // NULO = veio da inscrição e nunca foi alterado. Quem preenche a inscrição é o jogador,
        // e é o estado de toda dupla anterior à coluna de autoria.
        if (dupla.ImpedimentoAlteradoPorId is not int quem) return AutoriaDoImpedimento.Jogador;

        return quem == dupla.Jogador1Id || quem == dupla.Jogador2Id
            ? AutoriaDoImpedimento.Jogador
            : AutoriaDoImpedimento.Organizador;
    }

    public static string Rotulo(AutoriaDoImpedimento autoria) => autoria switch
    {
        AutoriaDoImpedimento.Jogador => "pedido pelo jogador",
        AutoriaDoImpedimento.Organizador => "posto pelo organizador",
        _ => "",
    };
}
