using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — O "–" DIZIA "FICOU NA MESMA POSIÇÃO" PRA UMA TABELA EM QUE NINGUÉM TINHA POSIÇÃO.
//
// 🗣️ Felipe, olhando o Padelímetro no ar: a coluna Torneio inteira em "–", nas 29 linhas.
//
// 🕳️ `MovimentoNoRanking.Aplicar` gravava `0` quando a lista "antes" está vazia, e `0` é o MESMO
// valor de "jogou e ficou onde estava" — que o `_SeloDeMovimento` desenha como "–" com o title
// "Ficou na mesma posição". Depois do primeiro torneio de um ranking, todo mundo lia que não
// tinha se mexido, quando a verdade é que não havia de onde se mexer.
//
// ⚠️ O próprio arquivo já tinha as duas decisões que apontam pra cá: o comentário do `Aplicar`
// diz "Sem base, sem selo", e o do partial diz que "novo NÃO é +0" porque trocar um estado por
// outro é "uma mentira pequena que some no meio da tabela e ninguém desconfia". Faltava o
// terceiro estado — e o teste que existia (`Sem_base_de_comparacao_ninguem_ganha_selo`) lacrava
// o defeito: o nome dizia "ninguém ganha selo", a asserção cobrava o 0 que vira selo.
//
// 🔑 O QUE ESTE ARQUIVO TRAVA NÃO É A REPRESENTAÇÃO, É A DISTINÇÃO. Por isso a `Linha` guarda
// `object?` e não o tipo do selo: se amanhã ele deixar de ser um enum, ou virar outra coisa, o
// teste continua cobrando a única regra que interessa — "sem base" e "ficou parado" não podem
// chegar na tela como o mesmo valor.
public class SeloSemBaseDeComparacaoTests
{
    private sealed class Linha
    {
        public int Id { get; init; }
        public object? Selo { get; set; }
    }

    private static List<Linha> Lista(params int[] ids) => ids.Select(i => new Linha { Id = i }).ToList();

    private static List<Linha> Aplicar(List<Linha> agora, params int[] ordemAntes)
    {
        MovimentoNoRanking.Aplicar(agora, ordemAntes, l => l.Id, (l, selo) => l.Selo = selo);
        return agora;
    }

    [Fact]
    public void Sem_base_de_comparacao_NAO_pode_chegar_na_tela_como_ficou_parado()
    {
        // Os dois cenários que a tela precisa contar diferente:
        //   sem base  → primeiro torneio deste ranking, ninguém tinha posição anterior;
        //   parado    → havia ranking antes, a pessoa estava nele e não saiu do lugar.
        var semBase = Aplicar(Lista(1, 2, 3));
        var parado = Aplicar(Lista(1, 2, 3), 1, 2, 3);

        Assert.NotEqual(parado[0].Selo, semBase[0].Selo);
    }

    [Fact]
    public void Sem_base_tambem_nao_pode_se_confundir_com_NOVO()
    {
        // "novo" é quem entrou agora num ranking QUE JÁ EXISTIA — e anunciar a tabela inteira
        // como estreia é o que o comentário do Aplicar recusa desde 08/08 ("anunciar 40 estreias
        // não informa nada"). Sem esta asserção, resolver o defeito empurrando todo mundo pro
        // "novo" passaria pelo teste de cima.
        var semBase = Aplicar(Lista(1, 2, 3));
        var novo = Aplicar(Lista(1, 99), 1);

        Assert.NotEqual(novo[1].Selo, semBase[0].Selo);
    }

    [Fact]
    public void O_selo_na_tela_nao_diz_FICOU_NA_MESMA_POSICAO_quando_nao_havia_posicao()
    {
        // O defeito só existe porque um humano LÊ o title. Aqui se cobra o texto que ele lê.
        var partial = File.ReadAllText(Path.Combine(RaizDoRepo(),
            "Padelizou", "Views", "Shared", "_SeloDeMovimento.cshtml"));

        Assert.Contains(Title(TituloSemBase), partial);

        // E o title antigo continua sendo de UM estado só: se ele aparecer em dois ramos, os
        // dois voltaram a dizer a mesma coisa e o defeito voltou por outro caminho.
        //
        // ⚠️ Conta `title="..."` e não a frase solta: os comentários do partial CITAM o texto
        // pra explicar o defeito de 14/09, e contar a citação faria este teste quebrar por
        // alguém escrever um comentário. O que se cobra aqui é o que o jogador lê.
        Assert.Equal(1, Regex.Matches(partial, Regex.Escape(Title(TituloParado))).Count);
    }

    // Os dois textos que o jogador lê, lado a lado — é a diferença inteira que este arquivo
    // defende, e deixá-los juntos aqui é o que faz a troca de um deles quebrar o teste.
    private static string Title(string texto) => $"title=\"{texto}\"";

    private const string TituloSemBase = "Ainda não há posição anterior para comparar";
    private const string TituloParado = "Ficou na mesma posição";

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
