using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/08/2026 — "O QUE CADA UM PRECISA PRA CLASSIFICAR", quando falta um jogo no grupo.
//
// ⚠️ O RISCO DESTA FEATURE NÃO É ELA NÃO APARECER: é ela APARECER MENTINDO. Um painel que
// promete "vence e passa" e o chaveamento manda a dupla pra casa é pior do que painel nenhum —
// e é o resultado garantido de escrever aqui uma segunda régua de classificação.
//
// Por isso o serviço não julga nada: ele preenche o placar que falta e pergunta ao
// `ClassificacaoDeGrupos` (a régua única, a mesma que monta a chave) quem classificou. Estes
// testes existem pra provar que a resposta é a da régua, inclusive quando ela é contraintuitiva.
public class OQuePrecisaParaClassificarTests
{
    private static readonly FormatoDaPartida.Formato Ate9 = new(1, 9);

    private static Dupla Dupla(int id, string nome) => new()
    {
        Id = id,
        Grupo = "A",
        Jogador1 = new Jogador { Nome = nome, Cpf = $"9990000000{id}", Login = $"j{id}" },
    };

    private static Partida Jogo(int dupla1, int dupla2, int? g1, int? g2) => new()
    {
        Dupla1Id = dupla1, Dupla2Id = dupla2,
        GamesDupla1 = g1, GamesDupla2 = g2,
        Fase = "Fase de Grupos",
    };

    // ===================== QUANDO O PAINEL APARECE =====================

    [Fact]
    public void Com_os_tres_jogos_por_fazer_nao_ha_o_que_dizer()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, null, null), Jogo(1, 3, null, null), Jogo(2, 3, null, null) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9));
    }

    [Fact]
    public void Faltando_DOIS_jogos_ainda_nao_da_pra_responder()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, null, null), Jogo(2, 3, null, null) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9));
    }

    [Fact]
    public void Grupo_terminado_nao_mostra_painel()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, 9, 7) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9));
    }

    // ⚠️ "Jogado" é ter VENCEDOR pela régua única, não é o Status. Um jogo finalizado 0x0 não
    // decidiu nada — contá-lo como jogado faria o painel simular o grupo errado, e o 0x0
    // finalizado por engano já aconteceu em produção (ver Services/QuemVenceu).
    [Fact]
    public void Jogo_finalizado_0x0_conta_como_NAO_jogado()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 0, 0), Jogo(2, 3, null, null) };

        // Dois sem vencedor → ainda não é hora.
        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9));
    }

    // ===================== O CASO DO FELIPE: 2 DE 3 JOGADOS =====================

    // A venceu os dois: já está dentro. Quem ganhar o último jogo pega a outra vaga, e a
    // MARGEM não importa — o vencedor sai com 1 vitória contra 0 do perdedor.
    [Fact]
    public void Com_um_lider_de_duas_vitorias_quem_vencer_o_ultimo_jogo_classifica()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        Assert.Equal("Ana", Assert.Single(quadro.JaClassificados).Jogador1!.Nome);
        Assert.Empty(quadro.SemChance);

        // Dois cenários e nenhum a mais: vitória de um, vitória do outro.
        Assert.Equal(2, quadro.Cenarios.Count);
        Assert.All(quadro.Cenarios, c => Assert.DoesNotContain("por", c.Resultado));
        Assert.Contains(quadro.Cenarios, c => c.Resultado == "Bia vencer" && c.ClassificadosIds.Contains(2));
        Assert.Contains(quadro.Cenarios, c => c.Resultado == "Cadu vencer" && c.ClassificadosIds.Contains(3));
    }

    // ⚠️ O TESTE QUE JUSTIFICA A FEATURE. Aqui a resposta é contraintuitiva e ninguém acerta de
    // cabeça na beira da quadra: as três duplas terminam com 1 vitória, o saldo decide, e a Bia
    // precisa vencer POR DOIS OU MAIS. Vencer por 1 classifica o Cadu, que perdeu o jogo.
    //
    //   Ana 9x7 Bia  (Ana +2)
    //   Cadu 9x8 Ana (Cadu +1, Ana −1)  → Ana fecha em +1
    //   falta Bia x Cadu
    [Fact]
    public void Quando_o_saldo_decide_o_painel_diz_POR_QUANTO_precisa_vencer()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[]
        {
            Jogo(1, 2, 9, 7),   // Ana ganha da Bia
            Jogo(3, 1, 9, 8),   // Cadu ganha da Ana
            Jogo(2, 3, null, null),
        };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // A Ana passa em qualquer cenário; ninguém está eliminado.
        Assert.Equal("Ana", Assert.Single(quadro.JaClassificados).Jogador1!.Nome);
        Assert.Empty(quadro.SemChance);

        var porMargem = quadro.Cenarios.Single(c => c.Resultado == "Bia vencer por 2 games ou mais");
        Assert.Contains(2, porMargem.ClassificadosIds);   // Bia entra
        Assert.DoesNotContain(3, porMargem.ClassificadosIds);

        var soPorUm = quadro.Cenarios.Single(c => c.Resultado == "Bia vencer por 1 game");
        Assert.DoesNotContain(2, soPorUm.ClassificadosIds);  // venceu e ficou fora
        Assert.Contains(3, soPorUm.ClassificadosIds);

        var cadu = quadro.Cenarios.Single(c => c.Resultado == "Cadu vencer");
        Assert.Contains(3, cadu.ClassificadosIds);
    }

    // Quando o jogo não muda nada, o painel diz isso em UMA linha — é a informação mais útil
    // que ele pode dar, e a mais fácil de se perder no meio de uma tabela de cenários.
    [Fact]
    public void Quando_o_jogo_nao_muda_nada_a_resposta_e_uma_linha_so()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, null, null) };

        // Só UM classifica: a Ana já é a primeira ganhando quem ganhar.
        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 1, Ate9)!;

        var unico = Assert.Single(quadro.Cenarios);
        Assert.Equal("Qualquer resultado", unico.Resultado);
        Assert.Equal(new[] { 1 }, unico.ClassificadosIds);
        Assert.Equal(2, quadro.SemChance.Count);
    }

    // ===================== A RÉGUA E O BANCO =====================

    // A prova de que não há segunda régua: o que o painel promete pra um placar tem que ser
    // exatamente o que o `ClassificacaoDeGrupos` responde quando aquele placar existe de fato.
    [Fact]
    public void O_que_o_painel_promete_e_o_que_a_regua_oficial_faz()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogados = new[] { Jogo(1, 2, 9, 7), Jogo(3, 1, 9, 8) };

        var quadro = OQuePrecisaParaClassificar.Montar(
            duplas, jogados.Append(Jogo(2, 3, null, null)).ToList(), 2, Ate9)!;

        // Bia vence por 2 (9x7): o painel diz que ela entra…
        var prometido = quadro.Cenarios.Single(c => c.Resultado == "Bia vencer por 2 games ou mais");

        // …e a régua oficial, com o jogo REALMENTE 9x7, diz a mesma coisa.
        var deVerdade = ClassificacaoDeGrupos
            .Calcular(duplas, jogados.Append(Jogo(2, 3, 9, 7)).ToList(), 2)
            .Select(c => c.DuplaId)
            .ToHashSet();

        Assert.Equal(deVerdade, prometido.ClassificadosIds.ToHashSet());
    }

    // ⚠️ A SIMULAÇÃO NÃO PODE ENCOSTAR NA PARTIDA DO BANCO. Se ela escrevesse o placar no
    // objeto rastreado pelo EF, bastaria um SaveChanges de qualquer outro ponto da requisição
    // pra gravar um resultado que ninguém jogou.
    [Fact]
    public void Simular_nao_mexe_na_partida_que_veio_do_banco()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var queFalta = Jogo(2, 3, null, null);
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), queFalta };

        OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9);

        Assert.Null(queFalta.GamesDupla1);
        Assert.Null(queFalta.GamesDupla2);
    }

    // Empate não pode virar cenário: `QuemVenceu` devolve null e o sistema recusa finalizar,
    // então prometer algo pra um 5x5 seria prometer pra um placar que não pode existir.
    [Fact]
    public void Nenhum_cenario_nasce_de_um_empate()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9)!;

        // Todo cenário nomeia um vencedor — nenhum fala em empate.
        Assert.All(quadro.Cenarios, c => Assert.Contains("vencer", c.Resultado));
    }

    // Na contagem por SOMA o total é fixo (5x2 e 4x3 fecham a mesma soma de 7), e o painel
    // continua respondendo — foi o formato que quase ficou de fora por só existir "até".
    [Fact]
    public void Funciona_tambem_no_torneio_de_games_por_SOMA()
    {
        var soma7 = new FormatoDaPartida.Formato(1, 7, ContagemDeGamesDoTorneio.Soma);
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 5, 2), Jogo(1, 3, 4, 3), Jogo(2, 3, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, soma7)!;

        Assert.NotEmpty(quadro.Cenarios);
        Assert.All(quadro.Cenarios, c => Assert.NotEmpty(c.ClassificadosIds));
    }
}
