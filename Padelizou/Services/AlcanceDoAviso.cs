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
}
