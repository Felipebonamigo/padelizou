using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Tests;

// Home nova: visitante vê o mapa, logado vê "hoje no seu padel".
public class HomeTests
{
    private static HomeController NovoHome(DbPadelContext ctx, int? usuarioLogadoId = null)
    {
        var controller = new HomeController(ctx, new EstatisticasService(ctx));
        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity()) // anônimo
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    private static async Task<HomeVM> Renderizar(DbPadelContext ctx, int? usuarioLogadoId = null)
    {
        var result = (ViewResult)await NovoHome(ctx, usuarioLogadoId).Index();
        return (HomeVM)result.Model!;
    }

    private static Categoria NovaCategoria(DbPadelContext ctx, string nomeTorneio, string status, bool oculto = false)
    {
        var torneio = new Torneio { Nome = nomeTorneio, Codigo = nomeTorneio, Status = status, Oculto = oculto, DataInicio = DateTime.Now };
        var cat = new Categoria { Nome = "2ª Masculina", Codigo = nomeTorneio + "C", Torneio = torneio };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();
        return cat;
    }

    [Fact]
    public async Task Visitante_nao_ve_dados_pessoais_e_torneio_oculto_fica_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        NovaCategoria(ctx, "Aberto Publico", "Inscrições Abertas");
        NovaCategoria(ctx, "Restrito Escondido", "Inscrições Abertas", oculto: true);
        NovaCategoria(ctx, "Copa Rolando", "Fase de Grupos");

        var vm = await Renderizar(ctx);

        Assert.Null(vm.PrimeiroNome);
        Assert.Null(vm.Onboarding);
        Assert.Null(vm.ProximoJogo);
        Assert.Single(vm.Abertos);
        Assert.Equal("Aberto Publico", vm.Abertos[0].Nome);   // o oculto não vaza
        Assert.Single(vm.EmAndamento);
        Assert.Equal("Copa Rolando", vm.EmAndamento[0].Nome);
    }

    [Fact]
    public async Task Logado_ve_proximo_jogo_com_hora_quadra_e_adversarios()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Eu Mesmo", Cpf = "1" };
        var parceiro = new Jogador { Nome = "Parceiro", Cpf = "2" };
        var rival1 = new Jogador { Nome = "Rival Um", Cpf = "3" };
        var rival2 = new Jogador { Nome = "Rival Dois", Cpf = "4" };
        ctx.Jogadores.AddRange(eu, parceiro, rival1, rival2);

        var cat = NovaCategoria(ctx, "Copa", "Fase de Grupos");
        var minhaDupla = new Dupla { CategoriaId = cat.Id, Jogador1Id = eu.Id, Jogador2Id = parceiro.Id };
        var rivalDupla = new Dupla { CategoriaId = cat.Id, Jogador1Id = rival1.Id, Jogador2Id = rival2.Id };
        ctx.Duplas.AddRange(minhaDupla, rivalDupla);
        ctx.SaveChanges();

        // Jogo de amanhã (deve aparecer) e outro depois de amanhã (não é "o próximo").
        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, TorneioId = cat.TorneioId, Codigo = "J1", Fase = "Grupo A",
            Dupla1Id = minhaDupla.Id, Dupla2Id = rivalDupla.Id, Status = "Agendada",
            HorarioPrevisto = DateTime.Now.AddDays(1), NomeQuadra = "Quadra 2",
        });
        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, TorneioId = cat.TorneioId, Codigo = "J2", Fase = "Grupo A",
            Dupla1Id = rivalDupla.Id, Dupla2Id = minhaDupla.Id, Status = "Agendada",
            HorarioPrevisto = DateTime.Now.AddDays(2), NomeQuadra = "Quadra 1",
        });
        ctx.SaveChanges();

        var vm = await Renderizar(ctx, eu.Id);

        Assert.Equal("Eu", vm.PrimeiroNome);
        Assert.NotNull(vm.ProximoJogo);
        Assert.Equal("Quadra 2", vm.ProximoJogo!.Quadra);
        Assert.Equal("Rival Um e Rival Dois", vm.ProximoJogo.Adversarios);

        // O torneio em que estou não repete na vitrine geral — aparece em "Seus torneios".
        Assert.Single(vm.MeusTorneios);
        Assert.Empty(vm.EmAndamento);
    }

    [Fact]
    public async Task Partida_finalizada_ou_sem_horario_nao_vira_proximo_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Eu", Cpf = "1" };
        var p2 = new Jogador { Nome = "P2", Cpf = "2" };
        var p3 = new Jogador { Nome = "P3", Cpf = "3" };
        var p4 = new Jogador { Nome = "P4", Cpf = "4" };
        ctx.Jogadores.AddRange(eu, p2, p3, p4);

        var cat = NovaCategoria(ctx, "Copa", "Fase de Grupos");
        var minha = new Dupla { CategoriaId = cat.Id, Jogador1Id = eu.Id, Jogador2Id = p2.Id };
        var outra = new Dupla { CategoriaId = cat.Id, Jogador1Id = p3.Id, Jogador2Id = p4.Id };
        ctx.Duplas.AddRange(minha, outra);
        ctx.SaveChanges();

        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, TorneioId = cat.TorneioId, Codigo = "F1", Fase = "Grupo A",
            Dupla1Id = minha.Id, Dupla2Id = outra.Id, Status = "Finalizada",
            HorarioPrevisto = DateTime.Now.AddHours(3), VencedorId = minha.Id,
        });
        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, TorneioId = cat.TorneioId, Codigo = "F2", Fase = "Grupo A",
            Dupla1Id = outra.Id, Dupla2Id = minha.Id, Status = "Agendada",
            HorarioPrevisto = null, // sem hora marcada ainda
        });
        ctx.SaveChanges();

        var vm = await Renderizar(ctx, eu.Id);

        Assert.Null(vm.ProximoJogo);
        Assert.Single(vm.MeusTorneios); // o torneio continua listado como meu
    }

    [Fact]
    public async Task Compromissos_ordenam_por_data_e_dupla_em_lista_de_espera_ganha_aviso()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Eu", Cpf = "1" };
        var p2 = new Jogador { Nome = "P2", Cpf = "2" };
        ctx.Jogadores.AddRange(eu, p2);

        var clube = new Clube { Nome = "Clube Bom", Contato = "54 9999-0000", Endereco = "Rua do Padel, 10" };
        ctx.Clubes.Add(clube);
        ctx.SaveChanges();
        var quadra = new QuadraClube { ClubeId = clube.Id, Nome = "Quadra Central" };
        ctx.QuadrasClube.Add(quadra);

        var cat = NovaCategoria(ctx, "Lotado", "Inscrições Abertas");
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = eu.Id, Jogador2Id = p2.Id, EmListaDeEspera = true });
        ctx.SaveChanges();

        // Duas reservas fora de ordem: a mais próxima tem que vir primeiro.
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            JogadorId = eu.Id, ClubeId = clube.Id, QuadraClubeId = quadra.Id,
            DataHora = DateTime.Now.AddDays(3), DuracaoMinutos = 60, Status = "Confirmada",
        });
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            JogadorId = eu.Id, ClubeId = clube.Id, QuadraClubeId = quadra.Id,
            DataHora = DateTime.Now.AddDays(1), DuracaoMinutos = 90, Status = "Confirmada",
        });
        ctx.SaveChanges();

        var vm = await Renderizar(ctx, eu.Id);

        Assert.Equal(2, vm.Compromissos.Count);
        Assert.True(vm.Compromissos[0].Data < vm.Compromissos[1].Data);
        Assert.True(vm.MeusTorneios.Single().ListaDeEspera);
    }
}
