using Padelizou.Models;

namespace Padelizou.Services;

// O que um desafio confirmado FAZ com o cinturão da categoria.
public enum EfeitoNoCinturao
{
    // O cinturão não muda: nenhuma das duas duplas é a dona.
    Nada,

    // O cinturão estava vago e o vencedor levou. É assim que ele nasce em cada categoria.
    PegouVago,

    // O vencedor tirou o cinturão de quem tinha.
    Tomou,

    // O dono venceu com o cinturão na mão. Vale +1 defesa.
    Defendeu,
}

// 🥊 O CINTURÃO — as regras, sem banco. Espec em DESAFIOS.md, seção 4.
//
// A mecânica é a do boxe: em cada categoria existe UM dono, e quem vencer o dono num desafio
// leva. É a parte da feature que faz alguém abrir o app numa terça sem motivo.
//
// ⚠️ TRÊS REGRAS EXISTEM PRA O CINTURÃO NÃO MORRER NO PRIMEIRO MÊS, e nenhuma delas é enfeite:
//
//  1. O DONO É OBRIGADO A DEFENDER. Recusou ou ignorou 3 desafios em 14 dias e o cinturão passa
//     pro primeiro que desafiou e não foi atendido. Sem isso, ganhar o cinturão e sumir seria a
//     estratégia ótima — e o campeão viraria um nome parado numa tela.
//  2. PERDER PRA QUALQUER UM VALE. Nada de "só o top 10 desafia": o cinturão é a porta de
//     entrada do jogador novo, não o clube fechado dos veteranos.
//  3. É DA DUPLA. Trocou de parceiro, começa de novo — é o que torna a dupla um personagem.
//
// 🔒 O recorte é a CATEGORIA, sem cidade (decisão do Felipe, 11/08/2026 — ver o comentário do
// modelo ReinadoNoCinturao).
public static class Cinturao
{
    // Quantas recusas/ausências de resposta custam o cinturão, e em que janela.
    public const int NaoAtendidosQueCustamOCinturao = 3;
    public static readonly TimeSpan JanelaDaDefesa = TimeSpan.FromDays(14);

    // ── A troca de mão na quadra ──────────────────────────────────────────────────────

    public static EfeitoNoCinturao Efeito(ReinadoNoCinturao? dono, Desafio desafio)
    {
        if (desafio.Status != Desafio.Confirmado) return EfeitoNoCinturao.Nada;

        var lado = EstadoDoDesafio.LadoVencedor(desafio);
        if (lado is not (1 or 2)) return EfeitoNoCinturao.Nada;

        // Cinturão vago: o vencedor do primeiro desafio confirmado da categoria leva. Sem isto o
        // cinturão nunca nasceria — não há dono pra alguém tomar.
        if (dono == null) return EfeitoNoCinturao.PegouVago;

        var chaveDoDono = ChaveDaDupla.De(dono.Jogador1Id, dono.Jogador2Id);
        var chaveVencedora = ChaveVencedora(desafio, lado.Value);
        var chavePerdedora = ChavePerdedora(desafio, lado.Value);

        if (chaveDoDono == chaveVencedora) return EfeitoNoCinturao.Defendeu;
        if (chaveDoDono == chavePerdedora) return EfeitoNoCinturao.Tomou;

        // Dois estranhos jogando entre si não mexem no cinturão de ninguém.
        return EfeitoNoCinturao.Nada;
    }

    public static (int Jogador1Id, int Jogador2Id) DuplaVencedora(Desafio desafio, int lado)
    {
        var (a, b) = lado == 1
            ? (desafio.DesafianteJogador1Id, desafio.DesafianteJogador2Id)
            : (desafio.DesafiadoJogador1Id, desafio.DesafiadoJogador2Id);

        return EmOrdem(a, b);
    }

    // Os dois ids sempre com o menor na frente: é o que faz "Felipe & Lucas" casar com
    // "Lucas & Felipe" (mesma disciplina do ChaveDaDupla).
    public static (int Jogador1Id, int Jogador2Id) EmOrdem(int a, int b) =>
        a <= b ? (a, b) : (b, a);

    private static string ChaveVencedora(Desafio d, int lado) => lado == 1
        ? ChaveDaDupla.De(d.DesafianteJogador1Id, d.DesafianteJogador2Id)
        : ChaveDaDupla.De(d.DesafiadoJogador1Id, d.DesafiadoJogador2Id);

    private static string ChavePerdedora(Desafio d, int lado) => lado == 1
        ? ChaveDaDupla.De(d.DesafiadoJogador1Id, d.DesafiadoJogador2Id)
        : ChaveDaDupla.De(d.DesafianteJogador1Id, d.DesafianteJogador2Id);

    // ── A defesa obrigatória ──────────────────────────────────────────────────────────

    // Um desafio que o dono NÃO ATENDEU: recusou, ou deixou a proposta morrer nas 48h.
    //
    // ⚠️ Cancelar um desafio já aceito NÃO conta, de propósito. A espec fala em "recusou ou
    // ignorou", e desmarcar tem motivo legítimo demais (chuva, lesão, quadra alagada) pra custar
    // um cinturão. Quem cancela por esporte perde de outro jeito: o adversário desafia de novo, e
    // aí é recusa.
    public static bool NaoAtendido(Desafio desafio, DateTime agora) =>
        desafio.Status == Desafio.Recusado || EstadoDoDesafio.PropostaVencida(desafio, agora);

    // QUANDO o dono deixou de atender. Recusa tem hora; ignorar acontece quando as 48h vencem.
    public static DateTime QuandoNaoAtendeu(Desafio desafio) =>
        desafio.Status == Desafio.Recusado
            ? desafio.RespondidoEm ?? desafio.PropostoEm
            : desafio.PropostoEm + EstadoDoDesafio.PrazoParaResponder;

    // Os desafios que contam contra o dono: dirigidos A ELE (ele é o lado desafiado), na
    // categoria do cinturão, não atendidos, dentro da janela — e depois de ele ser dono.
    //
    // ⚠️ O corte por `ComecouEm` importa: uma recusa de quando a dupla ainda não tinha o cinturão
    // não é fuga de defesa, é uma dupla qualquer dizendo que não pode jogar.
    public static List<Desafio> NaoAtendidosQueContam(
        ReinadoNoCinturao dono, IEnumerable<Desafio> desafiosDaCategoria, DateTime agora)
    {
        var chaveDoDono = ChaveDaDupla.De(dono.Jogador1Id, dono.Jogador2Id);
        var inicioDaJanela = agora - JanelaDaDefesa;

        return desafiosDaCategoria
            .Where(d => d.CategoriaPadraoId == dono.CategoriaPadraoId)
            .Where(d => ChaveDaDupla.De(d.DesafiadoJogador1Id, d.DesafiadoJogador2Id) == chaveDoDono)
            .Where(d => NaoAtendido(d, agora))
            .Where(d => QuandoNaoAtendeu(d) >= inicioDaJanela && QuandoNaoAtendeu(d) >= dono.ComecouEm)
            .OrderBy(QuandoNaoAtendeu)
            .ToList();
    }

    // Quem herda o cinturão quando o dono não defende: o PRIMEIRO que desafiou e não foi
    // atendido. Null = o dono ainda está dentro da conta.
    //
    // "O primeiro", e não "o último" nem "o melhor colocado": quem tentou antes esperou mais.
    public static Desafio? QuemHerdaPorOmissao(
        ReinadoNoCinturao dono, IEnumerable<Desafio> desafiosDaCategoria, DateTime agora)
    {
        var naoAtendidos = NaoAtendidosQueContam(dono, desafiosDaCategoria, agora);

        return naoAtendidos.Count >= NaoAtendidosQueCustamOCinturao ? naoAtendidos[0] : null;
    }

    // Quantas faltam pro dono perder. É o que a tela mostra pra ele ANTES de acontecer — perder
    // o cinturão sem nunca ter sido avisado seria uma regra escondida.
    public static int QuantasFaltamParaPerder(
        ReinadoNoCinturao dono, IEnumerable<Desafio> desafiosDaCategoria, DateTime agora) =>
        Math.Max(0, NaoAtendidosQueCustamOCinturao
            - NaoAtendidosQueContam(dono, desafiosDaCategoria, agora).Count);
}
