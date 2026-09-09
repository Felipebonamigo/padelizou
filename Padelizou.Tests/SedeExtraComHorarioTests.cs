using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O LOCAL EXTERNO ALUGADO POR ALGUMAS HORAS (08/09/2026).
//
// 🗣️ Felipe: "esse do ER por exemplo, como colocou muita dupla, ele terá q locar um local
// externo ao dele, ou seja, adicionar mais quadras para por os jogos, então teremos que por
// essa opção, e ai também vai ter q por quantos jogos vão para la, ou quais horarios, quais
// categorias".
//
// ⚠️ METADE DISSO JÁ EXISTIA desde 21/08: torneio em mais de um clube, quadra sabendo em que
// clube fica, categoria presa a um clube, folga pra atravessar a cidade. O que faltava eram
// DUAS coisas:
//
//  1. A JANELA DA QUADRA. O local externo é alugado das 8h às 14h de sábado, e até aqui toda
//     quadra valia o expediente inteiro do torneio.
//  2. O TRANSBORDO. Prender a categoria INTEIRA num clube (o que já existia) é o jeito do Dez
//     E Batata; o do Er é outro — a sede principal enche e o que sobra vai pro externo.
//
// 💡 "Quantos jogos vão pra lá" NÃO virou campo, decisão tomada com o Felipe: é `quadras ×
// rodadas da janela`, ou seja, uma CONTA. Dois campos pra mesma informação discordariam, e o
// organizador não saberia qual mandou.
public class SedeExtraComHorarioTests
{
    private const int Principal = 1;
    private const int Externo = 2;
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Quadra Q(string nome, int? clube, DateTime? de = null, DateTime? ate = null) =>
        new() { Nome = nome, ClubeId = clube, DisponivelDe = de, DisponivelAte = ate };

    private static SedesDoTorneio Montar(IEnumerable<Quadra> quadras, IEnumerable<Categoria> categorias) =>
        SedesDoTorneio.Montar(Principal, 30, quadras, categorias);

    private static SedesDoTorneio DuasSedes(DateTime? de = null, DateTime? ate = null,
        bool categoriaPodeTransbordar = true) =>
        Montar(
            new[] { Q("Central", Principal), Q("Externa 1", Externo, de, ate) },
            new[] { new Categoria { Id = 10, Nome = "5ª F", Codigo = "C5F",
                                    PodeJogarNaSedeExtra = categoriaPodeTransbordar } });

    // ── A janela da quadra ────────────────────────────────────────────────────────────────

    [Fact]
    public void Quadra_sem_janela_esta_aberta_a_qualquer_hora()
    {
        var sede = DuasSedes();

        Assert.True(sede.QuadraAberta("Central", Sabado.AddHours(3)));
        Assert.True(sede.QuadraAberta("Central", Sabado.AddHours(22)));
    }

    [Fact]
    public void Quadra_com_janela_fecha_antes_e_depois()
    {
        var sede = DuasSedes(de: Sabado.AddHours(8), ate: Sabado.AddHours(14));

        Assert.False(sede.QuadraAberta("Externa 1", Sabado.AddHours(7)));
        Assert.True(sede.QuadraAberta("Externa 1", Sabado.AddHours(8)));
        Assert.True(sede.QuadraAberta("Externa 1", Sabado.AddHours(13).AddMinutes(59)));
        Assert.False(sede.QuadraAberta("Externa 1", Sabado.AddHours(14)));
    }

    // A janela é MEIO ABERTA, mesmo formato de JanelasDeImpedimento: o jogo que começa às 14h
    // em ponto já está fora de uma janela que termina às 14h.
    [Fact]
    public void So_o_lado_de_abertura_conta_como_dentro()
    {
        var sede = DuasSedes(de: Sabado.AddHours(8), ate: Sabado.AddHours(14));

        Assert.True(sede.QuadraAberta("Externa 1", Sabado.AddHours(8)));
        Assert.False(sede.QuadraAberta("Externa 1", Sabado.AddHours(14)));
    }

    [Fact]
    public void So_uma_das_pontas_tambem_vale()
    {
        var soAte = DuasSedes(ate: Sabado.AddHours(14));
        Assert.True(soAte.QuadraAberta("Externa 1", Sabado.AddHours(3)));
        Assert.False(soAte.QuadraAberta("Externa 1", Sabado.AddHours(15)));

        var soDe = DuasSedes(de: Sabado.AddHours(8));
        Assert.False(soDe.QuadraAberta("Externa 1", Sabado.AddHours(7)));
        Assert.True(soDe.QuadraAberta("Externa 1", Sabado.AddHours(23)));
    }

    // ⚠️ Nome que não está no cadastro não fecha nada. Mesmo motivo de `ClubeDaQuadra`:
    // `Partida.NomeQuadra` é texto solto, e uma quadra escrita direto no jogo não pode
    // desaparecer da grade por não ter cadastro.
    [Fact]
    public void Quadra_desconhecida_conta_como_aberta()
    {
        Assert.True(DuasSedes(de: Sabado.AddHours(8), ate: Sabado.AddHours(14))
            .QuadraAberta("Quadra que ninguém cadastrou", Sabado.AddHours(3)));
    }

    // ⚠️ DECISÃO REVISTA EM 09/09/2026. Este teste dizia o contrário — "uma quadra com janela
    // num torneio de sede única continuaria aberta sempre, porque a janela nasceu pro local
    // ALUGADO" —, e isso valeu enquanto a janela só existia numa sub-aba de sedes. Com a tela
    // de planejamento oferecendo "de que horas até que horas" pra CADA quadra (pedido do
    // Felipe: o Er pode alugar quadra no próprio complexo), a saída antecipada virava campo
    // que aceita o valor e o joga fora. A regra nova está em JanelaDeQuadraNoClubeUnicoTests;
    // o que fica aqui é a metade que NÃO mudou: sede única continua sendo sede única.
    [Fact]
    public void Torneio_de_uma_sede_so_respeita_a_janela_sem_virar_duas_sedes()
    {
        var sede = Montar(new[] { Q("Central", null, Sabado.AddHours(8), Sabado.AddHours(14)) },
                          Array.Empty<Categoria>());

        Assert.False(sede.MaisDeUmClube);
        Assert.True(sede.QuadraAberta("Central", Sabado.AddHours(9)));
        Assert.False(sede.QuadraAberta("Central", Sabado.AddHours(20)));
    }

    // ── Qual quadra é da sede EXTRA ───────────────────────────────────────────────────────

    [Fact]
    public void A_sede_extra_e_a_que_nao_e_o_clube_principal()
    {
        var sede = DuasSedes();

        Assert.False(sede.EhSedeExtra("Central"));
        Assert.True(sede.EhSedeExtra("Externa 1"));
    }

    // ── O transbordo por categoria ────────────────────────────────────────────────────────

    [Fact]
    public void Categoria_marcada_pode_ir_pro_externo()
    {
        Assert.True(DuasSedes(categoriaPodeTransbordar: true).PodeIrPraSedeExtra(10));
    }

    [Fact]
    public void Categoria_desmarcada_nao_pode()
    {
        Assert.False(DuasSedes(categoriaPodeTransbordar: false).PodeIrPraSedeExtra(10));
    }

    // ⚠️ O DEFAULT PRESERVA O QUE JÁ EXISTE. Antes desta coluna, categoria sem clube fixo podia
    // ser marcada em qualquer quadra do torneio. Nascer `false` prenderia, em silêncio, todas
    // as categorias soltas de todo torneio de duas sedes que já está no banco.
    [Fact]
    public void Categoria_nasce_podendo_ir()
    {
        Assert.True(new Categoria().PodeJogarNaSedeExtra);
    }

    // Categoria que a grade não conhece (id fora do torneio) não vira trava: mesma régua
    // defensiva de `QuadrasDe`, que nunca devolve lista vazia.
    [Fact]
    public void Categoria_desconhecida_nao_e_travada()
    {
        Assert.True(DuasSedes().PodeIrPraSedeExtra(999));
    }
}
