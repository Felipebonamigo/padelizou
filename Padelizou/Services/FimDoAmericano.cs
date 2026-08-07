using Padelizou.Models;

namespace Padelizou.Services;

// Como termina um Torneio Americano.
//
// A REGRA (Felipe, 06/08/2026): vence quem somou mais games. Só existe rodada final se DOIS OU
// MAIS empatarem na liderança — sem empate, o líder é campeão e ninguém joga mais nada.
//
// Isto aqui nasceu de um defeito achado testando o formato ponta a ponta: o robô montava
// sozinho uma "Final" cruzando os 4 primeiros, SEMPRE — mesmo sem empate e mesmo com a opção
// de desempate desligada. E aí o torneio terminava com DUAS respostas pra "quem ganhou":
//
//   • a tela de Classificação somava só as rodadas (fase "Americano N") e coroava o líder
//     em games;
//   • a conquista do perfil lia `Dupla.UltimaFase == "Campeao"`, que era carimbado em quem
//     vencia a tal Final.
//
// No ensaio, a Ana fez 56 games (a maior soma do torneio), apareceu em 1º na tabela e NÃO
// levou o título; o Fabio, 2º com 53, levou. Cada metade fazia certo o que fora programada
// pra fazer — o erro era existir uma final de mata-mata num formato que não tem final.
//
// Nenhuma regra nova mora aqui: quem diz se um empate PODE virar partida continua sendo
// TabelaDoAmericano.ProblemaParaDesempatar. Este arquivo só traduz a resposta dela em o que
// o robô deve fazer — de propósito, pra não virar uma segunda cópia da mesma régua.
public static class FimDoAmericano
{
    public enum Desfecho
    {
        // Líder isolado: é o campeão, e o torneio acaba aqui.
        CampeaoDireto,

        // Dois empatados e o torneio previu desempate: joga-se uma partida pelo título.
        PrecisaDesempate,

        // Empate que uma partida não resolve (três ou mais), ou torneio que não previu
        // desempate. O sistema NÃO inventa critério — quem decide é o organizador.
        OrganizadorDecide,
    }

    public record Decisao(
        Desfecho Tipo,
        Jogador? Campeao,
        IReadOnlyList<Jogador> Empatados,
        string? Recado);

    private static readonly IReadOnlyList<Jogador> Ninguem = Array.Empty<Jogador>();

    public static Decisao Decidir(
        IReadOnlyList<TabelaDoAmericano.Linha> classificacao, bool torneioPreveDesempate)
    {
        if (classificacao.Count == 0)
        {
            return new Decisao(Desfecho.OrganizadorDecide, null, Ninguem,
                "Nenhum jogo finalizado — não há classificação pra apurar.");
        }

        var empatados = TabelaDoAmericano.EmpatadosNaLideranca(classificacao);

        // Líder isolado é o caso normal, e é o único em que o sistema decide sozinho.
        if (empatados.Count < 2)
        {
            return new Decisao(Desfecho.CampeaoDireto, classificacao[0].Jogador, Ninguem, null);
        }

        // A partir daqui há empate. Quem diz se ele vira partida é a régua que já existia —
        // `rodadasPendentes: 0` porque este ponto só é alcançado com tudo jogado.
        var problema = TabelaDoAmericano.ProblemaParaDesempatar(
            torneioPreveDesempate, rodadasPendentes: 0, quantosEmpatados: empatados.Count);

        return problema == null
            ? new Decisao(Desfecho.PrecisaDesempate, null, empatados, null)
            : new Decisao(Desfecho.OrganizadorDecide, null, empatados, problema);
    }

    // O aviso que o organizador lê quando o título fica na mão dele. Cita os nomes: "houve
    // empate" sem dizer entre quem obriga a pessoa a ir caçar na tabela.
    public static string RecadoDoEmpate(IReadOnlyList<Jogador> empatados, string? motivo)
    {
        var nomes = string.Join(", ", empatados.Select(e => NomeBonito.Formatar(e.Nome)));
        return $"O Americano terminou empatado na liderança entre {nomes}. {motivo}".Trim();
    }
}
