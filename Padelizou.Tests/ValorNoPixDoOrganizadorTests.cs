using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUANTO MANDAR NO PIX.
//
// 🗣️ Pedido do Felipe, 01/09/2026: *"acho que pode ser interessante colocar o valor perto desse
// local aonde tem o pix do organizador do torneio"*.
//
// O card dava a chave e pedia o comprovante, mas não dizia o número que a pessoa tem que
// digitar no app do banco. Ela achava o preço no cabeçalho da página, lá em cima — e o preço
// do cabeçalho é POR PESSOA. Numa inscrição de dupla, quem lê "R$ 150,00" e paga 150 manda
// metade do que deve, e quem descobre isso é o organizador, contando comprovante na mão.
//
// ⚠️ Nenhuma conta nova de dinheiro nasce aqui: `PorPessoa` é o mesmo `PrecoInscricao` que o
// cabeçalho já mostra neste exato cenário (no "por fora" o `ViewBag.PrecoTotal` é nulo e a
// tela cai no campo), e `DaInscricao` só multiplica pelo `PessoasPorInscricao` que o próprio
// Torneio já expõe.
public class ValorNoPixDoOrganizadorTests
{
    private static Torneio PorFora(string formato = "Chave", decimal preco = 150m) => new()
    {
        Nome = "NATA PADEL TOUR",
        Codigo = "PIXVAL",
        FormaPagamento = FormaDePagamentoDoTorneio.Externo,
        ChavePixOrganizador = "51994643580",
        Formato = formato,
        PrecoInscricao = preco,
    };

    [Fact]
    public void O_valor_por_pessoa_e_o_mesmo_preco_anunciado_no_cabecalho()
    {
        // Se estes dois números divergirem, a página passa a dizer dois preços diferentes pro
        // mesmo torneio — e o de baixo, colado na chave Pix, é o que a pessoa vai pagar.
        var torneio = PorFora();

        Assert.Equal(torneio.PrecoInscricao, PixDoOrganizador.ValorPorPessoa(torneio));
    }

    [Fact]
    public void Na_inscricao_de_dupla_o_total_e_o_dobro()
    {
        // É o caso que motivou tudo: a dupla paga por duas pessoas.
        Assert.Equal(300m, PixDoOrganizador.ValorDaInscricao(PorFora()));
    }

    [Fact]
    public void No_americano_o_total_e_o_de_uma_pessoa_so()
    {
        // Americano é inscrição individual — mostrar "a dupla" ali seria cobrar em dobro.
        Assert.Equal(150m, PixDoOrganizador.ValorDaInscricao(PorFora(formato: "Americano")));
    }

    [Fact]
    public void Quando_a_inscricao_e_de_uma_pessoa_so_nao_ha_dois_valores_pra_mostrar()
    {
        // No Americano os dois números são o mesmo, e imprimir "R$ 150,00 por pessoa ·
        // R$ 150,00 a inscrição" só faz a pessoa procurar a diferença que não existe.
        Assert.False(PixDoOrganizador.ValePorPessoaEPorInscricao(PorFora(formato: "Americano")));
        Assert.True(PixDoOrganizador.ValePorPessoaEPorInscricao(PorFora()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void O_total_pode_mudar_quando_ha_impedimento_ou_segunda_categoria(
        bool cobraImpedimento, bool permiteVariasCategorias)
    {
        // ⚠️ O número do card é o preço BASE. Impedimento marcado soma, e a segunda categoria
        // do mesmo jogador pode custar menos (Services/PrecoDaInscricao). Prometer um total
        // exato nesses torneios seria mandar a pessoa pagar o valor errado no Pix — o card
        // avisa em vez de mentir.
        var torneio = PorFora();
        torneio.TaxaPorImpedimento = cobraImpedimento ? 20m : 0m;
        torneio.PermiteMultiplasCategorias = permiteVariasCategorias;

        Assert.True(PixDoOrganizador.OTotalPodeVariar(torneio));
    }

    [Fact]
    public void Sem_impedimento_pago_e_sem_segunda_categoria_o_valor_e_exato()
    {
        var torneio = PorFora();
        torneio.TaxaPorImpedimento = 0m;
        torneio.PermiteMultiplasCategorias = false;

        Assert.False(PixDoOrganizador.OTotalPodeVariar(torneio));
    }
}
