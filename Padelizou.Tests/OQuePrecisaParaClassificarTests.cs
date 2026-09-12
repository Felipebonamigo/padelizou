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

    // ⚠️ COM PARCEIRO desde 11/09/2026: o nome que os cenários usam virou `Dupla.NomeCurto`
    // ("Bia / Bruna"), o MESMO da lista de jogos do grupo. Era `Jogador1.ComoChamar`, e o
    // pop-up acabava chamando a mesma dupla de três jeitos na mesma tela.
    private static Dupla Dupla(int id, string nome, string parceiro = "Parceiro") => new()
    {
        Id = id,
        Grupo = "A",
        Jogador1 = new Jogador { Nome = nome, Cpf = $"9990000000{id}", Login = $"j{id}" },
        Jogador2 = new Jogador { Nome = parceiro, Cpf = $"9991000000{id}", Login = $"p{id}" },
    };

    // ⚠️ COM `Status`, como em produção: o painel decide o que já foi jogado olhando pra ele,
    // e uma partida de teste sem status nenhum esconderia justamente o defeito de 12/09/2026
    // (jogo EM QUADRA contado como decidido). Jogo sem placar nasce "Agendada"; com placar,
    // "Finalizada" — pro jogo em quadra existe o `EmQuadra` logo abaixo.
    private static Partida Jogo(int dupla1, int dupla2, int? g1, int? g2) => new()
    {
        Dupla1Id = dupla1, Dupla2Id = dupla2,
        GamesDupla1 = g1, GamesDupla2 = g2,
        Fase = "Fase de Grupos",
        Status = g1 == null && g2 == null ? "Agendada" : "Finalizada",
    };

    // O jogo que está ACONTECENDO: placar parcial na tela, nada decidido.
    private static Partida EmQuadra(int dupla1, int dupla2, int g1, int g2) => new()
    {
        Dupla1Id = dupla1, Dupla2Id = dupla2,
        GamesDupla1 = g1, GamesDupla2 = g2,
        Fase = "Fase de Grupos",
        Status = "AoVivo",
    };

    // ===================== QUANDO O PAINEL APARECE =====================

    [Fact]
    public void Com_os_tres_jogos_por_fazer_nao_ha_o_que_dizer()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, null, null), Jogo(1, 3, null, null), Jogo(2, 3, null, null) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos));
    }

    [Fact]
    public void Faltando_DOIS_jogos_ainda_nao_da_pra_responder()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, null, null), Jogo(2, 3, null, null) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos));
    }

    [Fact]
    public void Grupo_terminado_nao_mostra_painel()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, 9, 7) };

        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos));
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
        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos));
    }

    // ═══════════════ O JOGO QUE AINDA ESTÁ EM QUADRA ═══════════════
    //
    // 🗣️ Felipe, 12/09/2026, num print do pop-up do Grupo B da 6ª Feminina (2ª Etapa ER Padel
    // Tour): *"Isso parece errado, é meio impossivel"*.
    //
    // 🕳️ E ERA. O painel dizia "Vania / Eliane — Já classificado" e "Bibiana / Caroline — Sem
    // chance" ENQUANTO as duas estavam jogando uma contra a outra: o jogo 568 estava AO VIVO,
    // 5 x 6 na parcial, e o painel leu o placar parcial como resultado final. Oito minutos
    // depois o placar virou e o pop-up trocou de resposta — a Bibiana, "sem chance", voltou a
    // depender do jogo. O painel eliminou uma dupla por causa de um game de vantagem no meio
    // de um jogo em andamento.
    //
    // ⚠️ A CAUSA É UMA PERGUNTA MAL FEITA: "tem vencedor?" (`QuemVenceu`) responde SIM pra
    // qualquer placar desigual, e placar parcial é desigual quase o tempo todo. Quem decide se
    // ACABOU é o `Status` — e o serviço vizinho que preenche a chave projetada
    // (`ClassificadosJaConhecidos`) já perguntava isso ("grupo com jogo em quadra também não").
    // Este não perguntava.
    //
    // ⚠️ E A TELA DENUNCIAVA A CONTRADIÇÃO NO MESMO CARD: a tabela do grupo, que soma só
    // partidas finalizadas (TorneiosController), mostrava a Bibiana com J1 V0 D1 −6; o pop-up
    // logo abaixo dava a ela uma vitória que ninguém tinha ganhado.
    [Fact]
    public void Jogo_em_quadra_nao_conta_como_jogado()
    {
        // O Grupo B do print, número por número: 1 = Cristina/Marina, 2 = Vania/Eliane,
        // 3 = Bibiana/Caroline.
        var duplas = new[] { Dupla(1, "Cristina"), Dupla(2, "Vania"), Dupla(3, "Bibiana") };
        var jogos = new[]
        {
            Jogo(3, 1, 3, 9),        // ENCERRADO: Bibiana/Caroline 3 x 9 Cristina/Marina
            EmQuadra(2, 3, 5, 6),    // AO VIVO:   Vania/Eliane 5 x 6 Bibiana/Caroline
            Jogo(2, 1, null, null),  // AGENDADO:  Vania/Eliane x Cristina/Marina
        };

        // FALTAM DOIS jogos, não um: o que está em quadra ainda não decidiu nada. Com dois em
        // aberto não há painel nenhum a mostrar — que é a régua que este arquivo já tinha.
        Assert.Null(OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos));
    }

    // O outro lado da mesma régua, e o que impede a "correção" preguiçosa de simplesmente
    // esconder o painel quando existe jogo em quadra: se o jogo EM QUADRA é o único que falta,
    // o painel é exatamente o que a pessoa na beira da quadra quer — e ele tem que simular
    // aquele jogo, não dar o placar parcial por final.
    [Fact]
    public void O_jogo_em_quadra_e_o_que_o_painel_simula_quando_e_o_ultimo()
    {
        var duplas = new[] { Dupla(1, "Cristina"), Dupla(2, "Vania"), Dupla(3, "Bibiana") };
        var emQuadra = EmQuadra(2, 3, 5, 6);
        var jogos = new[]
        {
            Jogo(3, 1, 3, 9),   // Cristina venceu a Bibiana
            Jogo(2, 1, 5, 9),   // Cristina venceu a Vania → 2 vitórias, já está dentro
            emQuadra,           // Vania x Bibiana, 5 x 6 na parcial: decide a 2ª vaga
        };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos);

        Assert.NotNull(quadro);
        Assert.Same(emQuadra, quadro!.JogoQueFalta);

        // Quem está 6 x 5 na frente NÃO está classificado, e quem está atrás NÃO está
        // eliminado: os dois dependem do fim do jogo, que é o que o placar da tela diz.
        Assert.Equal(OQuePrecisaParaClassificar.Estado.JaClassificado,
            quadro.Situacoes.Single(s => s.Dupla.Id == 1).Estado);
        Assert.Equal(OQuePrecisaParaClassificar.Estado.Depende,
            quadro.Situacoes.Single(s => s.Dupla.Id == 2).Estado);
        Assert.Equal(OQuePrecisaParaClassificar.Estado.Depende,
            quadro.Situacoes.Single(s => s.Dupla.Id == 3).Estado);
    }

    // ===================== O CASO DO FELIPE: 2 DE 3 JOGADOS =====================

    // A venceu os dois: já está dentro. Quem ganhar o último jogo pega a outra vaga, e a
    // MARGEM não importa — o vencedor sai com 1 vitória contra 0 do perdedor.
    [Fact]
    public void Com_um_lider_de_duas_vitorias_quem_vencer_o_ultimo_jogo_classifica()
    {
        var duplas = new[] { Dupla(1, "Ana"), Dupla(2, "Bia"), Dupla(3, "Cadu") };
        var jogos = new[] { Jogo(1, 2, 9, 3), Jogo(1, 3, 9, 5), Jogo(2, 3, null, null) };

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos)!;

        Assert.Equal("Ana", Assert.Single(quadro.JaClassificados).Jogador1!.Nome);
        Assert.Empty(quadro.SemChance);

        // Dois cenários e nenhum a mais: vitória de um, vitória do outro.
        Assert.Equal(2, quadro.Cenarios.Count);
        Assert.All(quadro.Cenarios, c => Assert.DoesNotContain("por", c.Resultado));
        Assert.Contains(quadro.Cenarios, c => c.Resultado == "Bia / Parceiro vencer" && c.ClassificadosIds.Contains(2));
        Assert.Contains(quadro.Cenarios, c => c.Resultado == "Cadu / Parceiro vencer" && c.ClassificadosIds.Contains(3));
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

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos)!;

        // A Ana passa em qualquer cenário; ninguém está eliminado.
        Assert.Equal("Ana", Assert.Single(quadro.JaClassificados).Jogador1!.Nome);
        Assert.Empty(quadro.SemChance);

        var porMargem = quadro.Cenarios.Single(c => c.Resultado == "Bia / Parceiro vencer por 2 games ou mais");
        Assert.Contains(2, porMargem.ClassificadosIds);   // Bia entra
        Assert.DoesNotContain(3, porMargem.ClassificadosIds);

        var soPorUm = quadro.Cenarios.Single(c => c.Resultado == "Bia / Parceiro vencer por 1 game");
        Assert.DoesNotContain(2, soPorUm.ClassificadosIds);  // venceu e ficou fora
        Assert.Contains(3, soPorUm.ClassificadosIds);

        var cadu = quadro.Cenarios.Single(c => c.Resultado == "Cadu / Parceiro vencer");
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
        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 1, Ate9, ClassificacaoDeGrupos.SemPontos)!;

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
            duplas, jogados.Append(Jogo(2, 3, null, null)).ToList(), 2, Ate9,
            ClassificacaoDeGrupos.SemPontos)!;

        // Bia vence por 2 (9x7): o painel diz que ela entra…
        var prometido = quadro.Cenarios.Single(c => c.Resultado == "Bia / Parceiro vencer por 2 games ou mais");

        // …e a régua oficial, com o jogo REALMENTE 9x7, diz a mesma coisa.
        var deVerdade = ClassificacaoDeGrupos
            .Calcular(duplas, jogados.Append(Jogo(2, 3, 9, 7)).ToList(), ClassificacaoDeGrupos.SemPontos, 2)
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

        OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos);

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

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, Ate9, ClassificacaoDeGrupos.SemPontos)!;

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

        var quadro = OQuePrecisaParaClassificar.Montar(duplas, jogos, 2, soma7, ClassificacaoDeGrupos.SemPontos)!;

        Assert.NotEmpty(quadro.Cenarios);
        Assert.All(quadro.Cenarios, c => Assert.NotEmpty(c.ClassificadosIds));
    }
}
