using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O casamento entre o extrato do banco e a fila do Pix direto (Services/ConciliacaoAutomaticaDoPix).
//
// O que está em jogo: confirmar sozinho tem que exigir prova forte (o TxId que a NOSSA
// cobrança gerou, mais o valor batendo); casar só por CPF é prova fraca e não pode confirmar
// nada — só sugerir, deixando o clique final pro admin.
public class ConciliacaoAutomaticaDoPixTests
{
    private static Jogador Professor(int id, string cpf) =>
        new() { Id = id, Nome = "Professor " + id, Login = "prof" + id, Cpf = cpf, IsProfessor = true };

    private static Pagamento Pendente(int id, decimal valor, string status = "Pendente") => new()
    {
        Id = id,
        Tipo = PixDireto.TipoAssinatura,
        JogadorId = 1,
        Valor = valor,
        MetodoPagamento = PixDireto.Metodo,
        Status = status,
    };

    [Fact]
    public void Pix_com_o_TxId_da_cobranca_e_mesmo_valor_confirma_sozinho()
    {
        var pendente = Pendente(id: 42, valor: 49.90m);
        var credito = new PixRecebidoDoBanco("E2E1", 49.90m, DateTime.Now, "11122233344", "Alguém",
            TxId: PixDireto.TxId(42));

        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([credito], [pendente], []);

        Assert.Single(resultado.ParaConfirmar);
        Assert.Equal(pendente, resultado.ParaConfirmar[0].Pendente);
        Assert.Empty(resultado.ParaSugerir);
    }

    [Fact]
    public void TxId_batendo_mas_valor_diferente_NAO_confirma()
    {
        // Cobrança pedia 49,90; caiu um Pix de 30 com o mesmo identificador — não é o mesmo
        // pagamento, é ruído ou erro de digitação de outra pessoa. Confirmar aqui creditaria
        // errado.
        var pendente = Pendente(id: 42, valor: 49.90m);
        var credito = new PixRecebidoDoBanco("E2E1", 30m, DateTime.Now, null, null, TxId: PixDireto.TxId(42));

        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([credito], [pendente], []);

        Assert.Empty(resultado.ParaConfirmar);
        Assert.Empty(resultado.ParaSugerir);
    }

    [Fact]
    public void Pix_sem_TxId_mas_com_CPF_de_professor_cadastrado_vira_sugestao_nao_confirmacao()
    {
        // O caso do Pix combinado por fora (WhatsApp): não existe cobrança nenhuma pra casar,
        // só o CPF de quem pagou. Isso é indício, não prova — por isso vira sugestão.
        var professor = Professor(7, "11122233344");
        var credito = new PixRecebidoDoBanco("E2E2", 49.90m, DateTime.Now, "111.222.333-44", "Jonatas", TxId: null);

        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([credito], [], [professor]);

        Assert.Empty(resultado.ParaConfirmar);
        Assert.Single(resultado.ParaSugerir);
        Assert.Equal(professor, resultado.ParaSugerir[0].Professor);
    }

    [Fact]
    public void CPF_que_nao_e_de_nenhum_professor_nao_gera_sugestao()
    {
        var professor = Professor(7, "11122233344");
        var credito = new PixRecebidoDoBanco("E2E3", 49.90m, DateTime.Now, "99988877766", "Outra pessoa", null);

        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([credito], [], [professor]);

        Assert.Empty(resultado.ParaConfirmar);
        Assert.Empty(resultado.ParaSugerir);
    }

    [Fact]
    public void Uma_cobranca_pendente_so_casa_com_UM_credito_mesmo_com_dois_de_valor_igual_no_lote()
    {
        var pendente = Pendente(id: 42, valor: 49.90m);
        var credito1 = new PixRecebidoDoBanco("E2E1", 49.90m, DateTime.Now, null, null, PixDireto.TxId(42));
        var credito2 = new PixRecebidoDoBanco("E2E2", 49.90m, DateTime.Now.AddMinutes(1), null, null, PixDireto.TxId(42));

        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([credito1, credito2], [pendente], []);

        // Só o primeiro (por horário) consome a cobrança; o segundo fica sem casar com nada
        // (nem confirma de novo, nem sugere — TxId não é CPF).
        Assert.Single(resultado.ParaConfirmar);
        Assert.Empty(resultado.ParaSugerir);
    }

    [Fact]
    public void Sem_recebidos_nenhum_resultado()
    {
        var resultado = ConciliacaoAutomaticaDoPix.Conciliar([], [Pendente(1, 49.90m)], [Professor(1, "123")]);

        Assert.Empty(resultado.ParaConfirmar);
        Assert.Empty(resultado.ParaSugerir);
    }
}
