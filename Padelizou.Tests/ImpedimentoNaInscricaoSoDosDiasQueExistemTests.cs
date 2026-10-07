using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A INSCRIÇÃO É ONDE O DINHEIRO ENTRA — e era onde o turno inexistente custava de verdade.
//
// A tela deixou de oferecer "Sexta à noite" num torneio que começa no sábado
// (Services/TurnosDoTorneio), mas a tela nunca é a regra: aba velha, aplicativo instalado com
// o JavaScript da abertura anterior e POST montado à mão continuam mandando o turno antigo. Se
// o `Create` aceitar, a dupla paga `TaxaPorImpedimento` por uma janela que
// `JanelasDeImpedimento.Da` não produz — e o sorteio a escala na sexta que ela "não podia".
public class ImpedimentoNaInscricaoSoDosDiasQueExistemTests
{
    private static readonly DateTime Sabado = new(2026, 10, 10);
    private static readonly DateTime Quinta = new(2026, 10, 8);

    private static async Task<(Torneio torneio, Categoria categoria, Jogador a, Jogador b)> MontarAsync(
        DbPadelContext ctx, DateTime inicio)
    {
        var torneio = new Torneio
        {
            Nome = "Torneio de Sábado", Codigo = "SAB1", Status = "Inscrições Abertas",
            DataInicio = inicio,
            PrecoInscricao = 100m,
            PermiteImpedimentos = true,
            PermiteImpedimentoQuintaNoite = true,
            PermiteImpedimentoSextaNoite = true,
            PermiteImpedimentoSabadoManha = true,
            PermiteImpedimentoSabadoTarde = true,
            TaxaPorImpedimento = 20m,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);

        // CPF com dígito verificador de verdade: o `Create` recusa CPF inválido antes de tudo,
        // e o teste passaria pelo motivo errado.
        var a = TestInfra.NovoJogador(81);
        a.Cpf = "11144477735"; a.Nome = "Andrey Souza";
        var b = TestInfra.NovoJogador(82);
        b.Cpf = "52998224725"; b.Nome = "Maickel Klein";
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();

        return (torneio, categoria, a, b);
    }

    [Fact]
    public async Task Torneio_de_sabado_nao_grava_impedimento_de_sexta_nem_cobra_por_ele()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, a, b) = await MontarAsync(ctx, Sabado);

        await TestInfra.NovoDuplasController(ctx, a.Id).Create(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: true, impSabadoManha: false, impSabadoTarde: false);

        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        Assert.False(dupla.ImpedimentoSextaNoite);
        Assert.Equal(TurnoDoImpedimento.Nenhum, AlteracaoDeImpedimento.TurnoAtual(dupla));
        // 2 pessoas × R$ 100, sem a taxa de um impedimento que o torneio não tem.
        Assert.Equal(200m, dupla.ValorInscricao);
    }

    [Fact]
    public async Task O_impedimento_que_o_torneio_TEM_continua_valendo_e_sendo_cobrado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, a, b) = await MontarAsync(ctx, Sabado);

        await TestInfra.NovoDuplasController(ctx, a.Id).Create(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: true, impSabadoTarde: false);

        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        Assert.True(dupla.ImpedimentoSabadoManha);
        Assert.Equal(220m, dupla.ValorInscricao);
    }

    [Fact]
    public async Task A_quinta_entra_na_conta_do_preco_como_os_outros_tres()
    {
        // ⚠️ DEFEITO SEPARADO, achado no mesmo lugar: a soma que congela o `ValorInscricao`
        // (DuplasController.Create) tinha só TRÊS parcelas — sexta, sábado de manhã e sábado à
        // tarde. Quem marcava "Quinta à noite" num torneio que começa na quinta levava a janela
        // de graça, e as outras três contas do sistema (ImpedimentosDa, ContarImpedimentos,
        // QuantoMudaOValor) sempre contaram os quatro: tirar o impedimento depois DERRUBAVA o
        // valor devido abaixo do preço da inscrição.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, a, b) = await MontarAsync(ctx, Quinta);

        await TestInfra.NovoDuplasController(ctx, a.Id).Create(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: true, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false);

        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        Assert.True(dupla.ImpedimentoQuintaNoite);
        Assert.Equal(220m, dupla.ValorInscricao);
    }
}
