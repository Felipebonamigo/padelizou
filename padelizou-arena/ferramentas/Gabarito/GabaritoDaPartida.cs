using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// Partidas inteiras simuladas pelo Padel.Core, a 120 passos por segundo: um retrato do estado a cada segundo de jogo
/// e todos os eventos da partida, pro PadelSim (C++) reproduzir. Cobre placar, árbitro, jogadores, IA, golpes
/// especiais e o humano simulado (o caminho do jogador humano: balanço, instante do contato, golpe pela entrada).
/// </summary>
static class GabaritoDaPartida
{
    public const float Passo = 1f / 120f;
    public const int PassosPorRetrato = 120;
    public const int PassosMaximos = 120 * 60 * 40;   // 40 min de jogo: nenhuma partida de 1 set passa disso

    public sealed record Caso(string Nome, OpcoesDaPartida Opcoes, PerfilDeHumano? Humano = null);

    public static readonly Caso[] Casos =
    [
        new("IA x IA, Médio", new OpcoesDaPartida { Dificuldade = Dificuldade.Medio, Humanos = OpcoesDaPartida.NinguemHumano, Semente = 42 }),
        new("IA x IA, Fácil", new OpcoesDaPartida { Dificuldade = Dificuldade.Facil, Humanos = OpcoesDaPartida.NinguemHumano, Semente = 7 }),
        new("IA x IA, Difícil", new OpcoesDaPartida { Dificuldade = Dificuldade.Dificil, Humanos = OpcoesDaPartida.NinguemHumano, Semente = 9 }),
        new("humano Intermediário x Médio, manual", new OpcoesDaPartida { Dificuldade = Dificuldade.Medio, Semente = 11 }, PerfilDeHumano.Intermediario),
        new("humano Avançado x Difícil, automático", new OpcoesDaPartida { Dificuldade = Dificuldade.Dificil, ModoDeGolpe = ModoDeGolpe.Automatico, Semente = 13 }, PerfilDeHumano.Avancado),
        new("pontos corridos 16, IA x IA", new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, PontosCorridos = 16, Semente = 5 }),
        new("vantagem, humano Iniciante x Fácil", new OpcoesDaPartida { Dificuldade = Dificuldade.Facil, PontoDeOuro = false, Semente = 21 }, PerfilDeHumano.Iniciante),
        new("formato curto, humano Avançado x Médio, manual", new OpcoesDaPartida { GamesPorSet = 4, PontosDoTieBreak = 10, Semente = 31 }, PerfilDeHumano.Avancado),
        // Semente escolhida pra o humano passar por todos os golpes do controle (inclusive víbora e por 4) e o set ir ao super tie-break.
        new("formato curto, quatro botões, humano Avançado x Médio", new OpcoesDaPartida { GamesPorSet = 4, PontosDoTieBreak = 10, Controle = Controle.QuatroBotoes, Semente = 126 }, PerfilDeHumano.Avancado),
    ];

    public static void Gravar(string pasta, string commit)
    {
        var ci = CultureInfo.InvariantCulture;
        string F(float f) => f.ToString("R", ci);
        string B(bool b) => b ? "1" : "0";
        string Cabecalho(string colunas) => $"# gabarito do Padel.Core em {commit}\n{colunas}\n";

        var sbCasos = new StringBuilder(Cabecalho("id,nome,dificuldade,pontoDeOuro,setsParaVencer,gamesPorSet,pontosDoTieBreak,humano0,perfilDoHumano,modoDeGolpe,semente,pontosCorridos,passos,resumo,vencedor,empate,pontos,golpes,maiorRally,faltas,lets,controle"));
        var sbRetratos = new StringBuilder(Cabecalho(
            "id,passo,estado,temporizador,tempoDeJogo,rally,ultimoErro,bx,by,bz,bvx,bvy,bvz,bwx,bwy,bwz,emJogo,rolando,parada," +
            "pontos0,pontos1,games0,games1,sets0,sets1,tieBreak,sacadorTime,sacadorJogador,faltas,golpeador,quicou,formacao0,formacao1," +
            string.Join(',', Enumerable.Range(0, 4).Select(i => $"j{i}x,j{i}y,j{i}vx,j{i}vy,j{i}balanco,j{i}tempoNoBalanco,j{i}cooldown,j{i}golpes"))));
        var sbEventos = new StringBuilder(Cabecalho("id,passo,tipo,jogador,time,motivo,golpe,lado"));

        for (int id = 0; id < Casos.Length; id++)
        {
            var caso = Casos[id];
            var p = new Partida(caso.Opcoes);
            int passo = 0;
            p.Evento += e => sbEventos.AppendLine(string.Join(',', id, passo, (int)e.Tipo,
                e.Jogador is null ? -1 : e.Jogador.Time * 2 + e.Jogador.Indice, e.Time,
                e.Motivo is Motivo m ? (int)m : -1, e.Golpe is TipoDeGolpe g ? (int)g : -1, e.Lado is LadoDoGolpe l ? (int)l : -1));
            HumanoSimulado? humano = caso.Humano is null ? null
                : new HumanoSimulado(0, caso.Humano, new Aleatorio(caso.Opcoes.Semente!.Value + 1000), controle: caso.Opcoes.Controle);
            var entradas = new Entrada[4];

            void Retrato()
            {
                var b = p.Bola;
                var s = new List<string>
                {
                    id.ToString(ci), passo.ToString(ci), ((int)p.Estado).ToString(ci), F(p.Temporizador), F(p.TempoDeJogo), p.RallyAtual.ToString(ci), F(p.UltimoErroDoHumano),
                    F(b.X), F(b.Y), F(b.Z), F(b.Vx), F(b.Vy), F(b.Vz), F(b.Wx), F(b.Wy), F(b.Wz), B(b.EmJogo), B(b.Rolando), B(b.Parada),
                    p.Placar.Pontos[0].ToString(ci), p.Placar.Pontos[1].ToString(ci), p.Placar.Games[0].ToString(ci), p.Placar.Games[1].ToString(ci),
                    p.Placar.Sets[0].ToString(ci), p.Placar.Sets[1].ToString(ci), B(p.Placar.EmTieBreak), p.Placar.Sacador.Time.ToString(ci), p.Placar.Sacador.Jogador.ToString(ci),
                    p.Arbitro.Faltas.ToString(ci), (p.Arbitro.Golpeador ?? -1).ToString(ci), B(p.Arbitro.QuicouNoReceptor),
                    ((int)p.IAs[0].Posicao).ToString(ci), ((int)p.IAs[1].Posicao).ToString(ci),
                };
                foreach (var j in p.Jogadores)
                    s.AddRange([F(j.X), F(j.Y), F(j.Vx), F(j.Vy), F(j.Balanco), F(j.TempoNoBalanco), F(j.Cooldown), j.Golpes.ToString(ci)]);
                sbRetratos.AppendLine(string.Join(',', s));
            }

            Retrato();
            while (!p.Acabou && passo < PassosMaximos)
            {
                passo++;
                if (humano is not null) entradas[0] = humano.Decidir(EstadoVisivel.De(p), Passo);
                p.Avancar(Passo, entradas);
                if (passo % PassosPorRetrato == 0 || p.Acabou) Retrato();
            }
            var o = caso.Opcoes;
            var est = p.Estatisticas;
            sbCasos.AppendLine(string.Join(',', id, caso.Nome.Replace(',', ';'), (int)o.Dificuldade, B(o.PontoDeOuro), o.SetsParaVencer, o.GamesPorSet, o.PontosDoTieBreak, B(caso.Humano is not null),
                caso.Humano?.Nome ?? "-", (int)o.ModoDeGolpe, o.Semente, o.PontosCorridos ?? -1, passo, p.Placar.Resumo(), p.Placar.Vencedor ?? -1, B(p.Placar.Empate),
                est.Pontos, est.Golpes, est.MaiorRally, est.Faltas, est.Lets, (int)o.Controle));
            Console.WriteLine($"partida {id} ({caso.Nome}): {passo} passos, {p.Placar.Resumo()}");
        }
        File.WriteAllText(Path.Combine(pasta, "partidas.csv"), sbCasos.ToString());
        File.WriteAllText(Path.Combine(pasta, "partidas-retratos.csv"), sbRetratos.ToString());
        File.WriteAllText(Path.Combine(pasta, "partidas-eventos.csv"), sbEventos.ToString());
    }
}
