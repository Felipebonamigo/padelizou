using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// Os Termos de Uso são a outra metade da Política de Privacidade: ela diz o que fazemos com
// os dados, eles dizem as regras do serviço — quem responde pelo torneio, quanto custa a taxa,
// e por que um aviso que não chegou não vale como desculpa.
//
// O risco aqui não é a página sumir: é ela ENVELHECER. Taxa e mensalidade existem em
// configuração justamente pra serem renegociadas sem republicar o site; um contrato que
// repete esses números à mão vira, na primeira renegociação, uma promessa pública diferente
// do que a máquina cobra — e aí é prova contra quem escreveu.
public class TermosDeUsoTests
{
    // ⚠️ O teste que não pode ser apagado: é ele que impede a página de prometer preço velho.
    [Fact]
    public void Os_numeros_saem_da_configuracao_e_nao_do_texto()
    {
        var pagina = Pagina();

        Assert.Contains("taxas.ComissaoPercentualExterno", pagina);
        Assert.Contains("taxas.ComissaoPercentualSomentePix", pagina);
        Assert.Contains("taxas.ComissaoPercentualTodasFormas", pagina);
        Assert.Contains("plano.PercentualAvulso", pagina);
        Assert.Contains("plano.MensalidadeAssinante", pagina);

        // Percentual escrito à mão ("15%", "3,5%") é o defeito que este arquivo existe pra
        // impedir. Os da página saem de @taxas/@plano e o "%" fica FORA do número.
        var percentualCravado = Regex.Match(pagina, @"\d+([,.]\d+)?\s*%");
        Assert.False(percentualCravado.Success,
            $"Os termos passaram a trazer um percentual escrito à mão ('{percentualCravado.Value}'). "
            + "Taxa mora em TaxasExibicao/PlanoProfessorSettings pra mudar sem republicar — "
            + "repetida aqui, ela vira promessa pública diferente do que o sistema cobra.");

        var valorCravado = Regex.Match(pagina, @"R\$\s*\d");
        Assert.False(valorCravado.Success,
            $"Os termos passaram a trazer um valor em reais escrito à mão ('{valorCravado.Value}'). "
            + "Mesmo motivo do percentual: mensalidade e anuidade vêm da configuração.");
    }

    // O rodapé aponta pra uma action; se ela não existir, o link do site inteiro dá 404 e
    // ninguém descobre — o defeito é mudo, como o da política que ficou anos em branco.
    [Fact]
    public void O_link_do_rodape_aponta_pra_uma_action_que_existe()
    {
        var layout = Arquivo(Path.Combine("Views", "Shared", "_Layout.cshtml"));
        Assert.Matches(@"asp-action=""Termos""", layout);

        var controller = Arquivo(Path.Combine("Controllers", "HomeController.cs"));
        Assert.Matches(@"IActionResult\s+Termos\s*\(", controller);
    }

    // Mesma regra da casa que vale na política: pro usuário existe "meio de pagamento".
    [Fact]
    public void Nao_expoe_o_nome_do_gateway_de_pagamento()
    {
        Assert.False(Pagina().Contains("asaas", StringComparison.OrdinalIgnoreCase),
            "Os termos passaram a citar o gateway pelo nome — na tela ele é 'meio de pagamento'.");
    }

    // As três coisas que fazem estes termos servirem pra alguma coisa quando algo dá errado.
    [Fact]
    public void Diz_quem_responde_pelo_torneio_pela_aula_e_pelo_aviso()
    {
        var pagina = Pagina();

        Assert.Contains("organizador", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("professor", pagina, StringComparison.OrdinalIgnoreCase);

        // O canal de aviso já falhou aqui de todo jeito (cota de e-mail estourada, WhatsApp
        // fora do ar, push que alcança poucos aparelhos). Dizer que a fonte oficial é o site
        // é honestidade, e é o que evita "mas eu não fui avisado".
        Assert.Contains("Avisos não são garantia", pagina);
    }

    // Os dois documentos se citam: quem chega num quer achar o outro.
    [Fact]
    public void Aponta_para_a_politica_de_privacidade()
    {
        Assert.Matches(@"asp-action=""Privacy""", Pagina());
    }

    private static string Pagina() =>
        Regex.Replace(Arquivo(Path.Combine("Views", "Home", "Termos.cshtml")),
            @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
