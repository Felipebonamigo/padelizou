using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// O PassoFixo (tempo do quadro → passos de 120 Hz, sem deriva e sem perder aperto) com sequências de quadros: 60 Hz
/// redondo, 144 Hz, quadros irregulares, quadro zero/negativo/infinito/NaN e o quadro gigante. Grava quantos passos cada
/// quadro deu e a entrada de cada passo, com os botões dos dois controles (A e B; X, Y e RB do de quatro botões).
/// </summary>
static class GabaritoDoPassoFixo
{
    public static void Gravar(string pasta, string commit)
    {
        var ci = CultureInfo.InvariantCulture;
        string D(double d) => d.ToString("R", ci);
        string F(float f) => f.ToString("R", ci);
        string B(bool b) => b ? "1" : "0";
        var sb = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,quadro,delta,dx,dy,acao,segurada,lob,cortado,ataque,ataqueSegurado,modificador,passos," +
            "passo,pdx,pdy,pacao,psegurada,plob,pcortado,pataque,pataqueSegurado,pmodificador\n");
        var sequencias = new List<double[]>
        {
            Enumerable.Repeat(1.0 / 60, 5000).ToArray(),     // 60 Hz por ~83 s: deriva apareceria aqui
            Enumerable.Repeat(1.0 / 144, 3000).ToArray(),
            Enumerable.Repeat(1.0 / 120, 3000).ToArray(),    // o caso do quadro 2301 do Godot
            Enumerable.Range(0, 2000).Select(i => (i % 7 + 3) / 600.0).ToArray(),
            new double[] { 0.0, -0.01, double.PositiveInfinity, double.NaN, 1.0 / 60, 5.0, 1.0 / 60, 1e-9, 1.0 / 240, 1.0 / 240 },
        };
        for (int id = 0; id < sequencias.Count; id++)
        {
            var p = new PassoFixo(120);
            for (int q = 0; q < sequencias[id].Length; q++)
            {
                double delta = sequencias[id][q];
                // Apertos esparsos e direção que muda: o que interessa é qual passo leva o aperto.
                var e = new Entrada((q % 11 - 5) / 5f, (q % 3 - 1) * 0.5f, AcaoPressionada: q % 13 == 0, AcaoSegurada: q % 4 < 2, LobPressionada: q % 17 == 5,
                    CortadoPressionado: q % 19 == 7, AtaquePressionado: q % 23 == 11, AtaqueSegurado: q % 5 < 2, ModificadorSegurado: q % 6 < 3);
                int passos = p.Quadro(delta, e);
                string quadro = string.Join(',', id, q, D(delta), F(e.Dx), F(e.Dy), B(e.AcaoPressionada), B(e.AcaoSegurada), B(e.LobPressionada),
                    B(e.CortadoPressionado), B(e.AtaquePressionado), B(e.AtaqueSegurado), B(e.ModificadorSegurado), passos);
                if (passos == 0) { sb.AppendLine($"{quadro},-1,0,0,0,0,0,0,0,0,0"); continue; }
                for (int i = 0; i < passos; i++)
                {
                    var x = p.EntradaDoPasso(i);
                    sb.AppendLine($"{quadro},{i},{F(x.Dx)},{F(x.Dy)},{B(x.AcaoPressionada)},{B(x.AcaoSegurada)},{B(x.LobPressionada)}," +
                        $"{B(x.CortadoPressionado)},{B(x.AtaquePressionado)},{B(x.AtaqueSegurado)},{B(x.ModificadorSegurado)}");
                }
            }
        }
        File.WriteAllText(Path.Combine(pasta, "passo-fixo.csv"), sb.ToString());
    }
}
