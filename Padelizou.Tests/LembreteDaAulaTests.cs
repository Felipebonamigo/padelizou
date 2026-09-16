using Padelizou.Services;

namespace Padelizou.Tests;

// "SUA AULA É AMANHÃ" e "SUA AULA É DAQUI A POUCO" — a régua sozinha.
//
// 🗣️ Maickel, 16/09/2026: *"Só talvez faria um 'push' — avisando 24hs e 1hora antes da aula —
// para as opções de aula de padel"*. Era a única parte do sistema com hora marcada que não
// avisava ninguém antes.
//
// A LIGAÇÃO (varrer o banco, achar quem avisar, gravar o marco) está em
// VarreduraDoLembreteDeAulaTests. Aqui só a conta: qual marco cabe agora e o que o texto diz.
public class LembreteDaAulaTests
{
    // Sábado, 19/09/2026, aula às 19h — e as horas antes dela.
    private static readonly DateTime Aula = new(2026, 9, 19, 19, 0, 0);

    // ── Qual marco cabe agora ────────────────────────────────────────────────────────────────

    [Fact]
    public void Longe_da_aula_nao_se_avisa_ninguem()
    {
        // Três dias antes: a pessoa marcou e a vida segue. Lembrete aqui é lembrete de nada.
        Assert.Null(LembreteDaAula.MarcoDevido(Aula, Aula.AddDays(-3), null));
    }

    [Fact]
    public void A_24_horas_sai_o_primeiro()
    {
        Assert.Equal(24, LembreteDaAula.MarcoDevido(Aula, Aula.AddHours(-24), null));
    }

    [Fact]
    public void Marco_ja_enviado_nao_se_repete()
    {
        // ⚠️ É o que faz a varredura poder rodar de 15 em 15 minutos — e o que faz um deploy no
        // meio da tarde não reenviar tudo de novo.
        Assert.Null(LembreteDaAula.MarcoDevido(Aula, Aula.AddHours(-20), 24));
    }

    [Fact]
    public void A_uma_hora_sai_o_segundo()
    {
        Assert.Equal(1, LembreteDaAula.MarcoDevido(Aula, Aula.AddMinutes(-50), 24));
    }

    [Fact]
    public void Aula_marcada_em_cima_da_hora_leva_UM_aviso_so_e_e_o_mais_urgente()
    {
        // Os dois marcos vencem juntos (faltam 40 minutos e ninguém avisou nada ainda). Vale o
        // mais urgente e o outro é dado por cumprido — senão a pessoa levaria dois avisos em
        // quinze minutos, um por tick. Mesma regra do lembrete de inscrição não paga.
        Assert.Equal(1, LembreteDaAula.MarcoDevido(Aula, Aula.AddMinutes(-40), null));
    }

    [Fact]
    public void Depois_do_marco_de_1h_nao_sobra_nada_pra_mandar()
    {
        Assert.Null(LembreteDaAula.MarcoDevido(Aula, Aula.AddMinutes(-40), 1));
    }

    [Fact]
    public void Aula_que_ja_comecou_nao_se_lembra()
    {
        // Avisar que a aula é "daqui a pouco" com o aluno já na quadra é pior que calar.
        Assert.Null(LembreteDaAula.MarcoDevido(Aula, Aula.AddMinutes(1), null));
    }

    // ── Madrugada ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_marco_de_24h_nao_acorda_ninguem_as_3_da_manha()
    {
        // Aula de sábado às 22h tem marco de 24h na sexta às 22h… e aula de domingo às 3h da
        // manhã não existe, mas o marco pode cair de madrugada quando a aula é cedo no dia
        // seguinte. Segurar até as 7h ainda entrega o aviso com mais de 15 horas de sobra.
        var aulaDeMadrugada = new DateTime(2026, 9, 19, 3, 0, 0);

        Assert.Null(LembreteDaAula.MarcoDevido(aulaDeMadrugada, aulaDeMadrugada.AddHours(-24), null));
    }

    [Fact]
    public void Segurado_de_madrugada_o_marco_de_24h_sai_as_7h()
    {
        var aulaDeMadrugada = new DateTime(2026, 9, 19, 3, 0, 0);

        Assert.Equal(24, LembreteDaAula.MarcoDevido(aulaDeMadrugada, new DateTime(2026, 9, 18, 7, 0, 0), null));
    }

    [Fact]
    public void O_marco_de_1h_NAO_tem_janela_de_horario()
    {
        // Quem tem aula às 6h já vai acordar às 5h de qualquer jeito. Segurar este aviso até as
        // 7h é a mesma coisa que não mandá-lo.
        var aulaCedo = new DateTime(2026, 9, 19, 6, 0, 0);

        Assert.Equal(1, LembreteDaAula.MarcoDevido(aulaCedo, aulaCedo.AddMinutes(-55), 24));
    }

    // ── O que o texto diz ────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_texto_conta_o_tempo_DE_VERDADE_e_nao_o_numero_do_marco()
    {
        // ⚠️ Quem entra pelo marco de 24h com a aula em 15 horas (porque o marco caiu de
        // madrugada e foi segurado até as 7h) não pode ouvir "amanhã": a aula é HOJE. A frase
        // sai da diferença entre as datas, nunca do marco que a trouxe até aqui.
        var aulaHojeANoite = new DateTime(2026, 9, 19, 22, 0, 0);
        var manhaDoMesmoDia = new DateTime(2026, 9, 19, 7, 0, 0);

        Assert.Contains("hoje às 22:00", LembreteDaAula.Quando(aulaHojeANoite, manhaDoMesmoDia));
        Assert.Contains("hoje", LembreteDaAula.Titulo(LembreteDaAula.UmaAula, aulaHojeANoite, manhaDoMesmoDia));
    }

    [Fact]
    public void A_vespera_diz_amanha()
    {
        Assert.Equal("amanhã às 19:00", LembreteDaAula.Quando(Aula, Aula.AddHours(-24)));
        Assert.Equal("Sua aula é amanhã", LembreteDaAula.Titulo(LembreteDaAula.UmaAula, Aula, Aula.AddHours(-24)));
    }

    [Fact]
    public void Em_cima_da_hora_o_titulo_diz_daqui_a_pouco()
    {
        Assert.Equal("Sua aula é daqui a pouco", LembreteDaAula.Titulo(LembreteDaAula.UmaAula, Aula, Aula.AddMinutes(-50)));
        Assert.Equal("Seu jogo-aula é daqui a pouco", LembreteDaAula.Titulo(LembreteDaAula.UmJogoAula, Aula, Aula.AddMinutes(-50)));
    }

    [Fact]
    public void Aula_de_depois_de_amanha_sai_com_a_data()
    {
        // Não acontece pelos marcos de hoje (24h é sempre a véspera), mas a frase é usada por
        // quem chamar — e "em 21/09" é o único jeito honesto de dizer isso sem contar dias.
        Assert.Equal("21/09 às 19:00", LembreteDaAula.Quando(Aula.AddDays(2), Aula.AddHours(-24)));
    }

    [Fact]
    public void So_o_aviso_de_24h_do_aluno_oferece_desmarcar()
    {
        // ⚠️ É o que faz o de 24h valer a pena: a política da maioria dos professores é
        // justamente 24h de antecedência, então este aviso chega no último instante em que
        // desmarcar ainda é de graça. No de 1h não há o que decidir — oferecer ali só faz o
        // aluno achar que dá tempo.
        var vespera = LembreteDaAula.Frase("Marcio", Aula, Aula.AddHours(-24), "Wallau", ofereceDesmarcar: true);
        var emCimaDaHora = LembreteDaAula.Frase("Marcio", Aula, Aula.AddMinutes(-50), "Wallau", ofereceDesmarcar: false);

        Assert.Equal("Com Marcio, amanhã às 19:00, em Wallau. Se não puder ir, desmarque pelo app.", vespera);
        Assert.Equal("Com Marcio, hoje às 19:00, em Wallau.", emCimaDaHora);
    }

    [Fact]
    public void Os_marcos_sao_24h_e_1h_e_estao_do_mais_distante_pro_mais_perto()
    {
        // O pedido do Maickel, escrito num lugar só. A ordem importa: a varredura lê o teto
        // (Marcos.Max()) pra limitar a consulta no banco.
        Assert.Equal(new[] { 24, 1 }, LembreteDaAula.Marcos);
    }
}
