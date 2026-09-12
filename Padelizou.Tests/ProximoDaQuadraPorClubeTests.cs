using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 10/09/2026 — "SEU JOGO É O PRÓXIMO" CRUZAVA OS DOIS CLUBES NO "POR ORDEM".
//
// Achado pela revisão adversarial e confirmado com reprodução: `MesmaQuadra` trata nulo≡nulo
// (regra do torneio pequeno sem quadra nomeada), então no Er — toda quadra nula, dois prédios —
// o aviso ia pro jogo mais cedo do torneio INTEIRO, inclusive do outro clube, e consumia o
// "avisa uma vez só" do jogo certo. Sem quadra dos dois lados, quem decide é o clube carimbado
// (Partida.ClubeId); com quadra nos dois, o nome, como sempre.
// No "por ordem de liberacao" a quadra e nula e o clube
// esta carimbado em Partida.ClubeId. O "seu jogo e o proximo" deveria ficar dentro do clube.
public class ProximoDaQuadraPorClubeTests
{
    private const int Er = 1, Radar = 2;
    private static readonly DateTime Sabado = new(2026, 9, 12, 8, 0, 0);
    private static int _id = 1;

    private static Partida Jogo(string status, string? quadra, DateTime? previsto, int clubeId)
    {
        var id = _id++;
        return new Partida
        {
            Id = id, Codigo = $"P{id}", Fase = "Grupo A", TorneioId = 1, Status = status,
            NomeQuadra = quadra, HorarioPrevisto = previsto, ClubeId = clubeId,
            Dupla1 = new Dupla { Jogador1Id = id * 10 + 1, Jogador2Id = id * 10 + 2 },
            Dupla2 = new Dupla { Jogador1Id = id * 10 + 3, Jogador2Id = id * 10 + 4 },
        };
    }

    [Fact]
    public void Sem_quadra_a_proxima_e_do_MESMO_clube_e_nao_a_mais_cedo_do_torneio()
    {
        // Sabado 08:50: termina no Er Padel um jogo da 3a Masc sem quadra escrita.
        var terminada = Jogo("Finalizada", null, Sabado.AddMinutes(0), Er);
        var noRadar = Jogo("Agendada", null, Sabado.AddMinutes(100), Radar); // 09:40, 6a Fem, Radar
        var noEr = Jogo("Agendada", null, Sabado.AddMinutes(150), Er);       // 10:30, Er Padel

        var escolhida = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { noRadar, noEr }, Sabado.AddMinutes(100));

        Assert.NotNull(escolhida);
        Assert.Equal(noEr.Id, escolhida!.Id);
    }

    // O aviso manda a pessoa se levantar: sem quadra, ele diz pelo menos o PRÉDIO.
    [Fact]
    public void Sem_quadra_o_texto_diz_o_clube_do_carimbo()
    {
        var sedes = SedesDoTorneio.Montar(Er, 30,
            new[] { new Quadra { Nome = "Arena 1", ClubeId = Er }, new Quadra { Nome = "Radar 1", ClubeId = Radar } },
            Array.Empty<Categoria>(),
            new Dictionary<int, string> { [Er] = "Er Padel", [Radar] = "Radar" });
        var proxima = Jogo("Agendada", null, Sabado.AddMinutes(150), Radar);

        var texto = AvisosDoDiaDeJogo.CorpoDoProximo(proxima, sedes);

        Assert.Contains("Radar", texto);
        Assert.Contains("seu jogo é o próximo", texto);
    }

    [Fact]
    public void Balcao_escreveu_a_quadra_no_jogo_terminado_e_o_aviso_ainda_sai_pra_alguem_do_clube()
    {
        // Balcao chamou o jogo na "Arena 3" pelo Controle de Placar; as agendadas continuam sem quadra.
        var terminada = Jogo("Finalizada", "Arena 3", Sabado, Er);
        var noEr = Jogo("Agendada", null, Sabado.AddMinutes(150), Er);

        var escolhida = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, new[] { noEr }, Sabado.AddMinutes(100));

        Assert.NotNull(escolhida);
    }
}
