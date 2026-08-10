using padelizou.Models;   // namespace legado (minúsculo) — CategoriaPadrao mora aqui
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Melhorias da área do jogador: pontos reais (no lugar do campo morto), evolução no tempo
// e onboarding de primeiro acesso.
public class JogadorExperienciaTests
{
    // Cria um torneio com data e devolve a categoria, pra pendurar duplas nele.
    //
    // ⚠️ `duplasDeEnchimento` existe por causa do PESO POR TAMANHO (10/08/2026, RANKING.md):
    // o ponto depende de quantas duplas a categoria tem, e 5 é o ponto de calibração (peso
    // 1,0), onde campeão vale 100 e semi vale 35 como sempre valeram. Cada teste enche até 5
    // contando as duplas que ele mesmo vai pendurar — o padrão 4 serve a quem pendura uma.
    // Sem isso, categoria de 1 dupla cai abaixo do piso de 3 e todo mundo leva 10.
    //
    // ⚠️ `Status = "Finalizado"` é obrigatório desde 10/08/2026: ponto só nasce com o torneio
    // COMEÇADO. O `Torneio` nasce com "Inscrições Abertas", e um torneio com campeão e
    // inscrição aberta é um mundo que não existe — sem esta linha, os fixtures descreviam
    // esse mundo e todos os testes de ponto passavam a somar zero.
    private static Categoria NovaCategoria(
        DbPadelContext ctx, string nome, DateTime? data, int duplasDeEnchimento = 4,
        string status = "Finalizado")
    {
        var torneio = new Torneio { Nome = nome, Codigo = nome, DataInicio = data, Status = status };
        var cat = new Categoria { Nome = "2ª Masculina", Codigo = nome + "C", Torneio = torneio };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();

        for (int i = 0; i < duplasDeEnchimento; i++)
        {
            var a = new Jogador { Nome = $"{nome} ench {i}A", Cpf = $"{nome}-ench-{i}A" };
            var b = new Jogador { Nome = $"{nome} ench {i}B", Cpf = $"{nome}-ench-{i}B" };
            ctx.Jogadores.AddRange(a, b);
            ctx.SaveChanges();
            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, UltimaFase = "Grupos",
            });
        }
        ctx.SaveChanges();

        return cat;
    }

    // ---------- Pontos reais ----------

    [Fact]
    public async Task ObterPontosPorJogador_soma_as_fases_e_ignora_quem_nao_foi_pedido()
    {
        using var ctx = TestInfra.NovoContexto();
        var a = new Jogador { Nome = "A", Cpf = "1" };
        var b = new Jogador { Nome = "B", Cpf = "2" };
        var forasteiro = new Jogador { Nome = "C", Cpf = "3" };
        ctx.Jogadores.AddRange(a, b, forasteiro);

        var cat = NovaCategoria(ctx, "T1", new DateTime(2026, 3, 1), duplasDeEnchimento: 3); // + as 2 daqui = 5
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, UltimaFase = "Campeao" });
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = forasteiro.Id, UltimaFase = "Semifinal" });
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);
        var pontos = await svc.ObterPontosPorJogadorAsync(new[] { a.Id, b.Id });

        Assert.Equal(135, pontos[a.Id]);   // 100 (campeão) + 35 (semi)
        Assert.Equal(100, pontos[b.Id]);
        Assert.False(pontos.ContainsKey(forasteiro.Id)); // não foi pedido, não volta
    }

    [Fact]
    public async Task ObterPontosPorJogador_devolve_zero_para_quem_nunca_jogou()
    {
        using var ctx = TestInfra.NovoContexto();
        var novato = new Jogador { Nome = "Novato", Cpf = "1" };
        ctx.Jogadores.Add(novato);
        ctx.SaveChanges();

        var pontos = await new EstatisticasService(ctx).ObterPontosPorJogadorAsync(new[] { novato.Id });

        // Zero explícito, não ausente — quem consome usa direto sem GetValueOrDefault.
        Assert.Equal(0, pontos[novato.Id]);
    }

    // ---------- Evolução ----------

    [Fact]
    public async Task Evolucao_distribui_pontos_no_mes_do_torneio_e_acumula()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "J", Cpf = "1" };
        var parceiro = new Jogador { Nome = "P", Cpf = "2" };
        ctx.Jogadores.AddRange(j, parceiro);

        var hoje = DateTime.Now;
        var mesPassado = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-1);
        var esteMes = new DateTime(hoje.Year, hoje.Month, 1);

        var catAntiga = NovaCategoria(ctx, "TA", mesPassado.AddDays(5));
        var catNova = NovaCategoria(ctx, "TB", esteMes.AddDays(1));
        ctx.Duplas.Add(new Dupla { CategoriaId = catAntiga.Id, Jogador1Id = j.Id, Jogador2Id = parceiro.Id, UltimaFase = "Semifinal" }); // 35
        ctx.Duplas.Add(new Dupla { CategoriaId = catNova.Id, Jogador1Id = j.Id, Jogador2Id = parceiro.Id, UltimaFase = "Campeao" });     // 100
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterEvolucaoJogadorAsync(j.Id, meses: 12);

        Assert.Equal(12, vm.Meses.Count);
        Assert.True(vm.TemDados);

        var doMesPassado = vm.Meses.Single(m => m.Mes == mesPassado);
        var doMesAtual = vm.Meses.Single(m => m.Mes == esteMes);

        Assert.Equal(35, doMesPassado.Pontos);
        Assert.Equal(35, doMesPassado.Acumulado);
        Assert.Equal(100, doMesAtual.Pontos);
        Assert.Equal(135, doMesAtual.Acumulado);  // acumulado nunca "reseta"
        Assert.Equal(1, doMesAtual.Titulos);
        Assert.Equal(135, vm.Total);
        Assert.Equal(100, vm.PontosNoUltimoMes);
    }

    [Fact]
    public async Task Evolucao_conta_o_que_veio_antes_da_janela_como_saldo_inicial()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "Veterano", Cpf = "1" };
        var p = new Jogador { Nome = "P", Cpf = "2" };
        ctx.Jogadores.AddRange(j, p);

        // Torneio de 3 anos atrás: fora da janela de 12 meses, mas os pontos são dele.
        var antigo = NovaCategoria(ctx, "TV", DateTime.Now.AddYears(-3));
        ctx.Duplas.Add(new Dupla { CategoriaId = antigo.Id, Jogador1Id = j.Id, Jogador2Id = p.Id, UltimaFase = "Campeao" });
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterEvolucaoJogadorAsync(j.Id, meses: 12);

        // A linha começa em 100, não em 0 — senão pareceria que ele regrediu.
        Assert.Equal(100, vm.Meses[0].Acumulado);
        Assert.Equal(0, vm.Meses[0].Pontos);
        Assert.False(vm.TemDados); // nada ganho DENTRO da janela: o gráfico não aparece
    }

    [Fact]
    public async Task Torneio_que_ainda_nao_comecou_nao_pontua_em_lugar_nenhum()
    {
        // ⚠️ ESTE TESTE AFIRMAVA O CONTRÁRIO até 10/08/2026, e o contrário era o defeito:
        // "torneio marcado pra daqui a 1 mês: a inscrição já pontua (10 de participação)".
        // O Felipe pediu que o ponto só nascesse com o torneio começado — e o que era um
        // detalhe virou porta escancarada quando a participação passou a multiplicar pelo
        // tamanho: bastaria se inscrever na categoria mais cheia que existisse.
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "J", Cpf = "1" };
        var p = new Jogador { Nome = "P", Cpf = "2" };
        ctx.Jogadores.AddRange(j, p);

        var passado = NovaCategoria(ctx, "TP", DateTime.Now.AddMonths(-2));
        // Torneio de daqui a 1 mês: inscrição aberta, ninguém jogou, ninguém pontua.
        var futuro = NovaCategoria(ctx, "TF", DateTime.Now.AddMonths(1),
            status: PortaDaInscricao.Aberta);
        ctx.Duplas.Add(new Dupla { CategoriaId = passado.Id, Jogador1Id = j.Id, Jogador2Id = p.Id, UltimaFase = "Final" }); // 60
        ctx.Duplas.Add(new Dupla { CategoriaId = futuro.Id, Jogador1Id = j.Id, Jogador2Id = p.Id, UltimaFase = "Grupos" }); // 0
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);
        var vm = await svc.ObterEvolucaoJogadorAsync(j.Id);
        var resumo = await svc.ObterResumoJogadorAsync(j.Id);

        // Só o vice do torneio que aconteceu.
        Assert.Equal(60, resumo.Pontos);

        // O gráfico continua tendo que terminar no MESMO total do perfil — era essa promessa
        // que obrigava a janela a se esticar pro futuro. Agora ela vale sem gambiarra.
        Assert.Equal(resumo.Pontos, vm.Total);
        Assert.Equal(60, vm.Total);

        // E a linha termina no mês corrente, que é onde um gráfico de evolução termina.
        var mesAtual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        Assert.Equal(mesAtual, vm.Meses[^1].Mes);
        Assert.DoesNotContain(vm.Meses, m => m.Mes > mesAtual);
    }

    [Fact]
    public async Task Evolucao_ignora_torneio_sem_data()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "J", Cpf = "1" };
        var p = new Jogador { Nome = "P", Cpf = "2" };
        ctx.Jogadores.AddRange(j, p);

        var semData = NovaCategoria(ctx, "TS", null);
        ctx.Duplas.Add(new Dupla { CategoriaId = semData.Id, Jogador1Id = j.Id, Jogador2Id = p.Id, UltimaFase = "Campeao" });
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterEvolucaoJogadorAsync(j.Id);

        Assert.False(vm.TemDados);       // sem data não dá pra posicionar no tempo
        Assert.Equal(0, vm.Total);

        // ...mas o total do perfil continua contando o torneio.
        var resumo = await new EstatisticasService(ctx).ObterResumoJogadorAsync(j.Id);
        Assert.Equal(100, resumo.Pontos);
    }

    // ---------- Onboarding ----------

    [Fact]
    public async Task Onboarding_de_jogador_novo_comeca_do_zero_e_pede_o_perfil()
    {
        using var ctx = TestInfra.NovoContexto();
        var novato = new Jogador { Nome = "Novato", Cpf = "1" };
        ctx.Jogadores.Add(novato);
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(novato.Id);

        Assert.False(vm.Completo);
        Assert.Equal(0, vm.Concluidos);
        Assert.Equal(0, vm.Percentual);
        Assert.Equal("Complete seu perfil", vm.Proximo!.Titulo);
    }

    [Fact]
    public async Task Onboarding_marca_o_perfil_so_quando_esta_todo_preenchido()
    {
        using var ctx = TestInfra.NovoContexto();
        // Falta o LadoQuadra: perfil ainda não conta como completo.
        var j = new Jogador { Nome = "J", Cpf = "1", FotoPerfil = "/uploads/foto.png", Cidade = "Caxias do Sul" };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);
        Assert.False((await svc.ObterOnboardingAsync(j.Id)).Passos[0].Concluido);

        j.LadoQuadra = "Direita";
        ctx.SaveChanges();

        Assert.True((await svc.ObterOnboardingAsync(j.Id)).Passos[0].Concluido);
    }

    [Fact]
    public async Task Onboarding_some_quando_o_jogador_cumpre_todos_os_passos()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador
        {
            Nome = "Completo", Cpf = "1",
            FotoPerfil = "/uploads/foto.png", Cidade = "Caxias do Sul", LadoQuadra = "Esquerda",
        };
        var outro = new Jogador { Nome = "Outro", Cpf = "2" };
        ctx.Jogadores.AddRange(j, outro);
        ctx.CategoriasPadrao.Add(new CategoriaPadrao { Nome = "2ª Masculina", Codigo = "2M", Tipo = "Masculina" });
        ctx.SaveChanges();

        var catPadrao = ctx.CategoriasPadrao.First();
        ctx.JogadorCategorias.Add(new JogadorCategoria { JogadorId = j.Id, CategoriaPadraoId = catPadrao.Id });
        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = j.Id, SeguidoId = outro.Id });

        var cat = NovaCategoria(ctx, "T1", DateTime.Now);
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = j.Id, Jogador2Id = outro.Id });
        ctx.PushSubscriptionsJogador.Add(new PushSubscriptionJogador
        {
            JogadorId = j.Id, Endpoint = "https://push.exemplo/1", P256dh = "k", Auth = "a",
        });
        ctx.SaveChanges();

        var vm = await new EstatisticasService(ctx).ObterOnboardingAsync(j.Id);

        Assert.True(vm.Completo);
        Assert.Equal(100, vm.Percentual);
        Assert.Null(vm.Proximo);
    }
}
