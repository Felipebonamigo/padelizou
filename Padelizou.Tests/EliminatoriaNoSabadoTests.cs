using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "A 5ª CATEGORIA FEMININA NÃO PODE TER JOGO SÁBADO À NOITE" — a régua por categoria.
//
// 🗣️ Pedido do Felipe, 08/09/2026: "colocar por categoria, se vai ter jogos de eliminatórias
// no sábado a noite ainda ou não. por exemplo, a 5a categoria feminina nao pode ter jogo
// sabado a noite, ai passaria para domingo de manha".
//
// ⚠️ SÓ AS ELIMINATÓRIAS, decisão do Felipe: os jogos de GRUPO da categoria continuam podendo
// cair no sábado à noite. Quem aplica esse recorte é GradeDeJogos.Encaixar — aqui só nasce a
// janela. E "passa pro domingo de manhã" não é regra escrita em lugar nenhum: bloqueada a
// noite de sábado, a próxima vaga que a grade oferece é a abertura do dia seguinte
// (Torneio.HoraInicioDiasSeguintes, 8h por padrão).
public class EliminatoriaNoSabadoTests
{
    private static readonly DateTime Sexta = new(2026, 8, 14);
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Torneio Torneio(DateTime? inicio) => new() { DataInicio = inicio };

    private static Categoria Categoria(bool podeNaNoite, int id = 1) => new()
    {
        Id = id, Nome = "5ª Categoria Feminina", Codigo = "C5F",
        EliminatoriaNoSabadoANoite = podeNaNoite,
    };

    // O padrão é o comportamento de hoje: a categoria PODE jogar sábado à noite, e nenhuma
    // janela nasce. Toda categoria que já existe no banco cai aqui.
    [Fact]
    public void Categoria_nasce_podendo_jogar_no_sabado_a_noite()
    {
        Assert.True(new Categoria().EliminatoriaNoSabadoANoite);
    }

    [Fact]
    public void Quem_pode_jogar_a_noite_nao_gera_janela()
    {
        var janelas = EliminatoriaNoSabado.Da(Torneio(Sexta.AddHours(18)), Categoria(podeNaNoite: true));

        Assert.Empty(janelas);
    }

    [Fact]
    public void Torneio_sem_data_nao_gera_janela()
    {
        var janelas = EliminatoriaNoSabado.Da(Torneio(null), Categoria(podeNaNoite: false));

        Assert.Empty(janelas);
    }

    [Fact]
    public void Torneio_sem_sabado_no_calendario_nao_gera_janela()
    {
        // Domingo 16/08 — torneio de um dia só, sem sábado nenhum pra bloquear.
        var janelas = EliminatoriaNoSabado.Da(Torneio(Sabado.AddDays(1).AddHours(8)), Categoria(podeNaNoite: false));

        Assert.Empty(janelas);
    }

    [Fact]
    public void Bloqueia_das_18h_ate_a_virada_do_sabado()
    {
        var janela = Assert.Single(EliminatoriaNoSabado.Da(Torneio(Sexta.AddHours(18)), Categoria(podeNaNoite: false)));

        Assert.Equal((Sabado.AddHours(18), Sabado.AddDays(1)), janela);
    }

    // A tarde do sábado continua livre: o corte é 18h, não meio-dia. Sem esta trava, a régua
    // nova comeria o turno da tarde inteiro e viraria outra coisa.
    [Fact]
    public void A_tarde_do_sabado_continua_livre()
    {
        var janelas = EliminatoriaNoSabado.Da(Torneio(Sexta.AddHours(18)), Categoria(podeNaNoite: false));

        Assert.DoesNotContain(janelas, j => Sabado.AddHours(14) >= j.Inicio && Sabado.AddHours(14) < j.Fim);
        Assert.Contains(janelas, j => Sabado.AddHours(21) >= j.Inicio && Sabado.AddHours(21) < j.Fim);
    }

    [Fact]
    public void PorCategoria_deixa_de_fora_quem_pode_jogar_a_noite()
    {
        var torneio = Torneio(Sexta.AddHours(18));
        torneio.Categorias.Add(Categoria(podeNaNoite: false, id: 3));
        torneio.Categorias.Add(Categoria(podeNaNoite: true, id: 4));

        var mapa = EliminatoriaNoSabado.PorCategoria(torneio);

        Assert.True(mapa.ContainsKey(3));
        Assert.False(mapa.ContainsKey(4));
    }
}
