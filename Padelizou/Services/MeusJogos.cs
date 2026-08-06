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
// ⚠️ Enquanto a categoria está na FASE DE GRUPOS não há como saber onde ele cai: a projeção
// fala em "1º do Grupo A", e quem vai ser o 1º ainda está em disputa. Aí o honesto é mostrar
// a chave inteira da categoria dele — é o caminho possível, e é o que ele quer ver. Quem
// decide isso é quem chama (`chaveAindaNaoComecou`), porque é lá que se sabe.
public static class MeusJogos
{
    // Um jogo real, reduzido ao que a regra precisa.
    // `Perdi` só vale pra jogo FINALIZADO — jogo em andamento ou agendado ainda pode ser meu.
    //
    // ⚠️ A CATEGORIA faz parte da identidade, não é enfeite: TODA categoria tem uma
    // "Semifinal 1". Sem ela na chave, ganhar as quartas da 5ª Masculina marcaria também a
    // semifinal da 6ª Feminina como "possível jogo seu".
    public record JogoReal(string Categoria, string Fase, int OrdemNaFase, bool SouEu, bool Perdi);

    // Quais jogos projetados podem ser do jogador.
    //
    // `byesComMeuNome`: os rótulos de bye que são dele (a dupla que folgou a primeira rodada
    // aparece pelo NOME na projeção, não por procedência).
    // `categoriasEmGrupos`: categorias dele cuja chave ainda não começou — nelas, tudo passa.
    public static List<ProximasFasesDaChave.JogoQueVem> Filtrar(
        IReadOnlyList<ProximasFasesDaChave.JogoQueVem> projetados,
        IReadOnlyList<JogoReal> jogosReais,
        IReadOnlyCollection<string> byesComMeuNome,
        IReadOnlyCollection<string> categoriasEmGrupos)
    {
        // O que "me leva adiante": a procedência de cada jogo meu que ainda não foi perdido.
        var alcancaveis = jogosReais
            .Where(j => j.SouEu && !j.Perdi)
            .Select(j => Procedencia(j.Categoria, j.Fase, j.OrdemNaFase))
            .ToHashSet();

        var meus = new List<ProximasFasesDaChave.JogoQueVem>();

        // Em ordem: a projeção já vem da rodada mais próxima pra mais distante, então quando
        // uma semifinal é marcada como minha, a final logo abaixo enxerga isso.
        foreach (var jogo in projetados)
        {
            bool ehMeu =
                categoriasEmGrupos.Contains(jogo.Categoria)
                || EhMeu(jogo.Categoria, jogo.Lado1, alcancaveis, byesComMeuNome)
                || EhMeu(jogo.Categoria, jogo.Lado2, alcancaveis, byesComMeuNome);

            if (!ehMeu) continue;

            meus.Add(jogo);
            alcancaveis.Add(Procedencia(jogo.Categoria, jogo.Fase, jogo.Numero));
        }

        return meus;
    }

    private static bool EhMeu(string categoria, ProximasFasesDaChave.Lado lado,
        HashSet<string> alcancaveis, IReadOnlyCollection<string> byesComMeuNome)
    {
        // Procedência desmontada: o lado aponta pra um jogo desta mesma chave — e "mesma
        // chave" quer dizer mesma CATEGORIA, que é a chave a que este jogo pertence.
        if (lado.DeQualFase != null && lado.DeQualNumero != null)
            return alcancaveis.Contains(Procedencia(categoria, lado.DeQualFase, lado.DeQualNumero.Value));

        // Sem procedência, o rótulo é um nome (bye) ou uma colocação de grupo ("2º do Grupo
        // C"). Só o nome pode ser dele — a colocação ainda não tem dono.
        return byesComMeuNome.Contains(lado.Rotulo);
    }

    // A chave que liga "quem sai deste jogo" a "quem entra no próximo". A projeção monta o
    // rótulo como "Vencedor Quartas de Final 1" e guarda fase e número desmontados; comparar
    // pelos dois campos evita depender do texto, que é de tela e muda.
    private static string Procedencia(string categoria, string fase, int numero) =>
        $"{categoria}|{fase}#{numero}";
}
