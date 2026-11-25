using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A FINALÍSSIMA DTN E O TROFÉU (Treta, 25/11/2026) — ver TRETA.md e Services/FinalissimaDtn.
//
// 🗣️ Organizador: "troca o do mesmo lado (direita com direita), tem que manter posição do
// torneio"; "a dupla chega em um consenso"; o Preto "não tem finalíssima, quem perde é votado".
public class TretaFinalissimaTests
{
    private static readonly DateTime Sabado = new(2026, 12, 5);

    private sealed class Cenario
    {
        public DbPadelContext Ctx = null!;
        public Torneio Torneio = null!;
        public Categoria Branco = null!;
        public Categoria Verde = null!;
        public Jogador Organizador = null!;
        public RoboDoChaveamento Robo() => new(Ctx, TestInfra.EstatisticasFalsas());
        public FinalissimaDtn Servico() => new(Ctx);
    }

    // Dois módulos, cada um com 2 grupos de 3 — o 3º de cada grupo vai pro DTN, e a final DTN
    // é 3ºA × 3ºB. Em toda dupla o jogador 1 joga na DIREITA (inscrição) e o 2 na esquerda
    // (perfil). `parDaFinalissima` liga Branco ↔ Verde.
    private static Cenario Montar(bool parDaFinalissima = true)
    {
        var c = new Cenario { Ctx = TestInfra.NovoContexto() };
        var ctx = c.Ctx;
        c.Organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(c.Organizador);
        c.Torneio = new Torneio
        {
            Nome = "Treta", Codigo = "TRETA", Status = "Fase de Grupos", ForaDoRanking = true,
            DataInicio = Sabado.AddHours(9), DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(9, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(22, 0, 0), QuantidadeQuadras = 3, TempoPrevistoPartidaMinutos = 30,
        };
        ctx.Torneios.Add(c.Torneio);
        c.Branco = new Categoria { Nome = "Branco", Codigo = "BR", Torneio = c.Torneio, ClassificadosPorGrupo = 2, DuplasNoDtnPorGrupo = 1 };
        c.Verde = new Categoria { Nome = "Verde", Codigo = "VD", Torneio = c.Torneio, ClassificadosPorGrupo = 2, DuplasNoDtnPorGrupo = 1 };
        ctx.Categorias.AddRange(c.Branco, c.Verde);
        ctx.SaveChanges();
        if (parDaFinalissima) c.Branco.ParDaFinalissimaId = c.Verde.Id;
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = c.Torneio.Id, JogadorId = c.Organizador.Id, NivelAcesso = "Criador" });
        foreach (var q in new[] { "Quadra 1", "Quadra 2", "Quadra 3" })
            ctx.Quadras.Add(new Quadra { TorneioId = c.Torneio.Id, Nome = q });

        int n = 1;
        foreach (var modulo in new[] { c.Branco, c.Verde })
        {
            for (int g = 0; g < 2; g++)
            {
                string letra = ((char)('A' + g)).ToString();
                var grupo = new GrupoTorneio { Categoria = modulo, Nome = "Grupo " + letra };
                ctx.Add(grupo);
                var doGrupo = new List<Dupla>();
                for (int i = 0; i < 3; i++)
                {
                    var j1 = TestInfra.NovoJogador(n++);
                    var j2 = TestInfra.NovoJogador(n++);
                    j2.LadoQuadra = "Esquerda";
                    ctx.Jogadores.AddRange(j1, j2);
                    var d = new Dupla
                    {
                        Categoria = modulo, Jogador1 = j1, Jogador2 = j2, GrupoTorneio = grupo, Grupo = letra,
                        Pago = true, LadoJogador1 = "Direita",
                    };
                    ctx.Duplas.Add(d);
                    doGrupo.Add(d);
                }
                for (int i = 0; i < 3; i++)
                    for (int j = i + 1; j < 3; j++)
                        ctx.Partidas.Add(new Partida
                        {
                            TorneioId = c.Torneio.Id, Categoria = modulo, Dupla1 = doGrupo[i], Dupla2 = doGrupo[j],
                            Codigo = Guid.NewGuid().ToString()[..6], Fase = "Grupo " + letra, Status = "Finalizada",
                            GamesDupla1 = 6, GamesDupla2 = g, SetsDupla1 = 1, SetsDupla2 = 0,
                        });
            }
        }
        ctx.SaveChanges();
        foreach (var p in ctx.Partidas.ToList()) p.VencedorId = p.Dupla1Id;
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return c;
    }

    private static async Task MontarAsDtnsAsync(Cenario c)
    {
        await c.Robo().MontarMataMataDosGruposAsync(c.Branco.Id, c.Torneio.Id);
        c.Ctx.ChangeTracker.Clear();
        await c.Robo().MontarMataMataDosGruposAsync(c.Verde.Id, c.Torneio.Id);
        c.Ctx.ChangeTracker.Clear();
    }

    private static async Task<Partida> FinalDaAsync(Cenario c, Func<Categoria, bool> qual) =>
        (await c.Ctx.Partidas.AsNoTracking().Include(p => p.Categoria).Where(p => p.Fase == "Final").ToListAsync())
            .Single(p => qual(p.Categoria));

    private static async Task FinalizarAsync(Cenario c, Partida partida, int vencedorId)
    {
        var p = await c.Ctx.Partidas.FirstAsync(x => x.Id == partida.Id);
        p.Status = "Finalizada";
        p.VencedorId = vencedorId;
        p.GamesDupla1 = vencedorId == p.Dupla1Id ? 6 : 3;
        p.GamesDupla2 = vencedorId == p.Dupla1Id ? 3 : 6;
        await c.Ctx.SaveChangesAsync();
        await TestInfra.NovoEncerramento(c.Ctx).AplicarAsync(
            p, acabouDeTerminar: true, new EncerramentoDaPartida.LinksDoAviso(null, null));
        c.Ctx.ChangeTracker.Clear();
    }

    // Termina as finais do Principal e das duas DTNs; devolve as duas perdedoras do DTN.
    private static async Task<(Dupla PerdeuBranco, Dupla PerdeuVerde)> FecharAsFinaisDtnAsync(Cenario c)
    {
        await MontarAsDtnsAsync(c);
        var dtnBranco = await FinalDaAsync(c, cat => cat.PapelDaChave == ChaveDtn.Dtn && cat.CategoriaDeOrigemId == c.Branco.Id);
        var dtnVerde = await FinalDaAsync(c, cat => cat.PapelDaChave == ChaveDtn.Dtn && cat.CategoriaDeOrigemId == c.Verde.Id);
        await FinalizarAsync(c, dtnBranco, dtnBranco.Dupla1Id);
        await FinalizarAsync(c, dtnVerde, dtnVerde.Dupla1Id);
        var pb = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dtnBranco.Dupla2Id);
        var pv = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dtnVerde.Dupla2Id);
        return (pb, pv);
    }

    private static async Task FecharOPrincipalAsync(Cenario c)
    {
        foreach (var modulo in new[] { c.Branco, c.Verde })
        {
            foreach (var semi in await c.Ctx.Partidas.AsNoTracking()
                         .Where(p => p.CategoriaId == modulo.Id && p.Fase == "Semifinal").ToListAsync())
                await FinalizarAsync(c, semi, semi.Dupla1Id);
            var final = await c.Ctx.Partidas.AsNoTracking().FirstAsync(p => p.CategoriaId == modulo.Id && p.Fase == "Final");
            await FinalizarAsync(c, final, final.Dupla1Id);
        }
    }

    private static Dupla Dupla(int direita, int esquerda, string? lado1 = "Direita", string? perfil2 = "Esquerda") => new()
    {
        Jogador1Id = direita, Jogador2Id = esquerda, LadoJogador1 = lado1,
        Jogador1 = new Jogador { Id = direita, Nome = "j1", Cpf = "1" },
        Jogador2 = new Jogador { Id = esquerda, Nome = "j2", Cpf = "2", LadoQuadra = perfil2 },
    };

    // ── A troca, pura ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Troca_direita_com_direita_e_cada_um_fica_no_seu_lado()
    {
        var branco = Dupla(direita: 1, esquerda: 2);
        var verde = Dupla(direita: 3, esquerda: 4);

        var proposta = FinalissimaDtn.ProporTroca(branco, verde);
        Assert.Equal(1, proposta.DireitaDaPrimeira);
        Assert.Equal(3, proposta.DireitaDaSegunda);
        Assert.True(proposta.Certa);

        // E₁ + D₂ × E₂ + D₁
        var (d1, d2) = FinalissimaDtn.Montar(branco, 1, verde, 3);
        Assert.Equal((2, 3), d1);
        Assert.Equal((4, 1), d2);
    }

    [Fact]
    public void Quando_o_lado_e_desconhecido_a_proposta_pede_confirmacao()
    {
        var branco = Dupla(direita: 1, esquerda: 2, lado1: "Ambos", perfil2: null);
        var verde = Dupla(direita: 3, esquerda: 4);

        Assert.False(FinalissimaDtn.ProporTroca(branco, verde).Certa);
    }

    [Fact]
    public void Jogador_1_na_esquerda_poe_o_parceiro_na_direita()
    {
        var branco = Dupla(direita: 1, esquerda: 2, lado1: "Esquerda", perfil2: null);
        var verde = Dupla(direita: 3, esquerda: 4);

        Assert.Equal(2, FinalissimaDtn.ProporTroca(branco, verde).DireitaDaPrimeira);
    }

    // ── Pendência ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Com_as_duas_finais_DTN_fechadas_a_Finalissima_fica_pendente_com_os_perdedores()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, pv) = await FecharAsFinaisDtnAsync(c);

        var pendente = Assert.Single(await c.Servico().PendentesAsync(c.Torneio.Id));
        Assert.Equal(c.Branco.Id, pendente.Modulo1.Id);
        Assert.Equal(pb.Id, pendente.Perdedora1.Id);
        Assert.Equal(pv.Id, pendente.Perdedora2.Id);
        Assert.Equal(pb.Jogador1Id, pendente.Proposta.DireitaDaPrimeira);
        Assert.True(pendente.Proposta.Certa);
    }

    [Fact]
    public async Task Com_uma_final_DTN_por_jogar_nada_esta_pendente()
    {
        var c = Montar();
        using var _ = c.Ctx;
        await MontarAsDtnsAsync(c);
        var dtnBranco = await FinalDaAsync(c, cat => cat.PapelDaChave == ChaveDtn.Dtn && cat.CategoriaDeOrigemId == c.Branco.Id);
        await FinalizarAsync(c, dtnBranco, dtnBranco.Dupla1Id);

        Assert.Empty(await c.Servico().PendentesAsync(c.Torneio.Id));
    }

    [Fact]
    public async Task O_torneio_nao_fecha_enquanto_a_Finalissima_nao_foi_criada()
    {
        var c = Montar();
        using var _ = c.Ctx;
        await MontarAsDtnsAsync(c);
        await FecharOPrincipalAsync(c);
        await FecharAsFinaisDtnAsync(c);

        Assert.NotEqual("Finalizado", (await c.Ctx.Torneios.FindAsync(c.Torneio.Id))!.Status);
    }

    [Fact]
    public async Task Sem_par_o_torneio_fecha_com_a_ultima_final()
    {
        var c = Montar(parDaFinalissima: false);
        using var _ = c.Ctx;
        await MontarAsDtnsAsync(c);
        await FecharOPrincipalAsync(c);
        await FecharAsFinaisDtnAsync(c);

        Assert.Equal("Finalizado", (await c.Ctx.Torneios.FindAsync(c.Torneio.Id))!.Status);
    }

    // ── Criar ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_cria_a_Finalissima_com_os_parceiros_trocados()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, pv) = await FecharAsFinaisDtnAsync(c);

        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
        await controller.CriarFinalissima(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pv.Jogador1Id);
        c.Ctx.ChangeTracker.Clear();

        var finalissima = await c.Ctx.Categorias.SingleAsync(x => x.PapelDaChave == ChaveDtn.Finalissima);
        Assert.Equal(c.Branco.Id, finalissima.CategoriaDeOrigemId);
        var duplas = await c.Ctx.Duplas.Where(d => d.CategoriaId == finalissima.Id).ToListAsync();
        Assert.Equal(2, duplas.Count);
        Assert.All(duplas, d => Assert.True(d.Pago));
        // E₁ + D₂ (esquerda do Branco com a direita do Verde) e E₂ + D₁.
        Assert.Contains(duplas, d => d.Jogador1Id == pb.Jogador2Id && d.Jogador2Id == pv.Jogador1Id);
        Assert.Contains(duplas, d => d.Jogador1Id == pv.Jogador2Id && d.Jogador2Id == pb.Jogador1Id);

        var jogo = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == finalissima.Id);
        Assert.Equal("Final", jogo.Fase);
        Assert.Empty(await c.Servico().PendentesAsync(c.Torneio.Id));
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_cria_a_Finalissima()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, pv) = await FecharAsFinaisDtnAsync(c);

        var controller = TestInfra.NovoTorneiosController(c.Ctx, pb.Jogador1Id);
        var r = await controller.CriarFinalissima(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pv.Jogador1Id);

        Assert.IsType<ForbidResult>(r);
        Assert.False(await c.Ctx.Categorias.AnyAsync(x => x.PapelDaChave == ChaveDtn.Finalissima));
    }

    [Fact]
    public async Task Criar_duas_vezes_nao_duplica()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, pv) = await FecharAsFinaisDtnAsync(c);
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);

        await controller.CriarFinalissima(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pv.Jogador1Id);
        c.Ctx.ChangeTracker.Clear();
        await TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id)
            .CriarFinalissima(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pv.Jogador1Id);

        Assert.Equal(1, await c.Ctx.Categorias.CountAsync(x => x.PapelDaChave == ChaveDtn.Finalissima));
    }

    [Fact]
    public async Task Direita_que_nao_e_da_dupla_e_recusada()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, _) = await FecharAsFinaisDtnAsync(c);

        var erro = await c.Servico().CriarAsync(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pb.Jogador2Id!.Value, c.Robo());

        Assert.NotNull(erro);
        Assert.False(await c.Ctx.Categorias.AnyAsync(x => x.PapelDaChave == ChaveDtn.Finalissima));
    }

    // ── O troféu ─────────────────────────────────────────────────────────────────────────

    private static async Task<(Categoria Finalissima, Dupla Venceu, Dupla Perdeu)> JogarAFinalissimaAsync(Cenario c)
    {
        var (pb, pv) = await FecharAsFinaisDtnAsync(c);
        await c.Servico().CriarAsync(c.Torneio.Id, c.Branco.Id, pb.Jogador1Id, pv.Jogador1Id, c.Robo());
        c.Ctx.ChangeTracker.Clear();
        var finalissima = await c.Ctx.Categorias.AsNoTracking().SingleAsync(x => x.PapelDaChave == ChaveDtn.Finalissima);
        var jogo = await c.Ctx.Partidas.AsNoTracking().SingleAsync(p => p.CategoriaId == finalissima.Id);
        await FinalizarAsync(c, jogo, jogo.Dupla1Id);
        var venceu = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == jogo.Dupla1Id);
        var perdeu = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == jogo.Dupla2Id);
        return (finalissima, venceu, perdeu);
    }

    [Fact]
    public async Task A_dupla_vencedora_registra_o_trofeu_pra_um_dos_perdedores()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (finalissima, venceu, perdeu) = await JogarAFinalissimaAsync(c);

        var controller = TestInfra.NovoTorneiosController(c.Ctx, venceu.Jogador2Id!.Value);
        await controller.RegistrarTrofeuDtn(c.Torneio.Id, finalissima.Id, perdeu.Jogador1Id);

        Assert.Equal(perdeu.Jogador1Id, (await c.Ctx.Categorias.FindAsync(finalissima.Id))!.TrofeuDtnJogadorId);
    }

    [Fact]
    public async Task O_trofeu_nao_vai_pra_quem_venceu()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (finalissima, venceu, _) = await JogarAFinalissimaAsync(c);

        var erro = await c.Servico().RegistrarTrofeuAsync(c.Torneio.Id, finalissima.Id, venceu.Jogador1Id,
            venceu.Jogador2Id!.Value, ehOrganizador: false);

        Assert.NotNull(erro);
        Assert.Null((await c.Ctx.Categorias.FindAsync(finalissima.Id))!.TrofeuDtnJogadorId);
    }

    [Fact]
    public async Task Quem_perdeu_nao_escolhe_o_trofeu()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (finalissima, _, perdeu) = await JogarAFinalissimaAsync(c);

        var erro = await c.Servico().RegistrarTrofeuAsync(c.Torneio.Id, finalissima.Id, perdeu.Jogador2Id!.Value,
            perdeu.Jogador1Id, ehOrganizador: false);

        Assert.NotNull(erro);
        Assert.Null((await c.Ctx.Categorias.FindAsync(finalissima.Id))!.TrofeuDtnJogadorId);
    }

    [Fact]
    public async Task Registrado_a_dupla_nao_troca_mas_o_organizador_corrige()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (finalissima, venceu, perdeu) = await JogarAFinalissimaAsync(c);
        var servico = c.Servico();
        Assert.Null(await servico.RegistrarTrofeuAsync(c.Torneio.Id, finalissima.Id, perdeu.Jogador1Id, venceu.Jogador1Id, false));

        Assert.NotNull(await servico.RegistrarTrofeuAsync(c.Torneio.Id, finalissima.Id, perdeu.Jogador2Id!.Value, venceu.Jogador2Id!.Value, false));
        Assert.Equal(perdeu.Jogador1Id, (await c.Ctx.Categorias.FindAsync(finalissima.Id))!.TrofeuDtnJogadorId);

        Assert.Null(await servico.RegistrarTrofeuAsync(c.Torneio.Id, finalissima.Id, perdeu.Jogador2Id!.Value, c.Organizador.Id, true));
        Assert.Equal(perdeu.Jogador2Id, (await c.Ctx.Categorias.FindAsync(finalissima.Id))!.TrofeuDtnJogadorId);
    }

    [Fact]
    public async Task Sem_par_o_troféu_sai_da_propria_final_DTN()
    {
        var c = Montar(parDaFinalissima: false);
        using var _ = c.Ctx;
        await MontarAsDtnsAsync(c);
        var final = await FinalDaAsync(c, cat => cat.PapelDaChave == ChaveDtn.Dtn && cat.CategoriaDeOrigemId == c.Branco.Id);
        await FinalizarAsync(c, final, final.Dupla1Id);
        var venceu = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == final.Dupla1Id);
        var perdeu = await c.Ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == final.Dupla2Id);

        var decisivo = Assert.Single(await c.Servico().DecisivosAsync(c.Torneio.Id));
        Assert.Equal(final.CategoriaId, decisivo.Categoria.Id);
        Assert.Null(await c.Servico().RegistrarTrofeuAsync(c.Torneio.Id, final.CategoriaId, perdeu.Jogador2Id!.Value, venceu.Jogador1Id, false));
    }

    // ── O card no Details ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task So_quem_organiza_ve_a_Finalissima_por_criar()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (pb, _) = await FecharAsFinaisDtnAsync(c);

        var doOrganizador = await c.Servico().PainelAsync(c.Torneio.Id, c.Organizador.Id, ehOrganizador: true);
        Assert.Single(doOrganizador!.Pendentes);

        var doAtleta = await c.Servico().PainelAsync(c.Torneio.Id, pb.Jogador1Id, ehOrganizador: false);
        Assert.True(doAtleta == null || doAtleta.Pendentes.Count == 0);
    }

    [Fact]
    public async Task So_a_dupla_vencedora_ganha_o_formulario_do_trofeu()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var (_, venceu, perdeu) = await JogarAFinalissimaAsync(c);

        var doVencedor = await c.Servico().PainelAsync(c.Torneio.Id, venceu.Jogador1Id, ehOrganizador: false);
        Assert.True(Assert.Single(doVencedor!.Trofeus).PodeEscolher);

        var doPerdedor = await c.Servico().PainelAsync(c.Torneio.Id, perdeu.Jogador1Id, ehOrganizador: false);
        Assert.False(Assert.Single(doPerdedor!.Trofeus).PodeEscolher);
    }
}

// O card do Treta no Details (FinalissimaDtn.PainelAsync).
public class PainelDoTretaTests
{
    [Fact]
    public async Task Torneio_sem_DTN_nao_mostra_card_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        Assert.Null(await new FinalissimaDtn(ctx).PainelAsync(torneio.Id, null, ehOrganizador: true));
    }
}

// As consultas novas do Treta viram SQL no Postgres? (o InMemory da suíte não traduz nada —
// ver TraducaoDeConsultasDoPerfilTests.)
public class TraducaoDasConsultasDoTretaTests
{
    private static DbPadelContext ContextoPostgres() =>
        new(new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options);

    [Fact]
    public void A_final_DTN_de_um_modulo_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int moduloId = 7;
        var sql = ctx.Partidas.AsNoTracking()
            .Where(p => p.Categoria.CategoriaDeOrigemId == moduloId
                     && p.Categoria.PapelDaChave == ChaveDtn.Dtn
                     && p.Fase == "Final" && p.Status == "Finalizada" && p.VencedorId != null)
            .Select(p => new { p.Dupla1Id, p.Dupla2Id, p.VencedorId })
            .ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void Os_atletas_da_final_decisiva_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        int categoriaId = 7;
        var sql = ctx.Partidas.AsNoTracking()
            .Where(p => p.CategoriaId == categoriaId && p.Fase == "Final" && p.Status == "Finalizada" && p.VencedorId != null)
            .Select(p => new
            {
                p.VencedorId, p.Dupla1Id,
                V1 = p.Dupla1.Jogador1Id, V2 = p.Dupla1.Jogador2Id,
                W1 = p.Dupla2.Jogador1Id, W2 = p.Dupla2.Jogador2Id,
            })
            .ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void Os_pares_antigos_da_Finalissima_viram_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 1;
        var envolvidos = new List<int> { 3, 4 };
        var sql = ctx.Categorias
            .Where(c => c.TorneioId == torneioId && c.ParDaFinalissimaId != null
                     && (envolvidos.Contains(c.ParDaFinalissimaId.Value) || envolvidos.Contains(c.Id)))
            .ToQueryString();
        Assert.Contains("SELECT", sql);
    }

    [Fact]
    public void A_DTN_invertida_no_avanco_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int categoriaId = 7;
        var sql = ctx.Categorias.Where(c => c.Id == categoriaId && c.PapelDaChave == ChaveDtn.Dtn).ToQueryString();
        Assert.Contains("SELECT", sql);
    }
}
