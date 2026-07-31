namespace Padelizou.ViewModels;

// O que a tela de relatório do bar mostra num período.
public class RelatorioBarVM
{
    public DateTime De { get; set; }
    public DateTime Ate { get; set; }

    public int Comandas { get; set; }
    public decimal Vendido { get; set; }

    // Somado à parte: desconto embutido no preço some do relatório e vira buraco no caixa.
    public decimal Descontos { get; set; }

    public List<(string Forma, decimal Valor, int Quantidade)> PorForma { get; set; } = new();
    public List<(string Produto, int Unidades, decimal Valor)> MaisVendidos { get; set; } = new();
    public List<(DateTime Dia, decimal Valor, int Comandas)> PorDia { get; set; } = new();

    // Os dois números que o dono precisa ver crescer pra desconfiar de alguma coisa.
    public int ItensCancelados { get; set; }
    public decimal ValorCancelado { get; set; }
    public int UnidadesPerdidas { get; set; }

    public decimal TicketMedio => Comandas > 0 ? Math.Round(Vendido / Comandas, 2) : 0m;

    public int Dias => (int)(Ate - De).TotalDays + 1;
    public decimal MediaPorDia => Dias > 0 ? Math.Round(Vendido / Dias, 2) : 0m;
}
