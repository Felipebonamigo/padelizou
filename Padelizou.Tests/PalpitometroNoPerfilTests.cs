using System.IO;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — A PRÓPRIA PESSOA DECIDE, NO PERFIL, SE VÊ O PALPITÔMETRO E SE VÊ OS NOMES.
//
// 🗣️ Felipe: *"e tambem a propria pessoa escolhe no seu perfil, se ela quer ver o palpitometro
// ou nao, e se quer ver os nomes de quem votou ou nao"*.
//
// 🔑 A PREFERÊNCIA DA PESSOA SÓ **SUBTRAI**, NUNCA SOMA. O organizador manda no torneio dele
// (`Torneio.PalpitometroEm`); a pessoa manda na tela dela. Quem desligou não vê; quem deixou
// ligado vê **se o organizador liberou**. As duas perguntas são um `&&`, e é por isso que elas
// não podem virar duas verdades que brigam — a régua mora num lugar só.
//
// ⚖️ PERGUNTADO, O FELIPE ESCOLHEU (14/09/2026):
//   · "ver os nomes de quem votou" é sobre **o que EU vejo** — não sobre o meu nome sumir da
//     lista dos outros. Quem desliga deixa de ver o modal "quem votou em quem"; o nome dela
//     continua aparecendo pra quem quer ver.
//   · desligar o palpitômetro **some com tudo**: o bloco nos jogos, a aba de palpiteiros do
//     torneio E a do ranking (com o card de compartilhar junto, que sai da mesma lista).
//
// ⚠️ ISTO NÃO É AUTORIZAÇÃO, É EXIBIÇÃO — e a diferença está testada lá embaixo. O servidor
// NÃO recusa o voto por causa da preferência da pessoa: ela é dona da própria tela, e um POST
// dela é ela mudando de ideia. Quem o servidor recusa é o alcance do ORGANIZADOR, que é regra
// do torneio e vale contra POST montado à mão.
public class PalpitometroNoPerfilTests
{
    // ─────────────────────────── A RÉGUA ───────────────────────────

    [Fact]
    public void Jogador_novo_NASCE_vendo_o_palpitometro_E_os_nomes()
    {
        // É o comportamento que o sistema sempre teve — as duas chaves existem pra quem não
        // quer, não porque a maioria fosse querer desligar.
        var novo = new Jogador();

        Assert.True(novo.VerPalpitometro);
        Assert.True(novo.VerQuemPalpitou);
    }

    [Theory]
    // organizador liberou + a pessoa quer = vê
    [InlineData(AlcanceDoPalpitometro.Todas, "3ª Categoria Feminina", true, true)]
    // organizador liberou + a pessoa NÃO quer = não vê
    [InlineData(AlcanceDoPalpitometro.Todas, "3ª Categoria Feminina", false, false)]
    // organizador desligou + a pessoa quer = NÃO vê (a preferência não SOMA)
    [InlineData(AlcanceDoPalpitometro.Nenhuma, "3ª Categoria Feminina", true, false)]
    // categoria fora do alcance + a pessoa quer = NÃO vê (idem)
    [InlineData(AlcanceDoPalpitometro.Masculina, "3ª Categoria Feminina", true, false)]
    // os dois desligados = não vê, e não há o que discutir
    [InlineData(AlcanceDoPalpitometro.Nenhuma, "3ª Categoria Masculina", false, false)]
    public void A_preferencia_da_pessoa_so_SUBTRAI_do_que_o_organizador_liberou(
        string alcance, string categoria, bool aPessoaQuerVer, bool veOPalpitometro)
    {
        Assert.Equal(veOPalpitometro, AlcanceDoPalpitometro.Libera(alcance, categoria, aPessoaQuerVer));
    }

    [Fact]
    public void A_regua_DO_TORNEIO_continua_existindo_sozinha_pro_servidor()
    {
        // ⚠️ As duas sobrecargas são de propósito, e a diferença é o assunto: a de DOIS
        // argumentos é a regra do TORNEIO — é ela que o `PalpiteService` usa pra recusar voto,
        // onde a preferência de exibição da pessoa não tem o que dizer. A de TRÊS é a da TELA.
        // Uma só, com a preferência embutida, faria o servidor recusar o voto de quem
        // simplesmente não quer o bloco na frente.
        Assert.True(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Todas, "3ª Feminina"));
        Assert.False(AlcanceDoPalpitometro.Libera(AlcanceDoPalpitometro.Todas, "3ª Feminina", aPessoaQuerVer: false));
    }

    // ─────────────────── EXIBIÇÃO NÃO É AUTORIZAÇÃO ───────────────────

    [Fact]
    public async Task O_servidor_NAO_recusa_o_voto_por_causa_da_preferencia_da_pessoa()
    {
        // ⚠️ ESTE TESTE GUARDA UMA DECISÃO, não um defeito. É tentador "fechar" aqui também —
        // mas a chave do perfil é sobre o que a pessoa VÊ, e ela é dona da própria tela. Um
        // POST vindo de quem desligou o bloco é ela mudando de ideia (ou outra aba aberta), e
        // recusar seria o sistema discutindo com o dono do palpite. Quem o servidor recusa é o
        // ALCANCE DO ORGANIZADOR — isso sim é regra do torneio, e tem teste próprio em
        // PalpitometroPorCategoriaTests.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = 6;
        torneio.SetsFaseGrupos = 1;
        torneio.PalpitometroEm = AlcanceDoPalpitometro.Todas;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = "Grupos",
            Codigo = "J1",
        };
        ctx.Partidas.Add(partida);

        var quemDesligou = new Jogador
        {
            Nome = "Não Quero Ver",
            Cpf = "55552000001",
            VerPalpitometro = false,
            VerQuemPalpitou = false,
        };
        ctx.Jogadores.Add(quemDesligou);
        await ctx.SaveChangesAsync();

        var resumo = await new PalpiteService(ctx)
            .RegistrarVotoAsync(partida.Id, quemDesligou.Id, duplas[0].Id);

        Assert.Equal(duplas[0].Id, resumo.MeuVotoDuplaId);
    }

    // ─────────────────── A ESCOLHA, NA TELA DE PREFERÊNCIAS ───────────────────

    [Fact]
    public async Task A_tela_de_preferencias_grava_as_DUAS_escolhas()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Eu", Cpf = "55552000002" };
        ctx.Jogadores.Add(eu);
        await ctx.SaveChangesAsync();

        Assert.True(eu.VerPalpitometro);   // nascem ligadas
        Assert.True(eu.VerQuemPalpitou);

        await SalvarPreferenciasAsync(ctx, eu.Id, verPalpitometro: false, verQuemPalpitou: false);

        var depois = await ctx.Jogadores.FindAsync(eu.Id);
        Assert.False(depois!.VerPalpitometro);
        Assert.False(depois.VerQuemPalpitou);

        // E religa — a chave não é de mão única.
        await SalvarPreferenciasAsync(ctx, eu.Id, verPalpitometro: true, verQuemPalpitou: true);

        depois = await ctx.Jogadores.FindAsync(eu.Id);
        Assert.True(depois!.VerPalpitometro);
        Assert.True(depois.VerQuemPalpitou);
    }

    [Fact]
    public async Task Formulario_ANTIGO_nao_RELIGA_o_que_a_pessoa_desligou()
    {
        // ⚠️ É por isto que os dois parâmetros são `bool?` e não `bool`, ao contrário dos oito
        // vizinhos desta mesma ação: eles nascem LIGADOS. Com `bool` e padrão `true`, uma aba
        // aberta antes deste deploy — que não manda o campo — RELIGARIA o palpitômetro de quem
        // desligou, a cada salvamento de qualquer outra preferência. Com padrão `false`, a
        // mesma aba DESLIGARIA o de todo mundo. Nulo = o campo não veio, e o gravado FICA.
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador
        {
            Nome = "Desliguei",
            Cpf = "55552000004",
            VerPalpitometro = false,
            VerQuemPalpitou = false,
        };
        ctx.Jogadores.Add(eu);
        await ctx.SaveChangesAsync();

        await SalvarPreferenciasAsync(ctx, eu.Id, verPalpitometro: null, verQuemPalpitou: null);

        var depois = await ctx.Jogadores.FindAsync(eu.Id);
        Assert.False(depois!.VerPalpitometro);
        Assert.False(depois.VerQuemPalpitou);
    }

    [Fact]
    public void As_duas_caixas_existem_na_tela_de_preferencias()
    {
        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Auth", "_PreferenciasFields.cshtml")));

        Assert.Contains("name=\"verPalpitometro\"", fonte);
        Assert.Contains("name=\"verQuemPalpitou\"", fonte);
    }

    // ─────────────────────────── O QUE SOME DA TELA ───────────────────────────

    [Fact]
    public async Task A_lista_de_jogos_leva_as_DUAS_preferencias_pra_tela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        organizador.VerPalpitometro = false;
        organizador.VerQuemPalpitou = false;
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Jogos(torneio.Id, null, null));

        Assert.Equal(false, view.ViewData["VerPalpitometro"]);
        Assert.Equal(false, view.ViewData["VerQuemPalpitou"]);
    }

    [Fact]
    public async Task SEM_perfil_conhecido_a_tela_mostra_TUDO()
    {
        // O controle, e ele guarda o caminho do VISITANTE: a preferência é de quem tem perfil, e
        // esta página é PÚBLICA. Sem jogador pra perguntar, o padrão tem que ser o de sempre —
        // senão o palpitômetro sumiria justamente pra quem a barra e o convite existem pra
        // atrair. Aqui isso é exercitado com um id que não existe no banco, que é exatamente o
        // `FindAsync` devolvendo nulo por onde o deslogado passa.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 0).Jogos(torneio.Id, null, null));

        Assert.Equal(true, view.ViewData["VerPalpitometro"]);
        Assert.Equal(true, view.ViewData["VerQuemPalpitou"]);
    }

    [Theory]
    [InlineData("_JogoEmLinha.cshtml")]
    [InlineData("_JogosDoTorneio.cshtml")]
    public void As_DUAS_apresentacoes_do_jogo_leem_a_preferencia_da_pessoa(string view)
    {
        // Mesma razão do teste irmão em PalpitometroPorCategoriaTests: a lista e o cartão do AO
        // VIVO têm markup PRÓPRIO. Uma preferência respeitada num e ignorada no outro é pior
        // que não ter a chave.
        var fonte = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view)));

        Assert.Contains("VerPalpitometro", fonte);
    }

    [Theory]
    [InlineData("_JogoEmLinha.cshtml")]
    [InlineData("_Palpitometro.cshtml")]
    public void TODO_botao_de_ver_quem_votou_obedece_a_preferencia(string view)
    {
        // ⚠️ SÃO QUATRO CHAMADAS de `verVotos(` espalhadas em dois arquivos — duas em cada. Um
        // botão esquecido entrega exatamente o que a pessoa pediu pra não ver, e ninguém
        // reporta: quem desligou não volta pra conferir se sobrou um.
        var caminho = Path.Combine(PastaDoProjeto(), "Views", "Torneios", view);
        var fonte = TestInfra.SemComentarios(File.ReadAllText(caminho));

        var chamadas = System.Text.RegularExpressions.Regex.Matches(fonte, @"verVotos\(");
        Assert.True(chamadas.Count > 0, $"Não achei nenhuma chamada de verVotos em {view}.");

        // ⚠️ CONTAR OCORRÊNCIAS NÃO SERVE, e a primeira versão deste teste errou por isso: o
        // `@if` fica ACIMA do botão e a guarda é uma variável local, então um arquivo com duas
        // guardas e três botões passaria. O que se cobra é POSIÇÃO — cada `verVotos(` tem que
        // ter a preferência mencionada no pedaço de markup logo antes dele.
        foreach (System.Text.RegularExpressions.Match chamada in chamadas)
        {
            var antes = fonte[Math.Max(0, chamada.Index - 700)..chamada.Index];
            Assert.True(antes.Contains("verQuemPalpitou", StringComparison.Ordinal),
                $"Em {view} há um botão de ver quem votou (posição {chamada.Index}) sem a guarda "
                + "`verQuemPalpitou` antes dele — ele entregaria os nomes a quem pediu pra não ver.");
        }
    }

    [Fact]
    public async Task A_aba_de_palpiteiros_DO_TORNEIO_some_pra_quem_desligou()
    {
        // 🗣️ Decisão do Felipe: desligar some com TUDO, ranking incluído.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.GamesFaseGrupos = 6;
        torneio.SetsFaseGrupos = 1;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = "Grupos",
            Codigo = "J1",
        };
        ctx.Partidas.Add(partida);

        var torcedor = new Jogador { Nome = "Torcedor", Cpf = "55552000003" };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();

        await new PalpiteService(ctx).RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id);

        // Com a chave ligada (o padrão), a aba existe pra quem olha.
        var comAAba = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Details(torneio.Id, null, null));
        Assert.True(comAAba.ViewData["TemRankingDePalpiteiros"] as bool?);

        // Desligada, some.
        organizador.VerPalpitometro = false;
        await ctx.SaveChangesAsync();

        var semAAba = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Details(torneio.Id, null, null));
        Assert.False(semAAba.ViewData["TemRankingDePalpiteiros"] as bool?);
    }

    // ─────────────────────────── INFRA ───────────────────────────

    private static async Task SalvarPreferenciasAsync(
        DbPadelContext ctx, int jogadorId, bool? verPalpitometro, bool? verQuemPalpitou)
    {
        var controller = TestInfra.NovoAuthController(ctx, jogadorId);
        await controller.Preferencias(
            ladoQuadra: null, lateralidade: null, instagram: null, perfilPrivado: false,
            notificarEmail: true, notificarWhatsApp: false, aceitaConvitesJogo: true,
            notificarTorneiosAbertos: true, notificarSeguidosTorneio: true,
            notificarAvisoJogo: true, notificarJogoAula: true, notificarRaqueteLivre: true,
            notificarHorarioVagoRegiao: false,
            verPalpitometro: verPalpitometro, verQuemPalpitou: verQuemPalpitou,
            categoriasSelecionadas: null, clubesSelecionados: null,
            diasHorariosSelecionados: null, cidadesSelecionadas: null);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
