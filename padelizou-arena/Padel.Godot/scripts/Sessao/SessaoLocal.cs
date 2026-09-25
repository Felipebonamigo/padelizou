using Padel.Core;

namespace Padel.Godot;

/// <summary>A partida roda aqui mesmo: contra a IA, cooperativo local ou demonstração (4 IAs).</summary>
public sealed class SessaoLocal : ISessao
{
    private readonly Partida _partida;
    private readonly int[] _locais;

    private readonly string?[] _nomes = new string?[4];

    /// <summary>nomeDoJogador vai pro primeiro jogador desta máquina (o segundo do coop vira "Jogador 2"); destro vale pros dois.</summary>
    public SessaoLocal(OpcoesDaPartida opcoes, string? nomeDoJogador = null, bool destro = true)
    {
        _partida = new Partida(opcoes);
        _locais = Enumerable.Range(0, 4).Where(i => opcoes.Humanos[i]).ToArray();
        for (int n = 0; n < _locais.Length; n++)
        {
            _partida.Jogadores[_locais[n]].Destro = destro;
            _nomes[_locais[n]] = n == 0 ? nomeDoJogador : $"Jogador {n + 1}";
        }
        // Demonstração (ninguém nos controles): "Você / Parceiro" mentiria; as duplas viram as cores da quadra.
        if (_locais.Length == 0) { _nomes[0] = "Lima 1"; _nomes[1] = "Lima 2"; _nomes[2] = "Laranja 1"; _nomes[3] = "Laranja 2"; }
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
            rj.Indice = i; rj.Time = j.Time; rj.Lado = j.Lado; rj.Nome = _nomes[i] is string nome && !string.IsNullOrWhiteSpace(nome) ? nome : j.Nome;
            rj.Destro = j.Destro;
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

    public EstadoVisivel? EstadoParaOBot() => EstadoVisivel.De(_partida);

    public void Dispose() => _partida.Evento -= AoEvento;
}
