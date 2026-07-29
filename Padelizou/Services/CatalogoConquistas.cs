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
//   no dia 1: conquista que ninguém alcança em meses não puxa ninguém.
// - A grade do perfil tem 4 por fileira — o total aqui fecha em 12 de propósito.
public static class CatalogoConquistas
{
    public const int JogosParaMensalista = 4;
    public const int TitulosParaBicampeao = 2;
    public const int TorneiosParaVeterano = 5;
    public const int VitoriasParaDezVitorias = 10;
    public const int ElogiosParaQuerido = 5;
    public const int AulasParaAlunoAplicado = 3;

    public static List<ConquistaVM> Montar(DadosParaConquistas d)
    {
        return new List<ConquistaVM>
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
            new() { Codigo = "DezVitorias", Titulo = "10 Vitórias", Icone = "bi-graph-up-arrow",
                    Conquistada = d.Vitorias >= VitoriasParaDezVitorias,
                    Descricao = $"Vença {VitoriasParaDezVitorias} jogos de torneio." },

            // A escada de quem vence
            new() { Codigo = "Finalista", Titulo = "Finalista", Icone = "bi-award-fill",
                    Conquistada = d.Finais > 0 || d.Titulos > 0,
                    Descricao = "Chegue a uma final." },
            new() { Codigo = "Campeao", Titulo = "Campeão", Icone = "bi-trophy-fill",
                    Conquistada = d.Titulos > 0,
                    Descricao = "Vença um torneio." },
            new() { Codigo = "Bicampeao", Titulo = "Bicampeão", Icone = "bi-gem",
                    Conquistada = d.Titulos >= TitulosParaBicampeao,
                    Descricao = $"Vença {TitulosParaBicampeao} torneios." },
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
        };
    }
}
