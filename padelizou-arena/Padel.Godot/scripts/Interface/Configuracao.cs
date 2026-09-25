using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>Como a próxima partida vai ser jogada.</summary>
public enum ModoDeJogo { Local, CoopLocal, CriarSala, EntrarNaSala, Demonstracao }

/// <summary>O que aconteceu ao ler o arquivo de configuração.</summary>
public enum ResultadoDaCarga { ArquivoAusente, Carregado, Corrompido }

/// <summary>
/// O que foi escolhido pra próxima partida. Estática de propósito: sobrevive à troca de cena
/// (Menu → Partida → Menu) sem precisar de autoload.
/// As preferências (dificuldade, formato, golpe, mão, nome, sala, volume) vão pra <c>user://configuracao.json</c>;
/// o que vale só pra esta execução (modo, semente, sair-após, screenshot) não é salvo.
/// Este arquivo não usa Godot — dá pra exercitar num console .NET qualquer. A parte que fala com o Godot
/// (caminho <c>user://</c>, volume no barramento de áudio) está em <c>Configuracao.Godot.cs</c>.
/// </summary>
public static partial class Configuracao
{
    public const int PortaPadrao = 7777;
    public const string EnderecoPadrao = "127.0.0.1";
    public const string NomePadrao = "Jogador";
    public const int TamanhoMaximoDoNome = 16;
    public const float VolumePadrao = 0.8f;

    private static int _setsParaVencer;
    private static string _nome = NomePadrao;
    private static string _endereco = EnderecoPadrao;
    private static int _portaDaSala = PortaPadrao;
    private static int _portaParaCriar = PortaPadrao;
    private static float _volume = VolumePadrao;

    public static ModoDeJogo Modo { get; set; }
    public static Dificuldade Dificuldade { get; set; }
    public static bool PontoDeOuro { get; set; }

    /// <summary>1 = set único; 2 = melhor de 3.</summary>
    public static int SetsParaVencer { get => _setsParaVencer; set => _setsParaVencer = Math.Clamp(value, 1, 2); }

    public static ModoDeGolpe ModoDeGolpe { get; set; }
    public static bool Destro { get; set; }

    /// <summary>Sem espaço nas pontas, no máximo <see cref="TamanhoMaximoDoNome"/> caracteres; vazio vira o padrão.</summary>
    public static string NomeDoJogador { get => _nome; set => _nome = LimparNome(value); }

    /// <summary>IP ou nome da máquina que hospeda a sala (sem porta).</summary>
    public static string EnderecoDaSala
    {
        get => _endereco;
        set => _endereco = EnderecoValido(value) ? value.Trim() : throw new ArgumentException($"Endereço inválido: '{value}'", nameof(value));
    }

    public static int PortaDaSala { get => _portaDaSala; set => _portaDaSala = ExigirPorta(value); }
    public static int PortaParaCriar { get => _portaParaCriar; set => _portaParaCriar = ExigirPorta(value); }

    /// <summary>Volume geral, 0 a 1.</summary>
    public static float Volume { get => _volume; set => _volume = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : VolumePadrao; }

    public static uint? Semente { get; set; }
    public static double? SairApos { get; set; }
    public static string? Screenshot { get; set; }

    /// <summary>
    /// A linha de comando pediu partida direta (--auto, --sair-apos, --host, --conectar): o menu não espera ninguém.
    /// Vale mesmo com o valor do argumento inválido — sem tela (CI), esperar no menu é travar pra sempre.
    /// </summary>
    public static bool PularMenu { get; private set; }

    static Configuracao() => RestaurarPadroes();

    /// <summary>Tudo de volta ao padrão, inclusive o que veio da linha de comando.</summary>
    public static void RestaurarPadroes()
    {
        RestaurarPreferencias();
        Modo = ModoDeJogo.Local;
        Semente = null;
        SairApos = null;
        Screenshot = null;
        PularMenu = false;
    }

    private static void RestaurarPreferencias() => Aplicar(new EscolhasSalvas());

    /// <summary>
    /// Lê os argumentos depois de "--" (OS.GetCmdlineUserArgs) por cima do que já está carregado.
    /// Os antigos: --auto, --semente N, --sair-apos S, --screenshot ARQ, --auto-golpe, --facil, --dificil.
    /// Os novos: --host PORTA, --conectar IP:PORTA, --nome NOME, --coop. Quando dois pedem modos diferentes, vale o último.
    /// Argumento desconhecido é ignorado em silêncio (a cena pode ter os seus, como --tela e --estado);
    /// valor inválido de um argumento conhecido é ignorado com aviso — a lista devolvida. Os que pulam o menu
    /// (--host, --conectar, --sair-apos) pulam mesmo assim: --host e --conectar ficam com a sala salva.
    /// --semente e --sair-apos são lidos exatamente como o PartidaNode lê, pra menu e partida não discordarem.
    /// </summary>
    public static List<string> LerLinhaDeComando(string[] args)
    {
        var avisos = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? valor = i + 1 < args.Length ? args[i + 1] : null;
            switch (arg)
            {
                case "--auto":
                    Modo = ModoDeJogo.Demonstracao;
                    PularMenu = true;
                    break;
                case "--coop":
                    Modo = ModoDeJogo.CoopLocal;
                    break;
                case "--auto-golpe": ModoDeGolpe = ModoDeGolpe.Automatico; break;
                case "--facil": Dificuldade = Dificuldade.Facil; break;
                case "--dificil": Dificuldade = Dificuldade.Dificil; break;
                case "--semente":
                    // A leitura do PartidaNode: uint.TryParse(texto) — NumberStyles.Integer.
                    if (valor is not null && uint.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var semente)) { Semente = semente; i++; }
                    else avisos.Add($"--semente precisa de um número inteiro positivo (veio '{valor}')");
                    break;
                case "--sair-apos":
                    PularMenu = true;
                    if (valor is not null && TentarLerSegundos(valor, out double segundos))
                    {
                        SairApos = segundos;
                        if (valor.Contains(','))
                            avisos.Add($"--sair-apos: '{valor}' foi lido como {segundos.ToString(CultureInfo.InvariantCulture)} s — a vírgula é separador de milhar; pra fração, use ponto (1.5)");
                        i++;
                    }
                    else avisos.Add($"--sair-apos precisa de segundos (veio '{valor}')");
                    break;
                case "--screenshot":
                    if (!string.IsNullOrWhiteSpace(valor) && !valor.StartsWith("--", StringComparison.Ordinal)) { Screenshot = valor; i++; }
                    else avisos.Add("--screenshot precisa do caminho do arquivo .png");
                    break;
                case "--nome":
                    if (!string.IsNullOrWhiteSpace(valor) && !valor.StartsWith("--", StringComparison.Ordinal)) { NomeDoJogador = valor; i++; }
                    else avisos.Add("--nome precisa de um nome");
                    break;
                case "--host":
                    // A porta é opcional: "--host" sozinho cria a sala na porta de sempre.
                    Modo = ModoDeJogo.CriarSala;
                    PularMenu = true;
                    if (valor is not null && !valor.StartsWith("--", StringComparison.Ordinal))
                    {
                        if (TentarLerPorta(valor, out int porta)) PortaParaCriar = porta;
                        else avisos.Add($"--host: porta inválida '{valor}', usando {PortaParaCriar}");
                        i++;
                    }
                    break;
                case "--conectar":
                    // Como o --host: endereço ruim não segura o jogo no menu — entra na sala salva, com aviso.
                    Modo = ModoDeJogo.EntrarNaSala;
                    PularMenu = true;
                    if (valor is not null && !valor.StartsWith("--", StringComparison.Ordinal))
                    {
                        if (TentarLerEndereco(valor, out string host, out int portaDaSala))
                        {
                            EnderecoDaSala = host;
                            PortaDaSala = portaDaSala;
                        }
                        else avisos.Add($"--conectar: endereço inválido '{valor}', usando a sala salva ({SalaSalva()})");
                        i++;
                    }
                    else avisos.Add($"--conectar precisa de IP:PORTA; usando a sala salva ({SalaSalva()})");
                    break;
            }
        }
        return avisos;
    }

    /// <summary>
    /// A leitura do --sair-apos no PartidaNode: double.TryParse com a cultura invariante, que aceita separador de
    /// milhar ("1,5" é 15). Negativo e NaN não valem; infinito vale (lá, é "sair quando a partida acabar").
    /// </summary>
    private static bool TentarLerSegundos(string texto, out double segundos) =>
        double.TryParse(texto, CultureInfo.InvariantCulture, out segundos) && segundos >= 0;

    private static string SalaSalva() => EnderecoDaSala.Contains(':') ? $"[{EnderecoDaSala}]:{PortaDaSala}" : $"{EnderecoDaSala}:{PortaDaSala}";

    /// <summary>Porta de 1 a 65535, só dígitos.</summary>
    public static bool TentarLerPorta(string? texto, out int porta)
    {
        porta = 0;
        return int.TryParse(texto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out porta) && PortaValida(porta);
    }

    public static bool PortaValida(int porta) => porta is >= 1 and <= 65535;

    /// <summary>IPv4, IPv6 (sem colchetes) ou nome de máquina.</summary>
    public static bool EnderecoValido(string? endereco) =>
        !string.IsNullOrWhiteSpace(endereco) && Uri.CheckHostName(endereco.Trim()) != UriHostNameType.Unknown;

    /// <summary>
    /// "192.168.0.10:7777", "meu-pc.local:7777", "[::1]:7777" ou só o endereço (porta padrão).
    /// A análise é a do <see cref="Uri"/>, que já sabe de colchete de IPv6 e de porta fora da faixa.
    /// </summary>
    public static bool TentarLerEndereco(string? texto, out string host, out int porta)
    {
        host = "";
        porta = PortaPadrao;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        texto = texto.Trim();
        // IPv6 sem colchete e sem porta ("::1"): o Uri não aceita, mas é um endereço completo.
        if (texto.Count(c => c == ':') > 1 && !texto.StartsWith('[') && Uri.CheckHostName(texto) == UriHostNameType.IPv6)
        {
            host = texto;
            return true;
        }
        if (!Uri.TryCreate("sala://" + texto, UriKind.Absolute, out var uri)) return false;
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0) return false;
        host = uri.IdnHost;   // sem colchetes no IPv6
        if (!uri.IsDefaultPort) porta = uri.Port;
        return EnderecoValido(host) && PortaValida(porta);
    }

    private static int ExigirPorta(int porta) =>
        PortaValida(porta) ? porta : throw new ArgumentOutOfRangeException(nameof(porta), porta, "A porta vai de 1 a 65535.");

    private static string LimparNome(string? nome)
    {
        string limpo = new string((nome ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpo.Length > TamanhoMaximoDoNome) limpo = limpo[..TamanhoMaximoDoNome].TrimEnd();
        return limpo.Length == 0 ? NomePadrao : limpo;
    }

    // ---- Arquivo ----

    /// <summary>
    /// Lê as preferências de <paramref name="caminho"/>. Arquivo ausente ou corrompido (JSON inválido, valor
    /// desconhecido) volta tudo ao padrão e segue; um valor fora da faixa volta só aquele campo ao padrão.
    /// Não mexe no que veio da linha de comando — por isso se chama antes de <see cref="LerLinhaDeComando"/>.
    /// </summary>
    public static ResultadoDaCarga Carregar(string caminho)
    {
        if (!File.Exists(caminho))
        {
            RestaurarPreferencias();
            return ResultadoDaCarga.ArquivoAusente;
        }
        try
        {
            var escolhas = JsonSerializer.Deserialize(File.ReadAllText(caminho), ContextoDeJson.Default.EscolhasSalvas);
            if (escolhas is null)
            {
                RestaurarPreferencias();
                return ResultadoDaCarga.Corrompido;
            }
            Aplicar(escolhas);
            return ResultadoDaCarga.Carregado;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            RestaurarPreferencias();
            return ResultadoDaCarga.Corrompido;
        }
    }

    /// <summary>
    /// Grava as preferências em <paramref name="caminho"/>. Escreve num temporário e troca, pra uma queda no meio
    /// não deixar o arquivo pela metade. Falha de disco não derruba o jogo: volta false com a mensagem.
    /// </summary>
    public static bool Salvar(string caminho, out string? erro)
    {
        erro = null;
        string temporario = caminho + ".tmp";
        try
        {
            string? pasta = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
            File.WriteAllText(temporario, JsonSerializer.Serialize(Escolhas(), ContextoDeJson.Default.EscolhasSalvas));
            File.Move(temporario, caminho, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            erro = e.Message;
            return false;
        }
    }

    /// <summary>O mesmo JSON que <see cref="Salvar"/> grava.</summary>
    public static string ParaJson() => JsonSerializer.Serialize(Escolhas(), ContextoDeJson.Default.EscolhasSalvas);

    private static EscolhasSalvas Escolhas() => new()
    {
        Dificuldade = Dificuldade,
        PontoDeOuro = PontoDeOuro,
        SetsParaVencer = SetsParaVencer,
        ModoDeGolpe = ModoDeGolpe,
        Destro = Destro,
        Nome = NomeDoJogador,
        EnderecoDaSala = EnderecoDaSala,
        PortaDaSala = PortaDaSala,
        PortaParaCriar = PortaParaCriar,
        Volume = Volume,
    };

    /// <summary>Aplica campo a campo; o que estiver fora da faixa fica com o padrão.</summary>
    private static void Aplicar(EscolhasSalvas e)
    {
        var padrao = new EscolhasSalvas();
        Dificuldade = Enum.IsDefined(e.Dificuldade) ? e.Dificuldade : padrao.Dificuldade;
        PontoDeOuro = e.PontoDeOuro;
        SetsParaVencer = e.SetsParaVencer is 1 or 2 ? e.SetsParaVencer : padrao.SetsParaVencer;
        ModoDeGolpe = Enum.IsDefined(e.ModoDeGolpe) ? e.ModoDeGolpe : padrao.ModoDeGolpe;
        Destro = e.Destro;
        NomeDoJogador = e.Nome ?? padrao.Nome;
        _endereco = EnderecoValido(e.EnderecoDaSala) ? e.EnderecoDaSala.Trim() : EnderecoPadrao;
        _portaDaSala = PortaValida(e.PortaDaSala) ? e.PortaDaSala : PortaPadrao;
        _portaParaCriar = PortaValida(e.PortaParaCriar) ? e.PortaParaCriar : PortaPadrao;
        Volume = e.Volume;
    }

    /// <summary>
    /// O que vai pro arquivo. Os valores iniciais são os padrões: campo ausente no JSON fica com o padrão.
    /// <c>set</c>, e não <c>init</c>, de propósito: com <c>init</c> o gerador do System.Text.Json monta o objeto
    /// passando todas as propriedades, e campo ausente vira <c>default</c> (false, 0) em vez do padrão.
    /// </summary>
    internal sealed record EscolhasSalvas
    {
        public int Versao { get; set; } = 1;
        public Dificuldade Dificuldade { get; set; } = Dificuldade.Medio;
        public bool PontoDeOuro { get; set; } = true;
        public int SetsParaVencer { get; set; } = 1;
        public ModoDeGolpe ModoDeGolpe { get; set; } = ModoDeGolpe.Manual;
        public bool Destro { get; set; } = true;
        public string Nome { get; set; } = NomePadrao;
        public string EnderecoDaSala { get; set; } = EnderecoPadrao;
        public int PortaDaSala { get; set; } = PortaPadrao;
        public int PortaParaCriar { get; set; } = PortaPadrao;
        public float Volume { get; set; } = VolumePadrao;
    }

    // Gerado em tempo de compilação: sem reflexão, o que o Godot pede pra não travar o descarregamento do assembly no editor.
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(EscolhasSalvas))]
    internal sealed partial class ContextoDeJson : JsonSerializerContext;
}
