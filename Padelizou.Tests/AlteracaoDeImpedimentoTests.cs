using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// TROCAR O IMPEDIMENTO DEPOIS DE SE INSCREVER.
//
// 🗣️ Pedido do Felipe, 02/09/2026: *"permita a pessoa alterar o impedimento, até o fechamento
// das inscrições"*. E, sobre o dinheiro: *"se já tem outro impedimento, mantém o mesmo custo;
// se não, avisa que é cobrado (quando for cobrado) e o valor que é adicionado"*.
//
// A régua cai redonda porque uma inscrição pode marcar NO MÁXIMO UM impedimento (ver
// Services/ImpedimentoUnico): a quantidade só vive em 0 ou 1, então só existem três
// movimentos — trocar (1 → 1, de graça), marcar (0 → 1, cobra) e tirar (1 → 0).
//
// ⚠️ O torneio cobra `TaxaPorImpedimento` por janela marcada, e o `ValorInscricao` é
// CONGELADO quando a inscrição nasce (Models/Dupla). Mexer no impedimento sem mexer no valor
// deixaria a dupla pagando por uma janela que não tem mais, ou tirando uma janela da grade de
// graça.
public class AlteracaoDeImpedimentoTests
{
    private static Torneio Torneio(string status = "Inscrições Abertas", decimal taxa = 20m) => new()
    {
        Id = 1, Nome = "2ª Etapa ER PADEL TOUR (EPT)", Codigo = "EPT2",
        Status = status, TaxaPorImpedimento = taxa, PrecoInscricao = 150m,
    };

    private static Dupla Dupla(bool sexta = false, bool sabadoManha = false,
        decimal? valor = 300m, bool pago = false) => new()
    {
        Id = 586, CategoriaId = 9, Jogador1Id = 7, Jogador2Id = 8, Codigo = "D586",
        ImpedimentoSextaNoite = sexta, ImpedimentoSabadoManha = sabadoManha,
        ValorInscricao = valor, Pago = pago,
    };

    // ── QUEM PODE MEXER ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Quem_esta_na_dupla_pode_alterar()
    {
        Assert.Null(AlteracaoDeImpedimento.MotivoParaNaoAlterar(Dupla(), Torneio(), 7));
        Assert.Null(AlteracaoDeImpedimento.MotivoParaNaoAlterar(Dupla(), Torneio(), 8));
    }

    [Fact]
    public void Quem_nao_esta_na_dupla_nao_mexe_no_impedimento_dos_outros()
    {
        var motivo = AlteracaoDeImpedimento.MotivoParaNaoAlterar(Dupla(), Torneio(), 99);

        Assert.Contains("não é sua", motivo);
    }

    [Theory]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Chaves em Aprovação")]
    [InlineData("Fase de Grupos")]
    [InlineData("Finalizado")]
    public void Fechada_a_inscricao_o_impedimento_congela(string status)
    {
        // É o limite que o Felipe pediu, e tem motivo de grade: depois do sorteio os jogos já
        // estão marcados, e mudar a janela agora obriga a remontar tudo.
        var motivo = AlteracaoDeImpedimento.MotivoParaNaoAlterar(Dupla(), Torneio(status), 7);

        Assert.Contains("inscrições", motivo);
    }

    [Fact]
    public void Inscricao_que_nao_existe_nao_estoura()
    {
        Assert.NotNull(AlteracaoDeImpedimento.MotivoParaNaoAlterar(null, Torneio(), 7));
        Assert.NotNull(AlteracaoDeImpedimento.MotivoParaNaoAlterar(Dupla(), null, 7));
    }

    // ── O DINHEIRO ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Trocar_um_impedimento_por_outro_nao_muda_o_valor()
    {
        // A régua do Felipe: "se já tem outro impedimento, mantém o mesmo custo".
        var dupla = Dupla(sexta: true);

        Assert.Equal(0m, AlteracaoDeImpedimento.QuantoMudaOValor(
            dupla, Torneio(), TurnoDoImpedimento.SabadoManha));
    }

    [Fact]
    public void Marcar_o_primeiro_impedimento_ADICIONA_a_taxa()
    {
        // "se não, avisa que é cobrado e o valor que é adicionado".
        var dupla = Dupla();

        Assert.Equal(20m, AlteracaoDeImpedimento.QuantoMudaOValor(
            dupla, Torneio(), TurnoDoImpedimento.SextaNoite));
    }

    [Fact]
    public void Tirar_o_impedimento_devolve_a_taxa_ao_valor_devido()
    {
        // Simétrico, e sem dinheiro andando: a inscrição ainda não foi paga, então o que muda
        // é quanto ela AINDA DEVE — não há nada pra estornar.
        var dupla = Dupla(sexta: true);

        Assert.Equal(-20m, AlteracaoDeImpedimento.QuantoMudaOValor(
            dupla, Torneio(), TurnoDoImpedimento.Nenhum));
    }

    [Fact]
    public void Torneio_que_nao_cobra_impedimento_nunca_muda_valor()
    {
        var dupla = Dupla();

        Assert.Equal(0m, AlteracaoDeImpedimento.QuantoMudaOValor(
            dupla, Torneio(taxa: 0m), TurnoDoImpedimento.SextaNoite));
    }

    // ── JÁ PAGO ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Inscricao_paga_ainda_pode_TROCAR_de_turno()
    {
        // Não mexe em um centavo: a quantidade continua 1. É a alteração mais comum — "não
        // posso mais na sexta, posso no sábado" — e barrá-la mandaria a pessoa pro organizador
        // sem necessidade nenhuma.
        var dupla = Dupla(sexta: true, pago: true);

        Assert.Null(AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            dupla, Torneio(), 7, TurnoDoImpedimento.SabadoManha));
    }

    [Fact]
    public void Inscricao_paga_NAO_pode_mudar_o_valor_por_conta_propria()
    {
        // ⚠️ Aqui o dinheiro JÁ entrou. Marcar um impedimento novo criaria cobrança extra e
        // tirar criaria devolução — e devolução neste sistema é o botão de estorno do
        // organizador, na mão (ver ESTORNO.md). A tela manda falar com ele em vez de fingir
        // que resolve.
        var marcando = AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            Dupla(pago: true), Torneio(), 7, TurnoDoImpedimento.SextaNoite);

        var tirando = AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            Dupla(sexta: true, pago: true), Torneio(), 7, TurnoDoImpedimento.Nenhum);

        Assert.Contains("organizador", marcando);
        Assert.Contains("organizador", tirando);
    }

    // ── APLICAR ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Aplicar_grava_o_turno_novo_e_apaga_o_velho()
    {
        // A trava do ImpedimentoUnico continua valendo depois da alteração: um, e só um.
        var dupla = Dupla(sexta: true);

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(), TurnoDoImpedimento.SabadoManha,
            quemAlterou: 8, agora: new DateTime(2026, 9, 2, 10, 0, 0));

        Assert.False(dupla.ImpedimentoSextaNoite);
        Assert.True(dupla.ImpedimentoSabadoManha);
        Assert.False(dupla.ImpedimentoQuintaNoite);
        Assert.False(dupla.ImpedimentoSabadoTarde);
    }

    [Fact]
    public void Aplicar_deixa_registrado_QUEM_mexeu_e_QUANDO()
    {
        // 🗣️ "deixe registrado quem marcou o impedimento". Sem isso, a dupla que chega no dia
        // reclamando do horário não tem como saber qual dos dois marcou — e o organizador,
        // menos ainda.
        var dupla = Dupla();
        var agora = new DateTime(2026, 9, 2, 10, 0, 0);

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(), TurnoDoImpedimento.SextaNoite,
            quemAlterou: 8, agora: agora);

        Assert.Equal(8, dupla.ImpedimentoAlteradoPorId);
        Assert.Equal(agora, dupla.ImpedimentoAlteradoEm);
    }

    [Fact]
    public void Aplicar_ajusta_o_valor_congelado_da_inscricao()
    {
        var dupla = Dupla(valor: 300m);

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(), TurnoDoImpedimento.SextaNoite,
            quemAlterou: 7, agora: DateTime.Now);

        Assert.Equal(320m, dupla.ValorInscricao);
    }

    [Fact]
    public void Inscricao_sem_valor_congelado_continua_sem_valor()
    {
        // Inscrição anterior à coluna `ValorInscricao` (nula de propósito — ver Models/Dupla).
        // Inventar um número aqui seria adivinhar o preço que valia naquele dia.
        var dupla = Dupla(valor: null);

        AlteracaoDeImpedimento.Aplicar(dupla, Torneio(), TurnoDoImpedimento.SextaNoite,
            quemAlterou: 7, agora: DateTime.Now);

        Assert.Null(dupla.ValorInscricao);
    }

    [Fact]
    public void O_turno_que_a_dupla_tem_hoje_e_legivel_num_lugar_so()
    {
        Assert.Equal(TurnoDoImpedimento.Nenhum, AlteracaoDeImpedimento.TurnoAtual(Dupla()));
        Assert.Equal(TurnoDoImpedimento.SextaNoite, AlteracaoDeImpedimento.TurnoAtual(Dupla(sexta: true)));
        Assert.Equal(TurnoDoImpedimento.SabadoManha, AlteracaoDeImpedimento.TurnoAtual(Dupla(sabadoManha: true)));
    }
}
