using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Cidades onde a dupla aceita jogar NESTE anúncio. Nenhuma linha = qualquer cidade.
//
// ⚠️ Aponta pro CATÁLOGO (Cidade), não pro texto livre do perfil. Quem monta a lista pra tela
// é Services/CidadesSemRepetir — "Gravataí", "GRAVATAI" e "Gravatai" são a mesma cidade e não
// podem virar três opções, nem três filtros que escondem gente um do outro.
[Table("AnuncioDesafioCidade")]
public class AnuncioDesafioCidade
{
    public int AnuncioDeDesafioId { get; set; }
    public int CidadeId { get; set; }

    [ForeignKey(nameof(AnuncioDeDesafioId))]
    public virtual AnuncioDeDesafio Anuncio { get; set; } = null!;

    [ForeignKey(nameof(CidadeId))]
    public virtual Cidade Cidade { get; set; } = null!;
}
