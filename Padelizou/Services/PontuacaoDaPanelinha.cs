using Padelizou.Models;

namespace Padelizou.Services;

// A conta de pontos do jogo de panelinha — vitória 3, derrota 1 (participação), empate 2 pra cada.
//
// ⚠️ POR QUE ISSO SAIU DE DENTRO DO CONTROLLER: o mesmo fato (um JogoSemanal) alimenta DOIS
// rankings que não são calculados do mesmo jeito. O do mês e o da semana são refeitos na hora, a
// partir dos jogos; o ranking GERAL mora gravado em `JogadorGrupo.PontuacaoInterna`, somado de
// pouco em pouco a cada jogo registrado. Enquanto só existia "registrar", os dois batiam por
// sorte. No momento em que passa a existir EDITAR e APAGAR, um total somado à mão fica com
// pontos-fantasma pra sempre: some o jogo da lista e a pessoa continua pontuada por ele.
//
// A saída é não ter duas contas. `Totais` é a única régua, e o ranking gravado é REFEITO a partir
// dela — nunca ajustado por diferença. Refazer também cura sozinho a distorção que já existir no
// banco, que somar/subtrair não curaria.
public static class PontuacaoDaPanelinha
{
    public const int PontosPorVitoria = 3;
    public const int PontosPorDerrota = 1;
    public const int PontosPorEmpate = 2;

    public static void Aplicar(Dictionary<int, int> pontos, JogoSemanal jogo)
    {
        int pontosDupla1, pontosDupla2;
        if (jogo.VencedorLado == 1) { pontosDupla1 = PontosPorVitoria; pontosDupla2 = PontosPorDerrota; }
        else if (jogo.VencedorLado == 2) { pontosDupla1 = PontosPorDerrota; pontosDupla2 = PontosPorVitoria; }
        else { pontosDupla1 = PontosPorEmpate; pontosDupla2 = PontosPorEmpate; }

        Somar(pontos, jogo.Dupla1Jogador1Id, pontosDupla1);
        Somar(pontos, jogo.Dupla1Jogador2Id, pontosDupla1);
        Somar(pontos, jogo.Dupla2Jogador1Id, pontosDupla2);
        Somar(pontos, jogo.Dupla2Jogador2Id, pontosDupla2);
    }

    // O total de cada jogador a partir de uma lista de jogos. Quem não aparece em jogo nenhum
    // não vem no dicionário — quem lê precisa tratar isso como ZERO, e não como "não mexer",
    // senão o último jogo apagado de alguém deixa o placar velho parado na tela.
    public static Dictionary<int, int> Totais(IEnumerable<JogoSemanal> jogos)
    {
        var pontos = new Dictionary<int, int>();
        foreach (var jogo in jogos) Aplicar(pontos, jogo);
        return pontos;
    }

    // ⚠️ NULO = CONVIDADO SEM NOME, E ELE NÃO PONTUA. É a mesma regra de 13/08/2026 que já valia
    // pro avulso com conta (ver GruposController.ParticipantesParaResultadoAsync), levada ao
    // limite lógico: sem identidade não há ranking possível. Os outros três recebem inteiro.
    //
    // ⚠️ NUNCA `?? 0` AQUI. Zero é um id válido de dicionário: os pontos de TODOS os convidados
    // de TODAS as panelinhas cairiam na mesma chave 0. Não vazaria pro ranking (nenhum
    // JogadorGrupo.JogadorId é 0), mas envenenaria qualquer leitura futura de `Totais` — e
    // ninguém acharia a causa, porque a soma continua "batendo" com ela mesma.
    private static void Somar(Dictionary<int, int> dict, int? id, int pts)
    {
        if (id == null) return;
        dict[id.Value] = dict.GetValueOrDefault(id.Value) + pts;
    }
}
