using Padelizou.Services;

namespace Padelizou.Tests;

// Em quantos grupos o Americano se divide e quem passa pro grupo final (Felipe, 06/08/2026).
//
// A regra: cada um joga com cada um do seu grupo sem repetir parceiro, todos jogam a MESMA
// quantidade de jogos, e com mais de um grupo os primeiros de cada formam um grupo final que
// decide. Número que não permite isso não é aceito — e a tela diz qual número fecha.
public class DivisaoDoAmericanoTests
{
    // ── OS CASOS QUE O FELIPE DEU ─────────────────────────────────────────────────────────

    [Fact]
    public void Dez_pessoas_viram_dois_grupos_de_cinco_com_quatro_finalistas()
    {
        var divisoes = DivisaoDoAmericano.Possiveis(10);

        var dois = Assert.Single(divisoes, d => d.Grupos == 2);
        Assert.Equal(5, dois.PorGrupo);
        Assert.Equal(2, dois.PassamPorGrupo);          // "passa os 2 primeiros de cada"
        Assert.Equal(4, dois.TamanhoDoGrupoFinal);
        Assert.Equal(5, dois.RodadasNaFaseDeGrupos);
        Assert.Equal(3, dois.RodadasNoGrupoFinal);
    }

    // Com 3 grupos, passar 2 de cada daria 6 finalistas — e 6 é justamente um número que não
    // fecha. Então sobe pra 3 de cada, e o grupo final tem 9.
    [Fact]
    public void Quinze_pessoas_viram_tres_grupos_de_cinco_e_passam_tres_de_cada()
    {
        var tres = Assert.Single(DivisaoDoAmericano.Possiveis(15), d => d.Grupos == 3);

        Assert.Equal(5, tres.PorGrupo);
        Assert.Equal(3, tres.PassamPorGrupo);
        Assert.Equal(9, tres.TamanhoDoGrupoFinal);
        Assert.True(DivisaoDoAmericano.TamanhoFecha(9));
    }

    // ── O GRUPO FINAL SEMPRE FECHA ────────────────────────────────────────────────────────

    // É a garantia central: se o grupo final não fechasse, o torneio terminaria justamente na
    // fase decisiva com alguém repetindo parceiro — o que a regra proíbe.
    [Theory]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(12)] [InlineData(13)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)] [InlineData(20)]
    [InlineData(24)] [InlineData(25)] [InlineData(26)] [InlineData(27)] [InlineData(30)]
    [InlineData(32)] [InlineData(36)] [InlineData(40)]
    public void Todo_grupo_final_oferecido_fecha_certinho(int pessoas)
    {
        foreach (var d in DivisaoDoAmericano.Possiveis(pessoas).Where(x => x.TemGrupoFinal))
        {
            Assert.True(DivisaoDoAmericano.TamanhoFecha(d.TamanhoDoGrupoFinal),
                $"{pessoas} em {d.Grupos}x{d.PorGrupo}: grupo final de {d.TamanhoDoGrupoFinal} não fecha");
        }
    }

    // Toda divisão oferecida também tem os GRUPOS fechando — senão a fase classificatória já
    // nasceria com parceiro repetido.
    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(8)] [InlineData(10)] [InlineData(15)]
    [InlineData(16)] [InlineData(20)] [InlineData(24)] [InlineData(25)] [InlineData(32)]
    public void Toda_divisao_oferecida_tem_grupos_que_fecham(int pessoas)
    {
        var divisoes = DivisaoDoAmericano.Possiveis(pessoas);

        Assert.NotEmpty(divisoes);
        Assert.All(divisoes, d =>
        {
            Assert.True(DivisaoDoAmericano.TamanhoFecha(d.PorGrupo));
            Assert.Equal(pessoas, d.Pessoas);
        });
    }

    // Ninguém pode ser classificado "por comparecimento": passar o grupo inteiro não é fase
    // classificatória. É o que barra 20 pessoas em 5 grupos de 4 — passariam os 4 de cada.
    [Theory]
    [InlineData(20)]
    [InlineData(12)]
    [InlineData(16)]
    public void Nao_se_oferece_divisao_em_que_o_grupo_inteiro_passa(int pessoas)
    {
        Assert.All(DivisaoDoAmericano.Possiveis(pessoas).Where(d => d.TemGrupoFinal),
            d => Assert.True(d.PassamPorGrupo < d.PorGrupo,
                $"{pessoas} em {d.Grupos}x{d.PorGrupo}: passariam {d.PassamPorGrupo} de {d.PorGrupo}"));
    }

    // ── NÚMEROS ACEITOS E RECUSADOS ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    [InlineData(12)] [InlineData(13)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(18)] [InlineData(20)] [InlineData(21)] [InlineData(24)] [InlineData(25)]
    public void Numeros_que_o_sistema_aceita(int pessoas)
    {
        Assert.True(DivisaoDoAmericano.Aceita(pessoas));
    }

    // Estes não têm jeito: nem sozinhos nem divididos em grupos iguais. O sistema recusa em
    // vez de sortear um torneio com parceiro repetido.
    [Theory]
    [InlineData(6)] [InlineData(7)] [InlineData(11)] [InlineData(14)]
    [InlineData(19)] [InlineData(22)] [InlineData(23)]
    public void Numeros_que_o_sistema_recusa(int pessoas)
    {
        Assert.False(DivisaoDoAmericano.Aceita(pessoas));
        Assert.Empty(DivisaoDoAmericano.Possiveis(pessoas));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void Menos_de_quatro_nao_da_torneio(int pessoas)
    {
        Assert.False(DivisaoDoAmericano.Aceita(pessoas));
    }

    // ── A SAÍDA, NÃO SÓ O PROBLEMA ────────────────────────────────────────────────────────

    // "Não dá" sem dizer o que fazer manda o organizador resolver sozinho no dia do torneio.
    [Theory]
    [InlineData(6, 5, 8)]
    [InlineData(7, 5, 8)]
    [InlineData(11, 10, 12)]
    [InlineData(14, 13, 15)]
    [InlineData(19, 18, 20)]
    public void Diz_qual_numero_fecha_pra_baixo_e_pra_cima(int pessoas, int abaixo, int acima)
    {
        var (a, b) = DivisaoDoAmericano.MaisProximosQueFecham(pessoas);

        Assert.Equal(abaixo, a);
        Assert.Equal(acima, b);
    }

    [Fact]
    public void O_aviso_diz_quantos_faltam()
    {
        var aviso = DivisaoDoAmericano.PorQueNaoFecha(14);

        Assert.Contains("14", aviso);
        Assert.Contains("15", aviso);   // chame mais 1
        Assert.Contains("13", aviso);   // ou jogue com 13
    }

    [Fact]
    public void Menos_de_quatro_tem_aviso_proprio()
    {
        Assert.Contains("4 pessoas", DivisaoDoAmericano.PorQueNaoFecha(3));
    }

    // ── AS CONTAS QUE A TELA MOSTRA ───────────────────────────────────────────────────────

    // É por este número que o organizador combina a quadra com o clube — se ele mentir, o
    // torneio vira noite adentro.
    [Theory]
    [InlineData(4, 3)]      // 1 grupo de 4: 3 rodadas
    [InlineData(5, 5)]      // 1 grupo de 5: 5 rodadas (um descansa por vez)
    [InlineData(8, 7)]
    [InlineData(9, 9)]
    [InlineData(12, 11)]
    [InlineData(13, 13)]
    public void Rodadas_de_um_grupo(int tamanho, int rodadas)
    {
        Assert.Equal(rodadas, DivisaoDoAmericano.RodadasDe(tamanho));
    }

    // Em todo tamanho que fecha, cada um joga uma vez com cada um dos outros — inclusive nos
    // tamanhos com descanso, porque ali cada um descansa exatamente uma rodada.
    [Theory]
    [InlineData(4, 3)] [InlineData(5, 4)] [InlineData(8, 7)] [InlineData(9, 8)]
    [InlineData(12, 11)] [InlineData(13, 12)]
    public void Cada_um_joga_n_menos_1_jogos(int tamanho, int jogos)
    {
        Assert.Equal(jogos, DivisaoDoAmericano.JogosPorPessoa(tamanho));
    }

    // A divisão é o que faz um torneio grande caber numa noite: 20 pessoas num grupo só são
    // 19 rodadas; em 4 grupos de 5 são 5 de grupo mais 3 de final.
    [Fact]
    public void Dividir_encurta_o_torneio()
    {
        var divisoes = DivisaoDoAmericano.Possiveis(20);

        var grupoUnico = divisoes.Single(d => d.Grupos == 1);
        var quatroDeCinco = divisoes.Single(d => d.Grupos == 4);

        Assert.Equal(19, grupoUnico.RodadasTotais);

        // 4 grupos de 5 passam 2 de cada: o grupo final tem 8, que são 7 rodadas.
        Assert.Equal(8, quatroDeCinco.TamanhoDoGrupoFinal);
        Assert.Equal(5 + 7, quatroDeCinco.RodadasTotais);
        Assert.True(quatroDeCinco.RodadasTotais < grupoUnico.RodadasTotais);
    }

    // A primeira da lista é a que mais mistura (menos grupos, grupos maiores) — é ela que se
    // parece com o Americano clássico, e a tela mostra nessa ordem.
    [Fact]
    public void As_divisoes_vem_da_que_mais_mistura_pra_que_menos()
    {
        var divisoes = DivisaoDoAmericano.Possiveis(24);

        Assert.Equal(1, divisoes[0].Grupos);
        Assert.True(divisoes.Zip(divisoes.Skip(1)).All(par => par.First.Grupos < par.Second.Grupos));
    }

    [Fact]
    public void A_explicacao_da_tela_cita_grupos_rodadas_e_finalistas()
    {
        var dez = DivisaoDoAmericano.Possiveis(10).Single(d => d.Grupos == 2);

        var texto = DivisaoDoAmericano.ComoFica(dez);

        Assert.Contains("2 grupos de 5", texto);
        Assert.Contains("Passam 2", texto);
        Assert.Contains("4", texto);   // os 4 do grupo final
    }

    // ── PARTIDAS ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(8, 14)]
    [InlineData(9, 18)]
    [InlineData(12, 33)]
    public void Partidas_de_um_grupo(int tamanho, int partidas)
    {
        Assert.Equal(partidas, DivisaoDoAmericano.PartidasDe(tamanho));
    }

    [Fact]
    public void O_total_de_partidas_soma_grupos_e_final()
    {
        var dez = DivisaoDoAmericano.Possiveis(10).Single(d => d.Grupos == 2);

        // 2 grupos de 5 (5 partidas cada) + grupo final de 4 (3 partidas).
        Assert.Equal(2 * 5 + 3, dez.PartidasTotais);
    }
}
