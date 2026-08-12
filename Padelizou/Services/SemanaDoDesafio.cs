using Padelizou.Models;

namespace Padelizou.Services;

// A SEMANA do anúncio: até quando ele vale, e como se responde "acabou?".
//
// ⚠️ A regra que não pode ser esquecida: **`Expirado` não é status**. Um anúncio guarda
// `ValeAte` e quem responde se ele acabou é o RELÓGIO, na leitura. Gravar exigiria um job
// virando a chave de todo anúncio à meia-noite de domingo — e um job que falha calado
// transforma anúncio vivo em anúncio morto sem ninguém saber. É a mesma lição do "Vencido"
// dos leads comerciais.
//
// ⚠️ E a segunda: anúncio vencido NÃO cancela desafio já aceito. O anúncio morre no domingo;
// o jogo combinado pode ser na terça, e ele é compromisso entre quatro pessoas. Por isso
// `Desafio.AnuncioDeDesafioId` é SetNull e nada aqui toca em desafio — a pergunta das 8
// inscrições que sumiram ("o que mais morre junto?") tem uma resposta só: nada.
public static class SemanaDoDesafio
{
    // A semana do padel é segunda → domingo: quem diz "essa semana eu jogo" está falando do
    // fim de semana que vem, não do que passou.
    public static DateTime FimDaSemanaDe(DateTime momento)
    {
        var diasAteDomingo = ((int)DayOfWeek.Sunday - (int)momento.DayOfWeek + 7) % 7;
        return momento.Date.AddDays(diasAteDomingo).AddDays(1).AddSeconds(-1);
    }

    public static DateTime FimDaSemanaSeguinte(DateTime momento) =>
        FimDaSemanaDe(momento).AddDays(7);

    // Quanto tempo de vida ainda tem um anúncio publicado hoje. Serve pra tela decidir o que
    // oferecer: publicar "esta semana" numa noite de sábado dá um anúncio de horas, e é aí que
    // a próxima semana precisa vir marcada por padrão.
    public static bool SemanaJaEstaAcabando(DateTime momento) =>
        FimDaSemanaDe(momento) - momento < TimeSpan.FromDays(2);

    public static DateTime FimDaSemanaSugerido(DateTime momento) =>
        SemanaJaEstaAcabando(momento) ? FimDaSemanaSeguinte(momento) : FimDaSemanaDe(momento);

    // ⚠️ `ValeAte` NULO é "até alguém aceitar": não vence nunca no relógio. Quem tira esse
    // anúncio do mural é o aceite de um desafio (ele vira `Fechado`) ou a própria dupla.
    public static bool Vencido(AnuncioDeDesafio anuncio, DateTime agora) =>
        anuncio.ValeAte != null && agora > anuncio.ValeAte.Value;

    public static bool SemPrazo(AnuncioDeDesafio anuncio) => anuncio.ValeAte == null;

    // O anúncio aparece no mural? Precisa das três coisas: publicado, com o parceiro dentro e
    // dentro do prazo (quando existe prazo). Rascunho fora é a trava do convite por link — ver
    // ConviteDoAnuncio; quem escolheu o parceiro na tela já nasce publicado.
    public static bool NoMural(AnuncioDeDesafio anuncio, DateTime agora) =>
        anuncio.Status == AnuncioDeDesafio.Publicado
        && anuncio.Jogador2Id != null
        && !Vencido(anuncio, agora);

    // O que a pessoa lê no cartão do próprio anúncio.
    public static string StatusNaTela(AnuncioDeDesafio anuncio, DateTime agora)
    {
        if (anuncio.Status == AnuncioDeDesafio.Cancelado) return "Cancelado";
        if (anuncio.Status == AnuncioDeDesafio.Fechado) return "Jogo marcado";
        if (anuncio.Status == AnuncioDeDesafio.Rascunho) return "Aguardando o parceiro";
        return Vencido(anuncio, agora) ? "Semana encerrada" : "No mural";
    }

    // "até domingo, 17/08" — o prazo dito como as pessoas falam dele.
    public static string RotuloDoPrazo(DateTime? valeAte) =>
        valeAte == null ? "até alguém aceitar" : $"até domingo, {valeAte.Value:dd/MM}";

    // Até quando dá pra marcar um jogo com um anúncio SEM prazo.
    //
    // ⚠️ Um teto precisa existir mesmo sem data no anúncio: "aceito desafio quando aparecer"
    // não é "aceito jogar em abril". Sem isto, o campo de data do desafio aceitaria qualquer
    // futuro — e um jogo marcado pra daqui a oito meses é um compromisso que ninguém honra e
    // que fica ocupando "Meus desafios" até lá.
    public static readonly TimeSpan TetoSemPrazo = TimeSpan.FromDays(30);

    // O limite de data pra um desafio entre DOIS anúncios: o menor prazo entre eles, e o teto
    // quando nenhum dos dois tem prazo.
    public static DateTime LimiteParaJogar(AnuncioDeDesafio a, AnuncioDeDesafio b, DateTime agora)
    {
        var prazos = new[] { a.ValeAte, b.ValeAte }.Where(p => p != null).Select(p => p!.Value).ToList();

        return prazos.Count == 0 ? agora + TetoSemPrazo : prazos.Min();
    }
}
