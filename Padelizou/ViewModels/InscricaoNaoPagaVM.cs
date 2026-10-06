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
// `JaPago` e `Falta` existem desde 06/10/2026, quando a inscrição passou a poder ser paga em
// PARTES ("pago só a minha metade"). `Valor` continua sendo o total da inscrição — é ele que a
// pessoa reconhece do anúncio do torneio; o que ela DEVE agora é o `Falta`.
//
// `PodeEscolherMinhaParte` só vem true quando há o que dividir: inscrição de dupla devendo mais
// que a parte de uma pessoa. Sozinho não há escolha, e quando falta só uma parte o botão é um.
public sealed record InscricaoNaoPagaVM(
    int? DuplaId,
    int? InscricaoAmericanaId,
    string Categoria,
    string Quem,
    decimal Valor,
    string? Conta,
    decimal JaPago = 0m,
    decimal Falta = 0m,
    bool PodeEscolherMinhaParte = false,
    decimal MinhaParte = 0m);
