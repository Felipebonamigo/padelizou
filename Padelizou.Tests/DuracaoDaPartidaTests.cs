using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A DURAÇÃO POR FASE (19/11/2026) — Rafael, da Wallau: finais de 2 sets de 6 num torneio de
// jogos de 50 min. Felipe: grupos, mata-mata, semi e final, com a opção "todas iguais".
public class DuracaoDaPartidaTests
{
    private static Torneio Torneio(int grupos = 50, int mataMata = 0, int semi = 0, int final = 0) => new()
    {
        Nome = "T", TempoPrevistoPartidaMinutos = grupos,
        TempoMataMataMinutos = mataMata, TempoSemifinalMinutos = semi, TempoFinalMinutos = final,
    };

    [Theory]
    [InlineData("Grupo A", 50)]
    [InlineData("Primeira Rodada", 60)]
    [InlineData("Oitavas de Final", 60)]
    [InlineData("Quartas de Final", 60)]
    [InlineData("Semifinal", 70)]
    [InlineData("Final", 80)]
    [InlineData(null, 50)]
    [InlineData("Rodada 3", 50)]
    public void Cada_fase_tem_a_sua_duracao(string? fase, int minutos)
    {
        Assert.Equal(minutos, DuracaoDaPartida.Minutos(Torneio(50, 60, 70, 80), fase));
    }

    // Zero é "igual aos grupos" — é o que todo torneio anterior a isto tem gravado.
    [Theory]
    [InlineData("Quartas de Final")]
    [InlineData("Semifinal")]
    [InlineData("Final")]
    public void Fase_sem_duracao_propria_usa_a_dos_grupos(string fase)
    {
        Assert.Equal(45, DuracaoDaPartida.Minutos(Torneio(45), fase));
    }

    [Fact]
    public void Grupos_zerado_continua_virando_50()
    {
        Assert.Equal(50, DuracaoDaPartida.Minutos(Torneio(0), "Final"));
    }

    // A grade tem vaga do tamanho dos grupos: jogo mais longo ocupa as vagas que precisar.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 1)]
    [InlineData(40, 1)]
    [InlineData(51, 2)]
    [InlineData(80, 2)]
    [InlineData(100, 2)]
    [InlineData(120, 3)]
    public void Jogo_longo_ocupa_mais_de_uma_vaga(int final, int vagas)
    {
        Assert.Equal(vagas, DuracaoDaPartida.Vagas(Torneio(50, final: final), "Final"));
    }

    [Fact]
    public void Todas_iguais_quando_nenhuma_fase_tem_duracao_propria()
    {
        Assert.True(DuracaoDaPartida.TodasIguais(Torneio(50)));
        Assert.False(DuracaoDaPartida.TodasIguais(Torneio(50, final: 80)));
        Assert.False(DuracaoDaPartida.TodasIguais(Torneio(50, mataMata: 40)));
    }
}

// A PREVISÃO CONTA VAGAS, não jogos: semi e final de 100 min numa grade de 50 tomam duas cada.
public class VagasDoMataMataTests
{
    private static Torneio Torneio(int semi = 0, int final = 0, int mataMata = 0) => new()
    {
        Nome = "T", TempoPrevistoPartidaMinutos = 50,
        TempoMataMataMinutos = mataMata, TempoSemifinalMinutos = semi, TempoFinalMinutos = final,
    };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]   // só a final
    [InlineData(3, 3)]   // semis + final
    [InlineData(7, 7)]   // quartas + semis + final
    public void Sem_duracao_propria_cada_jogo_e_uma_vaga(int jogos, int vagas)
    {
        Assert.Equal(vagas, PrevisaoDoTorneio.VagasDoMataMata(Torneio(), jogos));
    }

    [Fact]
    public void Semi_e_final_longas_contam_as_vagas_que_tomam()
    {
        // 7 jogos: 4 quartas (40 → 1 vaga), 2 semis (100 → 2), 1 final (100 → 2).
        Assert.Equal(4 + 4 + 2, PrevisaoDoTorneio.VagasDoMataMata(Torneio(semi: 100, final: 100, mataMata: 40), 7));
    }
}
