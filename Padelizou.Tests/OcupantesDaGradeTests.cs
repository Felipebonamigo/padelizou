using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUEM OCUPA A QUADRA — o mapa que impede a mesma PESSOA de ser marcada em duas quadras no
// mesmo horário (a mesma pessoa inscrita em duas categorias são duas Duplas de Ids diferentes,
// e sem este mapa a grade só compara Ids).
//
// 💥 O DEFEITO QUE ESTE ARQUIVO TRAVA (09/09/2026): o mapa filtrava `d.Jogador2Id != null` —
// escrito assim pra poder fazer `Jogador2Id!.Value` na linha seguinte, não por regra nenhuma.
// Enquanto dupla incompleta ficava fora do sorteio isso era inofensivo. No dia em que ela
// passou a ENTRAR na chave, virou o pior tipo de defeito: quem se inscreveu sozinho sumia do
// mapa de pessoas e podia ser marcado em DUAS QUADRAS AO MESMO TEMPO — calado, porque a tela
// "Conferir grade" empresta este mesmo mapa e também não veria nada.
public class OcupantesDaGradeTests
{
    private static Dupla Dupla(int id, int jogador1, int? jogador2) =>
        new() { Id = id, Codigo = $"D{id}", Jogador1Id = jogador1, Jogador2Id = jogador2 };

    [Fact]
    public void Dupla_fechada_ocupa_a_quadra_com_as_duas_pessoas()
    {
        var mapa = RoboDoChaveamento.OcupantesPorDupla(new[] { Dupla(1, 10, 11) });

        Assert.Equal(new[] { 10, 11 }, mapa[1]);
    }

    [Fact]
    public void Inscricao_sozinha_continua_no_mapa_com_a_pessoa_que_existe()
    {
        // O CORAÇÃO DO CONSERTO. Meia dupla ainda é uma pessoa ocupando uma quadra — e é
        // justamente ela que corre risco de estar inscrita noutra categoria também.
        var mapa = RoboDoChaveamento.OcupantesPorDupla(new[] { Dupla(1, 10, null) });

        Assert.True(mapa.ContainsKey(1), "A inscrição sozinha sumiu do mapa: a grade deixaria "
            + "de enxergar esse jogador como pessoa e poderia marcá-lo em duas quadras.");
        Assert.Equal(new[] { 10 }, mapa[1]);
    }

    [Fact]
    public void A_mesma_pessoa_sozinha_em_duas_categorias_aparece_nas_duas_duplas()
    {
        // O cenário real: Paulo se inscreve sozinho na 3ª e também joga a 4ª com parceiro. Se
        // a inscrição sozinha não estiver no mapa, a grade marca os dois jogos dele no mesmo
        // horário, em quadras diferentes.
        var mapa = RoboDoChaveamento.OcupantesPorDupla(new[]
        {
            Dupla(1, 10, null),   // Paulo sozinho na 3ª
            Dupla(2, 10, 12),     // Paulo com parceiro na 4ª
        });

        Assert.Contains(10, mapa[1]);
        Assert.Contains(10, mapa[2]);
    }

    [Fact]
    public void Time_continua_fora_do_mapa()
    {
        // Regra antiga que continua valendo, e o motivo está no comentário do método: todo
        // TIME tem o organizador no Jogador1Id, então comparar por pessoa faria todo time
        // conflitar com todo time e empurraria a grade inteira pra frente.
        var time = Dupla(9, 777, null);
        time.NomeTime = "Nata Padel";

        Assert.Empty(RoboDoChaveamento.OcupantesPorDupla(new[] { time }));
    }
}
