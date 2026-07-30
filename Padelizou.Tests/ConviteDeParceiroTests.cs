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

        Assert.True(ConviteDeParceiro.Valido(DuplaSemParceiro(token), Abertas, token));
    }

    [Fact]
    public void Token_errado_nao_vale()
    {
        var dupla = DuplaSemParceiro(ConviteDeParceiro.NovoToken());

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, ConviteDeParceiro.NovoToken()));
        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, ""));
        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, null));
    }

    [Fact]
    public void Dupla_que_ja_tem_parceiro_nao_aceita_mais_convite()
    {
        // É a corrida entre duas pessoas com o mesmo link: quem chegar depois é recusado.
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);
        dupla.Jogador2Id = 20;

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, token));
        Assert.Equal("Essa dupla já está completa — alguém aceitou antes.",
            ConviteDeParceiro.MotivoDeNaoValer(dupla, Abertas));
    }

    [Fact]
    public void Convite_morre_quando_as_inscricoes_encerram()
    {
        // Depois do sorteio a dupla já está numa chave — entrar aí bagunçaria os jogos.
        var token = ConviteDeParceiro.NovoToken();
        var dupla = DuplaSemParceiro(token);

        Assert.False(ConviteDeParceiro.Valido(dupla, "Fase de Grupos", token));
        Assert.Equal("As inscrições deste torneio já foram encerradas.",
            ConviteDeParceiro.MotivoDeNaoValer(dupla, "Fase de Grupos"));
    }

    [Fact]
    public void Dupla_sem_token_nao_aceita_convite_nenhum()
    {
        // Token virou null ao ser aceito: o mesmo link não fecha a dupla duas vezes.
        var dupla = DuplaSemParceiro(ConviteDeParceiro.NovoToken());
        dupla.ConviteToken = null;

        Assert.False(ConviteDeParceiro.Valido(dupla, Abertas, "qualquer-coisa"));
    }

    [Fact]
    public void Dupla_inexistente_nao_estoura_e_explica()
    {
        Assert.False(ConviteDeParceiro.Valido(null, Abertas, "token"));
        Assert.Equal("Esse convite não existe mais.", ConviteDeParceiro.MotivoDeNaoValer(null, Abertas));
    }
}
