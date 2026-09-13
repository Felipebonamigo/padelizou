using System;
using System.Collections.Generic;
using System.Linq;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUEM JÁ CHEGOU INTEIRO JOGA PRIMEIRO — dentro do mesmo horário (12/09/2026).
//
// 🗣️ Felipe, num print de dois jogos das 16:20: *"na parte do checkin, quando houverem 2 ou mais
// jogos no mesmo horario, coloque para 'primeiro' a jogar (desse determinado horario) por exemplo,
// digamos que a Carla Girardi chegue antes que as demais do segundo jogo do print, esse jogo vai
// pra cima se tornando o primeiro das 16:20 da lista"*.
//
// 🎯 A RAZÃO É FÍSICA, NÃO PREFERÊNCIA: jogo em que falta gente **não pode começar**. Deixá-lo no
// topo faz o organizador chamar uma quadra que vai esperar. Por isso a presença entra ACIMA da
// ordem gravada à mão (decisão do Felipe, perguntada antes de escrever): entre os que estão
// completos a ordem dele continua valendo, e entre os incompletos também — a presença só reordena
// quando uns podem começar e outros não.
//
// ⚠️ E NUNCA FURA O HORÁRIO. O primeiro critério continua sendo a hora: um jogo completo das 16:20
// não passa na frente de um incompleto das 15:30. "Primeiro a jogar" é dentro do horário dele.
//
// ⚠️ TORNEIO SEM CHAMADA NÃO MUDA NADA: sem check-in o dicionário chega vazio, ninguém está
// "completo", e a fila é exatamente a de antes. É o que o último teste guarda.
public class OrdemPelaPresencaNoHorarioTests
{
    private static readonly DateTime Dezesseis20 = new(2026, 9, 12, 16, 20, 0);
    private static readonly DateTime Quinze30 = new(2026, 9, 12, 15, 30, 0);
    private static readonly DateTime Chegou = new(2026, 9, 12, 15, 50, 0);

    private static Dupla Par(int jogador1, int? jogador2) => new()
    {
        Jogador1Id = jogador1,
        Jogador2Id = jogador2,
        Jogador1 = new Jogador { Id = jogador1, Nome = $"J{jogador1}" },
        Jogador2 = jogador2 is int dois ? new Jogador { Id = dois, Nome = $"J{dois}" } : null,
    };

    // Um jogo com quatro jogadores de ids previsíveis: 10*id .. 10*id+3.
    private static Partida Jogo(int id, DateTime hora, int? ordem = null) => new()
    {
        Id = id, Codigo = $"J{id}", Status = "Agendada", Fase = "Grupo",
        CategoriaId = 1, HorarioPrevisto = hora, OrdemNoHorario = ordem,
        Dupla1 = Par(id * 10, id * 10 + 1),
        Dupla2 = Par(id * 10 + 2, id * 10 + 3),
    };

    private static int[] Todos(int jogo) => new[] { jogo * 10, jogo * 10 + 1, jogo * 10 + 2, jogo * 10 + 3 };

    // ⚠️ A CHAVE É (PartidaId, JogadorId): a presença é do JOGO (Models/PresencaNoJogo). Marcar
    // "o jogador 20 chegou" sem dizer PRA QUE JOGO é a herança que o Felipe mandou tirar.
    private static Dictionary<(int PartidaId, int JogadorId), DateTime> Presentes(params int[] jogos) =>
        jogos.SelectMany(j => Todos(j).Select(p => (PartidaId: j, JogadorId: p)))
             .ToDictionary(k => k, _ => Chegou);

    private static Dictionary<(int PartidaId, int JogadorId), DateTime> NoJogo(int jogo, params int[] jogadores) =>
        jogadores.ToDictionary(j => (PartidaId: jogo, JogadorId: j), _ => Chegou);

    private static IEnumerable<ProximasFasesDaChave.JogoQueVem> SemPrevia() =>
        Array.Empty<ProximasFasesDaChave.JogoQueVem>();

    // O CASO DO PRINT: dois jogos às 16:20. O 2 está inteiro (a Carla chegou), o 1 não.
    [Fact]
    public void O_jogo_com_todos_presentes_vira_o_primeiro_do_horario()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dezesseis20), Jogo(2, Dezesseis20) },
            SemPrevia(),
            Presentes(2));

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // Faltando UMA pessoa o jogo não sobe: é o estado do print ANTES de a Carla chegar.
    [Fact]
    public void Faltando_uma_pessoa_o_jogo_nao_sobe()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dezesseis20), Jogo(2, Dezesseis20) },
            SemPrevia(),
            NoJogo(2, Todos(2).Take(3).ToArray()));

        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.Jogo!.Id));
    }

    // Com os dois completos, some o critério e volta a régua de sempre (aqui, o Id).
    [Fact]
    public void Com_os_dois_completos_vale_a_regua_de_sempre()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(2, Dezesseis20), Jogo(1, Dezesseis20) },
            SemPrevia(),
            Presentes(1, 2));

        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.Jogo!.Id));
    }

    // ⚠️ A HORA MANDA ANTES DE TUDO: completo das 16:20 não fura o incompleto das 15:30.
    [Fact]
    public void A_presenca_nao_fura_o_horario()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Quinze30), Jogo(2, Dezesseis20) },
            SemPrevia(),
            Presentes(2));

        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.Jogo!.Id));
    }

    // A decisão do Felipe: presença ACIMA da ordem gravada. O jogo 1 está fixado no topo pelas
    // setas ↑↓ (ordem 1), mas quem pode começar é o 2.
    [Fact]
    public void A_presenca_vence_a_ordem_gravada_a_mao()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dezesseis20, ordem: 1), Jogo(2, Dezesseis20, ordem: 2) },
            SemPrevia(),
            Presentes(2));

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // E entre os COMPLETOS a ordem gravada continua mandando — é o que faz as setas seguirem úteis.
    [Fact]
    public void Entre_os_completos_a_ordem_gravada_continua_valendo()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dezesseis20, ordem: 2), Jogo(2, Dezesseis20, ordem: 1) },
            SemPrevia(),
            Presentes(1, 2));

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // INSCRIÇÃO SEM PARCEIRO: a vaga sozinha entra na chave desde 09/09. Exigir dois checks
    // deixaria essa dupla eternamente "faltando alguém" — a régua é a do PresencaNoDia.
    [Fact]
    public void Dupla_de_uma_pessoa_so_conta_como_completa_com_um_check()
    {
        var sozinha = Jogo(2, Dezesseis20);
        sozinha.Dupla1 = Par(20, null);

        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dezesseis20), sozinha },
            SemPrevia(),
            NoJogo(2, 20, 22, 23));

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // A PRÉVIA não tem jogadores — ela não pode "estar completa" nem subir por isso.
    [Fact]
    public void A_previa_nao_sobe_pela_presenca()
    {
        var previa = new ProximasFasesDaChave.JogoQueVem(
            "5ª Feminina", "Final", 1, Dezesseis20,
            new ProximasFasesDaChave.Lado("Vencedor SF1"),
            new ProximasFasesDaChave.Lado("Vencedor SF2"),
            CategoriaId: 9, OrdemNoHorario: null);

        var linhas = OrdemNoHorario.Ordenar(new[] { Jogo(1, Dezesseis20) }, new[] { previa }, Presentes(1));

        Assert.NotNull(linhas[0].Jogo);
        Assert.NotNull(linhas[1].Previsto);
    }

    // ⚠️ TORNEIO SEM CHAMADA: dicionário vazio, e a fila é EXATAMENTE a de antes. É esta a trava
    // que impede a mudança de mexer nos torneios que não usam check-in.
    [Fact]
    public void Sem_check_in_a_fila_e_a_mesma_de_antes()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(2, Dezesseis20), Jogo(1, Dezesseis20) },
            SemPrevia(),
            new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.Jogo!.Id));
    }
}
