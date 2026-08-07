using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("CategoriaPadrao")]
public partial class CategoriaPadrao
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Tipo { get; set; } = null!;

    // A categoria continua no catálogo, mas some das telas de escolha. É o único jeito de
    // tirar uma categoria de circulação sem apagar a linha: torneio antigo, preferência de
    // jogador e aviso guardam o Id dela, e apagar deixaria tudo isso órfão.
    //
    // Ligar de volta é um UPDATE de uma linha — não precisa de deploy.
    public bool Ativa { get; set; } = true;
}