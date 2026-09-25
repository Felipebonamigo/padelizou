namespace Padel.Core.Perfil;

/// <summary>
/// Assina o <see cref="Partida.Evento"/> de uma partida e junta, pro(s) jogador(es) do perfil, o que vira estatística e
/// conquista: <see cref="Resumo"/> a qualquer hora (no fim, ou no abandono). Crie logo depois da Partida, antes do
/// primeiro <see cref="Partida.Avancar"/>; <see cref="Dispose"/> desassina.
/// <para>
/// Como lê a partida (a ordem dos eventos é a da <see cref="Partida"/>): Golpe marca quem bateu por último e com que golpe;
/// Rede marca que a bola desse golpe tocou a rede; Saiu olha a <see cref="Partida.Bola"/> — parada no ponto de saída — pra
/// saber se foi pela porta (o evento não diz); Falta e Let encerram o lance sem ponto; Ponto/Game/Set/Partida trazem quem
/// ganhou (Time) e o <see cref="Motivo"/>, e chegam com o <see cref="Placar"/> JÁ atualizado. Por isso o ponto de ouro
/// usa o placar guardado depois do ponto anterior (falta e let não mexem no placar), e os sets novos saem de
/// <see cref="Placar.SetsAnteriores"/>.
/// </para>
/// </summary>
public sealed class ColetorDaPartida : IDisposable
{
    private readonly Partida _partida;
    private readonly ModoDaPartida _modo;
    private readonly int[] _indices;
    private readonly Jogador[] _doPerfil;
    private readonly int _time;
    private ResumoDaPartida? _congelado;

    private readonly Dictionary<TipoDeGolpe, int> _golpesPorTipo = [];
    private readonly Dictionary<LadoDoGolpe, int> _golpesPorLado = [];
    private readonly Dictionary<TipoDeGolpe, int> _vencedores = [];
    private readonly Dictionary<Motivo, int> _erros = [];
    private int _pontosVencidos, _pontosPerdidos, _golpesNaRede, _maiorRally;
    private int _ouroDisputados, _ouroVencidos, _maiorDesvantagemRevertida, _saidasPelaPorta;

    // O lance em andamento: quem bateu por último, com que golpe, se a bola tocou a rede, se saiu pela porta.
    private Jogador? _ultimoGolpeador;
    private TipoDeGolpe? _ultimoGolpe;
    private bool _tocouARede, _saiuPelaPorta;
    // O placar antes do próximo ponto.
    private bool _proximoEPontoDeOuro;
    private int _setsVistos, _desvantagemNoSet;

    /// <param name="indicesDoPerfil">Índices (time*2 + índice, de 0 a 3) dos jogadores do perfil: um, ou os dois de um time no coop.</param>
    public ColetorDaPartida(Partida partida, ModoDaPartida modo, params int[] indicesDoPerfil)
    {
        ArgumentNullException.ThrowIfNull(partida);
        ArgumentNullException.ThrowIfNull(indicesDoPerfil);
        if (indicesDoPerfil.Length == 0) throw new ArgumentException("Diga qual jogador é o do perfil (índice de 0 a 3).", nameof(indicesDoPerfil));
        foreach (int i in indicesDoPerfil)
            if (i is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(indicesDoPerfil), i, "Índice de jogador vai de 0 a 3 (time*2 + índice).");
        if (indicesDoPerfil.Distinct().Count() != indicesDoPerfil.Length) throw new ArgumentException("Índice do perfil repetido.", nameof(indicesDoPerfil));
        if (indicesDoPerfil.Select(i => i / 2).Distinct().Count() != 1)
            throw new ArgumentException("Os jogadores do perfil precisam ser do mesmo time (coop): vitória e placar são de um time só.", nameof(indicesDoPerfil));

        _partida = partida;
        _modo = modo;
        _indices = [.. indicesDoPerfil.Order()];
        _doPerfil = [.. _indices.Select(i => partida.Jogadores[i])];
        _time = _indices[0] / 2;
        _proximoEPontoDeOuro = partida.Placar.EmPontoDecisivo;
        _setsVistos = partida.Placar.SetsAnteriores.Count;
        _desvantagemNoSet = Desvantagem();
        partida.Evento += AoEvento;
    }

    /// <summary>Desassina e congela o resumo: depois disto <see cref="Resumo"/> devolve sempre o do instante do Dispose.</summary>
    public void Dispose()
    {
        if (_congelado is not null) return;
        _partida.Evento -= AoEvento;
        _congelado = Montar();
    }

    private bool DoPerfil(Jogador? jogador) => jogador is not null && Array.IndexOf(_doPerfil, jogador) >= 0;
    private int Desvantagem() => _partida.Placar.Games[1 - _time] - _partida.Placar.Games[_time];

    private void AoEvento(EventoDaPartida e)
    {
        switch (e.Tipo)
        {
            case TipoDeEventoDaPartida.Golpe:
                _ultimoGolpeador = e.Jogador;
                _ultimoGolpe = e.Golpe;
                _tocouARede = false;
                _saiuPelaPorta = false;
                if (DoPerfil(e.Jogador))
                {
                    if (e.Golpe is TipoDeGolpe tipo) Somar(_golpesPorTipo, tipo);
                    if (e.Lado is LadoDoGolpe lado) Somar(_golpesPorLado, lado);
                }
                break;
            case TipoDeEventoDaPartida.Rede:
                _tocouARede = true;
                break;
            case TipoDeEventoDaPartida.Saiu:
                _saiuPelaPorta = SaiuPelaPorta(_partida.Bola);
                break;
            case TipoDeEventoDaPartida.Falta:
                if (DoPerfil(_ultimoGolpeador) && _tocouARede) _golpesNaRede += 1;
                EncerrarLance();
                break;
            case TipoDeEventoDaPartida.Let:
            case TipoDeEventoDaPartida.SaquePreparado:
                EncerrarLance();
                break;
            case TipoDeEventoDaPartida.Ponto:
            case TipoDeEventoDaPartida.Game:
            case TipoDeEventoDaPartida.Set:
            case TipoDeEventoDaPartida.Partida:
                FecharPonto(e.Time, e.Motivo);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// A <see cref="Bola"/> que acabou de sair fica parada no ponto em que cruzou a parede (Bola.Sair): na lateral, |x| = 5;
    /// pela porta, se ali é a porta (a mesma regra que a física usou, <see cref="Quadra.NaPorta"/>). O evento Saiu da
    /// partida não carrega isso — o da bola carrega (PelaPorta), mas a Partida o traduz sem os campos.
    /// </summary>
    private static bool SaiuPelaPorta(Bola bola) =>
        !bola.EmJogo && MathF.Abs(bola.X) >= Quadra.MeiaLargura && Quadra.NaPorta(bola.Y, bola.Z);

    private void FecharPonto(int para, Motivo? motivo)
    {
        bool nosso = para == _time;
        if (nosso) _pontosVencidos += 1; else _pontosPerdidos += 1;
        if (_proximoEPontoDeOuro)
        {
            _ouroDisputados += 1;
            if (nosso) _ouroVencidos += 1;
        }
        if (DoPerfil(_ultimoGolpeador))
        {
            if (nosso)
            {
                if (_ultimoGolpe is TipoDeGolpe golpe) Somar(_vencedores, golpe);
                if (_saiuPelaPorta && motivo == Motivo.Fora) _saidasPelaPorta += 1;
            }
            else
            {
                if (motivo is Motivo m) Somar(_erros, m);
                if (_tocouARede) _golpesNaRede += 1;
            }
        }
        _maiorRally = Math.Max(_maiorRally, _partida.RallyAtual);

        // Sets que fecharam neste ponto (o Placar já zerou os games deles e os guardou em SetsAnteriores).
        var sets = _partida.Placar.SetsAnteriores;
        for (; _setsVistos < sets.Count; _setsVistos++)
        {
            var set = sets[_setsVistos];
            if (set.Games[_time] > set.Games[1 - _time])
                _maiorDesvantagemRevertida = Math.Max(_maiorDesvantagemRevertida, _desvantagemNoSet);
            _desvantagemNoSet = 0;
        }
        // O game que fecha um set vencido é do vencedor: não aumenta a desvantagem dele. Então basta olhar depois de cada ponto.
        _desvantagemNoSet = Math.Max(_desvantagemNoSet, Desvantagem());
        _proximoEPontoDeOuro = _partida.Placar.EmPontoDecisivo;
        EncerrarLance();
    }

    private void EncerrarLance()
    {
        _ultimoGolpeador = null;
        _ultimoGolpe = null;
        _tocouARede = false;
        _saiuPelaPorta = false;
    }

    private static void Somar<T>(Dictionary<T, int> contagem, T chave) where T : notnull =>
        contagem[chave] = contagem.GetValueOrDefault(chave) + 1;

    /// <summary>O que foi coletado até agora. Pode ser chamado a qualquer hora; no meio, Terminada e Venceu são false.</summary>
    public ResumoDaPartida Resumo() => _congelado ?? Montar();

    private ResumoDaPartida Montar()
    {
        var placar = _partida.Placar;
        int rival = 1 - _time;
        var sets = placar.SetsAnteriores
            .Select(s => new SetDoResumo(s.Games[_time], s.Games[rival], s.TieBreak?[_time], s.TieBreak?[rival]))
            .ToList();
        if (!placar.Acabou) sets.Add(new SetDoResumo(placar.Games[_time], placar.Games[rival], Encerrado: false));
        bool[] humanos = _partida.Opcoes.Humanos;   // como começou: no online, o rival que cai vira IA no meio (Jogador.Humano), e isso não conta
        return new ResumoDaPartida
        {
            Modo = _modo,
            Dificuldade = _partida.Opcoes.Dificuldade,
            RivalDaIA = !humanos[rival * 2] && !humanos[rival * 2 + 1],
            IndicesDoPerfil = [.. _indices],
            TimeDoPerfil = _time,
            Terminada = placar.Acabou,
            Venceu = placar.Vencedor == _time,
            Sets = sets,
            PontosVencidos = _pontosVencidos,
            PontosPerdidos = _pontosPerdidos,
            DuracaoEmSegundos = _partida.TempoDeJogo,
            GolpesPorTipo = new Dictionary<TipoDeGolpe, int>(_golpesPorTipo),
            GolpesPorLado = new Dictionary<LadoDoGolpe, int>(_golpesPorLado),
            VencedoresPorTipo = new Dictionary<TipoDeGolpe, int>(_vencedores),
            ErrosPorMotivo = new Dictionary<Motivo, int>(_erros),
            GolpesNaRede = _golpesNaRede,
            MaiorRally = _maiorRally,
            PontosDeOuroDisputados = _ouroDisputados,
            PontosDeOuroVencidos = _ouroVencidos,
            MaiorDesvantagemRevertida = _maiorDesvantagemRevertida,
            SaidasPelaPorta = _saidasPelaPorta,
        };
    }
}
