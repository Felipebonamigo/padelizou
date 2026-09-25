using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Teste do som (cenas/TesteSom.tscn): toca cada som em sequência pelo SomNode e imprime "ok &lt;nome&gt;" por som —
/// confere que o som certo disparou, que alguma voz tocou e que terminou. Depois confere o que a especificação pede ao
/// SomNode olhando os TOCADORES de verdade, e não o que o SomNode diz que fez: cada voz no ponto 3D pedido, altura
/// sorteada em ±5 %, sorteio que nunca repete a última variação, intensidade do golpe no volume, golpe &gt; bola &gt;
/// passo na disputa pelo pool, limite de vozes e o volume geral em todos os tocadores (vozes 3D, ambiente, público, bip).
/// No fim imprime "TesteSom: N/N ok" e sai com código 0, ou 1 se algo falhou. Sem tela:
///   godot --headless --audio-driver Dummy --path Padel.Godot res://cenas/TesteSom.tscn
/// </summary>
public partial class TesteSomNode : Node3D
{
    private const double EsperaMaxima = 8.0;
    /// <summary>A especificação: altura variando ±5 %. Literal de propósito — mexer na constante do SomNode tem que quebrar aqui.</summary>
    private const float AlturaMaxima = 0.05f;
    /// <summary>Disparos seguidos por família no caso do sorteio.</summary>
    private const int DisparosDoSorteio = 60;

    private readonly SomNode _som = new() { Name = "Som" };
    private readonly List<string> _tocados = [];
    private int _oks, _falhas;

    // Câmera de TV em (0; 7,5; 17,5): a quadra da casa é z > 0, a dos rivais z < 0.
    private static readonly Vector3 Perto = new(-1.5f, 1f, 6f);
    private static readonly Vector3 Longe = new(1.5f, 1.2f, -7f);

    public override void _Ready()
    {
        AddChild(_som);
        _som.AoTocar += _tocados.Add;
        Rodar();
    }

    private async void Rodar()
    {
        try
        {
            await Roteiro();
        }
        catch (Exception e)
        {
            Falhar("roteiro", e.ToString());
        }
        GD.Print($"TesteSom: {_oks}/{_oks + _falhas} ok");
        GetTree().Quit(_falhas == 0 ? 0 : 1);
    }

    private async Task Roteiro()
    {
        await Quadros(2);
        ConferirCarregamento();
        ConferirGolpesDoCore();

        foreach (var tipo in new[] { "drive", "voleio", "smash", "bandeja", "lob" })
            await Caso($"golpe_{tipo}", () => _som.TocarGolpe(Perto, tipo, 0.8f), $"golpe_{tipo}");
        await Caso("golpe Smash (nome do Core, do outro lado)", () => _som.TocarGolpe(Longe, nameof(TipoDeGolpe.Smash), 1f), "golpe_smash");
        await Caso("golpe Vibora (nome do Core)", () => _som.TocarGolpe(Longe, nameof(TipoDeGolpe.Vibora), 0.6f), "golpe_bandeja");
        await Caso("golpe SmashPor4 (nome do Core)", () => _som.TocarGolpe(Longe, nameof(TipoDeGolpe.SmashPor4), 1f), "golpe_smash");
        await Caso("golpe SmashPor3 (nome do Core)", () => _som.TocarGolpe(Longe, nameof(TipoDeGolpe.SmashPor3), 1f), "golpe_smash");
        await Caso("golpe Contrapared (nome do Core)", () => _som.TocarGolpe(Longe, nameof(TipoDeGolpe.Contrapared), 0.5f), "golpe_lob");
        await Caso("quique", () => _som.TocarQuique(new Vector3(0.5f, 0f, 4f)), "quique");
        await Caso("vidro", () => _som.TocarParede(new Vector3(3f, 1.2f, 10f), grade: false), "vidro");
        await Caso("grade", () => _som.TocarParede(new Vector3(5f, 3.5f, 2f), grade: true), "grade");
        await Caso("rede", () => _som.TocarRede(new Vector3(0f, 0.8f, 0f)), "rede");
        await CasoPassos();
        await Caso("ponto a favor (bip + aplauso)", () => _som.Ponto(aFavorDaCasa: true), "bip_placar", "publico_aplauso");
        await Caso("ponto contra (bip + uhh)", () => _som.Ponto(aFavorDaCasa: false), "bip_placar", "publico_uhh");
        await CasoAmbiente();
        await CasoPosicao();
        await CasoAltura();
        await CasoSorteio();
        await CasoIntensidade();
        await CasoPrioridade();
        await CasoLimiteDeVozes();
        await CasoVolume();
        await CasoSaidaDaArvore();
        await CasoEncerrado();
    }

    /// <summary>Encerrado (o jogo está fechando), o nó não toca mais nada — a partida ainda roda durante a espera da saída.</summary>
    private async Task CasoEncerrado()
    {
        var outro = new SomNode { Name = "SomEncerrado" };
        AddChild(outro);
        await Quadros(2);
        outro.IniciarAmbiente();
        outro.Encerrar();
        outro.IniciarAmbiente();
        outro.TocarGolpe(Perto, "smash", 1f);
        outro.TocarQuique(Perto);
        outro.TocarParede(Longe, grade: true);
        outro.TocarRede(Perto);
        for (int i = 0; i < 30; i++) outro.TocarPassos(Perto, 6f, 0);
        outro.Ponto(aFavorDaCasa: false);
        await Quadros(1);
        int tocando = outro.GetChildren().Count(Tocando);
        RemoveChild(outro);
        outro.QueueFree();
        await Esperar(0.25);
        if (tocando > 0) { Falhar("encerrado", $"{tocando} tocador(es) tocando depois de Encerrar"); return; }
        Ok("encerrado (nada toca depois de Encerrar)");
    }

    /// <summary>
    /// Sair da árvore com som tocando (fim de partida, troca de cena, fechar o jogo) para todos os tocadores. Sem isso o
    /// Godot fecha com as reproduções vivas e acusa "ObjectDB instances were leaked" / "resources still in use at exit".
    /// </summary>
    private async Task CasoSaidaDaArvore()
    {
        var outro = new SomNode { Name = "SomQueSai" };
        AddChild(outro);
        await Quadros(2);
        outro.IniciarAmbiente();
        outro.TocarGolpe(Perto, "smash", 1f);
        outro.TocarParede(Longe, grade: false);
        outro.Ponto(aFavorDaCasa: true);
        await Quadros(1);
        var tocadores = outro.GetChildren().Where(n => n is AudioStreamPlayer or AudioStreamPlayer3D).ToList();
        int antes = tocadores.Count(Tocando);
        RemoveChild(outro);
        int depois = tocadores.Count(Tocando);
        outro.QueueFree();
        await Esperar(0.25);   // o servidor de áudio recolhe as reproduções paradas no ciclo de mixagem seguinte (quadro sem tela é microssegundo)
        if (antes < 4) { Falhar("saída da árvore", $"só {antes} tocador(es) tocando antes de sair (esperado 4: ambiente, golpe, vidro, público)"); return; }
        if (depois > 0) { Falhar("saída da árvore", $"{depois} de {antes} tocador(es) continuaram tocando depois de o nó sair da árvore"); return; }
        Ok($"saída da árvore ({antes} tocadores parados ao sair)");
    }

    private static bool Tocando(Node n) => n switch
    {
        AudioStreamPlayer p => p.Playing,
        AudioStreamPlayer3D p => p.Playing,
        _ => false,
    };

    private void ConferirCarregamento()
    {
        string[] familias = ["golpe_drive", "golpe_voleio", "golpe_smash", "golpe_bandeja", "golpe_lob", "quique", "vidro",
            "grade", "rede", "passo", "publico_aplauso", "publico_uhh", "bip_placar"];
        int soma = 0;
        foreach (var f in familias)
        {
            int n = _som.Variacoes(f);
            if (n is < 3 or > 5) { Falhar("carregamento", $"{f} com {n} variações (esperado 3 a 5)"); return; }
            soma += n;
            for (int i = 1; i <= n; i++)
            {
                if (ResourceLoader.Load($"{SomNode.Pasta}{f}_{i}.wav") is not AudioStreamWav wav || wav.MixRate != 44100 || wav.Stereo)
                { Falhar("carregamento", $"{f}_{i}.wav não importou como mono 44,1 kHz"); return; }
            }
        }
        if (_som.ArquivosCarregados != soma + 1) { Falhar("carregamento", $"{_som.ArquivosCarregados} arquivos, esperado {soma + 1}"); return; }
        if (!_som.AmbienteEmLoop) { Falhar("carregamento", "ambiente sem loop"); return; }
        // O SomNode força o loop; aqui a pergunta é se o importador leu o loop do próprio WAV (bloco 'smpl'),
        // numa cópia fresca, fora do cache que o SomNode já mexeu.
        string caminho = $"{SomNode.Pasta}ambiente_clube.wav";
        if (ResourceLoader.Load(caminho, "", ResourceLoader.CacheMode.Ignore) is not AudioStreamWav fresco
            || fresco.LoopMode != AudioStreamWav.LoopModeEnum.Forward || fresco.LoopBegin != 0)
        { Falhar("carregamento", "o importador não leu o loop do bloco smpl do ambiente"); return; }
        int quadros = (int)Math.Round(fresco.GetLength() * fresco.MixRate);
        Ok($"carregamento ({_som.ArquivosCarregados} arquivos mono 44,1 kHz: {familias.Length} famílias com 3 a 5 variações + " +
           $"ambiente com loop lido do WAV, {fresco.LoopBegin}..{fresco.LoopEnd} de {quadros} quadros)");
    }

    private void ConferirGolpesDoCore()
    {
        var semSom = Enum.GetNames<TipoDeGolpe>().Where(n => SomNode.FamiliaDoGolpe(n).Length == 0).ToList();
        if (semSom.Count > 0) Falhar("golpes do Core", $"sem som: {string.Join(", ", semSom)}");
        else Ok($"golpes do Core ({Enum.GetNames<TipoDeGolpe>().Length} tipos mapeados)");
    }

    private async Task Caso(string nome, Action disparar, params string[] esperados)
    {
        _tocados.Clear();
        ulong t0 = Time.GetTicksUsec();
        disparar();
        foreach (var e in esperados)
        {
            if (!_tocados.Exists(t => t.StartsWith(e + "_", StringComparison.Ordinal)))
            {
                Falhar(nome, $"não tocou {e} (tocou: {string.Join(", ", _tocados)})");
                await EsperarSilencio();
                return;
            }
        }
        if (_som.VozesTocando == 0) { Falhar(nome, "nenhuma voz tocando logo depois do disparo"); return; }
        if (!await EsperarSilencio()) { Falhar(nome, $"não terminou em {EsperaMaxima} s"); return; }
        Ok($"{nome} ({string.Join(" + ", _tocados)}, {(Time.GetTicksUsec() - t0) / 1e6:F2} s)");
    }

    private async Task CasoPassos()
    {
        // Parado não faz barulho.
        _tocados.Clear();
        for (int i = 0; i < 30; i++) { _som.TocarPassos(new Vector3(-2f, 0f, 7f), 0.2f, 0); await Quadros(1); }
        if (_tocados.Count > 0) { Falhar("passo", $"parado tocou {_tocados.Count} passo(s)"); return; }

        // Correndo a 5 m/s por 1 s: ~3,3 passos/s, o primeiro em meio intervalo → 3 (aceita 2 a 5).
        // Cada passo tem que soar onde o jogador está naquele quadro.
        var pos = new Vector3(-3f, 0f, 7f);
        int vozesNovas = 0;
        string? foraDoLugar = null;
        ulong t0 = Time.GetTicksUsec();
        while (Time.GetTicksUsec() - t0 < 1_000_000)
        {
            pos.X += 5f * (float)GetProcessDeltaTime();
            var voz = NovaVoz(() => _som.TocarPassos(pos, 5f, 0));
            if (voz is not null)
            {
                vozesNovas++;
                if (voz.GlobalPosition.DistanceTo(pos) > 1e-3f) foraDoLugar ??= $"passo soou em {voz.GlobalPosition}, jogador em {pos}";
            }
            await Quadros(1);
        }
        int passos = _tocados.Count(t => EhDaFamilia(t, "passo"));
        if (passos is < 2 or > 5) { Falhar("passo", $"{passos} passos em 1 s a 5 m/s (esperado 2 a 5)"); return; }
        if (vozesNovas != passos) { Falhar("passo", $"{passos} passos disparados, {vozesNovas} vozes novas achadas"); return; }
        if (foraDoLugar is not null) { Falhar("passo", foraDoLugar); await EsperarSilencio(); return; }
        if (!await EsperarSilencio()) { Falhar("passo", "não terminou"); return; }
        Ok($"passo ({passos} passos em 1 s a 5 m/s, cada um onde o jogador estava: {string.Join(" + ", _tocados)}; parado, nenhum)");
    }

    private async Task CasoAmbiente()
    {
        _tocados.Clear();
        _som.IniciarAmbiente();
        await Esperar(0.5);
        if (!_tocados.Contains("ambiente_clube") || !_som.AmbienteTocando) { Falhar("ambiente_clube", "não começou"); return; }
        if (!_som.AmbienteEmLoop) { Falhar("ambiente_clube", "sem loop"); return; }
        _som.PararAmbiente();
        ulong t0 = Time.GetTicksUsec();
        while (_som.AmbienteTocando && (Time.GetTicksUsec() - t0) / 1e6 < SomNode.SegundosDeFadeDoAmbiente + 2) await Quadros(1);
        if (_som.AmbienteTocando) { Falhar("ambiente_clube", "não parou depois do fade"); return; }
        Ok($"ambiente_clube (loop; entrou e saiu com fade de {SomNode.SegundosDeFadeDoAmbiente} s em {(Time.GetTicksUsec() - t0) / 1e6:F2} s)");
    }

    private async Task CasoLimiteDeVozes()
    {
        _tocados.Clear();
        for (int i = 0; i < 40; i++) _som.TocarParede(new Vector3(i % 5 - 2f, 1.5f, 9.9f), grade: false);
        int vozes = _som.VozesTocando;
        if (_tocados.Count != 40) { Falhar("limite de vozes", $"{_tocados.Count} de 40 vidros dispararam (bola rouba a voz mais antiga)"); return; }
        if (vozes > SomNode.LimiteDeVozes) { Falhar("limite de vozes", $"{vozes} vozes tocando, limite {SomNode.LimiteDeVozes}"); return; }
        // Com o pool cheio de bolas, passo (menor prioridade) é descartado, não rouba. Corre 0,35 s (o vidro dura ~0,76 s):
        // tempo, não quadros — sem tela os quadros voam e o primeiro passo (em meio intervalo, ~0,14 s) nem chegaria.
        int descartadas = _som.VozesDescartadas;
        _tocados.Clear();
        var pos = new Vector3(0f, 0f, 5f);
        ulong t0 = Time.GetTicksUsec();
        while (Time.GetTicksUsec() - t0 < 350_000) { pos.X += 0.01f; _som.TocarPassos(pos, 6f, 3); await Quadros(1); }
        if (_tocados.Count > 0) { Falhar("limite de vozes", $"passo roubou voz de bola: {string.Join(", ", _tocados)}"); return; }
        if (_som.VozesDescartadas == descartadas) { Falhar("limite de vozes", "nenhum passo chegou a ser descartado: o caso não testou nada"); return; }
        _som.TocarPassos(pos, 0f, 3);
        if (!await EsperarSilencio()) { Falhar("limite de vozes", "não terminou"); return; }
        Ok($"limite de vozes ({vozes}/{SomNode.LimiteDeVozes} com 40 vidros de uma vez; {_som.VozesDescartadas - descartadas} passo(s) descartado(s) com o pool cheio)");
    }

    private async Task CasoPosicao()
    {
        const string nome = "posição 3D";
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou antes do caso"); return; }
        // Cada som num ponto diferente, longe da origem (onde a voz nasce): a voz que tocou tem que estar lá.
        var casos = new (string som, Vector3 ponto, Action<Vector3> tocar)[]
        {
            ("golpe perto", Perto, p => _som.TocarGolpe(p, "drive", 1f)),
            ("golpe longe", Longe, p => _som.TocarGolpe(p, "smash", 1f)),
            ("quique", new Vector3(2.5f, 0f, -4f), _som.TocarQuique),
            ("vidro", new Vector3(-4.9f, 1.4f, 9.9f), p => _som.TocarParede(p, grade: false)),
            ("grade", new Vector3(4.9f, 3.6f, -3f), p => _som.TocarParede(p, grade: true)),
            ("rede", new Vector3(-1f, 0.85f, 0f), _som.TocarRede),
        };
        foreach (var (som, ponto, tocar) in casos)
        {
            var voz = NovaVoz(() => tocar(ponto));
            string? erro = voz is null ? $"{som}: o disparo não ligou exatamente uma voz nova"
                : voz.GlobalPosition.DistanceTo(ponto) > 1e-3f ? $"{som}: a voz tocou em {voz.GlobalPosition}, pedido {ponto}"
                : null;
            if (erro is not null) { Falhar(nome, erro); await EsperarSilencio(); return; }
        }
        if (!await EsperarSilencio()) { Falhar(nome, "não terminou"); return; }
        Ok($"{nome} ({casos.Length} sons, cada voz no ponto pedido; os passos, no caso deles)");
    }

    private async Task CasoAltura()
    {
        const string nome = "altura ±5 %";
        // Vozes 3D: três rajadas de 15 golpes a partir do silêncio — as vozes tocando são exatamente as do disparo.
        var golpes = new List<float>();
        for (int r = 0; r < 3; r++)
        {
            if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
            for (int i = 0; i < 15; i++) _som.TocarGolpe(Perto, "voleio", 1f);
            var tocando = Vozes3DTocando();
            if (tocando.Count != 15) { Falhar(nome, $"{tocando.Count} vozes tocando depois de 15 golpes"); await EsperarSilencio(); return; }
            golpes.AddRange(tocando.Select(p => p.PitchScale));
        }
        // Público e bip (não posicionais): cada Ponto() reinicia os dois.
        var publico = new List<float>();
        var bip = new List<float>();
        for (int i = 0; i < 40; i++)
        {
            _som.Ponto(aFavorDaCasa: i % 2 == 0);
            var tocadorDoPublico = NaoPosicional("publico_");
            var tocadorDoBip = NaoPosicional("bip_placar_");
            if (tocadorDoPublico is null || tocadorDoBip is null) { Falhar(nome, "Ponto() não deixou público e bip tocando"); await EsperarSilencio(); return; }
            publico.Add(tocadorDoPublico.PitchScale);
            bip.Add(tocadorDoBip.PitchScale);
        }
        string? erro = ConferirAlturas("golpes", golpes) ?? ConferirAlturas("público", publico) ?? ConferirAlturas("bip", bip);
        if (!await EsperarSilencio()) { Falhar(nome, "não terminou"); return; }
        if (erro is not null) { Falhar(nome, erro); return; }
        Ok($"{nome} ({golpes.Count} golpes de {golpes.Min():F3} a {golpes.Max():F3}; público de {publico.Min():F3} a {publico.Max():F3}; " +
           $"bip de {bip.Min():F3} a {bip.Max():F3}, {publico.Count} pontos)");
    }

    /// <summary>Todas dentro de ±5 % e espalhadas pros dois lados de 1. Com 40 ou mais sorteios uniformes, ficarem todos
    /// acima de 0,99 (ou todos abaixo de 1,01) tem chance de 0,6^40 ≈ 1e-9: se acontece, a altura não está sendo sorteada.</summary>
    private static string? ConferirAlturas(string quem, List<float> alturas)
    {
        var fora = alturas.Where(a => Math.Abs(a - 1f) > AlturaMaxima + 1e-4f).ToList();
        if (fora.Count > 0) return $"{quem}: {fora.Count} de {alturas.Count} fora de ±5 % (ex.: {fora[0]:F4})";
        if (alturas.Min() > 0.99f || alturas.Max() < 1.01f)
            return $"{quem}: a altura não varia pros dois lados (de {alturas.Min():F4} a {alturas.Max():F4} em {alturas.Count} disparos)";
        return null;
    }

    private async Task CasoSorteio()
    {
        const string nome = "sorteio sem repetir";
        var casos = new (string familia, Action tocar)[]
        {
            ("golpe_drive", () => _som.TocarGolpe(Perto, "drive", 1f)),
            ("golpe_voleio", () => _som.TocarGolpe(Perto, "voleio", 1f)),
            ("golpe_smash", () => _som.TocarGolpe(Longe, "smash", 1f)),
            ("golpe_bandeja", () => _som.TocarGolpe(Longe, "bandeja", 1f)),
            ("golpe_lob", () => _som.TocarGolpe(Perto, "lob", 1f)),
            ("quique", () => _som.TocarQuique(new Vector3(0f, 0f, 5f))),
            ("vidro", () => _som.TocarParede(new Vector3(0f, 1f, 9.9f), grade: false)),
            ("grade", () => _som.TocarParede(new Vector3(5f, 3.5f, 0f), grade: true)),
            ("rede", () => _som.TocarRede(new Vector3(0f, 0.8f, 0f))),
        };
        foreach (var (familia, tocar) in casos)
        {
            // Do silêncio: um som de prioridade maior segurando o pool descartaria os disparos (bola não rouba golpe).
            if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
            _tocados.Clear();
            for (int i = 0; i < DisparosDoSorteio; i++) tocar();
            string? erro = ConferirSequencia(familia, [.. _tocados], DisparosDoSorteio);
            if (erro is not null) { Falhar(nome, erro); await EsperarSilencio(); return; }
        }
        // Público e bip, os não posicionais: cada Ponto() sorteia os dois.
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
        _tocados.Clear();
        for (int i = 0; i < DisparosDoSorteio; i++) _som.Ponto(aFavorDaCasa: true);
        for (int i = 0; i < DisparosDoSorteio; i++) _som.Ponto(aFavorDaCasa: false);
        List<string> sequencia = [.. _tocados];
        string? erroDoPonto =
            ConferirSequencia("bip_placar", [.. sequencia.Where(t => EhDaFamilia(t, "bip_placar"))], 2 * DisparosDoSorteio)
            ?? ConferirSequencia("publico_aplauso", [.. sequencia.Where(t => EhDaFamilia(t, "publico_aplauso"))], DisparosDoSorteio)
            ?? ConferirSequencia("publico_uhh", [.. sequencia.Where(t => EhDaFamilia(t, "publico_uhh"))], DisparosDoSorteio);
        if (!await EsperarSilencio()) { Falhar(nome, "não terminou"); return; }
        if (erroDoPonto is not null) { Falhar(nome, erroDoPonto); return; }
        Ok($"{nome} ({casos.Length + 3} famílias, {DisparosDoSorteio} disparos seguidos cada: nenhuma variação duas vezes seguidas, todas apareceram)");
    }

    /// <summary>Nenhuma variação duas vezes seguidas, e todas aparecem. Sem repetir a última, cada sorteio escolhe entre as
    /// outras: com 60 disparos e 5 variações, faltar uma tem chance de ~2e-7; e sem a regra, 60 disparos sem nenhuma
    /// repetição seguida teriam chance de 0,8^59 ≈ 2e-6.</summary>
    private string? ConferirSequencia(string familia, List<string> sequencia, int esperados)
    {
        if (sequencia.Count != esperados || !sequencia.All(t => EhDaFamilia(t, familia)))
            return $"{familia}: esperados {esperados} disparos da família, vieram {sequencia.Count} ({string.Join(", ", sequencia.Take(6))}…)";
        for (int i = 1; i < sequencia.Count; i++)
            if (sequencia[i] == sequencia[i - 1]) return $"{familia}: {sequencia[i]} duas vezes seguidas (disparos {i} e {i + 1} de {sequencia.Count})";
        int distintas = sequencia.Distinct().Count();
        if (distintas != _som.Variacoes(familia)) return $"{familia}: só {distintas} de {_som.Variacoes(familia)} variações em {sequencia.Count} disparos";
        return null;
    }

    private async Task CasoIntensidade()
    {
        const string nome = "intensidade do golpe";
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
        // Volume geral em 1 aqui: o caso do volume é o último e volta a 1.
        float[] intensidades = [-1f, 0f, 0.25f, 0.5f, 0.75f, 1f, 2f];
        var niveis = new float[intensidades.Length];
        for (int k = 0; k < intensidades.Length; k++)
        {
            float intensidade = intensidades[k];
            var voz = NovaVoz(() => _som.TocarGolpe(Perto, "drive", intensidade));
            if (voz is null) { Falhar(nome, $"intensidade {intensidade}: o disparo não ligou exatamente uma voz nova"); await EsperarSilencio(); return; }
            niveis[k] = voz.VolumeDb;
        }
        float abaixo = niveis[0], fraco = niveis[1], forte = niveis[5], acima = niveis[6];
        bool sobe = true;
        for (int k = 2; k <= 5; k++) sobe &= niveis[k] > niveis[k - 1] + 0.1f;
        string? erro =
            Math.Abs(forte - _som.DbGolpe) > 0.05f ? $"força 1 saiu a {forte:F2} dB, esperado o nível do golpe ({_som.DbGolpe:F2} dB)"
            : forte - fraco is < 8f or > 12f ? $"de 0 a 1 o golpe subiu {forte - fraco:F2} dB (esperado ~10)"
            : !sobe ? $"o volume não sobe a cada degrau de intensidade: {string.Join(" / ", niveis.Select(n => n.ToString("F2")))} dB"
            : Math.Abs(abaixo - fraco) > 0.05f || Math.Abs(acima - forte) > 0.05f ? $"intensidade fora de 0..1 não foi limitada: -1 → {abaixo:F2}, 2 → {acima:F2} dB"
            : null;
        if (!await EsperarSilencio()) { Falhar(nome, "não terminou"); return; }
        if (erro is not null) { Falhar(nome, erro); return; }
        Ok($"{nome} (0 → {fraco:F2} dB, 0,5 → {niveis[3]:F2}, 1 → {forte:F2}: sobe {forte - fraco:F2} dB; -1 e 2 limitados)");
    }

    private async Task CasoPrioridade()
    {
        const string nome = "prioridade golpe > bola";
        // Pool cheio de golpes: bola (quique, vidro, grade, rede) é descartada — não corta um golpe no meio.
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
        for (int i = 0; i < SomNode.LimiteDeVozes; i++) _som.TocarGolpe(Perto, "smash", 1f);
        if (ContarVozes("golpe_") != SomNode.LimiteDeVozes) { Falhar(nome, $"o pool não encheu de golpes ({ContarVozes("golpe_")})"); await EsperarSilencio(); return; }
        int descartadas = _som.VozesDescartadas;
        _tocados.Clear();
        _som.TocarQuique(new Vector3(0f, 0f, 4f));
        _som.TocarParede(new Vector3(3f, 1.2f, 10f), grade: false);
        _som.TocarParede(new Vector3(5f, 3.5f, 2f), grade: true);
        _som.TocarRede(new Vector3(0f, 0.8f, 0f));
        string? erro = _tocados.Count > 0 ? $"bola roubou voz de golpe: tocou {string.Join(", ", _tocados)}"
            : ContarVozes("golpe_") != SomNode.LimiteDeVozes ? $"sobraram {ContarVozes("golpe_")} golpes tocando"
            : _som.VozesDescartadas != descartadas + 4 ? $"{_som.VozesDescartadas - descartadas} bola(s) descartada(s), esperado 4"
            : null;
        if (erro is null)
        {
            // Golpe novo com o pool cheio de golpes: rouba um (o golpe que acabou de sair importa mais que o de antes).
            _tocados.Clear();
            _som.TocarGolpe(Longe, "drive", 1f);
            if (!_tocados.Exists(t => EhDaFamilia(t, "golpe_drive"))) erro = "golpe novo não tocou com o pool cheio de golpes";
        }
        if (erro is not null) { Falhar(nome, erro); await EsperarSilencio(); return; }

        // Pool cheio de bolas: golpe rouba a voz de uma delas.
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
        for (int i = 0; i < SomNode.LimiteDeVozes; i++) _som.TocarParede(new Vector3(i % 5 - 2f, 1.5f, 9.9f), grade: false);
        descartadas = _som.VozesDescartadas;
        _tocados.Clear();
        _som.TocarGolpe(Perto, "bandeja", 1f);
        int golpes = ContarVozes("golpe_"), vidros = ContarVozes("vidro_");
        if (!_tocados.Exists(t => EhDaFamilia(t, "golpe_bandeja")) || golpes != 1 || vidros != SomNode.LimiteDeVozes - 1 || _som.VozesDescartadas != descartadas)
        { Falhar(nome, $"com o pool cheio de vidros o golpe não roubou uma voz ({golpes} golpe(s) e {vidros} vidro(s) tocando)"); await EsperarSilencio(); return; }
        if (!await EsperarSilencio()) { Falhar(nome, "não terminou"); return; }
        Ok($"{nome} (pool cheio de golpes: 4 bolas descartadas e golpe novo rouba golpe; cheio de vidros: golpe rouba 1 vidro; passo, no limite de vozes)");
    }

    private async Task CasoVolume()
    {
        const string nome = "volume geral";
        if (!await EsperarSilencio()) { Falhar(nome, "o pool não esvaziou"); return; }
        // Tudo tocando ao mesmo tempo: ambiente (já entrando), público, bip e duas vozes 3D.
        _som.IniciarAmbiente();
        await Esperar(0.3);
        _som.Ponto(aFavorDaCasa: true);
        _som.TocarGolpe(Perto, "drive", 1f);
        _som.TocarParede(new Vector3(3f, 1.2f, 10f), grade: false);
        var tocadores = Tocadores();
        string? erro = tocadores.Count != SomNode.LimiteDeVozes + 3 ? $"{tocadores.Count} tocadores no SomNode, esperado {SomNode.LimiteDeVozes} vozes + ambiente, público e bip"
            : NaoPosicional("ambiente_clube") is null || NaoPosicional("publico_") is null || NaoPosicional("bip_placar_") is null || Vozes3DTocando().Count != 2
                ? "ambiente, público, bip e 2 vozes deviam estar tocando"
            : null;
        if (erro is not null) { Falhar(nome, erro); await EncerrarVolume(); return; }

        // Vale pro que já está tocando — e pro que está parado, que herda o nível no próximo disparo.
        float[] antes = Niveis(tocadores);
        _som.Volume(0.5f);
        float[] metade = Niveis(tocadores);
        _som.Volume(0f);
        float[] mudo = Niveis(tocadores);
        _som.Volume(1f);
        float[] depois = Niveis(tocadores);
        float menos6 = Mathf.LinearToDb(0.5f);
        for (int k = 0; k < tocadores.Count && erro is null; k++)
        {
            string quem = tocadores[k].Name;
            erro = Math.Abs(metade[k] - antes[k] - menos6) > 0.05f ? $"{quem}: volume 0,5 mudou {metade[k] - antes[k]:F2} dB (esperado {menos6:F2})"
                : mudo[k] - antes[k] > -60f ? $"{quem}: volume 0 deixou {mudo[k]:F2} dB (antes {antes[k]:F2})"
                : Math.Abs(depois[k] - antes[k]) > 0.05f ? $"{quem}: voltar a 1 deixou {depois[k]:F2} dB (antes {antes[k]:F2})"
                : null;
        }
        if (erro is not null) { Falhar(nome, erro); await EncerrarVolume(); return; }

        // Som novo com o volume em 0,5 também sai 6 dB abaixo, nos dois caminhos: voz 3D e público/bip.
        _som.Volume(0.5f);
        var voz = NovaVoz(() => _som.TocarGolpe(Longe, "lob", 1f));
        _som.Ponto(aFavorDaCasa: false);
        var uhh = NaoPosicional("publico_uhh");
        var bip = NaoPosicional("bip_placar_");
        erro = voz is null || uhh is null || bip is null ? "golpe, uhh e bip deviam estar tocando"
            : Math.Abs(voz.VolumeDb - (_som.DbGolpe + menos6)) > 0.05f ? $"golpe novo a {voz.VolumeDb:F2} dB, esperado {_som.DbGolpe + menos6:F2}"
            : Math.Abs(uhh.VolumeDb - (_som.DbUhh + menos6)) > 0.05f ? $"uhh novo a {uhh.VolumeDb:F2} dB, esperado {_som.DbUhh + menos6:F2}"
            : Math.Abs(bip.VolumeDb - (_som.DbBip + menos6)) > 0.05f ? $"bip novo a {bip.VolumeDb:F2} dB, esperado {_som.DbBip + menos6:F2}"
            : null;
        if (!await EncerrarVolume()) { Falhar(nome, erro ?? "não terminou"); return; }
        if (erro is not null) { Falhar(nome, erro); return; }
        Ok($"{nome} ({tocadores.Count} tocadores — {SomNode.LimiteDeVozes} vozes 3D, ambiente, público e bip — com 5 tocando: " +
           $"0,5 → {menos6:F2} dB em cada um, 0 → mudo, 1 → de volta; som novo em 0,5 sai {menos6:F2} dB abaixo)");
    }

    /// <summary>Volume de volta a 1, ambiente desligado (com o fade) e pool em silêncio.</summary>
    private async Task<bool> EncerrarVolume()
    {
        _som.Volume(1f);
        _som.PararAmbiente();
        ulong t0 = Time.GetTicksUsec();
        while (_som.AmbienteTocando && (Time.GetTicksUsec() - t0) / 1e6 < SomNode.SegundosDeFadeDoAmbiente + 2) await Quadros(1);
        return !_som.AmbienteTocando & await EsperarSilencio();
    }

    // ───────────────────── olhando os tocadores de verdade (não o que o SomNode diz que fez) ─────────────────────

    private List<AudioStreamPlayer3D> Vozes3DTocando() => [.. _som.GetChildren().OfType<AudioStreamPlayer3D>().Where(p => p.Playing)];

    /// <summary>A voz que o disparo ligou: a única que toca agora e não tocava antes. Só vale com vaga no pool (sem roubo).</summary>
    private AudioStreamPlayer3D? NovaVoz(Action disparar)
    {
        var antes = Vozes3DTocando().ToHashSet();
        disparar();
        var novas = Vozes3DTocando().Where(p => !antes.Contains(p)).ToList();
        return novas.Count == 1 ? novas[0] : null;
    }

    /// <summary>O tocador não posicional (ambiente, público, bip) tocando um arquivo que começa com o prefixo.</summary>
    private AudioStreamPlayer? NaoPosicional(string prefixo) =>
        _som.GetChildren().OfType<AudioStreamPlayer>().FirstOrDefault(p => p.Playing && NomeDoSom(p.Stream).StartsWith(prefixo, StringComparison.Ordinal));

    private int ContarVozes(string prefixo) => Vozes3DTocando().Count(p => NomeDoSom(p.Stream).StartsWith(prefixo, StringComparison.Ordinal));

    private List<Node> Tocadores() => [.. _som.GetChildren().Where(n => n is AudioStreamPlayer or AudioStreamPlayer3D)];

    private static float[] Niveis(List<Node> tocadores) => [.. tocadores.Select(n => n switch
    {
        AudioStreamPlayer p => p.VolumeDb,
        AudioStreamPlayer3D p => p.VolumeDb,
        _ => float.NaN,
    })];

    private static string NomeDoSom(AudioStream? stream) => stream is null ? "" : stream.ResourcePath.GetFile().GetBaseName();

    private static bool EhDaFamilia(string nome, string familia) => nome.StartsWith(familia + "_", StringComparison.Ordinal);

    private async Task<bool> EsperarSilencio()
    {
        ulong t0 = Time.GetTicksUsec();
        while (_som.VozesTocando > 0)
        {
            if ((Time.GetTicksUsec() - t0) / 1e6 > EsperaMaxima) return false;
            await Quadros(1);
        }
        return true;
    }

    private async Task Quadros(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Esperar(double segundos) =>
        await ToSignal(GetTree().CreateTimer(segundos), SceneTreeTimer.SignalName.Timeout);

    private void Ok(string texto)
    {
        _oks++;
        GD.Print($"ok {texto}");
    }

    private void Falhar(string nome, string motivo)
    {
        _falhas++;
        GD.PrintErr($"FALHOU {nome}: {motivo}");
    }
}
