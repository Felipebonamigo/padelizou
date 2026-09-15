using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// AS CONSULTAS QUE A ENQUETE GANHOU EM 14/09/2026 REALMENTE VIRAM SQL?
//
// São três: o texto sobre o Padelizou lido pelo torneio (ele mora em `FeedbackSite`, não na
// avaliação), o filtro de quem organiza — que passou a carregar um `IN (...)` de lista local
// pra não perder quem escreveu SÓ sobre o sistema — e a lista de quem respondeu.
//
// Mesmo buraco que TraducaoDasConsultasDePalpiteTests fecha do lado do palpitômetro: o banco
// InMemory do resto da suíte não traduz nada. Uma consulta que o Postgres recusaria passa lisa
// por milhares de testes verdes e só estoura na primeira abertura real da aba de gestão —
// aconteceu em 19/08/2026. `ToQueryString()` compila a consulta sem abrir conexão nenhuma, e
// cada uma é compilada SOZINHA, na forma exata em que o serviço a escreve.
public class TraducaoDasConsultasDaEnqueteTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void O_texto_sobre_o_Padelizou_lido_pelo_torneio_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 7;

        // Mesma forma de EnqueteDoTorneio.ParaModerarAsync (primeira consulta).
        var consulta = ctx.FeedbacksSite
            .AsNoTracking()
            .Where(f => f.TorneioId == torneioId)
            .Select(f => new { f.JogadorId, f.Texto });

        Assert.Contains("SELECT", consulta.ToQueryString());
    }

    [Fact]
    public void O_filtro_de_quem_organiza_COM_o_IN_de_quem_so_escreveu_do_sistema_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 7;
        var quemEscreveuDoSistema = new List<int> { 3, 9 };

        // Mesma forma de EnqueteDoTorneio.ParaModerarAsync + LerAsync: o `Contains` sobre
        // lista local (que vira `IN (...)`) somado à navegação pro Jogador na projeção.
        var consulta = ctx.AvaliacoesDeTorneio
            .AsNoTracking()
            .Where(a => a.TorneioId == torneioId
                     && (a.ComentarioClube != null || a.ComentarioOrganizacao != null
                         || quemEscreveuDoSistema.Contains(a.JogadorId)))
            .OrderByDescending(a => a.CriadoEm)
            .Take(200)
            .Select(a => new
            {
                a.Id,
                a.TorneioId,
                TorneioNome = a.Torneio.Nome,
                a.NotaClube,
                a.NotaOrganizacao,
                a.NotaSistema,
                a.ComentarioClube,
                a.ComentarioOrganizacao,
                a.Anonimo,
                a.PublicadoEm,
                a.CriadoEm,
                AutorNome = a.Jogador.Nome,
                AutorApelido = a.Jogador.Apelido,
                AutorFoto = a.Jogador.FotoPerfil,
                AutorId = a.JogadorId,
            });

        Assert.Contains("SELECT", consulta.ToQueryString());
    }

    [Fact]
    public void A_lista_de_quem_respondeu_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        int torneioId = 7;

        // Mesma forma de EnqueteDoTorneio.QuemAvaliouAsync.
        var consulta = ctx.AvaliacoesDeTorneio
            .AsNoTracking()
            .Where(a => a.TorneioId == torneioId)
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => new
            {
                a.NotaClube,
                a.NotaOrganizacao,
                a.NotaSistema,
                a.Anonimo,
                a.CriadoEm,
                AutorId = a.JogadorId,
                AutorNome = a.Jogador.Nome,
                AutorApelido = a.Jogador.Apelido,
                AutorFoto = a.Jogador.FotoPerfil,
            });

        Assert.Contains("SELECT", consulta.ToQueryString());
    }
}
