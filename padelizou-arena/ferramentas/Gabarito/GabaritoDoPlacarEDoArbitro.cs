using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// O que as partidas do gabarito não alcançam: tie-break, vários sets, vantagem longa, pontos corridos com empate
/// (placar) e falta, let, dupla falta, parede própria no saque, saque fora da caixa (árbitro). Sequências sorteadas,
/// gravadas depois de cada passo.
/// </summary>
static class GabaritoDoPlacarEDoArbitro
{
    public static void Gravar(string pasta, string commit)
    {
        var ci = CultureInfo.InvariantCulture;
        string F(float f) => f.ToString("R", ci);
        string B(bool b) => b ? "1" : "0";

        // ---- Placar: (ponto de ouro, sets pra vencer, pontos corridos, time que saca, chance do time 0 ganhar o ponto;
        // negativa = os pontos alternam).
        var configs = new (bool Ouro, int Sets, int? Corridos, int Saca, float Chance)[]
        {
            (true, 1, null, 0, 0.5f), (true, 2, null, 1, 0.5f), (false, 1, null, 0, 0.5f), (false, 3, null, 0, 0.55f),
            (true, 3, null, 1, 0.45f), (false, 2, null, 1, 0.5f), (true, 1, null, 0, 0.5f), (true, 2, null, 0, 0.5f),
            (false, 1, 24, 0, 0.5f), (false, 1, 16, 1, 0.5f), (false, 1, 21, 0, 0.6f), (false, 1, 32, 1, 0.5f),
            (false, 1, 24, 1, -1f), (true, 1, null, 0, -1f),   // chance negativa: os pontos alternam (empate garantido)
        };
        var sbPlacar = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,ouro,sets,corridos,saca,ponto,para,evento,timeDoEvento,pontos0,pontos1,games0,games1,sets0,sets1,tieBreak,vencedor,empate,sacadorTime,sacadorJogador,ladoDoSaque,decisivo,texto0,texto1,resumo\n");
        for (int id = 0; id < configs.Length; id++)
        {
            var c = configs[id];
            var r = new Aleatorio((uint)(777 + id));
            var p = c.Corridos is int total ? Placar.DePontosCorridos(total, c.Saca) : new Placar(c.Ouro, c.Sets, c.Saca);
            for (int ponto = 0; ponto < 2000 && !p.Acabou; ponto++)
            {
                int para = c.Chance < 0 ? ponto % 2 : r.Proximo() < c.Chance ? 0 : 1;
                var ev = p.PontoPara(para);
                sbPlacar.AppendLine(string.Join(',', id, B(c.Ouro), c.Sets, c.Corridos ?? -1, c.Saca, ponto, para, (int)ev.Tipo, ev.Time,
                    p.Pontos[0], p.Pontos[1], p.Games[0], p.Games[1], p.Sets[0], p.Sets[1], B(p.EmTieBreak), p.Vencedor ?? -1, B(p.Empate),
                    p.Sacador.Time, p.Sacador.Jogador, (int)p.LadoDoSaque, B(p.EmPontoDecisivo), p.TextoDosPontos(0), p.TextoDosPontos(1), p.Resumo()));
            }
            // Um ponto depois do fim: Encerrada.
            var depois = p.PontoPara(0);
            sbPlacar.AppendLine(string.Join(',', id, B(c.Ouro), c.Sets, c.Corridos ?? -1, c.Saca, -1, 0, (int)depois.Tipo, depois.Time,
                p.Pontos[0], p.Pontos[1], p.Games[0], p.Games[1], p.Sets[0], p.Sets[1], B(p.EmTieBreak), p.Vencedor ?? -1, B(p.Empate),
                p.Sacador.Time, p.Sacador.Jogador, (int)p.LadoDoSaque, B(p.EmPontoDecisivo), p.TextoDosPontos(0), p.TextoDosPontos(1), p.Resumo()));
        }
        File.WriteAllText(Path.Combine(pasta, "placar.csv"), sbPlacar.ToString());

        // ---- Árbitro: sequências de ações (saque, golpe, evento da bola, novo ponto), sorteadas com a lógica do jogo por perto.
        var sbArb = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,passo,acao,time,ladoDaCaixa,direita,tipo,x,y,z,lado,parede,superficie,porta,decisao,para,motivo,faltas,emSaque,golpeador,quicou,tocou,cruzou,podeGolpear0,podeGolpear1\n");
        var ra = new Aleatorio(4242);
        for (int id = 0; id < 400; id++)
        {
            var a = new Arbitro();
            int passos = 3 + (int)(ra.Proximo() * 10);
            for (int k = 0; k < passos; k++)
            {
                float s = ra.Proximo();
                string acao; int time = -1, ladoDaCaixa = 0; bool direita = false; string evento = ",,,,,,,";
                Decisao? d = null;
                if (k == 0 || s < 0.12f)
                {
                    time = ra.Proximo() < 0.5f ? 0 : 1; direita = ra.Proximo() < 0.5f; ladoDaCaixa = -Quadra.LadoDoTime(time);
                    a.IniciarSaque(time, Quadra.CaixaDeSaque(ladoDaCaixa, direita)); acao = "saque";
                }
                else if (s < 0.25f) { time = ra.Proximo() < 0.5f ? 0 : 1; a.RegistrarGolpe(time); acao = "golpe"; }
                else if (s < 0.28f) { a.NovoPonto(); acao = "novo"; }
                else
                {
                    var tipo = (TipoDeEventoDaBola)(int)(ra.Proximo() * 5);
                    float x = (ra.Proximo() - 0.5f) * 10, y = (ra.Proximo() - 0.5f) * 20, z = ra.Proximo() * 4;
                    int lado = ra.Proximo() < 0.5f ? 1 : -1;
                    var parede = ra.Proximo() < 0.5f ? QualParede.Lateral : QualParede.Fundo;
                    var sup = tipo == TipoDeEventoDaBola.Parede ? (ra.Proximo() < 0.5f ? Superficie.Vidro : Superficie.Grade)
                        : tipo == TipoDeEventoDaBola.Saiu ? Superficie.Aberto : Superficie.Chao;
                    bool porta = tipo == TipoDeEventoDaBola.Saiu && ra.Proximo() < 0.3f;
                    var e = new EventoDaBola(tipo, x, y, z, lado, parede, sup, porta);
                    d = a.Processar(e); acao = "evento";
                    evento = string.Join(',', (int)tipo, F(x), F(y), F(z), lado, (int)parede, (int)sup, B(porta));
                }
                sbArb.AppendLine(string.Join(',', id, k, acao, time, ladoDaCaixa, B(direita), evento,
                    d is Decisao dd ? (int)dd.Tipo : -1, d is Decisao d2 ? d2.Para : -2, d is Decisao d3 ? (int)d3.Motivo : -1,
                    a.Faltas, B(a.EmSaque), a.Golpeador ?? -1, B(a.QuicouNoReceptor), B(a.TocouARede), B(a.Cruzou), B(a.PodeGolpear(0)), B(a.PodeGolpear(1))));
            }
        }
        File.WriteAllText(Path.Combine(pasta, "arbitro.csv"), sbArb.ToString());
    }
}
