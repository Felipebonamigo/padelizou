using Padelizou.Models;

namespace Padelizou.ViewModels;

// Painel financeiro de quem RECEBE pelo app (organizador de torneio, professor, dono de
// clube). Todos os três usam a mesma tabela Pagamento, então a mesma tela serve os três
// papéis — muda só a origem do dinheiro (torneio / aula / quadra).
public class ExtratoFinanceiroVM
{
    // "mes" | "ano" | "sempre" — recorte de todos os números desta tela.
    public string Periodo { get; set; } = "sempre";
    public string PeriodoRotulo => Periodo switch
    {
        "mes" => "neste mês",
        "ano" => "neste ano",
        _ => "desde o começo"
    };

    public decimal Recebido { get; set; }        // confirmado, já é seu
    public decimal AReceber { get; set; }        // pendente de pagamento
    public decimal Estornado { get; set; }       // devolvido ao jogador
    public decimal TaxaPaga { get; set; }        // comissão do Padelizou sobre o confirmado
    public int QtdRecebimentos { get; set; }

    // De onde veio o dinheiro (um torneio, as aulas, as quadras).
    public List<OrigemFinanceiraVM> PorOrigem { get; set; } = new();

    // Quem ainda não pagou — o "a cobrar" do dia a dia.
    public List<Pagamento> Pendentes { get; set; } = new();

    // Extrato detalhado do período.
    public List<Pagamento> Movimentos { get; set; } = new();

    // O que EU paguei como jogador (inscrições, aulas, quadras).
    public List<Pagamento> MinhasCompras { get; set; } = new();

    // Só pro dono da plataforma: a comissão consolidada de todo mundo.
    public bool EhAdmin { get; set; }
    public decimal ComissaoPlataforma { get; set; }
    public decimal ComissaoPlataformaPendente { get; set; }

    public bool TemMovimento => Movimentos.Count > 0 || Pendentes.Count > 0;
}

// Uma linha de "de onde veio o dinheiro": um torneio específico, ou o agregado de aulas/quadras.
public class OrigemFinanceiraVM
{
    public string Nome { get; set; } = "";
    public string Tipo { get; set; } = "";      // "Torneio" | "Aula" | "Quadra"
    public string Icone { get; set; } = "bi-cash-coin";
    public decimal Recebido { get; set; }
    public decimal Pendente { get; set; }
    public int Qtd { get; set; }
}
