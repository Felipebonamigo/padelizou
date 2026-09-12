using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// QUEM JÁ CHEGOU AO CLUBE, uma linha por PESSOA (12/09/2026).
//
// 🗣️ Felipe: *"Mude para um check por jogador, por que é assim que controla check in"*. Até aqui
// a presença era `Dupla.CheckInEm` — uma coluna que respondia "a dupla apareceu", que é o que o
// W.O. precisa e não é o que a mesa faz no sábado: quem chega é uma pessoa por vez, e o
// organizador precisa saber QUAL dos dois falta pra ligar pra pessoa certa.
//
// ⚠️ A CHAVE É (TorneioId, JogadorId), E ISSO É METADE DO DESENHO: quem chegou ao clube chegou
// pro torneio INTEIRO. Quem joga 5ª Masculina e Mista faz UM check, e ele vale nas duas — com a
// presença pendurada na dupla, a mesma pessoa seria marcada duas vezes e as telas discordariam
// sobre ela estar no clube.
//
// ⚠️ LINHA EXISTE = CHEGOU. Não há `bool Presente`: desfazer é apagar a linha, e é a PK composta
// — não um `if` em C# — que segura o clique duplo no balcão. Mesmo molde do `TorneioMarcador`
// (escada do CLAUDE.md, degrau 4).
//
// ⚠️ `ChegouEm` é HORA LOCAL (`DateTime.Now`), como todo carimbo de tempo daqui: as colunas são
// `timestamp without time zone` e o fuso do VPS é America/Sao_Paulo.
[Table("PresencaNoTorneio")]
public class PresencaNoTorneio
{
    public int TorneioId { get; set; }
    public int JogadorId { get; set; }

    // A que horas apareceu — é o que a tela mostra em "chegou 08:12".
    public DateTime ChegouEm { get; set; } = DateTime.Now;

    public virtual Torneio Torneio { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
