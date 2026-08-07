using padelizou.Models;

namespace Padelizou.Services;

// O catálogo de categorias que as telas podem OFERECER.
//
// Categoria desligada (CategoriaPadrao.Ativa = false) continua no banco e continua valendo
// onde já foi usada — o que ela perde é a vez de ser escolhida de novo. A regra mora aqui em
// vez de repetida em cada consulta porque são oito telas listando o mesmo catálogo, e uma
// delas esquecida é a categoria desligada reaparecendo num canto que ninguém olha.
public static class CatalogoDeCategorias
{
    public static IQueryable<CategoriaPadrao> Ativas(this IQueryable<CategoriaPadrao> catalogo)
        => catalogo.Where(c => c.Ativa);
}
