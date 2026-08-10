using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O filtro do Ranking mostrava "GRAVATAI", "Gravataí" e "Gravatai" como três cidades, e
// "PORTO ALEGRE", "Porto Alegre" e "Porto alegre" como mais três. Cada uma veio de alguém
// digitando a cidade do seu jeito — no perfil ou no formulário público de inscrição.
//
// `NomeDeCidade` já sabia dizer que as três são a mesma; o que faltava era aplicar isso na
// hora de MONTAR a lista, e — a parte que não pode ser esquecida — na hora de CONSULTAR.
public class CidadesSemRepetirTests
{
    // ---------- Texto livre ----------

    [Fact]
    public void Grafias_da_mesma_cidade_viram_uma_opcao_so()
    {
        var lista = CidadesSemRepetir.Nomes(new[]
        {
            "GRAVATAI", "Gravataí", "gravatai", "Gravatai",
            "PORTO ALEGRE", "Porto Alegre", "Porto alegre",
            "Canoas",
        });

        Assert.Equal(new[] { "Canoas", "Gravataí", "Porto Alegre" }, lista);
    }

    [Fact]
    public void Cidades_diferentes_de_verdade_continuam_separadas()
    {
        // Gravataí (RS) e Gravatá (PE) não são a mesma cidade, e "POA" não é grafia de nada:
        // Poá existe em São Paulo. Aqui não se adivinha apelido — quem some da lista some por
        // não ter torneio, não por parecer estranho.
        var lista = CidadesSemRepetir.Nomes(new[] { "Gravataí", "Gravatá", "POA", "Porto Alegre" });

        Assert.Equal(4, lista.Count);
    }

    [Theory]
    [InlineData(new[] { "GRAVATAI", "Gravataí", "gravatai" }, "Gravataí")]
    [InlineData(new[] { "GRAVATAI", "Gravatai" }, "Gravatai")]     // sem a acentuada, a de caixa normal
    [InlineData(new[] { "GRAVATAI", "gravatai" }, "GRAVATAI")]     // nenhuma boa: escolhe uma, sempre a mesma
    [InlineData(new[] { "são paulo", "São Paulo" }, "São Paulo")]
    public void A_grafia_que_vai_pra_tela_e_a_de_quem_escreveu_certo(string[] grafias, string esperada)
    {
        Assert.Equal(esperada, CidadesSemRepetir.MelhorGrafia(grafias));
    }

    [Fact]
    public void Escolher_a_cidade_da_lista_traz_todas_as_grafias()
    {
        // É o coração da mudança: a tela oferece UMA opção, mas a consulta tem que achar
        // TODOS. Sem isto, quem digitou "GRAVATAI" sumiria do ranking sem erro nenhum.
        var conhecidas = new[] { "GRAVATAI", "Gravataí", "gravatai", "Canoas" };

        var grafias = CidadesSemRepetir.GrafiasDe(new[] { "Gravataí" }, conhecidas);

        Assert.Contains("GRAVATAI", grafias);
        Assert.Contains("Gravataí", grafias);
        Assert.Contains("gravatai", grafias);
        Assert.DoesNotContain("Canoas", grafias);
    }

    [Fact]
    public void Cidade_que_ninguem_mais_tem_filtra_pra_vazio_e_nao_pro_Brasil_inteiro()
    {
        // Link antigo com uma cidade que sumiu do cadastro. Devolver lista vazia de grafias
        // faria o `Contains` do filtro não excluir ninguém — o filtro passaria a mostrar todo
        // mundo justamente quando não deveria mostrar ninguém.
        var grafias = CidadesSemRepetir.GrafiasDe(new[] { "Ivoti" }, new[] { "Canoas" });

        Assert.Equal(new[] { "Ivoti" }, grafias);
    }

    [Fact]
    public void Cidade_da_url_passa_a_ser_escrita_como_a_lista_escreve()
    {
        // Sem isto o chip do filtro apareceria "GRAVATAI" e o select ofereceria "Gravataí"
        // logo ao lado — a mesma cidade duas vezes na mesma linha.
        var canonica = CidadesSemRepetir.Canonizar(new[] { "GRAVATAI" }, new[] { "Gravataí", "Canoas" });

        Assert.Equal(new[] { "Gravataí" }, canonica);
    }

    [Theory]
    [InlineData("gravatai", "Gravataí")]   // já existe bem escrita → adota a boa
    [InlineData("Gravataí", "Gravataí")]
    [InlineData("Ivoti", "Ivoti")]         // cidade nova → fica como veio
    public void Quem_digita_agora_escreve_como_quem_digitou_antes(string digitada, string esperada)
    {
        var conhecidas = new[] { "Gravataí", "Gravataí", "Canoas" };

        Assert.Equal(esperada, CidadesSemRepetir.EscritaComoAsOutras(digitada, conhecidas));
    }

    [Fact]
    public void Quem_escreve_certo_nao_e_rebaixado_por_quem_escreveu_errado_antes()
    {
        // Senão a primeira pessoa a digitar "GRAVATAI" decidiria a grafia de todas as
        // próximas, e a lista nunca teria como melhorar.
        Assert.Equal("Gravataí", CidadesSemRepetir.EscritaComoAsOutras("Gravataí", new[] { "GRAVATAI", "GRAVATAI" }));
    }

    [Fact]
    public void Cidade_em_branco_nao_vira_cidade_de_nome_vazio()
    {
        Assert.Equal("", CidadesSemRepetir.EscritaComoAsOutras("   ", new[] { "Canoas" }));
        Assert.Empty(CidadesSemRepetir.Nomes(new[] { "", "   ", (string?)null }));
    }

    // ---------- Catálogo (tabela Cidades) ----------

    [Fact]
    public void Catalogo_com_duas_linhas_da_mesma_cidade_vira_uma_opcao()
    {
        var catalogo = new List<Cidade>
        {
            new() { Id = 1, Nome = "Gravataí", Estado = "RS" },
            new() { Id = 7, Nome = "Gravatai", Estado = "RS" },
            new() { Id = 9, Nome = "Canoas",   Estado = "RS" },
        };

        var lista = CidadesSemRepetir.Agrupar(catalogo);

        Assert.Equal(2, lista.Count);
        var gravatai = lista.Single(c => c.Nome == "Gravataí");

        // O id do <option> é o MENOR: link antigo e vínculo já gravado continuam valendo.
        Assert.Equal(1, gravatai.Id);
        Assert.Equal(new[] { 1, 7 }, gravatai.Ids);
    }

    [Fact]
    public void Escolher_uma_das_linhas_do_catalogo_acha_quem_esta_na_outra()
    {
        var catalogo = new List<Cidade>
        {
            new() { Id = 1, Nome = "Gravataí", Estado = "RS" },
            new() { Id = 7, Nome = "GRAVATAI", Estado = "RS" },
            new() { Id = 9, Nome = "Canoas",   Estado = "RS" },
        };

        // O professor cadastrado na linha 7 tem que aparecer pra quem escolheu a linha 1.
        Assert.Equal(new[] { 1, 7 }, CidadesSemRepetir.IdsDaMesma(1, catalogo));
        Assert.Equal(new[] { 1, 7 }, CidadesSemRepetir.IdsDaMesma(7, catalogo));
        Assert.Equal(new[] { 9 }, CidadesSemRepetir.IdsDaMesma(9, catalogo));
    }

    [Fact]
    public void Linha_sem_uf_e_a_mesma_cidade_incompleta_e_nao_outra_cidade()
    {
        var catalogo = new List<Cidade>
        {
            new() { Id = 1, Nome = "Gravataí", Estado = "RS" },
            new() { Id = 2, Nome = "Gravataí", Estado = null },
        };

        var lista = CidadesSemRepetir.Agrupar(catalogo);

        Assert.Single(lista);
        Assert.Equal("RS", lista[0].Estado);
        Assert.Equal("Gravataí - RS", lista[0].Rotulo);
    }

    [Fact]
    public void Homonimas_de_estados_diferentes_sao_cidades_diferentes()
    {
        // São Francisco existe em meia dúzia de UFs; juntar as duas esconderia uma delas.
        var catalogo = new List<Cidade>
        {
            new() { Id = 1, Nome = "São Francisco", Estado = "MG" },
            new() { Id = 2, Nome = "Sao Francisco", Estado = "SP" },
        };

        var lista = CidadesSemRepetir.Agrupar(catalogo);

        Assert.Equal(2, lista.Count);
        Assert.Equal(new[] { "MG", "SP" }, lista.Select(c => c.Estado));
    }

    [Fact]
    public async Task Catalogo_nao_ganha_segunda_linha_por_causa_de_acento()
    {
        // A comparação era `ToLower()`: pegava "GRAVATAI" e deixava "Gravatai" passar.
        using var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Nome = "Gravataí", Estado = "RS" });
        await ctx.SaveChangesAsync();

        var achada = await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "gravatai", "RS");

        Assert.Equal("Gravataí", achada!.Nome);
        Assert.Single(ctx.Cidades);
    }

    [Fact]
    public async Task Espaco_duplicado_tambem_nao_cria_linha_nova_no_catalogo()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Nome = "Porto Alegre", Estado = "RS" });
        await ctx.SaveChangesAsync();

        await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "  Porto    Alegre ", "RS");

        Assert.Single(ctx.Cidades);
    }
}
