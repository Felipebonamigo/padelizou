using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O convite que fecha a dupla sem ninguém digitar o CPF do outro. Antes, definir o parceiro
// exigia os 11 dígitos do CPF dele — que ninguém sabe de cabeça — então inscrever a dupla
// dependia de uma conversa por fora antes de o site conseguir ajudar.
public class ConviteDeParceiroTests
{
    private const string Abertas = "Inscrições Abertas";

    private static Dupla DuplaSemParceiro(string token) => new()
    {
        Id = 1,
        CategoriaId = 1,
        Jogador1Id = 10,
        ConviteToken = token,
        ConviteCriadoEm = DateTime.Now,
    };

    [Fact]
    public void Token_novo_e_longo_e_nao_repete()
    {
        var a = ConviteDeParceiro.NovoToken();
        var b = ConviteDeParceiro.NovoToken();

        // O link vai por WhatsApp: token curto seria chutável na força bruta.
        Assert.True(a.Length >= 40, $"token curto demais: {a.Length} chars");
        Assert.NotEqual(a, b);
        // base64url: sem +, / ou = pra não quebrar dentro de uma URL.
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }

    [Fact]
    public void Convite_vale_com_token_certo_dupla_incompleta_e_inscricoes_abertas()
    {
        var token = ConviteDeParceiro.NovoToken();

        Assert.True(ConviteDeParceiro.Valido(DuplaSemParceiro(token), Abertas, token, jaComecouAJogar: false));
    }

    [Fact]
    public void Token_errado_nao_vale()
    {
        var dupla = DuplaSemParceiro(ConviteDeParceiro.NovoToken());

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, ConviteDeParceiro.NovoToken(), jaComecouAJogar: false));
        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, "", jaComecouAJogar: false));
        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, null, jaComecouAJogar: false));
    }

    [Fact]
    public void Dupla_que_ja_tem_parceiro_nao_aceita_mais_convite()
    {
        // É a corrida entre duas pessoas com o mesmo link: quem chegar depois é recusado.
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);
        dupla.Jogador2Id = 20;

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, token, jaComecouAJogar: false));
        Assert.Equal("Essa dupla já está completa — alguém aceitou antes.",
            ConviteDeParceiro.MotivoDeNaoValer(dupla, Abertas, jaComecouAJogar: false));
    }

    // 09/09/2026: o convite deixou de morrer com o fim das inscrições. Como a dupla sem
    // parceiro passa a ENTRAR na chave, o link precisa continuar valendo até a bola rolar pra
    // ela — mandar o link no WhatsApp em cima da hora é o jeito mais comum de salvar a vaga.
    [Fact]
    public void Convite_continua_valendo_depois_do_sorteio_enquanto_a_dupla_nao_jogou()
    {
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);

        Assert.True(ConviteDeParceiro.Valido(dupla, "Fase de Grupos", token, jaComecouAJogar: false));
    }

    [Fact]
    public void Convite_morre_quando_a_dupla_entra_em_quadra()
    {
        // A bola rolou com a vaga vazia: pendurar um nome no jogo depois seria reescrever o
        // que já foi jogado.
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);

        Assert.False(ConviteDeParceiro.Valido(dupla, "Fase de Grupos", token, jaComecouAJogar: true));
        Assert.Equal("Essa dupla já entrou em quadra — não dá mais pra definir o parceiro.",
            ConviteDeParceiro.MotivoDeNaoValer(dupla, "Fase de Grupos", jaComecouAJogar: true));
    }

    [Fact]
    public void Convite_morre_com_o_torneio_cancelado()
    {
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);

        Assert.False(ConviteDeParceiro.Valido(dupla, "Cancelado", token, jaComecouAJogar: false));
    }

    [Fact]
    public void Dupla_sem_token_nao_aceita_convite_nenhum()
    {
        // Token virou null ao ser aceito: o mesmo link não fecha a dupla duas vezes.
        var dupla = DuplaSemParceiro(ConviteDeParceiro.NovoToken());
        dupla.ConviteToken = null;

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, "qualquer-coisa", jaComecouAJogar: false));
    }

    [Fact]
    public void Dupla_inexistente_nao_estoura_e_explica()
    {
        Assert.False(ConviteDeParceiro.Valido(null, Abertas, "token", jaComecouAJogar: false));
        Assert.Equal("Esse convite não existe mais.", ConviteDeParceiro.MotivoDeNaoValer(null, Abertas, jaComecouAJogar: false));
    }
}
