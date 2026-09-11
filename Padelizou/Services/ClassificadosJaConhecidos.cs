using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// QUEM JÁ **É** O "1º DO GRUPO B" — a colocação vira nome no instante em que o grupo fecha.
//
// 🗣️ Felipe, 11/09/2026, com o Grupo B encerrado e o Grupo A sem jogar: *"Avança sim, pq o grupo
// b esta definido, tem q por o 1º e 2º nas semifinais"* · *"sempre que um grupo finalizar, igual
// da foto, o Grupo B já está definido, então já pode mudar, na semifinal o 1º do B e o 2º do B, e
// use essa regra, para já ir alterando conforme o jogo for finalizando"*.
//
// 🕳️ O QUE A TELA FAZIA: escrevia a chave inteira por colocação até o ÚLTIMO jogo da CATEGORIA
// acabar. Com o Grupo B encerrado — três jogos, cada dupla com dois — "2º do Grupo B" já tem
// nome, sobrenome e foto no sistema, e o quadro continuava com a frase genérica. Quem acabou de
// classificar não se reconhecia no próprio caminho até a final.
//
// ⚠️ ISTO É RÓTULO, NÃO PARTIDA. O jogo da semifinal continua nascendo só quando os DOIS lados
// existem: `Partida.Dupla1Id`/`Dupla2Id` são NOT NULL, e vaga vazia no banco exigiria migration.
// Quem cria jogo é o RoboDoChaveamento; aqui só se responde quem ocupa a vaga.
//
// ⚠️ E POR GRUPO FECHADO, NUNCA POR JOGO SOLTO. No meio do grupo a classificação é provisória —
// a dupla em 1º com um jogo a menos cai pra 2º na rodada seguinte —, e trocar o nome a cada
// placar poria no quadro uma dupla que sai dele dez minutos depois. Fechado o grupo, a ordem dele
// não muda mais: é o único momento em que a promessa é firme.
//
// ⚠️ O QUE CONTINUA PROVISÓRIO é de qual LADO da chave cada um cai: a semeadura compara a
// campanha entre os grupos (ChaveamentoMataMata.MontarPrimeiraFase), então o 1º do B pode abrir a
// Semifinal 1 ou a 2 dependendo de como o Grupo A terminar. O aviso que a tela já dá — *"quem
// passa e de que lado da chave cai depende da campanha de cada dupla"* — continua valendo, e é
// por isso que ele não sai junto com esta mudança.
public static class ClassificadosJaConhecidos
{
    /// <summary>
    /// Nome da dupla que ocupa cada vaga JÁ DEFINIDA, por (nome do grupo, colocação).
    /// Grupo com jogo em aberto não entra — nenhuma vaga dele é firme ainda.
    /// </summary>
    /// <param name="grupos">Os grupos da categoria, com as duplas (basta o Id delas).</param>
    /// <param name="partidasDeGrupo">
    /// Os jogos de fase de grupos da categoria. Vêm com <c>Dupla1</c>/<c>Dupla2</c> carregados:
    /// é de lá que sai o nome de exibição, e não das duplas do grupo — quem carrega os grupos
    /// nem sempre traz os jogadores junto, e aí todo mundo viraria "Dupla 37".
    /// </param>
    /// <param name="vagasPorGrupo">Quantos classificam (ClassificacaoDeGrupos.VagasPorGrupo).</param>
    public static Dictionary<(string Grupo, int Posicao), string> De(
        IEnumerable<GrupoTorneio> grupos,
        IReadOnlyList<Partida> partidasDeGrupo,
        int vagasPorGrupo)
    {
        var conhecidos = new Dictionary<(string, int), string>();
        int passam = Math.Max(1, vagasPorGrupo);

        foreach (var grupo in grupos)
        {
            var idsDoGrupo = grupo.Duplas.Select(d => d.Id).ToHashSet();
            if (idsDoGrupo.Count == 0) continue;

            var jogosDoGrupo = partidasDeGrupo
                .Where(p => idsDoGrupo.Contains(p.Dupla1Id) && idsDoGrupo.Contains(p.Dupla2Id))
                .ToList();

            // Grupo sem jogo nenhum não decidiu nada — e grupo com jogo em quadra também não.
            if (jogosDoGrupo.Count == 0 || jogosDoGrupo.Any(p => p.Status != "Finalizada")) continue;

            // As duplas vêm dos próprios jogos: são as que trazem Jogador1/Jogador2 carregados.
            var duplas = jogosDoGrupo
                .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                .Where(d => d != null)
                .DistinctBy(d => d.Id)
                .ToList();
            if (duplas.Count == 0) continue;

            // A MESMA régua que monta a chave (Services/ClassificacaoDeGrupos.Ordenar). Um nome
            // que saísse de outra ordenação poria na vaga uma dupla diferente da que o robô vai
            // pôr — a tela prometendo um confronto que o sábado não faz.
            var ranking = ClassificacaoDeGrupos.Ordenar(duplas, jogosDoGrupo);

            for (int pos = 0; pos < ranking.Count && pos < passam; pos++)
                conhecidos[(grupo.Nome, pos + 1)] = ranking[pos].Dupla.NomeDeExibicao;
        }

        return conhecidos;
    }
}
