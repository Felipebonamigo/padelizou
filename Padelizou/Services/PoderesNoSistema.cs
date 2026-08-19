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
// O PARCEIRO DO RANKING (10/08/2026) é a quarta, e ela não é "menos poder" — é OUTRO EIXO: não
// é quanto se pode mexer, é quanto se enxerga. Ele vê UMA tela do painel, a da nossa parceria
// com eles, e o resto do sistema simplesmente não existe do lado de lá.
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

    // ── DINHEIRO É SÓ DO RAIZ ──────────────────────────────────────────────────────────
    //
    // Terceiro eixo (18/08/2026, decisão do Felipe: "adm não vê nada de financeiro, somente
    // eu"). `PodeOlharTudo` respondia por duas perguntas diferentes — "enxerga a operação?" e
    // "enxerga o caixa?" —, e é justamente por elas serem a mesma função que o administrador
    // nomeado e o assistente liam a receita do MEI, a comissão de cada parceiro, a margem do
    // registro de resultados e o caixa de qualquer torneio.
    //
    // ⚠️ NÃO É "PodeEditarTudo com outro nome": o admin nomeado edita o sistema inteiro e
    // mesmo assim não vê um real. Quem escrever `PodeEditarTudo` no lugar disto reabre o
    // buraco pro admin nomeado sem que nenhuma tela pareça diferente.
    //
    // ⚠️ Esta função responde por DINHEIRO NOSSO E DE TERCEIROS: receita, comissão, margem,
    // mensalidade de professor, caixa de torneio e Pix alheio. As duas exceções mora cada uma
    // na sua função abaixo, e as duas são a mesma ideia — ver o PRÓPRIO dinheiro nunca passou
    // por aqui: o organizador vê o caixa do torneio dele pelo nível de acesso
    // (AcessoAoDinheiroDoTorneio), e o parceiro vê o extrato dele pela flag de parceiro.
    public static bool PodeVerDinheiro(Jogador? jogador) =>
        jogador is { IsAdminRaiz: true };

    // A MESMA pergunta feita pelo crachá, pra quem só tem o `User` na mão (as views e os
    // controllers que não buscam o Jogador). Sobrecarga em vez de `claim == "true"` espalhado:
    // no dia em que a régua mudar, ela muda uma vez.
    //
    // ⚠️ O crachá só é reescrito quando a pessoa entra de novo. Serve pra ESCONDER (aba,
    // cartão, coluna); quem RECUSA a entrada continua sendo a action, que lê o banco.
    public static bool PodeVerDinheiro(System.Security.Claims.ClaimsPrincipal? usuario) =>
        usuario?.FindFirst("IsAdminRaiz")?.Value == "true";

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

    // ── O PARCEIRO DO RANKING: UMA TELA, E NADA MAIS ───────────────────────────────────
    //
    // O assistente é "vê TUDO e não edita nada". Este é o oposto no eixo do que se enxerga:
    // vê UMA página — o relatório da parceria — e nem o resto do painel existe pra ele.
    //
    // ⚠️ Ele NÃO entra em `PodeOlharTudo`. É a mesma disciplina da flag do assistente, um
    // degrau mais estreita: `PodeOlharTudo` destrava o painel inteiro, o financeiro e a
    // gestão de qualquer torneio, e nada disso é da conta de uma empresa de fora. Somar aqui
    // seria dar a chave da casa pra quem foi convidado a ver uma prateleira.
    //
    // Quem pode LER o relatório: os dois admins, o assistente e o parceiro. Escrever, ninguém
    // — a tela não tem um único formulário.
    public static bool PodeVerRelatorioDoRanking(Jogador? jogador) =>
        PodeOlharTudo(jogador) || jogador is { IsParceiroRanking: true };

    // A tela continua aberta pra quem é da casa, mas as COLUNAS DE VALOR não: o relatório
    // conta quem aderiu, quem foi barrado e quem passou pelo filtro — isso é operação — e
    // também quanto a parceria rende, que é dinheiro (18/08/2026).
    //
    // O parceiro continua vendo os valores porque a conta é DELE: é o extrato do que ele tem
    // a receber por pessoa casada, e esconder aqui esvaziaria a tela que existe pra ele.
    public static bool PodeVerValoresDoRankingRs(Jogador? jogador) =>
        PodeVerDinheiro(jogador) || jogador is { IsParceiroRanking: true };

    // Está aqui SÓ por causa do relatório: o painel não existe pra ele. É o que decide se a
    // pessoa cai direto na tela da parceria ao entrar em /Admin, em vez de ser mandada embora.
    //
    // ⚠️ Parceiro que também seja admin (o Felipe testando com a própria conta, por exemplo)
    // NÃO cai nisto — a flag soma, não substitui, igual à do assistente.
    public static bool SoVeORelatorioDoRanking(Jogador? jogador) =>
        jogador is { IsParceiroRanking: true } && !PodeOlharTudo(jogador);

    // ── O PARCEIRO COMERCIAL: UMA TELA, E SÓ AS LINHAS DELE ────────────────────────────
    //
    // Quinto perfil (11/08/2026), e o mais estreito de todos. O parceiro do Ranking vê UMA
    // tela inteira; este vê uma tela FILTRADA NELE. A diferença é o que está em jogo: dois
    // parceiros comerciais disputam os mesmos contatos, e a tela mostra quem cada um trouxe,
    // quanto rendeu e quanto vai receber. Um enxergar o outro é vazamento entre concorrentes.
    //
    // ⚠️ ESTAS DUAS FUNÇÕES NÃO SÃO A TRAVA — elas só dizem quem entra na tela. Quem garante
    // que o parceiro vê apenas as próprias linhas é AdminController.Comissoes, que IMPÕE o id
    // da sessão e ignora a query string. Uma checagem de "pode ver a tela" nunca responde
    // "pode ver ESTA linha", e confundir as duas é como se vaza dado em painel filtrado.
    //
    // ⚠️ A tela é DINHEIRO inteiro, então quem é da casa entra por `PodeVerDinheiro` — o
    // administrador nomeado e o assistente não veem a carteira de ninguém (18/08/2026).
    public static bool PodeVerComissoesDeParceiro(Jogador? jogador) =>
        PodeVerDinheiro(jogador) || jogador is { IsParceiroComercial: true };

    // Está aqui SÓ por causa do extrato dele: o resto do painel não existe do lado de lá.
    // Parceiro que também seja RAIZ (o Felipe indicando alguém) NÃO cai nisto — a flag soma,
    // não substitui, igual às outras duas.
    //
    // ⚠️ Administrador nomeado que também seja parceiro comercial cai aqui SIM, e é o certo:
    // ele vê o extrato dele porque é o dinheiro DELE, e não o dos outros. O desvio de /Admin
    // que usa esta função só acontece pra quem não tem painel nenhum (ver Index), então ele
    // continua caindo no painel normalmente.
    public static bool SoVeAsPropriasComissoes(Jogador? jogador) =>
        jogador is { IsParceiroComercial: true } && !PodeVerDinheiro(jogador);
}
