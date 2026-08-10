using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O convite de instalar o app deixou de aparecer no primeiro carregamento de página e passou a
// sair LOGO DEPOIS de a pessoa ganhar alguma coisa que gera aviso: conta criada, inscrição
// confirmada, dupla fechada, aula pedida. A diferença não é de texto — é de pergunta: quem
// acabou de se inscrever quer saber quando as chaves saem, e é isso que o aviso entrega.
//
// O que estes testes guardam:
//   * o motivo desconhecido NÃO convida (é o que chega de um TempData gravado pela versão
//     anterior do site no meio de um deploy, e um modal aparecendo do nada é pior que nenhum);
//   * quem NÃO conseguiu se inscrever não é convidado — parabenizar quem levou "não" é o
//     jeito mais rápido de o convite virar irritação.
public class ConviteNoMomentoDeValorTests
{
    [Fact]
    public void Motivo_desconhecido_nao_convida_ninguem()
    {
        Assert.Null(ConviteDeInstalarApp.Da(null));
        Assert.Null(ConviteDeInstalarApp.Da(""));
        Assert.Null(ConviteDeInstalarApp.Da("motivo-que-nao-existe-mais"));
    }

    [Fact]
    public void Cada_momento_tem_a_sua_conversa()
    {
        var motivos = new[]
        {
            ConviteDeInstalarApp.ContaCriada,
            ConviteDeInstalarApp.InscricaoConfirmada,
            ConviteDeInstalarApp.DuplaFechada,
            ConviteDeInstalarApp.AulaMarcada,
        };

        var conversas = motivos.Select(m => ConviteDeInstalarApp.Da(m)!).ToList();

        Assert.All(conversas, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Selo));
            Assert.False(string.IsNullOrWhiteSpace(c.Titulo));
            Assert.False(string.IsNullOrWhiteSpace(c.Texto));
        });

        // Selo repetido entre dois momentos é sinal de que um deles foi ligado ao motivo
        // errado — e aí a tela diz "CONTA CRIADA" pra quem acabou de marcar aula.
        Assert.Equal(conversas.Count, conversas.Select(c => c.Selo).Distinct().Count());

        // O argumento da inscrição é o sorteio; se ele sumir, o convite volta a falar de
        // atalho na tela inicial, que é justamente o que ninguém instala por causa.
        Assert.Contains("chaves", ConviteDeInstalarApp.Da(ConviteDeInstalarApp.InscricaoConfirmada)!.Titulo,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inscricao_confirmada_convida_a_instalar_o_app()
    {
        var (ctx, torneio, categoria, jogador) = MontarAmericanoAberto();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, jogador.Id);
        await controller.InscreverIndividual(torneio.Id, categoria.Id, jogador.Nome, jogador.Cpf);

        Assert.Equal(ConviteDeInstalarApp.InscricaoConfirmada,
            controller.TempData[ConviteDeInstalarApp.ChaveTempData]);
    }

    // A recusa não pode virar comemoração. Categoria de outro torneio é o caminho de erro mais
    // curto que existe aqui, e ele passa pelo mesmo método.
    [Fact]
    public async Task Inscricao_recusada_nao_convida_ninguem()
    {
        var (ctx, torneio, _, jogador) = MontarAmericanoAberto();
        using var _1 = ctx;

        var deOutroTorneio = new Torneio
        {
            Nome = "Outro", Codigo = "OUT", Status = "Inscrições Abertas", Formato = "Americano",
            DataInicio = new DateTime(2026, 9, 1, 19, 0, 0),
        };
        ctx.Torneios.Add(deOutroTorneio);
        var categoriaIntrusa = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = deOutroTorneio };
        ctx.Categorias.Add(categoriaIntrusa);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, jogador.Id);
        await controller.InscreverIndividual(torneio.Id, categoriaIntrusa.Id, jogador.Nome, jogador.Cpf);

        Assert.False(controller.TempData.ContainsKey(ConviteDeInstalarApp.ChaveTempData),
            "Uma inscrição RECUSADA deixou o convite de instalar o app no TempData. A tela "
            + "seguinte diz 'inscrição confirmada, receba o aviso das chaves' pra quem não "
            + "está inscrito em nada.");
    }

    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador jogador)
        MontarAmericanoAberto()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio
        {
            Nome = "Americano de teste", Codigo = "AMT", Status = "Inscrições Abertas",
            Formato = "Americano", QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 8, 20, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);

        var jogador = new Jogador { Nome = "Fulano Beltrano", Cpf = "52998224725" };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        return (ctx, torneio, categoria, jogador);
    }
}
