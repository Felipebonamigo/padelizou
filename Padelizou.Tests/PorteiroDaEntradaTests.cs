using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A ENTRADA RESTRITA.
//
// O dev passou a rodar com uma cópia do banco de produção, e cópia de produção traz junto a
// SENHA de todo mundo. O portão de acesso antecipado não segura isso — `/Auth/Login` é caminho
// liberado nele de propósito —, então as contas reais entrariam no ensaio com a senha do dia a
// dia e veriam o próprio nome inscrito num torneio que não existe.
//
// Estes testes seguram a régua dos dois lados: no ambiente restrito só entra quem está na
// lista, e na PRODUÇÃO nada muda — porque o jeito de estragar isto é uma lista esquecida vazia
// trancando todo mundo do lado de fora do próprio site.
public class PorteiroDaEntradaTests
{
    private static Jogador Jogador(string? email = null, string? login = null, string cpf = "99900000001") =>
        new() { Id = 1, Nome = "Alguém", Cpf = cpf, Email = email, Login = login };

    [Fact]
    public void Sem_lista_qualquer_conta_entra()
    {
        // ⚠️ O TESTE MAIS IMPORTANTE DO ARQUIVO. É o estado da produção, e o de qualquer
        // ambiente que suba sem a chave. Vermelho aqui quer dizer que o site trancou os
        // usuários do lado de fora sem ninguém ter pedido isso.
        var porteiro = PorteiroDeTeste.Entrada();

        Assert.False(porteiro.Restringindo);
        Assert.True(porteiro.PodeEntrar(Jogador(email: "qualquer.um@gmail.com")));

        // Nem conta sem contato nenhum é barrada: sem lista, o porteiro não tem opinião.
        Assert.True(porteiro.PodeEntrar(Jogador()));
    }

    [Fact]
    public void Com_lista_so_entra_quem_esta_nela()
    {
        var porteiro = PorteiroDeTeste.Entrada("felipe.bonamigo@gmail.com", "Foka");

        Assert.True(porteiro.Restringindo);
        Assert.True(porteiro.PodeEntrar(Jogador(email: "felipe.bonamigo@gmail.com")));
        Assert.False(porteiro.PodeEntrar(Jogador(email: "jogador.de.verdade@gmail.com", login: "zeca")));
    }

    [Fact]
    public void A_lista_casa_por_email_login_OU_cpf()
    {
        // Quem escreve a lista está no meio de um deploy e usa o que tem à mão. Os três já são
        // identificadores de uma pessoa só — é o que o IdentidadeJogador garante.
        Assert.True(PorteiroDeTeste.Entrada("Foka").PodeEntrar(Jogador(login: "Foka")));
        Assert.True(PorteiroDeTeste.Entrada("99900000042").PodeEntrar(Jogador(cpf: "99900000042")));
        Assert.True(PorteiroDeTeste.Entrada("alguem@gmail.com").PodeEntrar(Jogador(email: "alguem@gmail.com")));
    }

    [Theory]
    [InlineData("FOKA")]
    [InlineData("  foka  ")]
    public void Maiuscula_e_espaco_nao_trancam_ninguem_pra_fora(string comoChegou)
    {
        // Um espaço colado sem querer na configuração não pode ser a diferença entre entrar e
        // ficar de fora do próprio ambiente de teste.
        Assert.True(PorteiroDeTeste.Entrada(comoChegou).PodeEntrar(Jogador(login: "Foka")));
        Assert.True(PorteiroDeTeste.Entrada("Foka").PodeEntrar(Jogador(login: comoChegou)));
    }

    [Fact]
    public void Cookie_de_conta_que_nao_existe_mais_NAO_entra()
    {
        // Acontece de verdade neste ambiente: o banco é trocado por baixo (nova cópia da
        // produção) enquanto navegadores por aí seguem com cookie de 90 dias. Com o ambiente
        // restrito, "não sei quem é" se resolve pro lado de fora.
        Assert.False(PorteiroDeTeste.Entrada("Foka").PodeEntrar(null));
    }

    [Fact]
    public void Sem_lista_ate_cookie_orfao_passa()
    {
        // O contrário do de cima, e de propósito: em produção quem responde "esta conta pode?"
        // é o resto do sistema, não este porteiro. Ele só existe quando há uma lista.
        Assert.True(PorteiroDeTeste.Entrada().PodeEntrar(null));
    }

    [Fact]
    public void Conta_sem_email_nem_login_nao_entra_por_engano()
    {
        // Pré-cadastro (jogador inscrito pelo organizador) nasce sem e-mail e sem login. Se a
        // comparação tratasse vazio como "casa com qualquer coisa", ele entraria em massa.
        Assert.False(PorteiroDeTeste.Entrada("Foka").PodeEntrar(
            new Jogador { Id = 2, Nome = "Pré-cadastro", Cpf = "99900000002" }));
    }
}
