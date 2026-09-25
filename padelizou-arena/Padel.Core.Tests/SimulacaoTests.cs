namespace Padel.Core.Tests;

/// <summary>Roda partidas inteiras com os quatro jogadores na IA, em tempo simulado, com semente fixa.</summary>
public class SimulacaoTests
{
    private static (Partida partida, float segundos) JogarAteOFim(OpcoesDaPartida opcoes, float limiteDeSegundos = 3600)
    {
        var partida = new Partida(opcoes);
        const float passo = 1f / 120f;
        float t = 0;
        var bola = partida.Bola;
        while (!partida.Acabou && t < limiteDeSegundos)
        {
            partida.Avancar(passo);
            t += passo;
            Assert.True(float.IsFinite(bola.X) && float.IsFinite(bola.Y) && float.IsFinite(bola.Z), "bola com NaN");
            Assert.True(MathF.Abs(bola.X) <= 5.01f && MathF.Abs(bola.Y) <= 10.01f, $"bola fora da quadra em ({bola.X}, {bola.Y})");
            foreach (var j in partida.Jogadores)
            {
                Assert.True(MathF.Abs(j.X) <= 4.7f && MathF.Abs(j.Y) <= 9.7f, $"jogador fora da quadra: {j.Nome}");
                Assert.True(Quadra.LadoDe(j.Y) == j.Lado, $"{j.Nome} atravessou a rede");
            }
        }
        return (partida, t);
    }

    private static OpcoesDaPartida SoIA(uint semente, Dificuldade d = Dificuldade.Medio, int sets = 1) =>
        new() { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = d, SetsParaVencer = sets };

    [Fact]
    public void Uma_partida_de_um_set_entre_IAs_termina_com_pontos_de_todo_tipo()
    {
        var (partida, segundos) = JogarAteOFim(SoIA(42));
        Assert.True(partida.Acabou, $"a partida não terminou em {segundos:F0} s simulados");
        Assert.True(partida.Placar.Vencedor is 0 or 1);
        var e = partida.Estatisticas;
        Assert.True(e.Pontos >= 24, $"poucos pontos: {e.Pontos}");
        Assert.True(e.MaiorRally >= 4, $"nenhum rally de verdade: maior foi {e.MaiorRally}");
        Assert.True(e.Motivos.GetValueOrDefault(Motivo.DoisQuiques) > 0, "ninguém venceu ponto por dois quiques");
        Assert.False(e.Motivos.ContainsKey(Motivo.BolaMorta), "bola morta é o caminho de segurança e não devia acontecer");
        Assert.True(segundos / e.Pontos < 60, $"pontos lentos demais: {segundos / e.Pontos:F1} s por ponto");
    }

    [Fact]
    public void A_IA_saca_dentro_da_caixa_na_maior_parte_das_vezes()
    {
        var (partida, _) = JogarAteOFim(SoIA(42, Dificuldade.Dificil));
        var e = partida.Estatisticas;
        int saques = e.Pontos + e.Faltas + e.Lets;
        Assert.True(e.Faltas < saques * 0.25f, $"faltas demais: {e.Faltas} em {saques} saques");
    }

    [Fact]
    public void O_placar_da_simulacao_bate_com_os_eventos_emitidos()
    {
        var partida = new Partida(SoIA(7));
        var contagem = new Dictionary<TipoDeEventoDaPartida, int>();
        partida.Evento += ev => contagem[ev.Tipo] = contagem.GetValueOrDefault(ev.Tipo) + 1;
        float t = 0;
        while (!partida.Acabou && t < 3600) { partida.Avancar(1f / 120f); t += 1f / 120f; }
        int C(TipoDeEventoDaPartida tipo) => contagem.GetValueOrDefault(tipo);
        int pontosDecididos = C(TipoDeEventoDaPartida.Ponto) + C(TipoDeEventoDaPartida.Game) + C(TipoDeEventoDaPartida.Set) + C(TipoDeEventoDaPartida.Partida);
        Assert.Equal(partida.Estatisticas.Pontos, pontosDecididos);
        Assert.Equal(1, C(TipoDeEventoDaPartida.Partida));
        Assert.Equal(1, C(TipoDeEventoDaPartida.Fim));
        Assert.Equal(partida.Estatisticas.Golpes, C(TipoDeEventoDaPartida.Golpe));
        var games = partida.Placar.SetsAnteriores[0].Games;
        Assert.Equal(games[0] + games[1], C(TipoDeEventoDaPartida.Game) + C(TipoDeEventoDaPartida.Partida) + C(TipoDeEventoDaPartida.Set));
    }

    [Fact]
    public void A_mesma_semente_reproduz_a_mesma_partida()
    {
        var a = JogarAteOFim(SoIA(99), 1200).partida;
        var b = JogarAteOFim(SoIA(99), 1200).partida;
        Assert.Equal(a.Placar.Resumo(), b.Placar.Resumo());
        Assert.Equal(a.Estatisticas.Pontos, b.Estatisticas.Pontos);
        Assert.Equal(a.Estatisticas.Golpes, b.Estatisticas.Golpes);
        Assert.Equal(a.Bola.X, b.Bola.X);
    }

    [Fact]
    public void A_dificuldade_gradua_o_facil_perde_e_o_dificil_vence_o_parceiro()
    {
        var facil = JogarAteOFim(SoIA(42, Dificuldade.Facil)).partida;
        var dificil = JogarAteOFim(SoIA(42, Dificuldade.Dificil)).partida;
        Assert.Equal(0, facil.Placar.Vencedor);
        Assert.Equal(1, dificil.Placar.Vencedor);
    }

    [Fact]
    public void O_humano_parado_perde_pontos_mas_o_parceiro_cobre_parte_deles()
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 3, Dificuldade = Dificuldade.Facil });
        float t = 0;
        while (!partida.Acabou && t < 900) { partida.Avancar(1f / 120f); t += 1f / 120f; }
        Assert.True(partida.Estatisticas.Pontos > 10, "com o humano parado o jogo devia seguir sozinho (saque automático)");
        Assert.True(partida.Jogadores[1].Golpes > 0, "o parceiro nunca bateu na bola");
    }

    [Fact]
    public void Um_humano_de_cada_lado_manda_entrada_no_proprio_referencial()
    {
        // Dois humanos: o da casa (lado +1) e o rival da metade direita (lado -1). Os dois apertam "pra frente" (Dy < 0):
        // o da casa anda pra -y, o de cima pra +y — os dois rumo à rede.
        var partida = new Partida(new OpcoesDaPartida { Semente = 5, Humanos = [true, false, true, false] });
        partida.Avancar(1f / 120f, new[] { new Entrada(0, 0, true, false), Entrada.Vazia, Entrada.Vazia, Entrada.Vazia });   // saca
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        float yCasa = partida.Jogadores[0].Y, yRival = partida.Jogadores[2].Y;
        var frente = new Entrada(0, -1, false, false);
        for (int i = 0; i < 30; i++) partida.Avancar(1f / 120f, new[] { frente, Entrada.Vazia, frente, Entrada.Vazia });
        Assert.True(partida.Jogadores[0].Y < yCasa, "o humano da casa devia ter andado rumo à rede (-y)");
        Assert.True(partida.Jogadores[2].Y > yRival, "o humano rival devia ter andado rumo à rede (+y)");
    }
}
