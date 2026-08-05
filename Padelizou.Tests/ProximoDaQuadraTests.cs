using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "A Quadra 3 vagou — próximo: Fulano × Beltrano [Começar agora]".
//
// Numa noite de 5 quadras e jogo curto, achar o próximo jogo daquela quadra era uma busca
// visual numa lista de 80 linhas, a cada 11 minutos, cinco vezes. A quadra fica parada
// justamente enquanto o organizador procura.
public class ProximoDaQuadraTests
{
    private static Partida Jogo(int id, string quadra, string hora, string status = "Agendada",
        DateTime? avisoEnviado = null) => new()
    {
        Id = id,
        Codigo = $"J{id}",
        TorneioId = 1,
        Status = status,
        NomeQuadra = quadra,
        HorarioPrevisto = DateTime.Parse($"2026-08-05 {hora}"),
        AvisoProximoEnviadoEm = avisoEnviado,
    };

    [Fact]
    public void O_proximo_e_o_primeiro_agendado_DA_MESMA_quadra()
    {
        var terminou = Jogo(1, "Quadra 3", "20:00", "Finalizada");
        var candidatas = new[]
        {
            Jogo(2, "Quadra 1", "20:11"),   // outra quadra, mesmo sendo mais cedo
            Jogo(3, "Quadra 3", "20:33"),
            Jogo(4, "Quadra 3", "20:22"),   // este
        };

        var proxima = AvisosDoDiaDeJogo.ProximaNaQuadra(terminou, candidatas);

        Assert.NotNull(proxima);
        Assert.Equal(4, proxima!.Id);
    }

    [Fact]
    public void A_TELA_sugere_de_novo_o_jogo_que_ja_recebeu_push()
    {
        // A diferença entre a sugestão e o aviso: o push não pode repetir (o jogador viria
        // correndo duas vezes), mas o organizador pode voltar nessa tela quantas vezes quiser.
        var terminou = Jogo(1, "Quadra 3", "20:00", "Finalizada");
        var jaAvisado = Jogo(2, "Quadra 3", "20:11", avisoEnviado: DateTime.Parse("2026-08-05 20:00"));

        Assert.Null(AvisosDoDiaDeJogo.ProximaAposTerminar(terminou, new[] { jaAvisado }));
        Assert.NotNull(AvisosDoDiaDeJogo.ProximaNaQuadra(terminou, new[] { jaAvisado }));
    }

    [Fact]
    public void Jogo_que_ja_esta_em_quadra_nao_e_sugerido()
    {
        var terminou = Jogo(1, "Quadra 3", "20:00", "Finalizada");
        var rolando = Jogo(2, "Quadra 3", "20:11", status: "AoVivo");

        Assert.Null(AvisosDoDiaDeJogo.ProximaNaQuadra(terminou, new[] { rolando }));
    }

    [Fact]
    public void Torneio_sem_quadra_nomeada_tambem_recebe_sugestao()
    {
        // Interno pequeno costuma não nomear quadra; sem isto a sugestão nunca sairia pra eles.
        var terminou = new Partida { Id = 1, Codigo = "A", TorneioId = 1, Status = "Finalizada" };
        var proximo = new Partida
        {
            Id = 2, Codigo = "B", TorneioId = 1, Status = "Agendada",
            HorarioPrevisto = DateTime.Parse("2026-08-05 20:11"),
        };

        Assert.Equal(2, AvisosDoDiaDeJogo.ProximaNaQuadra(terminou, new[] { proximo })?.Id);
    }

    [Fact]
    public void Sem_jogo_agendado_naquela_quadra_nao_ha_o_que_sugerir()
    {
        var terminou = Jogo(1, "Quadra 5", "22:00", "Finalizada");
        var deOutraQuadra = Jogo(2, "Quadra 1", "22:11");

        Assert.Null(AvisosDoDiaDeJogo.ProximaNaQuadra(terminou, new[] { deOutraQuadra }));
    }
}
