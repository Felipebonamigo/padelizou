using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// UM MVP FEMININO E UM MASCULINO.
//
// 🗣️ Felipe, 11/10/2026: *"na votação para o MVP, permita votar em um feminino e um masculino"*;
// e em 13/10, depois de segurar a mudança durante o NATA: *"vamos fazer para os próximos, separar a
// votação do feminino e do masculino"*. Desenho aprovado por ele, opção (a): o naipe do candidato
// sai da CATEGORIA em que ele foi campeão — Feminina → Feminino, Masculina → Masculino —; em
// categoria mista ou livre, do sexo do cadastro; sem nenhum dos dois, fica fora da cédula dividida.
//
// ⚠️ SÓ DIVIDE QUANDO HÁ OS DOIS NAIPES ENTRE OS CAMPEÕES. Torneio só masculino continua com a
// cédula única de sempre — e é por isso que nenhum teste antigo de MvpDoTorneioTests muda.
//
// ⚠️ O PASSADO NÃO SE REESCREVE: torneio que já recebeu voto SEM naipe (todo voto anterior a esta
// mudança) segue com cédula única, mesmo tendo campeões dos dois naipes. Senão o MVP de um torneio
// já apurado sumiria da página — e a conquista do perfil de quem foi eleito junto.
public class MvpPorNaipeTests
{
    private static readonly DateTime Domingo = new(2026, 10, 11, 18, 0, 0);
    private static DateTime Aberta => Domingo.AddHours(1);
    private static DateTime Fechada => Domingo.AddHours(MvpDoTorneio.HorasParaVotar + 1);

    private sealed record Cenario(Torneio Torneio, Dupla CampeasF, Dupla ViceF, Dupla CampeoesM, Dupla ViceM);

    private static long _cpf = 70000000000;

    private static Jogador Pessoa(DbPadelContext ctx, string nome, string? sexo = null)
    {
        var j = new Jogador { Nome = nome, Cpf = (++_cpf).ToString("00000000000"), Sexo = sexo };
        ctx.Jogadores.Add(j);
        return j;
    }

    private static (Dupla campea, Dupla vice) Categoria(DbPadelContext ctx, Torneio torneio, string nome,
        string? sexoDosJogadores = null, DateTime? fim = null)
    {
        var categoria = new Categoria { Nome = nome, Codigo = nome[..2] + torneio.Id, Torneio = torneio };
        ctx.Categorias.Add(categoria);
        var campea = new Dupla
        {
            Categoria = categoria, UltimaFase = "Campeao",
            Jogador1 = Pessoa(ctx, $"{nome} Campeão 1", sexoDosJogadores),
            Jogador2 = Pessoa(ctx, $"{nome} Campeão 2", sexoDosJogadores),
        };
        var vice = new Dupla
        {
            Categoria = categoria, UltimaFase = "Final",
            Jogador1 = Pessoa(ctx, $"{nome} Vice 1", sexoDosJogadores),
            Jogador2 = Pessoa(ctx, $"{nome} Vice 2", sexoDosJogadores),
        };
        ctx.Duplas.AddRange(campea, vice);
        ctx.SaveChanges();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Dupla1Id = campea.Id, Dupla2Id = vice.Id,
            VencedorId = campea.Id, Status = "Finalizada", HorarioFimReal = fim ?? Domingo, Fase = "Final",
            Codigo = "F" + categoria.Id,
        });
        ctx.SaveChanges();
        return (campea, vice);
    }

    private static Cenario Nata(DbPadelContext ctx, DateTime? fim = null)
    {
        var torneio = new Torneio { Nome = "NATA PADEL TOUR", Codigo = "NATA", Status = "Finalizado" };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        var (cf, vf) = Categoria(ctx, torneio, "5ª Categoria Feminina", fim: fim);
        var (cm, vm) = Categoria(ctx, torneio, "5ª Categoria Masculina", fim: fim);
        return new Cenario(torneio, cf, vf, cm, vm);
    }

    private static CedulaDoMvp Cedula(VotacaoDeMvp v, string naipe) => v.Cedulas.Single(x => x.Naipe == naipe);

    [Fact]
    public async Task Com_campeoes_dos_dois_naipes_a_cedula_se_divide_em_feminino_e_masculino()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, c.Torneio.Id, c.ViceM.Jogador1Id, Aberta);

        Assert.Equal(new[] { NaipeDoMvp.Feminino, NaipeDoMvp.Masculino }, votacao!.Cedulas.Select(x => x.Naipe));
        Assert.Equal(new[] { c.CampeasF.Jogador1Id, c.CampeasF.Jogador2Id!.Value }.OrderBy(x => x),
            Cedula(votacao, NaipeDoMvp.Feminino).Candidatos.Select(x => x.JogadorId).OrderBy(x => x));
    }

    [Fact]
    public async Task Da_pra_votar_em_uma_feminina_E_em_um_masculino()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);
        int eleitor = c.ViceM.Jogador1Id;

        Assert.Null(await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, eleitor, c.CampeasF.Jogador1Id, Aberta));
        Assert.Null(await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, eleitor, c.CampeoesM.Jogador1Id, Aberta));

        var votos = await ctx.VotosDeMvp.Where(v => v.VotanteId == eleitor).ToListAsync();
        Assert.Equal(2, votos.Count);
        Assert.Contains(votos, v => v.Naipe == NaipeDoMvp.Feminino && v.CandidatoId == c.CampeasF.Jogador1Id);
        Assert.Contains(votos, v => v.Naipe == NaipeDoMvp.Masculino && v.CandidatoId == c.CampeoesM.Jogador1Id);

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, c.Torneio.Id, eleitor, Aberta);
        Assert.Equal(c.CampeasF.Jogador1Id, Cedula(votacao!, NaipeDoMvp.Feminino).MeuVoto);
        Assert.Equal(c.CampeoesM.Jogador1Id, Cedula(votacao!, NaipeDoMvp.Masculino).MeuVoto);
    }

    [Fact]
    public async Task Votar_de_novo_no_mesmo_naipe_TROCA_e_nao_mexe_no_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);
        int eleitor = c.ViceF.Jogador1Id;

        await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, eleitor, c.CampeasF.Jogador1Id, Aberta);
        await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, eleitor, c.CampeoesM.Jogador1Id, Aberta);
        await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, eleitor, c.CampeasF.Jogador2Id!.Value, Aberta);

        var votos = await ctx.VotosDeMvp.Where(v => v.VotanteId == eleitor).ToListAsync();
        Assert.Equal(2, votos.Count);
        Assert.Contains(votos, v => v.Naipe == NaipeDoMvp.Feminino && v.CandidatoId == c.CampeasF.Jogador2Id!.Value);
        Assert.Contains(votos, v => v.Naipe == NaipeDoMvp.Masculino && v.CandidatoId == c.CampeoesM.Jogador1Id);
    }

    [Fact]
    public async Task Apurada_elege_um_MVP_por_naipe_e_a_conquista_conta_os_dois()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);
        foreach (var e in new[] { c.ViceF.Jogador1Id, c.ViceF.Jogador2Id!.Value, c.ViceM.Jogador1Id, c.ViceM.Jogador2Id!.Value })
        {
            await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, e, c.CampeasF.Jogador1Id, Aberta);
            await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, e, c.CampeoesM.Jogador2Id!.Value, Aberta);
        }

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, c.Torneio.Id, null, Fechada);

        Assert.True(votacao!.Encerrada);
        Assert.Equal(c.CampeasF.Jogador1Id, Assert.Single(Cedula(votacao, NaipeDoMvp.Feminino).Vencedores).JogadorId);
        Assert.Equal(c.CampeoesM.Jogador2Id!.Value, Assert.Single(Cedula(votacao, NaipeDoMvp.Masculino).Vencedores).JogadorId);

        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, c.CampeasF.Jogador1Id, Fechada));
        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, c.CampeoesM.Jogador2Id!.Value, Fechada));
    }

    [Fact]
    public async Task Torneio_so_masculino_continua_com_a_cedula_unica()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "Só Masc", Codigo = "SM", Status = "Finalizado" };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        var (c4, v4) = Categoria(ctx, torneio, "4ª Categoria Masculina");
        var (c5, _) = Categoria(ctx, torneio, "5ª Categoria Masculina");

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, v4.Jogador1Id, Aberta);
        Assert.Equal(NaipeDoMvp.Unico, Assert.Single(votacao!.Cedulas).Naipe);

        // Um voto só: o segundo TROCA, como sempre foi.
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, v4.Jogador1Id, c4.Jogador1Id, Aberta);
        await MvpDoTorneio.VotarAsync(ctx, torneio.Id, v4.Jogador1Id, c5.Jogador1Id, Aberta);
        Assert.Single(await ctx.VotosDeMvp.Where(v => v.VotanteId == v4.Jogador1Id).ToListAsync());
    }

    [Fact]
    public async Task Torneio_que_ja_tinha_voto_sem_naipe_nao_tem_o_resultado_reescrito()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);
        // Votos de ANTES desta mudança: sem naipe, numa cédula única.
        foreach (var e in new[] { c.ViceF.Jogador1Id, c.ViceF.Jogador2Id!.Value, c.ViceM.Jogador1Id })
            ctx.VotosDeMvp.Add(new VotoDeMvp
            {
                TorneioId = c.Torneio.Id, VotanteId = e, CandidatoId = c.CampeoesM.Jogador1Id, CriadoEm = Domingo,
            });
        await ctx.SaveChangesAsync();

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, c.Torneio.Id, null, Fechada);

        var unica = Assert.Single(votacao!.Cedulas);
        Assert.Equal(NaipeDoMvp.Unico, unica.Naipe);
        Assert.Equal(c.CampeoesM.Jogador1Id, Assert.Single(unica.Vencedores).JogadorId);
        Assert.Equal(1, await MvpDoTorneio.VezesEleitoMvpAsync(ctx, c.CampeoesM.Jogador1Id, Fechada));
    }

    [Fact]
    public async Task Na_mista_quem_decide_e_o_sexo_do_cadastro_e_sem_ele_fica_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx);
        var categoria = new Categoria { Nome = "Mista A", Codigo = "MX", Torneio = c.Torneio };
        ctx.Categorias.Add(categoria);
        var ela = Pessoa(ctx, "Ela da Mista", SexoDoJogador.Feminino);
        var semSexo = Pessoa(ctx, "Sem Sexo Informado");
        ctx.Duplas.Add(new Dupla { Categoria = categoria, UltimaFase = "Campeao", Jogador1 = ela, Jogador2 = semSexo });
        await ctx.SaveChangesAsync();

        var votacao = await MvpDoTorneio.DoTorneioAsync(ctx, c.Torneio.Id, null, Aberta);

        Assert.Contains(Cedula(votacao!, NaipeDoMvp.Feminino).Candidatos, x => x.JogadorId == ela.Id);
        Assert.DoesNotContain(votacao!.Cedulas.SelectMany(x => x.Candidatos), x => x.JogadorId == semSexo.Id);

        // E o servidor recusa o voto em quem ficou fora — a tela não é a regra.
        Assert.NotNull(await MvpDoTorneio.VotarAsync(ctx, c.Torneio.Id, c.ViceM.Jogador1Id, semSexo.Id, Aberta));
    }

    [Fact]
    public void O_banco_trava_um_voto_por_pessoa_POR_NAIPE()
    {
        using var ctx = TestInfra.NovoContexto();
        var indice = ctx.Model.FindEntityType(typeof(VotoDeMvp))!.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(new[] { "TorneioId", "VotanteId", "Naipe" }, indice.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task Depois_do_primeiro_voto_a_tela_fica_na_cedula_que_falta_e_so_desce_depois_do_segundo()
    {
        // O voto aceito desce pra enquete (âncora #avaliar) porque a cédula RECOLHE. Dividida,
        // ela só recolhe com os DOIS votos: descer depois do primeiro esconderia a outra cédula
        // de quem ainda tem o que votar.
        using var ctx = TestInfra.NovoContexto();
        var c = Nata(ctx, fim: DateTime.Now.AddMinutes(-30));
        int eleitor = c.ViceM.Jogador1Id;

        var primeiro = await TestInfra.NovoTorneiosController(ctx, eleitor).VotarMvp(c.Torneio.Id, c.CampeasF.Jogador1Id);
        Assert.Null(Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(primeiro).Fragment);

        var segundo = await TestInfra.NovoTorneiosController(ctx, eleitor).VotarMvp(c.Torneio.Id, c.CampeoesM.Jogador1Id);
        Assert.Equal(MvpDoTorneio.AncoraDaEnquete,
            Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(segundo).Fragment);
    }

    private static string TelaDoMvp()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Torneios", "Mvp.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Mvp.cshtml");
    }

    [Fact]
    public void A_tela_desenha_uma_cedula_por_naipe_com_o_voto_DE_CADA_UMA()
    {
        // O serviço pode separar perfeitamente e a tela continuar desenhando UMA lista com o voto
        // geral — e aí o "Seu voto" de uma cédula apareceria marcado na outra.
        var tela = TelaDoMvp();

        Assert.Contains("foreach (var cedula in Model.Cedulas)", tela);
        Assert.Contains("cedula.Titulo", tela);
        Assert.Contains("cedula.MeuVoto", tela);
        Assert.Contains("cedula.CandidatosNaTela", tela);
        Assert.Contains("cedula.Vencedores", tela);
        // O voto geral não decide mais nada na lista: dividida, ele é nulo.
        Assert.DoesNotContain("Model.MeuVoto", tela);
        Assert.DoesNotContain("Model.CandidatosNaTela", tela);
    }

    [Fact]
    public void A_apuracao_por_naipe_traduz_pra_SQL_do_Postgres()
    {
        // InMemory não valida SQL (CLAUDE.md): o agrupamento por (naipe, candidato) da conquista
        // do perfil passaria lisa aqui e só estouraria na primeira visita a um perfil em produção.
        var opcoes = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(opcoes);

        var sql = ctx.VotosDeMvp.AsNoTracking()
            .Where(v => v.TorneioId == 1)
            .GroupBy(v => new { v.Naipe, v.CandidatoId })
            .Select(g => new { g.Key.Naipe, g.Key.CandidatoId, Votos = g.Count() })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("\"Naipe\"", sql);
    }
}
