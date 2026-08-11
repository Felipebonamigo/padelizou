using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Clubes onde a dupla aceita jogar NESTE anúncio. Nenhuma linha = qualquer clube.
// Espelha JogadorClube, congelado na semana anunciada.
[Table("AnuncioDesafioClube")]
public class AnuncioDesafioClube
{
    public int AnuncioDeDesafioId { get; set; }
    public int ClubeId { get; set; }

    [ForeignKey(nameof(AnuncioDeDesafioId))]
    public virtual AnuncioDeDesafio Anuncio { get; set; } = null!;

    [ForeignKey(nameof(ClubeId))]
    public virtual Clube Clube { get; set; } = null!;
}
