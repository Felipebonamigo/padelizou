using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A TAXA DE IMPEDIMENTO É DA INSCRIÇÃO, NÃO DO JOGADOR.
//
// Felipe, 18/08/2026: "o valor a ser cobrado por impedimento, avisar q é pela dupla e nao por
// jogador (inclusive veja se a regra ja é assim, se não for, faça ser)".
//
// A regra JÁ era assim — em `PrecoDaInscricao.Total` a taxa entra FORA do laço que soma
// pessoa por pessoa, porque o que o impedimento consome é uma janela da grade do organizador,
// e a grade não sabe quantas pessoas tem a dupla. O que faltava era a TELA dizer isso: sem
// dizer, o organizador que digita R$ 20 supõe que vira R$ 40 na dupla e põe R$ 10.
//
// ⚠️ Estes testes existem pra que a regra continue sendo essa. Ela é fácil de perder numa
// refatoração: bastaria alguém mover a soma da taxa pra dentro do laço "pra ficar tudo
// junto", e o preço dobraria sem nenhum teste reclamar — porque não havia teste.
public class TaxaDeImpedimentoPorDuplaTests
{
    private static Torneio Torneio(decimal inscricao, decimal taxa) => new()
    {
        Nome = "Torneio do impedimento",
        Codigo = "TIMP",
        Status = "Inscrições Abertas",
        DataInicio = new DateTime(2026, 8, 21),
        PrecoInscricao = inscricao,
        TaxaPorImpedimento = taxa,
        PermiteImpedimentos = true,
    };

    // O caso do pedido: dupla, um impedimento, taxa de 20 → soma 20, e NÃO 40.
    [Fact]
    public void A_taxa_entra_uma_vez_por_dupla_e_nao_uma_por_jogador()
    {
        var torneio = Torneio(inscricao: 150m, taxa: 20m);

        var total = PrecoDaInscricao.Total(torneio, [false, false], impedimentos: 1);

        Assert.Equal(150m + 150m + 20m, total);
    }

    // A prova de que é da INSCRIÇÃO e não da pessoa: com uma pessoa só (dupla procurando
    // parceiro) a taxa é a mesma. Se ela fosse por jogador, aqui daria a metade.
    [Fact]
    public void Inscricao_de_um_so_jogador_paga_a_MESMA_taxa()
    {
        var torneio = Torneio(inscricao: 150m, taxa: 20m);

        var dupla = PrecoDaInscricao.Total(torneio, [false, false], impedimentos: 1);
        var sozinho = PrecoDaInscricao.Total(torneio, [false], impedimentos: 1);

        // O que sobra depois de tirar o preço das pessoas é a taxa — igual nos dois casos.
        Assert.Equal(20m, dupla - 300m);
        Assert.Equal(20m, sozinho - 150m);
    }

    [Fact]
    public void Sem_impedimento_nao_soma_nada()
    {
        var torneio = Torneio(inscricao: 150m, taxa: 20m);

        Assert.Equal(300m, PrecoDaInscricao.Total(torneio, [false, false], impedimentos: 0));
    }

    // ⚠️ O TETO REAL: a dupla marca no máximo UM impedimento (Services/ImpedimentoUnico), então
    // o que a tela promete — "este é o valor máximo que se soma" — só é verdade enquanto a
    // trava do "só um" estiver de pé. Este teste amarra as duas pontas: se alguém afrouxar o
    // ImpedimentoUnico, o texto da tela de criação passa a mentir sobre dinheiro.
    [Fact]
    public void A_dupla_nao_consegue_marcar_mais_de_um_impedimento()
    {
        var (quinta, sexta, sabadoManha, sabadoTarde) =
            ImpedimentoUnico.Apenas(quinta: true, sexta: true, sabadoManha: true, sabadoTarde: true);

        var marcados = new[] { quinta, sexta, sabadoManha, sabadoTarde }.Count(x => x);

        Assert.Equal(1, marcados);
    }

    // E o desconto de segunda categoria não pega na taxa: ela não é preço de pessoa.
    [Fact]
    public void Desconto_de_segunda_inscricao_nao_mexe_na_taxa()
    {
        var torneio = Torneio(inscricao: 150m, taxa: 20m);
        torneio.PermiteMultiplasCategorias = true;
        torneio.PrecoSegundaInscricao = 100m;

        var total = PrecoDaInscricao.Total(torneio, [true, true], impedimentos: 1);

        Assert.Equal(100m + 100m + 20m, total);
    }
}

// MUDAR O PREÇO COM O TORNEIO JÁ ABERTO — o aviso de que o valor antigo já valeu pra alguém.
public class AvisoDeMudancaDePrecoTests
{
    // Sem ninguém inscrito não há o que avisar: o preço ainda não valeu pra pessoa nenhuma, e
    // o aviso seria só ruído na tela de quem está terminando de configurar o torneio.
    [Fact]
    public void Sem_inscricao_nenhuma_nao_avisa()
    {
        Assert.Null(AvisoDeMudancaDePreco.Texto(0));
        Assert.Null(AvisoDeMudancaDePreco.Texto(-1));
    }

    [Fact]
    public void Com_uma_inscricao_o_texto_fica_no_singular()
    {
        var texto = AvisoDeMudancaDePreco.Texto(1);

        Assert.NotNull(texto);
        Assert.Contains("1 inscrição já foi feita", texto);
    }

    [Fact]
    public void Com_varias_o_texto_diz_quantas()
    {
        var texto = AvisoDeMudancaDePreco.Texto(9);

        Assert.NotNull(texto);
        Assert.Contains("9 inscrições já foram feitas", texto);
    }

    // ⚠️ O QUE O AVISO PRECISA DIZER, e é o contrário do que a pessoa supõe: mudar o preço
    // NÃO reescreve quem já entrou. Sem essa frase o organizador ou cobra a diferença de quem
    // já pagou, ou conta com dinheiro que não vem.
    [Fact]
    public void O_aviso_diz_que_vale_so_pra_quem_entrar_depois()
    {
        var texto = AvisoDeMudancaDePreco.Texto(3)!;

        Assert.Contains("daqui pra frente", texto);
        Assert.Contains("quem já entrou continua com o valor que combinou", texto);
    }
}
