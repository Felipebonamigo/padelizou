using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A PÁGINA DE TIMES: títulos, presidente, elenco por categoria, o mais vitorioso, as sedes —
// e a aba de transferências ao lado.
//
// O que estes testes guardam são as três coisas que a tela consegue MENTIR sem quebrar:
//   · contar como troféu de alguém a taça de uma dupla-TIME (onde o Jogador1Id é o organizador
//     que digitou a lista, não quem jogou);
//   · exibir campeão de torneio CANCELADO, que a página do próprio torneio não exibe;
//   · perder uma troca de camisa que não passou pelo registro de transferências — o buraco
//     invisível, porque o jogador aparece certinho no time novo e só o histórico fica furado.
public class PaginaDeTimesTests
{
    // ── Elenco por categoria ──────────────────────────────────────────────────────────

    private sealed record Membro(string Nome, params string[] Categorias);

    private static List<ElencoPorCategoria.Grupo<Membro>> Agrupar(params Membro[] membros) =>
        ElencoPorCategoria.Agrupar(membros, m => m.Categorias);

    [Fact]
    public void Elenco_reparte_o_time_pelas_categorias_na_ordem_das_outras_telas()
    {
        var grupos = Agrupar(
            new Membro("Bruno", "6ª Categoria Masculina"),
            new Membro("Rafael", "Open Masculina"),
            new Membro("Camila", "4ª Categoria Feminina"));

        // Masculinas da mais forte pra mais fraca, depois as femininas — a mesma escada de
        // CategoriaNaTela, que é o que o resto do site usa.
        Assert.Equal(
            new[] { "Open Masculina", "6ª Masculina", "4ª Feminina" },
            grupos.Select(g => g.Curto));
    }

    [Fact]
    public void Quem_aceita_duas_categorias_aparece_nas_duas()
    {
        // É o ponto da tela: quem procura parceiro na 5ª precisa ver quem joga a 5ª, mesmo que
        // a pessoa também jogue a 4ª. Mostrá-la só na mais forte a esconderia de metade do time.
        var grupos = Agrupar(new Membro("Diego", "4ª Categoria Masculina", "5ª Categoria Masculina"));

        Assert.Equal(2, grupos.Count);
        Assert.All(grupos, g => Assert.Single(g.Membros));
    }

    [Fact]
    public void Quem_nao_marcou_categoria_cai_num_balde_no_fim_e_nao_some()
    {
        var grupos = Agrupar(
            new Membro("Sem preferência"),
            new Membro("Rafael", "Open Masculina"));

        Assert.Equal("Open Masculina", grupos[0].Curto);

        var balde = grupos[1];
        Assert.True(balde.EhSemCategoria);
        Assert.Single(balde.Membros);

        // ⚠️ O rótulo do balde NÃO passa pelo `Curto`, que tira a palavra "Categoria" de
        // qualquer lugar do texto: "Sem categoria informada" viraria "Sem informada".
        Assert.Equal(ElencoPorCategoria.SemCategoria, balde.Curto);
    }

    // ── Presidente ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Presidente_e_o_administrador_mais_antigo()
    {
        var primeiro = new AdministradorTimeVM
        {
            Jogador = new Jogador { Id = 1, Nome = "Rafael", Cpf = "1" },
            ConcedidoEm = new DateTime(2026, 1, 10),
        };
        var depois = new AdministradorTimeVM
        {
            Jogador = new Jogador { Id = 2, Nome = "Bruno", Cpf = "2" },
            ConcedidoEm = new DateTime(2026, 8, 1),
        };

        // Ordem de entrada invertida de propósito: quem manda é a data, não a posição na lista.
        var presidente = AdministracaoTime.Presidente(new[] { depois, primeiro }, a => a.ConcedidoEm);

        Assert.Same(primeiro, presidente);
    }

    [Fact]
    public void Time_sem_administrador_fica_sem_presidente()
    {
        // O estado da maioria dos 44 times importados do ranking. A tela diz que não há
        // presidente em vez de eleger alguém por conta própria.
        Assert.Null(AdministracaoTime.Presidente(
            Array.Empty<AdministradorTimeVM>(), a => a.ConcedidoEm));
    }

    // ── O mais vitorioso ──────────────────────────────────────────────────────────────

    [Fact]
    public void Empate_no_topo_mostra_todos_os_empatados()
    {
        var rafael = new Jogador { Id = 1, Nome = "Rafael", Cpf = "1" };
        var bruno = new Jogador { Id = 2, Nome = "Bruno", Cpf = "2" };
        var diego = new Jogador { Id = 3, Nome = "Diego", Cpf = "3" };

        var vencedores = TitulosDoTime.MaioresVencedores(
            new[] { rafael, bruno, diego },
            new Dictionary<int, int> { [1] = 5, [2] = 5, [3] = 2 });

        // Eleger um dos dois pelo nome faria o destaque mentir sobre o outro.
        Assert.Equal(new[] { "Bruno", "Rafael" }, vencedores.Select(v => v.Jogador.Nome));
        Assert.All(vencedores, v => Assert.Equal(5, v.Trofeus));
    }

    [Fact]
    public void Elenco_sem_nenhum_titulo_nao_tem_destaque()
    {
        // É o pedido explícito: sem troféu, a seção inteira não aparece na tela.
        var vencedores = TitulosDoTime.MaioresVencedores(
            new[] { new Jogador { Id = 1, Nome = "Rafael", Cpf = "1" } },
            new Dictionary<int, int>());

        Assert.Empty(vencedores);
    }

    // ── Títulos, contra o banco ───────────────────────────────────────────────────────

    // Um torneio com uma categoria, pronto pra receber duplas campeãs.
    private static Categoria CategoriaDe(DbPadelContext ctx, string statusDoTorneio, string nome = "3ª Categoria Masculina")
    {
        var torneio = new Torneio
        {
            Nome = $"Torneio {statusDoTorneio}",
            Codigo = Guid.NewGuid().ToString("N")[..6],
            Status = statusDoTorneio,
            DataInicio = new DateTime(2026, 7, 1),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = nome, Codigo = "CAT", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        return categoria;
    }

    [Fact]
    public async Task Titulo_de_torneio_cancelado_nao_entra_na_estante()
    {
        var ctx = TestInfra.NovoContexto();
        var organizador = new Jogador { Id = 1, Nome = "Organizador", Cpf = "1" };
        ctx.Jogadores.Add(organizador);
        var time = new Time { Id = 10, Nome = "Nata Padel" };
        ctx.Times.Add(time);
        ctx.SaveChanges();

        var valeu = CategoriaDe(ctx, "Finalizado");
        var naoAconteceu = CategoriaDe(ctx, CancelamentoDoTorneio.Status);

        ctx.Duplas.AddRange(
            new Dupla { Categoria = valeu, Jogador1Id = 1, NomeTime = "Nata Padel", TimeId = 10, UltimaFase = CampeoesDoTorneio.FaseDeCampeao },
            new Dupla { Categoria = naoAconteceu, Jogador1Id = 1, NomeTime = "Nata Padel", TimeId = 10, UltimaFase = CampeoesDoTorneio.FaseDeCampeao });
        await ctx.SaveChangesAsync();

        var titulos = await TitulosDoTime.DoTimeAsync(ctx, 10);

        // Se a página do torneio não anuncia aquele campeão, a estante do time não pode exibi-lo.
        Assert.Single(titulos);
        Assert.Equal("Torneio Finalizado", titulos[0].Torneio);
    }

    [Fact]
    public async Task Taca_de_dupla_TIME_nao_vira_trofeu_de_quem_cadastrou_o_time()
    {
        var ctx = TestInfra.NovoContexto();
        var organizador = new Jogador { Id = 1, Nome = "Quem digitou a lista", Cpf = "1" };
        var atleta = new Jogador { Id = 2, Nome = "Quem jogou", Cpf = "2" };
        ctx.Jogadores.AddRange(organizador, atleta);
        ctx.Times.Add(new Time { Id = 10, Nome = "Nata Padel" });
        ctx.SaveChanges();

        var categoria = CategoriaDe(ctx, "Finalizado");

        ctx.Duplas.AddRange(
            // A dupla-TIME: o Jogador1Id aponta pro organizador que cadastrou, não pra quem jogou.
            new Dupla { Categoria = categoria, Jogador1Id = 1, NomeTime = "Nata Padel", TimeId = 10, UltimaFase = CampeoesDoTorneio.FaseDeCampeao },
            // A dupla de gente: esta sim coroa as pessoas.
            new Dupla { Categoria = categoria, Jogador1Id = 2, UltimaFase = CampeoesDoTorneio.FaseDeCampeao });
        await ctx.SaveChangesAsync();

        var trofeus = await TitulosDoTime.TrofeusPorJogadorAsync(ctx, new[] { 1, 2 });

        Assert.False(trofeus.ContainsKey(1));   // o organizador não ganhou nada
        Assert.Equal(1, trofeus[2]);
    }

    [Fact]
    public async Task A_taca_da_dupla_conta_pros_DOIS()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Rafael", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Bruno", Cpf = "2" });
        ctx.SaveChanges();

        var categoria = CategoriaDe(ctx, "Finalizado");
        ctx.Duplas.Add(new Dupla
        {
            Categoria = categoria,
            Jogador1Id = 1,
            Jogador2Id = 2,
            UltimaFase = CampeoesDoTorneio.FaseDeCampeao,
        });
        await ctx.SaveChangesAsync();

        var trofeus = await TitulosDoTime.TrofeusPorJogadorAsync(ctx, new[] { 1, 2 });

        Assert.Equal(1, trofeus[1]);
        Assert.Equal(1, trofeus[2]);
    }

    // ── Sedes ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Time_pode_ter_mais_de_uma_sede_e_trocar_a_lista_inteira()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Arena Beira Rio" },
            new Clube { Id = 2, Nome = "Padel Garden" },
            new Clube { Id = 3, Nome = "Smash Padel" });
        ctx.Times.Add(new Time { Id = 10, Nome = "Nata Padel" });
        await ctx.SaveChangesAsync();

        await SedesDoTime.DefinirAsync(ctx, 10, new[] { 1, 2 });
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { "Arena Beira Rio", "Padel Garden" },
            (await SedesDoTime.DoTimeAsync(ctx, 10)).Select(c => c.Nome));

        // Trocar a lista tira o que saiu e põe o que entrou.
        await SedesDoTime.DefinirAsync(ctx, 10, new[] { 2, 3 });
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { "Padel Garden", "Smash Padel" },
            (await SedesDoTime.DoTimeAsync(ctx, 10)).Select(c => c.Nome));
    }

    [Fact]
    public async Task Clube_que_nao_existe_vira_sede_a_menos_e_nao_erro_de_gravacao()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena Beira Rio" });
        ctx.Times.Add(new Time { Id = 10, Nome = "Nata Padel" });
        await ctx.SaveChangesAsync();

        // O 99 não existe: descartado aqui, em vez de estourar a chave estrangeira longe daqui.
        await SedesDoTime.DefinirAsync(ctx, 10, new[] { 1, 99 });
        await ctx.SaveChangesAsync();

        Assert.Single(await SedesDoTime.DoTimeAsync(ctx, 10));
    }

    // ── Transferências ────────────────────────────────────────────────────────────────

    private static DbPadelContext CenarioDeTransferencias()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Times.AddRange(
            new Time { Id = 10, Nome = "Nata Padel" },
            new Time { Id = 20, Nome = "Clube dos Feras" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Rafael Souza", Cpf = "20000000001" },
            new Jogador { Id = 2, Nome = "Bruno Alves", Cpf = "20000000002" });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Trocar_de_time_deixa_rastro_na_janela_de_transferencias()
    {
        var ctx = CenarioDeTransferencias();
        var rafael = await ctx.Jogadores.FindAsync(1);

        Assert.True(TransferenciasDeTime.Registrar(ctx, rafael!, 10));   // estreia
        await ctx.SaveChangesAsync();
        Assert.True(TransferenciasDeTime.Registrar(ctx, rafael!, 20));   // troca
        await ctx.SaveChangesAsync();

        var movimentos = await TransferenciasDeTime.RecentesAsync(ctx);

        Assert.Equal(2, movimentos.Count);
        Assert.Equal(20, rafael!.TimeId);

        // O mais recente primeiro.
        Assert.Equal("Rafael Souza saiu do Nata Padel e foi pro Clube dos Feras",
            TransferenciasDeTime.Frase(movimentos[0]));
        Assert.Equal("Rafael Souza vestiu a camisa do Nata Padel",
            TransferenciasDeTime.Frase(movimentos[1]));
    }

    [Fact]
    public async Task Salvar_o_perfil_sem_mexer_no_time_nao_inventa_transferencia()
    {
        var ctx = CenarioDeTransferencias();
        var rafael = await ctx.Jogadores.FindAsync(1);
        rafael!.TimeId = 10;
        await ctx.SaveChangesAsync();

        // Sem isto, a aba encheria de "Fulano saiu do Nata e entrou no Nata" a cada
        // salvamento de perfil.
        Assert.False(TransferenciasDeTime.Registrar(ctx, rafael, 10));
        await ctx.SaveChangesAsync();

        Assert.Empty(await TransferenciasDeTime.RecentesAsync(ctx));
    }

    [Fact]
    public async Task Sair_do_time_sem_entrar_em_outro_e_uma_saida()
    {
        var ctx = CenarioDeTransferencias();
        var bruno = await ctx.Jogadores.FindAsync(2);
        TransferenciasDeTime.Registrar(ctx, bruno!, 10);
        await ctx.SaveChangesAsync();
        TransferenciasDeTime.Registrar(ctx, bruno!, null);
        await ctx.SaveChangesAsync();

        var movimentos = await TransferenciasDeTime.RecentesAsync(ctx);

        Assert.Null(bruno!.TimeId);
        Assert.Equal("Bruno Alves deixou o Nata Padel", TransferenciasDeTime.Frase(movimentos[0]));
    }

    [Fact]
    public async Task Filtro_separa_o_que_entrou_do_que_saiu_do_time()
    {
        var ctx = CenarioDeTransferencias();
        var rafael = await ctx.Jogadores.FindAsync(1);
        var bruno = await ctx.Jogadores.FindAsync(2);

        TransferenciasDeTime.Registrar(ctx, rafael!, 10);        // entra no Nata
        TransferenciasDeTime.Registrar(ctx, bruno!, 20);         // entra nos Feras
        await ctx.SaveChangesAsync();
        TransferenciasDeTime.Registrar(ctx, bruno!, 10);         // sai dos Feras, entra no Nata
        await ctx.SaveChangesAsync();

        var entradasNoNata = await TransferenciasDeTime.RecentesAsync(ctx, 10, TransferenciasDeTime.Sentido.Entradas);
        Assert.Equal(2, entradasNoNata.Count);

        var saidasDoNata = await TransferenciasDeTime.RecentesAsync(ctx, 10, TransferenciasDeTime.Sentido.Saidas);
        Assert.Empty(saidasDoNata);

        var saidasDosFeras = await TransferenciasDeTime.RecentesAsync(ctx, 20, TransferenciasDeTime.Sentido.Saidas);
        Assert.Single(saidasDosFeras);

        // Sem sentido escolhido, o time mostra tudo que passou pelo vestiário — nas duas direções.
        var tudoDosFeras = await TransferenciasDeTime.RecentesAsync(ctx, 20);
        Assert.Equal(2, tudoDosFeras.Count);
    }

    [Fact]
    public async Task Busca_por_jogador_acha_pelo_nome()
    {
        var ctx = CenarioDeTransferencias();
        var rafael = await ctx.Jogadores.FindAsync(1);
        var bruno = await ctx.Jogadores.FindAsync(2);
        TransferenciasDeTime.Registrar(ctx, rafael!, 10);
        TransferenciasDeTime.Registrar(ctx, bruno!, 20);
        await ctx.SaveChangesAsync();

        // Minúsculas de propósito: a busca do sistema não diferencia caixa.
        var achados = await TransferenciasDeTime.RecentesAsync(ctx, busca: "rafael");

        Assert.Single(achados);
        Assert.Equal(1, achados[0].JogadorId);
    }

    [Fact]
    public async Task Quem_pediu_exclusao_da_conta_nao_aparece_na_busca()
    {
        var ctx = CenarioDeTransferencias();
        var rafael = await ctx.Jogadores.FindAsync(1);
        TransferenciasDeTime.Registrar(ctx, rafael!, 10);
        rafael!.ExcluidoEm = DateTime.Now;
        await ctx.SaveChangesAsync();

        // A busca inteira do sistema respeita isso (BuscaJogador.Filtrar) — reescrever um
        // `Contains` aqui teria sido a primeira régua a esquecer da LGPD.
        Assert.Empty(await TransferenciasDeTime.RecentesAsync(ctx, busca: "rafael"));
    }

    [Fact]
    public void Time_apagado_depois_do_movimento_nao_apaga_a_frase()
    {
        // A FK é SetNull: a linha sobrevive com o lado em branco, e a tela precisa admitir a
        // lacuna em vez de dizer "saiu de (nada)".
        var movimento = new TransferenciaDeTime
        {
            Jogador = new Jogador { Id = 1, Nome = "Rafael Souza", Cpf = "1" },
            TimeAnteriorId = 99,
            TimeAnterior = null,
            TimeNovoId = null,
            Em = DateTime.Now,
        };

        Assert.Equal("Rafael Souza deixou o um time removido", TransferenciasDeTime.Frase(movimento));
    }

    // ── A tela, de ponta a ponta ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_pagina_do_time_monta_elenco_titulos_presidente_e_sedes()
    {
        var ctx = TestInfra.NovoContexto();

        var rafael = new Jogador { Id = 1, Nome = "Rafael Souza", Cpf = "1", TimeId = 10 };
        var bruno = new Jogador { Id = 2, Nome = "Bruno Alves", Cpf = "2", TimeId = 10 };
        ctx.Jogadores.AddRange(rafael, bruno);
        ctx.Times.Add(new Time { Id = 10, Nome = "Nata Padel" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena Beira Rio" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 1, Nome = "3ª Categoria Masculina", Codigo = "3M", Tipo = "Masculina",
        });
        ctx.SaveChanges();

        ctx.JogadorCategorias.Add(new JogadorCategoria { JogadorId = 1, CategoriaPadraoId = 1 });
        ctx.TimeSedes.Add(new TimeSede { TimeId = 10, ClubeId = 1 });
        ctx.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = 10, JogadorId = 1, ConcedidoPorId = 1, ConcedidoEm = new DateTime(2026, 1, 1),
        });

        var categoria = CategoriaDe(ctx, "Finalizado");
        ctx.Duplas.AddRange(
            // Um título DO TIME.
            new Dupla { Categoria = categoria, Jogador1Id = 1, NomeTime = "Nata Padel", TimeId = 10, UltimaFase = CampeoesDoTorneio.FaseDeCampeao },
            // E uma taça pessoal do Bruno.
            new Dupla { Categoria = categoria, Jogador1Id = 2, UltimaFase = CampeoesDoTorneio.FaseDeCampeao });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTimesController(ctx, usuarioLogadoId: 1);
        var resultado = await controller.Detalhes(10) as Microsoft.AspNetCore.Mvc.ViewResult;
        var vm = Assert.IsType<TimeDetalheVM>(resultado!.Model);

        Assert.Equal("Rafael Souza", vm.Presidente!.Jogador.Nome);
        Assert.Single(vm.Titulos);
        Assert.Equal("Arena Beira Rio", Assert.Single(vm.Sedes).Nome);

        // O Rafael marcou a 3ª; o Bruno não marcou nada e cai no balde do fim.
        Assert.Equal(new[] { "3ª Masculina", ElencoPorCategoria.SemCategoria },
            vm.Elenco.Select(g => g.Curto));

        // Quem mais ganhou é o Bruno: a taça do TIME não é troféu de ninguém.
        var destaque = Assert.Single(vm.MaioresVencedores);
        Assert.Equal("Bruno Alves", destaque.Jogador.Nome);
        Assert.Equal(1, destaque.Trofeus);
    }
}
