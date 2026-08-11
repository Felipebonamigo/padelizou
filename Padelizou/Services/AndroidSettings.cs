using System.Text.RegularExpressions;

namespace Padelizou.Services;

// O app da Play Store é o próprio site dentro de uma casca (TWA — Trusted Web Activity).
// O que faz o Android confiar nessa casca é UM arquivo público:
// https://padelizou.com.br/.well-known/assetlinks.json
//
// Ele diz "o app com este nome de pacote, assinado com esta chave, pode abrir meus links".
// Sem ele o app até abre, mas com a barra de endereço do Chrome por cima — deixa de parecer
// app, que é a única coisa que a loja compra. E a falha é MUDA: nada quebra, nada loga.
//
// Por que em configuração e não no repositório: a impressão digital só existe depois que a
// chave de assinatura é criada, e ela MUDA quando o Google assina por cima (Play App Signing,
// que é o padrão). Preencher por drop-in do systemd deixa o Felipe corrigir sem republicar —
// mesmo raciocínio da chave Pix e do CNPJ da política de privacidade.
public class AndroidSettings
{
    // ⚠️ O nome do pacote é DEFINITIVO: depois que o app sobe uma vez, o Google não deixa
    // trocar — seria outro app, com outra ficha e outros instaladores. Combinar aqui e no
    // Bubblewrap ANTES do primeiro envio (ver ANDROID.md).
    public string PackageName { get; set; } = "br.com.padelizou.app";

    // Uma ou mais impressões digitais SHA-256, separadas por vírgula, ponto e vírgula ou
    // espaço. Na prática são DUAS: a da chave de upload (a que fica na máquina do Felipe) e a
    // que o Google gera ao reassinar. As duas precisam estar aqui, senão o app instalado pela
    // loja não é reconhecido — o teste local passa e só o usuário final vê a barra de endereço.
    public string Sha256Fingerprints { get; set; } = "";

    public IReadOnlyList<string> ImpressoesDigitais => Normalizar(Sha256Fingerprints).Validas;

    // O que foi preenchido mas não é uma impressão digital. Existe pra ser reclamado no log:
    // uma delas com um dígito a menos derrubaria o arquivo inteiro pro Google, então elas são
    // descartadas — e descartar em silêncio é justamente o defeito que se paga caro depois.
    public IReadOnlyList<string> ImpressoesInvalidas => Normalizar(Sha256Fingerprints).Invalidas;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(PackageName) && ImpressoesDigitais.Count > 0;

    // Formato exigido pelo Google (assetlinks.json). A estrutura é fixa; o que varia é o
    // pacote e as impressões digitais.
    public object MontarAssetLinks() => new[]
    {
        new
        {
            relation = new[] { "delegate_permission/common.handle_all_urls" },
            target = new
            {
                @namespace = "android_app",
                package_name = PackageName,
                sha256_cert_fingerprints = ImpressoesDigitais.ToArray()
            }
        }
    };

    // A mesma impressão digital aparece escrita de jeitos diferentes dependendo de onde é
    // copiada: o Play Console mostra com dois-pontos e em maiúsculas, o keytool às vezes em
    // minúsculas, e copiar da tela costuma trazer espaço e quebra de linha junto. Tudo isso
    // é a mesma chave — normalizar aqui evita um "não sei por que não valida" que só se
    // enxerga comparando 64 caracteres a olho.
    public static (IReadOnlyList<string> Validas, IReadOnlyList<string> Invalidas) Normalizar(string? bruto)
    {
        var validas = new List<string>();
        var invalidas = new List<string>();

        foreach (var pedaco in (bruto ?? "").Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var hex = pedaco.Replace(":", "").ToUpperInvariant();

            if (hex.Length == 64 && Regex.IsMatch(hex, "^[0-9A-F]+$"))
                validas.Add(string.Join(":", Enumerable.Range(0, 32).Select(i => hex.Substring(i * 2, 2))));
            else
                invalidas.Add(pedaco);
        }

        return (validas, invalidas);
    }
}
