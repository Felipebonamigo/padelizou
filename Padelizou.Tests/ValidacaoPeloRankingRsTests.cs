using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A regra que consulta o Ranking RS na inscrição.
//
// O teste mais importante deste arquivo NÃO é o que barra o sandbagger — é o que garante que
// ranking fora do ar deixa a inscrição passar. A chave da integração está estrangulada
// enquanto o Ranking RS não libera 100%, então "não deu pra consultar" é o caso COMUM hoje,
// não a exceção: se ele virasse recusa, um torneio inteiro ficaria sem conseguir se inscrever.
public class ValidacaoPeloRankingRsTests
{
    // ── Um ranking de mentira, que responde o que o teste mandar ──────────────────────────
    private class RankingFalso : IRankingRsService
    {
        public bool Configurado { get; set; } = true;
        public List<AtletaParaValidar> Perguntas { get; } = new();
        public Func<AtletaParaValidar, ResultadoDoRanking> Responder { get; set; } = Aprovar;

        // Pro caso em que a resposta do LOTE não bate com a pergunta — a API devolvendo menos
        // linhas do que recebeu. Nulo = responde uma por atleta, que é o normal.
        public Func<IReadOnlyList<AtletaParaValidar>, IReadOnlyList<ResultadoDoRanking>>? RespostaDoLote { get; set; }

        public Task<ResultadoDoRanking> ValidarAsync(string nome, int categoriaRsId, CancellationToken ct = default)
            => Task.FromResult(Responder(new AtletaParaValidar(nome, categoriaRsId)));

        public Task<IReadOnlyList<ResultadoDoRanking>> ValidarLoteAsync(
            IReadOnlyList<AtletaParaValidar> atletas, CancellationToken ct = default)
        {
            Perguntas.AddRange(atletas);
            return Task.FromResult(RespostaDoLote != null
                ? RespostaDoLote(atletas)
                : (IReadOnlyList<ResultadoDoRanking>)atletas.Select(Responder).ToList());
        }
    }

    private static ResultadoDoRanking Aprovar(AtletaParaValidar a) =>
        new(RespostaDoRanking.Aprovado, a.Nome, "6ª Masculina", true,
            Array.Empty<CategoriaBloqueante>(), a.Referencia);

    private static ResultadoDoRanking Reprovar(AtletaParaValidar a) =>
        new(RespostaDoRanking.ForaDeCategoria, a.Nome, "6ª Masculina", true,
            new[] { new CategoriaBloqueante("2ª Masculina", 235, 9) }, a.Referencia);

    private static ResultadoDoRanking NaoConsultar(AtletaParaValidar a) =>
        new(RespostaDoRanking.NaoConsultado, a.Nome, null, false,
            Array.Empty<CategoriaBloqueante>(), a.Referencia);

    // ── Cenário ───────────────────────────────────────────────────────────────────────────
    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria) Montar(
        bool validacaoLigada = true, int? categoriaRs = 112)
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "Interno", Codigo = "INT1", Status = "Inscrições Abertas",
            ValidarPeloRankingRs = validacaoLigada,
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria
        {
            Nome = "6ª Categoria", Codigo = "C6", Torneio = torneio,
            RankingRsCategoriaId = categoriaRs,
        };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        return (ctx, torneio, categoria);
    }

    private static ValidacaoPeloRankingRs Servico(DbPadelContext ctx, RankingFalso ranking) =>
        new(ctx, ranking, NullLogger<ValidacaoPeloRankingRs>.Instance);

    private static readonly ValidacaoPeloRankingRs.Pessoa Silvano = new("Silvano Hernandorena", "11144477735");

    // ── O QUE MAIS IMPORTA: falha nunca vira recusa ───────────────────────────────────────

    [Fact]
    public async Task Ranking_fora_do_ar_NAO_recusa_a_inscricao()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = NaoConsultar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        // E não deixa lixo pra trás: ninguém foi barrado, então não há o que o organizador decidir.
        Assert.Empty(ctx.BloqueiosDoRanking);
    }

    [Fact]
    public async Task Sem_chave_configurada_nem_pergunta()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Configurado = false, Responder = Reprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        Assert.Empty(ranking.Perguntas);
    }

    [Fact]
    public async Task Torneio_com_a_validacao_desligada_nem_pergunta()
    {
        var (ctx, torneio, categoria) = Montar(validacaoLigada: false);
        var ranking = new RankingFalso { Responder = Reprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        Assert.Empty(ranking.Perguntas);
    }

    // Categoria que não casa com o ranking (o caso de "6ª Categoria", que não diz o sexo).
    [Fact]
    public async Task Categoria_sem_par_no_ranking_nem_pergunta()
    {
        var (ctx, torneio, categoria) = Montar(categoriaRs: null);
        var ranking = new RankingFalso { Responder = Reprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        Assert.Empty(ranking.Perguntas);
    }

    // ── Barrando de verdade ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reprovado_recusa_dizendo_o_motivo_e_que_o_organizador_vai_avaliar()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.NotNull(recusa);
        Assert.Contains("Silvano Hernandorena", recusa);
        Assert.Contains("2ª Masculina", recusa);
        Assert.Contains("235", recusa);                       // os pontos, pra recusa não ser vaga
        Assert.Contains("organizador", recusa);               // e que ainda dá pra reverter
    }

    [Fact]
    public async Task Reprovado_vira_linha_pendente_pro_organizador_decidir()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        var bloqueio = Assert.Single(ctx.BloqueiosDoRanking);
        Assert.Equal(SituacaoDoBloqueio.Pendente, bloqueio.Situacao);
        Assert.Equal(Silvano.Cpf, bloqueio.Cpf);
        Assert.Equal(Silvano.Nome, bloqueio.NomeConsultado);
        Assert.Equal(112, bloqueio.CategoriaRsId);
        Assert.Equal("6ª Masculina", bloqueio.CategoriaRsNome);
        Assert.Equal(categoria.Id, bloqueio.CategoriaId);
        Assert.Equal(torneio.Id, bloqueio.TorneioId);
    }

    // A LIÇÃO DOS ELOGIOS: tentar de novo não pode empilhar linha. Se empilhasse, a fila do
    // organizador encheria de repetições da mesma pessoa e ele decidiria a mesma coisa cinco
    // vezes — foi exatamente assim que a tabela de Elogios cresceu debaixo da consulta em prod.
    [Fact]
    public async Task Tentar_de_novo_atualiza_a_mesma_linha_em_vez_de_empilhar()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };
        var servico = Servico(ctx, ranking);

        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });
        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });
        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Single(ctx.BloqueiosDoRanking);
    }

    // ── A decisão do organizador ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_o_organizador_LIBEROU_passa_sem_nem_ser_consultado()
    {
        var (ctx, torneio, categoria) = Montar();
        ctx.BloqueiosDoRanking.Add(new BloqueioDoRanking
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Cpf = Silvano.Cpf,
            NomeConsultado = Silvano.Nome, CategoriaRsId = 112, Motivo = "qualquer",
            Situacao = SituacaoDoBloqueio.Liberado,
        });
        ctx.SaveChanges();

        var ranking = new RankingFalso { Responder = Reprovar };
        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        // Nem gastou consulta: a decisão do organizador vale mais que a resposta da API, e a
        // chave tem cota.
        Assert.Empty(ranking.Perguntas);
    }

    // Liberação vale pra UMA categoria. Liberar alguém na 6ª não pode soltá-lo na 4ª também.
    [Fact]
    public async Task Liberacao_vale_so_na_categoria_em_que_foi_dada()
    {
        var (ctx, torneio, categoria) = Montar();
        var outra = new Categoria
        {
            Nome = "4ª Masculina", Codigo = "C4", TorneioId = torneio.Id, RankingRsCategoriaId = 108,
        };
        ctx.Categorias.Add(outra);
        ctx.BloqueiosDoRanking.Add(new BloqueioDoRanking
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Cpf = Silvano.Cpf,
            NomeConsultado = Silvano.Nome, CategoriaRsId = 112, Motivo = "qualquer",
            Situacao = SituacaoDoBloqueio.Liberado,
        });
        ctx.SaveChanges();

        var ranking = new RankingFalso { Responder = Reprovar };
        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, outra, new[] { Silvano });

        Assert.NotNull(recusa);
    }

    // Depois de o organizador bater o martelo em "Mantido", uma nova tentativa do jogador não
    // pode devolver o caso pra fila como se fosse novidade — ele já decidiu.
    [Fact]
    public async Task Tentativa_nova_nao_devolve_pra_fila_o_que_o_organizador_ja_manteve()
    {
        var (ctx, torneio, categoria) = Montar();
        ctx.BloqueiosDoRanking.Add(new BloqueioDoRanking
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Cpf = Silvano.Cpf,
            NomeConsultado = Silvano.Nome, CategoriaRsId = 112, Motivo = "motivo antigo",
            Situacao = SituacaoDoBloqueio.Mantido,
        });
        ctx.SaveChanges();

        var ranking = new RankingFalso { Responder = Reprovar };
        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.NotNull(recusa);
        var bloqueio = Assert.Single(ctx.BloqueiosDoRanking);
        Assert.Equal(SituacaoDoBloqueio.Mantido, bloqueio.Situacao);
        // O motivo, esse sim, é atualizado: o ranking muda com o tempo.
        Assert.Contains("235", bloqueio.Motivo);
    }

    // ── Detalhes que economizam consulta (a chave tem cota) ───────────────────────────────

    [Fact]
    public async Task Os_dois_da_dupla_vao_numa_consulta_so()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Aprovar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria,
            new[] { Silvano, new ValidacaoPeloRankingRs.Pessoa("Outro Jogador", "52998224725") });

        Assert.Equal(2, ranking.Perguntas.Count);
        // E cada um vai identificado pelo CPF, não pelo nome: dois homônimos no mesmo torneio
        // embaralhariam as respostas.
        Assert.Contains(ranking.Perguntas, p => p.Referencia == Silvano.Cpf);
        Assert.Contains(ranking.Perguntas, p => p.Referencia == "52998224725");
    }

    [Fact]
    public async Task A_mesma_pessoa_repetida_e_perguntada_uma_vez_so()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Aprovar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano, Silvano });

        Assert.Single(ranking.Perguntas);
    }

    [Fact]
    public async Task Aprovado_passa_sem_deixar_bloqueio()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Aprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        Assert.Empty(ctx.BloqueiosDoRanking);
    }

    // Um reprovado no meio de aprovados barra a dupla, e só ele vira linha.
    [Fact]
    public async Task So_o_reprovado_vira_bloqueio()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso
        {
            Responder = a => a.Referencia == Silvano.Cpf ? Reprovar(a) : Aprovar(a),
        };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria,
            new[] { Silvano, new ValidacaoPeloRankingRs.Pessoa("Parceiro Limpo", "52998224725") });

        Assert.NotNull(recusa);
        Assert.Contains("Silvano", recusa);
        Assert.DoesNotContain("Parceiro Limpo", recusa);
        var bloqueio = Assert.Single(ctx.BloqueiosDoRanking);
        Assert.Equal(Silvano.Cpf, bloqueio.Cpf);
    }

    [Fact]
    public async Task Pessoa_sem_cpf_ou_sem_nome_nao_vai_pra_consulta()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria,
            new[] { new ValidacaoPeloRankingRs.Pessoa("", ""), new ValidacaoPeloRankingRs.Pessoa("Sem Cpf", "") });

        Assert.Null(recusa);
        Assert.Empty(ranking.Perguntas);
    }

    // ── O REGISTRO DA CONSULTA ────────────────────────────────────────────────────────────
    //
    // Até 10/08/2026 só a RECUSA virava linha. Quem era consultado e passava não deixava
    // rastro nenhum — e sem rastro, "passou pelo filtro" só podia ser calculado por subtração,
    // que conta junto quem nunca foi perguntado. Ver Models/ConsultaAoRankingRs.
    //
    // ⚠️ Estes testes protegem uma coisa que não se vê: cada caminho de saída do
    // MotivoDeRecusaAsync tem que deixar sua marca. O caminho que ESQUECER de anotar não
    // quebra nada — só some silenciosamente do relatório do parceiro.

    [Fact]
    public async Task Aprovado_pelo_ranking_vira_registro_de_consulta()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Aprovar };

        var recusa = await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        var consulta = Assert.Single(ctx.ConsultasAoRankingRs);
        Assert.Equal(ResultadoDaConsulta.Aprovado, consulta.Resultado);
        Assert.Equal(Silvano.Cpf, consulta.Cpf);
        Assert.Equal(112, consulta.CategoriaRsId);
        Assert.True(RelatorioDoRankingRs.PassouPeloFiltro(consulta));
    }

    [Fact]
    public async Task Barrado_pelo_ranking_vira_registro_E_bloqueio()
    {
        // Os dois, e cada um pro seu uso: o bloqueio é a fila do organizador, a consulta é a
        // contagem da parceria. Guardar só um dos dois deixaria uma das duas telas mentindo.
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Equal(ResultadoDaConsulta.Barrado, Assert.Single(ctx.ConsultasAoRankingRs).Resultado);
        Assert.Single(ctx.BloqueiosDoRanking);
    }

    [Fact]
    public async Task Categoria_sem_par_no_ranking_anota_que_NINGUEM_foi_perguntado()
    {
        // ⚠️ É o caso mais comum de todos, e o mais fácil de ler errado: a inscrição passa
        // igualzinho a de quem foi aprovado. Sem esta linha, os dois ficam idênticos no banco.
        var (ctx, torneio, categoria) = Montar(categoriaRs: null);
        var ranking = new RankingFalso { Responder = Reprovar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        var consulta = Assert.Single(ctx.ConsultasAoRankingRs);
        Assert.Equal(ResultadoDaConsulta.SemDePara, consulta.Resultado);
        Assert.Null(consulta.CategoriaRsId);
        Assert.False(RelatorioDoRankingRs.PassouPeloFiltro(consulta));
        Assert.Empty(ranking.Perguntas);
    }

    [Fact]
    public async Task Ranking_fora_do_ar_anota_que_nao_deu_pra_consultar()
    {
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = NaoConsultar };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Equal(ResultadoDaConsulta.SemResposta, Assert.Single(ctx.ConsultasAoRankingRs).Resultado);
    }

    [Fact]
    public async Task Integracao_sem_chave_anota_que_o_filtro_nao_rodou()
    {
        // ⚠️ O cenário que assusta: a chave sair do ar não muda NADA que se veja — as
        // inscrições passam todas e o site não reclama. Esta linha é a única testemunha.
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Configurado = false };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Equal(ResultadoDaConsulta.SemResposta, Assert.Single(ctx.ConsultasAoRankingRs).Resultado);
    }

    [Fact]
    public async Task Torneio_que_NAO_aderiu_nao_gera_registro_nenhum()
    {
        // A tabela é da parceria. Anotar "não consultei" pra todo inscrito do site inteiro
        // encheria o banco de linha que não responde pergunta nenhuma.
        var (ctx, torneio, categoria) = Montar(validacaoLigada: false);

        await Servico(ctx, new RankingFalso()).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Empty(ctx.ConsultasAoRankingRs);
    }

    [Fact]
    public async Task Tentar_de_novo_ATUALIZA_a_mesma_linha_em_vez_de_empilhar()
    {
        // Quem insiste no formulário é uma pessoa consultada, não cinco — senão o relatório do
        // parceiro contaria a teimosia dela como volume da parceria.
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = NaoConsultar };
        var servico = Servico(ctx, ranking);

        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });
        ranking.Responder = Aprovar;
        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        var consulta = Assert.Single(ctx.ConsultasAoRankingRs);
        Assert.Equal(ResultadoDaConsulta.Aprovado, consulta.Resultado);
    }

    [Fact]
    public async Task Liberado_pelo_organizador_NAO_e_reescrito_como_aprovado()
    {
        // ⚠️ O ranking barrou mesmo; quem passou por cima foi o organizador. Reescrever a
        // linha pra "Aprovado" apagaria do relatório exatamente o caso em que a nossa decisão
        // discordou da deles — que é o que eles mais têm interesse em ver.
        var (ctx, torneio, categoria) = Montar();
        var ranking = new RankingFalso { Responder = Reprovar };
        var servico = Servico(ctx, ranking);

        await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        var bloqueio = ctx.BloqueiosDoRanking.Single();
        bloqueio.Situacao = SituacaoDoBloqueio.Liberado;
        await ctx.SaveChangesAsync();

        // A pessoa volta e se inscreve — agora ela passa, mas pela decisão do organizador.
        var recusa = await servico.MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano });

        Assert.Null(recusa);
        Assert.Equal(ResultadoDaConsulta.Barrado, Assert.Single(ctx.ConsultasAoRankingRs).Resultado);
    }

    [Fact]
    public async Task Cada_pessoa_do_lote_ganha_a_sua_linha()
    {
        var (ctx, torneio, categoria) = Montar();
        var limpo = new ValidacaoPeloRankingRs.Pessoa("Parceiro Limpo", "52998224725");
        var ranking = new RankingFalso
        {
            Responder = a => a.Referencia == Silvano.Cpf ? Reprovar(a) : Aprovar(a),
        };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano, limpo });

        Assert.Equal(2, ctx.ConsultasAoRankingRs.Count());
        Assert.Equal(ResultadoDaConsulta.Barrado,
            ctx.ConsultasAoRankingRs.Single(c => c.Cpf == Silvano.Cpf).Resultado);
        Assert.Equal(ResultadoDaConsulta.Aprovado,
            ctx.ConsultasAoRankingRs.Single(c => c.Cpf == limpo.Cpf).Resultado);
    }

    [Fact]
    public async Task Lote_devolvido_incompleto_nao_some_com_ninguem()
    {
        // Se a API deixar alguém de fora da resposta, essa pessoa não pode desaparecer do
        // relatório como se nunca tivesse tentado se inscrever.
        var (ctx, torneio, categoria) = Montar();
        var esquecido = new ValidacaoPeloRankingRs.Pessoa("Esquecido", "52998224725");
        var ranking = new RankingFalso
        {
            // Devolve só o primeiro do lote.
            RespostaDoLote = atletas => atletas.Take(1).Select(Aprovar).ToList(),
        };

        await Servico(ctx, ranking).MotivoDeRecusaAsync(torneio, categoria, new[] { Silvano, esquecido });

        Assert.Equal(2, ctx.ConsultasAoRankingRs.Count());
        Assert.Equal(ResultadoDaConsulta.SemResposta,
            ctx.ConsultasAoRankingRs.Single(c => c.Cpf == esquecido.Cpf).Resultado);
    }
}
