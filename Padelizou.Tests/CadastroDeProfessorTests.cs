using Padelizou.Services;

namespace Padelizou.Tests;

// Em 28/07/2026, 7 professores de 7 nos três ambientes estavam sem cidade — e portanto ninguém no
// site inteiro conseguia marcar uma aula, sem nenhum erro aparecer em tela. Um aviso no painel não
// resolveu. Estes testes seguram a regra que agora obriga.
public class CadastroDeProfessorTests
{
    [Fact]
    public void Sem_cidade_a_primeira_cobranca_e_a_cidade()
    {
        // A ordem não é arbitrária: a tela de marcar aula pergunta a cidade primeiro, então
        // mandar cadastrar o local antes deixaria a pessoa fora da lista do mesmo jeito.
        Assert.Equal(PendenciaDoProfessor.Cidade, CadastroDeProfessor.Pendencia(temCidade: false, temLocal: false));
        Assert.Equal(PendenciaDoProfessor.Cidade, CadastroDeProfessor.Pendencia(temCidade: false, temLocal: true));
    }

    [Fact]
    public void Com_cidade_mas_sem_local_cobra_o_local()
    {
        Assert.Equal(PendenciaDoProfessor.Local, CadastroDeProfessor.Pendencia(temCidade: true, temLocal: false));
    }

    [Fact]
    public void Com_os_dois_nao_cobra_nada()
    {
        // Se isto quebrar, o professor fica preso num laço de redirecionamento e não abre o painel.
        Assert.Equal(PendenciaDoProfessor.Nenhuma, CadastroDeProfessor.Pendencia(temCidade: true, temLocal: true));
    }

    [Theory]
    [InlineData(PendenciaDoProfessor.Cidade, "MinhasCidades")]
    [InlineData(PendenciaDoProfessor.Local, "MeusLocais")]
    [InlineData(PendenciaDoProfessor.Nenhuma, "Dashboard")]
    public void Cada_pendencia_leva_pra_tela_que_resolve_ela(PendenciaDoProfessor pendencia, string acaoEsperada)
    {
        Assert.Equal(acaoEsperada, CadastroDeProfessor.AcaoPara(pendencia));
    }

    [Fact]
    public void Quem_esta_em_dia_nao_recebe_mensagem_de_cobranca()
    {
        Assert.Null(CadastroDeProfessor.MensagemPara(PendenciaDoProfessor.Nenhuma));
    }

    [Theory]
    [InlineData(PendenciaDoProfessor.Cidade)]
    [InlineData(PendenciaDoProfessor.Local)]
    public void A_mensagem_diz_a_consequencia_e_nao_so_a_tarefa(PendenciaDoProfessor pendencia)
    {
        var texto = CadastroDeProfessor.MensagemPara(pendencia);

        Assert.False(string.IsNullOrWhiteSpace(texto));
        // "Cadastre sua cidade" soa burocrático e a pessoa deixa pra depois. A mensagem precisa
        // falar do ALUNO — é ele que some quando falta o dado.
        Assert.Contains("aluno", texto, StringComparison.OrdinalIgnoreCase);
    }
}
