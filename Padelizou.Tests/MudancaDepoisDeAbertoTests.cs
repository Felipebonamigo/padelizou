using Padelizou.Services;

namespace Padelizou.Tests;

// O QUE PODE MUDAR COM O TORNEIO JÁ ABERTO.
//
// Felipe, 18/08/2026: "permita q os organizadores vejam todos os dados de criação, igualmente
// na edição". O motivo é bom — campo que só existe no cadastro vira beco, e isso já aconteceu
// três vezes aqui (forma de recebimento, reabrir inscrições, "só confirmo depois de pago").
//
// ⚠️ Mas "ver tudo" não é "poder tudo". Estes testes guardam os dois casos em que mexer com
// gente inscrita não dá erro nenhum — dá ESTRAGO SILENCIOSO, que é pior porque ninguém vê
// acontecer.
public class MudancaDepoisDeAbertoTests
{
    // ⚠️ O caso que motivou a trava: baixar o limite abaixo de quem já entrou NÃO apaga
    // ninguém. É pior — o torneio passa a se comportar como lotado, as inscrições continuam
    // lá, e o organizador só percebe porque param de chegar inscrições novas.
    [Fact]
    public void Limite_nao_pode_ficar_abaixo_de_quem_ja_entrou()
    {
        var problema = MudancaDepoisDeAberto.ProblemaComOLimite(novoLimite: 8, inscricoesJaFeitas: 20);

        Assert.NotNull(problema);
        Assert.Contains("20 inscrições", problema);
    }

    [Fact]
    public void Limite_igual_ao_que_ja_existe_e_permitido()
    {
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOLimite(novoLimite: 20, inscricoesJaFeitas: 20));
    }

    [Fact]
    public void Aumentar_o_limite_sempre_pode()
    {
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOLimite(novoLimite: 64, inscricoesJaFeitas: 20));
    }

    // Tirar o teto não expulsa ninguém — é sempre seguro.
    [Fact]
    public void Sem_limite_e_sempre_permitido()
    {
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOLimite(novoLimite: null, inscricoesJaFeitas: 20));
    }

    [Fact]
    public void Limite_negativo_e_recusado()
    {
        Assert.NotNull(MudancaDepoisDeAberto.ProblemaComOLimite(novoLimite: -1, inscricoesJaFeitas: 0));
    }

    // ⚠️ Chave e Americano guardam inscrição em TABELAS diferentes. Virar a chave com jogos
    // criados deixa o torneio com metade dos dados de cada formato — e isso não aparece na
    // tela até alguém abrir o chaveamento.
    [Fact]
    public void Nao_da_pra_trocar_o_formato_com_gente_inscrita()
    {
        var problema = MudancaDepoisDeAberto.ProblemaComOFormato("Padrao", "Americano", inscricoesJaFeitas: 4);

        Assert.NotNull(problema);
        Assert.Contains("órfãs", problema);
    }

    // Sem ninguém inscrito não há o que ficar órfão: trocar é inofensivo.
    [Fact]
    public void Trocar_o_formato_antes_de_qualquer_inscricao_pode()
    {
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOFormato("Padrao", "Americano", inscricoesJaFeitas: 0));
    }

    // Salvar a tela sem mexer no formato não pode ser recusado — senão o organizador de um
    // torneio cheio não consegue salvar mais NADA nessa aba.
    [Fact]
    public void Salvar_sem_mudar_o_formato_nunca_e_recusado()
    {
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOFormato("Padrao", "Padrao", inscricoesJaFeitas: 30));
        Assert.Null(MudancaDepoisDeAberto.ProblemaComOFormato("Padrao", null, inscricoesJaFeitas: 30));
    }
}
