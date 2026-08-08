using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "Terminou o torneio mas não tá com cara de campeã" (Felipe, 06/08/2026): a tabela dizia
// quem estava em primeiro e nada mais, então o fim do torneio era igualzinho ao meio.
//
// O que estes testes seguram é o CONTRÁRIO do troféu: coroar cedo é pior que não coroar.
// Líder da segunda rodada de seis não é campeão, e empate na liderança do que decide vira
// rodada de desempate — ali ainda não há campeã, há duas candidatas.
public class CampeaDoAmericanoTests
{
    private int _id = 1;

    private Jogador J(string nome) => new() { Id = _id++, Nome = nome, Email = $"{nome}@t.com" };

    private Partida Jogo(string fase, Jogador a, Jogador b, Jogador c, Jogador d,
                         int? games1 = null, int? games2 = null) => new()
    {
        Id = _id++,
        Codigo = $"T{_id}",
        Fase = fase,
        Status = games1 == null ? "Agendada" : "Finalizada",
        Dupla1 = new Dupla { Id = _id++, Jogador1 = a, Jogador1Id = a.Id, Jogador2 = b, Jogador2Id = b.Id },
        Dupla2 = new Dupla { Id = _id++, Jogador1 = c, Jogador1Id = c.Id, Jogador2 = d, Jogador2Id = d.Id },
        GamesDupla1 = games1,
        GamesDupla2 = games2,
    };

    [Fact]
    public void Grupo_unico_encerrado_e_sem_empate_tem_campea()
    {
        var (a, b, c, d) = (J("Ana"), J("Bia"), J("Cris"), J("Duda"));
        var jogos = new List<Partida>
        {
            Jogo(FaseDoAmericano.RodadaUnica(1), a, b, c, d, 6, 2),
            Jogo(FaseDoAmericano.RodadaUnica(2), a, c, b, d, 6, 3),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(jogos, passamPorGrupo: 0);
        var campea = ClassificacaoDoAmericano.Campea(tabelas);

        Assert.NotNull(campea);
        Assert.Equal(a.Id, campea!.Jogador.Id);   // 12 games, a maior soma
    }

    [Fact]
    public void Enquanto_tem_jogo_pra_rolar_nao_ha_campea()
    {
        var (a, b, c, d) = (J("Ana"), J("Bia"), J("Cris"), J("Duda"));
        var jogos = new List<Partida>
        {
            Jogo(FaseDoAmericano.RodadaUnica(1), a, b, c, d, 6, 2),
            Jogo(FaseDoAmericano.RodadaUnica(2), a, c, b, d),   // ainda não jogado
        };

        var tabelas = ClassificacaoDoAmericano.Montar(jogos, passamPorGrupo: 0);

        Assert.False(tabelas.Single().Encerrada);
        Assert.Null(ClassificacaoDoAmericano.Campea(tabelas));
    }

    // Empate na liderança do que decide o título vira rodada de desempate — coroar aqui
    // seria escolher uma das duas por critério nenhum.
    [Fact]
    public void Empate_na_lideranca_nao_coroa_ninguem()
    {
        var (a, b, c, d) = (J("Ana"), J("Bia"), J("Cris"), J("Duda"));
        var jogos = new List<Partida>
        {
            Jogo(FaseDoAmericano.RodadaUnica(1), a, b, c, d, 4, 4),
            Jogo(FaseDoAmericano.RodadaUnica(2), a, c, b, d, 4, 4),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(jogos, passamPorGrupo: 0);

        Assert.True(tabelas.Single().Encerrada);
        Assert.Null(ClassificacaoDoAmericano.Campea(tabelas));
    }

    // Com vários grupos e sem grupo final, a fase classificatória não coroa ninguém — nem
    // que os grupos tenham terminado. Quem decide é o grupo final, que ainda vai nascer.
    [Fact]
    public void Grupos_encerrados_sem_grupo_final_nao_coroam_ninguem()
    {
        var (a, b, c, d) = (J("Ana"), J("Bia"), J("Cris"), J("Duda"));
        var (e, f, g, h) = (J("Eva"), J("Fran"), J("Gi"), J("Hel"));
        var jogos = new List<Partida>
        {
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 1), a, b, c, d, 6, 2),
            Jogo(FaseDoAmericano.RodadaDeGrupo("B", 1), e, f, g, h, 6, 2),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(jogos, passamPorGrupo: 2);

        Assert.All(tabelas, t => Assert.True(t.Encerrada));
        Assert.Null(ClassificacaoDoAmericano.Campea(tabelas));
    }
}
