using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;   // GrupoTorneio ficou no namespace legado, em minúsculo
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// O FILTRO DA ABA JOGOS NÃO PODE ESTRAGAR O RESTO DA PÁGINA (10/09/2026).
//
// Revisão adversarial do PR #128, minutos depois do merge. O filtro por clube/quadra/fase
// nasceu recortando `ViewBag.JogosQueVem` e a lista de agendados — e essas duas coisas são
// lidas por MAIS gente do que a lista de jogos:
//
// 🕳️ 1. A ABA CHAVES CASA A PRÉVIA COM A VAGA POR ÍNDICE (`daFase[i]`, Details.cshtml). Uma
//    lista recortada faz a Semifinal 2 prevista aparecer na vaga da Semifinal 1 — o quadro
//    mostrando confronto que não existe. ⚠️ Isto JÁ ACONTECIA com o "meus jogos", que recorta a
//    mesma lista desde 09/09; o filtro novo só tornou frequente o que era raro.
//
// 🕳️ 2. O AVISO VERMELHO DO "RECALCULAR HORÁRIOS" CONTA A LISTA DA TELA. Com o filtro no Radar
//    ele promete refazer "11 jogos" e refaz os 88 do torneio — um diálogo de ação destrutiva
//    mentindo sobre o tamanho do estrago, que é exatamente o que os PRs #123 e #125 foram
//    escritos pra evitar.
//
// 🕳️ 3. NO TORNEIO POR ORDEM (o do Er) TODO JOGO TEM QUADRA NULA — `OrdemDeLiberacao.ApagarAsQuadras`.
//    A lista de quadras cai no CADASTRO (`NomesDeQuadra.Disponiveis` com `nosJogos` vazio), então o
//    select aparecia com as três quadras e QUALQUER escolha esvaziava a tela. O filtro quebrado
//    justamente no torneio que o pedido do Felipe citava.
//
// 🕳️ 4. E ele mostrava essas quadras a VISITANTE com a chave ainda não aprovada — a lista de
//    quadras não passa pelo portão de `CarregarViewBagJogosAsync`. Regra 0.
//
// A correção é uma só: o que a TELA oferece e o que as OUTRAS abas leem sai da grade inteira do
// torneio, não da lista recortada.
public class FiltroDeJogosNaoAtrapalhaAsOutrasTelasTests
{
    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador,
        Clube Er, Clube Radar, Partida SemiNoEr, Partida SemiNoRadar, Partida GrupoNoEr);

    private static async Task<Cenario> TorneioAsync(bool porOrdem = false)
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
            SemHorarioPrevisto = porOrdem,
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
        Dupla NovaDupla(Categoria c)
        {
            var j1 = TestInfra.NovoJogador(cpf++);
            var j2 = TestInfra.NovoJogador(cpf++);
            ctx.Jogadores.AddRange(j1, j2);
            var d = new Dupla { Categoria = c, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(d);
            return d;
        }

        Partida NovoJogo(Categoria c, string fase, string? quadra, int minutos, int? clube = null)
        {
            var jogo = new Partida
            {
                TorneioId = torneio.Id,
                Categoria = c,
                Dupla1 = NovaDupla(c),
                Dupla2 = NovaDupla(c),
                Codigo = $"J{cpf:000}",
                Status = "Agendada",
                Fase = fase,
                // No "por ordem" a quadra é apagada pelo motor — todo jogo fica sem.
                NomeQuadra = porOrdem ? null : quadra,
                ClubeId = clube,
                HorarioPrevisto = torneio.DataInicio!.Value.AddMinutes(minutos),
            };
            ctx.Partidas.Add(jogo);
            return jogo;
        }

        var semiNoEr = NovoJogo(masculina, "Semifinal", "Arena 1", 0, clube: er.Id);
        var semiNoRadar = NovoJogo(masculina, "Semifinal", "Quadra Radar", 0, clube: radar.Id);
        var grupoNoEr = NovoJogo(feminina, "Grupo A", "Arena 1", 50, clube: er.Id);
        await ctx.SaveChangesAsync();

        return new Cenario(ctx, torneio, organizador, er, radar, semiNoEr, semiNoRadar, grupoNoEr);
    }

    private static async Task<TorneiosController> AbrirAsync(Cenario c, int? clube = null, string? quadra = null,
        string? fase = null, int? time = null, int[]? categorias = null)
    {
        c.Ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
        var r = await controller.Jogos(c.Torneio.Id, time, categorias, soMeusJogos: false,
            clubeFiltroId: clube, quadraFiltro: quadra, faseFiltro: fase);
        Assert.IsType<ViewResult>(r);
        return controller;
    }

    // ── 1. A ABA CHAVES LÊ A PROJEÇÃO INTEIRA ────────────────────────────────────────────
    [Fact]
    public async Task A_projecao_da_aba_chaves_nao_encolhe_com_o_filtro()
    {
        var c = await TorneioAsync();

        var semFiltro = await AbrirAsync(c);
        var completaSemFiltro = (List<JogoQueVem>)semFiltro.ViewBag.ProjecaoCompleta;
        Assert.NotEmpty(completaSemFiltro);

        // Por FASE, e não por clube: a prévia da Final tem UM clube só, e filtrar por ele
        // poderia mantê-la — o teste passaria sem provar nada. "Semifinal" a exclui sempre.
        var comFiltro = await AbrirAsync(c, fase: "Semifinal");

        // A lista da ABA JOGOS encolhe (é o que o filtro faz)...
        var naLista = (List<JogoQueVem>)comFiltro.ViewBag.JogosQueVem;
        Assert.Empty(naLista);
        Assert.NotEmpty(completaSemFiltro);

        // ...e a que a ABA CHAVES casa por índice, não.
        var completaComFiltro = (List<JogoQueVem>)comFiltro.ViewBag.ProjecaoCompleta;
        Assert.Equal(completaSemFiltro.Count, completaComFiltro.Count);
        Assert.Equal(completaSemFiltro.Select(j => j.FaseNumerada), completaComFiltro.Select(j => j.FaseNumerada));
    }

    // O "meus jogos" recorta a MESMA lista desde 09/09 — a aba Chaves já saía errada por ele.
    [Fact]
    public async Task A_projecao_da_aba_chaves_nao_encolhe_nem_com_meus_jogos()
    {
        var c = await TorneioAsync();
        var completa = (List<JogoQueVem>)(await AbrirAsync(c)).ViewBag.ProjecaoCompleta;

        c.Ctx.ChangeTracker.Clear();
        var euJogo = c.SemiNoEr.Dupla1.Jogador1Id;
        var controller = TestInfra.NovoTorneiosController(c.Ctx, euJogo);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null, soMeusJogos: true));

        Assert.Equal(completa.Count, ((List<JogoQueVem>)controller.ViewBag.ProjecaoCompleta).Count);
    }

    [Fact]
    public void A_aba_chaves_da_pagina_do_torneio_le_a_projecao_completa()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));
        // Do ponto em que a aba Chaves lê a projeção até o fim do arquivo. A âncora mudou em
        // 11/09/2026 (o quadro virou partial), o que ela guarda não.
        var chaves = fonte[fonte.IndexOf("var projecaoDaCategoria", StringComparison.Ordinal)..];

        // O quadro casa por índice: tem que ler a lista inteira, nunca a recortada.
        Assert.Contains("ProjecaoCompleta", fonte);
        Assert.DoesNotContain("ViewBag.JogosQueVem", chaves);
    }

    // ── 2. O AVISO DO RECALCULAR CONTA A GRADE INTEIRA ───────────────────────────────────
    [Fact]
    public async Task O_aviso_do_recalcular_conta_os_jogos_do_TORNEIO_e_nao_os_da_tela()
    {
        var c = await TorneioAsync();

        var comFiltro = await AbrirAsync(c, clube: c.Radar.Id);

        Assert.Single((List<Partida>)comFiltro.ViewBag.Agendadas);
        Assert.Equal(3, (int)comFiltro.ViewBag.AgendadasNoTorneio);
    }

    [Fact]
    public void O_texto_do_recalcular_usa_a_contagem_do_torneio()
    {
        // O botão saiu da lista de jogos em 10/09/2026 e foi pro Painel de Controle; a
        // contagem continua sendo a do TORNEIO, que é o que ele refaz.
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));
        var recalcular = fonte[fonte.IndexOf("action=\"RefazerGrade\"", StringComparison.Ordinal)..];
        var ateOFim = recalcular[..recalcular.IndexOf("</form>", StringComparison.Ordinal)];

        Assert.Contains("agendadasNoTorneio", ateOFim);
        Assert.DoesNotContain("@agendadasList.Count jogos", ateOFim);
    }

    // ── 3. POR ORDEM: NÃO EXISTE QUADRA PRA FILTRAR ──────────────────────────────────────
    [Fact]
    public async Task Torneio_por_ordem_nao_oferece_filtro_de_quadra()
    {
        var c = await TorneioAsync(porOrdem: true);

        var controller = await AbrirAsync(c);

        // O cadastro tem duas quadras, mas nenhum JOGO tem quadra: filtrar por ela só
        // esvaziaria a tela.
        Assert.Equal(2, ((List<string>)controller.ViewBag.QuadrasDoTorneio).Count);
        Assert.Empty((IReadOnlyList<string>)controller.ViewBag.QuadrasParaFiltrar);
    }

    [Fact]
    public async Task O_filtro_de_quadra_so_oferece_quadra_que_algum_jogo_usa()
    {
        var c = await TorneioAsync();

        var controller = await AbrirAsync(c);

        Assert.Equal(new[] { "Arena 1", "Quadra Radar" },
            ((IReadOnlyList<string>)controller.ViewBag.QuadrasParaFiltrar).OrderBy(q => q));
    }

    // ── 4. REGRA 0: A CHAVE NÃO APROVADA NÃO VAZA PELO SELECT ────────────────────────────
    [Fact]
    public async Task Com_a_chave_pendente_o_visitante_nao_ve_quadra_nem_fase_no_filtro()
    {
        var c = await TorneioAsync();
        c.Torneio.Status = AprovacaoDeChaves.Pendente;
        await c.Ctx.SaveChangesAsync();
        c.Ctx.ChangeTracker.Clear();

        // Visitante deslogado, pela porta que NÃO redireciona (a aba embutida no Details).
        var controller = TestInfra.NovoTorneiosController(c.Ctx, 0);
        Assert.IsType<ViewResult>(await controller.Details(c.Torneio.Id, null, null));

        Assert.Empty((IReadOnlyList<string>)controller.ViewBag.QuadrasParaFiltrar);
        Assert.Empty((IReadOnlyList<(string Valor, string Rotulo)>)controller.ViewBag.FasesDoTorneio);
        Assert.Equal(0, (int)controller.ViewBag.AgendadasNoTorneio);
    }

    // ── 5. O QUE A TELA OFERECE NÃO ENCOLHE COM OS OUTROS FILTROS ────────────────────────
    [Fact]
    public async Task As_fases_para_escolher_nao_somem_quando_uma_categoria_e_marcada()
    {
        var c = await TorneioAsync();
        var feminina = c.GrupoNoEr.CategoriaId;

        var controller = await AbrirAsync(c, categorias: new[] { feminina });

        // A categoria feminina só tem grupos, mas o select não pode perder "Semifinal" —
        // senão a escolha de fase que já estava ligada some sem ninguém desligar.
        var fases = ((IReadOnlyList<(string Valor, string Rotulo)>)controller.ViewBag.FasesDoTorneio)
            .Select(f => f.Valor).ToList();
        Assert.Contains("Semifinal", fases);
        Assert.Contains(FasesTorneio.FaseDeGrupos, fases);
    }

    // ── 6. O NÚMERO DO JOGO NA FASE NÃO DEPENDE DE FILTRO NENHUM ─────────────────────────
    [Fact]
    public async Task O_numero_do_jogo_na_fase_nao_muda_nem_com_o_filtro_de_time()
    {
        var c = await TorneioAsync();

        // Dá time só a quem joga a SEGUNDA semifinal — o filtro de time vira SQL e tira a
        // primeira antes mesmo de a lista existir.
        c.Ctx.ChangeTracker.Clear();
        var time = new Time { Nome = "Time do Radar" };
        c.Ctx.Times.Add(time);
        await c.Ctx.SaveChangesAsync();
        var segunda = await c.Ctx.Partidas.FindAsync(c.SemiNoRadar.Id);
        var dupla = await c.Ctx.Duplas.FindAsync(segunda!.Dupla1Id);
        var jogador = await c.Ctx.Jogadores.FindAsync(dupla!.Jogador1Id);
        jogador!.TimeId = time.Id;
        await c.Ctx.SaveChangesAsync();

        var controller = await AbrirAsync(c, time: time.Id);

        var numeros = (Dictionary<int, int>)controller.ViewBag.NumeroNaFase;
        Assert.Equal(2, numeros[c.SemiNoRadar.Id]);
    }

    // ── 7. AS RODADAS DO AMERICANO EM ORDEM DE NÚMERO ────────────────────────────────────
    [Fact]
    public void As_rodadas_do_americano_saem_em_ordem_numerica()
    {
        var fases = FiltroDeJogos.FasesParaEscolher(new[]
        {
            "Americano Rodada 10", "Americano Rodada 2", "Americano Rodada 1", "Americano Rodada 11",
        });

        Assert.Equal(
            new[] { "Americano Rodada 1", "Americano Rodada 2", "Americano Rodada 10", "Americano Rodada 11" },
            fases.Select(f => f.Valor));
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

    // Uma categoria de GRUPOS de verdade: quatro grupos de duas duplas, uma rodada marcada.
    // É o desenho da 4ª Masculina do Er — e o que faz a projeção ter o que montar.
    private static async Task<int> CategoriaDeGruposAsync(Cenario c, string nome)
    {
        var categoria = new Categoria { Nome = nome, Codigo = "CGRP", TorneioId = c.Torneio.Id, ClassificadosPorGrupo = 2 };
        c.Ctx.Categorias.Add(categoria);
        await c.Ctx.SaveChangesAsync();

        int cpf = 200;
        foreach (var letra in new[] { "A", "B", "C", "D" })
        {
            var grupo = new GrupoTorneio { Categoria = categoria, Nome = $"Grupo {letra}" };
            c.Ctx.Add(grupo);

            var duplas = new List<Dupla>();
            for (int i = 0; i < 2; i++)
            {
                var j1 = TestInfra.NovoJogador(cpf++);
                var j2 = TestInfra.NovoJogador(cpf++);
                c.Ctx.Jogadores.AddRange(j1, j2);
                var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2, GrupoTorneio = grupo, Grupo = letra };
                c.Ctx.Duplas.Add(dupla);
                duplas.Add(dupla);
            }

            c.Ctx.Partidas.Add(new Partida
            {
                TorneioId = c.Torneio.Id,
                Categoria = categoria,
                Dupla1 = duplas[0],
                Dupla2 = duplas[1],
                Codigo = $"G{letra}",
                Status = "Agendada",
                Fase = $"Grupo {letra}",
                NomeQuadra = "Arena 1",
                ClubeId = c.Er.Id,
                HorarioPrevisto = c.Torneio.DataInicio!.Value.AddMinutes(100),
            });
        }

        await c.Ctx.SaveChangesAsync();
        return categoria.Id;
    }

    // ── 8. O FILTRO DE CATEGORIA E A PRÉVIA (10/09/2026) ─────────────────────────────────
    //
    // 🗣️ Felipe, com "3ª Feminina" marcada na aba Jogos do Er: *"eu selecionei '3 feminina' e
    // esta aparecendo jogos de outras categorias no filtro"* — e o print mostrava três Oitavas
    // da 4ª Masculina, com o selo "prévia", no meio da lista.
    //
    // 🕳️ A CAUSA É MAIS FUNDA QUE O SINTOMA. O filtro de categoria (e o de time) vira SQL lá em
    // cima, então `todasAsPartidas` — o nome promete o torneio inteiro — chega à projeção JÁ
    // RECORTADA. Daí saem dois estragos: as cadeias são montadas a partir das CATEGORIAS do
    // banco (todas elas, o filtro não as alcança) e por isso a prévia da 4ª aparece; e o
    // horário de cada uma é calculado numa grade que só enxerga os jogos que passaram no
    // filtro, ou seja, com as quadras das outras categorias parecendo livres.

    [Fact]
    public async Task A_previa_obedece_ao_filtro_de_categoria()
    {
        // ⚠️ A CATEGORIA QUE VAZA É A DE GRUPOS, e é por isso que este teste monta uma. A
        // projeção parte das CATEGORIAS lidas do banco (`aindaEmGrupos`), que o filtro não
        // alcança — a 4ª Masculina do print é assim. A de chave direta, por acidente, some
        // junto com os jogos dela: sem mata-mata na lista filtrada, não há de onde partir.
        var c = await TorneioAsync();
        var feminina = c.GrupoNoEr.CategoriaId;
        var outra = await CategoriaDeGruposAsync(c, "5ª Categoria Masculina");

        var semFiltro = (List<JogoQueVem>)(await AbrirAsync(c)).ViewBag.JogosQueVem;
        Assert.Contains(semFiltro, j => j.CategoriaId == outra);

        var previas = (List<JogoQueVem>)(await AbrirAsync(c, categorias: new[] { feminina })).ViewBag.JogosQueVem;

        Assert.DoesNotContain(previas, j => j.CategoriaId == outra);
        Assert.All(previas, j => Assert.Equal(feminina, j.CategoriaId));
    }

    [Fact]
    public async Task A_previa_e_calculada_na_grade_INTEIRA_mesmo_com_o_filtro_ligado()
    {
        // As duas quadras das 9:50 estão tomadas por jogos da FEMININA, então a Final prevista
        // da masculina não cabe lá e vai pra 10:40. Filtrando pela masculina, a grade que a
        // projeção enxerga fica sem esses dois jogos — e a Final volta pras 9:50, um horário
        // que na vida real não existe. O horário da prévia não pode depender do que a tela
        // está mostrando.
        var c = await TorneioAsync();
        var masculina = c.SemiNoEr.CategoriaId;

        var j1 = TestInfra.NovoJogador(90);
        var j2 = TestInfra.NovoJogador(91);
        c.Ctx.Jogadores.AddRange(j1, j2);
        c.Ctx.Partidas.Add(new Partida
        {
            TorneioId = c.Torneio.Id,
            CategoriaId = c.GrupoNoEr.CategoriaId,
            Dupla1 = new Dupla { CategoriaId = c.GrupoNoEr.CategoriaId, Jogador1 = j1 },
            Dupla2 = new Dupla { CategoriaId = c.GrupoNoEr.CategoriaId, Jogador1 = j2 },
            Codigo = "JLOT",
            Status = "Agendada",
            Fase = "Grupo A",
            NomeQuadra = "Quadra Radar",
            ClubeId = c.Radar.Id,
            HorarioPrevisto = c.GrupoNoEr.HorarioPrevisto,
        });
        await c.Ctx.SaveChangesAsync();

        var semFiltro = (List<JogoQueVem>)(await AbrirAsync(c)).ViewBag.JogosQueVem;
        var comFiltro = (List<JogoQueVem>)(await AbrirAsync(c, categorias: new[] { masculina })).ViewBag.JogosQueVem;

        var finalSem = semFiltro.Single(j => j.CategoriaId == masculina && j.Fase == "Final");
        var finalCom = comFiltro.Single(j => j.CategoriaId == masculina && j.Fase == "Final");

        Assert.Equal(finalSem.Horario, finalCom.Horario);
    }

    [Fact]
    public async Task Os_horarios_que_o_definir_horario_oferece_contam_a_ocupacao_do_torneio_inteiro()
    {
        // Mesmo defeito, outra porta: a lista de horários do "Definir horário" (10/09/2026) diz
        // quantas quadras sobram em cada um. Contando só os jogos que passaram no filtro, ela
        // ofereceria como livre um horário que está lotado — e o servidor recusaria a escolha.
        var c = await TorneioAsync();
        var masculina = c.SemiNoEr.CategoriaId;

        var controller = await AbrirAsync(c, categorias: new[] { masculina });
        var slots = (List<HorariosDaGrade.Slot>)controller.ViewBag.SlotsDaGrade;

        var noveECinquenta = slots.Single(s => s.Horario == c.GrupoNoEr.HorarioPrevisto);
        Assert.Equal(1, noveECinquenta.Ocupadas);
    }
}
