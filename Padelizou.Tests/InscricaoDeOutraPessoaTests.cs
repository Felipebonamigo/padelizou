using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUEM FOI POSTO NUMA INSCRIÇÃO POR OUTRA PESSOA — a regra pura, sem banco.
//
// 🗣️ Felipe, 16/09/2026: *"para quando alguem inscrever um parceiro no torneio, avisar o
// parceiro e permitir recusar, ao recusar o primeiro fica sozinho no torneio e o avisa"*.
//
// ⚠️ A PERGUNTA É "QUEM NÃO CLICOU", e não "quem é o jogador 2". O formulário de inscrição
// deixa escolher os DOIS lados pelo nome (Felipe, 14/08/2026), então o autor pode ser o
// jogador 1, o jogador 2, ou nenhum dos dois — é o caso do organizador que inscreve uma dupla
// inteira na secretaria do clube. Amarrar a pergunta ao slot deixaria justamente esse caso
// calado, que é o único em que NINGUÉM dos dois pediu pra entrar.
public class InscricaoDeOutraPessoaTests
{
    [Fact]
    public void O_parceiro_de_quem_inscreveu_e_quem_responde()
    {
        // O caso comum: eu me inscrevo e levo alguém junto.
        Assert.Equal(new[] { 8 }, InscricaoDeOutraPessoa.QuemPrecisaResponder(7, 8, autorId: 7));
    }

    [Fact]
    public void Quem_se_poe_como_parceiro_tambem_inscreveu_o_outro()
    {
        // O autor no slot 2 não é detalhe de tela: o formulário escolhe os dois lados pelo
        // nome, e quem preencheu o jogador 1 com OUTRA pessoa inscreveu essa pessoa.
        Assert.Equal(new[] { 7 }, InscricaoDeOutraPessoa.QuemPrecisaResponder(7, 8, autorId: 8));
    }

    [Fact]
    public void Organizador_que_inscreve_a_dupla_inteira_pergunta_aos_DOIS()
    {
        Assert.Equal(new[] { 7, 8 }, InscricaoDeOutraPessoa.QuemPrecisaResponder(7, 8, autorId: 99));
    }

    [Fact]
    public void Quem_se_inscreve_sozinho_nao_recebe_pergunta_nenhuma()
    {
        Assert.Empty(InscricaoDeOutraPessoa.QuemPrecisaResponder(7, null, autorId: 7));
    }

    [Fact]
    public void Inscricao_sem_parceiro_feita_por_outro_ainda_pergunta()
    {
        // "Ainda não tenho parceiro" inscrito por terceiro é exatamente o caso de alguém que
        // pode nem saber que está no torneio.
        Assert.Equal(new[] { 7 }, InscricaoDeOutraPessoa.QuemPrecisaResponder(7, null, autorId: 99));
    }

    [Fact]
    public void Sem_autor_nao_ha_a_quem_responder()
    {
        // Inscrição que nasceu sem ninguém logado (importação, cobrança antiga sem o autor
        // gravado). Sem saber QUEM inscreveu, a faixa diria "você foi inscrito por" e pararia
        // ali — e a recusa não teria a quem avisar. Nada é perguntado, e nada muda pra ela.
        Assert.Empty(InscricaoDeOutraPessoa.QuemPrecisaResponder(7, 8, autorId: null));
    }

    // ── QUEM PODE RECUSAR ────────────────────────────────────────────────────────────────

    private static Torneio TorneioAberto(string status = "Inscrições Abertas") =>
        new() { Id = 1, Nome = "Copa Teste", Codigo = "T1", Status = status };

    private static Dupla Dupla(int jogador1, int? jogador2) =>
        new() { Id = 10, CategoriaId = 5, Jogador1Id = jogador1, Jogador2Id = jogador2, Codigo = "D1" };

    private static InscritoPorOutro Pergunta(int jogadorId, int autorId = 7) =>
        new() { DuplaId = 10, JogadorId = jogadorId, InscritoPorId = autorId };

    [Fact]
    public void Quem_foi_posto_na_inscricao_recusa()
    {
        Assert.Null(InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(), Pergunta(8), quemPede: 8));
    }

    [Fact]
    public void Quem_fez_a_inscricao_nao_recusa_a_propria_inscricao()
    {
        // Sem linha, não há o que recusar: quem clicou tem a porta de sempre (desistir), que
        // pergunta se sai só ele ou os dois. Duas portas pro mesmo ato, com perguntas
        // diferentes, é como as duas telas divergem.
        var motivo = InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(), pergunta: null, quemPede: 7);

        Assert.NotNull(motivo);
        Assert.Contains("sair da dupla", motivo);
    }

    [Fact]
    public void A_pergunta_de_um_nao_serve_pro_outro()
    {
        // A linha é do jogador 8; o 7 não pode usá-la pra tirar o 8 do torneio.
        var motivo = InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(), Pergunta(8), quemPede: 7);

        Assert.NotNull(motivo);
    }

    [Fact]
    public void Estranho_nao_recusa_inscricao_alheia()
    {
        var motivo = InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(), Pergunta(8), quemPede: 99);

        Assert.Contains("não é sua", motivo);
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Em Andamento")]
    [InlineData("Finalizado")]
    public void Depois_do_sorteio_a_saida_e_com_o_organizador(string status)
    {
        // Mesma trava da desistência, e de propósito a MESMA função: a dupla já está numa
        // chave, com adversários contando com ela. Duas réguas escritas separadas é como uma
        // delas acaba deixando sair depois do sorteio.
        var motivo = InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(status), Pergunta(8), quemPede: 8);

        Assert.Contains("organizador", motivo);
    }

    [Fact]
    public void Ja_ter_confirmado_nao_prende_ninguem_no_torneio()
    {
        // Quem clicou "está certo" e mudou de ideia continua podendo sair. O "está certo" só
        // tira a faixa da tela — não é um contrato.
        var pergunta = Pergunta(8);
        pergunta.ConfirmadoEm = new DateTime(2026, 9, 16, 10, 0, 0);

        Assert.Null(InscricaoDeOutraPessoa.MotivoParaNaoRecusar(
            Dupla(7, 8), TorneioAberto(), pergunta, quemPede: 8));
    }
}
