using Padelizou.Services;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// A Semifinal e a Final não existiam pra quem olhava a tela: o motor só cria a rodada quando
// a anterior fecha, então o jogador via a primeira rodada e mais nada — sem saber a que horas
// voltar nem contra quem pode jogar.
//
// O horário sai da duração configurada no torneio, igual ao resto da grade. Atrasar na
// prática não muda o slot.
public class ProximasFasesDaChaveTests
{
    private static readonly TimeSpan FimDoDia = TimeSpan.FromHours(23);
    private static readonly TimeSpan AberturaSeguinte = TimeSpan.FromHours(8);

    private static PartidaDaChave Jogo(int id, string fase, string d1, string d2, string hora) =>
        new(id, fase, d1, d2, DateTime.Parse($"2026-08-08 {hora}"));

    private static List<JogoQueVem> Montar(IReadOnlyList<PartidaDaChave> partidas,
        IReadOnlyList<string>? byes = null, int quadras = 2, int duracao = 30) =>
        ProximasFasesDaChave.Montar(partidas, byes ?? Array.Empty<string>(),
            duracao, quadras, FimDoDia, AberturaSeguinte);

    [Fact]
    public void Sem_mata_mata_nao_projeta_nada()
    {
        Assert.Empty(Montar(Array.Empty<PartidaDaChave>()));
    }

    [Fact]
    public void Da_semifinal_sai_a_final()
    {
        var semis = new[]
        {
            Jogo(1, "Semifinal", "Ana/Bruno", "Carla/Diego", "20:00"),
            Jogo(2, "Semifinal", "Edu/Fabi", "Gabi/Helo", "20:00"),
        };

        var final = Assert.Single(Montar(semis));

        Assert.Equal("Final", final.Fase);
        Assert.Equal("Vencedor Semifinal 1", final.Lado1.Rotulo);
        Assert.Equal("Vencedor Semifinal 2", final.Lado2.Rotulo);
    }

    [Fact]
    public void A_rodada_seguinte_e_EXATA_e_a_de_depois_lista_quem_pode_chegar()
    {
        // Quartas → Semi sai "vencedor de X × Y" (só dois podem chegar). Semi → Final já são
        // quatro candidatos por lado, e aí a tela diz isso em vez de fingir precisão.
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "19:00"),
            Jogo(3, "Quartas de Final", "E1/E2", "F1/F2", "19:30"),
            Jogo(4, "Quartas de Final", "G1/G2", "H1/H2", "19:30"),
        };

        var jogos = Montar(quartas);

        Assert.Equal(new[] { "Semifinal", "Semifinal", "Final" }, jogos.Select(j => j.Fase));
        Assert.Equal("Vencedor Quartas de Final 1", jogos[0].Lado1.Rotulo);
        Assert.Equal("Vencedor Semifinal 1", jogos[2].Lado1.Rotulo);
        Assert.Equal("Vencedor Semifinal 2", jogos[2].Lado2.Rotulo);
    }

    [Fact]
    public void O_primeiro_cruza_com_o_ULTIMO_igual_ao_sorteio_de_verdade()
    {
        // ChaveamentoMataMata.ParearVencedores cruza vencedores[i] com vencedores[n-1-i].
        // Parear diferente aqui prometeria na tela um confronto que o torneio não vai fazer.
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "19:00"),
            Jogo(3, "Quartas de Final", "E1/E2", "F1/F2", "19:30"),
            Jogo(4, "Quartas de Final", "G1/G2", "H1/H2", "19:30"),
        };

        var semis = Montar(quartas).Where(j => j.Fase == "Semifinal").ToList();

        // 1º jogo com o 4º, 2º com o 3º.
        Assert.Equal("Vencedor Quartas de Final 1", semis[0].Lado1.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 4", semis[0].Lado2.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 2", semis[1].Lado1.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 3", semis[1].Lado2.Rotulo);
    }

    [Fact]
    public void Quem_passou_direto_aparece_pelo_NOME_e_entra_no_fim_da_fila()
    {
        // Bye não é candidato: já se sabe quem é. E ele entra depois dos vencedores, igual
        // ao AvancoDaChave — é o que faz cada vencedor pegar uma dupla descansada.
        var primeira = new[]
        {
            Jogo(1, "Primeira Rodada", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Primeira Rodada", "C1/C2", "D1/D2", "19:00"),
        };

        var jogos = Montar(primeira, byes: new[] { "Cabeça1", "Cabeça2" });
        var proxima = jogos.Where(j => j.Fase == "Semifinal").ToList();

        Assert.Equal(2, proxima.Count);
        Assert.Equal("Vencedor Primeira Rodada 1", proxima[0].Lado1.Rotulo);
        Assert.Equal("Cabeça2", proxima[0].Lado2.Rotulo);
        // Bye tem NOME, não procedência: ele não veio de jogo nenhum.
    }

    [Fact]
    public void O_nome_da_fase_sai_de_QUANTOS_sobraram_e_nao_de_encadear_a_anterior()
    {
        // Achado escrevendo isto: encadear ProximaFase("Primeira Rodada") dá "Oitavas de
        // Final", mas 2 jogos + 2 byes são 4 duplas, ou seja SEMIFINAL. Os robôs de verdade
        // nomeiam por NomeFase(quantos avançam) — encadear anunciaria na tela uma fase que o
        // torneio nunca vai criar.
        var primeira = new[]
        {
            Jogo(1, "Primeira Rodada", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Primeira Rodada", "C1/C2", "D1/D2", "19:00"),
        };

        var fases = Montar(primeira, byes: new[] { "Cabeça1", "Cabeça2" })
            .Select(j => j.Fase).Distinct();

        Assert.Equal(new[] { "Semifinal", "Final" }, fases);
    }

    [Fact]
    public void O_horario_segue_a_duracao_do_torneio_e_o_numero_de_quadras()
    {
        // 4 jogos em 2 quadras = duas levas; a fase seguinte começa depois da última.
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "19:00"),
            Jogo(3, "Quartas de Final", "E1/E2", "F1/F2", "19:30"),
            Jogo(4, "Quartas de Final", "G1/G2", "H1/H2", "19:30"),
        };

        var jogos = Montar(quartas, quadras: 2, duracao: 30);
        var semis = jogos.Where(j => j.Fase == "Semifinal").ToList();
        var final = jogos.Single(j => j.Fase == "Final");

        // Última quartas às 19:30, jogos de 30 min: a fase acaba 20:00 e a semi abre 20:30 —
        // uma rodada de folga, porque quem sai das quartas é quem joga a semi. As duas semis
        // cabem nas 2 quadras, e a final abre 21:30 pela mesma conta.
        Assert.Equal(DateTime.Parse("2026-08-08 20:30"), semis[0].Horario);
        Assert.Equal(DateTime.Parse("2026-08-08 20:30"), semis[1].Horario);
        Assert.Equal(DateTime.Parse("2026-08-08 21:30"), final.Horario);
    }

    [Fact]
    public void Com_uma_quadra_so_os_jogos_da_fase_nao_caem_no_mesmo_horario()
    {
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "19:00"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "19:40"),
            Jogo(3, "Quartas de Final", "E1/E2", "F1/F2", "20:20"),
            Jogo(4, "Quartas de Final", "G1/G2", "H1/H2", "21:00"),
        };

        var semis = Montar(quartas, quadras: 1, duracao: 40)
            .Where(j => j.Fase == "Semifinal").Select(j => j.Horario).ToList();

        // Últimas quartas 21:00 → acabam 21:40, e a semi abre 22:20 (a folga de uma rodada).
        Assert.Equal(DateTime.Parse("2026-08-08 22:20"), semis[0]);
        Assert.Equal(DateTime.Parse("2026-08-08 23:00"), semis[1]);
    }

    // ---- Uma fase nunca encosta na fase que a alimenta ----

    [Fact]
    public void A_final_nao_cai_no_mesmo_horario_da_semifinal_que_a_decide()
    {
        // O defeito, visto pelo Felipe na tela do Interno Los Corneteiros (05/08/2026):
        // "Semifinal 2 22:01" e logo abaixo "Final 22:01". A conta de slots era CORRIDA entre
        // as rodadas — com 5 quadras, as 2 semis ocupavam 2 delas, sobravam 3 vagas no mesmo
        // horário e a final entrava numa. Fisicamente impossível: os finalistas ainda estavam
        // em quadra jogando a semi.
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "21:37"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "21:37"),
            Jogo(3, "Quartas de Final", "E1/E2", "F1/F2", "21:37"),
            Jogo(4, "Quartas de Final", "G1/G2", "H1/H2", "21:37"),
        };

        var jogos = Montar(quartas, quadras: 5, duracao: 12);
        var semis = jogos.Where(j => j.Fase == "Semifinal").ToList();
        var final = jogos.Single(j => j.Fase == "Final");

        Assert.All(semis, s => Assert.NotEqual(s.Horario, final.Horario));
        Assert.True(final.Horario > semis.Max(s => s.Horario));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void Nenhuma_fase_comeca_antes_de_a_anterior_acabar(int quadras)
    {
        // A regra vale em qualquer número de quadras — e é justamente quando a rodada NÃO
        // enche as quadras que a conta corrida furava. 8 jogos de primeira rodada + 8 byes
        // percorrem oitavas → quartas → semi → final, então a varredura pega toda fronteira.
        var primeira = Enumerable.Range(1, 8)
            .Select(i => Jogo(i, "Primeira Rodada", $"A{i}", $"B{i}", "19:00"))
            .ToList();
        var byes = Enumerable.Range(1, 8).Select(i => $"Cabeça {i}").ToList();

        var jogos = Montar(primeira, byes, quadras: quadras, duracao: 30);

        foreach (var fase in jogos.Select(j => j.Fase).Distinct())
        {
            var anterior = jogos.Where(j => j.Fase == fase).Max(j => j.Horario)!.Value;
            var seguintes = jogos.SkipWhile(j => j.Fase != fase).Where(j => j.Fase != fase).ToList();

            Assert.All(seguintes, j => Assert.True(j.Horario > anterior,
                $"{quadras} quadra(s): {j.FaseNumerada} às {j.Horario:HH:mm} não espera a {fase}, " +
                $"que só acaba depois de {anterior:HH:mm}"));
        }
    }

    [Fact]
    public void Jogo_que_passa_do_fim_do_dia_vai_pra_abertura_do_dia_seguinte()
    {
        // Mesma regra que já vale pro resto da grade (GradeDeJogos.DepoisDe): não marca jogo
        // de madrugada porque a conta deu.
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "A1/A2", "B1/B2", "22:40"),
            Jogo(2, "Quartas de Final", "C1/C2", "D1/D2", "22:40"),
        };

        var semi = Montar(quartas, quadras: 2, duracao: 40).First();

        Assert.Equal(DateTime.Parse("2026-08-09 08:00"), semi.Horario);
    }

    [Fact]
    public void A_final_encerra_a_projecao()
    {
        // Depois da final não há fase nenhuma — e a projeção não pode ficar inventando.
        var final = new[] { Jogo(1, "Final", "A1/A2", "B1/B2", "21:00") };

        Assert.Empty(Montar(final));
    }

    [Fact]
    public void Parte_da_fase_mais_ADIANTADA_que_ja_tem_jogo()
    {
        // A primeira rodada continua no banco depois de acabar; projetar a partir dela
        // recriaria a semifinal que já existe.
        var partidas = new[]
        {
            Jogo(1, "Semifinal", "A1/A2", "B1/B2", "20:00"),
            Jogo(2, "Semifinal", "C1/C2", "D1/D2", "20:00"),
            Jogo(3, "Primeira Rodada", "A1/A2", "X1/X2", "19:00"),
            Jogo(4, "Primeira Rodada", "B1/B2", "Y1/Y2", "19:00"),
        };

        var jogos = Montar(partidas);

        Assert.Equal("Final", Assert.Single(jogos).Fase);
    }

    [Fact]
    public void Sem_horario_marcado_a_projecao_ainda_diz_quem_joga()
    {
        // Torneio "por ordem de liberação" não tem horário previsto. O confronto continua
        // valendo — some só a hora.
        var semis = new[]
        {
            new PartidaDaChave(1, "Semifinal", "Ana/Bruno", "Carla/Diego", null),
            new PartidaDaChave(2, "Semifinal", "Edu/Fabi", "Gabi/Helo", null),
        };

        var final = Assert.Single(Montar(semis));

        Assert.Null(final.Horario);
        Assert.Equal("Vencedor Semifinal 1", final.Lado1.Rotulo);
    }

    // ---- Categoria que AINDA está na fase de grupos ----
    // Ela não tem jogo de mata-mata nenhum de onde partir, então a projeção começa nas
    // COLOCAÇÕES. Sem isso, só a chave direta mostrava o caminho até a final na lista de
    // jogos — ela já nasce com a primeira rodada criada.

    [Fact]
    public void Categoria_em_fase_de_grupos_projeta_o_mata_mata_desde_as_colocacoes()
    {
        var jogos = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" },
            classificadosPorGrupo: 2,
            horarioBase: new DateTime(2026, 8, 5, 20, 0, 0),
            duracaoMinutos: 12, quadras: 5,
            ultimoInicioDoDia: new TimeSpan(23, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            categoria: "6ª Masculina");

        // 4 grupos x 2 = 8 classificados: Quartas (4), Semifinal (2), Final (1).
        Assert.Equal(new[] { "Quartas de Final", "Quartas de Final", "Quartas de Final",
                             "Quartas de Final", "Semifinal", "Semifinal", "Final" },
                     jogos.Select(j => j.Fase));

        // A primeira rodada fala em COLOCAÇÃO; da segunda em diante, em número de jogo.
        Assert.StartsWith("1º do Grupo", jogos[0].Lado1.Rotulo);
        Assert.StartsWith("2º do Grupo", jogos[0].Lado2.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 1", jogos[4].Lado1.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 4", jogos[4].Lado2.Rotulo);
        Assert.Equal("Vencedor Semifinal 1", jogos[^1].Lado1.Rotulo);

        // Numeração reinicia a cada fase — é o que o rótulo cita.
        Assert.Equal(new[] { 1, 2, 3, 4 }, jogos.Where(j => j.Fase == "Quartas de Final").Select(j => j.Numero));
        Assert.Equal("Quartas de Final 3", jogos[2].FaseNumerada);
        Assert.Equal("Final", jogos[^1].FaseNumerada);   // jogo único não numera

        Assert.All(jogos, j => Assert.Equal("6ª Masculina", j.Categoria));
        Assert.All(jogos, j => Assert.NotNull(j.Horario));
    }

    [Fact]
    public void Rotulo_nao_lista_nomes_de_dupla_e_por_isso_nao_estoura_a_linha()
    {
        // O motivo da mudança: "Um destes 4: Bernardo Mendonça & Alexandre Medina, Geison
        // Moyses & …" estourava a largura e era cortado justamente no fim.
        var jogos = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" }, 2,
            null, 12, 5, new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0));

        Assert.All(jogos, j =>
        {
            Assert.DoesNotContain("Um destes", j.Lado1.Rotulo);
            Assert.DoesNotContain("Um destes", j.Lado2.Rotulo);
            Assert.True(j.Lado1.Rotulo.Length <= 30, $"rótulo comprido: {j.Lado1.Rotulo}");
            Assert.True(j.Lado2.Rotulo.Length <= 30, $"rótulo comprido: {j.Lado2.Rotulo}");
        });
    }

    [Fact]
    public void Sem_grupo_sorteado_nao_ha_o_que_projetar()
    {
        var jogos = ProximasFasesDaChave.MontarDosGrupos(
            Array.Empty<string>(), 2, null, 12, 5, new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0));

        Assert.Empty(jogos);
    }
}
