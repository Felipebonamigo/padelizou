using Padelizou.ViewModels;

namespace Padelizou.Services;

// As molduras da foto do jogador — uma por conquista (pedido do Felipe, 13/08/2026).
//
// A moldura é o lado VISÍVEL da conquista: o badge fica no perfil, a moldura viaja com a foto
// pra toda tela do sistema. É por isso que o código da moldura É o código da conquista, e não
// um catálogo próprio: moldura sem conquista correspondente seria enfeite à venda, e conquista
// sem moldura seria promessa quebrada — o teste que amarra os dois conjuntos falha nas duas
// direções.
//
// ⚠️ O desenho mora no CSS (site.css, classes `pdz-m-{Codigo}`), não aqui. Este arquivo só
// responde três perguntas: quais existem, como se chamam, e se ESTE jogador pode usar AQUELA.
public static class CatalogoMolduras
{
    // `Livre` = de fábrica: liberada pra todo mundo desde o primeiro dia, sem conquista.
    // Existe por dois motivos (14/08/2026): o Felipe pediu as molduras divertidas da
    // referência dele, e o jogador NOVO — zero conquista — não tinha nenhuma pra vestir.
    // A de fábrica é o brinquedo de chegada; a de conquista continua sendo o troféu.
    public record Moldura(string Codigo, string Nome, string ComoDestravar, bool Livre = false);

    // Na ordem do catálogo de conquistas — a tela de escolha mostra na mesma ordem em que o
    // perfil mostra os badges, e ordem diferente nas duas telas parece catálogo diferente.
    // As de fábrica vêm primeiro: são as que TODO MUNDO pode vestir já.
    public static readonly IReadOnlyList<Moldura> Todas = new List<Moldura>
    {
        // As de fábrica (da referência do Felipe: orelhas, sapo, chapéu — o que É moldura
        // de jogo). Sem gancho de conquista, e o teste de sincronia sabe disso.
        new("Gatinha",   "Gatinha",    "De fábrica — sua desde o primeiro dia.", Livre: true),
        new("Sapinho",   "Sapinho",    "De fábrica — sua desde o primeiro dia.", Livre: true),
        new("Raposa",    "Raposa",     "De fábrica — sua desde o primeiro dia.", Livre: true),
        new("Bruxinha",  "Bruxinha",   "De fábrica — sua desde o primeiro dia.", Livre: true),
        new("GatoPreto", "Gato Preto", "De fábrica — sua desde o primeiro dia.", Livre: true),
        new("Fada",      "Fada",       "De fábrica — sua desde o primeiro dia.", Livre: true),

        // A escada de quem joga
        new("Estreia",       "Primeira Bola",   "Entre no seu primeiro jogo ou torneio."),
        new("Mensalista",    "Presença",        "Jogue 4 jogos fixos de grupo."),
        new("Veterano",      "Estrada",         "Dispute 5 torneios."),
        new("Explorador",    "Rosa dos Ventos", "Jogue torneios em 3 clubes diferentes."),

        // A escada das vitórias — esquenta conforme sobe
        new("DezVitorias",            "Embalo",     "Vença 10 jogos de torneio."),
        new("VinteCincoVitorias",     "Ritmo",      "Vença 25 jogos de torneio."),
        new("CinquentaVitorias",      "Meio Cento", "Vença 50 jogos de torneio."),
        new("CemVitorias",            "Centena",    "Vença 100 jogos de torneio."),
        new("CentoCinquentaVitorias", "Brasa",      "Vença 150 jogos de torneio."),
        new("DuzentasVitorias",       "Órbita",     "Vença 200 jogos de torneio."),

        // A escada de quem vence — prata, o ouro do primeiro título, e daí em diante cada
        // título é uma JOIA diferente. Era tudo variação de ouro e, na vitrine, o Felipe
        // resumiu (14/08): "tem muitos ouro igual". Tinha — em 26px virava tudo o mesmo anel.
        new("Finalista",    "Prata",     "Chegue a uma final."),
        new("Campeao",      "Ouro",      "Vença um torneio."),
        new("Bicampeao",    "Rubi",      "Vença 2 torneios."),
        new("Tricampeao",   "Safira",    "Vença 3 torneios."),
        new("Tetracampeao", "Ametista",  "Vença 4 torneios."),
        new("Pentacampeao", "Esmeralda", "Vença 5 torneios."),
        new("Hexacampeao",  "Colmeia",   "Vença 6 torneios."),
        new("Heptacampeao", "Sol",       "Vença 7 torneios."),
        new("Octacampeao",  "Platina",   "Vença 8 torneios."),
        new("Nonacampeao",  "Escudo",    "Vença 9 torneios."),
        new("Decacampeao",  "Lenda",     "Vença 10 torneios."),

        // As sociais
        new("MvpDeTorneio",    "Estrela da Noite",   "Seja eleito o MVP de um torneio."),
        new("QueridoDaQuadra", "Coração da Torcida", "Receba elogios de 5 jogadores diferentes."),
        new("DoTime",          "Uniforme",           "Vista a camisa de um time."),
        new("AlunoAplicado",   "Caderno",            "Faça 3 aulas com professor."),
        new("Organizador",     "Prancheta",          "Organize um torneio."),
        new("Professor",       "Mestre",             "Dê aulas de padel pelo Padelizou."),
    };

    private static readonly Dictionary<string, Moldura> PorCodigoIndex =
        Todas.ToDictionary(m => m.Codigo);

    public static Moldura? PorCodigo(string? codigo) =>
        codigo != null && PorCodigoIndex.TryGetValue(codigo, out var m) ? m : null;

    // A classe CSS que desenha a moldura. Vazio = foto limpa, sem wrapper especial — e o
    // código passa por PorCodigo antes, então valor inventado no banco não vira classe.
    public static string ClasseCss(string? codigo) =>
        PorCodigo(codigo) is { } m ? $"pdz-m-{m.Codigo}" : "";

    // A trava do POST de escolher, pura e testável (o mesmo padrão de RemocaoDeLocal).
    // Devolve o motivo para NÃO usar, ou null quando pode.
    //
    // ⚠️ A LISTA DA TELA NÃO É A TRAVA: a tela cinza a moldura bloqueada, mas o POST chega
    // com qualquer string. Sem esta conferência, um formulário montado à mão vestiria a
    // moldura de Decacampeão em quem nunca jogou.
    public static string? MotivoParaNaoUsar(string? codigo, IEnumerable<ConquistaVM> conquistasDoJogador)
    {
        // Tirar a moldura (voltar pra foto limpa) é sempre permitido.
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var moldura = PorCodigo(codigo);
        if (moldura == null)
            return "Essa moldura não existe.";

        // De fábrica não pede conquista nenhuma — é dela que o jogador novo vive.
        if (moldura.Livre) return null;

        var conquista = conquistasDoJogador.FirstOrDefault(c => c.Codigo == codigo);
        if (conquista == null || !conquista.Conquistada)
            return "Essa moldura vem de uma conquista que você ainda não destravou.";

        return null;
    }
}
