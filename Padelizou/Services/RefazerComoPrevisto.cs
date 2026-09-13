using Padelizou.Models;

namespace Padelizou.Services;

// QUANDO O BOTÃO "REFAZER COMO PREVISTO" TEM O QUE FAZER.
//
// Régua única das duas pontas: a AÇÃO (TorneiosController.RefazerMataMataComoPrevisto) e o
// PAINEL que oferece o botão na página do torneio. Nasceu porque as duas discordavam — o
// painel aparecia em toda categoria com chave publicada, inclusive nas que a ação recusaria
// e nas que já estavam certas.
//
// 🗣️ Felipe, 13/09/2026, com as 7 categorias do ER já conferidas e o painel em todas elas:
// *"acho que podemos ocultar isso agora que resolveu, não?"*. Ocultar de vez tiraria a saída
// de emergência; o que ele pediu, de fato, é que ele não apareça quando não há nada a fazer.
public static class RefazerComoPrevisto
{
    public enum Estado
    {
        SemDesenho,      // categoria sem mata-mata de grupos
        SoCongelar,      // o mata-mata ainda não existe, e o desenho não está congelado
        NadaAFazer,      // já congelado e sem chave, ou chave já idêntica ao previsto
        Recusado,        // alguma guarda impede — a ação diria o porquê
        PodeRefazer      // há jogo pra voltar ao previsto
    }

    public sealed record Mudanca(Partida Jogo, int Lado1, int Lado2);

    public sealed record Avaliacao(Estado Estado, string? Recusa, IReadOnlyList<Mudanca> Mudancas)
    {
        // O painel só se mostra quando o clique muda alguma coisa.
        public bool ValeMostrarOPainel => Estado is Estado.PodeRefazer or Estado.SoCongelar;
    }

    public static Avaliacao Avaliar(
        CruzamentoDoMataMata.Mapa? desenho,
        bool desenhoCongelado,
        IReadOnlyList<Partida> deGrupo,
        IReadOnlyList<Partida> doMataMata,
        IReadOnlyCollection<ChaveamentoMataMata.Classificado> classificados)
    {
        var nenhuma = (IReadOnlyList<Mudanca>)Array.Empty<Mudanca>();

        if (desenho == null)
            return new Avaliacao(Estado.SemDesenho,
                "esta categoria não tem mata-mata de grupos pra refazer.", nenhuma);

        // ⚠️ MATA-MATA QUE AINDA NÃO EXISTE NÃO É "NADA A FAZER" — é a hora de congelar. Mas
        // só enquanto o desenho estiver solto: o robô congela sozinho desde 12/09, e com ele
        // já gravado o clique não mudaria nada.
        if (doMataMata.Count == 0)
            return new Avaliacao(desenhoCongelado ? Estado.NadaAFazer : Estado.SoCongelar, null, nenhuma);

        // A classificação final precisa estar fechada: é dela que sai quem é "2º do Grupo C".
        if (deGrupo.Any(p => p.Status != "Finalizada"))
            return new Avaliacao(Estado.Recusado,
                "ainda tem jogo de grupo em aberto — a colocação final não está decidida.", nenhuma);

        // Só a ABERTURA. Se a chave já passou da primeira rodada, mexer nela reescreveria o
        // caminho de quem já venceu.
        var abertura = CruzamentoDoMataMata.NomeDaAbertura(desenho);
        if (doMataMata.Any(p => p.Fase != abertura))
            return new Avaliacao(Estado.Recusado,
                $"a chave já passou da {abertura.ToLowerInvariant()} — não dá mais pra refazer o cruzamento.",
                nenhuma);

        // A régua única de "a bola já rolou neste jogo" (Services/AprovacaoDeChaves).
        if (doMataMata.Any(p => p.Status != "Agendada" || p.HorarioInicioReal != null))
            return new Avaliacao(Estado.Recusado,
                "já tem jogo do mata-mata em andamento ou finalizado — refazer agora deixaria "
                + "dupla em dois jogos e dupla em nenhum.", nenhuma);

        if (doMataMata.Count != desenho.Confrontos.Count)
            return new Avaliacao(Estado.Recusado,
                $"a chave no ar tem {doMataMata.Count} jogo(s) e o previsto tem "
                + $"{desenho.Confrontos.Count} — os formatos não batem.", nenhuma);

        // Resolve o quadro INTEIRO antes de decidir qualquer coisa — o tudo-ou-nada da ação.
        var mudancas = new List<Mudanca>();
        for (int i = 0; i < desenho.Confrontos.Count; i++)
        {
            var confronto = desenho.Confrontos[i];
            if (CruzamentoDoMataMata.IdDaVaga(confronto.Lado1, classificados) is not int lado1
                || CruzamentoDoMataMata.IdDaVaga(confronto.Lado2, classificados) is not int lado2)
            {
                return new Avaliacao(Estado.Recusado,
                    $"não consegui dizer quem é {confronto.Lado1.Rotulo} ou {confronto.Lado2.Rotulo}. "
                    + "Nada foi mudado.", nenhuma);
            }

            var jogo = doMataMata[i];
            if (jogo.Dupla1Id == lado1 && jogo.Dupla2Id == lado2) continue;

            mudancas.Add(new Mudanca(jogo, lado1, lado2));
        }

        return mudancas.Count == 0
            ? new Avaliacao(Estado.NadaAFazer, null, nenhuma)
            : new Avaliacao(Estado.PodeRefazer, null, mudancas);
    }
}
