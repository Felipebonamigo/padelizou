namespace Padelizou.Services;

// Em quantos grupos um Torneio Americano se divide, e quem passa pro grupo final.
//
// A REGRA (Felipe, 06/08/2026):
//   • cada um joga com CADA UM do seu grupo, sem repetir parceiro;
//   • todos jogam a MESMA quantidade de jogos — por isso os grupos são iguais;
//   • com mais de um grupo, os primeiros de cada um formam um GRUPO FINAL, que decide;
//   • o sistema só aceita número de inscritos que permita isso, e avisa qual número fecha.
//
// Por que a divisão existe: "cada um com cada um" obriga n−1 rodadas. Vinte pessoas num
// grupo só são 19 rodadas — perto de 8 horas de quadra. Divididas em 4 grupos de 5, são 5
// rodadas de grupo mais 3 de final. O mesmo torneio cabe numa noite.
public static class DivisaoDoAmericano
{
    // Um tamanho de grupo em que "cada um com cada um" FECHA, sem repetir parceiro.
    //
    // São n(n−1)/2 duplas a formar e cada jogo forma 2: pra dar número inteiro de jogos,
    // n(n−1)/2 tem que ser par — o que acontece quando n é múltiplo de 4 ou múltiplo de 4
    // mais 1 (4, 5, 8, 9, 12, 13, 16, 17...). Com 6 pessoas dariam 7,5 jogos, e não existe
    // meio jogo. Isso é aritmética; nenhum sorteio contorna.
    public static bool TamanhoFecha(int tamanho) => tamanho >= 4 && tamanho % 4 <= 1;

    // Quantas rodadas um grupo desse tamanho tem. Múltiplo de 4: todos jogam toda rodada,
    // n−1 rodadas. Múltiplo de 4 mais 1: um descansa por vez, n rodadas — e cada um joga
    // n−1, porque descansa exatamente uma.
    public static int RodadasDe(int tamanho) => tamanho % 4 == 0 ? tamanho - 1 : tamanho;

    // Em qualquer tamanho que fecha, cada pessoa joga uma vez com cada uma das outras.
    public static int JogosPorPessoa(int tamanho) => tamanho - 1;

    // Quantas partidas um grupo desse tamanho gera (cada jogo forma 2 das duplas).
    public static int PartidasDe(int tamanho) => tamanho * (tamanho - 1) / 4;

    public record Divisao(int Grupos, int PorGrupo, int PassamPorGrupo)
    {
        public int Pessoas => Grupos * PorGrupo;
        public bool TemGrupoFinal => Grupos > 1;

        public int TamanhoDoGrupoFinal => TemGrupoFinal ? Grupos * PassamPorGrupo : 0;

        public int RodadasNaFaseDeGrupos => RodadasDe(PorGrupo);
        public int RodadasNoGrupoFinal => TemGrupoFinal ? RodadasDe(TamanhoDoGrupoFinal) : 0;
        public int RodadasTotais => RodadasNaFaseDeGrupos + RodadasNoGrupoFinal;

        public int PartidasTotais =>
            Grupos * PartidasDe(PorGrupo) + (TemGrupoFinal ? PartidasDe(TamanhoDoGrupoFinal) : 0);
    }

    // Quantos passam de cada grupo pro grupo final: o MENOR número que faz o grupo final
    // fechar. Com 2, 4 ou 6 grupos, passam 2 de cada (4, 8 ou 12 finalistas). Com 3 grupos,
    // 2 de cada dariam 6 finalistas — que não fecha —, então passam 3, e o grupo final tem 9.
    //
    // Null = não há número que sirva sem classificar o grupo inteiro. Uma divisão assim não é
    // oferecida: passar todo mundo não é fase classificatória, é jogar o torneio duas vezes.
    public static int? QuantosPassam(int grupos, int porGrupo)
    {
        if (grupos < 2) return null;

        for (int passam = 2; passam < porGrupo; passam++)
        {
            if (TamanhoFecha(grupos * passam)) return passam;
        }
        return null;
    }

    // Todas as formas de dividir `pessoas`, da que MAIS MISTURA (menos grupos, portanto
    // grupos maiores) pra que menos. A primeira da lista é a que mais se parece com o
    // Americano clássico; as últimas são as que cabem numa noite.
    public static IReadOnlyList<Divisao> Possiveis(int pessoas)
    {
        var achadas = new List<Divisao>();
        if (pessoas < 4) return achadas;

        for (int grupos = 1; grupos <= pessoas / 4; grupos++)
        {
            if (pessoas % grupos != 0) continue;

            int porGrupo = pessoas / grupos;
            if (!TamanhoFecha(porGrupo)) continue;

            if (grupos == 1)
            {
                achadas.Add(new Divisao(1, porGrupo, 0));
                continue;
            }

            var passam = QuantosPassam(grupos, porGrupo);
            if (passam != null) achadas.Add(new Divisao(grupos, porGrupo, passam.Value));
        }

        return achadas;
    }

    // O número de inscritos serve pra alguma coisa?
    public static bool Aceita(int pessoas) => Possiveis(pessoas).Count > 0;

    // Os números aceitos mais próximos, pra baixo e pra cima. É o que transforma "não dá" em
    // "chame mais um" ou "tire um" — sem isso o organizador fica sabendo do problema e não da
    // saída, no dia do torneio.
    public static (int? Abaixo, int? Acima) MaisProximosQueFecham(int pessoas)
    {
        int? abaixo = null;
        for (int n = pessoas - 1; n >= 4; n--)
        {
            if (Aceita(n)) { abaixo = n; break; }
        }

        int? acima = null;
        for (int n = pessoas + 1; n <= pessoas + 12; n++)
        {
            if (Aceita(n)) { acima = n; break; }
        }

        return (abaixo, acima);
    }

    // ---- As frases que a tela mostra ------------------------------------------------

    public static string ComoFica(Divisao d)
    {
        if (!d.TemGrupoFinal)
        {
            return $"1 grupo de {d.PorGrupo} — {d.RodadasNaFaseDeGrupos} rodadas, "
                 + $"{JogosPorPessoa(d.PorGrupo)} jogos pra cada um.";
        }

        return $"{d.Grupos} grupos de {d.PorGrupo} — {d.RodadasNaFaseDeGrupos} rodadas, "
             + $"{JogosPorPessoa(d.PorGrupo)} jogos pra cada um. "
             + $"Passam {d.PassamPorGrupo} de cada grupo e os {d.TamanhoDoGrupoFinal} "
             + $"disputam o grupo final ({d.RodadasNoGrupoFinal} rodadas).";
    }

    // O aviso de quando o número não fecha.
    public static string PorQueNaoFecha(int pessoas)
    {
        if (pessoas < 4)
        {
            return "O Americano precisa de pelo menos 4 pessoas — é o que fecha uma quadra.";
        }

        var (abaixo, acima) = MaisProximosQueFecham(pessoas);
        var saidas = new List<string>();
        if (acima != null) saidas.Add($"chame mais {acima.Value - pessoas} e feche em {acima.Value}");
        if (abaixo != null) saidas.Add($"ou jogue com {abaixo.Value}");

        return $"Com {pessoas} pessoas não dá pra todo mundo jogar a mesma quantidade de jogos "
             + $"sem repetir parceiro. "
             + (saidas.Count > 0 ? char.ToUpper(saidas[0][0]) + saidas[0][1..] + " " + string.Join(" ", saidas.Skip(1)) + "." : "");
    }
}
