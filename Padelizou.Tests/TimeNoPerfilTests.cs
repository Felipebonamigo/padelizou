using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// O TIME QUE A PESSOA REPRESENTA APARECE NO PERFIL (16/09/2026).
//
// 🗣️ Felipe: *"aqui no perfil, coloque tambem o time que ele representa"*.
//
// O time já era entidade desde sempre (`Jogador.TimeId`) e já aparecia no RANKING e no escudo
// da lista de jogos — só o perfil, que é a página onde se vai justamente pra saber quem a
// pessoa é, não dizia. Sem migration: a coluna existe.
public class TimeNoPerfilTests
{
    // ⚠️ DOIS CONTEXTOS SOBRE O MESMO BANCO, e isto é o coração do teste. Com um só, o EF
    // InMemory costura `jogador.Time` sozinho pelo rastreador de mudanças — o `Time` semeado
    // já está na memória do contexto — e o teste passaria VERDE mesmo sem o `Include` no
    // controller. Seria o defeito dentro do teste que deveria pegá-lo. Semear num contexto e
    // consultar em outro é o que faz o `Include` ser realmente necessário.
    private static DbContextOptions<DbPadelContext> BancoCompartilhado() =>
        new DbContextOptionsBuilder<DbPadelContext>()
            .UseInMemoryDatabase("time_no_perfil_" + Guid.NewGuid())
            .Options;

    private static void Semear(DbContextOptions<DbPadelContext> opcoes, int? timeId, string? logo)
    {
        using var ctx = new DbPadelContext(opcoes);
        if (timeId is int id)
            ctx.Times.Add(new Time { Id = id, Nome = "ER Padel", Logo = logo });
        ctx.Jogadores.Add(new Jogador { Id = 7, Nome = "Jogador Com Time", Cpf = "1", TimeId = timeId });
        ctx.SaveChanges();
    }

    private static async Task<Jogador> JogadorDaTelaAsync(DbContextOptions<DbPadelContext> opcoes)
    {
        using var ctx = new DbPadelContext(opcoes);
        var resultado = await TestInfra.NovoJogadoresController(ctx, null)
            .Perfil(7, TestInfra.PortaDosDesafiosDe(ctx));

        var view = Assert.IsType<ViewResult>(resultado);
        var modelo = Assert.IsType<(Jogador, List<Dupla>)>(view.Model);
        return modelo.Item1;
    }

    [Fact]
    public async Task O_perfil_carrega_o_time_junto_do_jogador()
    {
        var opcoes = BancoCompartilhado();
        Semear(opcoes, timeId: 10, logo: "/uploads/logos-time/er.png");

        var jogador = await JogadorDaTelaAsync(opcoes);

        // Sem o Include no controller isto vem nulo, e a tela não mostra time nenhum — SEM
        // erro, sem log, sem nada. É o jeito que este defeito falharia: calado.
        Assert.NotNull(jogador.Time);
        Assert.Equal("ER Padel", jogador.Time!.Nome);
    }

    [Fact]
    public async Task Quem_nao_tem_time_abre_o_perfil_do_mesmo_jeito()
    {
        // Ter time é opcional e a maioria não tem — o perfil não pode depender disso.
        var opcoes = BancoCompartilhado();
        Semear(opcoes, timeId: null, logo: null);

        var jogador = await JogadorDaTelaAsync(opcoes);

        Assert.Null(jogador.Time);
    }

    // ── A tela ───────────────────────────────────────────────────────────────────────────

    private static string Perfil() => File.ReadAllText(
        Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Jogadores", "Perfil.cshtml"));

    [Fact]
    public void A_tela_mostra_o_nome_do_time_e_leva_pra_vitrine_dele()
    {
        var html = Perfil();

        Assert.Matches(@"Model\.jogador\.Time is [\w.]*Time \w+", html);
        Assert.Contains("@time.Nome", html);
        Assert.Matches(@"asp-controller=""Times""\s+asp-action=""Detalhes""\s+asp-route-id=""@time\.Id""", html);
    }

    [Fact]
    public void O_escudo_so_desenha_quando_o_time_TEM_logo()
    {
        // Mesma regra que o _JogoEmLinha já aplica: os 44 times importados do ranking nasceram
        // sem logo, e `src=""` é ícone quebrado — aqui seria bem no alto do perfil.
        var html = Perfil();

        Assert.Matches(@"!string\.IsNullOrEmpty\(time\.Logo\)[\s\S]{0,200}?<img[^>]*src=""@time\.Logo""", html);
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
