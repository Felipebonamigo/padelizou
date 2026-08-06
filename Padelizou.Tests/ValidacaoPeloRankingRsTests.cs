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

        public Task<ResultadoDoRanking> ValidarAsync(string nome, int categoriaRsId, CancellationToken ct = default)
            => Task.FromResult(Responder(new AtletaParaValidar(nome, categoriaRsId)));

        public Task<IReadOnlyList<ResultadoDoRanking>> ValidarLoteAsync(
            IReadOnlyList<AtletaParaValidar> atletas, CancellationToken ct = default)
        {
            Perguntas.AddRange(atletas);
            return Task.FromResult<IReadOnlyList<ResultadoDoRanking>>(
                atletas.Select(Responder).ToList());
        }

        // A vitrine do perfil não tem nada a ver com a regra de inscrição — ver
        // PosicaoNoRankingRsTests, que testa esta parte contra um servidor de mentira.
        public Task<IReadOnlyList<PosicaoNoRanking>> PosicoesAsync(string nome, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PosicaoNoRanking>>(Array.Empty<PosicaoNoRanking>());
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
}
