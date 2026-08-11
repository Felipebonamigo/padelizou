namespace Padelizou.Services;

// O TEXTO dos avisos que os Desafios geram, e por qual canal cada um sai.
//
// Fica fora do controller pelo motivo de sempre: texto de aviso é regra, e regra escrita
// dentro da ação não tem como ser conferida por teste sem subir meio sistema junto.
//
// ⚠️ A DECISÃO DE CANAL É A PARTE PERIGOSA DESTE ARQUIVO. Um mural é uma máquina de spam
// esperando ser ligada, e a cota de e-mail já estourou duas vezes (130 e-mails perdidos num
// dia, com duas recuperações de senha no meio). A régua do AlcanceDoAviso, aplicada aqui:
//
//   · DESAFIO RECEBIDO → AppEWhatsApp. É o único que passa nos três critérios: pessoal (é
//     sobre a pessoa), urgente (a proposta morre em 48h) e acionável (ela aceita ou recusa).
//   · Resposta, placar lançado, cancelamento → SoApp. Acionável, mas não perde valor amanhã.
//   · Ranking, cinturão → AppSemEmail. Bilhete social: bom de ver, ninguém age por causa dele.
//
// ⚠️ E o que NÃO existe: "nova dupla aberta na sua cidade". Isso é broadcast — exatamente o
// que a Meta chama de spam e o que restringiu o número numa noite. O mural é PULL: a pessoa
// entra e olha. No máximo um resumo semanal, e nunca um aviso por anúncio publicado.
public static class AvisoDoDesafio
{
    // Quem desafiou, em que categoria, onde e quando. O corpo precisa dos quatro: um convite
    // sem lugar e hora obriga a abrir o app pra saber se dá — e metade não abre.
    public static TextoDoAviso Recebido(string dupla, string categoria, string clube, DateTime quando) =>
        new("Vocês foram desafiados! 🥊",
            $"{dupla} chamou pra jogar {categoria} no {clube}, {quando:dd/MM 'às' HH'h'mm}. "
            + "Você tem 48h pra responder.");

    // ⚠️ Plural: o sujeito é uma DUPLA. "Ana e Bruno topou" foi o que saiu na primeira
    // conferência — o nome vem de DuplaNaTela e são sempre duas pessoas.
    public static TextoDoAviso Aceito(string dupla, string clube, DateTime quando) =>
        new("Desafio aceito! 🎾",
            $"{dupla} toparam. Jogo marcado no {clube}, {quando:dd/MM 'às' HH'h'mm}.");

    public static TextoDoAviso Recusado(string dupla) =>
        new("Desafio recusado",
            $"{dupla} não vão poder dessa vez. Seu anúncio continua no mural.");

    public static TextoDoAviso Cancelado(string quem, DateTime quando) =>
        new("Desafio cancelado",
            $"{quem} desmarcou o jogo de {quando:dd/MM 'às' HH'h'mm}.");

    // O corpo diz o placar E o prazo, porque o prazo é o que a pessoa precisa saber pra agir:
    // sem responder, esse placar vale.
    public static TextoDoAviso PlacarLancado(string quem, string placar) =>
        new("Confirme o placar",
            $"{quem} lançou {placar}. Se você não responder em 72h, esse placar vale.");

    public static TextoDoAviso PlacarConfirmado(string placar) =>
        new("Placar confirmado ✅",
            $"O desafio fechou em {placar}. Já está no seu histórico.");

    public static TextoDoAviso PlacarContestado(string quem) =>
        new("Placar contestado",
            $"{quem} não concordou com o placar. Ninguém pontua até isso ser resolvido.");

    // O convite pro parceiro entra aqui e não no AvisoSocial porque não é bilhete: sem o "sim"
    // dele o anúncio não vai pro mural, então este aviso é a única coisa que destrava a dupla.
    public static TextoDoAviso ConvitePraDupla(string quem) =>
        new("Te chamaram pra um desafio",
            $"{quem} quer fazer dupla com você nesta semana. Confirme pra o anúncio ir pro mural.");

    // ── O cinturão ────────────────────────────────────────────────────────────────────
    //
    // Os quatro primeiros são bilhetes sociais (AppSemEmail): bons de ver, ninguém age por causa
    // deles. O ÚLTIMO não é — quem perde por não defender precisa saber POR QUE perdeu, senão
    // vira "o site tirou meu cinturão".

    public static TextoDoAviso CinturaoVago(string categoria) =>
        new("Vocês são os donos do cinturão! 🥊",
            $"O cinturão da {categoria} estava vago e é de vocês. Agora é defender.");

    public static TextoDoAviso CinturaoTomado(string categoria) =>
        new("Cinturão conquistado! 🥊",
            $"Vocês tomaram o cinturão da {categoria}. Agora é defender.");

    public static TextoDoAviso CinturaoPerdido(string categoria) =>
        new("O cinturão mudou de mão",
            $"Vocês perderam o cinturão da {categoria} na quadra. Desafie de novo pra retomar.");

    public static TextoDoAviso CinturaoPorOmissao(string categoria) =>
        new("O cinturão é de vocês! 🥊",
            $"O dono da {categoria} não respondeu aos desafios, e o cinturão passou pra vocês — "
            + "foram os primeiros a chamar.");

    // ⚠️ Diz o número e o prazo, porque é a única forma de a regra não parecer arbitrária.
    public static TextoDoAviso CinturaoPerdidoPorOmissao(string categoria) =>
        new("Vocês perderam o cinturão",
            $"O cinturão da {categoria} passou adiante: foram {Cinturao.NaoAtendidosQueCustamOCinturao} "
            + $"desafios recusados ou sem resposta em {Cinturao.JanelaDaDefesa.Days} dias. "
            + "Aceite um desafio pra retomar.");
}
