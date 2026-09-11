using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUAL CATEGORIA A ABA "Chaves e Grupos" ABRE.
//
// 🗣️ Felipe, 10/09/2026, com o seletor novo no ar: *"esta fora de ordem"* e *"venha sempre
// selecionado a categoria que o usuario esta cadastrado (se estiver em duas, vem na melhor
// delas 1>2>3>4)"*.
//
// 🕳️ A lista saía na ordem em que as categorias foram CRIADAS — o mesmo defeito que fez nascer
// o `CategoriaNaTela.Ordem` em 08/08 ("a 4ª Feminina aparecia no fim, depois da 7ª"). O seletor
// herdou isso das pills, que já vinham assim.
//
// ⚠️ A ORDEM DE TELA É A DE LÁ, e não uma nova: `CategoriaNaTela.Ordem` já é a régua do site
// (as masculinas na escada, depois as femininas, depois mista e casais). Uma segunda conta de
// "qual vem antes" faria esta tela discordar das outras sobre a mesma lista.
public class CategoriaQueAbreNasChavesTests
{
    // ── SEM NINGUÉM LOGADO: a primeira da ordem de TELA, não a primeira do banco ──────────

    [Fact]
    public void Anonimo_abre_a_primeira_da_ordem_de_tela()
    {
        // Criadas fora de ordem de propósito: é assim que o torneio do Er está no banco.
        var categorias = new[] { Cat(1, "6ª Categoria Masculina"), Cat(2, "3ª Categoria Feminina"),
                                 Cat(3, "3ª Categoria Masculina") };

        Assert.Equal(3, CategoriaQueAbre.Escolher(categorias, meuJogadorId: null)?.Id);
    }

    [Fact]
    public void Sem_categoria_nenhuma_nao_estoura()
    {
        Assert.Null(CategoriaQueAbre.Escolher(Array.Empty<Categoria>(), meuJogadorId: 7));
    }

    // ── COM JOGADOR: a dele ──────────────────────────────────────────────────────────────

    [Fact]
    public void Abre_na_categoria_em_que_eu_estou_inscrito()
    {
        var minha = Cat(2, "6ª Categoria Masculina", ComDupla(jogador1: 7));
        var categorias = new[] { Cat(1, "3ª Categoria Masculina"), minha };

        // Sem a régua, abriria na 3ª — que é a primeira da ordem de tela e não é a minha.
        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    [Fact]
    public void Conta_tambem_quem_e_o_SEGUNDO_da_dupla()
    {
        // Metade das inscrições do site é gente que foi chamada, e não que chamou.
        var minha = Cat(2, "5ª Categoria Masculina", ComDupla(jogador1: 99, jogador2: 7));
        var categorias = new[] { Cat(1, "3ª Categoria Masculina"), minha };

        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    [Fact]
    public void Em_duas_abre_na_MELHOR_delas()
    {
        // 🗣️ "se estiver em duas, vem na melhor delas 1>2>3>4": manda o NÍVEL, e o menor ganha.
        var categorias = new[] { Cat(1, "5ª Categoria Masculina", ComDupla(jogador1: 7)),
                                 Cat(2, "3ª Categoria Masculina", ComDupla(jogador1: 7)) };

        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    [Fact]
    public void A_melhor_vale_mesmo_cruzando_escadas()
    {
        // O caso que separa "melhor" de "primeira da lista": na ordem de TELA a masculina vem
        // antes da feminina, mas a régua do Felipe é por nível — a 3ª ganha da 6ª.
        var categorias = new[] { Cat(1, "6ª Categoria Masculina", ComDupla(jogador1: 7)),
                                 Cat(2, "3ª Categoria Feminina", ComDupla(jogador1: 7)) };

        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    [Fact]
    public void Open_ganha_de_todas()
    {
        // Open é o topo da escada em CategoriaNaTela — sem isto ela perderia até pra 2ª.
        var categorias = new[] { Cat(1, "2ª Categoria Masculina", ComDupla(jogador1: 7)),
                                 Cat(2, "Open Masculina", ComDupla(jogador1: 7)) };

        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    [Fact]
    public void Quem_nao_esta_em_nenhuma_das_listadas_cai_na_primeira_da_ordem()
    {
        // O organizador olhando a chave, e também quem se inscreveu numa categoria que ainda
        // não tem chave pra mostrar (ela nem chega nesta lista).
        var categorias = new[] { Cat(1, "6ª Categoria Masculina", ComDupla(jogador1: 99)),
                                 Cat(2, "3ª Categoria Masculina", ComDupla(jogador1: 99)) };

        Assert.Equal(2, CategoriaQueAbre.Escolher(categorias, meuJogadorId: 7)?.Id);
    }

    // ── infra ────────────────────────────────────────────────────────────────────────────

    private static Categoria Cat(int id, string nome, params Dupla[] duplas) =>
        new() { Id = id, Nome = nome, Duplas = duplas.ToList() };

    private static Dupla ComDupla(int jogador1, int? jogador2 = null) =>
        new() { Jogador1Id = jogador1, Jogador2Id = jogador2 };
}
