using Padelizou.Services;

namespace Padelizou.Tests;

// JUNTAR O QUE OS DOIS CAMPOS DA TELA MANDAM.
//
// A tela de Adicionar Aula deixou de usar `<input type="datetime-local">` (25/08/2026): no
// Android ele abre a RODINHA de rolagem, e o pedido do Felipe foi o calendário pra data e o
// relógio pro horário — que é o que `type="date"` e `type="time"` abrem, separados.
//
// ⚠️ O PARSING É INVARIANTE, E ISSO É A REGRA INTEIRA DESTE ARQUIVO. O app roda com cultura
// pt-BR (Program.cs), e `<input type="date">` manda SEMPRE "yyyy-MM-dd". Lido em pt-BR,
// "2026-08-18" não é 18 de agosto — é lixo, ou pior, outra data. O mesmo comentário já existe
// no `DatasDaAulaFixa.Ler`, que caiu nessa armadilha antes.
public class DataEHoraDoFormularioTests
{
    [Fact]
    public void Junta_a_data_do_calendario_com_a_hora_do_relogio()
    {
        var quando = DataEHoraDoFormulario.Juntar("2026-08-18", "14:00");

        Assert.Equal(new DateTime(2026, 8, 18, 14, 0, 0), quando);
    }

    // ⚠️ O TESTE QUE JUSTIFICA O ARQUIVO: em pt-BR "18/08/2026" é uma data VÁLIDA, e um
    // `DateTime.TryParse` sem cultura invariante aceitaria os dois formatos — deixando passar
    // um valor que o `<input type="date">` nunca manda, vindo de formulário montado à mão.
    // Recusar é o certo: o campo tem UM formato, e ele é o do HTML.
    [Theory]
    [InlineData("18/08/2026")]
    [InlineData("08/18/2026")]
    [InlineData("18-08-2026")]
    public void Data_fora_do_formato_do_HTML_e_recusada(string data)
    {
        Assert.Null(DataEHoraDoFormulario.Juntar(data, "14:00"));
    }

    // `<input type="time">` manda "HH:mm"; com `step` em segundos manda "HH:mm:ss". Os dois
    // são o mesmo campo, e o segundo não pode ser recusado por ser mais preciso.
    [Fact]
    public void Hora_com_segundos_continua_valendo()
    {
        Assert.Equal(new DateTime(2026, 8, 18, 14, 0, 30),
            DataEHoraDoFormulario.Juntar("2026-08-18", "14:00:30"));
    }

    // ⚠️ MEIA-NOITE É HORA, NÃO É VAZIO. Uma implementação que testasse `TimeSpan.Zero` como
    // "não preenchido" recusaria a aula das 00:00 — rara, mas real, e o erro seria mudo.
    [Fact]
    public void Meia_noite_e_um_horario_de_verdade()
    {
        Assert.Equal(new DateTime(2026, 8, 18, 0, 0, 0),
            DataEHoraDoFormulario.Juntar("2026-08-18", "00:00"));
    }

    [Theory]
    [InlineData(null, "14:00")]
    [InlineData("2026-08-18", null)]
    [InlineData("", "14:00")]
    [InlineData("2026-08-18", "")]
    [InlineData("   ", "   ")]
    [InlineData(null, null)]
    public void Faltando_um_dos_dois_nao_ha_data(string? data, string? hora)
    {
        Assert.Null(DataEHoraDoFormulario.Juntar(data, hora));
    }

    // Data que não existe no calendário não pode virar outra data por arredondamento.
    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("qualquer coisa")]
    public void Data_impossivel_nao_vira_data(string data)
    {
        Assert.Null(DataEHoraDoFormulario.Juntar(data, "10:00"));
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("14:60")]
    [InlineData("duas da tarde")]
    public void Hora_impossivel_nao_vira_hora(string hora)
    {
        Assert.Null(DataEHoraDoFormulario.Juntar("2026-08-18", hora));
    }

    // O que a tela precisa pra PREENCHER os dois campos de volta (sugestão do GET, volta de
    // validação): os mesmos formatos que o HTML manda, nunca os de pt-BR.
    [Fact]
    public void Devolve_os_dois_campos_no_formato_que_o_HTML_espera()
    {
        var quando = new DateTime(2026, 8, 18, 9, 5, 0);

        Assert.Equal("2026-08-18", DataEHoraDoFormulario.ParaCampoDeData(quando));
        Assert.Equal("09:05", DataEHoraDoFormulario.ParaCampoDeHora(quando));
    }

    // Ida e volta: o que a tela escreve nos campos, o servidor lê de volta igual.
    [Fact]
    public void O_que_a_tela_escreve_o_servidor_le_de_volta()
    {
        var original = new DateTime(2026, 12, 31, 23, 45, 0);

        var lido = DataEHoraDoFormulario.Juntar(
            DataEHoraDoFormulario.ParaCampoDeData(original),
            DataEHoraDoFormulario.ParaCampoDeHora(original));

        Assert.Equal(original, lido);
    }
}
