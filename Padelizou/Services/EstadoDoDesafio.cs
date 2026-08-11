using Padelizou.Models;

namespace Padelizou.Services;

// O que dá pra fazer com um desafio AGORA — e quem pode fazer.
//
// Toda a máquina de estados mora aqui, e não espalhada em `if`s do controller, porque são
// quatro pessoas mexendo na mesma linha por caminhos diferentes (mural, caixa de avisos, link
// do WhatsApp) e a mesma pergunta é feita na tela e no POST. Regra em dois lugares diverge; e
// numa que decide "esse placar conta?" divergir significa ranking errado.
//
// ⚠️ DOIS PRAZOS, E OS DOIS SÃO LIDOS DO RELÓGIO:
//
//  · 48h pra RESPONDER a proposta. Sem resposta, a proposta morre — e morrer aqui é sumir da
//    tela, não virar linha "Expirado" gravada por job.
//  · 72h pra CONTESTAR o placar lançado. Sem resposta, o placar VALE.
//
// O silêncio confirmar é decisão consciente: exigir o "sim" do perdedor faria de SUMIR a
// jogada ótima — bastaria não responder pra congelar o ranking de quem te ganhou.
//
// ⚠️ Quem materializa a confirmação no banco (gravando status e congelando os pontos) é
// FechamentoDeDesafiosBackgroundService. A tela NÃO depende dele: ela lê o relógio pelo
// `StatusNaTela`. É de propósito — job que morre calado não pode fazer a tela mentir.
public static class EstadoDoDesafio
{
    public static readonly TimeSpan PrazoParaResponder = TimeSpan.FromHours(48);
    public static readonly TimeSpan PrazoParaContestar = TimeSpan.FromHours(72);

    // ── O que o relógio já decidiu ─────────────────────────────────────────────────────

    public static bool PropostaVencida(Desafio desafio, DateTime agora) =>
        desafio.Status == Desafio.Proposto && agora > desafio.PropostoEm + PrazoParaResponder;

    public static bool PlacarValeuPeloRelogio(Desafio desafio, DateTime agora) =>
        desafio.Status == Desafio.PlacarLancado
        && desafio.LancadoEm != null
        && agora > desafio.LancadoEm.Value + PrazoParaContestar;

    // O status que a pessoa lê. É o gravado, corrigido pelo relógio nos dois casos acima.
    public static string StatusNaTela(Desafio desafio, DateTime agora)
    {
        if (PropostaVencida(desafio, agora)) return "Sem resposta";
        if (PlacarValeuPeloRelogio(desafio, agora)) return Desafio.Confirmado;
        return desafio.Status;
    }

    // Quanto falta pra outra dupla perder a vez. É o que dá urgência ao cartão sem inventar
    // pressão: "faltam 9h pra responder" é fato, "responda logo!" é chateação.
    public static TimeSpan? TempoRestanteParaResponder(Desafio desafio, DateTime agora)
    {
        if (desafio.Status != Desafio.Proposto) return null;
        var resta = desafio.PropostoEm + PrazoParaResponder - agora;
        return resta > TimeSpan.Zero ? resta : null;
    }

    public static TimeSpan? TempoRestanteParaContestar(Desafio desafio, DateTime agora)
    {
        if (desafio.Status != Desafio.PlacarLancado || desafio.LancadoEm == null) return null;
        var resta = desafio.LancadoEm.Value + PrazoParaContestar - agora;
        return resta > TimeSpan.Zero ? resta : null;
    }

    // ── Quem pode o quê ────────────────────────────────────────────────────────────────

    public static bool EhDoLadoDesafiante(Desafio desafio, int jogadorId) =>
        desafio.LadoDesafiante.Contains(jogadorId);

    public static bool EhDoLadoDesafiado(Desafio desafio, int jogadorId) =>
        desafio.LadoDesafiado.Contains(jogadorId);

    public static bool EstaEnvolvido(Desafio desafio, int jogadorId) =>
        desafio.Envolvidos.Contains(jogadorId);

    // Aceitar ou recusar: só a dupla desafiada, só enquanto a proposta está de pé.
    public static bool PodeResponder(Desafio desafio, int jogadorId, DateTime agora) =>
        desafio.Status == Desafio.Proposto
        && !PropostaVencida(desafio, agora)
        && EhDoLadoDesafiado(desafio, jogadorId);

    // Desistir. Vale pros dois lados enquanto o jogo não aconteceu: quem propôs pode voltar
    // atrás, e quem aceitou pode furar — o que não pode é o furo ser silencioso, e por isso
    // cancelamento gera aviso pro outro lado.
    public static bool PodeCancelar(Desafio desafio, int jogadorId) =>
        desafio.Status is Desafio.Proposto or Desafio.Aceito
        && EstaEnvolvido(desafio, jogadorId);

    // ⚠️ SÓ DEPOIS DE O JOGO COMEÇAR. Placar lançado às 9h pra um jogo das 20h não é placar: é
    // chute, ou é engano de quem clicou na linha errada da lista. Qualquer um dos quatro pode
    // lançar — quem estava com o celular na mão.
    public static bool PodeLancarPlacar(Desafio desafio, int jogadorId, DateTime agora) =>
        desafio.Status == Desafio.Aceito
        && agora >= desafio.DataHora
        && EstaEnvolvido(desafio, jogadorId);

    // Confirmar ou contestar: só o lado OPOSTO ao de quem lançou, e só dentro do prazo.
    // Depois do prazo o placar já valeu — deixar contestar ali reabriria jogo fechado.
    public static bool PodeResponderPlacar(Desafio desafio, int jogadorId, DateTime agora)
    {
        if (desafio.Status != Desafio.PlacarLancado) return false;
        if (PlacarValeuPeloRelogio(desafio, agora)) return false;
        if (desafio.LancadoPorId == null) return false;

        // Quem lançou não confirma o próprio placar: seria a dupla dela assinando sozinha.
        var lancouDoLadoDesafiante = EhDoLadoDesafiante(desafio, desafio.LancadoPorId.Value);
        return lancouDoLadoDesafiante
            ? EhDoLadoDesafiado(desafio, jogadorId)
            : EhDoLadoDesafiante(desafio, jogadorId);
    }

    // Marcar que alguém não apareceu. Fica com quem foi — o outro lado não vai registrar a
    // própria falta. Só depois da hora marcada, pelo mesmo motivo do placar.
    public static bool PodeRegistrarFalta(Desafio desafio, int jogadorId, DateTime agora) =>
        desafio.Status == Desafio.Aceito
        && agora >= desafio.DataHora
        && EstaEnvolvido(desafio, jogadorId);

    // ── O vencedor ─────────────────────────────────────────────────────────────────────

    // 1 = desafiante, 2 = desafiado, null = empate ou sem placar. A régua é a MESMA da partida
    // de torneio (sets mandam, games desempatam) — ver QuemVenceu.Lado, que existe pra este
    // arquivo não recriar o `4 > null` que já custou uma final com campeão errado.
    public static int? LadoVencedor(Desafio desafio) =>
        QuemVenceu.Lado(desafio.SetsDesafiante, desafio.SetsDesafiado,
            desafio.GamesDesafiante, desafio.GamesDesafiado);

    // Por que este placar não pode ser lançado. Null = pode.
    public static string? MotivoParaNaoLancar(int? sets1, int? sets2, int? games1, int? games2)
    {
        if (games1 == null && games2 == null && sets1 == null && sets2 == null)
            return "Marque o placar — sem números não dá pra dizer quem venceu.";

        return QuemVenceu.Lado(sets1, sets2, games1, games2) == null
            ? "O placar está empatado — no padel alguém tem que fechar o jogo. Ajuste e lance de novo."
            : null;
    }
}
