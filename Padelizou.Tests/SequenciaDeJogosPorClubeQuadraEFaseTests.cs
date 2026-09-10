using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// A SEQUÊNCIA DE JOGOS POR CLUBE, POR QUADRA E POR FASE (10/09/2026).
//
// 🗣️ Felipe, num print da aba Jogos do 2º Etapa ER Padel Tour (88 jogos em dois clubes):
// *"Crie uma função aqui, para poder [ver] a sequencia de jogos, Por clube, por quadra, por
// fase (quartas, semi, etc)"*. A lista tinha filtro de time e de categoria; quem está no
// balcão do Radar quer só o que acontece no Radar, quem cuida da Arena 1 quer a fila DELA, e
// quem acompanha o mata-mata quer só as quartas.
//
// A régua mora em Services/FiltroDeJogos e vale pros DOIS tipos de linha da aba: o jogo real
// (Partida) e a prévia (JogoQueVem). O CLUBE de um jogo é o mesmo que a etiqueta da linha
// escreve (Services/LugarDoJogo.ClubeDoJogo): filtrar por "Radar" e ver uma linha etiquetada
// "Er Padel" seria a tela discordando de si mesma.
public class SequenciaDeJogosPorClubeQuadraEFaseTests
{
    private const int ErPadel = 10;
    private const int Radar = 20;

    private static SedesDoTorneio DoisClubes() => SedesDoTorneio.Montar(
        clubePrincipalId: ErPadel,
        minutosParaTrocarDeClube: 30,
        quadras: new[]
        {
            new Quadra { Nome = "Arena 1" },
            new Quadra { Nome = "Quadra Radar", ClubeId = Radar },
        },
        categorias: new[]
        {
            new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "C3M", PodeJogarNaSedeExtra = false },
            new Categoria { Id = 6, Nome = "6ª Feminina", Codigo = "C6F" },
        },
        nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel", [Radar] = "Radar Esportes" });

    private static SedesDoTorneio UmClubeSo() => SedesDoTorneio.Montar(
        clubePrincipalId: ErPadel,
        minutosParaTrocarDeClube: 0,
        quadras: new[] { new Quadra { Nome = "Arena 1" }, new Quadra { Nome = "Arena 2" } },
        categorias: Array.Empty<Categoria>(),
        nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel" });

    private static Partida Jogo(string? quadra, string fase, int categoriaId = 6, int? clube = null) => new()
    {
        Codigo = "X",
        Status = "Agendada",
        NomeQuadra = quadra,
        Fase = fase,
        CategoriaId = categoriaId,
        ClubeId = clube,
    };

    // ── A RÉGUA, SEM BANCO ───────────────────────────────────────────────────────────────

    [Fact]
    public void Por_quadra_so_passa_o_jogo_daquela_quadra_sem_ligar_pra_caixa_nem_espaco()
    {
        var filtro = new FiltroDeJogos(Quadra: " arena 1 ");
        var sedes = DoisClubes();

        Assert.True(filtro.Aceita(sedes, Jogo("Arena 1", "Grupo A")));
        Assert.False(filtro.Aceita(sedes, Jogo("Quadra Radar", "Grupo A")));
        // Jogo por ordem (sem quadra) não está em quadra nenhuma.
        Assert.False(filtro.Aceita(sedes, Jogo(null, "Grupo A")));
    }

    [Fact]
    public void Por_fase_de_grupos_passam_todos_os_grupos_e_nenhum_mata_mata()
    {
        // "Grupo A", "Grupo B" e o "Fase de Grupos" dos seeds antigos são a MESMA fase pra quem
        // filtra — ver Services/FasesTorneio.
        var filtro = new FiltroDeJogos(Fase: FasesTorneio.FaseDeGrupos);
        var sedes = DoisClubes();

        Assert.True(filtro.Aceita(sedes, Jogo("Arena 1", "Grupo A")));
        Assert.True(filtro.Aceita(sedes, Jogo("Arena 1", "Grupo B")));
        Assert.True(filtro.Aceita(sedes, Jogo("Arena 1", FasesTorneio.FaseDeGrupos)));
        Assert.False(filtro.Aceita(sedes, Jogo("Arena 1", "Semifinal")));
    }

    [Fact]
    public void Por_fase_de_mata_mata_passa_so_aquela_fase()
    {
        var filtro = new FiltroDeJogos(Fase: "Semifinal");
        var sedes = DoisClubes();

        Assert.True(filtro.Aceita(sedes, Jogo("Arena 1", "Semifinal")));
        Assert.False(filtro.Aceita(sedes, Jogo("Arena 1", "Quartas de Final")));
        Assert.False(filtro.Aceita(sedes, Jogo("Arena 1", "Grupo A")));
    }

    // A precedência é a da etiqueta (Services/LugarDoJogo): a quadra manda; sem quadra, o
    // carimbo do motor (Partida.ClubeId); sem os dois, o que a categoria já determina.
    [Fact]
    public void Por_clube_a_quadra_manda_depois_o_carimbo_depois_a_categoria()
    {
        var radar = new FiltroDeJogos(ClubeId: Radar);
        var er = new FiltroDeJogos(ClubeId: ErPadel);
        var sedes = DoisClubes();

        Assert.True(radar.Aceita(sedes, Jogo("Quadra Radar", "Grupo A")));
        // A quadra vence o carimbo: a Mesa chamou o jogo em outra quadra, e é lá que ele é.
        Assert.False(radar.Aceita(sedes, Jogo("Arena 1", "Grupo A", clube: Radar)));
        Assert.True(er.Aceita(sedes, Jogo("Arena 1", "Grupo A", clube: Radar)));
        // Sem quadra (por ordem), o carimbo responde.
        Assert.True(radar.Aceita(sedes, Jogo(null, "Grupo A", clube: Radar)));
        // Sem quadra nem carimbo, a categoria tirada do externo é do clube principal.
        Assert.True(er.Aceita(sedes, Jogo(null, "Grupo A", categoriaId: 3)));
        Assert.False(radar.Aceita(sedes, Jogo(null, "Grupo A", categoriaId: 3)));
        // A categoria livre sem nada não é de clube nenhum — o filtro não chuta.
        Assert.False(radar.Aceita(sedes, Jogo(null, "Grupo A", categoriaId: 6)));
        Assert.False(er.Aceita(sedes, Jogo(null, "Grupo A", categoriaId: 6)));
    }

    [Fact]
    public void A_previa_segue_a_mesma_regua_do_jogo_real()
    {
        var sedes = DoisClubes();
        var previa = new JogoQueVem("6ª Feminina", "Final", 1, null,
            new ProximasFasesDaChave.Lado("Vencedor Semifinal 1"),
            new ProximasFasesDaChave.Lado("Vencedor Semifinal 2"),
            Quadra: "Quadra Radar", CategoriaId: 6);

        Assert.True(new FiltroDeJogos(ClubeId: Radar).Aceita(sedes, previa));
        Assert.False(new FiltroDeJogos(ClubeId: ErPadel).Aceita(sedes, previa));
        Assert.True(new FiltroDeJogos(Quadra: "Quadra Radar").Aceita(sedes, previa));
        Assert.False(new FiltroDeJogos(Quadra: "Arena 1").Aceita(sedes, previa));
        Assert.True(new FiltroDeJogos(Fase: "Final").Aceita(sedes, previa));
        Assert.False(new FiltroDeJogos(Fase: "Semifinal").Aceita(sedes, previa));
    }

    [Fact]
    public void Os_tres_filtros_juntos_exigem_os_tres()
    {
        var filtro = new FiltroDeJogos(ClubeId: Radar, Quadra: "Quadra Radar", Fase: "Semifinal");
        var sedes = DoisClubes();

        Assert.True(filtro.Aceita(sedes, Jogo("Quadra Radar", "Semifinal")));
        Assert.False(filtro.Aceita(sedes, Jogo("Quadra Radar", "Final")));
        Assert.False(filtro.Aceita(sedes, Jogo("Arena 1", "Semifinal")));
    }

    [Fact]
    public void Sem_filtro_tudo_passa_e_o_filtro_se_diz_inativo()
    {
        Assert.False(FiltroDeJogos.Nenhum.Ativo);
        // O select manda "" quando ninguém escolheu; espaço em branco também não é escolha.
        Assert.False(new FiltroDeJogos(Quadra: "  ", Fase: "").Ativo);
        Assert.True(new FiltroDeJogos(Fase: "Final").Ativo);

        Assert.True(FiltroDeJogos.Nenhum.Aceita(null, Jogo(null, "Grupo A")));
        Assert.True(new FiltroDeJogos(Quadra: "  ").Aceita(null, Jogo(null, "Grupo A")));
    }

    [Fact]
    public void As_fases_para_escolher_saem_na_ordem_do_torneio_com_os_grupos_juntos()
    {
        var fases = FiltroDeJogos.FasesParaEscolher(new[]
        {
            "Grupo B", "Final", "Grupo A", "Quartas de Final", "Semifinal", FasesTorneio.FaseDeGrupos,
            "Quartas de Final",
        });

        Assert.Equal(
            new[] { FasesTorneio.FaseDeGrupos, "Quartas de Final", "Semifinal", "Final" },
            fases.Select(f => f.Valor));
        Assert.Equal("Fase de grupos", fases[0].Rotulo);
        Assert.Equal("Quartas de Final", fases[1].Rotulo);
    }

    // ── O CLUBE DO FILTRO É O CLUBE DA ETIQUETA ──────────────────────────────────────────

    [Fact]
    public void O_clube_do_jogo_segue_a_precedencia_da_etiqueta()
    {
        var sedes = DoisClubes();

        Assert.Equal(ErPadel, LugarDoJogo.ClubeDoJogo(sedes, "Arena 1", 6));
        // A quadra vence a categoria presa em casa.
        Assert.Equal(Radar, LugarDoJogo.ClubeDoJogo(sedes, "Quadra Radar", 3));
        // Nome de quadra fora do cadastro: a etiqueta mostra só a quadra, e o filtro não chuta.
        Assert.Null(LugarDoJogo.ClubeDoJogo(sedes, "Inventada", 6, Radar));
        Assert.Equal(ErPadel, LugarDoJogo.ClubeDoJogo(sedes, null, 3));
        Assert.Equal(Radar, LugarDoJogo.ClubeDoJogo(sedes, null, 6, Radar));
        Assert.Null(LugarDoJogo.ClubeDoJogo(sedes, null, 6));

        // Um clube só: toda quadra é dele, inclusive a que a Mesa escreveu à mão.
        Assert.Equal(ErPadel, LugarDoJogo.ClubeDoJogo(UmClubeSo(), "Inventada", 6));
        Assert.Equal(ErPadel, LugarDoJogo.ClubeDoJogo(UmClubeSo(), null, 6));

        Assert.Null(LugarDoJogo.ClubeDoJogo(null, "Arena 1", 6, Radar));
    }

    [Fact]
    public void A_lista_de_clubes_vem_com_o_principal_primeiro()
    {
        Assert.Equal(new[] { (ErPadel, "Er Padel"), (Radar, "Radar Esportes") }, DoisClubes().Clubes);
        Assert.Equal(new[] { (ErPadel, "Er Padel") }, UmClubeSo().Clubes);
        Assert.Empty(SedesDoTorneio.Nenhuma.Clubes);
    }

    // ── A TELA: DA URL ATÉ A LISTA ───────────────────────────────────────────────────────
    //
    // O torneio do print, encolhido: dois clubes (Er Padel é o do torneio; a Quadra Radar é do
    // Radar), duas semifinais da 4ª Masculina — uma em cada clube — e dois jogos de grupo da
    // 6ª Feminina: um na Arena 1 e um POR ORDEM, sem quadra, carimbado no Radar pelo motor.
    // A Final da 4ª ainda não existe: é prévia.
    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Clube Er, Clube Radar,
        Partida SemiNoEr, Partida SemiNoRadar, Partida GrupoNoEr, Partida GrupoNoRadarSemQuadra);

    private static async Task<Cenario> TorneioDoErAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var er = new Clube { Nome = "Er Padel" };
        var radar = new Clube { Nome = "Radar Esportes" };
        ctx.Clubes.AddRange(er, radar);
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);
        await ctx.SaveChangesAsync();

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER Padel Tour",
            Codigo = "ERPT2",
            Status = "Em Andamento",
            DataInicio = DateTime.Today.AddHours(9),
            QuantidadeQuadras = 2,
            ClubeId = er.Id,
            TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);
        var masculina = new Categoria { Nome = "4ª Categoria Masculina", Codigo = "C4M", Torneio = torneio, ChaveDireta = true };
        var feminina = new Categoria { Nome = "6ª Categoria Feminina", Codigo = "C6F", Torneio = torneio };
        ctx.Categorias.AddRange(masculina, feminina);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Arena 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra Radar", ClubeId = radar.Id });

        int cpf = 1;
        Dupla NovaDupla(Categoria categoria)
        {
            var j1 = TestInfra.NovoJogador(cpf++);
            var j2 = TestInfra.NovoJogador(cpf++);
            ctx.Jogadores.AddRange(j1, j2);
            var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(dupla);
            return dupla;
        }

        Partida NovoJogo(Categoria categoria, string fase, string? quadra, int minutos, int? clube = null)
        {
            var jogo = new Partida
            {
                TorneioId = torneio.Id,
                Categoria = categoria,
                Dupla1 = NovaDupla(categoria),
                Dupla2 = NovaDupla(categoria),
                Codigo = $"J{cpf:000}",
                Status = "Agendada",
                Fase = fase,
                NomeQuadra = quadra,
                ClubeId = clube,
                HorarioPrevisto = torneio.DataInicio!.Value.AddMinutes(minutos),
            };
            ctx.Partidas.Add(jogo);
            return jogo;
        }

        var semiNoEr = NovoJogo(masculina, "Semifinal", "Arena 1", 0);
        var semiNoRadar = NovoJogo(masculina, "Semifinal", "Quadra Radar", 0);
        var grupoNoEr = NovoJogo(feminina, "Grupo A", "Arena 1", 50);
        var grupoNoRadarSemQuadra = NovoJogo(feminina, "Grupo A", null, 50, clube: radar.Id);
        await ctx.SaveChangesAsync();

        return new Cenario(ctx, torneio, organizador, er, radar, semiNoEr, semiNoRadar, grupoNoEr, grupoNoRadarSemQuadra);
    }

    private static async Task<TorneiosController> AbrirJogosAsync(Cenario c,
        int? clube = null, string? quadra = null, string? fase = null)
    {
        // Sem isto o teste mente: no InMemory as entidades montadas acima continuam rastreadas
        // e as navegações vêm preenchidas de graça (ver MeusJogosNaTelaTests).
        c.Ctx.ChangeTracker.Clear();

        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
        var resultado = await controller.Jogos(c.Torneio.Id, null, null, soMeusJogos: false,
            clubeFiltroId: clube, quadraFiltro: quadra, faseFiltro: fase);

        Assert.IsType<ViewResult>(resultado);
        return controller;
    }

    private static int[] Agendadas(TorneiosController controller) =>
        ((List<Partida>)controller.ViewBag.Agendadas).Select(p => p.Id).OrderBy(id => id).ToArray();

    private static List<JogoQueVem> Previas(TorneiosController controller) =>
        (List<JogoQueVem>)controller.ViewBag.JogosQueVem;

    [Fact]
    public async Task Sem_filtro_a_lista_e_a_de_sempre()
    {
        var c = await TorneioDoErAsync();

        var controller = await AbrirJogosAsync(c);

        Assert.Equal(4, Agendadas(controller).Length);
        Assert.Single(Previas(controller), j => j.Fase == "Final");
    }

    [Fact]
    public async Task Por_quadra_a_lista_so_traz_a_fila_daquela_quadra()
    {
        var c = await TorneioDoErAsync();

        var controller = await AbrirJogosAsync(c, quadra: "Quadra Radar");

        Assert.Equal(new[] { c.SemiNoRadar.Id }, Agendadas(controller));
    }

    [Fact]
    public async Task Por_clube_entra_o_jogo_por_ordem_que_o_motor_carimbou_naquele_clube()
    {
        var c = await TorneioDoErAsync();

        var noRadar = await AbrirJogosAsync(c, clube: c.Radar.Id);
        Assert.Equal(new[] { c.SemiNoRadar.Id, c.GrupoNoRadarSemQuadra.Id }.OrderBy(id => id), Agendadas(noRadar));

        var noEr = await AbrirJogosAsync(c, clube: c.Er.Id);
        Assert.Equal(new[] { c.SemiNoEr.Id, c.GrupoNoEr.Id }.OrderBy(id => id), Agendadas(noEr));
    }

    [Fact]
    public async Task Por_fase_a_previa_tambem_obedece()
    {
        var c = await TorneioDoErAsync();

        var semis = await AbrirJogosAsync(c, fase: "Semifinal");
        Assert.Equal(new[] { c.SemiNoEr.Id, c.SemiNoRadar.Id }.OrderBy(id => id), Agendadas(semis));
        Assert.Empty(Previas(semis));

        var final = await AbrirJogosAsync(c, fase: "Final");
        Assert.Empty(Agendadas(final));
        Assert.Single(Previas(final), j => j.Fase == "Final");

        var grupos = await AbrirJogosAsync(c, fase: FasesTorneio.FaseDeGrupos);
        Assert.Equal(new[] { c.GrupoNoEr.Id, c.GrupoNoRadarSemQuadra.Id }.OrderBy(id => id), Agendadas(grupos));
        Assert.Empty(Previas(grupos));
    }

    // "Semifinal 2" continua sendo a Semifinal 2 quando a Semifinal 1 sumiu da tela pelo
    // filtro: o número é o que a prévia cita ("Vencedor Semifinal 2"), e renumerar a lista
    // filtrada apontaria a referência pro jogo errado.
    [Fact]
    public async Task O_numero_do_jogo_na_fase_nao_muda_com_o_filtro()
    {
        var c = await TorneioDoErAsync();

        var controller = await AbrirJogosAsync(c, quadra: "Quadra Radar");

        var numeros = (Dictionary<int, int>)controller.ViewBag.NumeroNaFase;
        Assert.Equal(2, numeros[c.SemiNoRadar.Id]);
    }

    [Fact]
    public async Task A_tela_oferece_os_clubes_e_as_fases_deste_torneio_e_lembra_a_escolha()
    {
        var c = await TorneioDoErAsync();

        var controller = await AbrirJogosAsync(c, clube: c.Radar.Id, fase: "Final");

        var clubes = (IReadOnlyList<(int Id, string Nome)>)controller.ViewBag.ClubesDoTorneio;
        Assert.Equal(new[] { (c.Er.Id, "Er Padel"), (c.Radar.Id, "Radar Esportes") }, clubes);

        var fases = (IReadOnlyList<(string Valor, string Rotulo)>)controller.ViewBag.FasesDoTorneio;
        Assert.Equal(new[] { FasesTorneio.FaseDeGrupos, "Semifinal", "Final" }, fases.Select(f => f.Valor));

        var filtro = (FiltroDeJogos)controller.ViewBag.FiltroDeJogos;
        Assert.Equal(c.Radar.Id, filtro.ClubeId);
        Assert.Equal("Final", filtro.Fase);
        Assert.True(filtro.Ativo);
    }

    // A aba Jogos embutida na página do torneio é a OUTRA porta da mesma lista — e é a que o
    // print mostra. As duas precisam obedecer ao mesmo filtro.
    [Fact]
    public async Task A_pagina_do_torneio_obedece_ao_mesmo_filtro()
    {
        var c = await TorneioDoErAsync();
        c.Ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);

        var resultado = await controller.Details(c.Torneio.Id, null, null, soMeusJogos: false,
            clubeFiltroId: null, quadraFiltro: "Quadra Radar", faseFiltro: null);

        Assert.IsType<ViewResult>(resultado);
        Assert.Equal(new[] { c.SemiNoRadar.Id }, Agendadas(controller));
    }

    // ── A TELA DE VERDADE: os selects existem e falam o nome que a ação espera ───────────
    //
    // A suíte não renderiza Razor; é teste de FONTE, como os outros da pasta. Sem ele, um select
    // com `name` errado passa verde e o filtro simplesmente não filtra.
    [Fact]
    public void Os_tres_filtros_estao_na_tela_e_o_meus_jogos_leva_os_tres_junto()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        Assert.Contains("name=\"clubeFiltroId\"", fonte);
        Assert.Contains("name=\"quadraFiltro\"", fonte);
        Assert.Contains("name=\"faseFiltro\"", fonte);

        // O link "Meus jogos" alterna sem perder o resto do filtro.
        Assert.Contains("asp-route-clubeFiltroId", fonte);
        Assert.Contains("asp-route-quadraFiltro", fonte);
        Assert.Contains("asp-route-faseFiltro", fonte);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        while (pasta != null)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
