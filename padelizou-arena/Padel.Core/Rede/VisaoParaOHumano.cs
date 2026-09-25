namespace Padel.Core.Rede;

/// <summary>
/// O que o humano simulado de um CLIENTE vê: monta o <see cref="EstadoVisivel"/> a partir do quadro que o
/// <see cref="ClienteDaPartida"/> desenha — e de nada mais. A bola é a do quadro (a interpolada,
/// <see cref="ClienteDaPartida.AtrasoDeInterpolacao"/> no passado), o próprio jogador é o predito, os outros são os
/// interpolados. Quem bateu por último, com que golpe e se a bola já quicou do lado de quem recebe o instantâneo não
/// carrega: saem dos eventos numerados que chegaram com os quadros, na ordem — como a tela toca o som. Nada vem da
/// Partida do host: é o que deixa comparar o humano no cliente com o mesmo humano no local (docs/JUSTICA-NA-REDE.md).
///
/// Estado e sacador: os do instantâneo de onde o quadro saiu, a menos que um evento mais novo que ele (e já na hora de
/// tocar) os tenha mudado — o saque sai (Golpe de saque → Rally), o ponto acaba (Ponto, Game, Set, Partida, Falta, Let
/// → FimDoPonto), o saque é preparado (→ Saque, com o sacador do evento) ou a partida termina (→ Fim). Sem isso o
/// humano do cliente ficaria até 4 ticks (um instantâneo) achando que o saque ainda não saiu com a bola já no ar, e a
/// reação dele começaria tarde por um defeito da régua, não da rede. A caixa do saque é sempre a do instantâneo (o
/// humano simulado não a lê).
///
/// Uso: uma por cliente, alimentada com TODO quadro de <see cref="ClienteDaPartida.ParaDesenhar"/>, na ordem — cada
/// evento chega em um quadro só. Serve igual pro quadro do host (<see cref="ServidorDaPartida.ParaDesenhar"/>, sem atraso).
/// Fora do rally a leitura pode diferir da do árbitro do host (uma falta de saque que quicou do lado de quem sacou deixa
/// "quicou" ligado aqui e desligado lá): o humano simulado não a lê fora do rally — no saque ele a zera.
/// </summary>
public sealed class VisaoParaOHumano
{
    private int _timeDoUltimoGolpe = -1;
    private TipoDeGolpe? _tipoDoUltimoGolpe;
    private bool _quicouDepoisDoUltimoGolpe;
    private EstadoDaPartida _estadoPelosEventos;
    private uint? _tickDoEstadoPelosEventos;
    private int _sacadorPreparado;
    private uint? _tickDoSaquePreparado;

    /// <summary>Lê os eventos do quadro (atualizam a leitura) e devolve o que o humano vê nele.</summary>
    public EstadoVisivel Ver(VisaoDaPartida quadro)
    {
        if (quadro.Jogadores.Count != Protocolo.Jogadores) throw new ArgumentException($"o quadro precisa ter os {Protocolo.Jogadores} jogadores", nameof(quadro));
        foreach (var evento in quadro.Eventos) Ler(evento);

        var estado = _tickDoEstadoPelosEventos is uint virada && virada > quadro.TickDoEstado ? _estadoPelosEventos : quadro.Estado;
        var sacadorDoPlacar = quadro.Placar.Sacador;
        int sacador = _tickDoSaquePreparado is uint preparado && preparado > quadro.TickDoEstado
            ? _sacadorPreparado
            : sacadorDoPlacar.Time * 2 + sacadorDoPlacar.Jogador;
        var jogadores = new JogadorVisivel[Protocolo.Jogadores];
        for (int i = 0; i < jogadores.Length; i++)
        {
            var j = quadro.Jogadores[i];
            int time = i / 2;
            jogadores[i] = new JogadorVisivel(j.X, j.Y, j.Vx, j.Vy, time, Quadra.LadoDoTime(time));
        }
        var b = quadro.Bola;
        return new EstadoVisivel(
            estado, sacador, quadro.CaixaDoSaque,
            b.X, b.Y, b.Z, b.Vx, b.Vy, b.Vz, b.Wx, b.Wy, b.Wz, b.EmJogo,
            _timeDoUltimoGolpe, _tipoDoUltimoGolpe, _quicouDepoisDoUltimoGolpe,
            jogadores);
    }

    /// <summary>Espelha o que a Partida e o Árbitro do host fazem com UltimoGolpe e QuicouNoReceptor a cada acontecimento.</summary>
    private void Ler(EventoNumerado e)
    {
        switch (e.Tipo)
        {
            case TipoDeEventoDaPartida.Golpe:
                _timeDoUltimoGolpe = e.Time;
                _tipoDoUltimoGolpe = e.Golpe;
                _quicouDepoisDoUltimoGolpe = false;   // Arbitro.RegistrarGolpe / IniciarSaque zeram
                if (e.Golpe == TipoDeGolpe.Saque) Virar(EstadoDaPartida.Rally, e.Tick);
                break;
            case TipoDeEventoDaPartida.Quique:
                // No rally, quique do lado de quem bateu encerra o ponto no mesmo tick; o que segue o jogo é do lado de quem recebe.
                if (_timeDoUltimoGolpe >= 0) _quicouDepoisDoUltimoGolpe = true;
                break;
            case TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida:
                _quicouDepoisDoUltimoGolpe = false;   // Arbitro.NovoPonto; quem bateu por último fica até o próximo saque, como UltimoGolpe
                Virar(EstadoDaPartida.FimDoPonto, e.Tick);
                break;
            case TipoDeEventoDaPartida.Falta or TipoDeEventoDaPartida.Let:
                Virar(EstadoDaPartida.FimDoPonto, e.Tick);
                break;
            case TipoDeEventoDaPartida.SaquePreparado:
                _timeDoUltimoGolpe = -1;   // IniciarPonto zera UltimoGolpe
                _tipoDoUltimoGolpe = null;
                _sacadorPreparado = e.Jogador;
                _tickDoSaquePreparado = e.Tick;
                Virar(EstadoDaPartida.Saque, e.Tick);
                break;
            case TipoDeEventoDaPartida.Fim:
                Virar(EstadoDaPartida.Fim, e.Tick);
                break;
            default:
                break;
        }
    }

    private void Virar(EstadoDaPartida estado, uint tick)
    {
        _estadoPelosEventos = estado;
        _tickDoEstadoPelosEventos = tick;
    }
}
