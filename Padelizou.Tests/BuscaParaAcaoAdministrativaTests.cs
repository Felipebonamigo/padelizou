using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A tela de teste de aviso do painel precisa achar a pessoa pelo que o admin tem na mão:
// login, e-mail, nome completo, apelido ou CPF. Nome não é único — e mandar o teste pro
// homônimo errado é pior que não mandar, porque o admin conclui que testou.
public class BuscaParaAcaoAdministrativaTests
{
    private static DbPadelContext Base()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Lucas Almeida Coelho", Apelido = "Foka",
                          Login = "foka", Email = "almeidalucascoelho@gmail.com", Cpf = "11111111111" },
            new Jogador { Id = 2, Nome = "Lucas Pereira", Login = "lucasp",
                          Email = "lucasp@exemplo.com", Cpf = "22222222222" },
            new Jogador { Id = 3, Nome = "Ana Beatriz", Login = "ana",
                          Email = "ana@exemplo.com", Cpf = "33333333333" });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<List<Jogador>> Achar(DbPadelContext ctx, string? termo) =>
        await BuscaJogador.ParaAcaoAdministrativaAsync(ctx, termo);

    [Theory]
    [InlineData("foka")]
    [InlineData("FOKA")]
    [InlineData("  foka  ")]
    [InlineData("almeidalucascoelho@gmail.com")]
    public async Task Login_e_email_acham_direto(string termo)
    {
        using var ctx = Base();

        var achados = await Achar(ctx, termo);

        Assert.Single(achados);
        Assert.Equal("Lucas Almeida Coelho", achados[0].Nome);
    }

    [Fact]
    public async Task Nome_completo_acha()
    {
        using var ctx = Base();

        var achados = await Achar(ctx, "Lucas Almeida Coelho");

        Assert.Single(achados);
        Assert.Equal(1, achados[0].Id);
    }

    [Fact]
    public async Task Apelido_acha()
    {
        using var ctx = Base();

        // "Foka" é apelido de um e login do mesmo — o exato ganha e não vira lista de escolha.
        Assert.Single(await Achar(ctx, "Foka"));
    }

    [Fact]
    public async Task Nome_parcial_com_mais_de_um_devolve_TODOS_pra_escolher()
    {
        // É o caso que a tela precisa tratar: dois Lucas. Escolher sozinho aqui mandaria o
        // teste pro Lucas errado em silêncio.
        using var ctx = Base();

        var achados = await Achar(ctx, "Lucas");

        Assert.Equal(2, achados.Count);
    }

    [Fact]
    public async Task Cpf_inteiro_acha_uma_pessoa_so()
    {
        using var ctx = Base();

        Assert.Single(await Achar(ctx, "22222222222"));
        Assert.Equal(2, (await Achar(ctx, "222.222.222-22"))[0].Id);
    }

    [Fact]
    public async Task Cpf_pela_metade_nao_lista_meio_banco()
    {
        // Busca parcial por CPF deixaria varrer os documentos da base aos poucos.
        using var ctx = Base();

        Assert.Empty(await Achar(ctx, "2222"));
    }

    [Fact]
    public async Task Quem_nao_existe_devolve_vazio()
    {
        using var ctx = Base();

        Assert.Empty(await Achar(ctx, "ninguem.aqui"));
        Assert.Empty(await Achar(ctx, ""));
    }

    [Fact]
    public void A_linha_do_candidato_desempata_sem_escrever_o_CPF_inteiro()
    {
        // O admin precisa distinguir dois homônimos, não ver o documento de ninguém numa tela
        // que pode estar sendo mostrada pra outra pessoa.
        var detalhe = LinhaDoCandidato.Detalhe(new Jogador
        {
            Nome = "Lucas Pereira", Login = "lucasp", Email = "lucasp@exemplo.com", Cpf = "22233344455",
        });

        Assert.Contains("lucasp", detalhe);
        Assert.Contains("333", detalhe);              // o miolo, que basta pra desempatar
        Assert.DoesNotContain("22233344455", detalhe); // nunca o número inteiro
    }

    [Fact]
    public void Candidato_sem_login_nem_email_ainda_diz_alguma_coisa()
    {
        // Pré-cadastro feito por parceiro não tem login: linha em branco pareceria bug.
        var detalhe = LinhaDoCandidato.Detalhe(new Jogador { Nome = "Sem Conta", Cpf = "" });

        Assert.False(string.IsNullOrWhiteSpace(detalhe));
    }

    [Fact]
    public void O_alcance_avisa_ANTES_de_mandar_quando_o_WhatsApp_nao_tem_como_chegar()
    {
        // Evita o teste que só serve pra descobrir que não tinha como chegar: o motivo já
        // aparece do lado do nome, com o botão de enviar ainda parado.
        var semNumero = LinhaDoCandidato.Alcance(new Jogador { Nome = "X", NotificarWhatsApp = true, Celular = null });
        var desmarcou = LinhaDoCandidato.Alcance(new Jogador { Nome = "X", NotificarWhatsApp = false, Celular = "51999998888" });
        var vaiChegar = LinhaDoCandidato.Alcance(new Jogador { Nome = "X", NotificarWhatsApp = true, Celular = "51999998888" });

        Assert.Contains("sem celular", semNumero);
        Assert.Contains("desmarcou", desmarcou);
        Assert.Contains("(51) 99999-8888", vaiChegar);
    }

    [Fact]
    public void O_aviso_verde_so_aparece_quando_algum_canal_entregou()
    {
        // "Mandei" sem dizer se chegou é a informação que menos serve numa tela de teste.
        var nenhum = new TesteDeNotificacaoVM
        {
            Resultado = new ResultadoTesteNotificacao
            {
                Push = ResultadoDoCanal.SemAppInstalado,
                WhatsApp = ResultadoDoCanal.FalhouNoEnvio,
            },
        };
        var umSo = new TesteDeNotificacaoVM
        {
            Resultado = new ResultadoTesteNotificacao
            {
                Push = ResultadoDoCanal.SemAppInstalado,
                WhatsApp = ResultadoDoCanal.Enviado,
            },
        };

        Assert.False(nenhum.AlgumCanalDeuCerto);
        Assert.True(umSo.AlgumCanalDeuCerto);
        Assert.False(new TesteDeNotificacaoVM().AlgumCanalDeuCerto); // antes de mandar
    }
}
