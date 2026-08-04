using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Na lista de inscritos do "Interno Los Corneteiros" conviviam "ALAN DA SILVEIRA MACHADO" e
// "charls gustavio polese" — cada um digita como quer, e junto numa lista fica feio. A caixa
// é arrumada só na EXIBIÇÃO: o nome é dado da pessoa e a coluna continua com o que ela
// escreveu.
public class NomeBonitoTests
{
    [Theory]
    [InlineData("ALAN DA SILVEIRA MACHADO", "Alan da Silveira Machado")]
    [InlineData("charls gustavio polese", "Charls Gustavio Polese")]
    [InlineData("Mateus muller figueiredo", "Mateus Muller Figueiredo")]
    [InlineData("HENDERSON TAKAHAMA", "Henderson Takahama")]
    [InlineData("Alexandre Medina", "Alexandre Medina")]
    public void Nome_torto_vira_nome_de_lista(string digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Theory]
    [InlineData("JOAO DA SILVA", "Joao da Silva")]
    [InlineData("maria dos santos", "Maria dos Santos")]
    [InlineData("PEDRO DE OLIVEIRA E SOUZA", "Pedro de Oliveira e Souza")]
    [InlineData("ana das neves", "Ana das Neves")]
    public void Particula_no_meio_do_nome_fica_minuscula(string digitado, string esperado)
    {
        // "Alan da Silveira", não "Alan Da Silveira".
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Fact]
    public void Particula_no_COMECO_continua_maiuscula()
    {
        // Quem se chama "Del Rey" perderia a maiúscula do próprio primeiro nome.
        Assert.Equal("Del Rey Consultoria", NomeBonito.Formatar("DEL REY CONSULTORIA"));
        Assert.Equal("Da Silva", NomeBonito.Formatar("da silva"));
    }

    [Theory]
    [InlineData("McDonald")]
    [InlineData("DiCaprio")]
    [InlineData("MacLeod")]
    [InlineData("O'Brien")]
    public void Quem_tem_maiuscula_no_meio_escreveu_assim_de_proposito(string nome)
    {
        // O rolo compressor viraria "Mcdonald" — estragando justamente o nome de quem se deu
        // ao trabalho de digitar certo.
        Assert.Equal(nome, NomeBonito.Formatar(nome));
    }

    [Theory]
    [InlineData("ana-maria souza", "Ana-Maria Souza")]
    [InlineData("d'avila", "D'Avila")]
    [InlineData("JOSE M. DA COSTA", "Jose M. da Costa")]
    public void Hifen_apostrofo_e_ponto_comecam_pedaco_novo(string digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Theory]
    [InlineData("  ALAN   DA   SILVA  ", "Alan da Silva")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Espaco_sobrando_e_vazio_nao_quebram(string? digitado, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.Formatar(digitado));
    }

    [Fact]
    public void Acento_sobrevive()
    {
        Assert.Equal("Otávio Wunsch Júnior", NomeBonito.Formatar("OTÁVIO WUNSCH JÚNIOR"));
        Assert.Equal("Eric Hübner", NomeBonito.Formatar("eric hübner"));
    }

    // ── O que a tela realmente usa ────────────────────────────────────────────────────────

    [Fact]
    public void O_que_esta_GRAVADO_nao_muda()
    {
        // O ponto da decisão: arrumar aparência não pode reescrever dado da pessoa.
        var jogador = new Jogador { Nome = "ALAN DA SILVEIRA MACHADO", Cpf = "11144477735" };

        Assert.Equal("Alan da Silveira Machado", jogador.NomeNaTela);
        Assert.Equal("ALAN DA SILVEIRA MACHADO", jogador.Nome);
    }

    [Fact]
    public void Apelido_tambem_sai_arrumado()
    {
        var jogador = new Jogador { Nome = "LUCAS ALMEIDA", Cpf = "11144477735", Apelido = "FOKA" };

        Assert.Equal("Foka", jogador.ComoChamar);
        Assert.Equal("Lucas Almeida (Foka)", jogador.NomeComApelido);
    }

    [Fact]
    public void A_dupla_mostra_os_dois_arrumados()
    {
        var dupla = new Dupla
        {
            Codigo = "D1",
            Jogador1 = new Jogador { Nome = "ALAN DA SILVEIRA MACHADO", Cpf = "11144477735" },
            Jogador2 = new Jogador { Nome = "henderson takahama", Cpf = "22255588846" },
        };

        Assert.Equal("Alan da Silveira Machado & Henderson Takahama", dupla.NomeDeExibicao);
    }

    [Fact]
    public void Time_continua_escrito_como_foi_cadastrado()
    {
        // Nome de time é marca, não nome de pessoa: "ST Led" e "Target.it" perderiam a
        // identidade se passassem pela mesma régua.
        var dupla = new Dupla { Codigo = "T1", NomeTime = "ST Led" };

        Assert.Equal("ST Led", dupla.NomeDeExibicao);
    }
}
