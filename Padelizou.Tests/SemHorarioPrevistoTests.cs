using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Tests;

// Torneio "por ordem de liberação": os jogos saem numa ORDEM, sem hora marcada, e entram na
// quadra conforme ela vaga. É como roda a maioria dos internos de clube — ninguém prevê
// quanto dura um jogo de 4 games, e uma grade que atrasa 40 min logo cedo passa o resto do
// dia mentindo pra todo mundo que confiou nela.
public class SemHorarioPrevistoTests
{
    [Fact]
    public async Task Com_a_chave_ligada_nenhum_jogo_nasce_com_hora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        Assert.Equal(6, jogos.Count);                                  // as chaves saem normalmente
        Assert.All(jogos, j => Assert.Null(j.HorarioPrevisto));        // e nenhuma com hora
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_escolha_e_feita_na_CRIACAO_e_chega_no_torneio(bool porOrdem)
    {
        // A chave existia só na tela de gestão, DEPOIS do torneio criado — então a criação
        // pedia hora de início, hora de fim e duração de jogo pra todo mundo, inclusive pra
        // quem ia chamar o jogo conforme a quadra vaga. Agora ela é a pergunta que decide se
        // aqueles campos aparecem, e é na criação que ela precisa chegar inteira.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true,
        });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        await ctx.SaveChangesAsync();

        var novo = new Torneio
        {
            Nome = "Interno do clube",
            ClubeId = 1,
            Status = "Inscrições Abertas",
            SetsFaseGrupos = 1,
            GamesFaseGrupos = 6,
            RestricaoCategoria = "Livre",
            FormaPagamento = "Externo",
            SemHorarioPrevisto = porOrdem,
        };

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(novo, new[] { 3 }, null, null, null, null);

        var criado = Assert.Single(ctx.Torneios);
        Assert.Equal(porOrdem, criado.SemHorarioPrevisto);
    }

    // Chama a edição do torneio com o mínimo obrigatório, ligando (ou não) o "por ordem".
    private static Task<IActionResult> EditarAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, bool porOrdem) =>
        TestInfra.NovoTorneiosController(ctx, organizadorId).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao,
            clubeId: torneio.ClubeId, quantidadeQuadras: 1, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: torneio.RestricaoCategoria, capa: null,
            tempoPrevistoPartidaMinutos: 50, semHorarioPrevisto: porOrdem);

    [Fact]
    public async Task LIGAR_o_por_ordem_apaga_o_horario_dos_jogos_que_ainda_nao_comecaram()
    {
        // O caso real: a chave foi sorteada COM horário e a opção foi ligada depois. Antes, a
        // tela ficava se contradizendo — faixa dizendo "os jogos não têm horário" em cima de
        // uma lista com hora marcada. No "Americano das Gurias do Padel" (07/08/2026) eram 10
        // jogos anunciando 00:54 às 04:14 da MADRUGADA pras jogadoras.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        Assert.All(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync(),
            j => Assert.NotNull(j.HorarioPrevisto));   // nasceram COM hora

        await EditarAsync(ctx, torneio, org.Id, porOrdem: true);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.NotEmpty(jogos);
        Assert.All(jogos, j => Assert.Null(j.HorarioPrevisto));
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.SemHorarioPrevisto);
    }

    [Fact]
    public async Task O_jogo_que_JA_COMECOU_nao_perde_nada()
    {
        // Hora real é registro do que aconteceu, não promessa — apagar seria reescrever o
        // passado do torneio.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var emQuadra = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        emQuadra.Status = "Em Andamento";
        emQuadra.HorarioInicioReal = new DateTime(2026, 8, 8, 19, 0, 0);
        var horaQueTinha = emQuadra.HorarioPrevisto;
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id, porOrdem: true);

        var depois = await ctx.Partidas.FindAsync(emQuadra.Id);
        Assert.Equal(horaQueTinha, depois!.HorarioPrevisto);
        Assert.Equal(new DateTime(2026, 8, 8, 19, 0, 0), depois.HorarioInicioReal);
    }

    [Fact]
    public async Task Salvar_a_edicao_SEM_mexer_na_chave_nao_apaga_horario_de_ninguem()
    {
        // A limpeza é da TRANSIÇÃO (desligado → ligado). Se ela rodasse a cada gravação, um
        // torneio já por ordem perderia horário que o organizador tivesse posto na mão, e um
        // torneio comum ficaria refém de qualquer salvamento.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        await EditarAsync(ctx, torneio, org.Id, porOrdem: false);

        Assert.All(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync(),
            j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public async Task Desligada_a_chave_a_grade_continua_marcando_hora_como_sempre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public async Task A_ordem_dos_jogos_sem_hora_e_a_mesma_em_que_eles_entrariam_na_grade()
    {
        // Sem hora, a ORDEM é a única promessa que sobra — e ela é a de criação. Se os jogos
        // saíssem embaralhados, o organizador não teria por onde chamar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();

        // Os jogos do Grupo A vêm antes dos do Grupo B: é a ordem em que o sorteio os criou.
        var fases = jogos.Select(j => j.Fase).ToList();
        Assert.Equal(fases.OrderBy(f => f, StringComparer.Ordinal), fases);
    }

    [Fact]
    public async Task O_mata_mata_gerado_depois_tambem_nasce_sem_hora()
    {
        // O robô de avanço agenda na grade por um caminho PRÓPRIO (AgendarNaGradeAsync).
        // Se ele ignorasse a chave, a fase de grupos ficaria sem hora e a semifinal apareceria
        // marcada pras 3h da manhã — o pior dos dois mundos.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 6, 2);

        var mataMata = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && !p.Fase.StartsWith("Grupo "))
            .ToListAsync();

        Assert.NotEmpty(mataMata);
        Assert.All(mataMata, j => Assert.Null(j.HorarioPrevisto));
    }
}
