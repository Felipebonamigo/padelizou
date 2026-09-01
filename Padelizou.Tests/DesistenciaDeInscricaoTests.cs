using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Desistir era mandar mensagem pro organizador, que mandava mensagem pro suporte. A regra que
// mais importa aqui: quem desiste sai, o PARCEIRO não é arrastado junto — ele estava inscrito
// também, muitas vezes já pagou, e perder a vaga porque o outro desistiu puniria o errado.
public class DesistenciaDeInscricaoTests
{
    private static Torneio TorneioAberto(string status = "Inscrições Abertas") =>
        new() { Id = 1, Nome = "Copa Teste", Codigo = "T1", Status = status };

    private static Dupla Dupla(int jogador1, int? jogador2) =>
        new() { Id = 10, CategoriaId = 5, Jogador1Id = jogador1, Jogador2Id = jogador2, Codigo = "D1" };

    [Fact]
    public void Quem_esta_na_dupla_pode_desistir()
    {
        var dupla = Dupla(7, 8);

        Assert.Null(DesistenciaDeInscricao.MotivoParaNaoDesistir(dupla, TorneioAberto(), 7));
        Assert.Null(DesistenciaDeInscricao.MotivoParaNaoDesistir(dupla, TorneioAberto(), 8));
    }

    [Fact]
    public void Quem_nao_esta_na_dupla_nao_tira_ninguem()
    {
        // Sem isto, o botão de desistir viraria botão de tirar qualquer um do torneio.
        var motivo = DesistenciaDeInscricao.MotivoParaNaoDesistir(Dupla(7, 8), TorneioAberto(), 99);

        Assert.Contains("não é sua", motivo);
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Em Andamento")]
    [InlineData("Finalizado")]
    public void Depois_do_encerramento_e_assunto_do_organizador(string status)
    {
        // A dupla já está numa chave, com jogos marcados e adversários contando com ela.
        var motivo = DesistenciaDeInscricao.MotivoParaNaoDesistir(Dupla(7, 8), TorneioAberto(status), 7);

        Assert.Contains("organizador", motivo);
    }

    [Fact]
    public void Inscricao_que_nao_existe_nao_estoura()
    {
        Assert.NotNull(DesistenciaDeInscricao.MotivoParaNaoDesistir(null, TorneioAberto(), 7));
        Assert.NotNull(DesistenciaDeInscricao.MotivoParaNaoDesistir(Dupla(7, 8), null, 7));
    }

    [Fact]
    public void Dupla_completa_perde_so_um_e_a_vaga_nao_abre()
    {
        var dupla = Dupla(7, 8);

        Assert.Equal(EfeitoDaDesistencia.SoSaiQuemDesistiu,
            DesistenciaDeInscricao.Efeito(dupla, EscolhaDeQuemSai.SoEu));

        // O que fica é sempre o OUTRO, não importa quem desistiu.
        Assert.Equal(8, DesistenciaDeInscricao.QuemFica(dupla, 7));
        Assert.Equal(7, DesistenciaDeInscricao.QuemFica(dupla, 8));
    }

    [Fact]
    public void Quem_estava_sozinho_leva_a_inscricao_embora()
    {
        // Sem ninguém pra segurar a vaga, ela volta pro torneio e a lista de espera anda.
        var dupla = Dupla(7, null);

        Assert.Equal(EfeitoDaDesistencia.AInscricaoAcaba,
            DesistenciaDeInscricao.Efeito(dupla, EscolhaDeQuemSai.SoEu));
        Assert.Null(DesistenciaDeInscricao.QuemFica(dupla, 7));
    }

    // ── A ESCOLHA DE QUEM SAI ─────────────────────────────────────────────────────────────
    // Antes o sistema decidia sozinho: na dupla completa, saía só quem clicou. Mas a dupla que
    // se inscreveu junto e desistiu junto precisava clicar duas vezes, em duas contas — e a
    // segunda metade quase nunca clicava, deixando meia inscrição ocupando a vaga até o
    // encerramento.

    [Fact]
    public void Na_dupla_completa_sair_so_eu_deixa_o_parceiro_inscrito()
    {
        var dupla = Dupla(7, 8);

        Assert.Equal(EfeitoDaDesistencia.SoSaiQuemDesistiu,
            DesistenciaDeInscricao.Efeito(dupla, EscolhaDeQuemSai.SoEu));
    }

    [Fact]
    public void Na_dupla_completa_cancelar_a_inscricao_inteira_abre_a_vaga()
    {
        // O que muda de verdade é a vaga: ela volta pra fila, e a lista de espera anda.
        var dupla = Dupla(7, 8);

        Assert.Equal(EfeitoDaDesistencia.AInscricaoAcaba,
            DesistenciaDeInscricao.Efeito(dupla, EscolhaDeQuemSai.ADuplaInteira));
    }

    [Theory]
    [InlineData(EscolhaDeQuemSai.SoEu)]
    [InlineData(EscolhaDeQuemSai.ADuplaInteira)]
    public void Quem_esta_sozinho_ignora_a_escolha(EscolhaDeQuemSai escolha)
    {
        // Não há parceiro pra ficar: as duas escolhas dão no mesmo, e perguntar na tela seria
        // fazer uma pergunta que não tem duas respostas.
        var dupla = Dupla(7, null);

        Assert.Equal(EfeitoDaDesistencia.AInscricaoAcaba,
            DesistenciaDeInscricao.Efeito(dupla, escolha));
    }

    [Fact]
    public void So_a_dupla_completa_tem_o_que_perguntar()
    {
        Assert.True(DesistenciaDeInscricao.TemEscolha(Dupla(7, 8)));
        Assert.False(DesistenciaDeInscricao.TemEscolha(Dupla(7, null)));
    }

    // ── AMERICANO ────────────────────────────────────────────────────────────────────────
    // O botão de desistir nasceu só pro laço de duplas. Inscrição de Americano vive em outra
    // tabela (InscricaoAmericana, um jogador por linha), então num torneio Americano o jogador
    // não tinha como sair sozinho — voltava a ser mensagem pro organizador, que é exatamente o
    // trabalho manual que este botão existe pra matar.

    private static InscricaoAmericana Americana(int jogador) =>
        new() { Id = 20, CategoriaId = 5, JogadorId = jogador };

    [Fact]
    public void No_americano_quem_se_inscreveu_pode_desistir()
    {
        Assert.Null(DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(
            Americana(7), TorneioAberto(), 7));
    }

    [Fact]
    public void No_americano_ninguem_tira_a_inscricao_do_outro()
    {
        var motivo = DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(
            Americana(7), TorneioAberto(), 99);

        Assert.Contains("não é sua", motivo);
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Em Andamento")]
    [InlineData("Finalizado")]
    public void No_americano_depois_do_encerramento_e_assunto_do_organizador(string status)
    {
        var motivo = DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(
            Americana(7), TorneioAberto(status), 7);

        Assert.Contains("organizador", motivo);
    }

    [Fact]
    public void No_americano_inscricao_que_nao_existe_nao_estoura()
    {
        Assert.NotNull(DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(
            null, TorneioAberto(), 7));
        Assert.NotNull(DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(
            Americana(7), null, 7));
    }
}
