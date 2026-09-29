using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Controllers;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTO O PATROCÍNIO RENDE EM CLIQUE (Felipe, 29/09/2026): *"no painel admin, adicione la em
// alguma parte, para ter a contagem das vezes que as pessoas clicaram no link dos patrocinadores
// (paralelo, grand padel)"*.
//
// ⚠️ O CLIQUE NUNCA TOCAVA NOSSO SERVIDOR: o logo aponta direto pra `paraleloesporte.com.br`.
// Não havia número escondido pra mostrar — era preciso um salto nosso pra existir contagem.
//
// ⚠️ E O `AcessoAoSite` NÃO SERVIA, mesmo sendo o contador que já existe: ele guarda SÓ o
// timestamp, de propósito ("sem identidade nenhuma"), sem caminho. Contar clique por lá exigiria
// gravar a URL de cada visita — que é exatamente o que aquela tabela se recusa a fazer.
public class CliquesNosPatrocinadoresTests
{
    private static PatrocinadoresSettings Cfg => new();   // a lista de código: Paralelo e Grand Padel

    private static PatrocinadoresController Controller(DbPadelContext ctx) =>
        new(ctx, Options.Create(Cfg));

    // ── A trava que mais importa ──────────────────────────────────────────────────────────

    [Fact]
    public async Task O_destino_sai_da_NOSSA_lista_e_nunca_do_pedido()
    {
        using var ctx = TestInfra.NovoContexto();

        // ⚠️ OPEN REDIRECT É O RISCO REAL DESTE BLOCO. Um endpoint que aceitasse `?url=` daria a
        // qualquer um um link de phishing saindo de `padelizou.com.br` — o domínio que as
        // pessoas reconhecem e no qual elas confiam. Por isso o parâmetro é o NOME, e a URL sai
        // de PatrocinadoresSettings, no servidor.
        //
        // Este teste existe pra quebrar no dia em que alguém "facilitar" passando a URL adiante.
        foreach (var tentativa in new[] { "https://phishing.example/x", "//phishing.example",
                                          "Paralelo/../../etc", "naoexiste" })
        {
            var resultado = await Controller(ctx).Ir(tentativa);
            Assert.IsNotType<RedirectResult>(resultado);
        }

        // E nada foi contado por tentativa inválida.
        Assert.Empty(await ctx.CliquesNoPatrocinador.ToListAsync());
    }

    // ── O caminho feliz ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_clique_leva_ao_site_do_patrocinador_e_fica_contado()
    {
        using var ctx = TestInfra.NovoContexto();

        var resultado = await Controller(ctx).Ir("Paralelo");

        var destino = Assert.IsType<RedirectResult>(resultado);
        Assert.Equal("https://www.paraleloesporte.com.br/", destino.Url);

        var clique = Assert.Single(await ctx.CliquesNoPatrocinador.ToListAsync());
        Assert.Equal("Paralelo", clique.Patrocinador);
    }

    [Fact]
    public async Task O_nome_no_link_nao_depende_de_acento_nem_de_caixa()
    {
        using var ctx = TestInfra.NovoContexto();

        // O nome vira parte da URL, e URL digitada à mão (ou copiada de um e-mail) chega torta.
        // Recusar por causa da caixa transformaria um patrocinador em link quebrado.
        var resultado = await Controller(ctx).Ir("grand padel");

        var destino = Assert.IsType<RedirectResult>(resultado);
        Assert.Equal("https://www.grandpadel.com.br/", destino.Url);
    }

    [Fact]
    public async Task O_registro_NAO_guarda_quem_clicou()
    {
        using var ctx = TestInfra.NovoContexto();
        await Controller(ctx).Ir("Paralelo");

        // ⚠️ MESMA RÉGUA DO `AcessoAoSite`, e ela é escrita lá: *"de propósito, SEM identidade
        // nenhuma: nem JogadorId, nem IP, nem sessão"*. A pergunta do Felipe é de VOLUME
        // ("quantas vezes clicaram"), e volume sai inteiro de um carimbo de tempo. Guardar quem
        // seria compilar dado pessoal que ninguém pediu.
        var colunas = typeof(CliqueNoPatrocinador).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("JogadorId", colunas);
        Assert.DoesNotContain("Ip", colunas);
        Assert.DoesNotContain("SessaoId", colunas);
    }

    // ── A contagem ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_contagem_separa_por_patrocinador_e_por_periodo()
    {
        using var ctx = TestInfra.NovoContexto();
        var agora = new DateTime(2026, 09, 29, 12, 0, 0);

        ctx.CliquesNoPatrocinador.AddRange(
            new CliqueNoPatrocinador { Patrocinador = "Paralelo", Quando = agora.AddDays(-1) },
            new CliqueNoPatrocinador { Patrocinador = "Paralelo", Quando = agora.AddDays(-3) },
            new CliqueNoPatrocinador { Patrocinador = "Paralelo", Quando = agora.AddDays(-40) },
            new CliqueNoPatrocinador { Patrocinador = "Grand Padel", Quando = agora.AddDays(-2) });
        await ctx.SaveChangesAsync();

        var contagem = await CliquesDoPatrocinio.ContarAsync(ctx, agora);

        // ⚠️ TOTAL E MÊS, e não só o total: é a diferença entre "o patrocínio funciona" e "o
        // patrocínio funcionava". Guardar uma linha por clique (e não um contador) é o que
        // torna as duas perguntas possíveis sem corrida de leitura-e-escrita.
        var paralelo = contagem.Single(c => c.Patrocinador == "Paralelo");
        Assert.Equal(3, paralelo.Total);
        Assert.Equal(2, paralelo.NoMes);

        var grand = contagem.Single(c => c.Patrocinador == "Grand Padel");
        Assert.Equal(1, grand.Total);
    }

    [Fact]
    public async Task Patrocinador_em_cartaz_sem_clique_aparece_com_zero()
    {
        using var ctx = TestInfra.NovoContexto();

        // ⚠️ Zero É RESPOSTA, e some se a lista sair do banco. "O Grand Padel não aparece" e "o
        // Grand Padel teve zero clique" são conclusões opostas, e a tela não pode confundi-las.
        var contagem = await CliquesDoPatrocinio.ContarAsync(ctx, DateTime.Now, Cfg);

        Assert.Equal(2, contagem.Count);
        Assert.All(contagem, c => Assert.Equal(0, c.Total));
    }

    [Fact]
    public void A_tela_de_metricas_mostra_os_cliques()
    {
        var tela = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Admin", "Metricas.cshtml"));

        Assert.Contains("CliquesNoPatrocinio", tela);
    }

    [Fact]
    public void O_rodape_manda_o_clique_pelo_nosso_salto()
    {
        var layout = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Shared", "_Layout.cshtml"));

        // ⚠️ E o `rel="sponsored"` CONTINUA: é o rótulo que o Google exige em link pago, e
        // perdê-lo no meio da mudança transformaria patrocínio em compra de link.
        Assert.Contains("Patrocinadores", layout);
        Assert.Contains("rel=\"sponsored noopener\"", layout);
        Assert.DoesNotContain("href=\"@patrocinador.Link\"", layout);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
