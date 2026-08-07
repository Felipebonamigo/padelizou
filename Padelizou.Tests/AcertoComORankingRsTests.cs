using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O que o Padelizou deve ao Ranking RS: R$ 1 por PESSOA, não por inscrição.
//
// A armadilha que estes testes existem pra trancar: a régua PARECE a mesma da taxa dos 5%
// (TaxaDoTorneioExterno.PessoasInscritas) e não é. Lá se cobra sobre o dinheiro que entrou,
// então quem se inscreveu em duas categorias pagou duas vezes e conta duas. Aqui a unidade é
// a pessoa — reaproveitar a outra contagem pagaria a mais em todo torneio com gente repetida.
public class AcertoComORankingRsTests
{
    private static readonly Dictionary<int, string> Categorias = new()
    {
        [1] = "4ª Masculina",
        [2] = "6ª Masculina",
        [3] = "Mista",
    };

    private static readonly Dictionary<int, string> Jogadores = new()
    {
        [10] = "Ana Souza",
        [11] = "Bruno Lima",
        [12] = "Carla Dias",
        [13] = "Diego Alves",
    };

    private static Dupla Dupla(int categoriaId, int j1, int? j2 = null,
        bool espera = false, string? nomeTime = null) => new()
    {
        CategoriaId = categoriaId,
        Jogador1Id = j1,
        Jogador2Id = j2,
        EmListaDeEspera = espera,
        NomeTime = nomeTime,
    };

    private static List<AcertoComORankingRs.PessoaCobrada> Cobradas(
        IEnumerable<Dupla> duplas, IEnumerable<InscricaoAmericana>? americanas = null) =>
        AcertoComORankingRs.Cobradas(AcertoComORankingRs.Achatar(
            duplas, americanas ?? Array.Empty<InscricaoAmericana>(), Categorias, Jogadores));

    [Fact]
    public void Quem_joga_duas_categorias_custa_um_real_so()
    {
        // Ana e Bruno na 4ª; Ana e Carla na 6ª. São 4 inscrições e TRÊS pessoas.
        var cobradas = Cobradas(new[]
        {
            Dupla(1, 10, 11),
            Dupla(2, 10, 12),
        });

        Assert.Equal(3, cobradas.Count);
        Assert.Equal(3m, AcertoComORankingRs.Valor(cobradas.Count, 1m));

        // E a Ana aparece uma vez só, com as duas categorias juntas — é o que a tela de
        // detalhe mostra pra conferência não virar discussão.
        var ana = Assert.Single(cobradas, p => p.JogadorId == 10);
        Assert.Equal(new[] { "4ª Masculina", "6ª Masculina" }, ana.Categorias);
    }

    [Fact]
    public void Somar_inscricoes_em_vez_de_pessoas_pagaria_a_mais()
    {
        // O mesmo cenário pela régua da taxa dos 5%: 4 inscrições. Este teste existe pra
        // deixar a diferença explícita — se alguém "unificar" as duas contagens um dia, é aqui
        // que o erro aparece.
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 10, 12) };

        int pelaReguaDoDinheiro = TaxaDoTorneioExterno.PessoasInscritas(
            duplas, Array.Empty<InscricaoAmericana>());

        Assert.Equal(4, pelaReguaDoDinheiro);
        Assert.Equal(3, Cobradas(duplas).Count);
    }

    [Fact]
    public void Lista_de_espera_e_time_ficam_de_fora()
    {
        var cobradas = Cobradas(new[]
        {
            Dupla(1, 10, 11),
            Dupla(1, 12, 13, espera: true),          // não joga
            Dupla(2, 12, nomeTime: "Nata Padel"),    // Jogador1Id de time é o ORGANIZADOR
        });

        Assert.Equal(2, cobradas.Count);
        Assert.DoesNotContain(cobradas, p => p.JogadorId == 12);
        Assert.DoesNotContain(cobradas, p => p.JogadorId == 13);
    }

    [Fact]
    public void Dupla_sem_parceiro_conta_uma_pessoa()
    {
        // Cobrar pela vaga vazia seria cobrar por quem ainda não existe.
        var cobradas = Cobradas(new[] { Dupla(1, 10) });

        Assert.Single(cobradas);
        Assert.Equal(10, cobradas[0].JogadorId);
    }

    [Fact]
    public void Americano_entra_junto_e_a_mesma_pessoa_nao_dobra()
    {
        // Ana na dupla da 4ª e também inscrita no americano da Mista: uma pessoa, duas
        // categorias, um real.
        var cobradas = Cobradas(
            new[] { Dupla(1, 10, 11) },
            new[]
            {
                new InscricaoAmericana { CategoriaId = 3, JogadorId = 10 },
                new InscricaoAmericana { CategoriaId = 3, JogadorId = 12, EmListaDeEspera = true },
            });

        Assert.Equal(2, cobradas.Count);
        var ana = Assert.Single(cobradas, p => p.JogadorId == 10);
        Assert.Equal(new[] { "4ª Masculina", "Mista" }, ana.Categorias);
    }

    [Fact]
    public void Chave_direta_repete_a_pessoa_e_isso_nao_cobra_de_novo()
    {
        // A chave direta remonta os MESMOS jogadores em outros pares. Agrupar por pessoa
        // dissolve o problema sozinho — sem precisar do filtro que a taxa dos 5% mantém na
        // query. A categoria repetida também não aparece duas vezes na linha.
        var cobradas = Cobradas(new[]
        {
            Dupla(1, 10, 11),
            Dupla(1, 10, 12),   // mesma categoria, outro par
        });

        Assert.Equal(3, cobradas.Count);
        var ana = Assert.Single(cobradas, p => p.JogadorId == 10);
        Assert.Equal(new[] { "4ª Masculina" }, ana.Categorias);
    }

    [Fact]
    public void Sem_ninguem_nao_ha_o_que_pagar()
    {
        Assert.Empty(Cobradas(Array.Empty<Dupla>()));
        Assert.Equal(0m, AcertoComORankingRs.Valor(0, 1m));
    }

    [Fact]
    public void Preco_diferente_de_um_real_continua_fechando()
    {
        // O custo é de tabela e pode ser renegociado — inclusive pra centavos.
        Assert.Equal(37.50m, AcertoComORankingRs.Valor(25, 1.50m));
        Assert.Equal(2.25m, AcertoComORankingRs.Valor(3, 0.75m));
    }

    // ── As guardas do acerto ──────────────────────────────────────────────────────────────

    [Fact]
    public void Torneio_que_nao_usa_o_ranking_nao_deve_nada()
    {
        var semRanking = new Torneio { Nome = "T", Codigo = "T1", ValidarPeloRankingRs = false };
        Assert.NotNull(AcertoComORankingRs.ProblemaParaAcertar(semRanking, 10, null));
    }

    [Fact]
    public void Nao_acerta_duas_vezes_o_mesmo_torneio()
    {
        var torneio = new Torneio { Nome = "T", Codigo = "T1", ValidarPeloRankingRs = true };
        var jaAcertado = new AcertoRankingRs
        {
            TorneioId = 1, PessoasCobradas = 10, Valor = 10m, AcertadoEm = DateTime.Now,
        };

        Assert.Null(AcertoComORankingRs.ProblemaParaAcertar(torneio, 10, null));
        Assert.NotNull(AcertoComORankingRs.ProblemaParaAcertar(torneio, 10, jaAcertado));
    }

    [Fact]
    public void Torneio_sem_inscrito_nao_gera_despesa_de_zero()
    {
        var torneio = new Torneio { Nome = "T", Codigo = "T1", ValidarPeloRankingRs = true };
        Assert.NotNull(AcertoComORankingRs.ProblemaParaAcertar(torneio, 0, null));
    }
}
