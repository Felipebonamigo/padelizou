using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A TABELA DE QUADRAS DO PLANEJADOR (09/09/2026).
//
// 🗣️ Felipe: *"adicionar aqui nessa tela uma ou mais quadras, para calcular corretamente, crie
// uma tabela também, para que possa controlar as quadras que estarão disponíveis, se são no
// mesmo clube ou não, e quais horários elas irão receber (de que horas até que horas, cada
// quadra)"*. E, perguntado, escolheu: **o planejador vira o lugar único de quadra** — nome,
// local e janela editados aqui, e a quantidade vira o número de linhas.
//
// ⚠️ TRÊS INVARIANTES que estas guardas seguram, e cada uma já foi bug de produção:
//
//   1. `QuantidadeQuadras` == número de quadras cadastradas, sempre. Quando divergem, a grade
//      oferece vaga que não tem nome e o jogo nasce COM HORA E SEM QUADRA (Interno de
//      05/08/2026; Services/NomesDeQuadra existe por causa disso).
//   2. Nome é identidade (constraint UQ_Quadra_Torneio_Nome; Services/NomeDeQuadraUnico):
//      `Partida.NomeQuadra` é texto solto, e duas "Quadra 1" seriam a MESMA quadra pra grade.
//   3. Quadra com jogo marcado não some: o jogo apontaria pra um nome que não existe mais, e
//      o seletor "mudar de quadra" não teria como trazê-lo de volta.
public class QuadrasNoPlanejadorTests
{
    private static readonly DateTime Sabado = new(2026, 9, 12);

    private static (Torneio torneio, Jogador organizador, Jogador estranho, Clube alugado) Cenario(DbPadelContext ctx)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "11122233344" };
        var doTorneio = new Clube { Nome = "ER Padel" };
        var alugado = new Clube { Nome = "Arena Alugada" };
        ctx.Jogadores.Add(estranho);
        ctx.Clubes.AddRange(doTorneio, alugado);
        ctx.SaveChanges();

        torneio.ClubeId = doTorneio.Id;
        torneio.QuantidadeQuadras = 2;
        torneio.DataInicio = new DateTime(2026, 9, 11);
        torneio.DataFim = new DateTime(2026, 9, 13);
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" },
            new Quadra { TorneioId = torneio.Id, Nome = "Quadra B" });
        ctx.SaveChanges();

        return (torneio, organizador, estranho, alugado);
    }

    private static List<Quadra> Quadras(DbPadelContext ctx, int torneioId) =>
        ctx.Quadras.Where(q => q.TorneioId == torneioId).OrderBy(q => q.Id).ToList();

    [Fact]
    public async Task Organizador_adiciona_quadra_com_local_e_janela_e_a_quantidade_acompanha()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, alugado) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        var resposta = await controller.SalvarQuadraDoPlanejamento(torneio.Id, quadraId: null,
            nome: " Alugada 1 ", clubeId: alugado.Id,
            de: Sabado.AddHours(8), ate: Sabado.AddHours(14));

        Assert.IsType<RedirectToActionResult>(resposta);

        var quadras = Quadras(ctx, torneio.Id);
        Assert.Equal(3, quadras.Count);

        var nova = quadras[2];
        Assert.Equal("Alugada 1", nova.Nome);          // sem os espaços das pontas
        Assert.Equal(alugado.Id, nova.ClubeId);
        Assert.Equal(Sabado.AddHours(8), nova.DisponivelDe);
        Assert.Equal(Sabado.AddHours(14), nova.DisponivelAte);

        // Invariante 1: a quantidade é a lista, não um número digitado à parte.
        Assert.Equal(3, (await ctx.Torneios.FindAsync(torneio.Id))!.QuantidadeQuadras);
    }

    // Quadra no PRÓPRIO clube do torneio guarda ClubeId nulo — é o que toda quadra de torneio
    // de uma sede só sempre foi, e é o que faz `SedesDoTorneio` continuar vendo uma sede só.
    [Fact]
    public async Task Quadra_no_clube_do_torneio_fica_sem_clube_gravado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.SalvarQuadraDoPlanejamento(torneio.Id, quadraId: null,
            nome: "Quadra C", clubeId: torneio.ClubeId, de: null, ate: null);

        Assert.Null(Quadras(ctx, torneio.Id)[2].ClubeId);
    }

    [Fact]
    public async Task Editar_muda_nome_local_e_janela_da_quadra_certa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, alugado) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        var b = Quadras(ctx, torneio.Id)[1];

        await controller.SalvarQuadraDoPlanejamento(torneio.Id, quadraId: b.Id,
            nome: "Quadra B (alugada)", clubeId: alugado.Id,
            de: Sabado.AddHours(8), ate: Sabado.AddHours(12));

        var quadras = Quadras(ctx, torneio.Id);
        Assert.Equal(2, quadras.Count);                       // editou, não criou
        Assert.Equal("Quadra A", quadras[0].Nome);            // a outra ficou quieta
        Assert.Equal("Quadra B (alugada)", quadras[1].Nome);
        Assert.Equal(alugado.Id, quadras[1].ClubeId);
        Assert.Equal(Sabado.AddHours(12), quadras[1].DisponivelAte);
    }

    // Invariante 2. A régua é a de Services/NomeDeQuadraUnico — "quadra a" e "Quadra A " são
    // a mesma quadra pra quem lê a tela.
    [Fact]
    public async Task Nome_repetido_e_recusado_sem_gravar_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.SalvarQuadraDoPlanejamento(torneio.Id, quadraId: null,
            nome: "quadra a ", clubeId: null, de: null, ate: null);

        Assert.Equal(2, Quadras(ctx, torneio.Id).Count);
        Assert.Equal(2, (await ctx.Torneios.FindAsync(torneio.Id))!.QuantidadeQuadras);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // Uma janela que fecha antes de abrir é quadra que nunca existe: a grade não marcaria
    // nada nela e ninguém saberia por quê. Recusar aqui é o único lugar em que dá pra dizer.
    [Theory]
    [InlineData(14, 8)]
    [InlineData(8, 8)]
    public async Task Janela_que_fecha_antes_de_abrir_e_recusada(int abreHora, int fechaHora)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.SalvarQuadraDoPlanejamento(torneio.Id, quadraId: null,
            nome: "Alugada", clubeId: null,
            de: Sabado.AddHours(abreHora), ate: Sabado.AddHours(fechaHora));

        Assert.Equal(2, Quadras(ctx, torneio.Id).Count);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Remover_quadra_ajusta_a_quantidade_e_leva_a_preferencia_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var b = Quadras(ctx, torneio.Id)[1];
        var categoria = ctx.Categorias.First(c => c.TorneioId == torneio.Id);
        ctx.QuadrasDaCategoria.Add(new QuadraDaCategoria { CategoriaId = categoria.Id, QuadraId = b.Id });
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.RemoverQuadraDoPlanejamento(torneio.Id, b.Id);

        Assert.Single(Quadras(ctx, torneio.Id));
        Assert.Equal(1, (await ctx.Torneios.FindAsync(torneio.Id))!.QuantidadeQuadras);

        // A preferência que apontava pra ela não fica órfã — órfã ela seria FK quebrada no
        // Postgres e, no InMemory dos testes, uma escolha apontando pro nada.
        Assert.False(await ctx.QuadrasDaCategoria.AnyAsync(p => p.QuadraId == b.Id));
    }

    // Invariante 3.
    [Fact]
    public async Task Quadra_com_jogo_marcado_nao_e_removida()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var b = Quadras(ctx, torneio.Id)[1];
        var duplas = ctx.Duplas.Take(2).ToList();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = duplas[0].CategoriaId, Codigo = "JG01",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Agendada",
            NomeQuadra = "quadra b",   // o jogo guarda texto; a comparação não pode ser sensível a caixa
        });
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.RemoverQuadraDoPlanejamento(torneio.Id, b.Id);

        Assert.Equal(2, Quadras(ctx, torneio.Id).Count);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // A última quadra não sai: torneio sem quadra nenhuma é grade sem vaga, e o Editar sempre
    // garantiu pelo menos uma (Math.Max(1, …)).
    [Fact]
    public async Task A_ultima_quadra_nao_pode_ser_removida()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var quadras = Quadras(ctx, torneio.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.RemoverQuadraDoPlanejamento(torneio.Id, quadras[0].Id);
        await controller.RemoverQuadraDoPlanejamento(torneio.Id, quadras[1].Id);

        Assert.Single(Quadras(ctx, torneio.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_mexe_em_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, estranho, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: estranho.Id);
        var a = Quadras(ctx, torneio.Id)[0];

        Assert.IsType<ForbidResult>(await controller.SalvarQuadraDoPlanejamento(
            torneio.Id, null, "Intrusa", null, null, null));
        Assert.IsType<ForbidResult>(await controller.RemoverQuadraDoPlanejamento(torneio.Id, a.Id));

        Assert.Equal(2, Quadras(ctx, torneio.Id).Count);
    }

    // ⚠️ A QUADRA DE OUTRO TORNEIO. O id vem do navegador; sem esta conferência, um POST
    // montado à mão renomearia (ou apagaria) a quadra do torneio de outro organizador.
    [Fact]
    public async Task Quadra_de_outro_torneio_nao_e_alcancada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (meu, organizador, _, _) = Cenario(ctx);
        var outro = new Torneio { Nome = "Outro", Codigo = "OUT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(outro);
        ctx.SaveChanges();
        var deleQuadra = new Quadra { TorneioId = outro.Id, Nome = "Dele" };
        ctx.Quadras.Add(deleQuadra);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.SalvarQuadraDoPlanejamento(meu.Id, deleQuadra.Id, "Minha agora", null, null, null);
        await controller.RemoverQuadraDoPlanejamento(meu.Id, deleQuadra.Id);

        var intacta = await ctx.Quadras.FindAsync(deleQuadra.Id);
        Assert.NotNull(intacta);
        Assert.Equal("Dele", intacta!.Nome);
    }

    // O planejador CONTA com as quadras de verdade: a alugada das 8h às 14h de sábado rende
    // só as rodadas dessa janela (a aritmética está em PlanejamentoDeQuadrasTests; aqui é a
    // porta entregando as quadras certas pra ela).
    [Fact]
    public async Task O_plano_usa_as_quadras_cadastradas_com_a_janela_de_cada_uma()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, alugado) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.SalvarQuadraDoPlanejamento(torneio.Id, null, "Alugada", alugado.Id,
            Sabado.AddHours(8), Sabado.AddHours(14));

        var vm = Assert.IsType<PlanejamentoDeQuadrasVM>(
            Assert.IsType<ViewResult>(await controller.Planejamento(torneio.Id)).Model);

        Assert.Equal(3, vm.Plano.Quadras);
        Assert.Equal(3, vm.Quadras.Count);
        // sábado: 20 rodadas × 2 + 8 da alugada
        Assert.Equal(48, vm.Plano.Dias.Single(d => d.Data == Sabado).Vagas);
    }

    // ⚠️ O EDITAR SEM OS CAMPOS DE QUADRA NÃO PODE APAGAR QUADRA. Os campos saíram da tela de
    // gestão (viraram link pro planejador), então o navegador passa a mandar o POST sem eles:
    // `quantidadeQuadras` chega 0 e `nomesQuadras` chega nulo. A reconciliação antiga fazia
    // `Math.Max(1, 0)` e APAGAVA todas as quadras menos uma — em silêncio, no meio de um
    // salvamento de preço ou de foto.
    [Fact]
    public async Task Editar_sem_os_campos_de_quadra_deixa_as_quadras_em_paz()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: 0, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        Assert.Equal(2, Quadras(ctx, torneio.Id).Count);
        Assert.Equal(2, (await ctx.Torneios.FindAsync(torneio.Id))!.QuantidadeQuadras);
    }
}
