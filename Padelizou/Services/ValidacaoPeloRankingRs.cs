using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A regra de inscrição que consulta o Ranking RS.
//
// Mora num serviço, e não dentro do controller, porque DUAS telas inscrevem gente na mesma
// categoria — o formulário de inscrição (DuplasController.Create) e a troca de parceiro
// (DuplasController.TrocarParceiro). Regra de torneio escrita em dois lugares é a origem
// documentada dos piores defeitos deste projeto: uma cópia é corrigida, a outra não, e o
// sistema passa a responder coisas diferentes conforme a tela por onde a pessoa entrou.
//
// TRÊS COISAS QUE ESTE ARQUIVO NUNCA FAZ:
//  1. Recusar quando não deu pra consultar. Ranking fora do ar, chave estrangulada, categoria
//     que não casa — tudo isso deixa a inscrição passar. O contrário transformaria uma queda
//     na casa dos outros em "ninguém consegue se inscrever" num sábado de manhã.
//  2. Dar a palavra final. A recusa vira LINHA em BloqueioDoRanking, e quem decide se ela fica
//     de pé é o organizador.
//  3. Consultar sem o organizador ter pedido. `Torneio.ValidarPeloRankingRs` nasce desligado.
public class ValidacaoPeloRankingRs
{
    private readonly DbPadelContext _context;
    private readonly IRankingRsService _ranking;
    private readonly ILogger<ValidacaoPeloRankingRs> _logger;

    public ValidacaoPeloRankingRs(DbPadelContext context, IRankingRsService ranking,
        ILogger<ValidacaoPeloRankingRs> logger)
    {
        _context = context;
        _ranking = ranking;
        _logger = logger;
    }

    public record Pessoa(string Nome, string Cpf);

    // null = pode se inscrever. Texto = a recusa que o jogador lê.
    //
    // ⚠️ ELE TAMBÉM ANOTA O QUE ACONTECEU, sempre que o torneio aderiu à validação — inclusive
    // quando a pergunta nem chegou a ser feita. Ver Models/ConsultaAoRankingRs: sem esse
    // registro, "aprovado pelo ranking" e "ninguém perguntou nada" ficam idênticos no banco, e
    // é justamente essa diferença que o relatório da parceria existe pra mostrar.
    //
    // O que a anotação NUNCA faz é mudar a resposta: cada caminho abaixo devolve exatamente o
    // que devolvia antes dela existir.
    public async Task<string?> MotivoDeRecusaAsync(Torneio torneio, Categoria categoria,
        IReadOnlyList<Pessoa> pessoas, CancellationToken ct = default)
    {
        // Torneio que não aderiu não gera linha nenhuma: aqui não há parceria pra relatar, e
        // anotar "não consultei" pra todo inscrito do site inteiro encheria a tabela de nada.
        if (!torneio.ValidarPeloRankingRs) return null;

        var candidatos = pessoas
            .Where(p => !string.IsNullOrWhiteSpace(p.Cpf) && !string.IsNullOrWhiteSpace(p.Nome))
            .GroupBy(p => p.Cpf).Select(g => g.First())  // a mesma pessoa não é perguntada duas vezes
            .ToList();
        if (candidatos.Count == 0) return null;

        // Integração sem chave. Antes disto ser anotado, o efeito prático era assustador: a
        // chave sair do ar não muda NADA que se veja — as inscrições passam todas, o site não
        // reclama, e nem nós nem eles teríamos como perceber que o filtro parou de existir.
        if (!_ranking.Configurado)
        {
            await AnotarAsync(torneio, categoria, null, candidatos
                .Select(p => new Anotacao(p.Cpf, p.Nome, ResultadoDaConsulta.SemResposta, false)), ct);
            return null;
        }

        // Categoria sem par no ranking não é validada. Ver Services/CategoriaDoRankingRs: o
        // nome no Padelizou é texto livre e a API deles não confere sexo, então casar no chute
        // devolveria um "APROVADO" tranquilo e errado.
        if (categoria.RankingRsCategoriaId is not int categoriaRsId)
        {
            await AnotarAsync(torneio, categoria, null, candidatos
                .Select(p => new Anotacao(p.Cpf, p.Nome, ResultadoDaConsulta.SemDePara, false)), ct);
            return null;
        }

        // Quem o organizador já liberou nesta categoria nem chega a ser consultado: a decisão
        // dele vale mais que a resposta da API, e reconsultar só gastaria a cota da chave.
        var cpfs = candidatos.Select(p => p.Cpf).ToList();
        var jaLiberados = await _context.BloqueiosDoRanking
            .Where(b => b.CategoriaId == categoria.Id
                        && cpfs.Contains(b.Cpf)
                        && b.Situacao == SituacaoDoBloqueio.Liberado)
            .Select(b => b.Cpf)
            .ToListAsync(ct);

        // ⚠️ O liberado NÃO vira anotação nova, e a linha antiga dele (Barrado) fica como
        // está: o ranking barrou mesmo, e quem passou por cima foi o organizador. Reescrever
        // pra "Aprovado" apagaria do relatório exatamente o caso em que a nossa decisão
        // discordou da deles — que é o que eles mais têm interesse em ver.
        var aConsultar = candidatos.Where(p => !jaLiberados.Contains(p.Cpf)).ToList();
        if (aConsultar.Count == 0) return null;

        var resultados = await _ranking.ValidarLoteAsync(
            aConsultar.Select(p => new AtletaParaValidar(p.Nome, categoriaRsId, p.Cpf)).ToList(), ct);

        var porCpf = resultados
            .Where(r => r.Referencia != null)
            .GroupBy(r => r.Referencia!).ToDictionary(g => g.Key, g => g.First());

        // Uma anotação por candidato, e não uma por resposta: se a API devolver o lote
        // incompleto, quem ficou de fora não pode desaparecer do relatório como se nunca
        // tivesse tentado se inscrever.
        await AnotarAsync(torneio, categoria, categoriaRsId, aConsultar.Select(p =>
        {
            if (!porCpf.TryGetValue(p.Cpf, out var r))
                return new Anotacao(p.Cpf, p.Nome, ResultadoDaConsulta.SemResposta, false);

            string resultado = r.Resposta switch
            {
                RespostaDoRanking.Aprovado => ResultadoDaConsulta.Aprovado,
                RespostaDoRanking.ForaDeCategoria => ResultadoDaConsulta.Barrado,
                _ => ResultadoDaConsulta.SemResposta,
            };
            return new Anotacao(p.Cpf, p.Nome, resultado, r.EncontradoNoRanking);
        }), ct);

        var reprovados = resultados.Where(r => r.Reprovado && r.Referencia != null).ToList();
        if (reprovados.Count == 0) return null;

        foreach (var r in reprovados)
        {
            await RegistrarAsync(torneio, categoria, categoriaRsId, r, ct);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ranking RS barrou {Quantos} pessoa(s) na categoria {Categoria} do torneio {Torneio}.",
            reprovados.Count, categoria.Id, torneio.Id);

        return Frase.Recusa(categoria.Nome, reprovados);
    }

    private record Anotacao(string Cpf, string Nome, string Resultado, bool EncontradoNoRanking);

    // Grava (ou atualiza) o que aconteceu com cada pessoa nesta categoria.
    //
    // ⚠️ Salva aqui dentro, e não no chamador: `MotivoDeRecusaAsync` tem quatro pontos de
    // saída, e três deles devolvem `null` sem gravar mais nada. Deixar o SaveChanges pro fim
    // perderia a anotação em exatamente os casos que mais interessam — os de "ninguém foi
    // consultado". O caminho que barrou salva de novo logo depois, junto dos bloqueios; um
    // SaveChanges a mais no mesmo request é barato.
    private async Task AnotarAsync(Torneio torneio, Categoria categoria, int? categoriaRsId,
        IEnumerable<Anotacao> anotacoes, CancellationToken ct)
    {
        var lista = anotacoes.ToList();
        if (lista.Count == 0) return;

        var cpfs = lista.Select(a => a.Cpf).ToList();
        var existentes = await _context.ConsultasAoRankingRs
            .Where(c => c.CategoriaId == categoria.Id && cpfs.Contains(c.Cpf))
            .ToListAsync(ct);

        foreach (var anotacao in lista)
        {
            var linha = existentes.FirstOrDefault(c => c.Cpf == anotacao.Cpf);
            if (linha == null)
            {
                linha = new ConsultaAoRankingRs
                {
                    TorneioId = torneio.Id,
                    CategoriaId = categoria.Id,
                    Cpf = anotacao.Cpf,
                };
                _context.ConsultasAoRankingRs.Add(linha);
            }

            linha.NomeConsultado = anotacao.Nome;
            linha.CategoriaRsId = categoriaRsId;
            linha.CategoriaRsNome = categoriaRsId is int id ? CategoriaDoRankingRs.NomeDoId(id) : null;
            linha.Resultado = anotacao.Resultado;
            linha.EncontradoNoRanking = anotacao.EncontradoNoRanking;
            linha.CriadoEm = DateTime.Now;
        }

        await _context.SaveChangesAsync(ct);
    }

    // ── RECONFERIR UM TORNEIO INTEIRO, DEPOIS DO FATO ─────────────────────────────────────
    //
    // Pergunta ao ranking sobre quem JÁ ESTÁ INSCRITO. Existe porque a validação sempre rodou
    // no instante da inscrição e só passou a deixar registro em 10/08/2026 — então o primeiro
    // torneio real ficou com 20 de 22 pessoas sem resposta guardada, e não havia como saber se
    // elas foram aprovadas ou se simplesmente não estão no ranking.
    //
    // ⚠️ TRÊS COISAS QUE ELA NUNCA FAZ, e são o que a tornam segura de apertar:
    //
    //  1. **Não desinscreve ninguém, nunca.** A pessoa já está no torneio; a chave dela já pode
    //     ter sido sorteada. O resultado vira INFORMAÇÃO na tela, e quem decide o que fazer com
    //     ela é o organizador — do mesmo jeito que a recusa na inscrição nunca é palavra final.
    //  2. **Não cria `BloqueioDoRanking`.** Aquela tabela é a fila de "tentou entrar e foi
    //     barrado"; pôr aqui alguém que ENTROU faria o organizador ler como inscrição recusada.
    //  3. **Não inventa resposta quando não dá pra perguntar.** Sem chave configurada ela se
    //     recusa a rodar, em vez de gravar um monte de "não deu pra consultar" — numa ação que
    //     alguém apertou de propósito, silêncio disfarçado de resultado é o pior desfecho.
    public record Reconferencia(
        int Consultados,
        int Aprovados,
        int Barrados,
        int SemPontuacaoNoRanking,
        int SemCorrespondencia,
        int SemResposta)
    {
        public static readonly Reconferencia Nenhuma = new(0, 0, 0, 0, 0, 0);
    }

    public async Task<Reconferencia> ReconferirTorneioAsync(Torneio torneio, CancellationToken ct = default)
    {
        var categorias = await _context.Categorias
            .Where(c => c.TorneioId == torneio.Id)
            .ToListAsync(ct);

        int consultados = 0, aprovados = 0, barrados = 0, semPontuacao = 0, semCorrespondencia = 0, semResposta = 0;

        foreach (var categoria in categorias)
        {
            var pessoas = await InscritosDaCategoriaAsync(categoria.Id, ct);
            if (pessoas.Count == 0) continue;

            // Categoria sem par no ranking: ninguém a consultar, mas o relatório precisa saber
            // POR QUE essa gente ficou sem conferência — senão ela cai no balde errado.
            if (categoria.RankingRsCategoriaId is not int categoriaRsId)
            {
                await AnotarAsync(torneio, categoria, null, pessoas
                    .Select(p => new Anotacao(p.Cpf, p.Nome, ResultadoDaConsulta.SemDePara, false)), ct);
                semCorrespondencia += pessoas.Count;
                continue;
            }

            var resultados = await _ranking.ValidarLoteAsync(
                pessoas.Select(p => new AtletaParaValidar(p.Nome, categoriaRsId, p.Cpf)).ToList(), ct);

            var porCpf = resultados.Where(r => r.Referencia != null)
                .GroupBy(r => r.Referencia!).ToDictionary(g => g.Key, g => g.First());

            var anotacoes = new List<Anotacao>();
            foreach (var pessoa in pessoas)
            {
                if (!porCpf.TryGetValue(pessoa.Cpf, out var r))
                {
                    anotacoes.Add(new Anotacao(pessoa.Cpf, pessoa.Nome, ResultadoDaConsulta.SemResposta, false));
                    semResposta++;
                    continue;
                }

                switch (r.Resposta)
                {
                    case RespostaDoRanking.Aprovado:
                        anotacoes.Add(new Anotacao(pessoa.Cpf, pessoa.Nome, ResultadoDaConsulta.Aprovado, r.EncontradoNoRanking));
                        aprovados++;
                        // ⚠️ A distinção que motivou este botão: quem não aparece no ranking
                        // passa por NÃO TER O QUE PROVAR, e não por ter sido conferido contra
                        // pontos. Somar os dois esconderia justamente o que se quer descobrir.
                        if (!r.EncontradoNoRanking) semPontuacao++;
                        consultados++;
                        break;

                    case RespostaDoRanking.ForaDeCategoria:
                        anotacoes.Add(new Anotacao(pessoa.Cpf, pessoa.Nome, ResultadoDaConsulta.Barrado, r.EncontradoNoRanking));
                        barrados++;
                        consultados++;
                        break;

                    default:
                        anotacoes.Add(new Anotacao(pessoa.Cpf, pessoa.Nome, ResultadoDaConsulta.SemResposta, false));
                        semResposta++;
                        break;
                }
            }

            await AnotarAsync(torneio, categoria, categoriaRsId, anotacoes, ct);
        }

        _logger.LogInformation(
            "Reconferência do torneio {Torneio} no ranking: {Consultados} consultados, {Barrados} fora de categoria.",
            torneio.Id, consultados, barrados);

        return new Reconferencia(consultados, aprovados, barrados, semPontuacao, semCorrespondencia, semResposta);
    }

    // Quem está inscrito numa categoria, com nome e CPF. As exclusões (lista de espera, time)
    // vêm de AcertoComORankingRs — a mesma régua do dinheiro e do relatório, num lugar só.
    private async Task<List<Pessoa>> InscritosDaCategoriaAsync(int categoriaId, CancellationToken ct)
    {
        var duplas = await AcertoComORankingRs
            .DuplasQueContam(_context.Duplas.Where(d => d.CategoriaId == categoriaId))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id })
            .ToListAsync(ct);

        var americanas = await AcertoComORankingRs
            .AmericanasQueContam(_context.InscricoesAmericanas.Where(i => i.CategoriaId == categoriaId))
            .Select(i => i.JogadorId)
            .ToListAsync(ct);

        var ids = duplas.SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
            .Where(i => i != null).Select(i => i!.Value)
            .Concat(americanas)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new List<Pessoa>();

        return await _context.Jogadores
            .Where(j => ids.Contains(j.Id) && j.Cpf != null && j.Cpf != "" && j.Nome != "")
            .Select(j => new Pessoa(j.Nome, j.Cpf))
            .ToListAsync(ct);
    }

    // Uma linha por pessoa em cada categoria — tentar de novo ATUALIZA, não empilha.
    private async Task RegistrarAsync(Torneio torneio, Categoria categoria, int categoriaRsId,
        ResultadoDoRanking resultado, CancellationToken ct)
    {
        var cpf = resultado.Referencia!;
        var existente = await _context.BloqueiosDoRanking
            .FirstOrDefaultAsync(b => b.CategoriaId == categoria.Id && b.Cpf == cpf, ct);

        var motivo = resultado.Motivo(categoria.Nome);

        if (existente == null)
        {
            _context.BloqueiosDoRanking.Add(new BloqueioDoRanking
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Cpf = cpf,
                NomeConsultado = resultado.Nome,
                CategoriaRsId = categoriaRsId,
                CategoriaRsNome = CategoriaDoRankingRs.NomeDoId(categoriaRsId),
                Motivo = motivo,
                Situacao = SituacaoDoBloqueio.Pendente,
            });
            return;
        }

        // Já existia. Atualiza o motivo (o ranking muda com o tempo) e a data da tentativa,
        // mas NÃO mexe na Situacao: se o organizador já bateu o martelo em "Mantido", uma nova
        // tentativa do jogador não pode devolver o caso pra fila como se fosse novidade.
        existente.NomeConsultado = resultado.Nome;
        existente.Motivo = motivo;
        existente.CriadoEm = DateTime.Now;
    }

    // As frases ficam separadas e estáticas pra poderem ser testadas sem banco nem rede.
    public static class Frase
    {
        public const string OrganizadorVaiAvaliar =
            "O organizador foi avisado e vai decidir se libera a inscrição.";

        public static string Recusa(string categoriaNoPadelizou, IReadOnlyList<ResultadoDoRanking> reprovados)
        {
            var motivos = string.Join(" ", reprovados.Select(r => r.Motivo(categoriaNoPadelizou)));
            return $"{motivos} {OrganizadorVaiAvaliar}";
        }
    }
}
