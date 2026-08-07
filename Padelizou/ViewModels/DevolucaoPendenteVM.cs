namespace Padelizou.ViewModels;

// Uma inscrição PAGA de um torneio que foi cancelado — ou seja, dinheiro que o organizador
// precisa devolver.
//
// O Padelizou não estorna sozinho (decisão do Felipe, 07/08/2026): mexer em dinheiro de
// verdade, sem volta, é grande demais pra sair junto de um botão de cancelar. O que ele faz é
// entregar a lista pronta — quem, quanto, e o telefone pra chamar.
public class DevolucaoPendenteVM
{
    public string Quem { get; set; } = "";
    public string Categoria { get; set; } = "";

    // Celular de quem fez a inscrição. É por ele que a devolução se combina, e é o dado que
    // faria o organizador abrir cinco telas pra achar.
    public string? Contato { get; set; }

    public decimal Valor { get; set; }
    public DateTime? PagoEm { get; set; }
}
