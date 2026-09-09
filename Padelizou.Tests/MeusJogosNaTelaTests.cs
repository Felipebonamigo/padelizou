using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// "MEUS JOGOS" NA TELA, DA CONSULTA ATÉ A LISTA — o caminho que MeusJogosTests não cobre.
//
// 🗣️ Reclamação do Felipe (09/09/2026), olhando a aba Jogos do torneio do Er com o filtro
// LIGADO: *"aqui esta exibindo um chaveamento que nao é meu jogo, por exemplo, eu sou do
// grupo A, nao tem por que exibir o chaveamento do grupo E"*.
//
// A regra mora em Services/MeusJogos e está travada lá. O que MORA AQUI é a LIGAÇÃO, e ela
// tem duas pontas que só quebram juntas na tela: o grupo da MINHA dupla precisa ser lido do
// banco (`Include` do GrupoTorneio — sem ele o grupo vem nulo e a chave inteira volta,
// calada), e o grupo da VAGA precisa vir montado pela projeção (`Lado.DeQualGrupo`). Uma
// unidade verde nas duas pontas não prova nenhuma das duas.
public class MeusJogosNaTelaTests
{
    private const int Duracao = 50;
    private static readonly string[] Grupos = ["Grupo A", "Grupo B", "Grupo C", "Grupo D", "Grupo E", "Grupo F"];

    // Seis grupos de duas duplas, uma rodada de grupo cada — o desenho da tela do print:
    // categoria inteira na fase de grupos, mata-mata ainda inexistente (é tudo projeção).
    // Devolve o jogador que está no GRUPO A.
    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador euDoGrupoA)> TorneioNosGruposAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER Padel Tour",
            Codigo = "ERPT2",
            Status = "Fase de Grupos",
            DataInicio = DateTime.Today.AddHours(9),
            QuantidadeQuadras = 4,
            TempoPrevistoPartidaMinutos = Duracao,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria
        {
            Nome = "4ª Categoria Masculina",
            Codigo = "CAT4M",
            Torneio = torneio,
            ChaveDireta = false,
            ClassificadosPorGrupo = 2,
        };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        foreach (var nome in new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4" })
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nome });

        int cpf = 1;
        Jogador? eu = null;

        for (int g = 0; g < Grupos.Length; g++)
        {
            var grupo = new GrupoTorneio { Categoria = categoria, Nome = Grupos[g] };
            ctx.Add(grupo);

            var duplas = new List<Dupla>();
            for (int d = 0; d < 2; d++)
            {
                var j1 = TestInfra.NovoJogador(cpf++);
                var j2 = TestInfra.NovoJogador(cpf++);
                ctx.Jogadores.AddRange(j1, j2);

                var dupla = new Dupla
                {
                    Categoria = categoria,
                    Jogador1 = j1,
                    Jogador2 = j2,
                    GrupoTorneio = grupo,
                    Grupo = Grupos[g][^1..],
                };
                ctx.Duplas.Add(dupla);
                duplas.Add(dupla);

                if (g == 0 && d == 0) eu = j1;
            }

            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                Categoria = categoria,
                Dupla1 = duplas[0],
                Dupla2 = duplas[1],
                Codigo = $"P{g:00}",
                Status = "Agendada",
                Fase = "Fase de Grupos",
                HorarioPrevisto = torneio.DataInicio!.Value.AddMinutes(Duracao * g),
            });
        }

        await ctx.SaveChangesAsync();
        return (ctx, torneio, eu!);
    }

    private static async Task<List<JogoQueVem>> ProjecaoDeAsync(DbPadelContext ctx, Torneio torneio, int jogadorId, bool soMeus)
    {
        // ⚠️ SEM ISTO O TESTE MENTE. No InMemory as entidades montadas acima continuam
        // rastreadas, e as navegações vêm preenchidas de graça — o `Include` do GrupoTorneio
        // vira enfeite e tirá-lo não derruba teste nenhum, enquanto em produção (contexto por
        // requisição, sem lazy loading) o grupo viria NULO e a chave inteira voltaria calada.
        ctx.ChangeTracker.Clear();

        var controller = TestInfra.NovoTorneiosController(ctx, jogadorId);
        var resultado = await controller.Jogos(torneio.Id, null, null, soMeusJogos: soMeus);

        Assert.IsType<ViewResult>(resultado);
        return (List<JogoQueVem>)controller.ViewBag.JogosQueVem;
    }

    // ⚠️ O TESTE DA RECLAMAÇÃO. Com o filtro ligado, a oitava entre "1º do Grupo E" e "2º do
    // Grupo F" não é caminho de quem está no Grupo A por resultado nenhum.
    [Fact]
    public async Task Nenhum_jogo_e_so_de_outros_grupos()
    {
        var (ctx, torneio, eu) = await TorneioNosGruposAsync();

        var meus = await ProjecaoDeAsync(ctx, torneio, eu.Id, soMeus: true);

        Assert.NotEmpty(meus);
        Assert.DoesNotContain(meus, j => DeOutroGrupo(j.Lado1) && DeOutroGrupo(j.Lado2));

        static bool DeOutroGrupo(ProximasFasesDaChave.Lado lado) =>
            lado.DeQualGrupo != null && lado.DeQualGrupo != "Grupo A";
    }

    // A prova de que o recorte REALMENTE aconteceu: cada jogo mostrado ou tem uma vaga do meu
    // grupo, ou sai de um jogo que também está na lista. Se o grupo da dupla não for lido do
    // banco (o `Include`), MeusJogos volta a mostrar a chave inteira e este teste cai — que é
    // exatamente o defeito que o Felipe viu.
    [Fact]
    public async Task Todo_jogo_mostrado_sai_do_meu_grupo()
    {
        var (ctx, torneio, eu) = await TorneioNosGruposAsync();

        var meus = await ProjecaoDeAsync(ctx, torneio, eu.Id, soMeus: true);
        var mostrados = new HashSet<string>();

        Assert.NotEmpty(meus);
        foreach (var jogo in meus)
        {
            Assert.True(
                Alcancavel(jogo.Lado1) || Alcancavel(jogo.Lado2),
                $"{jogo.FaseNumerada}: {jogo.Lado1.Rotulo} × {jogo.Lado2.Rotulo} não sai do Grupo A");

            mostrados.Add(jogo.FaseNumerada);
        }

        bool Alcancavel(ProximasFasesDaChave.Lado lado) =>
            lado.DeQualGrupo == "Grupo A"
            || (lado.DeQualFase != null && mostrados.Contains($"{lado.DeQualFase} {lado.DeQualNumero}"));
    }

    // E o recorte é RECORTE: sem o filtro a chave inteira continua ali, que é o que a aba de
    // chaves e o jogador sem filtro querem ver.
    [Fact]
    public async Task Sem_o_filtro_a_chave_inteira_continua()
    {
        var (ctx, torneio, eu) = await TorneioNosGruposAsync();

        var meus = await ProjecaoDeAsync(ctx, torneio, eu.Id, soMeus: true);
        var todos = await ProjecaoDeAsync(ctx, torneio, eu.Id, soMeus: false);

        Assert.True(meus.Count < todos.Count, $"não recortou nada: {meus.Count} de {todos.Count}");
        Assert.All(meus, j => Assert.Contains(todos, t => t.FaseNumerada == j.FaseNumerada
                                                      && t.Categoria == j.Categoria));
    }
}
