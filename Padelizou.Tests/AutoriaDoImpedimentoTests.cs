using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ "não consegue ver qual impedimento foi solicitado pelo usuário, e qual pelo organizador"
// (reclamação de usuário, 09/09/2026).
//
// O dado pra responder isso JÁ EXISTIA desde 02/09: `Dupla.ImpedimentoAlteradoPorId` guarda
// quem mexeu por último. Faltava a pergunta — a tela imprimia só a data ("alterado em"), que
// diz QUANDO e não POR QUEM.
//
// ⚠️ A REGRA É COMPARAR COM OS DONOS DA INSCRIÇÃO, e não "é organizador?": o organizador de um
// torneio pode estar inscrito nele (acontece o tempo todo em interno). Se ele mexe no impedimento
// da PRÓPRIA dupla, quem pediu foi o jogador — ele mesmo. Perguntar "essa pessoa organiza?"
// responderia "organizador" e mentiria na tela.
//
// A concentração não passa por aqui: ela é do organizador POR DEFINIÇÃO (o servidor não aceita
// de mais ninguém), então a tela diz isso sem precisar de coluna nenhuma.
public class AutoriaDoImpedimentoTests
{
    private static Dupla Dupla(int? alteradoPor, bool comImpedimento = true) => new()
    {
        Jogador1Id = 10,
        Jogador2Id = 11,
        ImpedimentoSextaNoite = comImpedimento,
        ImpedimentoAlteradoPorId = alteradoPor,
    };

    // NULO = veio da inscrição e nunca foi alterado. Quem preenche a inscrição é o jogador —
    // é o estado de toda dupla anterior à coluna de autoria, e a resposta certa pra elas.
    [Fact]
    public void Sem_registro_de_alteracao_o_pedido_e_do_jogador()
    {
        Assert.Equal(AutoriaDoImpedimento.Jogador, AutoriaDoImpedimentoDaDupla.De(Dupla(alteradoPor: null)));
    }

    [Fact]
    public void Mexido_por_quem_esta_na_dupla_e_do_jogador()
    {
        Assert.Equal(AutoriaDoImpedimento.Jogador, AutoriaDoImpedimentoDaDupla.De(Dupla(alteradoPor: 10)));
        Assert.Equal(AutoriaDoImpedimento.Jogador, AutoriaDoImpedimentoDaDupla.De(Dupla(alteradoPor: 11)));
    }

    [Fact]
    public void Mexido_por_alguem_de_fora_da_dupla_e_do_organizador()
    {
        Assert.Equal(AutoriaDoImpedimento.Organizador, AutoriaDoImpedimentoDaDupla.De(Dupla(alteradoPor: 99)));
    }

    // Sem impedimento nenhum não há autoria a mostrar — a tela não deve escrever "pedido pelo
    // jogador" ao lado de "sem impedimento".
    [Fact]
    public void Sem_impedimento_nao_ha_autoria()
    {
        Assert.Equal(AutoriaDoImpedimento.Ninguem,
            AutoriaDoImpedimentoDaDupla.De(Dupla(alteradoPor: 99, comImpedimento: false)));
    }

    [Theory]
    [InlineData(AutoriaDoImpedimento.Jogador, "jogador")]
    [InlineData(AutoriaDoImpedimento.Organizador, "organizador")]
    public void O_rotulo_diz_de_quem_foi(AutoriaDoImpedimento autoria, string esperado)
    {
        Assert.Contains(esperado, AutoriaDoImpedimentoDaDupla.Rotulo(autoria), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sem_autoria_o_rotulo_e_vazio()
    {
        Assert.Equal("", AutoriaDoImpedimentoDaDupla.Rotulo(AutoriaDoImpedimento.Ninguem));
    }

    // ⚠️ O CASO QUE FAZ A REGRA SER "É DA DUPLA?" E NÃO "É ORGANIZADOR?": o organizador jogando
    // o próprio torneio, mexendo no próprio impedimento. Quem pediu foi ele COMO JOGADOR.
    [Fact]
    public void Organizador_que_joga_mexendo_na_propria_dupla_conta_como_jogador()
    {
        var doOrganizador = new Dupla
        {
            Jogador1Id = 7, Jogador2Id = 8,
            ImpedimentoSabadoTarde = true,
            ImpedimentoAlteradoPorId = 7,   // ele mesmo, que também organiza
        };

        Assert.Equal(AutoriaDoImpedimento.Jogador, AutoriaDoImpedimentoDaDupla.De(doOrganizador));
    }
}
