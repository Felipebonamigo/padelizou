using Godot;

namespace Padel.Godot;

/// <summary>
/// O som da partida. Carrega os WAVs de res://audio/ (sintetizados por ferramentas/sintetizar_sons.py — ponte até a
/// gravação num clube do REALISMO.md), mantém um pool de AudioStreamPlayer3D com limite de vozes e expõe só métodos
/// com primitivos, pra quem chama (a partida local, o replay, o cliente de rede) não depender de nada daqui.
/// Cada disparo sorteia uma das variações do som (sem repetir a última) e varia a altura em ±5 %: é o que tira a
/// repetição mecânica de um rali de 20 golpes.
/// Posições são do mundo do Godot (já convertidas: Coordenadas.ParaGodot). O ouvinte é a câmera atual.
/// </summary>
public partial class SomNode : Node3D
{
    public const string Pasta = "res://audio/";
    public const int LimiteDeVozes = 16;
    public const int MaximoDeVariacoes = 9;
    public const float VariacaoDeAltura = 0.05f;
    /// <summary>Abaixo disso o jogador está parado (ajeitando o pé não faz barulho de passo).</summary>
    public const float VelocidadeMinimaDoPasso = 0.5f;
    /// <summary>Duração da entrada e da saída do ambiente.</summary>
    public const float SegundosDeFadeDoAmbiente = 1.5f;

    // Nível-base de cada família em dB, com o arquivo normalizado em -3 dBFS de pico. Acertado pela sonoridade (RMS)
    // medida de cada arquivo: golpe como referência; vidro um pouco acima (é o som mais marcante do padel); passos
    // bem baixos; o ambiente é cama, não protagonista. Exportados pra afinar no editor, ao vivo.
    [Export] public float DbGolpe = 0f;
    [Export] public float DbVidro = -1f;
    [Export] public float DbGrade = -2f;
    [Export] public float DbQuique = -4f;
    [Export] public float DbRede = -8f;
    [Export] public float DbPasso = -16f;
    [Export] public float DbAplauso = -8f;
    [Export] public float DbUhh = -4f;
    [Export] public float DbBip = -22f;
    [Export] public float DbAmbiente = -16f;

    /// <summary>Disparado a cada som que começa a tocar, com o nome do arquivo sem extensão ("golpe_drive_3").
    /// Pra teste e depuração; o jogo não precisa ouvir.</summary>
    public event Action<string>? AoTocar;

    public int ArquivosCarregados { get; private set; }
    public int VozesDescartadas { get; private set; }
    public float VolumeGeral => _volume;
    public float VolumeGeralDb => _volume <= 0.0001f ? -80f : Mathf.LinearToDb(_volume);
    public bool AmbienteTocando => _ambiente.Playing;
    public bool AmbienteEmLoop => _streamDoAmbiente is { LoopMode: not AudioStreamWav.LoopModeEnum.Disabled };

    /// <summary>Vozes posicionais + público + bip tocando agora (o ambiente não conta).</summary>
    public int VozesTocando
    {
        get
        {
            int n = _publico.Playing ? 1 : 0;
            if (_bip.Playing) n++;
            foreach (var v in _vozes) if (v.Tocador.Playing) n++;
            return n;
        }
    }

    public int Variacoes(string familia) => _sons.TryGetValue(familia, out var lista) ? lista.Length : 0;

    private static readonly string[] Familias =
    [
        "golpe_drive", "golpe_voleio", "golpe_smash", "golpe_bandeja", "golpe_lob",
        "quique", "vidro", "grade", "rede", "passo", "publico_aplauso", "publico_uhh", "bip_placar",
    ];
    private const string ArquivoDoAmbiente = "ambiente_clube";

    private enum Prioridade { Passo = 1, Bola = 2, Golpe = 3 }

    private sealed class Voz
    {
        public readonly AudioStreamPlayer3D Tocador;
        public float BaseDb;
        public Prioridade Prioridade;
        public double Inicio;
        public Voz(AudioStreamPlayer3D tocador) => Tocador = tocador;
    }

    private sealed class Trilha
    {
        public int Jogador;
        public Vector3 Posicao;
        public double VistaEm, UltimoPasso;
    }

    private readonly Dictionary<string, AudioStreamWav[]> _sons = new();
    private readonly Dictionary<string, int> _ultimaVariacao = new();
    private readonly HashSet<string> _golpesDesconhecidos = new();
    private readonly Voz[] _vozes = new Voz[LimiteDeVozes];
    private readonly List<Trilha> _trilhas = [];
    private readonly AudioStreamPlayer _ambiente = new() { Name = "Ambiente" };
    private readonly AudioStreamPlayer _publico = new() { Name = "Publico" };
    private readonly AudioStreamPlayer _bip = new() { Name = "Bip" };
    private bool _encerrado;
    private readonly RandomNumberGenerator _sorteio = new();
    private AudioStreamWav? _streamDoAmbiente;
    private float _baseDbDoPublico, _baseDbDoBip;
    private float _volume = 1f;
    private float _nivelDoAmbiente, _alvoDoAmbiente;
    private double _relogio;

    public SomNode()
    {
        for (int i = 0; i < LimiteDeVozes; i++)
        {
            _vozes[i] = new Voz(new AudioStreamPlayer3D
            {
                Name = $"Voz{i}",
                // Referência de 14 m: é mais ou menos a distância da câmera de TV ao meio da quadra da casa. Quem
                // está do outro lado da rede soa ~6 dB mais baixo, como na transmissão (e não 10+, que some).
                UnitSize = 14f,
                MaxDb = 3f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                // O filtro de "ar" do Godot corta em 5 kHz por padrão; a 30 m isso abafa o toc demais.
                AttenuationFilterCutoffHz = 9000f,
                AttenuationFilterDb = -10f,
                PanningStrength = 0.7f,
            });
        }
    }

    public override void _Ready()
    {
        _sorteio.Randomize();
        foreach (var v in _vozes) AddChild(v.Tocador);
        AddChild(_ambiente);
        AddChild(_publico);
        AddChild(_bip);
        Carregar();
    }

    private void Carregar()
    {
        foreach (var familia in Familias)
        {
            var lista = new List<AudioStreamWav>();
            for (int i = 1; i <= MaximoDeVariacoes; i++)
            {
                string caminho = $"{Pasta}{familia}_{i}.wav";
                if (!ResourceLoader.Exists(caminho)) break;
                if (ResourceLoader.Load(caminho) is AudioStreamWav wav) lista.Add(wav);
                else GD.PushError($"SomNode: {caminho} existe mas não carregou como AudioStreamWav");
            }
            if (lista.Count == 0) GD.PushError($"SomNode: nenhuma variação de '{familia}' em {Pasta} — rode ferramentas/sintetizar_sons.py e reimporte");
            _sons[familia] = [.. lista];
            ArquivosCarregados += lista.Count;
        }

        string ambiente = $"{Pasta}{ArquivoDoAmbiente}.wav";
        if (ResourceLoader.Exists(ambiente) && ResourceLoader.Load(ambiente) is AudioStreamWav wavAmbiente)
        {
            // O WAV já traz o loop (bloco 'smpl'), mas o modo de importação pode ter sido mudado no editor:
            // garante o loop pra frente do começo ao fim — o arquivo foi feito pra emendar exatamente aí.
            wavAmbiente.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wavAmbiente.LoopBegin = 0;
            wavAmbiente.LoopEnd = (int)Math.Round(wavAmbiente.GetLength() * wavAmbiente.MixRate);
            _streamDoAmbiente = wavAmbiente;
            _ambiente.Stream = wavAmbiente;
            ArquivosCarregados++;
        }
        else GD.PushError($"SomNode: {ambiente} não carregou — o ambiente fica mudo");
    }

    /// <summary>
    /// Saindo da árvore (fim de partida, troca de cena, fechar o jogo): para tudo. O Godot para sozinho os tocadores 3D,
    /// mas não os outros (ambiente, público, bip) — e fechar o jogo com reprodução viva vaza AudioStreamPlaybackWAV.
    /// </summary>
    public override void _ExitTree() => Encerrar();

    /// <summary>
    /// Para tudo na hora, sem fade, e o nó não toca mais nada. Quem vai FECHAR o jogo chama isto e espera um ciclo de
    /// mixagem antes do Quit: parar só no _ExitTree do desligamento chega tarde, e o servidor de áudio não recolhe as
    /// reproduções a tempo; e o jogo ainda roda durante a espera, então nada pode voltar a tocar.
    /// </summary>
    public void Encerrar()
    {
        _encerrado = true;
        foreach (var v in _vozes) v.Tocador.Stop();
        _ambiente.Stop();
        _publico.Stop();
        _bip.Stop();
        _alvoDoAmbiente = 0f;
    }

    public override void _Process(double delta)
    {
        _relogio += delta;
        AtualizarAmbiente((float)delta);
        // Trilhas de passos que ninguém atualiza há um tempo são de quem parou ou saiu.
        _trilhas.RemoveAll(t => _relogio - t.VistaEm > 0.5);
    }

    // ───────────────────────────── API (só primitivos) ─────────────────────────────

    /// <summary>
    /// Golpe de raquete. tipoDeGolpe aceita os sons ("drive", "voleio", "smash", "bandeja", "lob") e os nomes do
    /// TipoDeGolpe do Core (Normal, Ataque, Saque, Defesa, Vibora, Erro…), sem diferenciar maiúsculas. intensidade de 0 a 1
    /// (força do golpe): de 0 a 1 o volume sobe ~10 dB.
    /// </summary>
    public void TocarGolpe(Vector3 posicao, string tipoDeGolpe, float intensidade)
    {
        string familia = FamiliaDoGolpe(tipoDeGolpe);
        if (familia.Length == 0)
        {
            if (_golpesDesconhecidos.Add(tipoDeGolpe)) GD.PushWarning($"SomNode: golpe '{tipoDeGolpe}' sem som próprio; tocando drive");
            familia = "golpe_drive";
        }
        float i = Mathf.Clamp(intensidade, 0f, 1f);
        Tocar3D(familia, posicao, DbGolpe + Mathf.LinearToDb(0.3f + 0.7f * i), Prioridade.Golpe);
    }

    /// <summary>Bola quicando na grama sintética com areia.</summary>
    public void TocarQuique(Vector3 posicao) => Tocar3D("quique", posicao, DbQuique, Prioridade.Bola);

    /// <summary>Bola na parede: vidro (grade = false) ou a grade metálica.</summary>
    public void TocarParede(Vector3 posicao, bool grade) =>
        Tocar3D(grade ? "grade" : "vidro", posicao, grade ? DbGrade : DbVidro, Prioridade.Bola);

    public void TocarRede(Vector3 posicao) => Tocar3D("rede", posicao, DbRede, Prioridade.Bola);

    /// <summary>
    /// Passos de quem está se mexendo. Chame A CADA QUADRO, por jogador, com a velocidade (m/s): o nó marca a cadência
    /// (de ~1,8 passo/s andando a ~3,8 correndo) e toca um passo quando é hora. jogador (0–3) identifica quem é; sem
    /// ele (-1), o nó reconhece o jogador pela proximidade com a última posição informada. Parado não faz barulho.
    /// Passo é a voz de menor prioridade: com o pool cheio, é descartado (nunca rouba a voz de uma bola).
    /// </summary>
    public void TocarPassos(Vector3 posicao, float velocidade, int jogador = -1)
    {
        var trilha = AcharTrilha(posicao, jogador);
        if (velocidade < VelocidadeMinimaDoPasso)
        {
            if (trilha is not null) _trilhas.Remove(trilha);   // parou: a próxima arrancada começa uma passada nova
            return;
        }
        double intervalo = 1.0 / PassosPorSegundo(velocidade);
        if (trilha is null)
        {
            // Arrancada: o primeiro passo sai em meio intervalo, não na hora (o pé de apoio já estava no chão).
            trilha = new Trilha { Jogador = jogador, UltimoPasso = _relogio - intervalo * 0.5 };
            _trilhas.Add(trilha);
        }
        trilha.Posicao = posicao;
        trilha.VistaEm = _relogio;
        if (_relogio - trilha.UltimoPasso < intervalo) return;
        trilha.UltimoPasso = _relogio;
        float forca = Mathf.Clamp(velocidade / 6.4f, 0f, 1f);
        Tocar3D("passo", posicao, DbPasso + Mathf.LinearToDb(0.4f + 0.6f * forca), Prioridade.Passo);
    }

    /// <summary>Fim de ponto: o bip do placar e o público — aplauso se a casa ganhou, "uhh" se perdeu.</summary>
    public void Ponto(bool aFavorDaCasa)
    {
        _baseDbDoBip = DbBip;
        TocarNaoPosicional(_bip, "bip_placar", _baseDbDoBip);
        _baseDbDoPublico = aFavorDaCasa ? DbAplauso : DbUhh;
        TocarNaoPosicional(_publico, aFavorDaCasa ? "publico_aplauso" : "publico_uhh", _baseDbDoPublico);
    }

    /// <summary>Liga o ambiente do clube (loop de 20 s) com entrada suave, de um ponto sorteado do loop.</summary>
    public void IniciarAmbiente()
    {
        if (_encerrado) return;
        if (_streamDoAmbiente is null) return;
        _alvoDoAmbiente = 1f;
        if (!_ambiente.Playing)
        {
            _nivelDoAmbiente = 0f;
            AtualizarAmbiente(0f);
            _ambiente.Play((float)(_sorteio.Randf() * _streamDoAmbiente.GetLength()));
            AoTocar?.Invoke(ArquivoDoAmbiente);
        }
    }

    /// <summary>Desliga o ambiente com saída suave (para de tocar quando chega a zero).</summary>
    public void PararAmbiente() => _alvoDoAmbiente = 0f;

    /// <summary>Volume geral de tudo o que este nó toca, de 0 (mudo) a 1. Vale também pro que já está tocando.</summary>
    public void Volume(float volume)
    {
        _volume = Mathf.Clamp(volume, 0f, 1f);
        foreach (var v in _vozes) v.Tocador.VolumeDb = v.BaseDb + VolumeGeralDb;
        _publico.VolumeDb = _baseDbDoPublico + VolumeGeralDb;
        _bip.VolumeDb = _baseDbDoBip + VolumeGeralDb;
        AtualizarAmbiente(0f);
    }

    // ───────────────────────────── por dentro ─────────────────────────────

    /// <summary>Tipo de golpe (som ou nome do Core) → família de arquivos. "" se não conhece.</summary>
    public static string FamiliaDoGolpe(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        // Contato cheio, com topspin ou chapado: o toc de referência. O saque de padel é por baixo e firme.
        "drive" or "normal" or "ataque" or "saque" or "erro" => "golpe_drive",
        // Bloqueio curto na rede: seco, pouca cauda.
        "voleio" or "volea" or "bloqueio" => "golpe_voleio",
        "smash" or "remate" or "por3" or "por4" or "smashpor3" or "smashpor4" => "golpe_smash",
        // Golpes cortados (slice/sidespin): a face "escova" a bola — a defesa do fundo também sai cortada.
        "bandeja" or "vibora" or "víbora" or "defesa" or "chiquita" => "golpe_bandeja",
        // A contrapared é um empurrão pra cima contra o próprio vidro: toque suave, como o lob.
        "lob" or "globo" or "contrapared" => "golpe_lob",
        _ => "",
    };

    /// <summary>Cadência de passos: ~1,8/s andando (1 m/s) até ~3,8/s no tiro (6,4 m/s) — deslocamento de padel é curto e picado.</summary>
    public static double PassosPorSegundo(float velocidade) => Math.Clamp(1.45 + 0.37 * velocidade, 1.6, 4.0);

    private Trilha? AcharTrilha(Vector3 posicao, int jogador)
    {
        Trilha? melhor = null;
        float melhorDist = 0.8f;   // a 6,4 m/s e 30 quadros/s, um jogador anda ~0,2 m entre chamadas
        foreach (var t in _trilhas)
        {
            if (jogador >= 0)
            {
                if (t.Jogador == jogador) return t;
                continue;
            }
            if (t.Jogador >= 0) continue;
            float d = t.Posicao.DistanceTo(posicao);
            if (d < melhorDist) { melhor = t; melhorDist = d; }
        }
        return melhor;
    }

    private AudioStreamWav? Sortear(string familia)
    {
        if (!_sons.TryGetValue(familia, out var lista) || lista.Length == 0) return null;
        int ultima = _ultimaVariacao.TryGetValue(familia, out var u) ? u : -1;
        int i = _sorteio.RandiRange(0, lista.Length - 1);
        if (i == ultima && lista.Length > 1) i = (i + 1 + _sorteio.RandiRange(0, lista.Length - 2)) % lista.Length;
        _ultimaVariacao[familia] = i;
        return lista[i];
    }

    private void Tocar3D(string familia, Vector3 posicao, float baseDb, Prioridade prioridade)
    {
        if (_encerrado) return;
        if (!IsInsideTree()) return;
        var stream = Sortear(familia);
        if (stream is null) return;
        var voz = PegarVoz(prioridade);
        if (voz is null) { VozesDescartadas++; return; }
        var p = voz.Tocador;
        p.Stop();
        p.Stream = stream;
        p.GlobalPosition = posicao;
        p.PitchScale = 1f + _sorteio.RandfRange(-VariacaoDeAltura, VariacaoDeAltura);
        voz.BaseDb = baseDb;
        voz.Prioridade = prioridade;
        voz.Inicio = _relogio;
        p.VolumeDb = baseDb + VolumeGeralDb;
        p.Play();
        AoTocar?.Invoke(stream.ResourcePath.GetFile().GetBaseName());
    }

    /// <summary>Uma voz livre; senão rouba a de menor prioridade (a mais antiga entre as iguais), desde que não seja
    /// mais importante que o som novo. Sem nenhuma, o som novo é descartado.</summary>
    private Voz? PegarVoz(Prioridade prioridade)
    {
        Voz? vitima = null;
        foreach (var v in _vozes)
        {
            if (!v.Tocador.Playing) return v;
            if (v.Prioridade > prioridade) continue;
            if (vitima is null || v.Prioridade < vitima.Prioridade || (v.Prioridade == vitima.Prioridade && v.Inicio < vitima.Inicio))
                vitima = v;
        }
        return prioridade == Prioridade.Passo ? null : vitima;
    }

    private void TocarNaoPosicional(AudioStreamPlayer tocador, string familia, float baseDb)
    {
        if (_encerrado) return;
        if (!IsInsideTree()) return;
        var stream = Sortear(familia);
        if (stream is null) return;
        tocador.Stop();
        tocador.Stream = stream;
        tocador.PitchScale = 1f + _sorteio.RandfRange(-VariacaoDeAltura, VariacaoDeAltura);
        tocador.VolumeDb = baseDb + VolumeGeralDb;
        tocador.Play();
        AoTocar?.Invoke(stream.ResourcePath.GetFile().GetBaseName());
    }

    private void AtualizarAmbiente(float delta)
    {
        _nivelDoAmbiente = Mathf.MoveToward(_nivelDoAmbiente, _alvoDoAmbiente, delta / SegundosDeFadeDoAmbiente);
        _ambiente.VolumeDb = DbAmbiente + VolumeGeralDb + (_nivelDoAmbiente <= 0.0001f ? -80f : Mathf.LinearToDb(_nivelDoAmbiente));
        if (_alvoDoAmbiente <= 0f && _nivelDoAmbiente <= 0f && _ambiente.Playing) _ambiente.Stop();
    }
}
