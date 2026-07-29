using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.ViewModels;

// Busca de jogadores com filtros combináveis. Serve pra achar parceiro: "quem é 3ª
// masculina, joga em Caxias e frequenta o Arena?".
public class BuscaJogadoresVM
{
    // ----- o que foi pedido -----
    // Aceita nome, apelido OU CPF — quem decide é Services/BuscaJogador.
    public string? Termo { get; set; }
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

    // Paginação: sem filtro a busca lista TODO MUNDO, e "todo mundo" cresce — a página
    // protege a tela e o banco ao mesmo tempo. 30 por página: cabe numa rolada de celular.
    public const int TamanhoDaPagina = 30;
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;

    // A busca filtrou por categoria e/ou clube (as que dependem de o jogador ter
    // declarado a preferência), e quantos de fato declararam.
    public bool FiltraPreferencia { get; set; }
    public int QtdDeclarou { get; set; }

    // Nenhum filtro preenchido: a tela mostra o convite em vez de "nada encontrado".
    public bool TemFiltro =>
        !string.IsNullOrWhiteSpace(Termo) || CategoriaId != null
        || !string.IsNullOrWhiteSpace(Estado) || !string.IsNullOrWhiteSpace(Cidade) || ClubeId != null;
}

public class JogadorEncontradoVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public string? Time { get; set; }
    public List<string> Categorias { get; set; } = new();
    public List<string> Clubes { get; set; } = new();

    // O jogador DECLAROU a categoria/clube filtrado, em vez de apenas "não ter dito nada".
    // Quem declarou aparece primeiro e ganha selo — sem isso o filtro seria inócuo,
    // porque quase ninguém preenche preferência e todo mundo entraria no resultado.
    public bool Declarou { get; set; }
}
