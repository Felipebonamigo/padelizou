using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTOS FALTAM PRO JOGO DA SEMANA.
//
// A conta é trivial; o que estes testes prendem é o que a tela DIZ com ela. Dois casos
// carregam o peso:
//
// 1) LISTA CHEIA DEMAIS NÃO É FALTA. Desde a presença presumida (21/08/2026) a panelinha
//    inteira nasce na lista — uma turma de 10 com 4 vagas abre a semana com 10 "na lista".
//    Uma subtração sem piso responderia "faltam -6", e o botão de convidar apareceria em
//    destaque justamente na semana em que vai sobrar gente.
//
// 2) SEM VAGAS CONFIGURADAS, A TELA NÃO OPINA. `VagasMaximas` é saneado na escrita
//    (GruposController.Configuracoes força 4 quando vem <= 0), mas linha antiga de banco não
//    passa por lá — e "faltam 4" inventado num grupo que nunca configurou nada é pior que
//    silêncio, porque manda a pessoa chamar gente pra uma quadra que talvez já esteja cheia.
public class VagasDaSessaoTests
{
    [Fact]
    public void Falta_um_fala_no_singular()
    {
        Assert.Equal(1, VagasDaSessao.Faltam(naLista: 3, vagas: 4));
        Assert.Equal("falta 1 pra fechar", VagasDaSessao.Frase(naLista: 3, vagas: 4));
    }

    [Fact]
    public void Faltam_dois_falam_no_plural()
    {
        Assert.Equal(2, VagasDaSessao.Faltam(naLista: 2, vagas: 4));
        Assert.Equal("faltam 2 pra fechar", VagasDaSessao.Frase(naLista: 2, vagas: 4));
    }

    [Fact]
    public void Ninguem_na_lista_falta_o_quarteto_inteiro()
    {
        Assert.Equal(4, VagasDaSessao.Faltam(naLista: 0, vagas: 4));
        Assert.True(VagasDaSessao.FaltaGente(naLista: 0, vagas: 4));
    }

    [Fact]
    public void Lista_completa_nao_diz_que_falta()
    {
        Assert.Equal(0, VagasDaSessao.Faltam(naLista: 4, vagas: 4));
        Assert.False(VagasDaSessao.FaltaGente(naLista: 4, vagas: 4));
        Assert.Equal("lista completa", VagasDaSessao.Frase(naLista: 4, vagas: 4));
    }

    // ⚠️ O CASO DA PRESENÇA PRESUMIDA: a panelinha inteira entra na lista sozinha, então
    // "mais gente que vaga" é o estado NORMAL de uma turma grande no começo da semana.
    [Fact]
    public void Lista_com_gente_a_mais_avisa_quantos_sobram_e_nao_pede_convidado()
    {
        Assert.Equal(0, VagasDaSessao.Faltam(naLista: 10, vagas: 4));
        Assert.False(VagasDaSessao.FaltaGente(naLista: 10, vagas: 4));
        Assert.Equal(6, VagasDaSessao.Sobram(naLista: 10, vagas: 4));
        Assert.Equal("6 a mais que as vagas", VagasDaSessao.Frase(naLista: 10, vagas: 4));
    }

    [Fact]
    public void Uma_pessoa_a_mais_fala_no_singular()
    {
        Assert.Equal("1 a mais que as vagas", VagasDaSessao.Frase(naLista: 5, vagas: 4));
    }

    // Sem vagas configuradas a tela cala a boca — nem falta, nem sobra, nem frase.
    [Fact]
    public void Sem_vagas_configuradas_nao_opina()
    {
        Assert.Null(VagasDaSessao.Frase(naLista: 3, vagas: 0));
        Assert.Equal(0, VagasDaSessao.Faltam(naLista: 3, vagas: 0));
        Assert.Equal(0, VagasDaSessao.Sobram(naLista: 3, vagas: 0));
        Assert.False(VagasDaSessao.FaltaGente(naLista: 3, vagas: 0));
    }

    [Fact]
    public void Vagas_negativas_nao_viram_frase_negativa()
    {
        Assert.Null(VagasDaSessao.Frase(naLista: 3, vagas: -4));
        Assert.Equal(0, VagasDaSessao.Faltam(naLista: 3, vagas: -4));
    }
}
