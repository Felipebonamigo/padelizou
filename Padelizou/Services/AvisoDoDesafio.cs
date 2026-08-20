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

    // A variante de quem TEM o cinturão: o mesmo desafio, mas com o que está em jogo no
    // título. Mesmo canal do Recebido — é o mesmo evento, com o mesmo prazo e a mesma ação;
    // só o texto sabe que este jogo vale o reinado.
    public static TextoDoAviso RecebidoPeloCinturao(string dupla, string categoria, string clube, DateTime quando) =>
        new("Seu reinado está em risco! 🥊",
            $"{dupla} vieram atrás do cinturão da {categoria}: {clube}, {quando:dd/MM 'às' HH'h'mm}. "
            + "Vocês têm 48h pra responder — e 3 desafios sem resposta custam o cinturão.");

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

    // ⚠️ Os quatro são avisados, inclusive quem "ganhou" a disputa. Decisão sobre briga entre
    // usuários que ninguém comunica é a próxima reclamação — e o silêncio faz o placar parecer
    // ter mudado sozinho.
    public static TextoDoAviso DisputaResolvida(string placar) =>
        new("A disputa do placar foi resolvida",
            $"O desafio fechou em {placar}, e os pontos entraram no ranking.");

    public static TextoDoAviso DisputaAnulada() =>
        new("O desafio foi anulado",
            "Não deu pra estabelecer o placar, então ninguém pontuou por esse jogo.");

    // O convite pro parceiro entra aqui e não no AvisoSocial porque não é bilhete: sem o "sim"
    // dele o anúncio não vai pro mural, então este aviso é a única coisa que destrava a dupla.
    public static TextoDoAviso ConvitePraDupla(string quem) =>
        new("Te chamaram pra um desafio",
            $"{quem} quer fazer dupla com você nesta semana. Confirme pra o anúncio ir pro mural.");

    // ⚠️ ESTE AVISO É O QUE SUSTENTA A REGRA. Desde 12/08/2026 dá pra montar a dupla sem pedir
    // licença ao parceiro — e o que torna isso aceitável é ele poder sair. "Poder sair" só vale
    // se ele SOUBER: o anúncio é público e diz a categoria, os clubes e a semana em que ele vai
    // jogar. Por isso o corpo diz, na mesma frase, o que aconteceu E como desfazer.
    public static TextoDoAviso ParceiroIncluido(string quem) =>
        new("Te colocaram numa dupla 🥊",
            $"{quem} criou um desafio com você, e ele já está no mural. "
            + "Se não quiser, é só abrir os Desafios e remover.");

    // ⚠️ DOIS TEXTOS, e não um: quem cancela pode ser o parceiro SAINDO ou quem criou
    // DESISTINDO, e as duas coisas soam muito diferentes pra quem lê. Na primeira conferência
    // saiu "Rafael não quis participar da dupla" sobre o próprio dono do anúncio.
    public static TextoDoAviso ParceiroSaiuDaDupla(string quem) =>
        new("Seu desafio saiu do mural",
            $"{quem} preferiu não participar da dupla, então o desafio foi removido.");

    public static TextoDoAviso CriadorRemoveuODesafio(string quem) =>
        new("O desafio de vocês saiu do mural",
            $"{quem} removeu o desafio. Nenhum jogo já marcado foi desmarcado.");

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

    // O ANÚNCIO PRA CATEGORIA quando o cinturão troca de mão (20/08/2026) — quem joga os
    // desafios daquela categoria fica sabendo que tem alvo novo. Um texto só pros três
    // caminhos (vago, tomado na quadra, herdado por omissão): pra quem lê de fora, a notícia
    // é a mesma — o cinturão tem donos novos, e dá pra desafiá-los.
    public static TextoDoAviso CinturaoTemNovosDonos(string categoria, string dupla) =>
        new("O cinturão mudou de mão! 🥊",
            $"{dupla} agora são os donos do cinturão da {categoria}. Quer tomar? Desafie.");
}
