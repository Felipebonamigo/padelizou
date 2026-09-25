using Padel.Core;

namespace Padel.Godot;

/// <summary>A partida roda aqui mesmo: contra a IA, cooperativo local ou demonstração (4 IAs).</summary>
public sealed class SessaoLocal : ISessao
{
    private readonly Partida _partida;
    private readonly int[] _locais;

    public SessaoLocal(OpcoesDaPartida opcoes)
    {
        _partida = new Partida(opcoes);
        _locais = Enumerable.Range(0, 4).Where(i => opcoes.Humanos[i]).ToArray();
        _partida.Evento += AoEvento;
        Retratar();
    }

    public Partida Partida => _partida;
    public IReadOnlyList<int> JogadoresLocais => _locais;
    public RetratoDaPartida Retrato { get; } = new();
    public bool Acabou => _partida.Acabou;

    public string ResumoParaLog()
    {
        var e = _partida.Estatisticas;
        return $"estado={_partida.Estado} placar={_partida.Placar.Resumo()} pontos={e.Pontos} golpes={e.Golpes} maiorRally={e.MaiorRally}";
    }

    public void Avancar(double delta, ReadOnlySpan<Entrada> entradas)
    {
        _partida.Avancar((float)delta, entradas);
        Retratar();
    }

    private void AoEvento(EventoDaPartida evento)
    {
        var bola = _partida.Bola;
        int jogador = evento.Jogador is Jogador j ? j.Time * 2 + j.Indice : -1;
        Retrato.Acontecimentos.Add(new Acontecimento(evento.Tipo, jogador, evento.Time, evento.Golpe, evento.Motivo, bola.X, bola.Y, bola.Z));
        if (evento.Tipo == TipoDeEventoDaPartida.Golpe && jogador >= 0)
        {
            var r = Retrato.Jogadores[jogador];
            r.UltimoGolpe = evento.Golpe;
            r.InstanteDoUltimoGolpe = _partida.TempoDeJogo;
        }
    }

    /// <summary>Copia o estado da Partida pro Retrato (os acontecimentos já entraram pelo evento).</summary>
    private void Retratar()
    {
        var p = _partida;
        var r = Retrato;
        r.TempoDeJogo = p.TempoDeJogo;
        r.Estado = p.Estado;
        r.Bola.X = p.Bola.X; r.Bola.Y = p.Bola.Y; r.Bola.Z = p.Bola.Z; r.Bola.EmJogo = p.Bola.EmJogo;
        for (int i = 0; i < 4; i++)
        {
            var j = p.Jogadores[i];
            var rj = r.Jogadores[i];
            rj.Indice = i; rj.Time = j.Time; rj.Lado = j.Lado; rj.Nome = j.Nome;
            rj.X = j.X; rj.Y = j.Y; rj.Vx = j.Vx; rj.Vy = j.Vy;
            rj.Humano = j.Humano;
            rj.Local = Array.IndexOf(_locais, i) >= 0;
            rj.FaseDoBalanco = j.Balancando ? Math.Clamp(j.TempoNoBalanco / Jogador.DuracaoDoBalanco, 0f, 1f) : 0f;
            rj.BalancoDeLob = j.BalancoDeLob;
        }
        var pl = r.Placar;
        for (int t = 0; t < 2; t++)
        {
            pl.Sets[t] = p.Placar.Sets[t];
            pl.Games[t] = p.Placar.Games[t];
            pl.Pontos[t] = p.Placar.TextoDosPontos(t);
        }
        if (pl.SetsAnteriores.Count != p.Placar.SetsAnteriores.Count)
        {
            pl.SetsAnteriores.Clear();
            pl.SetsAnteriores.AddRange(p.Placar.SetsAnteriores);
        }
        pl.TimeSacando = p.Placar.Sacador.Time;
        pl.EmTieBreak = p.Placar.EmTieBreak;
        pl.EmPontoDecisivo = p.Placar.EmPontoDecisivo;
        pl.Vencedor = p.Placar.Vencedor;
        pl.Resumo = p.Placar.Resumo();
        r.Mensagem = p.Mensagem?.Texto ?? "";
        r.MensagemEmDestaque = p.Mensagem?.Destaque == true;
        r.MensagemSuave = p.Mensagem?.Suave == true;
        r.CaixaDoSaque = p.Estado == EstadoDaPartida.Saque ? p.CaixaDoSaque : null;
        r.PingMs = null;
    }

    public void Dispose() => _partida.Evento -= AoEvento;
}
