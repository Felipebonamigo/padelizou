using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — VER QUEM PALPITOU **E O QUE** CADA UM PALPITOU. 🗣️ Felipe, num print da lista de
// jogos do 2ª Etapa ER PADEL TOUR, com a frase "A galera crava 9 x 7 (1 de 3)" marcada:
// *"tambem permita clicar e ver quem colocou o palpitometro e qual o placar"*.
//
// 🕳️ O modal "quem votou em quem" já existia desde sempre — e mostrava só o NOME. O placar, que
// é a metade interessante ("quem foi que cravou 9x7?"), morria no banco: a barra dizia quantos,
// a frase dizia o consenso, e não havia tela nenhuma que ligasse um palpite a uma pessoa.
public class VerQuemPalpitouTests
{
    // ─────────────────────────── O QUE O SERVIÇO DEVOLVE ───────────────────────────

    [Fact]
    public async Task O_modal_diz_QUAL_PLACAR_cada_um_palpitou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var cravou = await NovoTorcedorAsync(ctx, "Cravou o Placar", "55540000001");
        var soOVencedor = await NovoTorcedorAsync(ctx, "So o Vencedor", "55540000002");

        await servico.RegistrarVotoAsync(partida.Id, cravou.Id, duplas[0].Id, 6, 4);
        await servico.RegistrarVotoAsync(partida.Id, soOVencedor.Id, duplas[0].Id);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        // ⚠️ "Cravou o Placar" chega na tela como "Cravou Placar" desde 11/09/2026: o nome passou
        // a sair pelo `NomeBonito.Curto` (primeiro + último). Quem manda no que se procura aqui é
        // o que a tela MOSTRA — e isso está travado em O_nome_do_votante_sai_ABREVIADO.
        var comPlacar = votantes.VotantesDupla1.Single(v => v.Nome == "Cravou Placar");
        Assert.Equal(6, comPlacar.PlacarVencedor);
        Assert.Equal(4, comPlacar.PlacarPerdedor);

        // ⚠️ Palpitar o placar é OPCIONAL e continua sendo — quem só disse quem vence aparece
        // sem placar nenhum, e não com um "0 x 0" inventado.
        var semPlacar = votantes.VotantesDupla1.Single(v => v.Nome == "So Vencedor");
        Assert.Null(semPlacar.PlacarVencedor);
        Assert.Null(semPlacar.PlacarPerdedor);
    }

    [Fact]
    public async Task O_placar_de_quem_votou_na_DUPLA_2_nao_sai_invertido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor da Dois", "55540000003");

        // ⚠️ No BANCO o placar mora na orientação do JOGO (lado 1 = Dupla1), então quem aposta
        // "6 x 4 pra Dupla 2" grava 4 x 6. Na tela ele tem que ler 6 x 4: o modal lista a pessoa
        // DEBAIXO da dupla em que ela votou, e ali "4 x 6" diria que ela apostou na derrota de
        // quem escolheu. É a mesma orientação da ficha que ela tocou.
        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[1].Id, 4, 6);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        var linha = Assert.Single(votantes.VotantesDupla2);
        Assert.Equal(6, linha.PlacarVencedor);
        Assert.Equal(4, linha.PlacarPerdedor);
    }

    [Fact]
    public async Task Em_jogo_de_dois_SETS_o_modal_diz_que_o_palpite_e_em_sets()
    {
        using var ctx = TestInfra.NovoContexto();
        // Sets = 2 é o "melhor de 3" do formato (ver PlacaresPossiveis): 2x0 e 2x1.
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx, setsDosGrupos: 2);
        var servico = new PalpiteService(ctx);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor de Sets", "55540000004");
        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 2, 0);

        var votantes = await servico.ObterVotantesAsync(partida.Id);

        // "2 x 0" sem a moeda ao lado é um placar de games que nenhum jogo termina.
        var linha = Assert.Single(votantes.VotantesDupla1);
        Assert.Equal(2, linha.PlacarVencedor);
        Assert.True(linha.PlacarEmSets);
    }

    // ─────────────────────────── O QUE SÓ EXISTE NA TELA ───────────────────────────
    //
    // ⚠️ Teste de FONTE: a suíte não renderiza Razor nem executa o modal. O que se trava aqui é
    // que os pontos de clique existem e que o JS desenha o placar — o resto é comportamento e
    // está travado acima.

    [Fact]
    public void A_frase_do_consenso_ABRE_o_modal_nas_duas_apresentacoes()
    {
        // 🗣️ O print do Felipe marcou exatamente esta frase. Ela é o lugar mais natural pra
        // perguntar "quem cravou?", e até agora era texto morto.
        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_Palpitrometro.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);

            var inicio = fonte.IndexOf("pdz-palpite-consenso", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei a frase do consenso em {arquivo}.");

            var trecho = fonte[inicio..Math.Min(fonte.Length, inicio + 900)];
            Assert.Contains("verVotos(", trecho);
        }
    }

    [Fact]
    public void A_frase_do_consenso_nao_diz_mais_que_CRAVA()
    {
        // 🗣️ Felipe, 11/09/2026: *"aqui por que tem isso? nao sei se faz muito sentido"* — num
        // jogo em que a frase dizia "A galera crava 9 x 5 (3 de 8)".
        //
        // ⚠️ "Cravar" é o verbo do RANKING: acertar o placar exato, depois do jogo, valendo 3
        // pontos. Emprestá-lo pra uma aposta de 3 em 8 faz a tela anunciar veredito onde há
        // pluralidade — e usa a mesma palavra pra duas coisas diferentes.
        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_Palpitrometro.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);

            Assert.Contains("Placar mais palpitado", fonte);
            Assert.DoesNotContain("A galera crava", fonte);
        }
    }

    [Fact]
    public void A_linha_do_consenso_NASCE_no_DOM_mesmo_sem_consenso()
    {
        // ⚠️ Ela precisa existir escondida, e não ser gerada por um `@if` do Razor: com o limiar
        // de dois palpites (11/09/2026), o caso mais comum de a linha PASSAR a existir é
        // justamente o seu palpite formando o par — e o `atualizarPalpitrometro` só sabe mostrar
        // um elemento que já está na página. Sem isso, a leitura da galera só apareceria no F5.
        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_Palpitrometro.cshtml" })
        {
            var fonte = Ler("Views", "Torneios", arquivo);

            // A TAG, e não a primeira menção: o comentário logo acima também cita a classe.
            var inicio = fonte.IndexOf("pdz-palpite-consenso\"", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei a tag da linha do consenso em {arquivo}.");

            // O `display` sai do dado, na própria tag — é a chave que o JS vira depois do voto.
            var trecho = fonte[inicio..Math.Min(fonte.Length, inicio + 220)];
            Assert.Contains("style=\"display:", trecho);
            Assert.Contains("TemPlacarMaisPalpitado", trecho);
        }
    }

    [Fact]
    public void Cada_votante_do_modal_e_uma_CAIXA_com_o_placar_no_mesmo_lugar()
    {
        // 🗣️ Felipe, 11/09/2026, num print do modal com 14 nomes numa coluna: *"deixe um
        // 'quadrado' ou algo assim, fica dificil ver quem fez o que nessa tela"*.
        //
        // 🕳️ Eram linhas soltas, sem moldura, e o placar só existia em ALGUMAS delas — a coluna
        // da direita ficava esburacada e o olho não sabia onde procurar. Com 14 palpites a lista
        // vira um bloco de texto.
        var js = Ler("wwwroot", "js", "palpitrometro.js");

        // A moldura de cada linha.
        Assert.Contains("rounded", js);
        Assert.Contains("border", js);

        // ⚠️ E o lugar do placar é SEMPRE o mesmo: quem não palpitou placar leva um traço, em
        // vez de deixar o buraco que faz a coluna da direita parecer defeito.
        Assert.Contains("sem-placar", js);
    }

    [Fact]
    public void O_modal_desenha_o_placar_de_cada_votante()
    {
        var js = Ler("wwwroot", "js", "palpitrometro.js");

        // Os nomes vêm do JSON do /Partidas/VerVotos, em camelCase.
        Assert.Contains("placarVencedor", js);
        Assert.Contains("placarPerdedor", js);
    }

    // ─────────────────────────── COMO O NOME APARECE ───────────────────────────
    //
    // 11/09/2026 — 🗣️ Felipe, no print do modal já empilhado: *"temos q tentar por o nome em uma
    // linha, talvez abreviar, e tambem talvez tenhamos q corrigir o case sensitive, pra nao ficar
    // tudo maiusculo e nem tudo minusculo"*.
    //
    // 🕳️ O `Montar` do PalpiteService era o ÚNICO ponto do palpitrômetro que escrevia
    // `Jogador.Nome` cru. Duas linhas acima, no mesmo arquivo, o "Cravaram o placar" já passava
    // pelo `NomeBonito` — então o mesmo torneio mostrava "JOAO EGIDIO FERREIRA DA ROCHA" numa
    // frase e "Joao Rocha" na outra.

    [Fact]
    public async Task O_nome_do_votante_sai_ABREVIADO_e_com_a_caixa_arrumada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        var gritado = await NovoTorcedorAsync(ctx, "JOAO EGIDIO FERREIRA DA ROCHA", "55540000005");
        var sussurrado = await NovoTorcedorAsync(ctx, "ana zenker pasinato", "55540000006");

        await servico.RegistrarVotoAsync(partida.Id, gritado.Id, duplas[0].Id, 6, 4);
        await servico.RegistrarVotoAsync(partida.Id, sussurrado.Id, duplas[0].Id, 6, 0);

        var votantes = await servico.ObterVotantesAsync(partida.Id);
        var nomes = votantes.VotantesDupla1.Select(v => v.Nome).OrderBy(n => n).ToList();

        // Primeiro + último, e a caixa arrumada: é o mesmo `NomeBonito.Curto` do resto do site.
        Assert.Equal(new[] { "Ana Pasinato", "Joao Rocha" }, nomes);
    }

    [Fact]
    public async Task Quem_digitou_o_nome_DE_PROPOSITO_com_maiuscula_no_meio_nao_perde_ela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var servico = new PalpiteService(ctx);

        // ⚠️ Arrumar caixa NÃO pode passar rolo compressor: "DiCaprio" viraria "Dicaprio", que é
        // estragar justamente o nome de quem se deu ao trabalho de digitar certo.
        var cuidadoso = await NovoTorcedorAsync(ctx, "Leonardo DiCaprio", "55540000007");
        await servico.RegistrarVotoAsync(partida.Id, cuidadoso.Id, duplas[0].Id);

        var votantes = await servico.ObterVotantesAsync(partida.Id);
        var linha = Assert.Single(votantes.VotantesDupla1);
        Assert.Equal("Leonardo DiCaprio", linha.Nome);
    }

    [Fact]
    public void O_nome_do_votante_ocupa_UMA_LINHA_so()
    {
        var js = Ler("wwwroot", "js", "palpitrometro.js");

        var inicio = js.IndexOf("function montarLista", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a montagem da lista de votantes.");
        // ⚠️ A janela cresceu em 11/09/2026 junto com a função (a caixa de cada votante e o traço
        // de quem não palpitou placar). Quando ela ficar curta o teste falha por CORTE, não por
        // defeito — o sintoma é "Sub-string not found" com o código certo na frente.
        var trecho = js[inicio..Math.Min(js.Length, inicio + 3600)];

        // ⚠️ `text-truncate` sozinho não corta nada dentro de um flex: sem `min-width:0` o item
        // se recusa a encolher abaixo do conteúdo e quem sai empurrado é a ficha do placar.
        Assert.Contains("text-truncate", trecho);
        Assert.Contains("min-width:0", trecho);
    }

    // ─────────────────────────── O LAYOUT NO CELULAR ───────────────────────────
    //
    // 11/09/2026 — 🗣️ Felipe, num print do modal aberto no celular: *"estou com esse visual
    // estourado, ajuste"*. 🕳️ As duas duplas dividiam a largura em `col-6` FIXO: num modal de
    // ~360px sobravam ~150px por coluna pra foto (28px) + nome + ficha do placar. "Deivid
    // Francisco dos Santos" virava três linhas, e a foto — item de flex, que encolhe por padrão —
    // saía achatada em vez de redonda.

    [Fact]
    public void No_celular_as_duas_duplas_do_modal_EMPILHAM_em_vez_de_dividir_a_largura()
    {
        var fonte = Ler("Views", "Torneios", "_ModalVerVotos.cshtml");

        // `col-6` sem ponto de quebra é a coluna fixa que espremia o nome no celular.
        Assert.DoesNotContain("\"col-6\"", fonte);
        Assert.Equal(2, Contagem(fonte, "class=\"col-12 col-sm-6\""));
    }

    [Fact]
    public void O_modal_rola_por_DENTRO_em_vez_de_esticar_a_tela()
    {
        // Empilhar dobra a altura da lista: sem isto o modal cresce pra fora da tela e o título
        // (com o X de fechar) sobe junto — no print ele já ocupava a tela inteira com 15 votos.
        var fonte = Ler("Views", "Torneios", "_ModalVerVotos.cshtml");

        Assert.Contains("modal-dialog-scrollable", fonte);
    }

    [Fact]
    public void A_foto_e_a_ficha_do_placar_do_votante_nao_ENCOLHEM()
    {
        var js = Ler("wwwroot", "js", "palpitrometro.js");

        var inicio = js.IndexOf("function montarLista", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a montagem da lista de votantes.");
        // Mesma nota do teste acima: janela curta reprova por CORTE, não por defeito.
        var trecho = js[inicio..Math.Min(js.Length, inicio + 3600)];

        // ⚠️ `width:28px` num filho de flex é só o TAMANHO BASE: sem travar o encolhimento a foto
        // redonda vira oval quando o nome é longo, e o "9 x 0" quebra em duas linhas.
        Assert.Contains("rounded-circle flex-shrink-0", trecho);
        Assert.Contains("text-nowrap", trecho);
    }

    // ─────────────────────────── INFRA ───────────────────────────

    private static async Task<(Partida partida, List<Dupla> duplas)> MontarJogoAgendadoAsync(
        DbPadelContext ctx, int gamesDosGrupos = 6, int setsDosGrupos = 1)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = gamesDosGrupos;
        torneio.SetsFaseGrupos = setsDosGrupos;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = FasesTorneio.FaseDeGrupos,
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (partida, duplas);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static int Contagem(string fonte, string alvo)
    {
        var total = 0;
        for (var i = fonte.IndexOf(alvo, StringComparison.Ordinal); i >= 0;
             i = fonte.IndexOf(alvo, i + alvo.Length, StringComparison.Ordinal)) total++;
        return total;
    }

    private static string Ler(params string[] caminho) =>
        File.ReadAllText(Path.Combine(new[] { PastaDoProjeto() }.Concat(caminho).ToArray()));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
