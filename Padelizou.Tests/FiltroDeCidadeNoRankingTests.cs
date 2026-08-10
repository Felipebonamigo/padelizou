using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A lista do filtro e a CONSULTA do filtro são duas metades da mesma promessa: mostrar uma
// opção só e continuar achando todo mundo. Estes testes prendem as duas juntas, porque errar
// uma delas é mudo — a tela abre igual, só vem gente a menos.
public class FiltroDeCidadeNoRankingTests
{
    // Quatro jogadores em Gravataí escrita de quatro jeitos, um em Canoas que nunca jogou
    // torneio, e um em São Paulo.
    private static (DbPadelContext ctx, EstatisticasService svc) Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        var j1 = new Jogador { Nome = "J1", Cpf = "99900000001", Cidade = "Gravataí", Estado = "RS" };
        var j2 = new Jogador { Nome = "J2", Cpf = "99900000002", Cidade = "GRAVATAI", Estado = "RS" };
        var j3 = new Jogador { Nome = "J3", Cpf = "99900000003", Cidade = "gravatai", Estado = "RS" };
        var j4 = new Jogador { Nome = "J4", Cpf = "99900000004", Cidade = "Gravatai", Estado = "RS" };
        var semTorneio = new Jogador { Nome = "Só cadastro", Cpf = "99900000005", Cidade = "Canoas", Estado = "RS" };
        var paulista = new Jogador { Nome = "SP", Cpf = "99900000006", Cidade = "São Paulo", Estado = "SP" };
        ctx.Jogadores.AddRange(j1, j2, j3, j4, semTorneio, paulista);

        var torneio = new Torneio { Nome = "Copa", Codigo = "CPT", DataInicio = new DateTime(2026, 6, 1) };
        var cat = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "C2M", Torneio = torneio };
        ctx.AddRange(torneio, cat);

        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2, UltimaFase = "Campeao" });
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j3, Jogador2 = j4, UltimaFase = "Final" });
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = paulista, UltimaFase = "Grupos" });
        ctx.SaveChanges();

        return (ctx, new EstatisticasService(ctx));
    }

    [Fact]
    public async Task Quatro_grafias_de_Gravatai_viram_uma_opcao_so()
    {
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var (_, cidades) = await svc.ObterLocaisDisponiveisAsync(null);

        Assert.Single(cidades, c => NomeDeCidade.Mesma(c, "Gravataí"));
        Assert.Contains("Gravataí", cidades);   // a bem escrita é a que aparece
    }

    [Fact]
    public async Task Escolher_Gravatai_traz_os_quatro_e_nao_so_um()
    {
        // Este é o teste que existe pro conserto não virar um estrago: agrupar a lista sem
        // ensinar a consulta a comparar do mesmo jeito deixaria 3 dos 4 de fora do ranking.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var doLocal = await svc.ObterJogadoresDoLocalAsync(new[] { "Gravataí" }, null);

        Assert.Equal(4, doLocal!.Count);
    }

    [Fact]
    public async Task O_ranking_so_oferece_cidade_onde_alguem_jogou_torneio()
    {
        // A primeira linha da página diz que tudo ali sai de resultado de torneio. Canoas só
        // tem cadastro — oferecê-la é prometer um ranking que volta vazio.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var (estados, cidades) = await svc.ObterLocaisDisponiveisAsync(null, somenteQuemJogouTorneio: true);

        Assert.Contains("Gravataí", cidades);
        Assert.Contains("São Paulo", cidades);
        Assert.DoesNotContain("Canoas", cidades);
        Assert.Equal(new[] { "RS", "SP" }, estados);
    }

    [Fact]
    public async Task A_busca_de_jogadores_continua_oferecendo_quem_so_tem_cadastro()
    {
        // A régua do torneio é do RANKING. A busca existe pra ACHAR gente — esconder a cidade
        // de quem ainda não jogou torneio esconderia justamente o jogador novo.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var (_, cidades) = await svc.ObterLocaisDisponiveisAsync(null);

        Assert.Contains("Canoas", cidades);
    }

    [Fact]
    public async Task Torneio_restrito_nao_faz_uma_cidade_aparecer_no_filtro()
    {
        // Mesma régua do resto do ranking (`DuplaContaNoRanking`): torneio fechado e Americano
        // não pontuam, então a cidade que só existe por causa deles prometeria tabela vazia.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var escondido = new Jogador { Nome = "Fechado", Cpf = "99900000007", Cidade = "Ivoti", Estado = "RS" };
        ctx.Jogadores.Add(escondido);
        var torneio = new Torneio { Nome = "Interno", Codigo = "INT", Restrito = true };
        var cat = new Categoria { Nome = "Livre", Codigo = "LIV", Torneio = torneio };
        ctx.AddRange(torneio, cat);
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = escondido, UltimaFase = "Campeao" });
        await ctx.SaveChangesAsync();

        var (_, cidades) = await svc.ObterLocaisDisponiveisAsync(null, somenteQuemJogouTorneio: true);

        Assert.DoesNotContain("Ivoti", cidades);
    }

    [Fact]
    public async Task Linha_de_TIME_nao_conta_como_torneio_jogado()
    {
        // Em categoria de times o `Jogador1Id` é o organizador que cadastrou — ele não jogou
        // nada, e a cidade dele não pode entrar no filtro por causa disso.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000008", Cidade = "Esteio", Estado = "RS" };
        ctx.Jogadores.Add(organizador);
        var torneio = new Torneio { Nome = "Times", Codigo = "TMS" };
        var cat = new Categoria { Nome = "Times", Codigo = "TM", Torneio = torneio };
        ctx.AddRange(torneio, cat);
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = organizador, NomeTime = "Time A", UltimaFase = "Campeao" });
        await ctx.SaveChangesAsync();

        var (_, cidades) = await svc.ObterLocaisDisponiveisAsync(null, somenteQuemJogouTorneio: true);

        Assert.DoesNotContain("Esteio", cidades);
    }

    [Fact]
    public async Task O_estado_escolhido_continua_recortando_a_lista_de_cidades()
    {
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var (_, cidades) = await svc.ObterLocaisDisponiveisAsync("RS", somenteQuemJogouTorneio: true);

        Assert.Equal(new[] { "Gravataí" }, cidades);
    }
}
