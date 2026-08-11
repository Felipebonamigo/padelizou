using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// Uma linha da tela /Admin/Comissoes: um cliente que um parceiro trouxe, e o que ele rendeu.
//
// Arquivo próprio, e não dentro de AdminViewModels.cs, só pra não disputar o mesmo arquivo com
// as outras telas do painel que crescem em paralelo.
public class ComissaoDeClienteVM
{
    public LeadComercial Lead { get; set; } = null!;

    public ComissaoDoParceiro.Conta Conta { get; set; } = ComissaoDoParceiro.Conta.Vazia;

    // O que caiu no mês que ainda está correndo. Fica separado do total porque é o único
    // pedaço que ainda pode crescer — o repasse fecha no dia 30.
    public decimal DoMesCorrente { get; set; }

    // Já fechado: é isto que entra no Pix do dia 10.
    public decimal Fechado => Conta.Total - DoMesCorrente;
}
