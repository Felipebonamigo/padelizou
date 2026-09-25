using Padel.Core;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// Monta o RetratoDaPartida (o que a tela desenha) a partir da VisaoDaPartida do Padel.Core.Rede — a mesma coisa pro
/// host (a partida dele, sem atraso) e pro cliente (os outros interpolados, o próprio jogador predito). Guarda também o
/// que o humano simulado precisa ver (quem bateu por último, se a bola já quicou), a partir dos eventos numerados.
/// </summary>
public sealed class RetratoDaVisao
{
    private readonly RetratoDaPartida _retrato;
    private int _timeDoUltimoGolpe = -1;
    private TipoDeGolpe? _tipoDoUltimoGolpe;
    private bool _quicouDepoisDoUltimoGolpe;

    public RetratoDaVisao(RetratoDaPartida retrato) => _retrato = retrato;

    public VisaoDaPartida? Ultima { get; private set; }

    public void Preencher(VisaoDaPartida v, IReadOnlyList<string> nomes, int? pingMs)
    {
        Ultima = v;
        var r = _retrato;
        r.TempoDeJogo = (float)(v.Tick / Protocolo.TicksPorSegundo);
        r.Estado = v.Estado;
        r.Bola.X = v.Bola.X; r.Bola.Y = v.Bola.Y; r.Bola.Z = v.Bola.Z; r.Bola.EmJogo = v.Bola.EmJogo;
        for (int i = 0; i < 4 && i < v.Jogadores.Count; i++)
        {
            var j = v.Jogadores[i];
            var rj = r.Jogadores[i];
            rj.Indice = i; rj.Time = i / 2; rj.Lado = Quadra.LadoDoTime(i / 2);
            rj.Nome = i < nomes.Count && !string.IsNullOrWhiteSpace(nomes[i]) ? nomes[i] : NomePadrao(i);
            rj.X = j.X; rj.Y = j.Y; rj.Vx = j.Vx; rj.Vy = j.Vy;
            rj.Humano = j.Humano;
            rj.Local = i == v.IndiceLocal;
            rj.FaseDoBalanco = j.Balancando ? Math.Clamp(j.TempoNoBalanco / Jogador.DuracaoDoBalanco, 0f, 1f) : 0f;
            rj.BalancoDeLob = j.BalancoDeLob;
        }
        var p = v.Placar;
        var pl = r.Placar;
        for (int t = 0; t < 2; t++) { pl.Sets[t] = p.Sets[t]; pl.Games[t] = p.Games[t]; pl.Pontos[t] = p.TextoDosPontos(t); }
        if (pl.SetsAnteriores.Count != p.SetsAnteriores.Count) { pl.SetsAnteriores.Clear(); pl.SetsAnteriores.AddRange(p.SetsAnteriores); }
        pl.TimeSacando = p.Sacador.Time;
        pl.EmTieBreak = p.EmTieBreak;
        pl.EmPontoDecisivo = p.EmPontoDecisivo;
        pl.Vencedor = p.Vencedor;
        pl.Resumo = p.Resumo();
        r.Mensagem = v.Mensagem?.Texto ?? "";
        r.MensagemEmDestaque = v.Mensagem?.Destaque == true;
        r.MensagemSuave = v.Mensagem?.Suave == true;
        r.CaixaDoSaque = v.Estado == EstadoDaPartida.Saque ? v.CaixaDoSaque : null;
        r.PingMs = pingMs;

        foreach (var e in v.Eventos)
        {
            // Os eventos não levam posição (custaria bytes em cada repetição); a bola do quadro é boa o bastante pro som.
            r.Acontecimentos.Add(new Acontecimento(e.Tipo, e.Jogador, e.Time, e.Golpe, e.Motivo, v.Bola.X, v.Bola.Y, v.Bola.Z));
            switch (e.Tipo)
            {
                case TipoDeEventoDaPartida.Golpe:
                    _timeDoUltimoGolpe = e.Time >= 0 ? e.Time : e.Jogador / 2;
                    _tipoDoUltimoGolpe = e.Golpe;
                    _quicouDepoisDoUltimoGolpe = false;
                    if (e.Jogador is >= 0 and < 4)
                    {
                        r.Jogadores[e.Jogador].UltimoGolpe = e.Golpe;
                        r.Jogadores[e.Jogador].InstanteDoUltimoGolpe = (float)(e.Tick / (double)Protocolo.TicksPorSegundo);
                    }
                    break;
                case TipoDeEventoDaPartida.Quique:
                    _quicouDepoisDoUltimoGolpe = true;
                    break;
                case TipoDeEventoDaPartida.SaquePreparado:
                case TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida:
                    _timeDoUltimoGolpe = -1; _tipoDoUltimoGolpe = null; _quicouDepoisDoUltimoGolpe = false;
                    break;
            }
        }
    }

    /// <summary>O que o humano simulado vê, montado da última visão (o cliente não tem a Partida).</summary>
    public EstadoVisivel? ParaOBot()
    {
        if (Ultima is not VisaoDaPartida v || v.Jogadores.Count < 4) return null;
        var jogadores = new JogadorVisivel[4];
        for (int i = 0; i < 4; i++)
        {
            var j = v.Jogadores[i];
            jogadores[i] = new JogadorVisivel(j.X, j.Y, j.Vx, j.Vy, i / 2, Quadra.LadoDoTime(i / 2));
        }
        var b = v.Bola;
        return new EstadoVisivel(v.Estado, v.Placar.Sacador.Time * 2 + v.Placar.Sacador.Jogador, v.CaixaDoSaque,
            b.X, b.Y, b.Z, b.Vx, b.Vy, b.Vz, b.Wx, b.Wy, b.Wz, b.EmJogo,
            _timeDoUltimoGolpe, _tipoDoUltimoGolpe, _quicouDepoisDoUltimoGolpe, jogadores);
    }

    private static string NomePadrao(int i) => i switch { 0 => "Host", 1 => "Parceiro", 2 => "Rival 1", _ => "Rival 2" };
}
