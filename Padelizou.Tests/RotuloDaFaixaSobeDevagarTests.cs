using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 09/11/2026 — O RÓTULO SOBE MAIS DEVAGAR, E A CATEGORIA DE REFERÊNCIA É A JOGADA.
//
// 🗣️ Felipe, com o print do Padelímetro: *"deivid não é 3ª, é 4ª, precisamos melhorar"* · *"demora
// mais para as pessoas trocarem de categoria, não é tão rápido, às vezes ficam ano na mesma"*.
// Escolha dele: folga de SUBIDA 50 → 100 (descida fica 50) e categoria de referência só de
// torneio já jogado, com empate de data indo pra mais forte.
//
// 🕳️ O caso real: Deivid Santos, 701 PDZ, 11 jogos, joga a 4ª (550–649). Com folga de 50 a porta
// de subida era 699 — passou por 2 pontos, depois de +123 em dois torneios de outubro, sem
// título nenhum acima da 4ª (chegou à final dela).
public class RotuloDaFaixaSobeDevagarTests
{
    private const string Terceira = "3ª Categoria Masculina";   // 650–749
    private const string Quarta = "4ª Categoria Masculina";     // 550–649
    private const string Quinta = "5ª Categoria Masculina";     // 450–549

    private static string Rotulo(int pdz, string? categoria, int jogos) =>
        FaixasDePadelimetro.FaixaExibida(pdz, categoria, jogos).Rotulo;

    [Fact]
    public void Deivid_701_PDZ_com_11_jogos_na_4a_segue_4a()
    {
        Assert.Equal("4ª", Rotulo(701, Quarta, jogos: 11));
    }

    [Fact]
    public void Subir_pede_100_acima_do_teto___descer_continua_com_50()
    {
        const int onzeJogos = 11;
        Assert.Equal("4ª", Rotulo(749, Quarta, onzeJogos));   // teto+100 ainda é 4ª
        Assert.Equal("3ª", Rotulo(750, Quarta, onzeJogos));   // passou → sobe
        Assert.Equal("4ª", Rotulo(500, Quarta, onzeJogos));   // piso−50 ainda é 4ª
        Assert.Equal("5ª", Rotulo(499, Quarta, onzeJogos));   // passou → desce
    }

    [Fact]
    public void Passar_a_porta_sobe_UM_degrau___nao_pula_faixa()
    {
        // A folga (100) é a largura de uma faixa: usar a faixa CRUA ao passar a porta faria 750
        // virar 2ª direto. Cada degrau acima pede 100 além do teto DELE.
        const int onzeJogos = 11;
        Assert.Equal("3ª", Rotulo(849, Quarta, onzeJogos));
        Assert.Equal("2ª", Rotulo(850, Quarta, onzeJogos));
        Assert.Equal("Open", Rotulo(950, Quarta, onzeJogos));
    }

    [Fact]
    public void O_faltam_X_mede_a_porta_nova()
    {
        // 690 na 4ª: o rótulo só vira 3ª em 750.
        Assert.Equal(60, FaixasDePadelimetro.FaltaPraMudarDeFaixa(690, Quarta, jogos: 11));
        // Já mostrando 3ª (760 na 4ª): o próximo degrau é a 2ª, em 850.
        Assert.Equal(90, FaixasDePadelimetro.FaltaPraMudarDeFaixa(760, Quarta, jogos: 11));
        // Mostrando 5ª depois de cair (480 na 4ª): volta pra 4ª em 500.
        Assert.Equal(20, FaixasDePadelimetro.FaltaPraMudarDeFaixa(480, Quarta, jogos: 11));
    }

    // ── A categoria de referência ────────────────────────────────────────────────────────

    private static readonly DateTime Hoje = new(2026, 11, 9);

    private static string? Referencia(params (string? Nome, DateTime? Data)[] inscricoes) =>
        FaixasDePadelimetro.CategoriaDeReferencia(inscricoes, Hoje);

    [Fact]
    public void Inscricao_em_torneio_que_ainda_nao_aconteceu_nao_conta()
    {
        // O Deivid está inscrito na 3ª em 20/11: se inscrever não é jogar.
        Assert.Equal(Quarta, Referencia(
            (Terceira, new DateTime(2026, 11, 20)),
            (Quarta, new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void Torneio_que_comeca_hoje_ja_conta()
    {
        Assert.Equal(Terceira, Referencia(
            (Terceira, Hoje),
            (Quarta, new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void Duas_categorias_no_mesmo_torneio_vale_a_mais_forte()
    {
        // 10/10: o Deivid jogou a 4ª e a 5ª no mesmo fim de semana.
        Assert.Equal(Quarta, Referencia(
            (Quinta, new DateTime(2026, 10, 10)),
            (Quarta, new DateTime(2026, 10, 10))));
        Assert.Equal(Quarta, Referencia(
            (Quarta, new DateTime(2026, 10, 10)),
            (Quinta, new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void Mista_e_fora_da_escada_continuam_de_fora()
    {
        Assert.Equal(Quarta, Referencia(
            ("Mista A", new DateTime(2026, 11, 1)),
            (Quarta, new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void So_inscricao_futura_ou_nenhuma_da_nulo()
    {
        Assert.Null(Referencia((Terceira, new DateTime(2026, 12, 4))));
        Assert.Null(Referencia());
    }

    [Fact]
    public void Torneio_sem_data_so_vale_quando_nao_ha_outro()
    {
        Assert.Equal(Quarta, Referencia(
            (Terceira, null),
            (Quarta, new DateTime(2026, 10, 10))));
        Assert.Equal(Terceira, Referencia((Terceira, null)));
    }

    // ── De ponta a ponta: a aba Padelímetro ──────────────────────────────────────────────

    [Fact]
    public async Task No_ranking_o_Deivid_aparece_4a_mesmo_inscrito_na_3a_que_ainda_vai_acontecer()
    {
        using var ctx = TestInfra.NovoContexto();
        var deivid = new Jogador { Nome = "Deivid Santos", Cpf = "88800000101", Padelimetro = 701, JogosDePadelimetro = 11 };
        var parceiro = new Jogador { Nome = "Parceiro", Cpf = "88800000102" };
        ctx.Jogadores.AddRange(deivid, parceiro);

        Categoria Categoria(string nome, DateTime data, int n) => new()
        {
            Nome = nome, Codigo = $"C{n:00}",
            Torneio = new Torneio { Nome = $"T{n}", Codigo = $"TRN{n:00}", Status = "Inscrições", DataInicio = data },
        };
        ctx.Duplas.AddRange(
            new Dupla { Categoria = Categoria(Quarta, DateTime.Today.AddDays(-30), 1), Jogador1 = deivid, Jogador2 = parceiro },
            new Dupla { Categoria = Categoria(Terceira, DateTime.Today.AddDays(11), 2), Jogador1 = deivid, Jogador2 = parceiro });
        await ctx.SaveChangesAsync();

        var lista = await new PadelimetroService(ctx).ListarRankingAsync(null);

        Assert.Equal("4ª", Assert.Single(lista).FaixaRotulo);
    }
}
