using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// Golpes do humano em cenas montadas, pros caminhos que as partidas do gabarito não exercitam (revisão de 15/10): A
/// segurado, por 3, smash de recurso longe da rede, botões no mesmo passo, víbora com o RB e a janela do timing do Padrão
/// e do Casual (apertando cedo e tarde). A cena é a do ControleDeQuatroBotoesTests: o rival acabou de bater, a bola vem
/// pro humano parado, e ele aperta no passo de referência (quando falta o momento ideal do balanço pra bola entrar no
/// alcance confortável) menos o adiantamento. Grava o golpe, o passo, a velocidade e o efeito da bola e o erro, em bits.
/// </summary>
static class GabaritoDosGolpesDoHumano
{
    private const float Passo = 1f / 120f;

    private sealed record Cena(string Nome, bool Alta, float Jy, string Botoes, bool SeguraA = false, bool SeguraY = false, bool Rb = false,
        float Dx = 0, float Dy = 0, Assistencia Assistencia = Assistencia.Pro, int Adiantamento = 0);

    private static readonly Cena[] Cenas =
    [
        new("baixa, A segurado", false, 8, "A", SeguraA: true),
        new("alta, A segurado", true, 2.5f, "A", SeguraA: true),
        new("alta, Y segurado com lado forte (por 3)", true, 1.5f, "Y", SeguraY: true, Dx: -1, Dy: -1),
        new("alta, Y segurado longe da rede (smash)", true, 4.5f, "Y", SeguraY: true, Dy: -1),
        new("alta, X e Y no mesmo passo", true, 2.5f, "XY"),
        new("baixa, B e Y no mesmo passo", false, 8, "BY"),
        new("baixa, A e X no mesmo passo", false, 8, "AX"),
        new("alta, Y segurado com RB (víbora)", true, 2.5f, "Y", SeguraY: true, Rb: true, Dx: 0.8f, Dy: -1),
        new("baixa, Y com RB (ataque)", false, 8, "Y", Rb: true),
        new("alta, A com lado forte", true, 2.5f, "A", Dx: 1),
        new("baixa, A, Pro, 5 passos cedo", false, 8, "A", Adiantamento: 5),
        new("baixa, A, Casual, 5 passos cedo", false, 8, "A", Assistencia: Assistencia.Casual, Adiantamento: 5),
        new("baixa, A, Casual, 4 passos tarde", false, 8, "A", Assistencia: Assistencia.Casual, Adiantamento: -4),
        new("baixa, A, Padrão, 3 passos cedo", false, 8, "A", Assistencia: Assistencia.Padrao, Adiantamento: 3),
        new("alta, X, Casual, 4 passos cedo", true, 2.5f, "X", Assistencia: Assistencia.Casual, Adiantamento: 4),
    ];

    public static void Gravar(string pasta, string commit)
    {
        var ci = CultureInfo.InvariantCulture;
        string F(float f) => f.ToString("R", ci);
        string B(bool b) => b ? "1" : "0";
        var sb = new StringBuilder($"# gabarito do Padel.Core em {commit}\n" +
            "id,semente,nome,alta,jy,botoes,seguraA,seguraY,rb,dx,dy,assistencia,adiantamento,tipo,passo,vx,vy,vz,wx,wy,wz,erro\n");
        int id = 0, golpes = 0;
        foreach (uint semente in new uint[] { 21, 22, 23 })
            foreach (var c in Cenas)
            {
                var (tipo, passo, bola, erro) = Rodar(semente, c);
                if (tipo >= 0) golpes++;
                sb.AppendLine(string.Join(',', id++, semente, c.Nome.Replace(',', ';'), B(c.Alta), F(c.Jy), c.Botoes, B(c.SeguraA), B(c.SeguraY), B(c.Rb),
                    F(c.Dx), F(c.Dy), (int)c.Assistencia, c.Adiantamento, tipo, passo,
                    F(bola.Vx), F(bola.Vy), F(bola.Vz), F(bola.Wx), F(bola.Wy), F(bola.Wz), F(erro)));
            }
        File.WriteAllText(Path.Combine(pasta, "golpes-do-humano.csv"), sb.ToString());
        Console.WriteLine($"golpes do humano: {id} cenas, {golpes} com golpe");
    }

    private static (int Tipo, int Passo, Bola Bola, float Erro) Rodar(uint semente, Cena c)
    {
        var partida = new Partida(new OpcoesDaPartida
        {
            Semente = semente, Humanos = [true, false, false, false], Controle = Controle.QuatroBotoes, Assistencia = c.Assistencia,
        });
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(0, 0, true, true), vazia, vazia, vazia]);
        var humano = partida.Jogadores[0];
        float jx = 2.0f, jy = c.Jy, bx = 2.6f, by = c.Alta ? c.Jy : 5.5f, bz = c.Alta ? 3.3f : 1.0f;
        var v = c.Alta ? new Velocidade(0, 0, 0, 0) : new Velocidade(0, 7, 1.5f, 0);
        partida.Arbitro.RegistrarGolpe(1);
        if (!c.Alta) partida.Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, bx, by, 0, 1));
        humano.X = jx; humano.Y = jy; humano.Vx = 0; humano.Vy = 0; humano.Cooldown = 0;
        partida.Bola.Posicionar(bx, by, bz);
        partida.Bola.Lancar(v);

        // O passo de referência: a bola anda igual até alguém bater, então dá pra achá-lo numa cópia dela.
        int referencia = -1;
        var copia = partida.Bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (int i = 0; i < 240 && referencia < 0; i++)
        {
            float falta = TempoAteOAlcance(copia, humano);
            if (falta >= 0 && falta <= Jogador.MomentoIdealDoBalanco) referencia = i;
            copia.Avancar(Passo, eventos);
        }
        int aperto = referencia - c.Adiantamento;

        int tipo = -1, passoDoGolpe = -1, passo = 0;
        var bolaNoGolpe = new Bola();
        float erro = 0;
        partida.Evento += ev =>
        {
            if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == humano && tipo < 0)
            {
                tipo = (int)ev.Golpe!.Value; passoDoGolpe = passo; bolaNoGolpe = partida.Bola.Clonar(); erro = partida.UltimoErroDoHumano;
            }
        };
        bool Tem(char b) => c.Botoes.Contains(b);
        for (passo = 0; passo < 240 && tipo < 0 && partida.Estado == EstadoDaPartida.Rally; passo++)
        {
            bool apertar = passo == aperto, depois = passo >= aperto;
            var e = !depois ? vazia : new Entrada(c.Dx, c.Dy,
                AcaoPressionada: apertar && Tem('A'), AcaoSegurada: Tem('A') && (apertar || c.SeguraA),
                LobPressionada: apertar && Tem('B'),
                CortadoPressionado: apertar && Tem('X'),
                AtaquePressionado: apertar && Tem('Y'), AtaqueSegurado: Tem('Y') && (apertar || c.SeguraY),
                ModificadorSegurado: c.Rb);
            partida.Avancar(Passo, [e, vazia, vazia, vazia]);
        }
        return (tipo, passoDoGolpe, bolaNoGolpe, erro);
    }

    private static float TempoAteOAlcance(Bola bola, Jogador j)
    {
        var clone = bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < 3; t += Passo)
        {
            if (j.AlcancaConfortavelmente(clone)) return t;
            clone.Avancar(Passo, eventos);
        }
        return -1;
    }
}
