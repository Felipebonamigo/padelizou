using Padelizou.Services;

namespace Padelizou.ViewModels;

public class SolicitarViewModel
{
    // `CidadeNaLista` e não `Cidade`: o que vai pro select é a cidade já agrupada, uma opção
    // por cidade de verdade, mesmo que o catálogo tenha duas linhas escritas diferente.
    public List<CidadeNaLista> Cidades { get; set; } = new();

    // As UFs que têm professor. O estado é um FUNIL, não um dado novo: ele já vem em cada
    // cidade — a lista existe só pra montar o primeiro select sem repetir a conta na view.
    //
    // ⚠️ Cidade sem UF (o campo é opcional no cadastro do professor) não some da tela: ela
    // aparece com o estado em branco e continua visível enquanto nenhuma UF for escolhida.
    public List<string> Estados => Cidades
        .Select(c => c.Estado)
        .Where(uf => !string.IsNullOrEmpty(uf))
        .Distinct()
        .OrderBy(uf => uf, StringComparer.Ordinal)
        .ToList()!;

    // Tem cidade órfã de UF? Só então vale oferecer a opção "sem estado" — inventar um item
    // "Outras" numa lista onde todo mundo tem UF é passo a mais sem escolha nenhuma.
    public bool TemCidadeSemEstado => Cidades.Any(c => string.IsNullOrEmpty(c.Estado));
}
