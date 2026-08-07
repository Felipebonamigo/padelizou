using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Como termina um Americano.
//
// A REGRA (Felipe, 06/08/2026): vence quem somou mais games; só existe rodada final se DOIS OU
// MAIS empatarem na liderança.
//
// Estes testes nasceram de um defeito achado rodando o formato inteiro no navegador: o robô
// montava uma "Final" de top-4 SEMPRE, e o torneio terminava com dois campeões diferentes — a
// tabela coroava o líder em games, a conquista do perfil coroava quem vencesse a tal final.
public class FimDoAmericanoTests
{
    private static Jogador J(int id, string nome) => new() { Id = id, Nome = nome, Cpf = $"9990000{id:0000}" };

    private static List<TabelaDoAmericano.Linha> Tabela(params (int Id, string Nome, int Games)[] linhas)
    {
        var lista = linhas
            .Select(l => new TabelaDoAmericano.Linha(J(l.Id, l.Nome), l.Games, 7, false))
            .OrderByDescending(l => l.TotalGames).ThenBy(l => l.Jogador.Id)
            .ToList();

        // O `Empatado` de verdade é calculado pelo Montar; aqui o cenário é montado à mão,
        // então marca-se quem divide a maior soma — senão o teste testaria outra coisa.
        int melhor = lista[0].TotalGames;
        int noTopo = lista.Count(l => l.TotalGames == melhor);
        return lista
            .Select(l => l with { Empatado = noTopo > 1 && l.TotalGames == melhor })
            .ToList();
    }

    // ── O caso normal: ninguém empatado ───────────────────────────────────────────────────

    [Fact]
    public void Lider_isolado_e_campeao_sem_jogar_mais_nada()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 53), (3, "Gisele", 50), (4, "Bruno", 48)),
            torneioPreveDesempate: false);

        Assert.Equal(FimDoAmericano.Desfecho.CampeaoDireto, d.Tipo);
        Assert.Equal("Ana", d.Campeao!.Nome);
        Assert.Empty(d.Empatados);
    }

    // A opção de desempate LIGADA não pode inventar partida onde não há empate — era
    // exatamente isso que o robô fazia, e foi o que deu dois campeões.
    [Fact]
    public void Lider_isolado_e_campeao_mesmo_com_desempate_ligado()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 53), (3, "Gisele", 50), (4, "Bruno", 48)),
            torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.CampeaoDireto, d.Tipo);
        Assert.Equal("Ana", d.Campeao!.Nome);
    }

    // O CASO QUE PRODUZIU O BUG: 2º e 3º empatados entre si não é empate na LIDERANÇA. Um
    // torneio assim não pode ganhar partida extra nenhuma.
    [Fact]
    public void Empate_fora_da_lideranca_nao_gera_partida()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 50), (3, "Gisele", 50), (4, "Bruno", 48)),
            torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.CampeaoDireto, d.Tipo);
        Assert.Equal("Ana", d.Campeao!.Nome);
    }

    // ── Empate de dois ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dois_empatados_com_desempate_previsto_vao_pra_quadra()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 56), (3, "Gisele", 50)),
            torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.PrecisaDesempate, d.Tipo);
        Assert.Null(d.Campeao);
        Assert.Equal(2, d.Empatados.Count);
        Assert.Contains(d.Empatados, e => e.Nome == "Ana");
        Assert.Contains(d.Empatados, e => e.Nome == "Fabio");
    }

    // Torneio que NÃO previu desempate resolve no critério do organizador — e o sistema não
    // pode escolher um dos dois por conta própria.
    [Fact]
    public void Dois_empatados_sem_desempate_previsto_ficam_com_o_organizador()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 56), (3, "Gisele", 50)),
            torneioPreveDesempate: false);

        Assert.Equal(FimDoAmericano.Desfecho.OrganizadorDecide, d.Tipo);
        Assert.Null(d.Campeao);
        Assert.Equal(2, d.Empatados.Count);
        Assert.NotNull(d.Recado);
    }

    // ── Três ou mais (decisão do Felipe, 06/08/2026: avisa e o organizador decide) ─────────

    [Fact]
    public void Tres_empatados_ficam_com_o_organizador_mesmo_com_desempate_previsto()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 56), (3, "Gisele", 56), (4, "Bruno", 48)),
            torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.OrganizadorDecide, d.Tipo);
        Assert.Null(d.Campeao);
        Assert.Equal(3, d.Empatados.Count);
        // Uma partida de padel não comporta três de lados opostos — o recado precisa dizer isso.
        Assert.Contains("mais de dois", d.Recado);
    }

    [Fact]
    public void O_recado_do_empate_cita_os_nomes()
    {
        var d = FimDoAmericano.Decidir(
            Tabela((1, "Ana", 56), (2, "Fabio", 56), (3, "Gisele", 56)),
            torneioPreveDesempate: true);

        var recado = FimDoAmericano.RecadoDoEmpate(d.Empatados, d.Recado);

        // "houve empate" sem dizer entre quem obriga o organizador a caçar na tabela.
        Assert.Contains("Ana", recado);
        Assert.Contains("Fabio", recado);
        Assert.Contains("Gisele", recado);
    }

    [Fact]
    public void Tabela_vazia_nao_coroa_ninguem()
    {
        var d = FimDoAmericano.Decidir(new List<TabelaDoAmericano.Linha>(), torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.OrganizadorDecide, d.Tipo);
        Assert.Null(d.Campeao);
    }

    // Um jogador só (categoria minúscula) não empata com ninguém.
    [Fact]
    public void Um_jogador_so_e_campeao()
    {
        var d = FimDoAmericano.Decidir(Tabela((1, "Ana", 20)), torneioPreveDesempate: true);

        Assert.Equal(FimDoAmericano.Desfecho.CampeaoDireto, d.Tipo);
        Assert.Equal("Ana", d.Campeao!.Nome);
    }
}
