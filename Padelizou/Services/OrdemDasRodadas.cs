using Padelizou.Models;

namespace Padelizou.Services;

// A ordem em que os jogos de GRUPO chegam ao encaixe.
//
// ⚠️ Existe por causa de um relato do Felipe (07/09/2026), olhando a grade do torneio do Er:
// *"tem jogos seguidos dos mesmos jogadores, isso temos q evitar"*. Medido no dev, num torneio
// gerado pelo app: 20 casos de dupla voltando à quadra no horário seguinte, em 31 jogos.
//
// A causa era a ordem de nascimento. Os jogos saíam grupo por grupo:
//
//     Grupo A: a×b, a×c, b×c      ← três jogos seguidos entre as MESMAS três duplas
//     Grupo B: d×e, d×f, e×f
//
// O encaixe é um guloso de primeira vaga: ele preserva a ordem que recebe. Recebendo assim, a
// dupla `a` joga, e o próximo jogo da fila é dela de novo.
//
// Intercalado por rodada, a mesma fila vira:
//
//     rodada 1: a×b, d×e, ...     ← um jogo de cada grupo
//     rodada 2: a×c, d×f, ...
//     rodada 3: b×c, e×f, ...
//
// e entre dois jogos de `a` passam tantos jogos quantos forem os outros grupos.
//
// ⚠️ POR QUE AQUI E NÃO NO ENCAIXE. Fazer o guloso escolher "quem descansou mais" foi tentado
// TRÊS vezes (duas em 08/08, uma em 07/09) e medido PIOR nas três — reordenar por descanso
// empurra os cansados pro fim, onde só sobram eles, e a pior espera estoura. O cabeçalho de
// DescansoNaGradeTests conta as duas primeiras; a terceira reprovou com pior espera 7 (teto 6).
//
// O mesmo arquivo diz por que o Americano nunca teve o problema: a fila dele JÁ CHEGA em ordem
// de rodada, montada pelo RodadasAmericano. Isto aqui é dar à fase de grupos a mesma coisa —
// na origem, e não no guloso.
public static class OrdemDasRodadas
{
    // Reordena SÓ os jogos de fase de grupos, intercalando os grupos por rodada. Todo o resto
    // (chave direta, mata-mata) fica exatamente onde estava.
    //
    // ⚠️ As POSIÇÕES da lista não mudam: um jogo de grupo continua ocupando o lugar de um jogo
    // de grupo. Só troca QUAL deles está em cada uma. Assim a separação por fase que o
    // `OrdemDaFila` acabou de fazer — chave direta abrindo, mata-mata no fim — sobrevive
    // intacta, e esta função não precisa saber nada sobre ela.
    // Quantos grupos entram no mesmo entrelace, dado o número de quadras.
    //
    // ⚠️ ESTE NÚMERO É O EQUILÍBRIO ENTRE AS DUAS REGRAS QUE SE OPÕEM. Entrelaçar TODOS os
    // grupos zera as emendas e estoura a espera: medido em 07/09/2026, num torneio de 56 duplas
    // em 2 quadras, a pior espera saltou de 2 pra 18 horários — a dupla jogava a 1ª rodada no
    // começo e a 3ª no fim do dia. Trocar "joga duas seguidas" por "senta quinze" não é conserto.
    //
    // A conta: entre dois jogos do mesmo grupo entram os jogos dos outros (N−1) grupos do bloco,
    // que ocupam (N−1)/quadras horários. Pra isso dar os `HorariosDeDescanso` pedidos:
    //
    //     (N − 1) / quadras >= descanso   →   N >= quadras * descanso + 1
    //
    // Com 2 quadras e descanso 2, são 5 grupos por bloco: o bastante pra ninguém voltar no
    // horário seguinte, e pouco o bastante pra ninguém atravessar o torneio esperando.
    public static int GruposPorBloco(int quadras) =>
        Math.Max(quadras, 1) * GradeDeJogos.HorariosDeDescanso + 1;

    public static List<Partida> IntercalarFaseDeGrupos(IReadOnlyList<Partida> jogos, int quadras)
    {
        // Ordem de primeira aparição, não a do dicionário: grade de torneio não pode depender
        // da ordem em que o .NET resolve resolver a enumeração de um Dictionary.
        var grupos = new List<List<Partida>>();
        var ondeEstaOGrupo = new Dictionary<(int Categoria, string Fase), int>();
        var posicoes = new List<int>();

        for (int i = 0; i < jogos.Count; i++)
        {
            var jogo = jogos[i];
            if (!FasesTorneio.EhFaseDeGrupos(jogo.Fase)) continue;

            posicoes.Add(i);

            var chave = (jogo.CategoriaId, jogo.Fase);
            if (!ondeEstaOGrupo.TryGetValue(chave, out var indice))
            {
                ondeEstaOGrupo[chave] = indice = grupos.Count;
                grupos.Add(new List<Partida>());
            }
            grupos[indice].Add(jogo);
        }

        // Um grupo só (ou nenhum) não tem com o que intercalar — e é o caso em que a régua não
        // pode fazer nada mesmo: três jogos entre as mesmas três duplas.
        if (grupos.Count <= 1) return jogos.ToList();

        // Em BLOCOS, não tudo de uma vez: ver GruposPorBloco. Cada bloco é entrelaçado inteiro
        // antes de o próximo começar, então a dupla joga as três partidas dela dentro de uma
        // vizinhança do dia, em vez de uma no começo e outra no fim.
        var intercalados = new List<Partida>(posicoes.Count);

        foreach (var bloco in grupos.Chunk(GruposPorBloco(quadras)))
        {
            int maiorDoBloco = bloco.Max(g => g.Count);

            for (int rodada = 0; rodada < maiorDoBloco; rodada++)
                foreach (var grupo in bloco)
                    if (rodada < grupo.Count)
                        intercalados.Add(grupo[rodada]);
        }

        var resultado = jogos.ToList();
        for (int k = 0; k < posicoes.Count; k++)
            resultado[posicoes[k]] = intercalados[k];

        return resultado;
    }
}
