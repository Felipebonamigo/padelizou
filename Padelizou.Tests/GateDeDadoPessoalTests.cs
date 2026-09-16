using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// GATE: DADO PESSOAL DE VERDADE NÃO ENTRA NO REPOSITÓRIO.
//
// 15/09/2026 — o repo é PÚBLICO, e tinha dado de gente real dentro. Não vazou por descuido de
// segredo: veio de PEDIDO DE SUPORTE copiado e colado direto pro código e pro STATUS.md, sempre
// com a melhor das intenções — documentar o caso que fez a correção existir.
//   · `JanelaDoParceiro.cs`, arquivo de PRODUÇÃO: *"troque o parceiro do paulo prass pelo
//     <cpf> cpf Arthur Prass"* — nome e CPF de pessoa real.
//   · `RecuperarSenhaPeloCpfTests.cs`: *"o usuário do CPF <cpf> não está conseguindo recuperar
//     sua senha"*.
// Eram 5 CPFs de verdade e 5 e-mails de verdade, em 12 arquivos.
//
// ⚠️ AS DUAS METADES DESTE GATE TÊM FORÇAS DIFERENTES, e vale saber qual é qual. O CPF é REGRA:
// dígito verificador válido fora da lista de sintéticos reprova, inclusive um que ninguém tenha
// visto ainda. O e-mail é LISTA DE CONHECIDOS: trava a volta destes, e não sabe julgar um
// endereço novo — não dá pra separar `alguem@gmail.com` (fixture) de um endereço real só
// olhando o domínio.
//
// ⚠️ ESTE GATE OLHA A ÁRVORE DE TRABALHO, NÃO O HISTÓRICO. O que já foi publicado continua nos
// commits antigos e isso ele NÃO conserta — some daqui pra frente, e é o que dá pra travar num
// teste. Limpar o histórico é decisão à parte, com reescrita de commits.
//
// COMO NÃO CAIR AQUI: em fixture e em comentário, use os CPFs sintéticos abaixo e e-mails
// `@exemplo.com`. Se o caso real precisa ser contado, conte SEM o dado — "o usuário não
// conseguia recuperar a senha" explica igual e não identifica ninguém.
public class GateDeDadoPessoalTests
{
    // Os CPFs sintéticos que a suíte já usa. São de padrão conhecido (111.444.777-35 e
    // parentes), gerados pra teste — e precisam de dígito verificador VÁLIDO porque
    // `Documentos.CpfEhValido` roda de verdade no caminho de inscrição.
    private static readonly HashSet<string> CpfsSinteticos = new()
    {
        "11144477735", "52998224725", "22255588846", "33366699957", "44477788827",
        "99900000005", "12345678909", "39053344705", "86412245561",

        // Da família inventada `5555NNNNNNN` dos torcedores do palpitômetro. Os irmãos dele
        // têm dígito INVÁLIDO e o gate nem os vê; este caiu num dígito válido por acaso —
        // é o falso positivo que a regra do dígito produz, e a lista é onde ele se resolve.
        "55551000001",
    };

    // O valor real NÃO fica aqui — guardar a lista de proibidos em texto seria republicar
    // exatamente o que este gate existe pra tirar. Fica o SHA-256.
    private static readonly HashSet<string> EmailsProibidos = new()
    {
        "4b2b824a9f06c1b85055349fdda9054fab967591adf37bcc1fbb8dcd419536e8",
        "0ee3db480365c7be587b6c2a951a566c1d09b075e83174310613793302994c70",
        "4ad7031ba07fcf26bec5779488b19551f4b6198f1ce7e5492dc7fe0e93f6a560",
        "a0304bd47d53ec3a869b6cdb0e65540fb1ebc663443e65eacaed812cf02a95b5",
        "757615236afc0508c4f8dafb9bf802b9203e17e8563876ea3da3784e3f9f13f4",
    };

    private static readonly string[] ExtensoesDeTexto =
        [".cs", ".cshtml", ".md", ".js", ".json", ".css", ".html", ".sh", ".yml", ".yaml", ".txt", ".sql", ".service"];

    [Fact]
    public void Nenhum_CPF_de_pessoa_real_no_repositorio()
    {
        var cpf = new Regex(@"(?<![\d.\-])(\d{3})\.?(\d{3})\.?(\d{3})-?(\d{2})(?![\d.\-])");
        var achados = new List<string>();

        foreach (var (caminho, texto) in ArquivosDeTexto())
            foreach (Match m in cpf.Matches(texto))
            {
                var digitos = string.Concat(m.Groups.Cast<Group>().Skip(1).Select(g => g.Value));
                if (DigitoVerificadorConfere(digitos) && !CpfsSinteticos.Contains(digitos))
                    achados.Add($"{caminho}: {digitos[..3]}…{digitos[^2..]} (linha {Linha(texto, m.Index)})");
            }

        Assert.True(achados.Count == 0,
            "CPF com dígito verificador válido fora da lista de sintéticos.\n"
            + "  · Veio de pessoa de verdade (pedido de suporte, base de produção)? TIRE do repositório.\n"
            + "  · Foi inventado e calhou de validar? Some em CpfsSinteticos, com o porquê.\n"
            + "  ⚠️ Na dúvida é a PRIMEIRA: pôr um CPF real na lista faz o gate mentir pra sempre.\n  "
            + string.Join("\n  ", achados.Distinct()));
    }

    [Fact]
    public void Nenhum_email_de_pessoa_real_no_repositorio()
    {
        var email = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase);
        var achados = new List<string>();

        foreach (var (caminho, texto) in ArquivosDeTexto())
            foreach (Match m in email.Matches(texto))
                if (EmailsProibidos.Contains(Sha256(m.Value.ToLowerInvariant())))
                    achados.Add($"{caminho} (linha {Linha(texto, m.Index)})");

        Assert.True(achados.Count == 0,
            "E-mail de pessoa real (bate com a lista de hashes proibidos):\n  "
            + string.Join("\n  ", achados.Distinct()));
    }

    // O gate só vale se estiver LENDO alguma coisa. Sem esta trava, um erro de caminho faria
    // os dois testes acima passarem varrendo zero arquivo — verde que não prova nada.
    [Fact]
    public void O_gate_esta_mesmo_varrendo_o_repositorio()
    {
        var arquivos = ArquivosDeTexto().ToList();
        Assert.True(arquivos.Count > 500, $"Varri só {arquivos.Count} arquivos — o caminho da raiz deve estar errado.");
        Assert.Contains(arquivos, a => a.caminho.EndsWith("STATUS.md", StringComparison.Ordinal));
    }

    private static IEnumerable<(string caminho, string texto)> ArquivosDeTexto()
    {
        var raiz = RaizDoRepo();
        foreach (var f in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(raiz, f);
            if (rel.Split(Path.DirectorySeparatorChar)
                   .Any(p => p is "bin" or "obj" or ".git" or "node_modules" or ".vs" or "lib")) continue;
            if (!ExtensoesDeTexto.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) continue;

            string texto;
            try { texto = File.ReadAllText(f); } catch (IOException) { continue; }
            yield return (rel, texto);
        }
    }

    private static int Linha(string texto, int indice) => texto.Take(indice).Count(c => c == '\n') + 1;

    private static string Sha256(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    private static bool DigitoVerificadorConfere(string c)
    {
        if (c.Length != 11 || c.All(d => d == c[0])) return false;
        for (var n = 9; n <= 10; n++)
        {
            var soma = 0;
            for (var i = 0; i < n; i++) soma += (c[i] - '0') * (n + 1 - i);
            if (soma * 10 % 11 % 10 != c[n] - '0') return false;
        }
        return true;
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
