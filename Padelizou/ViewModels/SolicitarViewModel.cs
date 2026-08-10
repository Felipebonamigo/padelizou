using Padelizou.Services;

namespace Padelizou.ViewModels;

public class SolicitarViewModel
{
    // `CidadeNaLista` e não `Cidade`: o que vai pro select é a cidade já agrupada, uma opção
    // por cidade de verdade, mesmo que o catálogo tenha duas linhas escritas diferente.
    public List<CidadeNaLista> Cidades { get; set; } = new();
}
