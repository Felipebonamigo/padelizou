using SkiaSharp;

namespace Padelizou.Services;

// A fonte com que os cards compartilháveis são desenhados — lida de ARQUIVO, nunca do sistema
// operacional.
//
// ⚠️ ISTO NÃO É CAPRICHO DE ESTILO, É A ÚNICA COISA QUE FUNCIONA EM PRODUÇÃO. O projeto usa o
// pacote `SkiaSharp.NativeAssets.Linux.NoDependencies` (ver Padelizou.csproj), que é o Skia
// compilado SEM fontconfig: no VPS o gerenciador de fontes do sistema nasce VAZIO. Aqui no
// Windows `SKTypeface.Default` cai no DirectWrite e desenha lindamente; no Linux ele devolve
// uma fonte sem glifo nenhum, e o card sai com fundo, moldura, foto e escudo — e TODO texto
// invisível. Sem exceção, sem log, sem nada que denuncie.
//
// É a mesma família de armadilha do fuso horário e do `Migrate()` dentro de try/catch: passa
// verde em tudo aqui e mente no cliente. Por isso a fonte é arquivo nosso, versionada junto do
// código, e por isso `Disponivel` existe — faltando o arquivo, o recurso RECUSA desenhar (quem
// chama devolve 404) em vez de entregar um retângulo mudo que ninguém sabe explicar.
//
// Poppins é a fonte de display do site (ver wwwroot/css/site.css) e é OFL — redistribuir junto
// do código é permitido, inclusive em repositório público, desde que a licença vá junto. Ela
// está em wwwroot/fonts/OFL.txt e não deve ser removida.
public sealed class FonteDoCartao
{
    // Só a Poppins, e não a dupla Poppins+Inter do site: card é texto de display do começo ao
    // fim (nome, número, categoria). Inter só ganharia de Poppins em parágrafo corrido, que
    // aqui não existe — e seria mais 300 KB no publish pra nada.
    public const string NomeDaPasta = "fonts";

    public const string ArquivoForte = "Poppins-Bold.ttf";
    public const string ArquivoMedia = "Poppins-SemiBold.ttf";
    public const string ArquivoNormal = "Poppins-Regular.ttf";

    private readonly ILogger<FonteDoCartao>? _logger;

    // Nulas quando o arquivo não foi encontrado. Ninguém deve usá-las sem passar por
    // `Disponivel` — e nenhum caminho do sistema cai em `SKTypeface.Default` de propósito.
    public SKTypeface? Forte { get; }
    public SKTypeface? Media { get; }
    public SKTypeface? Normal { get; }

    public bool Disponivel => Forte != null && Media != null && Normal != null;

    // O que dizer a um humano quando não dá pra desenhar. Vai pro log e pro admin, nunca pro
    // jogador — pra ele o botão simplesmente não aparece.
    public string? Impedimento { get; }

    public FonteDoCartao(IWebHostEnvironment ambiente, ILogger<FonteDoCartao>? logger = null)
        : this(Path.Combine(ambiente.WebRootPath ?? "", NomeDaPasta), logger)
    {
    }

    // Construtor por CAMINHO pra o teste poder apontar pra uma pasta vazia e provar que o
    // sistema recusa desenhar — que é justamente o caso que não dá pra reproduzir aqui no
    // Windows de outro jeito, porque aqui sempre há fonte de sistema pra encobrir a falta.
    public FonteDoCartao(string pasta, ILogger<FonteDoCartao>? logger = null)
    {
        _logger = logger;

        Forte = Carregar(pasta, ArquivoForte);
        Media = Carregar(pasta, ArquivoMedia);
        Normal = Carregar(pasta, ArquivoNormal);

        if (Disponivel) return;

        var faltando = new[] { (ArquivoForte, Forte), (ArquivoMedia, Media), (ArquivoNormal, Normal) }
            .Where(f => f.Item2 == null)
            .Select(f => f.Item1)
            .ToList();

        Impedimento =
            $"A fonte dos cards não foi carregada (falta {string.Join(", ", faltando)} em {pasta}). "
            + "Sem ela o card sairia com todos os textos invisíveis no Linux, então nenhum é gerado.";

        // Erro, e não aviso: é recurso público que deixa de existir, e o sintoma no cliente
        // ("o botão de compartilhar sumiu") não aponta pra cá sozinho.
        _logger?.LogError("{Impedimento}", Impedimento);
    }

    private SKTypeface? Carregar(string pasta, string arquivo)
    {
        try
        {
            var caminho = Path.Combine(pasta, arquivo);
            if (!File.Exists(caminho)) return null;

            // FromFile e não FromFamilyName: o nome da família passaria pelo gerenciador do
            // sistema, que é exatamente quem não existe no VPS.
            return SKTypeface.FromFile(caminho);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha ao ler a fonte {Arquivo} dos cards.", arquivo);
            return null;
        }
    }
}
