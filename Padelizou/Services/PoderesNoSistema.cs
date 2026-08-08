using Padelizou.Models;

namespace Padelizou.Services;

// Quem VÊ e quem EDITA o sistema inteiro — duas perguntas que até 08/08/2026 eram uma só.
//
// `IsAdminRaiz` e `IsAdminGeral` sempre significaram ver E editar juntos: as duas viram a
// credencial `IsAdmin`, que destrava desde o painel /Admin até mexer no placar e na gestão de
// QUALQUER torneio. Não havia como deixar alguém acompanhar a operação sem entregar o volante.
//
// O ASSISTENTE é essa terceira resposta (pedido do Felipe: o Foka acompanha tudo, só o Felipe
// edita). Ele entra apenas do lado da leitura.
//
// ⚠️ A REGRA DE OURO DESTE ARQUIVO: `PodeEditarTudo` é EXATAMENTE o que `IsAdmin` sempre foi,
// e o assistente não aparece nela. A flag nova nunca é somada à credencial de escrita — é o
// que garante que, se eu esquecer um lugar, o efeito seja "o assistente não vê essa tela" e
// nunca "o assistente editou". Errar pra menos aqui é barato; pra mais, não.
public static class PoderesNoSistema
{
    // Ver o sistema inteiro: os dois admins e o assistente.
    public static bool PodeOlharTudo(Jogador? jogador) =>
        jogador is { IsAdminRaiz: true } or { IsAdminGeral: true } or { IsAssistente: true };

    // Mexer no sistema inteiro. É a régua histórica do `IsAdmin`, sem o assistente.
    public static bool PodeEditarTudo(Jogador? jogador) =>
        jogador is { IsAdminRaiz: true } or { IsAdminGeral: true };

    // Está olhando de fora: vê tudo e não muda nada. É o que acende a faixa de só-leitura e
    // desliga os formulários na tela.
    //
    // ⚠️ Assistente que TAMBÉM é admin não é só-leitura — a flag soma, não substitui.
    public static bool SoOlha(Jogador? jogador) =>
        PodeOlharTudo(jogador) && !PodeEditarTudo(jogador);

    // O que a pessoa lê no topo da tela que ela não pode mexer. Fica aqui, e não na view,
    // porque a faixa aparece no painel admin E na gestão do torneio — e as duas têm que
    // dizer a mesma coisa.
    public const string AvisoDeSoLeitura =
        "Você está vendo como assistente do sistema: enxerga tudo, mas não altera nada. "
        + "Nos torneios que você organiza, seus botões continuam funcionando normalmente.";
}
