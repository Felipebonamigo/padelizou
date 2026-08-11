using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Categorias que a dupla aceita enfrentar NESTE anúncio. Nenhuma linha = sem restrição.
// Espelha JogadorCategoria de propósito — é a mesma pergunta, congelada na semana anunciada.
[Table("AnuncioDesafioCategoria")]
public class AnuncioDesafioCategoria
{
    public int AnuncioDeDesafioId { get; set; }
    public int CategoriaPadraoId { get; set; }

    [ForeignKey(nameof(AnuncioDeDesafioId))]
    public virtual AnuncioDeDesafio Anuncio { get; set; } = null!;

    [ForeignKey(nameof(CategoriaPadraoId))]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;
}
