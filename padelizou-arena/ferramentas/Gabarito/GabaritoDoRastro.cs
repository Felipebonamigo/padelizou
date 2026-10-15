using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// O rastro-fantasma (RastroFantasma.Prever) de algumas bolas: saída de fundo, saída cruzada pela lateral, bola curta,
/// vidro antes do quique, o pedido no passo do vidro (o nível Padrão), o teto de 3 s, a bola fora de jogo e o lado −1.
/// Grava quantos pontos, o primeiro depois da parede e cada ponto, pro PadelSim reproduzir bit a bit.
/// </summary>
static class GabaritoDoRastro
{
    private sealed record Caso(string Nome, float X, float Y, float Z, Velocidade V, int Lado, bool EmJogo = true, bool AteOVidro = false, bool JaQuicou = false);

    /// <summary>Uma bola junto ao vidro de fundo que, no primeiro passo, quica (o 2º quique) e bate no vidro (revisão de 15/10).</summary>
    private static Caso QuiqueEVidroNoMesmoPasso()
    {
        for (float y0 = 9.80f; y0 < 9.999f; y0 += 0.0005f)
            for (float z0 = 0.005f; z0 < 0.2f; z0 += 0.005f)
            {
                var b = new Bola();
                b.Posicionar(0, y0, z0);
                var v = new Velocidade(0, 12, -3, 0);
                b.Lancar(v);
                var eventos = new List<EventoDaBola>();
                b.Avancar(RastroFantasma.Passo, eventos);
                if (eventos.Count >= 2 && eventos[0].Tipo == TipoDeEventoDaBola.Quique && eventos[1].Tipo == TipoDeEventoDaBola.Parede)
                    return new Caso("2º quique e vidro no mesmo passo", 0, y0, z0, v, 1, JaQuicou: true);
            }
        throw new InvalidOperationException("a cena do quique e vidro no mesmo passo sumiu");
    }

    private static readonly Caso[] Casos =
    [
        new("saída de fundo", 1, -8, 1, new Velocidade(0, 20, 3, 0), 1),
        new("bola curta, dois quiques antes do vidro", 1, -8, 1, new Velocidade(0, 11, 4, 0), 1),
        new("vidro antes do quique", 1, -8, 1, new Velocidade(0, 28, 3, 0), 1),
        new("pedido no passo do vidro (Padrão)", 1, -8, 1, new Velocidade(0, 20, 3, 0), 1, AteOVidro: true),
        new("cruzada com topspin e sidespin", -3, -8, 1.1f, new Velocidade(5.5f, 19, 3.2f, 0, -150, 0, 40), 1),
        new("lob alto que passa de 3 s", 0, 5, 1, new Velocidade(0, 0.5f, 16, 0), 1),
        new("bola fora de jogo", 0, 5, 1, new Velocidade(0, 0, 0, 0), 1, EmJogo: false),
        new("saída de fundo do lado −1", -1, 8, 1, new Velocidade(0, -20, 3, 0), -1),
        QuiqueEVidroNoMesmoPasso(),
    ];

    public static void Gravar(string pasta, string commit)
    {
        var ci = CultureInfo.InvariantCulture;
        string F(float f) => f.ToString("R", ci);
        string Cabecalho(string colunas) => $"# gabarito do Padel.Core em {commit}\n{colunas}\n";
        var sbCasos = new StringBuilder(Cabecalho("id,nome,x,y,z,vx,vy,vz,wx,wy,wz,lado,emJogo,passosAntes,jaQuicou,pontos,primeiroDepoisDaParede"));
        var sbPontos = new StringBuilder(Cabecalho("id,i,x,y,z,t"));
        for (int id = 0; id < Casos.Length; id++)
        {
            var c = Casos[id];
            var bola = new Bola();
            bola.Posicionar(c.X, c.Y, c.Z);
            if (c.EmJogo) bola.Lancar(c.V);
            // O nível Padrão pede o rastro no passo em que a bola bate no vidro, já quicada.
            int passosAntes = 0;
            bool jaQuicou = c.JaQuicou;
            if (c.AteOVidro)
            {
                var eventos = new List<EventoDaBola>();
                bool bateu = false;
                while (!bateu)
                {
                    eventos.Clear();
                    bola.Avancar(RastroFantasma.Passo, eventos);
                    passosAntes++;
                    foreach (var e in eventos)
                    {
                        if (e.Tipo == TipoDeEventoDaBola.Quique) jaQuicou = true;
                        if (e.Tipo == TipoDeEventoDaBola.Parede) bateu = true;
                    }
                }
            }
            var rastro = RastroFantasma.Prever(bola, c.Lado, jaQuicou);
            var v = c.V;
            sbCasos.AppendLine(string.Join(',', id, c.Nome.Replace(',', ';'), F(c.X), F(c.Y), F(c.Z), F(v.Vx), F(v.Vy), F(v.Vz), F(v.Wx), F(v.Wy), F(v.Wz),
                c.Lado, c.EmJogo ? 1 : 0, passosAntes, jaQuicou ? 1 : 0, rastro.Pontos.Count, rastro.PrimeiroDepoisDaParede));
            for (int i = 0; i < rastro.Pontos.Count; i++)
            {
                var p = rastro.Pontos[i];
                sbPontos.AppendLine(string.Join(',', id, i, F(p.X), F(p.Y), F(p.Z), F(p.T)));
            }
            Console.WriteLine($"rastro {id} ({c.Nome}): {rastro.Pontos.Count} pontos, saída de parede no {rastro.PrimeiroDepoisDaParede}");
        }
        File.WriteAllText(Path.Combine(pasta, "rastros.csv"), sbCasos.ToString());
        File.WriteAllText(Path.Combine(pasta, "rastros-pontos.csv"), sbPontos.ToString());
    }
}
