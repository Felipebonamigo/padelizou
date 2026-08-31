using Padelizou.Services;

namespace Padelizou.Tests;

// E-MAIL DIGITADO ERRADO NÃO ENTRA MAIS CALADO (28/08/2026).
//
// 🐛 O CASO REAL: o Pedro se cadastrou com `pedrojunior_1978@hotmial.com` — "hotmial", não
// "hotmail". O `<input type="email">` da tela achou ótimo, porque a SINTAXE está perfeita: tem
// arroba, tem domínio, tem ponto. E no servidor não havia validação nenhuma.
//
// O estrago é silencioso e só aparece no pior momento: e-mail de confirmação de aula não
// chega, e "Esqueci minha senha" — que é a ÚNICA saída de quem esqueceu — manda o link pra um
// domínio que não existe. A pessoa fica trancada fora da própria conta sem entender por quê.
//
// ⚠️ POR QUE O TYPO É RECUSADO E NÃO SÓ AVISADO: um domínio a UMA letra de gmail/hotmail não é
// caixa postal de ninguém — é engano de dedo ou domínio de phishing. Nos dois casos, mandar
// link de redefinição de senha pra lá é pior que recusar o cadastro. E a recusa não deixa a
// pessoa presa: a mensagem DIZ o endereço certo, e ela conserta numa edição.
public class EmailDoCadastroTests
{
    // ── Sintaxe: o que o `type="email"` já pega na tela, e o servidor não pegava ──────────

    [Theory]
    [InlineData("felipe@padelizou.com.br")]
    [InlineData("pedro.junior_1978@hotmail.com")]
    [InlineData("a@b.co")]
    public void Endereco_bom_passa(string email) => Assert.Null(EmailDoCadastro.Problema(email));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba.com")]
    [InlineData("dois@@arrobas.com")]
    [InlineData("sem@dominio")]          // sem ponto depois do arroba
    [InlineData("@semnome.com")]
    [InlineData("com espaco@gmail.com")]
    public void Endereco_quebrado_e_recusado(string email) =>
        Assert.NotNull(EmailDoCadastro.Problema(email));

    // ── O caso do Pedro: sintaxe boa, domínio que não existe ──────────────────────────────

    [Fact]
    public void O_erro_do_Pedro_e_recusado_e_a_mensagem_diz_o_certo()
    {
        var problema = EmailDoCadastro.Problema("pedrojunior_1978@hotmial.com");

        Assert.NotNull(problema);
        // A mensagem tem que entregar o endereço PRONTO: mandar a pessoa "conferir o domínio"
        // é devolver pra ela o trabalho que a máquina acabou de fazer.
        Assert.Contains("pedrojunior_1978@hotmail.com", problema);
    }

    [Theory]
    [InlineData("gmai.com", "gmail.com")]
    [InlineData("gmial.com", "gmail.com")]
    [InlineData("gmail.co", "gmail.com")]
    [InlineData("hotmial.com", "hotmail.com")]
    [InlineData("hotmail.co", "hotmail.com")]
    [InlineData("outlok.com", "outlook.com")]
    [InlineData("yaho.com", "yahoo.com")]
    [InlineData("icloud.co", "icloud.com")]
    public void Dominio_a_uma_letra_de_um_grande_vira_sugestao(string errado, string certo)
    {
        var problema = EmailDoCadastro.Problema($"alguem@{errado}");

        Assert.NotNull(problema);
        Assert.Contains($"alguem@{certo}", problema);
    }

    // ⚠️ A CONTRAPROVA, e é ela que impede a régua de virar uma peneira: domínio de verdade
    // NÃO pode ser confundido com typo só por ser pequeno ou desconhecido. Se qualquer um
    // destes for recusado, a validação está barrando gente com e-mail bom — que é um estrago
    // maior que o typo que ela conserta.
    [Theory]
    [InlineData("felipe@padelizou.com.br")]
    [InlineData("contato@uol.com.br")]
    [InlineData("joao@bol.com.br")]
    [InlineData("maria@terra.com.br")]
    [InlineData("ana@empresa-pequena.com.br")]
    [InlineData("jose@ig.com.br")]
    [InlineData("pedro@live.com")]
    [InlineData("carla@me.com")]
    [InlineData("lucas@protonmail.com")]
    public void Dominio_de_verdade_nunca_e_confundido_com_typo(string email) =>
        Assert.Null(EmailDoCadastro.Problema(email));

    // O domínio certo, escrito certo, obviamente passa — o teste existe porque uma régua de
    // distância mal escrita recusaria o PRÓPRIO alvo dela.
    [Theory]
    [InlineData("alguem@gmail.com")]
    [InlineData("alguem@hotmail.com")]
    [InlineData("alguem@outlook.com")]
    [InlineData("alguem@yahoo.com.br")]
    public void O_dominio_certo_passa(string email) => Assert.Null(EmailDoCadastro.Problema(email));

    // Maiúscula e espaço nas pontas são do dedo, não do endereço.
    [Fact]
    public void Maiuscula_e_espaco_nas_pontas_nao_atrapalham()
    {
        Assert.Null(EmailDoCadastro.Problema("  Pedro.Junior@Hotmail.COM  "));
        Assert.NotNull(EmailDoCadastro.Problema("  Pedro@HOTMIAL.com  "));
    }
}
