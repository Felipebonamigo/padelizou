using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O RESULTADO DE UM JOGO DE PANELINHA, depois que o vencedor deixou de ser uma conta.
//
// Veio de uma sugestão de usuário (18/08/2026): registrar jogo sem precisar digitar placar.
// Isso parece mudança de tela e não é — até aqui `JogoSemanal` NÃO TINHA coluna de vencedor,
// e quem venceu era deduzido dos games.
//
// ⚠️ O ESTRAGO QUE ESTES TESTES IMPEDEM: com o vencedor deduzido, salvar sem placar grava
// 0 x 0, e 0 x 0 **é empate** — 2 pontos pra cada um. O ranking da panelinha iria se enchendo
// de empates que ninguém jogou, sem erro nenhum em lugar nenhum, e o sintoma apareceria
// semanas depois como "o ranking está estranho".
public class ResultadoDoJogoSemanalTests
{
    private static JogoSemanal Jogo() => new()
    {
        GrupoId = 1,
        DataJogo = new DateTime(2026, 8, 18),
        Dupla1Jogador1Id = 1,
        Dupla1Jogador2Id = 2,
        Dupla2Jogador1Id = 3,
        Dupla2Jogador2Id = 4,
        RegistradoPorId = 1,
    };

    [Fact]
    public void Da_pra_registrar_so_com_o_vencedor()
    {
        var jogo = Jogo();

        Assert.Null(ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, null, null));
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla1, null, null);

        Assert.Equal(1, jogo.VencedorLado);
        Assert.Null(jogo.GamesDupla1);
        Assert.Null(jogo.GamesDupla2);
        Assert.Null(ResultadoDoJogoSemanal.Placar(jogo));
    }

    // ⚠️ O TESTE QUE JUSTIFICA A MIGRAÇÃO INTEIRA. Jogo sem placar dá 3 pontos pra quem venceu
    // e 1 pra quem perdeu — e NÃO 2 e 2. Com o vencedor ainda sendo deduzido dos games, este
    // é exatamente o caso que gravava empate.
    [Fact]
    public void Jogo_sem_placar_pontua_como_vitoria_e_nao_como_empate()
    {
        var jogo = Jogo();
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla1, null, null);

        var pontos = PontuacaoDaPanelinha.Totais([jogo]);

        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, pontos[1]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, pontos[2]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorDerrota, pontos[3]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorDerrota, pontos[4]);
    }

    [Fact]
    public void Empate_continua_existindo_e_vale_dois_pra_cada()
    {
        var jogo = Jogo();
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Empate, 6, 6);

        var pontos = PontuacaoDaPanelinha.Totais([jogo]);

        Assert.All(new[] { 1, 2, 3, 4 }, id => Assert.Equal(PontuacaoDaPanelinha.PontosPorEmpate, pontos[id]));
    }

    [Fact]
    public void Placar_junto_com_o_vencedor_e_guardado()
    {
        var jogo = Jogo();
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla2, 3, 6);

        Assert.Equal("3 x 6", ResultadoDoJogoSemanal.Placar(jogo));
        Assert.Equal(2, jogo.VencedorLado);
    }

    // ⚠️ Contradição é RECUSADA, não "corrigida". Os dois vieram da mesma tela e da mesma
    // pessoa: um dos dois cliques foi errado e o sistema não sabe qual. Escolher sozinho grava
    // um resultado que ninguém pediu — foi assim que uma final de torneio saiu com o campeão
    // errado neste projeto (ver Services/QuemVenceu).
    [Fact]
    public void Placar_que_contradiz_o_vencedor_e_recusado()
    {
        var motivo = ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, 3, 6);

        Assert.NotNull(motivo);
        Assert.Contains("OUTRA dupla", motivo);
    }

    [Fact]
    public void Placar_empatado_com_vencedor_marcado_e_recusado()
    {
        var motivo = ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, 6, 6);

        Assert.NotNull(motivo);
        Assert.Contains("empatado", motivo);
    }

    // Placar é opcional INTEIRO — meio placar não diz nada.
    [Fact]
    public void Meio_placar_e_recusado()
    {
        Assert.NotNull(ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, 6, null));
        Assert.NotNull(ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, null, 6));
    }

    [Fact]
    public void Sem_marcar_vencedor_nao_salva()
    {
        var motivo = ResultadoDoJogoSemanal.MotivoParaNaoSalvar(vencedorLado: 7, null, null);

        Assert.NotNull(motivo);
        Assert.Contains("Marque quem venceu", motivo);
    }

    [Fact]
    public void Games_negativos_sao_recusados()
    {
        Assert.NotNull(ResultadoDoJogoSemanal.MotivoParaNaoSalvar(ResultadoDoJogoSemanal.Dupla1, -1, 0));
    }

    // ⚠️ A RÉGUA DO BACKFILL DA MIGRAÇÃO. A coluna nova nasce com `defaultValue: 0`, e aqui 0
    // é EMPATE — sem o UPDATE da migração, todo jogo que já existe viraria empate e o
    // recálculo espalharia isso pro ranking gravado de todas as panelinhas.
    //
    // Este teste guarda a conta que o SQL da migração faz. Se ela mudar aqui e não lá (ou
    // vice-versa), é a mesma regra em dois lugares discordando — o defeito de sempre.
    [Theory]
    [InlineData(6, 3, ResultadoDoJogoSemanal.Dupla1)]
    [InlineData(3, 6, ResultadoDoJogoSemanal.Dupla2)]
    [InlineData(6, 6, ResultadoDoJogoSemanal.Empate)]
    [InlineData(0, 0, ResultadoDoJogoSemanal.Empate)]
    public void O_vencedor_deduzido_do_placar_e_a_regua_do_backfill(int g1, int g2, int esperado)
    {
        Assert.Equal(esperado, ResultadoDoJogoSemanal.LadoPeloPlacar(g1, g2));
    }

    // Corrigir o placar tem que corrigir o vencedor junto. Enquanto o vencedor era calculado
    // isso vinha de graça; agora é um campo, e um editar que só escrevesse os games deixaria
    // o jogo mostrando 6x3 com a outra dupla marcada como vencedora — e o ranking segue o
    // vencedor, não o placar.
    [Fact]
    public void Corrigir_o_jogo_reescreve_vencedor_e_placar_juntos()
    {
        var jogo = Jogo();
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla1, 6, 3);

        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla2, 3, 6);

        Assert.Equal(2, jogo.VencedorLado);
        Assert.Equal("3 x 6", ResultadoDoJogoSemanal.Placar(jogo));

        var pontos = PontuacaoDaPanelinha.Totais([jogo]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorDerrota, pontos[1]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, pontos[3]);
    }

    // Apagar o placar de um jogo que tinha placar não pode mexer em quem venceu.
    [Fact]
    public void Tirar_o_placar_nao_muda_quem_venceu()
    {
        var jogo = Jogo();
        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla1, 6, 3);

        ResultadoDoJogoSemanal.Aplicar(jogo, ResultadoDoJogoSemanal.Dupla1, null, null);

        Assert.Equal(1, jogo.VencedorLado);
        Assert.Null(ResultadoDoJogoSemanal.Placar(jogo));
    }
}
