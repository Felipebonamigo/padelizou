namespace Padelizou.ViewModels;

// Painel "Métricas de uso" do admin — os números que dizem se o sistema está crescendo
// e quanto a plataforma já faturou no ano (pro controle do teto do MEI).
public class MetricasAdminVM
{
    // Cadastros
    public int TotalJogadores { get; set; }
    public int JogadoresNovos7 { get; set; }
    public int JogadoresNovos30 { get; set; }

    // Inscrições em torneio (Dupla + Americano)
    public int InscricoesNovas7 { get; set; }
    public int InscricoesNovas30 { get; set; }

    // Pagamentos confirmados
    public int PagamentosConfirmados30 { get; set; }
    public decimal ValorConfirmado30 { get; set; }

    // Engajamento
    public int JogadoresComApp { get; set; }
    public int TorneiosAtivos { get; set; }
    public int TorneiosTotal { get; set; }

    // MEI — comissão da plataforma no ano corrente contra o teto anual
    public decimal ComissaoAno { get; set; }
    public decimal TetoMei { get; set; }
    public int PercentualMei => TetoMei > 0 ? (int)Math.Round(ComissaoAno / TetoMei * 100) : 0;

    // Série das últimas 8 semanas (mais antiga primeiro)
    public List<SemanaMetricaVM> Semanas { get; set; } = new();
}

public class SemanaMetricaVM
{
    public DateTime Inicio { get; set; }   // segunda-feira
    public int Cadastros { get; set; }
    public int Inscricoes { get; set; }
    public int Pagamentos { get; set; }
    public decimal Valor { get; set; }
}
