using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O EDITOR DE SEDES MUDOU DE CASA (08/09/2026).
//
// 🗣️ Felipe: "move o editor de sedes pra aba nova". Ele saiu do formulário gigante de
// "Gerenciar Torneio" e virou um POST próprio na sub-aba "Quadras e sedes", ao lado da janela
// do local alugado, do transbordo e do "só um jogo por dupla lá" — que é onde o assunto mora.
//
// ⚠️ O QUE MUDOU JUNTO, E NÃO DAVA PRA NÃO MUDAR: no formulário antigo a quadra era endereçada
// por POSIÇÃO (o 3º campo de nome ↔ o 3º select de clube), o que só funcionava porque nome e
// clube viajavam no MESMO POST. Separados, duas abas abertas fariam as posições discordarem e
// o clube da quadra 3 iria parar na quadra 4 — calado. Agora é por **Id**, no mesmo formato
// "id:clube" que a categoria já usava.
//
// ⚠️ E A REGRESSÃO QUE MAIS IMPORTA está no fim deste arquivo: salvar "Gerenciar Torneio" não
// pode mais encostar em sede nenhuma. A trava velha (`sedesInformadas`) existia justamente pra
// isso, e ela saiu junto com o campo.
public class SedesNaAbaDeQuadrasTests
{
    [Fact]
    public async Task Organizador_diz_em_que_clube_cada_quadra_fica()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, m.Org.Id).AlterarSedesDoTorneio(
            m.Torneio.Id,
            clubesQuadras: new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            clubesCategorias: null,
            minutosParaTrocarDeClube: null);

        Assert.Equal(m.Alugado.Id, (await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
        // A que não foi citada volta pro clube do torneio — a tela manda uma linha por quadra,
        // então "não veio" quer dizer "é de casa", e não "não mexi".
        Assert.Null((await ctx.Quadras.FindAsync(m.Quadra1.Id))!.ClubeId);
    }

    [Fact]
    public async Task E_em_que_clube_cada_categoria_joga()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, m.Org.Id).AlterarSedesDoTorneio(
            m.Torneio.Id,
            clubesQuadras: new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            clubesCategorias: new[] { $"{m.Categoria.Id}:{m.Alugado.Id}" },
            minutosParaTrocarDeClube: null);

        Assert.Equal(m.Alugado.Id, (await ctx.Categorias.FindAsync(m.Categoria.Id))!.ClubeId);
    }

    [Fact]
    public async Task A_folga_pra_atravessar_a_cidade_vem_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, m.Org.Id).AlterarSedesDoTorneio(
            m.Torneio.Id,
            clubesQuadras: new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            clubesCategorias: null,
            minutosParaTrocarDeClube: 45);

        Assert.Equal(45, (await ctx.Torneios.FindAsync(m.Torneio.Id))!.MinutosParaTrocarDeClube);
    }

    // ⚠️ A MESMA RECUSA QUE O FORMULÁRIO ANTIGO TINHA (SedesDoTorneio.MotivoParaNaoSalvar):
    // categoria num clube sem quadra nenhuma jogaria em qualquer lugar, e a escolha não valeria
    // de nada. E a recusa é ANTES de gravar qualquer coisa — meio salvo é pior que nada salvo.
    [Fact]
    public async Task Categoria_num_clube_sem_quadra_e_recusada_sem_gravar_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, m.Org.Id).AlterarSedesDoTorneio(
            m.Torneio.Id,
            clubesQuadras: new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            clubesCategorias: new[] { $"{m.Categoria.Id}:{m.SemQuadra.Id}" },
            minutosParaTrocarDeClube: null);

        Assert.Null((await ctx.Categorias.FindAsync(m.Categoria.Id))!.ClubeId);
        Assert.Null((await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
    }

    // ⚠️ O valor vem do navegador. Sem conferir contra ESTE torneio, um POST montado à mão
    // mudaria a quadra de outro organizador de clube — e a grade dele obedeceria no sorteio
    // seguinte.
    [Fact]
    public async Task Quadra_de_outro_torneio_no_POST_e_ignorada()
    {
        using var ctx = TestInfra.NovoContexto();
        var doOutro = Montar(ctx);
        var meu = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, meu.Org.Id).AlterarSedesDoTorneio(
            meu.Torneio.Id,
            clubesQuadras: new[] { $"{doOutro.Quadra2.Id}:{meu.Alugado.Id}" },
            clubesCategorias: null,
            minutosParaTrocarDeClube: null);

        Assert.Null((await ctx.Quadras.FindAsync(doOutro.Quadra2.Id))!.ClubeId);
    }

    [Fact]
    public async Task Categoria_de_outro_torneio_no_POST_e_ignorada()
    {
        using var ctx = TestInfra.NovoContexto();
        var doOutro = Montar(ctx);
        var meu = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, meu.Org.Id).AlterarSedesDoTorneio(
            meu.Torneio.Id,
            clubesQuadras: new[] { $"{meu.Quadra2.Id}:{meu.Alugado.Id}" },
            clubesCategorias: new[] { $"{doOutro.Categoria.Id}:{meu.Alugado.Id}" },
            minutosParaTrocarDeClube: null);

        Assert.Null((await ctx.Categorias.FindAsync(doOutro.Categoria.Id))!.ClubeId);
    }

    // ⚠️ O OUTRO LADO DO MESMO FILTRO, e é ELE que o torna carga: `clubeDaCategoria` alimenta a
    // validação de "categoria em clube sem quadra". Sem o filtro, uma categoria de OUTRO torneio
    // apontada pra um clube sem quadra AQUI faria a recusa disparar — e o organizador não
    // conseguiria salvar as próprias sedes por causa de um valor que nem é dele.
    [Fact]
    public async Task Categoria_de_outro_torneio_nao_bloqueia_o_salvamento_daqui()
    {
        using var ctx = TestInfra.NovoContexto();
        var doOutro = Montar(ctx);
        var meu = Montar(ctx);

        await TestInfra.NovoTorneiosController(ctx, meu.Org.Id).AlterarSedesDoTorneio(
            meu.Torneio.Id,
            clubesQuadras: new[] { $"{meu.Quadra2.Id}:{meu.Alugado.Id}" },
            clubesCategorias: new[] { $"{doOutro.Categoria.Id}:{meu.SemQuadra.Id}" },
            minutosParaTrocarDeClube: null);

        Assert.Equal(meu.Alugado.Id, (await ctx.Quadras.FindAsync(meu.Quadra2.Id))!.ClubeId);
    }

    [Fact]
    public async Task So_organizador_mexe_nas_sedes()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000033" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).AlterarSedesDoTorneio(
            m.Torneio.Id,
            clubesQuadras: new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            clubesCategorias: null,
            minutosParaTrocarDeClube: 99);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
    }

    // Voltar pra uma sede só: nenhuma quadra em clube nenhum, e a categoria solta junto.
    [Fact]
    public async Task Desfazer_as_sedes_solta_as_categorias_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, m.Org.Id);

        await controller.AlterarSedesDoTorneio(m.Torneio.Id,
            new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            new[] { $"{m.Categoria.Id}:{m.Alugado.Id}" }, null);

        await controller.AlterarSedesDoTorneio(m.Torneio.Id, clubesQuadras: null,
            clubesCategorias: null, minutosParaTrocarDeClube: null);

        Assert.Null((await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
        Assert.Null((await ctx.Categorias.FindAsync(m.Categoria.Id))!.ClubeId);
    }

    // ── A REGRESSÃO: "Gerenciar Torneio" não encosta mais em sede ──────────────────────────

    // ⚠️ ESTE É O TESTE QUE PAGA A MUDANÇA DE CASA. Enquanto o editor morava no formulário de
    // gestão, a marca `sedesInformadas` era o que impedia um POST sem os campos de apagar as
    // sedes de um torneio em andamento. Movido o editor, a marca saiu — e se o `Editar` ainda
    // escrevesse sede, salvar qualquer coisa na gestão (o nome do torneio, o preço) zeraria a
    // configuração inteira, calada, no meio do fim de semana.
    [Fact]
    public async Task Salvar_a_gestao_do_torneio_nao_mexe_em_sede_nenhuma()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, m.Org.Id);

        await controller.AlterarSedesDoTorneio(m.Torneio.Id,
            new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" },
            new[] { $"{m.Categoria.Id}:{m.Alugado.Id}" }, 40);

        await controller.Editar(
            id: m.Torneio.Id, nome: "Nome novo", localTorneio: null, dataInicio: m.Torneio.DataInicio,
            precoInscricao: 100, clubeId: m.Casa.Id,
            quantidadeQuadras: 2, nomesQuadras: new[] { "Casa 1", "Casa 2" },
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        Assert.Equal(m.Alugado.Id, (await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
        Assert.Equal(m.Alugado.Id, (await ctx.Categorias.FindAsync(m.Categoria.Id))!.ClubeId);
        Assert.Equal(40, (await ctx.Torneios.FindAsync(m.Torneio.Id))!.MinutosParaTrocarDeClube);
    }

    // Quadra NOVA criada pela gestão nasce sem clube = "no clube do torneio". É o certo: quem
    // acabou de criá-la ainda não disse onde ela fica, e chutar a sede alugada mandaria jogo
    // pra um lugar que o organizador não escolheu.
    [Fact]
    public async Task Quadra_nova_criada_na_gestao_nasce_no_clube_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var m = Montar(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, m.Org.Id);

        await controller.AlterarSedesDoTorneio(m.Torneio.Id,
            new[] { $"{m.Quadra2.Id}:{m.Alugado.Id}" }, null, null);

        await controller.Editar(
            id: m.Torneio.Id, nome: m.Torneio.Nome, localTorneio: null, dataInicio: m.Torneio.DataInicio,
            precoInscricao: 100, clubeId: m.Casa.Id,
            quantidadeQuadras: 3, nomesQuadras: new[] { "Casa 1", "Casa 2", "Casa 3" },
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        var terceira = await ctx.Quadras.FirstAsync(q => q.TorneioId == m.Torneio.Id && q.Nome == "Casa 3");
        Assert.Null(terceira.ClubeId);
        // E a que já estava no alugado continua lá.
        Assert.Equal(m.Alugado.Id, (await ctx.Quadras.FindAsync(m.Quadra2.Id))!.ClubeId);
    }

    private record Cenario(Torneio Torneio, Jogador Org, Categoria Categoria,
                           Quadra Quadra1, Quadra Quadra2, Clube Casa, Clube Alugado, Clube SemQuadra);

    private static Cenario Montar(DbPadelContext ctx)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var casa = new Clube { Nome = $"Casa {torneio.Id}" };
        var alugado = new Clube { Nome = $"Alugada {torneio.Id}" };
        var semQuadra = new Clube { Nome = $"Sem quadra {torneio.Id}" };
        ctx.Clubes.AddRange(casa, alugado, semQuadra);
        ctx.SaveChanges();

        torneio.ClubeId = casa.Id;
        torneio.QuantidadeQuadras = 2;

        var q1 = new Quadra { TorneioId = torneio.Id, Nome = "Casa 1" };
        var q2 = new Quadra { TorneioId = torneio.Id, Nome = "Casa 2" };
        ctx.Quadras.AddRange(q1, q2);
        ctx.SaveChanges();

        return new Cenario(torneio, org, categoria, q1, q2, casa, alugado, semQuadra);
    }
}
