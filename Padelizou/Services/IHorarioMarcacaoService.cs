namespace Padelizou.Services;

// Preco = valor da quadra definido pelo clube. PrecoTotal = o que o jogador realmente paga
// (já com a taxa do Padelizou, quando o dono escolheu somá-la). Os dois vêm prontos daqui pra
// que a tela nunca mostre um valor menor do que o cobrado no checkout.
public record SlotMarcacao(
    int QuadraClubeId,
    string QuadraNome,
    DateTime Inicio,
    DateTime Fim,
    decimal? Preco,
    decimal? PrecoTotal = null,
    decimal? TaxaServico = null);

public interface IHorarioMarcacaoService
{
    // Expande as regras recorrentes (HorarioMarcacaoDisponivel) do clube pro dia pedido,
    // subtraindo o que já foi marcado (MarcacaoJogo confirmada) — mesma lógica de
    // AulasController.ObterHorarios, só que escopada a Clube/QuadraClube em vez de
    // Professor/LocalAula.
    Task<List<SlotMarcacao>> ObterSlotsDisponiveisAsync(int clubeId, DateTime data);

    // Elegibilidade + envio (e-mail/push) pra quem tem NotificarHorarioVagoRegiao e alguma
    // JogadorCidade batendo com o Clube.CidadeId. Reaproveitado pelo botão manual "Avisar vaga
    // hoje" e pelo job diário. Retorna quantos jogadores foram notificados.
    Task<int> NotificarHorarioVagoAsync(int clubeId);
}
