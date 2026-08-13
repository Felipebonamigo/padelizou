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
    //
    // 09/08/2026 — depois de a cota estourar DE NOVO (130 e-mails perdidos num dia, duas
    // recuperações de senha entre eles), duas famílias inteiras vieram pra cá:
    //
    //   • RESULTADO DE PARTIDA, pros 4 que jogaram e pros seguidores deles. Quem jogou
    //     estava na quadra e já sabe o placar; e uma rodada terminando junto é rajada.
    //   • "ALGUÉM QUE VOCÊ SEGUE SE INSCREVEU", nas duas cópias do gancho.
    //
    // ⚠️ A pergunta pra decidir se um aviso novo entra aqui: *"a pessoa já sabe disso por ter
    // estado lá?"* e *"ela faz alguma coisa por causa deste aviso?"*. Se já sabe, ou se não
    // faz nada, é aqui. E-mail é caro e é o único canal que alcança quem não instalou o app —
    // gastar em recado que não pede ação é tirar de quem pede.
    AppSemEmail,

    // App + WhatsApp, e SEM e-mail. Não é "mais um canal": é pro aviso cujo e-mail JÁ FOI
    // MANDADO à mão, com conteúdo melhor do que o genérico daqui.
    //
    // 13/08/2026 — nasceu pra SOLICITAÇÃO DE AULA. Ela manda, no próprio controller, um e-mail
    // com os botões **Aceitar** e **Recusar**; o e-mail deste serviço chegaria atrás, com o
    // mesmo assunto e sem botão nenhum. Antes disso o jeito de evitar a duplicata era
    // `AppSemEmail`, que também calava o WhatsApp — as duas coisas vinham amarradas sem
    // ninguém ter decidido isso.
    //
    // ⚠️ Este valor é o ÚLTIMO de propósito: os anteriores mantêm o número que sempre tiveram.
    AppEWhatsAppSemEmail,
}

// As duas perguntas que a entrega faz, num lugar só. O enum já é 2×2 disfarçado de lista
// (WhatsApp sim/não × e-mail sim/não), e espalhar a comparação pelos `if` do entregador é o
// jeito conhecido de um valor novo entrar e ser esquecido em metade dos canais.
public static class AlcanceDoAvisoRegras
{
    // Silêncio é o padrão aqui: o WhatsApp custa o que os outros não custam (a Meta restringe
    // o número), então só vai quando o aviso PEDIU.
    public static bool VaiNoWhatsApp(this AlcanceDoAviso alcance) =>
        alcance is AlcanceDoAviso.AppEWhatsApp or AlcanceDoAviso.AppEWhatsAppSemEmail;

    // O e-mail é o contrário: vai por omissão, e o aviso precisa dispensá-lo na cara.
    public static bool VaiNoEmail(this AlcanceDoAviso alcance) =>
        alcance is not (AlcanceDoAviso.AppSemEmail or AlcanceDoAviso.AppEWhatsAppSemEmail);
}
