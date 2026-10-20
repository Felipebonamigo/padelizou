using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 20/10/2026 — A LEGENDA DA MISTA TEM QUE FICAR À VISTA DEPOIS DE ESCOLHER.
//
// 🗣️ Felipe, olhando o seletor de categoria no iPhone: *"aqui tem q deixar claro também
// quando for selecionado"*. A legenda entrou na LISTA de opções (PR #382), mas depois de
// escolhida o campo fica estreito e o iOS mostra só "Mista B": a pessoa decide e perde a
// informação. Agora há uma caixa fixa abaixo do seletor.
//
// 🕳️ E UM DEFEITO QUE A PRÓPRIA LEGENDA CRIOU: o aviso "Por que quer se inscrever na ...?" lê o
// TEXTO da opção pra descobrir o nível ("4ª" → 4ª categoria). Com "Mista E: 7ª Masc, 7ª Fem e
// 6ª Fem" no texto, a Mista passou a valer 7ª — e quem declarou nível mais forte ouvia uma
// pergunta absurda. A régua passa a ler `data-nome`, o nome SEM legenda.
//
// O comportamento em si é JS dentro do Razor; aqui se guarda o que o Razor precisa entregar pra
// ele funcionar, no mesmo estilo de AbaQueFicaOndeEstaTests.
public class LegendaDaCategoriaEscolhidaTests
{
    private static string Fonte() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios", "Details.cshtml"));

    // As duas listas de inscrição (dupla e Americano) usam `cat.Id` como valor; o "Adicionar
    // categoria" do organizador usa `c.Id` e não entra aqui.
    private static string[] OpcoesDeInscricao() =>
        Regex.Matches(Fonte(), @"<option value=""@cat\.Id""[^>]*>[^<]*CategoriaNaTela\.ComLegenda\(cat\.Nome\)[^<]*</option>")
            .Select(m => m.Value).ToArray();

    [Fact]
    public void As_duas_listas_de_inscricao_entregam_a_legenda_em_cada_opcao()
    {
        var opcoes = OpcoesDeInscricao();

        Assert.Equal(2, opcoes.Length);
        Assert.All(opcoes, o => Assert.Contains("data-legenda=\"@Padelizou.Services.CategoriaNaTela.Legenda(cat.Nome)\"", o));
    }

    [Fact]
    public void As_duas_listas_de_inscricao_entregam_o_nome_sem_legenda()
    {
        var opcoes = OpcoesDeInscricao();

        Assert.Equal(2, opcoes.Length);
        Assert.All(opcoes, o => Assert.Contains("data-nome=\"@Padelizou.Services.CategoriaNaTela.Curto(cat.Nome)\"", o));
    }

    [Fact]
    public void Existe_a_caixa_que_mostra_a_legenda_da_categoria_escolhida()
    {
        // A caixa precisa existir NAS DUAS formas (dupla e Americano): em cada uma só um dos
        // dois formulários é desenhado, então o id não se repete na página.
        var caixas = Regex.Matches(Fonte(), @"id=""pdzLegendaCategoria""").Count;

        Assert.Equal(2, caixas);
    }

    [Fact]
    public void O_script_mostra_a_legenda_ao_trocar_a_categoria()
    {
        var fonte = Fonte();

        Assert.Contains("getElementById('pdzLegendaCategoria')", fonte);
        Assert.Contains("getAttribute('data-legenda')", fonte);
    }

    [Fact]
    public void O_aviso_de_categoria_mais_fraca_le_o_nome_sem_legenda()
    {
        // Sem isto "Mista E: 7ª Masc, 7ª Fem e 6ª Fem" vira nível 7ª e o aviso dispara à toa.
        var fonte = Fonte();
        var inicio = fonte.IndexOf("function avaliar()", StringComparison.Ordinal);
        Assert.True(inicio > 0, "Não achei a função avaliar() do aviso de categoria.");

        var corpo = fonte.Substring(inicio, 500);
        Assert.Contains("getAttribute('data-nome')", corpo);
    }
}
