using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Pra onde vai o dinheiro que é do Padelizou: a chave Pix do CNPJ, o nome e a cidade que
// aparecem no aplicativo de quem paga.
public record DadosDoPix(string Chave, string Nome, string Cidade);

// A chave Pix do Padelizou, guardada no banco e editada pelo painel do admin.
//
// Por que NÃO no systemd, como o resto da configuração: trocar a chave Pix é operação de
// negócio, não de deploy. Se um dia a conta mudar, quem troca é o Felipe pelo celular — e
// enquanto a chave estiver errada, todo Pix cai na conta errada. Isso não pode depender de
// abrir ssh no servidor. É o mesmo raciocínio do portão de acesso (ver PortaoDeAcesso).
//
// Sem cache de propósito, ao contrário do portão: isto é lido em duas telas que quase ninguém
// abre, e não em toda requisição. Uma consulta na hora vale menos que uma cópia em memória
// que pode ficar velha.
public static class ChavePixDoPadelizou
{
    public const string ChaveDaChave = "PixDireto.Chave";
    public const string ChaveDoNome = "PixDireto.Nome";
    public const string ChaveDaCidade = "PixDireto.Cidade";

    // Null = ninguém configurou ainda. Quem chama trata isso como "Pix indisponível" e cai
    // no gateway — nunca como erro.
    public static async Task<DadosDoPix?> LerAsync(DbPadelContext context)
    {
        var chaves = new[] { ChaveDaChave, ChaveDoNome, ChaveDaCidade };

        var linhas = await context.ConfiguracoesDoSistema
            .AsNoTracking()
            .Where(c => chaves.Contains(c.Chave))
            .ToDictionaryAsync(c => c.Chave, c => c.Valor);

        linhas.TryGetValue(ChaveDaChave, out var chave);
        linhas.TryGetValue(ChaveDoNome, out var nome);
        linhas.TryGetValue(ChaveDaCidade, out var cidade);

        // Configuração pela metade é o mesmo que nenhuma: um BR Code sem chave não recebe
        // dinheiro, e um sem nome é recusado pelo banco do pagador.
        if (Problema(chave, nome, cidade) != null) return null;

        return new DadosDoPix(chave!.Trim(), nome!.Trim(), cidade!.Trim());
    }

    // A validação, separada da leitura porque a tela de configuração precisa dela ANTES de
    // gravar — o admin tem que ver o que está faltando enquanto ainda está no formulário.
    public static string? Problema(string? chave, string? nome, string? cidade)
    {
        if (string.IsNullOrWhiteSpace(chave)) return "Informe a chave Pix que vai receber.";
        if (string.IsNullOrWhiteSpace(nome)) return "Informe o nome do recebedor (o que aparece no app de quem paga).";
        if (string.IsNullOrWhiteSpace(cidade)) return "Informe a cidade do recebedor.";

        // 77 é o teto do campo no BR Code (o caso é a chave de e-mail). Acima disso o código
        // sai malformado e nenhum banco lê.
        if (chave.Trim().Length > 77) return "Chave Pix longa demais — o limite é 77 caracteres.";

        return null;
    }

    public static async Task GravarAsync(DbPadelContext context,
        string chave, string nome, string cidade, int? porJogadorId)
    {
        await DefinirAsync(context, ChaveDaChave, chave.Trim(), porJogadorId);
        await DefinirAsync(context, ChaveDoNome, nome.Trim(), porJogadorId);
        await DefinirAsync(context, ChaveDaCidade, cidade.Trim(), porJogadorId);

        await context.SaveChangesAsync();
    }

    private static async Task DefinirAsync(DbPadelContext context, string chave, string valor, int? porJogadorId)
    {
        var linha = await context.ConfiguracoesDoSistema.FirstOrDefaultAsync(c => c.Chave == chave);

        if (linha == null)
        {
            linha = new ConfiguracaoDoSistema { Chave = chave };
            context.ConfiguracoesDoSistema.Add(linha);
        }

        linha.Valor = valor;
        linha.AtualizadoEm = DateTime.Now;
        linha.AtualizadoPorJogadorId = porJogadorId;
    }
}
