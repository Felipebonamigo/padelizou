namespace Padel.Core.Tests;

/// <summary>O humano simulado joga só pela Entrada, como uma pessoa no controle (modo Manual).</summary>
public class HumanoSimuladoTests
{
    private const float Passo = 1f / 120f;

    private sealed class Registro
    {
        public int Golpes, PontosDaCasa, Pontos;
        public List<float> TempoNoBalancoNoContato { get; } = [];
        public bool EntradaForaDaFaixa;
    }

    /// <summary>Humano simulado no jogador 0 (com parceiro IA) contra a dupla de IA, por até `segundos` de jogo ou até acabar.</summary>
    private static (Partida partida, Registro registro) Jogar(PerfilDeHumano perfil, Dificuldade rival, uint semente, float segundos = 1800)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = rival, Humanos = [true, false, false, false] });
        var humano = new HumanoSimulado(0, perfil, new Aleatorio(semente * 31u + 7u));
        var r = new Registro();
        partida.Evento += ev =>
        {
            if (ev.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
            {
                r.Pontos++;
                if (ev.Time == 0) r.PontosDaCasa++;
            }
            if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == partida.Jogadores[0] && ev.Golpe != TipoDeGolpe.Saque)
            {
                r.Golpes++;
                r.TempoNoBalancoNoContato.Add(partida.Jogadores[0].TempoNoBalanco);
            }
        };
        var entradas = new Entrada[4];
        for (float t = 0; t < segundos && !partida.Acabou; t += Passo)
        {
            var e = humano.Decidir(EstadoVisivel.De(partida), Passo);
            if (MathF.Abs(e.Dx) > 1 || MathF.Abs(e.Dy) > 1 || float.IsNaN(e.Dx) || float.IsNaN(e.Dy)) r.EntradaForaDaFaixa = true;
            entradas[0] = e;
            partida.Avancar(Passo, entradas);
        }
        return (partida, r);
    }

    [Fact]
    public void O_profissional_com_parceiro_vence_a_dupla_facil()
    {
        var (partida, r) = Jogar(PerfilDeHumano.Profissional, Dificuldade.Facil, semente: 1000);
        Assert.True(partida.Acabou, "a partida devia terminar");
        Assert.Equal(0, partida.Placar.Vencedor);
        Assert.True(r.PontosDaCasa > r.Pontos / 2, $"pontos da casa: {r.PontosDaCasa} de {r.Pontos}");
    }

    [Fact]
    public void O_iniciante_ganha_menos_pontos_contra_a_dificil_do_que_o_profissional()
    {
        int iniciante = 0, profissional = 0, total1 = 0, total2 = 0;
        foreach (uint semente in new uint[] { 1000, 1001, 1002 })
        {
            var (_, a) = Jogar(PerfilDeHumano.Iniciante, Dificuldade.Dificil, semente, segundos: 400);
            var (_, b) = Jogar(PerfilDeHumano.Profissional, Dificuldade.Dificil, semente, segundos: 400);
            iniciante += a.PontosDaCasa; total1 += a.Pontos;
            profissional += b.PontosDaCasa; total2 += b.Pontos;
        }
        float pi = (float)iniciante / total1, pp = (float)profissional / total2;
        Assert.True(pi < 0.5f, $"o iniciante não devia ganhar a maioria dos pontos contra a Difícil: {pi:P0}");
        Assert.True(pp > pi, $"o profissional ({pp:P0}) devia ganhar mais pontos que o iniciante ({pi:P0})");
    }

    [Fact]
    public void A_entrada_fica_sempre_entre_menos_um_e_um_e_ele_bate_na_bola()
    {
        var (partida, r) = Jogar(PerfilDeHumano.Intermediario, Dificuldade.Medio, semente: 1001, segundos: 300);
        Assert.False(r.EntradaForaDaFaixa);
        Assert.True(r.Golpes >= 10, $"o humano simulado bateu só {r.Golpes} vezes em 300 s");
        Assert.True(partida.Estatisticas.Pontos >= 10);
    }

    [Fact]
    public void O_iniciante_erra_balanco_as_vezes()
    {
        var (partida, _) = Jogar(PerfilDeHumano.Iniciante, Dificuldade.Medio, semente: 1002, segundos: 400);
        Assert.True(partida.Jogadores[0].BalancosNoAr > 0, "quem começa devia balançar no ar de vez em quando");
    }

    [Fact]
    public void A_mesma_semente_da_a_mesma_partida()
    {
        var (a, ra) = Jogar(PerfilDeHumano.Avancado, Dificuldade.Medio, semente: 77, segundos: 300);
        var (b, rb) = Jogar(PerfilDeHumano.Avancado, Dificuldade.Medio, semente: 77, segundos: 300);
        Assert.Equal(a.Placar.Resumo(), b.Placar.Resumo());
        Assert.Equal(ra.Golpes, rb.Golpes);
        Assert.Equal(a.Bola.X, b.Bola.X);
        Assert.Equal(a.Jogadores[0].X, b.Jogadores[0].X);
    }

    /// <summary>
    /// Com timing quase perfeito, o contato sai perto do momento ideal do balanço. Trava um defeito real: o humano
    /// simulado cronometrava o aperto pela chegada da bola ao ponto ao lado do corpo, mas a raquete bate quando a bola
    /// ENTRA no alcance confortável — e 72 de 115 contatos saíam a menos de 0,04 s do aperto (o ideal é 0,12 s).
    /// </summary>
    [Fact]
    public void Com_timing_quase_perfeito_o_contato_sai_perto_do_momento_ideal_do_balanco()
    {
        var perfil = PerfilDeHumano.Profissional with { DesvioDoTempo = 0.001f };
        var tempos = new List<float>();
        foreach (uint semente in new uint[] { 1000, 1001 })
            tempos.AddRange(Jogar(perfil, Dificuldade.Medio, semente, segundos: 300).registro.TempoNoBalancoNoContato);
        Assert.True(tempos.Count >= 20, $"poucos golpes pra medir: {tempos.Count}");
        tempos.Sort();
        float mediana = tempos[tempos.Count / 2];
        Assert.InRange(mediana, Jogador.MomentoIdealDoBalanco - 0.04f, Jogador.MomentoIdealDoBalanco + 0.04f);
    }
}
