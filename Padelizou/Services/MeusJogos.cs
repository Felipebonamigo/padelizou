namespace Padelizou.Services;

// "MOSTRE SÓ OS MEUS JOGOS" — inclusive os que ainda não existem.
//
// Num torneio de 86 jogos o jogador rola a lista inteira pra achar os dele. Filtrar os jogos
// REAIS é trivial (ele está numa das duas duplas). O que dá trabalho — e é o que ele pediu —
// são os jogos que ainda não existem: "se eu ganhar este, jogo qual, a que horas?".
//
// A projeção (Services/ProximasFasesDaChave) descreve os lados por PROCEDÊNCIA: "Vencedor
// Quartas de Final 1". Então "este jogo pode ser meu" se resolve seguindo a corrente:
//
//   1. todo jogo REAL meu que ainda pode me levar adiante entrega um "Vencedor <fase> <n>";
//   2. quem pegou BYE entrega o próprio nome;
//   3. um jogo projetado é meu se algum lado dele cita algo alcançável — e, sendo meu, ele
//      passa a entregar "Vencedor <fase dele> <n dele>", que alimenta a rodada seguinte.
//
// ⚠️ Jogo que EU PERDI não entrega nada. Sem isso a corrente não pararia nunca e o eliminado
// veria a final como "possível jogo dele" — a tela viraria mentira justo pra quem já está
// triste.
//
// ⚠️ Enquanto a categoria está na FASE DE GRUPOS a projeção fala em "1º do Grupo A", e quem
// vai ser o 1º ainda está em disputa — mas O GRUPO DELE JÁ SE SABE, e é ele que recorta a
// chave. Quem está no Grupo A pode terminar em 1º ou em 2º e cair nas DUAS vagas do Grupo A;
// a oitava entre "1º do Grupo E" e "2º do Grupo F" não é caminho dele por nenhum resultado.
// A primeira versão mostrava a chave inteira da categoria e o Felipe reclamou (09/09/2026):
// eram 12 jogos numa tela cujo botão promete só os dele.
public static class MeusJogos
{
    // Um jogo real, reduzido ao que a regra precisa.
    // `Perdi` só vale pra jogo FINALIZADO — jogo em andamento ou agendado ainda pode ser meu.
    //
    // ⚠️ A CATEGORIA faz parte da identidade, não é enfeite: TODA categoria tem uma
    // "Semifinal 1". Sem ela na chave, ganhar as quartas da 5ª Masculina marcaria também a
    // semifinal da 6ª Feminina como "possível jogo seu".
    public record JogoReal(string Categoria, string Fase, int OrdemNaFase, bool SouEu, bool Perdi);

    // Onde o jogador está numa categoria que ainda não saiu dos grupos. `Grupo` nulo é a
    // dupla sem grupo sorteado — não dá pra dizer por onde ele entra, e aí a chave inteira
    // da categoria volta a ser a resposta honesta.
    public record VagaNosGrupos(string Categoria, string? Grupo);

    // Quais jogos projetados podem ser do jogador.
    //
    // `byesComMeuNome`: os rótulos de bye que são dele (a dupla que folgou a primeira rodada
    // aparece pelo NOME na projeção, não por procedência).
    // `minhasVagasNosGrupos`: em que grupo ele está, nas categorias dele cuja chave ainda não
    // começou.
    public static List<ProximasFasesDaChave.JogoQueVem> Filtrar(
        IReadOnlyList<ProximasFasesDaChave.JogoQueVem> projetados,
        IReadOnlyList<JogoReal> jogosReais,
        IReadOnlyCollection<string> byesComMeuNome,
        IReadOnlyCollection<VagaNosGrupos> minhasVagasNosGrupos)
    {
        // O que "me leva adiante": a procedência de cada jogo meu que ainda não foi perdido.
        var alcancaveis = jogosReais
            .Where(j => j.SouEu && !j.Perdi)
            .Select(j => Procedencia(j.Categoria, j.Fase, j.OrdemNaFase))
            .ToHashSet();

        // As vagas que podem ser dele: TODAS as colocações do grupo dele — 1º e 2º saem em
        // lados opostos do quadro, e ele ainda não sabe qual vai ser.
        var minhasVagas = minhasVagasNosGrupos
            .Where(v => v.Grupo != null)
            .Select(v => Vaga(v.Categoria, v.Grupo!))
            .ToHashSet();

        // Sem grupo sorteado não dá pra dizer por onde ele entra; aí a chave inteira da
        // categoria volta a ser a resposta honesta.
        var categoriasSemGrupo = minhasVagasNosGrupos
            .Where(v => v.Grupo == null)
            .Select(v => v.Categoria)
            .ToHashSet();

        var meus = new List<ProximasFasesDaChave.JogoQueVem>();

        // Em ordem: a projeção já vem da rodada mais próxima pra mais distante, então quando
        // uma semifinal é marcada como minha, a final logo abaixo enxerga isso.
        foreach (var jogo in projetados)
        {
            bool ehMeu =
                categoriasSemGrupo.Contains(jogo.Categoria)
                || EhMeu(jogo.Categoria, jogo.Lado1, alcancaveis, byesComMeuNome, minhasVagas)
                || EhMeu(jogo.Categoria, jogo.Lado2, alcancaveis, byesComMeuNome, minhasVagas);

            if (!ehMeu) continue;

            meus.Add(jogo);
            alcancaveis.Add(Procedencia(jogo.Categoria, jogo.Fase, jogo.Numero));
        }

        return meus;
    }

    private static bool EhMeu(string categoria, ProximasFasesDaChave.Lado lado,
        HashSet<string> alcancaveis, IReadOnlyCollection<string> byesComMeuNome,
        IReadOnlyCollection<string> minhasVagas)
    {
        // Procedência desmontada: o lado aponta pra um jogo desta mesma chave — e "mesma
        // chave" quer dizer mesma CATEGORIA, que é a chave a que este jogo pertence.
        if (lado.DeQualFase != null && lado.DeQualNumero != null)
            return alcancaveis.Contains(Procedencia(categoria, lado.DeQualFase, lado.DeQualNumero.Value));

        // Colocação de grupo ("2º do Grupo C"): ainda não tem dono, mas tem GRUPO — e o grupo
        // já basta pra dizer que não é dele. A categoria entra na conta porque toda categoria
        // tem um "Grupo A".
        if (lado.DeQualGrupo != null)
            return minhasVagas.Contains(Vaga(categoria, lado.DeQualGrupo));

        // Sem procedência e sem grupo, o rótulo é um nome: o bye da chave que já começou.
        return byesComMeuNome.Contains(lado.Rotulo);
    }

    // A chave que liga "quem sai deste jogo" a "quem entra no próximo". A projeção monta o
    // rótulo como "Vencedor Quartas de Final 1" e guarda fase e número desmontados; comparar
    // pelos dois campos evita depender do texto, que é de tela e muda.
    private static string Procedencia(string categoria, string fase, int numero) =>
        $"{categoria}|{fase}#{numero}";

    // O mesmo, pra vaga de grupo: "5ª Masculina|Grupo A". Sem a categoria, estar no Grupo A
    // de uma marcaria as oitavas do Grupo A de TODAS as outras.
    private static string Vaga(string categoria, string grupo) => $"{categoria}|{grupo}";
}
