using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// O STATUS QUE UM **POST** COM ERRO DEVOLVE.
//
// ⚠️ Todo POST que dava erro chegava ao cliente como **400**, escondendo o status real.
// Medido em produção (18/08/2026, build-579): `GET /rota-que-nao-existe` devolvia 404
// certinho, e `POST` na MESMA rota devolvia 400. O webhook de pagamento recusado por token
// respondia 400 no lugar do 401 que o controller retorna — e o log do app registrava a recusa
// direitinho, então a divergência só aparecia de fora.
//
// A causa não estava em controller nenhum: `UseStatusCodePagesWithReExecute` preserva o
// MÉTODO, então o 404/401 virava um POST para `/Home/NaoEncontrado`, esse POST batia no
// filtro global de antiforgery e era recusado com 400 — e 400 era o que sobrava na resposta.
//
// ⚠️ POR QUE ISSO IMPORTA MAIS DO QUE UM NÚMERO ERRADO: 400 quer dizer "corpo malformado".
// Quem for investigar um dia começa procurando JSON quebrado, quando o problema era rota
// inexistente ou credencial recusada. O status é a primeira pista de qualquer diagnóstico, e
// esse mandava para o lado errado.
//
// A correção é de pipeline: a tela de erro só entra em GET/HEAD. Página de erro é para GENTE
// NAVEGANDO; quem chama por POST é formulário, app ou serviço, e para esses o que serve é o
// status cru.
public class StatusDePostComErroTests
{
    private static string Program()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Program.cs")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Program.cs"));
    }

    // ⚠️ TESTE DE CÓDIGO-FONTE, e é o único jeito honesto aqui: o defeito é da ORDEM do
    // pipeline, não de uma regra que dê pra chamar. Um teste de controller passa nos dois
    // mundos — o `NaoEncontrado` sempre devolveu o status certo; quem trocava era a camada
    // acima dele. Mesmo motivo do teste de CSS do dropdown e do token do webhook.
    [Fact]
    public void A_tela_de_erro_so_vale_para_GET_e_HEAD()
    {
        var program = Program();

        // A chamada continua existindo (o teste dos colegas em StatusDaTelaDeErroTests
        // garante a FORMA dela) — o que se cobra aqui é que ela esteja dentro de um ramo
        // condicionado ao método.
        Assert.Matches(
            @"UseWhen\(\s*\r?\n?\s*ctx\s*=>\s*HttpMethods\.IsGet\(ctx\.Request\.Method\)\s*\|\|\s*HttpMethods\.IsHead\(ctx\.Request\.Method\)",
            program);

        // E que o ReExecute esteja no ramo, não solto no pipeline. Solto = todo POST com erro
        // volta a virar 400.
        Assert.Matches(
            @"ramo\s*=>\s*ramo\.UseStatusCodePagesWithReExecute\(",
            program);
    }

    // ⚠️ A OUTRA METADE: não pode sobrar uma chamada SOLTA. Alguém que reintroduza a linha
    // original — numa limpeza, num merge malfeito — traria o defeito de volta com o ramo novo
    // ainda no arquivo, e o teste acima continuaria verde.
    [Fact]
    public void Nao_sobrou_nenhum_ReExecute_fora_do_ramo()
    {
        var program = Program();

        var todas = Regex.Matches(program, @"UseStatusCodePagesWithReExecute\(");
        var noRamo = Regex.Matches(program, @"ramo\.UseStatusCodePagesWithReExecute\(");

        Assert.Equal(noRamo.Count, todas.Count);
        Assert.Single(todas);
    }
}
