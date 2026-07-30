using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Padelizou.Tests;

// O app instalado no celular é montado a partir de DOIS arquivos que ninguém compila: o
// manifest.json e o sw.js. Errar um nome de arquivo em qualquer um dos dois não gera aviso
// nenhum — o build passa, o site abre normalmente, e o estrago só aparece no celular de outra
// pessoa:
//
//   * no sw.js, `cache.addAll` é tudo-ou-nada: UM caminho errado derruba a instalação inteira
//     do service worker. Sem cache, sem tela offline, sem push. E em silêncio.
//   * no manifest, um ícone ou print que não existe faz o convite de instalação virar uma
//     caixa quebrada, ou simplesmente não aparecer.
//
// Como esses arquivos citam imagens pelo caminho, qualquer renomeada em wwwroot/image os quebra
// à distância. Estes testes são o fio que liga uma coisa na outra.
public class PwaArquivosTests
{
    private static string Wwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "manifest.json")))
            dir = dir.Parent;

        Assert.True(dir != null, "Não achei a pasta wwwroot subindo a partir de " + AppContext.BaseDirectory);
        return Path.Combine(dir!.FullName, "Padelizou", "wwwroot");
    }

    private static string CaminhoLocal(string src) =>
        Path.Combine(Wwwroot(), src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static JsonElement Manifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Wwwroot(), "manifest.json"))).RootElement;

    private static IEnumerable<JsonElement> Declarados()
    {
        var m = Manifest();
        foreach (var i in m.GetProperty("icons").EnumerateArray()) yield return i;
        foreach (var s in m.GetProperty("screenshots").EnumerateArray()) yield return s;
    }

    [Fact]
    public void Todo_icone_e_print_do_manifest_existe_no_disco()
    {
        var sumidos = Declarados()
            .Select(d => d.GetProperty("src").GetString()!)
            .Distinct()
            .Where(src => !File.Exists(CaminhoLocal(src)))
            .ToList();

        Assert.True(sumidos.Count == 0,
            "manifest.json aponta pra arquivo que não existe: " + string.Join(", ", sumidos));
    }

    // O `sizes` não é enfeite: é por ele que o navegador escolhe o ícone e monta o convite de
    // instalação. Declarar 192 e entregar 512 faz o Android descartar o ícone em silêncio.
    [Fact]
    public void Tamanho_declarado_bate_com_o_tamanho_real_da_imagem()
    {
        var erros = new List<string>();

        foreach (var d in Declarados())
        {
            var src = d.GetProperty("src").GetString()!;
            var declarado = d.GetProperty("sizes").GetString()!;

            using var codec = SKCodec.Create(CaminhoLocal(src));
            Assert.True(codec != null, $"Não consegui ler a imagem {src}");

            var real = $"{codec!.Info.Width}x{codec.Info.Height}";
            if (real != declarado) erros.Add($"{src}: manifest diz {declarado}, arquivo tem {real}");
        }

        Assert.True(erros.Count == 0, string.Join(" | ", erros));
    }

    // Regra do Chrome: prints de celular ("narrow") precisam ter TODOS a mesma proporção, senão
    // ele descarta o conjunto e volta pra barrinha sem graça — o oposto do motivo de existirem.
    [Fact]
    public void Prints_de_celular_tem_todos_a_mesma_proporcao()
    {
        var proporcoes = Manifest().GetProperty("screenshots").EnumerateArray()
            .Where(s => s.GetProperty("form_factor").GetString() == "narrow")
            .Select(s =>
            {
                var partes = s.GetProperty("sizes").GetString()!.Split('x');
                return (Src: s.GetProperty("src").GetString()!, Proporcao: Math.Round(double.Parse(partes[0]) / double.Parse(partes[1]), 3));
            })
            .ToList();

        Assert.NotEmpty(proporcoes);
        Assert.True(proporcoes.Select(p => p.Proporcao).Distinct().Count() == 1,
            "Prints de celular com proporções diferentes: " +
            string.Join(", ", proporcoes.Select(p => $"{p.Src}={p.Proporcao}")));
    }

    [Fact]
    public void Todo_arquivo_guardado_pelo_service_worker_existe_no_disco()
    {
        var sumidos = ArquivosDoServiceWorker().Where(a => !File.Exists(CaminhoLocal(a))).ToList();

        Assert.True(sumidos.Count == 0,
            "sw.js manda guardar arquivo que não existe (isso derruba o service worker inteiro): "
            + string.Join(", ", sumidos));
    }

    // A tela de "sem internet" só aparece se ela mesma tiver sido guardada antes — buscar a
    // página offline PELA REDE, justamente quando a rede caiu, não funcionaria nunca.
    [Fact]
    public void A_pagina_offline_esta_entre_os_arquivos_guardados()
    {
        Assert.Contains("/offline.html", ArquivosDoServiceWorker());
    }

    private static List<string> ArquivosDoServiceWorker()
    {
        var sw = File.ReadAllText(Path.Combine(Wwwroot(), "sw.js"));

        var lista = Regex.Match(sw, @"const STATIC_ASSETS = \[(.*?)\];", RegexOptions.Singleline);
        Assert.True(lista.Success, "Não achei STATIC_ASSETS no sw.js — o formato do arquivo mudou.");

        // A lista mistura texto entre aspas com a constante PAGINA_OFFLINE. Resolver a constante
        // aqui é de propósito: se alguém renomear a tela offline só no topo do sw.js, o teste
        // continua conferindo o caminho novo em vez de um valor fixo escrito aqui.
        var offline = Regex.Match(sw, @"const PAGINA_OFFLINE = ""(?<v>[^""]+)""");
        Assert.True(offline.Success, "Não achei PAGINA_OFFLINE no sw.js.");

        return lista.Groups[1].Value
            .Split(',')
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Select(e => e == "PAGINA_OFFLINE" ? offline.Groups["v"].Value : e.Trim('"'))
            .ToList();
    }
}
