using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 31/08/2026 — "TODOS" NA ABA DE INSCRITOS (Felipe, num print da aba): "deixe na primeira,
// 'todos' com todos inscritos, sem separar a categoria, por ordem do mais recente pro mais
// antigo".
//
// A aba SEMPRE obrigou a escolher uma categoria pra ver alguém. Quem organiza responde
// "quem entrou hoje?" o dia inteiro, e a resposta exigia abrir as nove categorias e comparar
// de cabeça — que é a mesma queixa que já tinha gerado o total no cabeçalho.
//
// A ordem é a razão de isto ser um serviço e não um `.OrderBy` solto na view: a suíte não
// renderiza Razor, então ordenação escrita lá dentro não tem como ser falsificada — e ela
// tem DOIS casos que erram calado (data nula e empate de data).
public class TodosOsInscritosTests
{
    private static Torneio Torneio(string formato = "Padrao") =>
        new() { Nome = "Interno", Codigo = "INT1", Formato = formato };

    private static Jogador Jogador(int i) => new() { Id = i, Nome = $"Jogador {i}", Cpf = $"1110000000{i}" };

    private static Categoria Categoria(int id, string nome, params Dupla[] duplas)
    {
        var cat = new Categoria { Id = id, Nome = nome, Codigo = $"C{id}", Duplas = duplas.ToList() };
        foreach (var d in duplas) d.CategoriaId = id;
        return cat;
    }

    private static Dupla Dupla(int id, DateTime? quando, bool espera = false, string? time = null) =>
        new()
        {
            Id = id,
            Codigo = $"D{id}",
            CriadoEm = quando,
            EmListaDeEspera = espera,
            NomeTime = time,
            Jogador1 = Jogador(id),
        };

    [Fact]
    public void Junta_as_inscricoes_de_TODAS_as_categorias()
    {
        // O ponto do pedido: sem separar a categoria.
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina", Dupla(10, new DateTime(2026, 8, 30))),
                Categoria(2, "4ª Categoria Feminina", Dupla(11, new DateTime(2026, 8, 29))),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public void Do_mais_recente_pro_mais_antigo()
    {
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina",
                    Dupla(10, new DateTime(2026, 8, 20)),
                    Dupla(11, new DateTime(2026, 8, 30))),
                Categoria(2, "4ª Categoria Feminina", Dupla(12, new DateTime(2026, 8, 25))),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(new[] { 11, 12, 10 }, lista.Select(i => i.Id));
    }

    [Fact]
    public void Inscricao_sem_data_vai_pro_FIM_da_lista()
    {
        // ⚠️ `CriadoEm` é NULO nas inscrições anteriores a 25/07/2026 (quando a coluna
        // nasceu) — ou seja, nulo quer dizer "das mais ANTIGAS que existem". Uma ordenação
        // descendente ingênua sobre `DateTime?` que trate nulo como "maior" mandaria as mais
        // velhas do sistema pro topo de uma lista chamada "mais recentes".
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina",
                    Dupla(10, null),
                    Dupla(11, new DateTime(2026, 8, 30))),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(new[] { 11, 10 }, lista.Select(i => i.Id));
    }

    [Fact]
    public void Mesma_data_desempata_pelo_id_maior_primeiro()
    {
        // Duas inscrições do mesmo instante existem de verdade: a dupla que se inscreve e o
        // parceiro que entra no mesmo segundo. Sem desempate a ordem fica a do banco, que
        // não é ordem nenhuma — e duas telas iguais mostrariam listas diferentes.
        var mesmoInstante = new DateTime(2026, 8, 30, 21, 14, 0);

        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina",
                    Dupla(10, mesmoInstante),
                    Dupla(12, mesmoInstante),
                    Dupla(11, mesmoInstante)),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(new[] { 12, 11, 10 }, lista.Select(i => i.Id));
    }

    [Fact]
    public void Cada_item_diz_de_que_categoria_veio()
    {
        // Numa lista misturada, sem isto ninguém sabe em que categoria a dupla entrou — que
        // é justamente a informação que o seletor dava e que "Todos" tira.
        // O nome vai CURTO, igual ao seletor (ver CategoriaNaTela.Curto).
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[] { Categoria(1, "3ª Categoria Masculina", Dupla(10, new DateTime(2026, 8, 30))) },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal("3ª Masculina", lista[0].Categoria);
    }

    [Fact]
    public void Americano_lista_PESSOAS_e_nunca_duplas()
    {
        // Ali a inscrição é individual: as duplas nem existem, e ler `cat.Duplas` daria uma
        // lista vazia num torneio cheio.
        var cat = Categoria(1, "Categoria Mista A");
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio("Americano"),
            new[] { cat },
            new[]
            {
                new InscricaoAmericana { Id = 5, CategoriaId = 1, Jogador = Jogador(5), CriadoEm = new DateTime(2026, 8, 28) },
                new InscricaoAmericana { Id = 6, CategoriaId = 1, Jogador = Jogador(6), CriadoEm = new DateTime(2026, 8, 30) },
            });

        Assert.Equal(new[] { 6, 5 }, lista.Select(i => i.Id));
        Assert.Equal("Mista A", lista[0].Categoria);
        Assert.All(lista, i => Assert.NotNull(i.Jogador));
        Assert.All(lista, i => Assert.Null(i.Dupla));
    }

    [Fact]
    public void Quem_esta_na_lista_de_espera_entra_MARCADO()
    {
        // Entra porque é quem mais aparece no topo de uma lista por chegada — quem espera se
        // inscreveu depois de a categoria lotar. Marcado porque, sem o selo, a mesma lista
        // diria que ele tem vaga.
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina",
                    Dupla(10, new DateTime(2026, 8, 20)),
                    Dupla(11, new DateTime(2026, 8, 30), espera: true)),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.Equal(new[] { 11, 10 }, lista.Select(i => i.Id));
        Assert.True(lista[0].EmListaDeEspera);
        Assert.False(lista[1].EmListaDeEspera);
    }

    [Fact]
    public void O_selo_de_pagamento_vem_junto()
    {
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[]
            {
                Categoria(1, "3ª Categoria Masculina",
                    new Dupla { Id = 10, Codigo = "D10", Jogador1 = Jogador(10), Pago = true, CriadoEm = new DateTime(2026, 8, 30) }),
            },
            Array.Empty<InscricaoAmericana>());

        Assert.True(lista[0].Pago);
    }

    [Fact]
    public void Time_entra_pela_propria_inscricao_e_nao_pelo_organizador_que_cadastrou()
    {
        // Categoria de times: a linha é um TIME, e o `Jogador1Id` dela aponta pro organizador
        // que cadastrou. A lista precisa carregar a dupla inteira pra tela desenhar o time —
        // ver o partial _LadoDaPartida, que já sabe fazer isso.
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[] { Categoria(1, "Categoria Livre", Dupla(10, new DateTime(2026, 8, 30), time: "Fúria")) },
            Array.Empty<InscricaoAmericana>());

        Assert.True(lista[0].Dupla!.EhTime);
        Assert.Equal("Fúria", lista[0].Dupla!.NomeTime);
    }

    [Fact]
    public void Categoria_sem_nenhuma_inscricao_nao_derruba_a_lista()
    {
        // `Categoria.Duplas` chega nulo em consulta sem Include — e a aba inteira é uma
        // página que não pode cair por causa de uma categoria vazia.
        var lista = TodosOsInscritos.MaisRecentesPrimeiro(
            Torneio(),
            new[] { new Categoria { Id = 1, Nome = "3ª Categoria Masculina", Codigo = "C1", Duplas = null! } },
            Array.Empty<InscricaoAmericana>());

        Assert.Empty(lista);
    }

    // ⚠️ O NOME CURTO NÃO PODE MUDAR O TROFÉU. Na lista misturada, o card escreve a categoria
    // CURTA e é ela que o chip recebe em ViewData["CategoriaAtual"] pra desenhar o selo de
    // histórico. Isso só é seguro porque `Curto` tira a palavra "Categoria" e o material é
    // reconhecido por outro trecho ("3ª", "Mista", "Iniciantes") — se um dia `Curto` passar a
    // podar mais que isso, o selo do card viraria "Geral" em silêncio, que é o pior jeito de
    // errar. Esta trava quebra antes disso chegar na tela.
    [Theory]
    [InlineData("1ª Categoria Masculina")]
    [InlineData("3ª Categoria Masculina")]
    [InlineData("4ª Categoria Feminina")]
    [InlineData("7ª Categoria Masculina")]
    [InlineData("Categoria Mista A")]
    [InlineData("Categoria Iniciantes")]
    [InlineData("Categoria Lendas")]
    public void O_nome_curto_da_o_MESMO_trofeu_que_o_nome_inteiro(string nomeInteiro)
    {
        var curto = CategoriaNaTela.Curto(nomeInteiro);

        Assert.Equal(TrofeuDeMaterial.Do(nomeInteiro).Chave, TrofeuDeMaterial.Do(curto).Chave);
    }
}
