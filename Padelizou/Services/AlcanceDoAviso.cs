namespace Padelizou.Services;

// Até onde um aviso vai. Nasceu da restrição por spam de 04/08/2026: o WhatsApp era o padrão
// de todo aviso, e uma noite de torneio (24 cadastros em uma hora) fez a Meta restringir o
// número em minutos.
//
// A régua que a Meta usa, traduzida: falar com alguém sobre algo em que a pessoa JÁ ESTÁ
// METIDA é normal; avisar alguém de algo que você quer que ela veja é spam.
public enum AlcanceDoAviso
{
    // Notificação do app e e-mail. É o PADRÃO de propósito: aviso novo nasce sem WhatsApp, e
    // quem quiser o canal precisa pedir na cara. O contrário — WhatsApp por omissão — foi
    // exatamente o que queimou o número, porque cada aviso novo entrava no canal sem ninguém
    // decidir isso.
    SoApp,

    // Também no WhatsApp. Reservado pro que é as três coisas ao mesmo tempo: **pessoal**
    // (sobre a própria pessoa), **urgente** (perde valor se ela vir amanhã) e **acionável**
    // (ela faz alguma coisa por causa dele).
    AppEWhatsApp,

    // Só a caixa de entrada e a notificação do app — SEM e-mail.
    //
    // Existe pros bilhetes sociais: alguém te elogiou, comentou no seu perfil, começou a te
    // seguir. São bons de ver, mas nenhum deles pede resposta nem tem hora pra ser lido, e
    // e-mail pra cada um é exatamente o que faz a pessoa marcar o remetente como lixo — aí
    // ela perde junto o aviso de que a chave saiu. A cota do Gmail já estourou uma vez.
    AppSemEmail,
}
