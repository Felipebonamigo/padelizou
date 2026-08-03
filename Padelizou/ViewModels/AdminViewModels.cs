using Padelizou.Services;

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

    // Como a série está fatiada: "dia", "semana" ou "mes" (ver Services/FaixasDeMetricas).
    // Semana é o padrão — foi o único agrupamento que existiu até 03/08/2026.
    public string Agrupamento { get; set; } = FaixasDeMetricas.Semana;

    // A série em si, da fatia mais ANTIGA pra mais recente (a tela inverte pra desenhar).
    public List<FaixaMetricaVM> Faixas { get; set; } = new();
}

// Uma linha da série: um dia, uma semana ou um mês, conforme o Agrupamento.
public class FaixaMetricaVM
{
    public DateTime Inicio { get; set; }
    public string Rotulo { get; set; } = "";
    public int Cadastros { get; set; }
    public int Inscricoes { get; set; }
    public int Pagamentos { get; set; }
    public decimal Valor { get; set; }
}
