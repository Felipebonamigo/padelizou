using Padelizou.Models;
using Padelizou.Services;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// A DURAÇÃO POR FASE DENTRO DA GRADE (19/11/2026). A vaga continua do tamanho dos grupos;
// o jogo mais longo OCUPA as vagas que precisar da quadra dele, e a fase seguinte espera o
// fim de verdade da anterior. Sem duração própria, tudo sai exatamente como antes.
public class DuracaoPorFaseNaGradeTests
{
    private static readonly DateTime Dia = new(2026, 11, 21);

    private static Partida Jogo(int d1, int d2, string fase, int categoriaId = 1) => new()
    {
        Codigo = $"{d1}x{d2}", Status = "Agendada", Fase = fase, CategoriaId = categoriaId,
        Dupla1Id = d1, Dupla2Id = d2,
    };

    private static List<DateTime> Vagas(int quantas) =>
        Enumerable.Range(0, quantas).Select(i => Dia.AddHours(10).AddMinutes(50 * i)).ToList();

    private static Func<Partida, TimeSpan> FinalDe80 =>
        p => TimeSpan.FromMinutes(p.Fase == "Final" ? 80 : 50);

    [Fact]
    public void Final_longa_segura_a_quadra_por_duas_vagas()
    {
        var final = Jogo(1, 2, "Final");
        var outroCurto = Jogo(5, 6, "Grupo A", categoriaId: 3);

        GradeDeJogos.Encaixar(new List<Partida> { final, outroCurto }, Vagas(4), 50,
            quadras: new[] { "Quadra 1" }, duracaoDoJogo: FinalDe80);

        Assert.Equal(Dia.AddHours(10), final.HorarioPrevisto);
        Assert.Equal(Dia.AddHours(10).AddMinutes(100), outroCurto.HorarioPrevisto);
        Assert.Equal("Quadra 1", outroCurto.NomeQuadra);
    }

    // Torneio sem quadra cadastrada: a vaga é só um horário, e a final longa consome a do
    // horário seguinte também.
    [Fact]
    public void Sem_quadra_cadastrada_a_final_longa_consome_a_vaga_seguinte()
    {
        var final = Jogo(1, 2, "Final");
        var outro = Jogo(5, 6, "Grupo A", categoriaId: 3);

        GradeDeJogos.Encaixar(new List<Partida> { final, outro }, Vagas(4), 50, duracaoDoJogo: FinalDe80);

        Assert.Equal(Dia.AddHours(10), final.HorarioPrevisto);
        Assert.Equal(Dia.AddHours(10).AddMinutes(100), outro.HorarioPrevisto);
    }

    [Fact]
    public void Quem_joga_a_final_longa_nao_e_chamado_antes_de_ela_acabar()
    {
        var final = Jogo(1, 2, "Final");
        var outraCategoria = Jogo(1, 9, "Final", categoriaId: 2);   // dupla 1 joga as duas

        GradeDeJogos.Encaixar(new List<Partida> { final, outraCategoria }, Vagas(4), 50,
            quadras: new[] { "Quadra 1", "Quadra 2" }, duracaoDoJogo: FinalDe80);

        Assert.Equal(Dia.AddHours(10), final.HorarioPrevisto);
        Assert.True(outraCategoria.HorarioPrevisto >= Dia.AddHours(10).AddMinutes(80),
            $"chamado às {outraCategoria.HorarioPrevisto:HH:mm}, com a final das 10:00 (80 min) em quadra");
    }

    // Já marcada (o "refazer grade" com a final no lugar): também segura a quadra.
    [Fact]
    public void Final_longa_ja_marcada_tambem_segura_a_quadra()
    {
        var marcada = Jogo(1, 2, "Final");
        marcada.HorarioPrevisto = Dia.AddHours(10);
        marcada.NomeQuadra = "Quadra 1";
        var outro = Jogo(5, 6, "Grupo A", categoriaId: 3);

        GradeDeJogos.Encaixar(new List<Partida> { outro }, Vagas(4).Skip(1).ToList(), 50,
            quadras: new[] { "Quadra 1" }, jaMarcados: new[] { marcada }, duracaoDoJogo: FinalDe80);

        Assert.Equal(Dia.AddHours(10).AddMinutes(100), outro.HorarioPrevisto);
    }

    [Fact]
    public void Sem_duracao_por_jogo_a_grade_e_a_de_sempre()
    {
        var final = Jogo(1, 2, "Final");
        var outro = Jogo(5, 6, "Grupo A", categoriaId: 3);

        GradeDeJogos.Encaixar(new List<Partida> { final, outro }, Vagas(4), 50, quadras: new[] { "Quadra 1" });

        Assert.Equal(Dia.AddHours(10).AddMinutes(50), outro.HorarioPrevisto);
    }

    // ── A FASE SEGUINTE ESPERA O FIM DE VERDADE DA ANTERIOR ─────────────────────────────
    private static Torneio Torneio(int semi = 0) => new()
    {
        Nome = "T", TempoPrevistoPartidaMinutos = 50, TempoSemifinalMinutos = semi,
        HoraInicioDoDia = new TimeSpan(8, 0, 0), HoraFimDoDia = new TimeSpan(23, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0), QuantidadeQuadras = 2,
    };

    [Fact]
    public void A_final_espera_a_semifinal_longa_acabar()
    {
        var semi = Jogo(1, 2, "Semifinal");
        semi.HorarioPrevisto = Dia.AddHours(10);

        var piso = LevasDaGrade.PisoDaCategoria(Torneio(semi: 80), new[] { semi },
            OrdemDasFases.Posto("Final"), 1);

        Assert.Equal(Dia.AddHours(10).AddMinutes(80), piso);
    }

    [Fact]
    public void Sem_duracao_propria_a_final_espera_o_de_sempre()
    {
        var semi = Jogo(1, 2, "Semifinal");
        semi.HorarioPrevisto = Dia.AddHours(10);

        var piso = LevasDaGrade.PisoDaCategoria(Torneio(), new[] { semi }, OrdemDasFases.Posto("Final"), 1);

        Assert.Equal(Dia.AddHours(10).AddMinutes(50), piso);
    }

    // ── A PRÉVIA (o que a tela promete e o robô cumpre) ─────────────────────────────────
    // Dois grupos, dois classificados cada: semifinal e final.
    private static CadeiaDeFases SemisEFinal() =>
        ProximasFasesDaChave.MontarDosGrupos(new[] { "Grupo A", "Grupo B" }, 2, Dia.AddHours(9).AddMinutes(10), "1ª");

    [Fact]
    public void Na_previa_a_final_sai_depois_do_fim_das_semis_longas()
    {
        var grade = new ConfiguracaoDaGrade(50, 2, Array.Empty<string>(),
            new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0),
            MinutosDaFase: f => f == "Semifinal" ? 80 : 50);

        var jogos = ProximasFasesDaChave.Agendar(new[] { SemisEFinal() }, grade);

        var ultimaSemi = jogos.Where(j => j.Fase == "Semifinal").Max(j => j.Horario!.Value);
        var final = jogos.Single(j => j.Fase == "Final").Horario!.Value;
        Assert.True(final >= ultimaSemi.AddMinutes(80), $"final às {final:HH:mm}, semi às {ultimaSemi:HH:mm}");
    }

    [Fact]
    public void Na_previa_o_jogo_longo_segura_a_vaga_seguinte()
    {
        // Uma quadra só: semi 1 às 10:00 (80 min = 2 vagas) → semi 2 não pode ser às 10:50.
        var grade = new ConfiguracaoDaGrade(50, 1, Array.Empty<string>(),
            new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0),
            MinutosDaFase: f => f == "Semifinal" ? 80 : 50);

        var semis = ProximasFasesDaChave.Agendar(new[] { SemisEFinal() }, grade)
            .Where(j => j.Fase == "Semifinal").Select(j => j.Horario!.Value).OrderBy(h => h).ToList();

        Assert.Equal(TimeSpan.FromMinutes(100), semis[1] - semis[0]);
    }

    // As semis JÁ EXISTEM (o robô criou) e a final ainda é prévia: ela abre depois do fim delas.
    [Fact]
    public void Na_previa_a_final_depois_das_semis_reais_espera_o_fim_delas()
    {
        var semis = new[]
        {
            new PartidaDaChave(1, "Semifinal", "A", "B", Dia.AddHours(10)),
            new PartidaDaChave(2, "Semifinal", "C", "D", Dia.AddHours(10)),
        };
        var grade = new ConfiguracaoDaGrade(50, 2, Array.Empty<string>(),
            new TimeSpan(23, 0, 0), new TimeSpan(8, 0, 0),
            MinutosDaFase: f => f == "Semifinal" ? 80 : 50);

        var final = ProximasFasesDaChave.Agendar(
                new[] { ProximasFasesDaChave.Montar(semis, Array.Empty<string>(), "1ª") }, grade)
            .Single(j => j.Fase == "Final");

        Assert.Equal(Dia.AddHours(10).AddMinutes(80), final.Horario);
    }
}

// O "Conferir grade" e a cópia de torneio também conhecem a duração por fase.
public class DuracaoPorFaseNaAuditoriaTests
{
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio(int final = 0) => new()
    {
        Nome = "T", Codigo = "T1", DataInicio = Sabado.AddHours(8), QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50, TempoFinalMinutos = final,
        HoraInicioDoDia = new TimeSpan(8, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = new Categoria { Id = 1, Nome = "3ª", Codigo = "C3" } };

    [Fact]
    public void Quem_esta_na_final_longa_e_chamado_pra_outro_jogo_antes_dela_acabar_e_acusado()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 10, 31) };
        var final = new Partida { Codigo = "F", Fase = "Final", CategoriaId = 1, Dupla1Id = 1, Dupla2Id = 2, HorarioPrevisto = Sabado.AddHours(10) };
        var outro = new Partida { Codigo = "G", Fase = "Grupo A", CategoriaId = 2, Dupla1Id = 3, Dupla2Id = 2, HorarioPrevisto = Sabado.AddHours(10).AddMinutes(50) };

        var comFinalLonga = AuditoriaDaGrade.Conferir(Torneio(final: 80), new[] { final, outro }, duplas, SedesDoTorneio.Nenhuma);
        var deSempre = AuditoriaDaGrade.Conferir(Torneio(), new[] { final, outro }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(comFinalLonga, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
        Assert.DoesNotContain(deSempre, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    [Fact]
    public void A_copia_do_torneio_leva_as_duracoes_por_fase()
    {
        var copiados = DuplicacaoDeTorneio.Copiadas;
        Assert.Contains(nameof(Padelizou.Models.Torneio.TempoMataMataMinutos), copiados);
        Assert.Contains(nameof(Padelizou.Models.Torneio.TempoSemifinalMinutos), copiados);
        Assert.Contains(nameof(Padelizou.Models.Torneio.TempoFinalMinutos), copiados);
    }
}
