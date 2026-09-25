using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Replay do ponto em câmera lenta, como na transmissão. Grava o Retrato a cada passo num anel de 12 s;
/// quando um ponto termina e merece (rally longo, remate vencedor, game/set/partida), reproduz o FINAL do ponto
/// (os últimos segundos, como a transmissão faz — o rally inteiro em câmera lenta cansa) a meia velocidade,
/// com a câmera de lado. Só na partida local: no online a
/// partida não pode parar pros outros (lá o replay é trabalho do fim de partida, no M3).
/// Não conhece Godot: é dado e tempo; quem desenha é a PartidaNode, com o mesmo código do jogo ao vivo.
/// </summary>
public sealed class ControleDeReplay
{
    public const float SegundosGuardados = 12f;
    public const float Velocidade = 0.5f;
    public const int GolpesPraMerecer = 10;
    /// <summary>Quanto do fim do ponto entra no replay: 3,5 s de jogo viram 7 s de tela a meia velocidade.</summary>
    public const float TrechoDoFim = 3.5f;
    private const float AntesDoSaque = 0.3f;
    private const float DepoisDoPonto = 0.6f;

    private readonly struct Quadro
    {
        public readonly float Tempo, BolaX, BolaY, BolaZ;
        public readonly bool BolaEmJogo;
        public readonly JogadorNoQuadro J0, J1, J2, J3;

        public Quadro(RetratoDaPartida r)
        {
            Tempo = r.TempoDeJogo; BolaX = r.Bola.X; BolaY = r.Bola.Y; BolaZ = r.Bola.Z; BolaEmJogo = r.Bola.EmJogo;
            J0 = new(r.Jogadores[0]); J1 = new(r.Jogadores[1]); J2 = new(r.Jogadores[2]); J3 = new(r.Jogadores[3]);
        }

        public JogadorNoQuadro Jogador(int i) => i switch { 0 => J0, 1 => J1, 2 => J2, _ => J3 };
    }

    private readonly struct JogadorNoQuadro
    {
        public readonly float X, Y, Vx, Vy, Fase, InstanteDoGolpe;
        public readonly bool Lob;
        public readonly TipoDeGolpe? Golpe;

        public JogadorNoQuadro(RetratoDoJogador j)
        {
            X = j.X; Y = j.Y; Vx = j.Vx; Vy = j.Vy; Fase = j.FaseDoBalanco; Lob = j.BalancoDeLob;
            Golpe = j.UltimoGolpe; InstanteDoGolpe = j.InstanteDoUltimoGolpe;
        }
    }

    private readonly Quadro[] _anel;
    private int _proximo, _quantos;
    private float _inicioDoRally = float.NaN;
    private int _golpesNoRally;
    private TipoDeGolpe? _ultimoGolpe;
    private float? _fimDoPonto;   // instante do fim do ponto que merece replay, esperando o "depois"
    private bool _mereceReplay;

    // Reprodução.
    private float _tocarDe, _tocarAte, _relogio;
    public bool Reproduzindo { get; private set; }
    public bool Ligado { get; set; } = true;
    public readonly RetratoDaPartida RetratoDoReplay = new();

    public ControleDeReplay(int passosPorSegundo = 120)
    {
        _anel = new Quadro[(int)(SegundosGuardados * passosPorSegundo)];
    }

    /// <summary>Chamado a cada passo de jogo ao vivo, ANTES de os acontecimentos serem descartados. Devolve true se um replay começou.</summary>
    public bool Observar(RetratoDaPartida r)
    {
        _anel[_proximo] = new Quadro(r);
        _proximo = (_proximo + 1) % _anel.Length;
        _quantos = Math.Min(_quantos + 1, _anel.Length);

        foreach (var a in r.Acontecimentos)
        {
            switch (a.Tipo)
            {
                case TipoDeEventoDaPartida.Golpe when a.Golpe == TipoDeGolpe.Saque:
                    _inicioDoRally = r.TempoDeJogo; _golpesNoRally = 1; _ultimoGolpe = TipoDeGolpe.Saque; _fimDoPonto = null;
                    break;
                case TipoDeEventoDaPartida.Golpe:
                    _golpesNoRally++; _ultimoGolpe = a.Golpe;
                    break;
                case TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida:
                    bool remateVencedor = _ultimoGolpe is TipoDeGolpe.Smash or TipoDeGolpe.Vibora && a.Motivo is Motivo.DoisQuiques or Motivo.Fora or Motivo.VoltouPeloVidro;
                    _mereceReplay = !float.IsNaN(_inicioDoRally) && (_golpesNoRally >= GolpesPraMerecer || remateVencedor || a.Tipo != TipoDeEventoDaPartida.Ponto);
                    _fimDoPonto = r.TempoDeJogo;
                    break;
            }
        }

        if (Ligado && _mereceReplay && _fimDoPonto is float fim && r.TempoDeJogo >= fim + DepoisDoPonto)
        {
            _mereceReplay = false;
            _fimDoPonto = null;
            float maisAntigo = _anel[_quantos < _anel.Length ? 0 : _proximo].Tempo;
            _tocarDe = Math.Max(Math.Max(_inicioDoRally - AntesDoSaque, fim - TrechoDoFim), maisAntigo);
            _tocarAte = r.TempoDeJogo;
            _relogio = _tocarDe;
            _inicioDoRally = float.NaN;
            Reproduzindo = _tocarAte > _tocarDe;
            return Reproduzindo;
        }
        return false;
    }

    /// <summary>Avança a reprodução (tempo real) e devolve o retrato do instante, ou null quando acabou.</summary>
    public RetratoDaPartida? Avancar(float delta, RetratoDaPartida aoVivo)
    {
        if (!Reproduzindo) return null;
        _relogio += delta * Velocidade;
        if (_relogio >= _tocarAte) { Reproduzindo = false; return null; }
        Preencher(_relogio, aoVivo);
        return RetratoDoReplay;
    }

    public void Pular() => Reproduzindo = false;

    /// <summary>Quanto do trecho já foi reproduzido, 0..1 (pra barra na tela).</summary>
    public float Progresso => _tocarAte > _tocarDe ? Math.Clamp((_relogio - _tocarDe) / (_tocarAte - _tocarDe), 0, 1) : 1;

    private void Preencher(float tempo, RetratoDaPartida aoVivo)
    {
        // Acha os dois quadros em volta do instante e interpola (a reprodução lenta não pode andar aos saltos).
        int inicio = _quantos < _anel.Length ? 0 : _proximo;
        Quadro a = _anel[inicio], b = a;
        for (int k = 0; k < _quantos; k++)
        {
            var q = _anel[(inicio + k) % _anel.Length];
            if (q.Tempo > tempo) { b = q; break; }
            a = q; b = q;
        }
        float u = b.Tempo > a.Tempo ? (tempo - a.Tempo) / (b.Tempo - a.Tempo) : 0;
        var r = RetratoDoReplay;
        r.TempoDeJogo = tempo;
        r.Estado = EstadoDaPartida.Rally;
        r.Bola.X = Lerp(a.BolaX, b.BolaX, u); r.Bola.Y = Lerp(a.BolaY, b.BolaY, u); r.Bola.Z = Lerp(a.BolaZ, b.BolaZ, u);
        r.Bola.EmJogo = a.BolaEmJogo;
        for (int i = 0; i < 4; i++)
        {
            var ja = a.Jogador(i); var jb = b.Jogador(i);
            var rj = r.Jogadores[i];
            var vivo = aoVivo.Jogadores[i];
            rj.Indice = vivo.Indice; rj.Time = vivo.Time; rj.Lado = vivo.Lado; rj.Nome = vivo.Nome;
            rj.Humano = vivo.Humano; rj.Local = vivo.Local; rj.Destro = vivo.Destro;
            rj.X = Lerp(ja.X, jb.X, u); rj.Y = Lerp(ja.Y, jb.Y, u);
            rj.Vx = Lerp(ja.Vx, jb.Vx, u); rj.Vy = Lerp(ja.Vy, jb.Vy, u);
            rj.FaseDoBalanco = ja.Fase; rj.BalancoDeLob = ja.Lob;
            rj.UltimoGolpe = ja.Golpe; rj.InstanteDoUltimoGolpe = ja.InstanteDoGolpe;
        }
        CopiarPlacar(aoVivo.Placar, r.Placar);
        r.Mensagem = "REPLAY";
        r.MensagemEmDestaque = false;
        r.MensagemSuave = true;
        r.CaixaDoSaque = null;
        r.Acontecimentos.Clear();
    }

    private static float Lerp(float a, float b, float u) => a + (b - a) * u;

    private static void CopiarPlacar(RetratoDoPlacar de, RetratoDoPlacar para)
    {
        for (int t = 0; t < 2; t++) { para.Sets[t] = de.Sets[t]; para.Games[t] = de.Games[t]; para.Pontos[t] = de.Pontos[t]; }
        if (para.SetsAnteriores.Count != de.SetsAnteriores.Count) { para.SetsAnteriores.Clear(); para.SetsAnteriores.AddRange(de.SetsAnteriores); }
        para.TimeSacando = de.TimeSacando; para.EmTieBreak = de.EmTieBreak; para.EmPontoDecisivo = de.EmPontoDecisivo;
        para.Vencedor = de.Vencedor; para.Resumo = de.Resumo;
    }
}
