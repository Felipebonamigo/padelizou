namespace Padelizou.ViewModels;

// Uma inscrição MINHA que ainda não foi paga, do jeito que a faixa "Sua inscrição está sem
// pagamento" precisa dela.
//
// ⚠️ São VÁRIAS, uma por categoria. A faixa nasceu mostrando uma só (um `FirstOrDefault` sem
// ordenação), e quem se inscreveu em duas categorias via uma cobrança e não via a outra — nem
// na tela, nem no botão. Pagava a que apareceu e continuava devendo, sem nada dizendo isso.
//
// `Valor` é o que está GRAVADO na inscrição, que é o que o checkout cobra
// (PagamentoInscricaoService.ValorJaCombinadoAsync) — nunca um recálculo. `Conta` é a
// explicação desse total, e vem nula quando não há conta que o reproduza: melhor sem frase do
// que com uma que não fecha (ver Services/ContaDaInscricao).
public sealed record InscricaoNaoPagaVM(
    int? DuplaId,
    int? InscricaoAmericanaId,
    string Categoria,
    string Quem,
    decimal Valor,
    string? Conta);
