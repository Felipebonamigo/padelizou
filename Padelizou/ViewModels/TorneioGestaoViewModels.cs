using Padelizou.Models;

namespace Padelizou.ViewModels;

// Financeiro do torneio numa tela só, quebrado por categoria — hoje o organizador
// precisa cruzar Pagamentos/Meus com a lista de inscritos pra saber onde está o dinheiro.
public class FinanceiroTorneioVM
{
    public Torneio Torneio { get; set; } = null!;

    public decimal Arrecadado { get; set; }
    public decimal Pendente { get; set; }
    public decimal Estornado { get; set; }
    public decimal TaxaPlataforma { get; set; }

    public int Inscritos { get; set; }
    public int Pagantes { get; set; }

    public List<FinanceiroCategoriaVM> PorCategoria { get; set; } = new();
    public List<PagamentoPendenteVM> Pendentes { get; set; } = new();

    // Quanto sobra pro organizador depois da comissão da plataforma.
    public decimal Liquido => Arrecadado - TaxaPlataforma;

    public bool TemMovimento => Arrecadado > 0 || Pendente > 0;

    // Torneio gratuito: a tela explica em vez de mostrar zeros.
    public bool EhGratuito => Torneio.PrecoInscricao <= 0;
}

public class FinanceiroCategoriaVM
{
    public string Categoria { get; set; } = "";
    public int Inscritos { get; set; }
    public int ListaDeEspera { get; set; }
    public decimal Arrecadado { get; set; }
    public decimal Pendente { get; set; }
    public decimal Estornado { get; set; }
}

public class PagamentoPendenteVM
{
    public string Jogador { get; set; } = "";
    public string? Celular { get; set; }
    public string Categoria { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? ExpiraEm { get; set; }
    public string? LinkCobranca { get; set; }
}

// Relatório de fechamento: o que o organizador manda pro patrocinador.
public class RelatorioTorneioVM
{
    public Torneio Torneio { get; set; } = null!;

    public int TotalDuplas { get; set; }
    public int TotalJogadores { get; set; }
    public int TotalPartidas { get; set; }
    public int PartidasFinalizadas { get; set; }
    public int TotalCategorias { get; set; }

    public decimal Arrecadado { get; set; }
    public decimal TaxaPlataforma { get; set; }
    public decimal Liquido => Arrecadado - TaxaPlataforma;

    public List<PodioCategoriaVM> Podios { get; set; } = new();
    public List<FinanceiroCategoriaVM> PorCategoria { get; set; } = new();

    // Alcance: quantas pessoas diferentes abriram a página do torneio não é medido,
    // então "público" aqui é o que dá pra provar — inscritos e seguidores alcançados.
    public int JogadoresAlcancados { get; set; }
    public DateTime GeradoEm { get; set; } = DateTime.Now;
}

public class PodioCategoriaVM
{
    public string Categoria { get; set; } = "";
    public string? Campea { get; set; }
    public string? Vice { get; set; }
    public List<string> Semifinalistas { get; set; } = new();
    public int Duplas { get; set; }
}

// "Cabe?" — a conta que o organizador precisa ver ANTES de sortear as chaves, quando ainda
// dá pra mudar quadras, duração ou horário. Depois do sorteio a grade já está marcada e
// remarcar significa avisar todo mundo de novo.
public class PrevisaoGradeVM
{
    public int Duplas { get; set; }
    public int Grupos { get; set; }
    public int JogosDeGrupo { get; set; }
    public int JogosDeMataMata { get; set; }
    public int TotalDeJogos { get; set; }

    public DateTime Inicio { get; set; }

    // Quando o último jogo TERMINA (o começo mais a duração) — é o que o organizador precisa
    // pra saber a que horas devolve a quadra.
    public DateTime FimPrevisto { get; set; }

    public int Dias { get; set; }
    public bool EstouraOPrazo { get; set; }
}
