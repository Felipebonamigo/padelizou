using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// MUDAR O PREÇO NA GESTÃO ATUALIZA QUEM AINDA NÃO PAGOU (18/11/2026).
//
// 🗣️ Rafael (3ª ETAPA WALLAU), pelo Felipe: a cobrança pelo WhatsApp saiu com R$ 110,00 por
// pessoa num torneio de R$ 140,00 — *"alterei lá e não mudou nada"*. Cada inscrição guardava o
// valor do dia em que foi feita (Dupla.ValorInscricao), e a cobrança por jogador reparte a
// diferença: 140 + (220 − 280) / 2 = 110.
//
// Decisão do Felipe: "Atualizar sozinho ao salvar" — quem NÃO pagou nada passa pro preço novo;
// quem pagou (inteiro ou uma parte) fica com o combinado.
public class ReajusteDoPrecoDasInscricoesTests
{
    private static Task<IActionResult> Editar(
        Padelizou.Controllers.TorneiosController controlador, int torneioId, decimal preco,
        decimal? segundo = null, decimal? taxa = null) =>
        controlador.Editar(
            torneioId, "Torneio de Teste", null, null, precoInscricao: preco, clubeId: 0,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            precoSegundaInscricao: segundo, taxaPorImpedimento: taxa);

    private static (Torneio torneio, Categoria categoria, Jogador organizador, Dupla dupla) Montar(
        DbPadelContext ctx, decimal precoAntigo = 110m, decimal valorDaDupla = 220m)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = precoAntigo;
        var dupla = ctx.Duplas.Single();
        dupla.ValorInscricao = valorDaDupla;
        ctx.SaveChanges();
        return (torneio, categoria, organizador, dupla);
    }

    [Fact]
    public async Task Subir_o_preco_atualiza_a_dupla_que_nao_pagou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx);

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(280m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    // O caso da Wallau: o preço JÁ está em 140 (o Rafael trocou antes do conserto), e a dupla
    // continua com 220. Salvar de novo, sem mexer no número, tem que alinhar.
    [Fact]
    public async Task Salvar_de_novo_com_o_mesmo_preco_alinha_quem_ficou_pra_tras()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx, precoAntigo: 140m, valorDaDupla: 220m);

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        var d = ctx.Duplas.Find(dupla.Id)!;
        Assert.Equal(280m, d.ValorInscricao);
        var partes = PagamentoPorJogador.Partes(ctx.Torneios.Find(torneio.Id)!, d, false, false);
        Assert.Equal(140m, partes.Jogador1);
        Assert.Equal(140m, partes.Jogador2);
    }

    [Fact]
    public async Task Dupla_paga_fica_com_o_que_combinou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx);
        dupla.Pago = true;
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(220m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    // Um dos dois já mandou o Pix e o organizador marcou: a dupla pagou uma parte do que
    // combinou. Reescrever o total agora cobraria a diferença de quem já acertou a metade dele.
    [Fact]
    public async Task Dupla_com_um_jogador_ja_pago_fica_com_o_que_combinou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx);
        ctx.PagamentosManuaisDeJogador.Add(new PagamentoManualDoJogador { DuplaId = dupla.Id, JogadorId = dupla.Jogador1Id });
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(220m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    // Cobrança do site já emitida com o valor antigo: mexer no total faria a tela pedir um
    // número e o Pix aberto cobrar outro.
    [Fact]
    public async Task Dupla_com_cobranca_do_site_aberta_fica_com_o_que_combinou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx);
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioPagarDepois", ReferenciaId = dupla.Id, TorneioId = torneio.Id,
            JogadorId = dupla.Jogador1Id, Valor = 220m, Status = "Pendente",
        });
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(220m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    // A SEGUNDA INSCRIÇÃO continua com o preço dela: quem está em duas categorias paga o
    // cheio na que fez primeiro e o da segunda na outra — a mesma régua da inscrição.
    [Fact]
    public async Task Segunda_inscricao_do_mesmo_jogador_usa_o_segundo_preco()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, primeira) = Montar(ctx);
        torneio.PermiteMultiplasCategorias = true;
        var outraCategoria = new Categoria { Nome = "3ª", Codigo = "CAT3M", Torneio = torneio };
        var parceiroNovo = new Jogador { Nome = "Parceiro Novo", Cpf = "52998224725" };
        ctx.AddRange(outraCategoria, parceiroNovo);
        ctx.SaveChanges();
        var segunda = new Dupla
        {
            CategoriaId = outraCategoria.Id, Jogador1Id = primeira.Jogador1Id, Jogador2Id = parceiroNovo.Id,
            ValorInscricao = 200m,
        };
        ctx.Duplas.Add(segunda);
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m, segundo: 100m);

        Assert.Equal(280m, ctx.Duplas.Find(primeira.Id)!.ValorInscricao);
        Assert.Equal(100m + 140m, ctx.Duplas.Find(segunda.Id)!.ValorInscricao);
    }

    [Fact]
    public async Task Impedimento_marcado_continua_somando_a_taxa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx, valorDaDupla: 240m);
        torneio.TaxaPorImpedimento = 20m;
        dupla.ImpedimentoSextaNoite = true;
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m, taxa: 20m);

        Assert.Equal(300m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    // Time é cadastrado pelo organizador e não paga inscrição pelo sistema.
    [Fact]
    public async Task Time_nao_e_mexido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, dupla) = Montar(ctx);
        dupla.NomeTime = "Time A";
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(220m, ctx.Duplas.Find(dupla.Id)!.ValorInscricao);
    }

    [Fact]
    public async Task Inscricao_individual_nao_paga_tambem_atualiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, dupla) = Montar(ctx);
        ctx.Duplas.Remove(dupla);
        var avulso = new Jogador { Nome = "Avulso", Cpf = "52998224725" };
        ctx.Jogadores.Add(avulso);
        ctx.SaveChanges();
        var inscricao = new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = avulso.Id, ValorInscricao = 110m };
        var paga = new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = organizador.Id, ValorInscricao = 110m, Pago = true };
        ctx.InscricoesAmericanas.AddRange(inscricao, paga);
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, 140m);

        Assert.Equal(140m, ctx.InscricoesAmericanas.Find(inscricao.Id)!.ValorInscricao);
        Assert.Equal(110m, ctx.InscricoesAmericanas.Find(paga.Id)!.ValorInscricao);
    }

    [Fact]
    public async Task A_mensagem_diz_quantas_inscricoes_mudaram()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _) = Montar(ctx);
        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await Editar(controlador, torneio.Id, 140m);

        Assert.Contains("1 inscrição que ainda não foi paga passou pro valor novo",
            controlador.TempData["Sucesso"] as string);
    }

    // O aviso da tela de gestão dizia o contrário ("quem já entrou continua com o valor que
    // combinou"). Agora diz a regra nova.
    [Fact]
    public void O_aviso_da_gestao_diz_que_quem_nao_pagou_muda_e_quem_pagou_fica()
    {
        var texto = AvisoDeMudancaDePreco.Texto(3)!;

        Assert.Contains("3 inscrições já foram feitas", texto);
        Assert.Contains("quem ainda não pagou passa pro valor novo", texto);
        Assert.Contains("quem já pagou", texto);
    }
}

// O PREÇO DA SEGUNDA INSCRIÇÃO NA TELA DO JOGADOR (18/11/2026).
// 🗣️ Rafael: *"acho que não mostra o preço da segunda em nenhum lugar, porque todos me
// perguntaram"*. Decisão do Felipe: mostrar, quando configurado.
public class FraseDoSegundoPrecoTests
{
    private static Torneio Torneio(decimal preco, decimal? segundo, bool multiplas = true) => new()
    {
        Nome = "T", PrecoInscricao = preco, PrecoSegundaInscricao = segundo, PermiteMultiplasCategorias = multiplas,
    };

    [Fact]
    public void Com_segundo_preco_diz_o_valor_por_pessoa()
    {
        Assert.Equal("Segunda categoria: R$ 100,00 por pessoa",
            PrecoDaInscricao.FraseDaSegunda(Torneio(140m, 100m)));
    }

    [Fact]
    public void Segunda_de_graca_diz_que_e_gratis()
    {
        Assert.Equal("Segunda categoria: grátis", PrecoDaInscricao.FraseDaSegunda(Torneio(140m, 0m)));
    }

    [Fact]
    public void Sem_segundo_preco_ou_igual_ou_sem_multiplas_nao_diz_nada()
    {
        Assert.Null(PrecoDaInscricao.FraseDaSegunda(Torneio(140m, null)));
        Assert.Null(PrecoDaInscricao.FraseDaSegunda(Torneio(140m, 140m)));
        Assert.Null(PrecoDaInscricao.FraseDaSegunda(Torneio(140m, 100m, multiplas: false)));
        Assert.Null(PrecoDaInscricao.FraseDaSegunda(Torneio(0m, 0m)));
    }
}

public class FraseDoSegundoPrecoComTaxaTests
{
    // Torneio que cobra pelo site mostra no topo o preço com a taxa; a segunda vem igual.
    [Fact]
    public void Com_valor_da_tela_usa_o_valor_da_tela()
    {
        var torneio = new Torneio { Nome = "T", PrecoInscricao = 140m, PrecoSegundaInscricao = 100m, PermiteMultiplasCategorias = true };
        Assert.Equal("Segunda categoria: R$ 104,50 por pessoa", PrecoDaInscricao.FraseDaSegunda(torneio, 104.5m));
    }
}

// As consultas do reajuste viram SQL de verdade (o InMemory não confere).
public class TraducaoDoReajusteDoPrecoTests
{
    [Fact]
    public void Cobrancas_do_site_das_duplas_viram_SQL()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x").Options;
        using var ctx = new DbPadelContext(options);
        var ids = new List<int> { 1, 2 };

        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(ctx.Pagamentos
            .Where(p => p.ReferenciaId != null && ids.Contains(p.ReferenciaId.Value)
                && (p.Tipo == "TorneioDupla" || p.Tipo == "TorneioPagarDepois")
                && (p.Status == "Confirmado" || p.Status == "Pendente" || p.Status == "AguardandoEstorno"))
            .Select(p => p.ReferenciaId!.Value));
        var sqlDuplas = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            ctx.Duplas.Where(d => d.Categoria.TorneioId == 7 && d.NomeTime == null).OrderBy(d => d.Id));

        Assert.Contains("SELECT", sql);
        Assert.Contains("SELECT", sqlDuplas);
    }
}
