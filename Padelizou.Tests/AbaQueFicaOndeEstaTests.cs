using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — A TELA NÃO SAI DEBAIXO DE QUEM ESTÁ OLHANDO.
//
// 🗣️ Felipe, três vezes no mesmo dia: *"as vezes to olhando as finalizadas e ele automaticamente
// volta para tela do ao vivo"* · *"ao mudar algum filtro, as vezes sai da tela que esta"* ·
// *"estava mexendo na aba palpiteiros e sozinho foi para o aovivo, isso nao pode acontecer, ele
// tem q se manter na tela q esta, a menos q o usuario clique em algo"*.
//
// 🕳️ O COMPORTAMENTO ESTAVA ESCRITO E MORTO. O `js/jogos-abas.js` existe desde 08/08/2026 pra
// lembrar a aba, e **nunca rodou nesta tela**: medido no HTML entregue, ele sai na linha 3941 e o
// `bootstrap.bundle.js` na 4736 — os scripts da lista de jogos são emitidos no CORPO da página e
// o Bootstrap só chega no fim, pelo `_Layout`. O `if (!window.bootstrap) return` disparava sempre,
// calado. Conferido no navegador: `sessionStorage` vazio depois do clique e ZERO ouvintes no
// `#jogosTabs`.
//
// ⚠️ O COMPORTAMENTO EM SI mora no conferidor de JS (`conferir-abas-que-ficam.js`), que roda o
// script na ORDEM DE PRODUÇÃO — sem Bootstrap — e cobra que a memória funcione mesmo assim. O que
// se guarda AQUI é o que só o Razor sabe: que as duas barras carregam o `data-torneio-id` de que
// a chave é feita. Sem ele a chave nasce sem número, e dois torneios abertos no mesmo dia passam
// a dividir a mesma memória de aba — foi exatamente o que aconteceu com a barra de cima, que não
// tinha o atributo e por isso não voltava pra aba certa.
public class AbaQueFicaOndeEstaTests
{
    [Theory]
    [InlineData("Details.cshtml", "torneioTabs")]
    [InlineData("_JogosDoTorneio.cshtml", "jogosTabs")]
    public void As_duas_barras_de_aba_dizem_de_qual_torneio_sao(string arquivo, string id)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDasViews(), arquivo));
        var barra = Regex.Match(fonte, @"<ul[^>]*id=""" + id + @"""[^>]*>");

        Assert.True(barra.Success, $"Não achei a barra #{id} em {arquivo}.");
        Assert.Contains("data-torneio-id", barra.Value);
    }

    [Fact]
    public void O_atualizador_so_recarrega_pra_quem_esta_olhando_o_ao_vivo()
    {
        // A trava fica no JS e é conferida de verdade pelo conferir-abas-que-ficam.js. Aqui só
        // se garante que ela não sumiu numa limpeza: `location.reload()` sem guarda nenhuma é o
        // defeito de volta, e nenhum teste em C# enxerga isso.
        //
        // 12/09/2026: o recarregamento virou CAMINHO DE ESCAPE (só quando o remendo cartão a
        // cartão não dá), e passa por `recarregarMantendoARolagem` pra guardar a altura da
        // página antes de sumir com ela.
        var js = File.ReadAllText(Path.Combine(RaizDoRepo(),
            "Padelizou", "wwwroot", "js", "jogos-ao-vivo-atualiza.js"));

        Assert.Contains("olhandoOAoVivo", js);
        Assert.Matches(new Regex(@"if\s*\(olhandoOAoVivo\(\)\)\s*\{\s*recarregarMantendoARolagem\(\);",
            RegexOptions.Singleline), js);
    }

    [Fact]
    public void A_grade_do_ao_vivo_tem_o_marcador_que_o_remendo_procura()
    {
        // 🗣️ Felipe, 12/09/2026: *"quando entrar ou sair um jogo do aovivo, ele apenas adicionar
        // na tela sem precisar carregar"*. O js/jogos-ao-vivo-atualiza.js insere e remove cartão
        // dentro de `#pdzAoVivoCartoes`. Sem o id no Razor ele não acha a grade, cai no caminho
        // de escape e a página volta a recarregar inteira — sem erro nenhum no console e sem
        // teste vermelho em lugar nenhum, porque o escape FUNCIONA. Só o Felipe veria, no
        // sábado, o YouTube parando sozinho de novo.
        var fonte = File.ReadAllText(Path.Combine(PastaDasViews(), "_JogosDoTorneio.cshtml"));

        Assert.Matches(new Regex(@"<div class=""row"" id=""pdzAoVivoCartoes"">"), fonte);
    }

    [Fact]
    public void O_relogio_do_ao_vivo_reconhece_jogo_agendado_na_tela()
    {
        // 🗣️ Felipe, 12/09/2026: *"Pessoal que tem o app no celular, disse q ao abrir ele fica
        // desatualizado as vezes no aovivo"*.
        //
        // O relógio de 20s só liga enquanto AINDA HÁ O QUE ACONTECER — jogo em quadra ou jogo
        // agendado —, e a segunda metade dessa régua é lida do HTML com `#agendadas .pdz-jl`.
        // São TRÊS peças que precisam concordar: o id do painel, a classe da linha de jogo e o
        // seletor no JS. Se qualquer uma derivar, o relógio deixa de ligar pra quem abre o app
        // antes do primeiro jogo — em silêncio, e sem nenhum outro teste enxergando: a tela
        // simplesmente fica em "Ao Vivo (0)" o dia inteiro, que é a queixa original.
        var lista = File.ReadAllText(Path.Combine(PastaDasViews(), "_JogosDoTorneio.cshtml"));
        Assert.Matches(new Regex(@"id=""agendadas"""), lista);

        var linha = File.ReadAllText(Path.Combine(PastaDasViews(), "_JogoEmLinha.cshtml"));
        Assert.Matches(new Regex(@"<div class=""pdz-jl[ @""]"), linha);

        var js = File.ReadAllText(Path.Combine(RaizDoRepo(),
            "Padelizou", "wwwroot", "js", "jogos-ao-vivo-atualiza.js"));
        Assert.Contains("#agendadas .pdz-jl", js);

        // E a volta pro primeiro plano precisa continuar acordando a tela: sem este ouvinte,
        // abrir o app volta a mostrar o de até 20 segundos atrás.
        Assert.Contains("visibilitychange", js);
    }

    [Fact]
    public void A_barra_de_filtros_guarda_a_altura_da_pagina()
    {
        // 🗣️ Felipe, 12/09/2026: *"quando eu clico em meu jogos, a pagina sobe la para o inicio
        // tambem, tinha q aparece na aba meus jogos ja"*.
        //
        // O `js/manter-posicao-na-lista.js` é OPT-IN por `data-manter-posicao` — quem sabe se a
        // ação volta pra mesma tela é quem escreveu o botão. Sem o atributo o clique passa
        // batido e a página renasce no começo, e nada nesta suíte enxergaria isso: o
        // comportamento do JS está travado no conferir-abas-que-ficam.js, mas o atributo é
        // markup, e markup só some em silêncio.
        var fonte = File.ReadAllText(Path.Combine(PastaDasViews(), "_JogosDoTorneio.cshtml"));

        var meusJogos = Regex.Match(fonte, @"<a[^>]*asp-route-soMeusJogos[^>]*>", RegexOptions.Singleline);
        Assert.True(meusJogos.Success, "Não achei o link do \"Meus jogos\".");
        Assert.Contains("data-manter-posicao", meusJogos.Value);

        var formulario = Regex.Match(fonte, @"<form[^>]*id=""filtroJogos""[^>]*>", RegexOptions.Singleline);
        Assert.True(formulario.Success, "Não achei o formulário dos filtros.");
        Assert.Contains("data-manter-posicao", formulario.Value);
    }

    [Fact]
    public void A_memoria_de_aba_nao_depende_do_bootstrap_pra_gravar()
    {
        // A armadilha que matou o recurso por um mês: o script roda ANTES do bootstrap.bundle.js
        // na página do torneio. Se voltar a desistir cedo (`!window.bootstrap` no topo), a
        // memória morre em silêncio de novo — sem erro no console, sem teste vermelho em C#.
        var js = File.ReadAllText(Path.Combine(RaizDoRepo(),
            "Padelizou", "wwwroot", "js", "jogos-abas.js"));

        Assert.Contains("DOMContentLoaded", js);
        Assert.DoesNotMatch(new Regex(@"if\s*\(!pills\s*\|\|\s*!window\.bootstrap\)\s*return"), js);
    }

    private static string PastaDasViews() =>
        Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios");

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
