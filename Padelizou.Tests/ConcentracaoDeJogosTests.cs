using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "COLOCAR OS 2 JOGOS NA SEXTA" — o favor que só o organizador (e o adm) concede.
//
// 🗣️ Pedido do Felipe, 08/09/2026: "permita também criar uma opção, lá nos impedimentos, de
// 'colocar os 2 jogos na sexta', colocar os 2 jogos no sábado a tarde, os 2 jogos no sábado de
// manha, apenas para os organizadores e adm do sistema, para que nós possamos auxiliar
// algumas pessoas".
//
// ⚠️ É O AVESSO DO IMPEDIMENTO, e é por isso que não cabia nos quatro booleanos: o impedimento
// diz "não posso em X" (e Services/ImpedimentoUnico garante no máximo UM ligado); a
// concentração diz "só posso em X", que é o complemento — precisaria de três ligados, quebrando
// a invariante e fazendo o preço contar três taxas.
//
// Escopo decidido pelo Felipe: vale só pra FASE DE GRUPOS ("os 2 jogos" são os 2 do grupo). A
// eliminatória acontece depois dos grupos por definição — forçá-la pro mesmo turno seria pedir
// o impossível. Quem aplica esse recorte é GradeDeJogos.Encaixar; ver ConcentracaoNaGradeTests.
public class ConcentracaoDeJogosTests
{
    // Sexta 14/08/2026, sábado 15/08 — mesmas datas de JanelasDeImpedimentoTests.
    private static readonly DateTime Sexta = new(2026, 8, 14);
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Torneio Torneio(DateTime? inicio) => new() { DataInicio = inicio };

    private static Dupla Dupla(TurnoDeConcentracao turno) => new() { ConcentrarJogosEm = turno };

    [Fact]
    public void Sem_concentracao_nao_ha_janela()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sexta.AddHours(18)), Dupla(TurnoDeConcentracao.Nenhuma));

        Assert.Empty(janelas);
    }

    [Fact]
    public void Torneio_sem_data_nao_gera_janela()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(null), Dupla(TurnoDeConcentracao.SextaNoite));

        Assert.Empty(janelas);
    }

    // ⚠️ A TRAVA MAIS IMPORTANTE DO ARQUIVO. "Só sexta" num torneio que não TEM sexta não pode
    // virar "proibido em toda parte" — seria a dupla sem horário nenhum, o oposto do favor.
    [Fact]
    public void Turno_que_o_torneio_nao_tem_nao_bloqueia_nada()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sabado.AddHours(8)), Dupla(TurnoDeConcentracao.SextaNoite));

        Assert.Empty(janelas);
    }

    [Fact]
    public void So_na_sexta_deixa_a_sexta_livre()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sexta.AddHours(18)), Dupla(TurnoDeConcentracao.SextaNoite));

        Assert.DoesNotContain(janelas, j => Sexta.AddHours(19) >= j.Inicio && Sexta.AddHours(19) < j.Fim);
        Assert.DoesNotContain(janelas, j => Sexta.AddHours(23) >= j.Inicio && Sexta.AddHours(23) < j.Fim);
    }

    [Fact]
    public void So_na_sexta_bloqueia_sabado_e_domingo_inteiros()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sexta.AddHours(18)), Dupla(TurnoDeConcentracao.SextaNoite));

        Assert.Contains(janelas, j => Sabado.AddHours(9) >= j.Inicio && Sabado.AddHours(9) < j.Fim);
        Assert.Contains(janelas, j => Sabado.AddHours(21) >= j.Inicio && Sabado.AddHours(21) < j.Fim);
        Assert.Contains(janelas, j => Sabado.AddDays(1).AddHours(10) >= j.Inicio && Sabado.AddDays(1).AddHours(10) < j.Fim);
        // E a quinta, que é ANTES do turno escolhido.
        Assert.Contains(janelas, j => Sexta.AddDays(-1).AddHours(20) >= j.Inicio && Sexta.AddDays(-1).AddHours(20) < j.Fim);
    }

    [Fact]
    public void So_no_sabado_de_manha_libera_ate_o_meio_dia_e_bloqueia_a_tarde()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sexta.AddHours(18)), Dupla(TurnoDeConcentracao.SabadoManha));

        Assert.DoesNotContain(janelas, j => Sabado.AddHours(9) >= j.Inicio && Sabado.AddHours(9) < j.Fim);
        // Meio-dia já é tarde — a janela é meio aberta, mesmo corte de JanelasDeImpedimento.
        Assert.Contains(janelas, j => Sabado.AddHours(12) >= j.Inicio && Sabado.AddHours(12) < j.Fim);
        Assert.Contains(janelas, j => Sexta.AddHours(20) >= j.Inicio && Sexta.AddHours(20) < j.Fim);
    }

    [Fact]
    public void So_no_sabado_a_tarde_bloqueia_a_manha_e_libera_a_tarde()
    {
        var janelas = ConcentracaoDeJogos.Da(Torneio(Sexta.AddHours(18)), Dupla(TurnoDeConcentracao.SabadoTarde));

        Assert.Contains(janelas, j => Sabado.AddHours(9) >= j.Inicio && Sabado.AddHours(9) < j.Fim);
        Assert.DoesNotContain(janelas, j => Sabado.AddHours(12) >= j.Inicio && Sabado.AddHours(12) < j.Fim);
        Assert.DoesNotContain(janelas, j => Sabado.AddHours(20) >= j.Inicio && Sabado.AddHours(20) < j.Fim);
    }

    // Mesmo padrão de JanelasDeImpedimento.PorDupla: dupla sem concentração fica FORA do mapa,
    // pra não sobrar entrada inútil pro Encaixar checar à toa.
    [Fact]
    public void PorDupla_deixa_de_fora_quem_nao_tem_concentracao()
    {
        var torneio = Torneio(Sexta.AddHours(18));
        var comConcentracao = Dupla(TurnoDeConcentracao.SextaNoite);
        comConcentracao.Id = 7;
        var sem = Dupla(TurnoDeConcentracao.Nenhuma);
        sem.Id = 8;

        var mapa = ConcentracaoDeJogos.PorDupla(torneio, new[] { comConcentracao, sem });

        Assert.True(mapa.ContainsKey(7));
        Assert.False(mapa.ContainsKey(8));
    }

    [Theory]
    [InlineData(TurnoDeConcentracao.SextaNoite, "Os 2 jogos na sexta à noite")]
    [InlineData(TurnoDeConcentracao.SabadoManha, "Os 2 jogos no sábado de manhã")]
    [InlineData(TurnoDeConcentracao.SabadoTarde, "Os 2 jogos no sábado à tarde")]
    public void Rotulo_diz_o_favor_por_extenso(TurnoDeConcentracao turno, string esperado)
    {
        Assert.Equal(esperado, ConcentracaoDeJogos.Rotulo(turno));
    }
}
