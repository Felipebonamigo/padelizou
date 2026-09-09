using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A PORTA DA ABA DE PLANEJAMENTO DE QUADRAS (09/09/2026).
//
// 🗣️ O recorte de quem vê é do próprio pedido do Felipe: "que apenas o organizador e criador e
// nós do sistema poderemos ver". Os três já são a MESMA régua que o torneio inteiro usa —
// `EhOrganizadorAsync` cobre organizador, criador (`NivelAcesso = "Criador"`) e
// `IsAdminRaiz`/`IsAdminGeral` —, então aqui não nasce papel de acesso nenhum. O que estes
// testes guardam é que a régua foi de fato chamada.
//
// ⚠️ A aritmética NÃO está aqui: ela é de PlanejamentoDeQuadrasTests. Isto guarda a porta
// (quem entra, quem não entra) e o único caminho que GRAVA — o "Aplicar".
public class PlanejamentoDeQuadrasNaTelaTests
{
    // 12 duplas numa categoria = 4 grupos, 12 jogos de grupo + 7 de mata-mata = 19 jogos.
    private static (Torneio torneio, Jogador organizador, Jogador estranho) Cenario(DbPadelContext ctx)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);

        var estranho = new Jogador { Nome = "Estranho", Cpf = "11122233344" };
        ctx.Jogadores.Add(estranho);

        torneio.DataInicio = new DateTime(2026, 9, 11);
        torneio.DataFim = new DateTime(2026, 9, 13);
        torneio.QuantidadeQuadras = 2;
        ctx.SaveChanges();

        return (torneio, organizador, estranho);
    }

    private static PlanejamentoDeQuadrasVM Ver(ViewResult resultado) =>
        Assert.IsType<PlanejamentoDeQuadrasVM>(resultado.Model);

    [Fact]
    public async Task Quem_nao_organiza_nao_abre_o_planejamento()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, estranho) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: estranho.Id);

        var resposta = await controller.Planejamento(torneio.Id);

        Assert.IsType<ForbidResult>(resposta);
    }

    [Fact]
    public async Task Organizador_ve_o_plano_com_os_jogos_que_o_torneio_ja_tem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        var vm = Ver(Assert.IsType<ViewResult>(await controller.Planejamento(torneio.Id)));

        // O número não é digitado: sai das duplas que já estão inscritas, pela MESMA conta que
        // o painel "como essa grade vai ficar" usa antes do sorteio.
        Assert.Equal(19, vm.Plano.TotalDeJogos);
        Assert.False(vm.Simulando);
        Assert.False(vm.JogosJaSorteados);

        // O prazo do torneio vira o prazo do plano — é o que transforma "termina domingo" em
        // "falta quadra".
        Assert.True(vm.Plano.PrazoDefinido);
        Assert.Equal(new DateTime(2026, 9, 13), vm.Ate);
    }

    [Fact]
    public async Task Simular_outro_numero_de_jogos_nao_grava_nada_no_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        var vm = Ver(Assert.IsType<ViewResult>(
            await controller.Planejamento(torneio.Id, quadras: 6, jogos: 300, duracao: 30)));

        Assert.Equal(300, vm.Plano.TotalDeJogos);
        Assert.Equal(6, vm.Plano.Quadras);
        Assert.True(vm.Simulando);

        // ⚠️ O PONTO DO TESTE: simular é PERGUNTA, não mudança. Enquanto o organizador não
        // apertar "Aplicar", o torneio continua com as 2 quadras e os 50 min dele.
        var gravado = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(2, gravado!.QuantidadeQuadras);
        Assert.Equal(50, gravado.TempoPrevistoPartidaMinutos);
    }

    [Fact]
    public async Task Aplicar_grava_os_horarios_que_o_organizador_escolheu()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        var resposta = await controller.AplicarPlanejamento(torneio.Id,
            dataInicio: new DateTime(2026, 9, 11),
            horaInicio: new TimeSpan(17, 0, 0),
            horaSeguintes: new TimeSpan(7, 30, 0),
            horaFim: new TimeSpan(22, 0, 0),
            duracao: 40);

        Assert.IsType<RedirectToActionResult>(resposta);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(new TimeSpan(17, 0, 0), gravado!.HoraInicioDoDia);
        Assert.Equal(new TimeSpan(7, 30, 0), gravado.HoraInicioDiasSeguintes);
        Assert.Equal(new TimeSpan(22, 0, 0), gravado.HoraFimDoDia);
        Assert.Equal(40, gravado.TempoPrevistoPartidaMinutos);
    }

    [Fact]
    public async Task Aplicar_recusa_quem_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, estranho) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: estranho.Id);

        var resposta = await controller.AplicarPlanejamento(torneio.Id,
            horaInicio: new TimeSpan(6, 0, 0), horaFim: new TimeSpan(23, 0, 0));

        Assert.IsType<ForbidResult>(resposta);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(new TimeSpan(18, 0, 0), gravado!.HoraInicioDoDia);
    }

    // ⚠️ FRONTEIRA DE CONFIANÇA, e por isso não cabe na escada de "quanto código o pedido
    // merece": um dia que fecha antes de abrir faz `GradeDeJogos.Horarios` tratar o dia como
    // SEM VIRADA — a grade inteira empilha no primeiro dia, varando a madrugada. Gravar isso
    // em silêncio seria estragar o sorteio seguinte pra quem só queria planejar.
    [Theory]
    [InlineData(8, 0, 7, 0)]    // fecha antes de abrir
    [InlineData(8, 0, 8, 0)]    // fecha na hora de abrir
    public async Task Aplicar_recusa_dia_que_fecha_antes_de_abrir(
        int abreHora, int abreMin, int fechaHora, int fechaMin)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.AplicarPlanejamento(torneio.Id,
            horaSeguintes: new TimeSpan(abreHora, abreMin, 0),
            horaFim: new TimeSpan(fechaHora, fechaMin, 0));

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(new TimeSpan(23, 50, 0), gravado!.HoraFimDoDia);
        Assert.Equal(new TimeSpan(8, 0, 0), gravado.HoraInicioDiasSeguintes);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // Depois do sorteio o número de jogos é FATO, não projeção — e o planejamento tem que
    // contar os jogos que existem, senão ele responderia sobre um torneio imaginário.
    [Fact]
    public async Task Depois_do_sorteio_o_plano_conta_os_jogos_que_existem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);

        var duplas = ctx.Duplas.Take(2).ToList();
        for (int i = 0; i < 5; i++)
        {
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = duplas[0].CategoriaId,
                Codigo = $"JG{i:00}",
                Dupla1Id = duplas[0].Id,
                Dupla2Id = duplas[1].Id,
                Status = "Agendada",
            });
        }
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        var vm = Ver(Assert.IsType<ViewResult>(await controller.Planejamento(torneio.Id)));

        Assert.True(vm.JogosJaSorteados);
        Assert.Equal(5, vm.Plano.TotalDeJogos);
    }

    // ⚠️ DUAS TELAS, UM TORNEIO. A aba do `Details` mostra o resumo e a tela cheia mostra a
    // conta inteira — e as duas respondem sobre o MESMO torneio, com as MESMAS configurações.
    // Enquanto cada uma montasse o próprio plano, bastava uma normalização diferente (a duração
    // 0 que vira 50, o `Math.Max(1, quadras)`) pra aba dizer "cabe" e a tela dizer "faltam 15" —
    // e o organizador acreditaria na que viu primeiro.
    [Fact]
    public async Task O_resumo_da_aba_e_a_tela_cheia_contam_a_mesma_coisa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);

        // Configuração que faz a conta DOER: 1 quadra e o fim de semana inteiro pra fechar.
        torneio.QuantidadeQuadras = 1;
        torneio.HoraInicioDoDia = new TimeSpan(19, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(19, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(22, 0, 0);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);
        var naAba = Assert.IsType<PlanejamentoDeQuadras.Plano>(controller.ViewBag.PlanejamentoResumo);

        var naTela = Ver(Assert.IsType<ViewResult>(await controller.Planejamento(torneio.Id))).Plano;

        Assert.Equal(naTela.TotalDeJogos, naAba.TotalDeJogos);
        Assert.Equal(naTela.Vagas, naAba.Vagas);
        Assert.Equal(naTela.Faltam, naAba.Faltam);
        Assert.Equal(naTela.QuadrasNecessarias, naAba.QuadrasNecessarias);
        Assert.Equal(naTela.UltimoJogoTermina, naAba.UltimoJogoTermina);

        // Sanidade: com 1 quadra e 3 horas por dia, ESTE cenário tem que faltar quadra —
        // senão as duas telas concordariam em "cabe" e o teste passaria sem exercitar nada.
        Assert.True(naAba.Faltam > 0, "o cenário parou de apertar; escolha outro");
    }

    // ⚠️ O BOTÃO "APLICAR" SÓ PODE ACENDER PELO QUE ELE DE FATO GRAVA. O planejador tem sete
    // botões, e o POST escreve CINCO deles: quadras não se aplicam daqui (quadra tem nome e
    // clube — ver o comentário do AplicarPlanejamento), e a hora de fechar por dia nem existe
    // como coluna. Acender o "Aplicar" quando o organizador mexe num desses dois seria a pior
    // mentira possível desta tela: ele aperta achando que comprou a terceira quadra.
    [Fact]
    public async Task Mexer_no_que_o_aplicar_nao_grava_nao_acende_o_botao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        // Quadras e hora de fechar do domingo: nenhum dos dois é gravável por aqui.
        var soSimulacao = Ver(Assert.IsType<ViewResult>(await controller.Planejamento(
            torneio.Id, quadras: 6, limites: "2026-09-13=14:00")));

        Assert.False(soSimulacao.MudouAlgo);
        Assert.Equal(6, soSimulacao.Plano.Quadras);   // simulou mesmo, só não é aplicável

        // Já a hora de início É gravável — e aí o botão tem o que fazer.
        var horarioNovo = Ver(Assert.IsType<ViewResult>(await controller.Planejamento(
            torneio.Id, horaInicio: new TimeSpan(16, 0, 0))));

        Assert.True(horarioNovo.MudouAlgo);
    }

    // ⚠️ A ABA É ESCONDIDA NA VIEW, e é por isso que existe teste LENDO A VIEW: o servidor
    // recusa `Torneios/Planejamento` pra quem não organiza (os dois testes lá em cima), mas o
    // resumo da aba é HTML já renderizado dentro do `Details` — a página que todo jogador
    // abre. Um `@@if` perdido numa edição futura não daria erro nenhum: entregaria a conta de
    // quadra alugada do organizador pra 63 duplas, calada.
    //
    // 🗣️ E o recorte é do pedido: "que apenas o organizador e criador e nós do sistema poderemos ver".
    [Theory]
    [InlineData("data-bs-target=\"#planejamento\"")]   // o botão da aba
    [InlineData("id=\"planejamento\"")]                // o painel com os números
    public void A_aba_de_planejamento_so_existe_dentro_do_gate_de_quem_gerencia(string marcaDaAba)
    {
        var linhas = File.ReadAllLines(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        var ondeAparece = linhas
            .Select((linha, i) => (linha, i))
            .Where(x => x.linha.Contains(marcaDaAba))
            .Select(x => x.i)
            .ToList();

        Assert.True(ondeAparece.Count > 0, $"Não achei '{marcaDaAba}' no Details.cshtml — a aba mudou de forma?");

        foreach (var linha in ondeAparece)
        {
            // O gate tem que estar ACIMA e PERTO: um `@@if` a 200 linhas de distância seria de
            // outro bloco, e a checagem viraria enfeite.
            bool protegida = Enumerable.Range(Math.Max(0, linha - 20), Math.Min(20, linha))
                .Any(i => linhas[i].Contains("ViewBag.PodeGerenciar == true"));

            Assert.True(protegida,
                $"A aba de planejamento (linha {linha + 1} do Details.cshtml) não está dentro de "
                + "um @if (ViewBag.PodeGerenciar == true). Do jeito que está, qualquer jogador "
                + "inscrito enxerga o planejamento de quadras do organizador.");
        }
    }

    // A raiz do projeto a partir da pasta de saída dos testes — mesmo caminho que
    // CamposDoPainelDePrevisaoTests usa pra ler o Create.cshtml.
    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Padelizou");
    }
}
