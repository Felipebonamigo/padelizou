using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// QUEM ESTÁ AQUI PRA ESTE JOGO — uma linha por pessoa POR PARTIDA (12/09/2026).
//
// 🗣️ Felipe, horas depois de a chamada por pessoa entrar no ar: *"e o checkin, ele herda dos
// outros jogos pra mesma pessoa? pq se sim, nao deveria, tem q ser separado jogo a jogo"*.
//
// 🕳️ HERDAVA — e estava escrito aqui como decisão. A chave era `(TorneioId, JogadorId)`, com o
// argumento de que *"quem chegou ao clube chegou pro torneio INTEIRO"*. O sábado desmentiu: quem
// joga 5ª Masculina às 15:30 e Mista às 19:00 aparecia verde nas duas assim que marcasse a
// primeira, e o organizador das 19:00 lia "todos presentes" para gente que tinha ido embora.
//
// ⚠️ A CHAVE É (PartidaId, JogadorId), e o check responde UMA pergunta: "esta pessoa está aqui
// pra ESTE jogo?". A mesma pessoa marca uma vez por jogo dela — que é mais trabalho no balcão e é
// o que o Felipe pediu, porque é o único jeito de a resposta ser verdadeira.
//
// ⚠️ É TAMBÉM O QUE SEGURA A ORDEM POR PRESENÇA (Services/OrdemNoHorario): com a herança, um jogo
// das 19:00 subiria pro topo do horário porque os quatro marcaram nos jogos da tarde.
//
// ⚠️ LINHA EXISTE = CHEGOU. Não há `bool Presente`: desfazer é apagar a linha, e é a PK composta
// — não um `if` em C# — que segura o clique duplo no balcão. Mesmo molde do `TorneioMarcador`
// (escada do CLAUDE.md, degrau 4).
//
// ⚠️ CASCADE NA PARTIDA, de propósito: refazer a grade apaga partidas, e o check delas vai junto.
// Check de um jogo que não existe mais não quer dizer nada — e sem o cascade o "Refazer grade"
// passaria a estourar por causa de uma tabela que ninguém está olhando.
//
// ⚠️ `ChegouEm` é HORA LOCAL (`DateTime.Now`), como todo carimbo de tempo daqui: as colunas são
// `timestamp without time zone` e o fuso do VPS é America/Sao_Paulo.
[Table("PresencaNoJogo")]
public class PresencaNoJogo
{
    public int PartidaId { get; set; }
    public int JogadorId { get; set; }

    // A que horas apareceu — é o que a tela mostra em "chegou 08:12".
    public DateTime ChegouEm { get; set; } = DateTime.Now;

    public virtual Partida Partida { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
