using System;
using System.Collections.Generic;
using System.Linq;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A ORDEM DOS JOGOS DENTRO DO MESMO HORÁRIO (10/09/2026).
//
// 🗣️ Felipe, arrumando o domingo do Er: *"quando eu altero um jogo, no mesmo horario, ele nao
// esta trocando a ordem na linha, tem q trocar tambem para q eu possa colocar a ordem que eu
// quiser"* — e, no mesmo fôlego: *"por padrão, se tem semifinal 1 e semifinal 2 no mesmo
// horario, siga a ordem automatica de a 1 vir antes da 2, mas permita q o usuario edite"*.
//
// 🕳️ A lista da aba Jogos ordenava SÓ por HorarioPrevisto, sobre uma lista que veio do Postgres
// SEM `ORDER BY`. No mesmo horário não havia desempate nenhum: a ordem era a que o banco
// devolvesse, e ela muda sozinha depois de um UPDATE. "Semifinal 1 antes da 2" era sorte.
public class OrdemDosJogosNoMesmoHorarioTests
{
    private static readonly DateTime Dez40 = new(2026, 9, 13, 10, 40, 0);
    private static readonly DateTime Onze30 = new(2026, 9, 13, 11, 30, 0);

    private static Partida Jogo(int id, DateTime hora, int? ordem = null, int categoria = 1) => new()
    {
        Id = id, Codigo = $"J{id}", Status = "Agendada", Fase = "Semifinal",
        CategoriaId = categoria, HorarioPrevisto = hora, OrdemNoHorario = ordem,
    };

    private static ProximasFasesDaChave.JogoQueVem Previa(
        int numero, DateTime hora, int? ordem = null, int categoria = 9, string fase = "Final") =>
        new("5ª Masculina", fase, numero, hora,
            new ProximasFasesDaChave.Lado("Vencedor SF1"),
            new ProximasFasesDaChave.Lado("Vencedor SF2"),
            CategoriaId: categoria, OrdemNoHorario: ordem);

    // O PADRÃO PEDIDO: sem ninguém mexer, a Semifinal 1 vem antes da Semifinal 2. Elas são da
    // mesma categoria e da mesma fase, e o que as numera é a ordem de Id (ReservasDeHorario
    // .NumeroNaFase) — então ordenar por Id É a ordem automática que o Felipe descreveu.
    [Fact]
    public void Sem_ordem_gravada_a_Semifinal_1_vem_antes_da_Semifinal_2()
    {
        // De propósito na ordem errada: é assim que o Postgres pode devolver depois de um UPDATE.
        var linhas = OrdemNoHorario.Ordenar(new[] { Jogo(2, Dez40), Jogo(1, Dez40) }, Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { 1, 2 }, linhas.Select(l => l.Jogo!.Id));
    }

    [Fact]
    public void O_horario_manda_antes_de_qualquer_desempate()
    {
        var linhas = OrdemNoHorario.Ordenar(new[] { Jogo(1, Onze30), Jogo(2, Dez40) }, Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // A ordem gravada é a do organizador — ela vence a automática, e é isso que faz "a ordem
    // que eu quiser" existir.
    [Fact]
    public void A_ordem_gravada_vence_a_automatica()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dez40, ordem: 2), Jogo(2, Dez40, ordem: 1) },
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // Quem NÃO tem ordem gravada vai pro fim do horário: a numeração explícita é 1..k a partir
    // do topo, então "sem número" só pode significar "abaixo dos numerados". Um jogo que nasceu
    // depois (a prévia que virou rodada) cai no fim em vez de furar a fila que o organizador montou.
    [Fact]
    public void Quem_nao_tem_ordem_gravada_fica_depois_de_quem_tem()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dez40), Jogo(2, Dez40, ordem: 1) },
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { 2, 1 }, linhas.Select(l => l.Jogo!.Id));
    }

    // A PRÉVIA ENTRA NA MESMA FILA. As semifinais e finais de domingo do Er ainda não nasceram
    // — deixá-las fora da ordenação seria não resolver o pedido.
    [Fact]
    public void A_previa_entra_na_mesma_fila_e_obedece_a_ordem_gravada()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dez40, ordem: 2) },
            new[] { Previa(1, Dez40, ordem: 1) }, new Dictionary<(int, int), DateTime>());

        Assert.Null(linhas[0].Jogo);
        Assert.NotNull(linhas[0].Previsto);
        Assert.Equal(1, linhas[1].Jogo!.Id);
    }

    // Sem ordem gravada, jogo REAL vem antes de prévia no mesmo horário — é o que a tela já
    // fazia (a lista concatenava reais e depois previstos), e mudar isso sem pedido seria
    // inventar regra.
    [Fact]
    public void Sem_ordem_gravada_o_jogo_real_vem_antes_da_previa()
    {
        var linhas = OrdemNoHorario.Ordenar(new[] { Jogo(1, Dez40) }, new[] { Previa(1, Dez40) }, new Dictionary<(int, int), DateTime>());

        Assert.Equal(1, linhas[0].Jogo!.Id);
        Assert.NotNull(linhas[1].Previsto);
    }

    // Duas prévias no mesmo horário desempatam por categoria → fase → número: a Semifinal 1
    // antes da Semifinal 2 também quando nenhuma das duas nasceu.
    [Fact]
    public void Duas_previas_no_mesmo_horario_seguem_categoria_fase_e_numero()
    {
        var linhas = OrdemNoHorario.Ordenar(
            Array.Empty<Partida>(),
            new[]
            {
                Previa(2, Dez40, categoria: 9, fase: "Semifinal"),
                Previa(1, Dez40, categoria: 9, fase: "Semifinal"),
                Previa(1, Dez40, categoria: 3, fase: "Semifinal"),
            }, new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { (3, 1), (9, 1), (9, 2) },
            linhas.Select(l => (l.Previsto!.CategoriaId!.Value, l.Previsto!.Numero)));
    }

    // ═══ MATERIALIZAR: o que faz a troca DENTRO do mesmo horário significar alguma coisa ═══
    //
    // Dois jogos no mesmo horário, os dois sem ordem gravada, trocarem `null` por `null` não
    // muda nada — era exatamente o ⇄ no-op que o Felipe reportou. Antes de trocar, a fila
    // ganha número explícito.
    //
    // ⚠️ SÓ O PREFIXO, até o jogo movido: numerar o horário inteiro fixaria também as prévias
    // ABAIXO dele, que ninguém tocou — e fixar prévia é criar reserva (Models/ReservaDeHorario),
    // que prende o horário dela contra a grade. Quem está embaixo continua no automático, e o
    // automático já vem depois do manual.
    [Fact]
    public void Materializar_numera_1_a_k_so_ate_o_jogo_movido()
    {
        var a = Jogo(1, Dez40);
        var b = Jogo(2, Dez40);
        var c = Jogo(3, Dez40);
        var linhas = OrdemNoHorario.Ordenar(new[] { a, b, c }, Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        var numerar = OrdemNoHorario.Materializar(linhas, linhas[0], linhas[1]).ToList();

        Assert.Equal(new[] { (linhas[0], 1), (linhas[1], 2) }, numerar);
    }

    [Fact]
    public void Materializar_nao_mexe_em_quem_ja_tem_numero_certo()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dez40, ordem: 1), Jogo(2, Dez40, ordem: 2) },
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Empty(OrdemNoHorario.Materializar(linhas, linhas[0], linhas[1]));
    }

    // Horários diferentes não precisam de número nenhum: quem separa os dois já é a hora, e o
    // que a troca faz é trocar o slot (hora + quadra + clube).
    [Fact]
    public void Materializar_nao_faz_nada_quando_os_dois_estao_em_horarios_diferentes()
    {
        var linhas = OrdemNoHorario.Ordenar(
            new[] { Jogo(1, Dez40), Jogo(2, Onze30) },
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Empty(OrdemNoHorario.Materializar(linhas, linhas[0], linhas[1]));
    }
}
