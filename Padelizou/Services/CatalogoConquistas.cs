using Padelizou.ViewModels;

namespace Padelizou.Services;

// Números de um jogador que as conquistas leem. Existe pra separar a REGRA (aqui, pura e
// testável) da COLETA (EstatisticasService, que consulta o banco).
public record DadosParaConquistas(
    bool JogouAlgumaVez,
    int JogosSemanais,
    bool EhOrganizador,
    bool TemTime,
    bool EhProfessor,
    int Titulos,
    int Finais,
    int TotalTorneios,
    int Vitorias,
    int ElogiosRecebidos,
    int AulasComoAluno);

// As conquistas do perfil. Calculadas na hora a partir do que o jogador já fez — não há
// tabela de conquistas no banco, então nunca há conquista "esquecida de dar": mudou o dado,
// mudou a conquista.
//
// Regras de desenho:
// - Conquista bloqueada é uma META, e a Descricao diz como destravar — badge cinza sem
//   explicação é decoração, não incentivo.
// - Os limiares são degraus curtos no começo (1 final, 5 torneios) porque a produção está
//   no dia 1: conquista que ninguém alcança em meses não puxa ninguém. Os degraus longos
//   (200 vitórias, decacampeão) existem pra dar teto à coleção, não pra puxar quem começou.
// - A grade do perfil tem 4 por fileira.
public static class CatalogoConquistas
{
    public const int JogosParaMensalista = 4;
    public const int TorneiosParaVeterano = 5;
    public const int ElogiosParaQuerido = 5;
    public const int AulasParaAlunoAplicado = 3;

    // A escada de vitórias em jogos de torneio. O primeiro degrau é o antigo "DezVitorias", e
    // o código dele NÃO muda: conquista que alguém já viu no próprio perfil não pode sumir
    // porque a lista cresceu.
    public static readonly (int Marca, string Codigo, string Titulo, string Icone)[] EscadaDeVitorias =
    {
        (10,  "DezVitorias",             "10 Vitórias",  "bi-graph-up-arrow"),
        (25,  "VinteCincoVitorias",      "25 Vitórias",  "bi-bar-chart-fill"),
        (50,  "CinquentaVitorias",       "50 Vitórias",  "bi-speedometer2"),
        (100, "CemVitorias",             "100 Vitórias", "bi-bullseye"),
        (150, "CentoCinquentaVitorias",  "150 Vitórias", "bi-fire"),
        (200, "DuzentasVitorias",        "200 Vitórias", "bi-rocket-takeoff-fill"),
    };

    // Bi, tri, tetra... é como a pessoa conta o próprio título na roda, então é assim que o
    // troféu se chama. O primeiro título fica de fora desta escada: ele é a conquista
    // "Campeão", que já estava no ar com esse código e esse texto.
    public static readonly (int Titulos, string Codigo, string Nome, string Icone)[] EscadaDeCampeao =
    {
        (2,  "Bicampeao",    "Bicampeão",    "bi-gem"),
        (3,  "Tricampeao",   "Tricampeão",   "bi-stars"),
        (4,  "Tetracampeao", "Tetracampeão", "bi-suit-diamond-fill"),
        (5,  "Pentacampeao", "Pentacampeão", "bi-lightning-charge-fill"),
        (6,  "Hexacampeao",  "Hexacampeão",  "bi-hexagon-fill"),
        (7,  "Heptacampeao", "Heptacampeão", "bi-brightness-high-fill"),
        (8,  "Octacampeao",  "Octacampeão",  "bi-diamond-fill"),
        (9,  "Nonacampeao",  "Nonacampeão",  "bi-shield-fill-check"),
        (10, "Decacampeao",  "Decacampeão",  "bi-infinity"),
    };

    public static List<ConquistaVM> Montar(DadosParaConquistas d)
    {
        var conquistas = new List<ConquistaVM>
        {
            // A escada de quem joga
            new() { Codigo = "Estreia", Titulo = "Estreia", Icone = "bi-flag-fill",
                    Conquistada = d.JogouAlgumaVez || d.JogosSemanais > 0,
                    Descricao = "Entre no seu primeiro jogo ou torneio." },
            new() { Codigo = "Mensalista", Titulo = "Mensalista", Icone = "bi-calendar2-check-fill",
                    Conquistada = d.JogosSemanais >= JogosParaMensalista,
                    Descricao = $"Jogue {JogosParaMensalista} jogos fixos de grupo." },
            new() { Codigo = "Veterano", Titulo = "Veterano", Icone = "bi-patch-check-fill",
                    Conquistada = d.TotalTorneios >= TorneiosParaVeterano,
                    Descricao = $"Dispute {TorneiosParaVeterano} torneios." },
        };

        // A escada das vitórias entra inteira, na ordem — é ela que dá o que perseguir depois
        // que os degraus curtos acabam.
        conquistas.AddRange(EscadaDeVitorias.Select(v => new ConquistaVM
        {
            Codigo = v.Codigo,
            Titulo = v.Titulo,
            Icone = v.Icone,
            Conquistada = d.Vitorias >= v.Marca,
            Descricao = $"Vença {v.Marca} jogos de torneio.",
        }));

        // A escada de quem vence
        conquistas.AddRange(new List<ConquistaVM>
        {
            new() { Codigo = "Finalista", Titulo = "Finalista", Icone = "bi-award-fill",
                    Conquistada = d.Finais > 0 || d.Titulos > 0,
                    Descricao = "Chegue a uma final." },
            new() { Codigo = "Campeao", Titulo = "Campeão", Icone = "bi-trophy-fill",
                    Conquistada = d.Titulos > 0,
                    Descricao = "Vença um torneio." },
        });

        conquistas.AddRange(EscadaDeCampeao.Select(c => new ConquistaVM
        {
            Codigo = c.Codigo,
            Titulo = c.Nome,
            Icone = c.Icone,
            Conquistada = d.Titulos >= c.Titulos,
            Descricao = $"Vença {c.Titulos} torneios.",
        }));

        conquistas.AddRange(new List<ConquistaVM>
        {
            new() { Codigo = "QueridoDaQuadra", Titulo = "Querido da Quadra", Icone = "bi-heart-fill",
                    Conquistada = d.ElogiosRecebidos >= ElogiosParaQuerido,
                    Descricao = $"Receba {ElogiosParaQuerido} elogios de outros jogadores." },

            // A escada de quem constrói o padel em volta
            new() { Codigo = "DoTime", Titulo = "Do time", Icone = "bi-shield-fill",
                    Conquistada = d.TemTime,
                    Descricao = "Vista a camisa de um time no seu perfil." },
            new() { Codigo = "AlunoAplicado", Titulo = "Aluno Aplicado", Icone = "bi-journal-check",
                    Conquistada = d.AulasComoAluno >= AulasParaAlunoAplicado,
                    Descricao = $"Faça {AulasParaAlunoAplicado} aulas com professor." },
            new() { Codigo = "Organizador", Titulo = "Organizador", Icone = "bi-clipboard-check-fill",
                    Conquistada = d.EhOrganizador,
                    Descricao = "Organize (ou ajude a organizar) um torneio." },
            new() { Codigo = "Professor", Titulo = "Professor", Icone = "bi-mortarboard-fill",
                    Conquistada = d.EhProfessor,
                    Descricao = "Dê aulas de padel pelo Padelizou." },
        });

        return conquistas;
    }
}
