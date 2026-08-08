using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O LINK DA CÂMERA É DA QUADRA, NÃO DO JOGO (Felipe, 08/08/2026).
//
// A câmera fica pendurada na quadra e transmite o dia inteiro. Gravar o link em cada partida
// dava dois problemas em cima do mesmo engano: o jogo que nascia depois do "usar este link em
// todos os próximos jogos desta quadra" chegava sem transmissão nenhuma, e o jogo que MUDAVA
// de quadra levava a câmera da quadra velha no bolso — quem abrisse a transmissão veria outra
// partida, ao vivo, achando que era esta.
public class TransmissaoDaQuadraTests
{
    private static Partida Jogo(int id, string? quadra, string? link, string status = "Agendada") => new()
    {
        Id = id,
        Codigo = $"J{id}",
        TorneioId = 1,
        Status = status,
        NomeQuadra = quadra,
        LinkTransmissao = link,
        Dupla1Id = id * 10,
        Dupla2Id = id * 10 + 1,
    };

    [Fact]
    public void A_quadra_conhece_a_camera_dela()
    {
        var jogos = new[]
        {
            Jogo(1, "Quadra 1", "https://youtu.be/UM", "AoVivo"),
            Jogo(2, "Quadra 2", "https://youtu.be/DOIS", "AoVivo"),
            Jogo(3, "Quadra 1", null),                 // o jogo novo, ainda sem link
        };

        var cameras = TransmissaoDaQuadra.PorQuadra(jogos);

        Assert.Equal("https://youtu.be/UM", cameras["Quadra 1"]);
        Assert.Equal("https://youtu.be/DOIS", cameras["Quadra 2"]);
        Assert.Equal("https://youtu.be/UM", TransmissaoDaQuadra.De("Quadra 1", jogos));
    }

    [Fact]
    public void Quadra_sem_transmissao_nenhuma_fica_de_fora()
    {
        // Ausência é resposta. Uma entrada vazia no mapa viraria "apague o que você digitou".
        var jogos = new[] { Jogo(1, "Quadra 3", null), Jogo(2, "Quadra 3", "  ") };

        Assert.Empty(TransmissaoDaQuadra.PorQuadra(jogos));
        Assert.Null(TransmissaoDaQuadra.De("Quadra 3", jogos));
        Assert.Null(TransmissaoDaQuadra.De(null, jogos));
    }

    [Fact]
    public void Com_links_diferentes_na_mesma_quadra_vale_o_que_esta_em_quadra_agora()
    {
        // A transmissão trocou de canal no meio do dia. "Qual vale" não pode depender da ordem
        // em que o banco devolveu as linhas: vale quem está jogando AGORA.
        var jogos = new[]
        {
            Jogo(9, "Quadra 1", "https://youtu.be/NOVO", "Agendada"),
            Jogo(1, "Quadra 1", "https://youtu.be/AGORA", "AoVivo"),
            Jogo(5, "Quadra 1", "https://youtu.be/VELHO", "Finalizada"),
        };

        Assert.Equal("https://youtu.be/AGORA", TransmissaoDaQuadra.De("Quadra 1", jogos));
    }

    [Fact]
    public void Sem_ninguem_em_quadra_vale_o_que_esta_por_vir_e_o_historico_e_o_ultimo_recurso()
    {
        var comAgendada = new[]
        {
            Jogo(5, "Quadra 1", "https://youtu.be/VELHO", "Finalizada"),
            Jogo(9, "Quadra 1", "https://youtu.be/NOVO", "Agendada"),
        };
        Assert.Equal("https://youtu.be/NOVO", TransmissaoDaQuadra.De("Quadra 1", comAgendada));

        var soHistorico = new[]
        {
            Jogo(2, "Quadra 1", "https://youtu.be/ANTIGO", "Finalizada"),
            Jogo(7, "Quadra 1", "https://youtu.be/ULTIMO", "Finalizada"),
        };
        // Empate de prioridade se resolve pelo jogo mais novo — ordenação TOTAL, sem sobrar
        // pergunta pro acaso.
        Assert.Equal("https://youtu.be/ULTIMO", TransmissaoDaQuadra.De("Quadra 1", soHistorico));
    }

    [Fact]
    public void Mudar_de_quadra_troca_a_camera_junto()
    {
        var daUm = Jogo(1, "Quadra 1", "https://youtu.be/UM", "AoVivo");
        var daDois = Jogo(2, "Quadra 2", "https://youtu.be/DOIS", "AoVivo");
        var jogos = new[] { daUm, daDois };
        var cameras = TransmissaoDaQuadra.PorQuadra(jogos);

        TrocaDeQuadra.Mudar(daUm, "Quadra 2", null, cameras);

        Assert.Equal("Quadra 2", daUm.NomeQuadra);
        Assert.Equal("https://youtu.be/DOIS", daUm.LinkTransmissao);   // a câmera ficou na quadra
        Assert.True(daUm.SendoTransmitida);
    }

    [Fact]
    public void Quadra_de_destino_sem_camera_deixa_o_jogo_sem_link()
    {
        // Errado calado é pior que vazio: manter o link da Quadra 1 aqui faria o público
        // assistir a OUTRA partida achando que era esta.
        var jogo = Jogo(1, "Quadra 1", "https://youtu.be/UM", "AoVivo");
        var cameras = TransmissaoDaQuadra.PorQuadra(new[] { jogo });

        TrocaDeQuadra.Mudar(jogo, "Quadra 3", null, cameras);

        Assert.Null(jogo.LinkTransmissao);
        Assert.False(jogo.SendoTransmitida);
    }

    [Fact]
    public void Quando_os_dois_jogos_trocam_de_quadra_cada_um_assume_a_camera_de_onde_chegou()
    {
        var daUm = Jogo(1, "Quadra 1", "https://youtu.be/UM", "AoVivo");
        var daDois = Jogo(2, "Quadra 2", "https://youtu.be/DOIS", "AoVivo");
        var cameras = TransmissaoDaQuadra.PorQuadra(new[] { daUm, daDois });

        TrocaDeQuadra.Mudar(daUm, "Quadra 2", daDois, cameras);

        Assert.Equal("Quadra 2", daUm.NomeQuadra);
        Assert.Equal("https://youtu.be/DOIS", daUm.LinkTransmissao);
        Assert.Equal("Quadra 1", daDois.NomeQuadra);
        Assert.Equal("https://youtu.be/UM", daDois.LinkTransmissao);
    }

    [Fact]
    public void Sem_o_mapa_de_cameras_a_troca_de_quadra_nao_encosta_no_link()
    {
        // Quem chama sem o mapa (teste antigo, chamada de outro fluxo) continua com o
        // comportamento de sempre — a mudança de câmera é opt-in de quem conhece o torneio.
        var jogo = Jogo(1, "Quadra 1", "https://youtu.be/UM", "AoVivo");

        TrocaDeQuadra.Mudar(jogo, "Quadra 2", null);

        Assert.Equal("Quadra 2", jogo.NomeQuadra);
        Assert.Equal("https://youtu.be/UM", jogo.LinkTransmissao);
    }
}
