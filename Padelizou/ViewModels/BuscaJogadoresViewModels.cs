using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.ViewModels;

// Busca de jogadores com filtros combináveis. Serve pra achar parceiro: "quem é 3ª
// masculina, joga em Caxias e frequenta o Arena?".
public class BuscaJogadoresVM
{
    // ----- o que foi pedido -----
    public string? Nome { get; set; }
    public int? CategoriaId { get; set; }
    public string? Estado { get; set; }
    public string? Cidade { get; set; }
    public int? ClubeId { get; set; }

    // ----- opções pra montar os selects -----
    public List<CategoriaPadrao> Categorias { get; set; } = new();
    public List<string> Estados { get; set; } = new();
    public List<string> Cidades { get; set; } = new();
    public List<Clube> Clubes { get; set; } = new();

    // ----- resultado -----
    public List<JogadorEncontradoVM> Resultados { get; set; } = new();
    public int TotalEncontrado { get; set; }
    public bool Truncado => TotalEncontrado > Resultados.Count;

    // Nenhum filtro preenchido: a tela mostra o convite em vez de "nada encontrado".
    public bool TemFiltro =>
        !string.IsNullOrWhiteSpace(Nome) || CategoriaId != null
        || !string.IsNullOrWhiteSpace(Estado) || !string.IsNullOrWhiteSpace(Cidade) || ClubeId != null;
}

public class JogadorEncontradoVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public string? Time { get; set; }
    public List<string> Categorias { get; set; } = new();
    public List<string> Clubes { get; set; } = new();
}
