namespace Padelizou.Services;

// O painel admin tinha 20 cartões numa grade só, e achar um virou trabalho (pedido do Felipe,
// 11/08). A saída não foi apagar tela nenhuma: telas do mesmo assunto viraram UM cartão que
// abre uma página com ABAS.
//
// ⚠️ Esta lista é a FONTE ÚNICA das duas pontas: o cartão em /Admin e a barra de abas dentro de
// cada tela saem daqui. Escrever a barra à mão em cada view seria a segunda cópia — e a
// segunda cópia é como uma aba nova nasce achável por um caminho e invisível pelo outro.
//
// ⚠️ E aqui NÃO mora permissão nenhuma além de "quem vê o cartão". Quem recusa a entrada
// continua sendo cada action (ObterJogadorAdminAsync / ObterJogadorAdminRaizAsync): esconder
// uma aba é cortesia, e cortesia não tranca porta.
// `SoRaiz` na ABA, e não só no grupo: "Parceiros" tem uma aba de dinheiro (Comissões) e uma de
// operação (Indicações), e sem isto o grupo inteiro teria que sumir pro administrador nomeado —
// ele perderia o caderno de quem-trouxe-quem junto com o extrato que não é da conta dele.
public record AbaDoPainel(string Titulo, string Action, string Icone, bool SoRaiz = false);

public record GrupoDoPainel(
    string Titulo,
    string Descricao,
    string Icone,
    IReadOnlyList<AbaDoPainel> Abas,
    // Só o admin RAIZ vê o cartão. Espelha o `@if (ehRaiz)` que já cercava essas telas no
    // painel — a trava de verdade continua na action.
    bool SoRaiz = false,
    // A frase que o raiz lê, quando ela fala de dinheiro. Sem isto o cartão prometeria ao
    // administrador nomeado ("quanto cada parceiro tem a receber") justamente o que ele não vai
    // encontrar lá dentro — e cartão que mente é pior do que cartão que some.
    string? DescricaoDoRaiz = null)
{
    public string DescricaoPara(bool ehRaiz) =>
        ehRaiz && DescricaoDoRaiz != null ? DescricaoDoRaiz : Descricao;
}

public static class PainelAdminAgrupado
{
    public static readonly IReadOnlyList<GrupoDoPainel> Grupos = new List<GrupoDoPainel>
    {
        new(
            Titulo: "Professores, clubes e times",
            Descricao: "Quem dá aula e em que plano está, o dono de cada clube, e quem administra cada time.",
            Icone: "bi-collection",
            Abas: new AbaDoPainel[]
            {
                new("Professores", "Professores", "bi-mortarboard"),
                new("Clubes", "Clubes", "bi-shop"),
                new("Times", "Times", "bi-shield-fill"),
            }),

        new(
            Titulo: "Números do Padelizou",
            Descricao: "Cadastros e inscrições ao longo do tempo — e quantos jogadores em cada cidade.",
            DescricaoDoRaiz: "Cadastros, inscrições, pagamentos e o teto do MEI — e quantos jogadores em cada cidade.",
            Icone: "bi-graph-up",
            Abas: new AbaDoPainel[]
            {
                new("Métricas de uso", "Metricas", "bi-graph-up"),
                new("Jogadores por região", "Regioes", "bi-geo-alt"),
            }),

        // ⚠️ "Placares contestados" entrou AQUI, e não num grupo próprio: um grupo de uma aba
        // só vira um cartão que promete abas e abre numa tela sem nenhuma (tem teste). E o
        // assunto é o mesmo dos outros dois — o que precisa de alguém olhar e decidir sobre uma
        // briga entre pessoas.
        new(
            Titulo: "O que precisa da sua palavra",
            Descricao: "As opiniões sobre o site, o que foi sinalizado nos perfis, e o placar que as duas duplas não acertaram.",
            Icone: "bi-chat-heart",
            Abas: new AbaDoPainel[]
            {
                new("Opiniões", "Feedbacks", "bi-chat-heart"),
                new("Comentários denunciados", "Denuncias", "bi-flag"),
                new("Placares contestados", "DesafiosEmDisputa", "bi-exclamation-diamond"),
            }),

        new(
            Titulo: "Parceiros",
            Descricao: "O caderno de quem trouxe quem, e em que pé está cada indicação.",
            DescricaoDoRaiz: "Quanto cada parceiro tem a receber, e o caderno de quem trouxe quem.",
            Icone: "bi-people",
            Abas: new AbaDoPainel[]
            {
                // Dinheiro: quanto cada parceiro tem a receber. Só o raiz (18/08/2026).
                new("Comissões", "Comissoes", "bi-cash-coin", SoRaiz: true),
                new("Indicações", "Leads", "bi-person-plus"),
            }),

        new(
            Titulo: "Torneios: aprovar e liberar",
            Descricao: "Todo torneio nasce fora da vitrine — e o perfil de organizador é liberado pessoa a pessoa.",
            Icone: "bi-patch-check",
            SoRaiz: true,
            Abas: new AbaDoPainel[]
            {
                new("Torneios para aprovar", "TorneiosParaAprovar", "bi-patch-check"),
                new("Quem pode criar torneio", "Organizadores", "bi-person-badge"),
            }),
    };

    // As abas que esta pessoa enxerga. Mora aqui, e não nas duas views, porque o cartão em
    // /Admin e a barra de abas dentro da tela precisam concordar: uma aba escondida num lugar e
    // visível no outro é como o administrador nomeado chegaria em /Admin/Comissoes por um
    // caminho lateral e levaria um Forbid sem entender.
    public static IReadOnlyList<AbaDoPainel> AbasVisiveis(GrupoDoPainel grupo, bool ehRaiz) =>
        grupo.Abas.Where(a => !a.SoRaiz || ehRaiz).ToList();

    // Os cartões do painel. Grupo cujas abas sumiram todas some junto — um cartão que abre
    // numa tela vazia é pior do que cartão nenhum.
    public static IReadOnlyList<GrupoDoPainel> GruposVisiveis(bool ehRaiz) =>
        Grupos.Where(g => (!g.SoRaiz || ehRaiz) && AbasVisiveis(g, ehRaiz).Count > 0).ToList();

    // Qual grupo contém esta action. É como cada tela descobre as próprias abas sem repetir a
    // lista — a view chama o partial, o partial pergunta aqui.
    public static GrupoDoPainel? DoAction(string? action) =>
        Grupos.FirstOrDefault(g => g.Abas.Any(a =>
            string.Equals(a.Action, action, StringComparison.OrdinalIgnoreCase)));
}
