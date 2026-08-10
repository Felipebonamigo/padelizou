using Padelizou.Models;

namespace Padelizou.Services;

// A tela /Admin/RankingRsRelatorio: a parceria com o Ranking, contada de ponta a ponta —
// torneios que aderiram, quem se inscreveu por eles, quem foi barrado e quem passou pelo
// filtro.
//
// ⚠️ ELA É LIDA POR GENTE DE FORA DA CASA — é a única tela do painel com essa característica.
// Isso muda o padrão de qualidade de duas maneiras concretas:
//
//  1. NÚMERO SEM RÉGUA VIRA COMPROMISSO. "1.240 atletas conferidos" numa página que o parceiro
//     abre não é informação, é promessa. Por isso cada contagem daqui tem uma definição escrita
//     ao lado, e as três situações que produzem o mesmo silêncio no banco — categoria sem
//     de-para, servidor fora do ar e integração sem chave — aparecem SEPARADAS de quem foi
//     realmente aprovado. Ver Models/ConsultaAoRankingRs.
//
//  2. O QUE NÃO É DA CONTA DELES NÃO ENTRA. Nada de caixa do Padelizou, nada de outros
//     torneios, nada de telefone ou e-mail de atleta. O CPF aparece mascarado pra quem é de
//     fora e inteiro pro dono — o mesmo dado, duas plateias.
//
// A composição mora aqui, e não na Razor, pelo motivo de sempre nesta casa: quase tudo abaixo é
// DEFINIÇÃO ("passou pelo filtro", "conferido", "cobertura"), e definição que mora na tela muda
// sem ninguém perceber.
public static class RelatorioDoRankingRs
{
    // ── AS DEFINIÇÕES, ANTES DOS NÚMEROS ──────────────────────────────────────────────

    // ⚠️ "PASSOU PELO FILTRO" É EXATAMENTE ISTO, e nada mais: perguntamos ao ranking e ele
    // liberou. Não é "se inscreveu e não foi barrado" — essa conta incluiria quem nunca foi
    // perguntado, que é a maioria silenciosa em todo torneio com categoria fora do de-para.
    public static bool PassouPeloFiltro(ConsultaAoRankingRs c) =>
        c.Resultado == ResultadoDaConsulta.Aprovado;

    // Houve pergunta e houve resposta. É o denominador honesto da parceria: o que a integração
    // de fato conferiu, e não o que ela teve a chance de conferir.
    public static bool FoiConferida(ConsultaAoRankingRs c) =>
        c.Resultado is ResultadoDaConsulta.Aprovado or ResultadoDaConsulta.Barrado;

    // A pessoa se inscreveu sem que o ranking fosse ouvido. Não é falha de ninguém em
    // particular — é o buraco da cobertura, e é o número que mostra onde a parceria ainda
    // não chegou.
    public static bool PassouSemConferencia(ConsultaAoRankingRs c) =>
        c.Resultado is ResultadoDaConsulta.SemDePara or ResultadoDaConsulta.SemResposta;

    public static string RotuloDoResultado(string? resultado) => resultado switch
    {
        ResultadoDaConsulta.Aprovado => "Aprovado pelo ranking",
        ResultadoDaConsulta.Barrado => "Barrado pelo ranking",
        ResultadoDaConsulta.SemDePara => "Categoria sem correspondência",
        ResultadoDaConsulta.SemResposta => "Não foi possível consultar",
        _ => "Sem registro de consulta",
    };

    // O que o organizador decidiu sobre uma recusa. O vocabulário importa: "Mantido" é
    // linguagem de banco, e quem lê a tela precisa da frase que descreve o efeito.
    public static string RotuloDaDecisao(BloqueioDoRanking b) => b.Situacao switch
    {
        SituacaoDoBloqueio.Liberado => "Liberado pelo organizador",
        SituacaoDoBloqueio.Mantido => "Recusa mantida",
        _ => "Aguardando o organizador",
    };

    // ⚠️ CPF MASCARADO PRA QUEM É DE FORA. Eles já recebem nome e CPF das pessoas que a gente
    // consulta — mas receber sob demanda, uma a uma, é diferente de ter a lista inteira numa
    // página. O que a tela precisa é distinguir homônimos, e três dígitos bastam pra isso.
    public static string CpfNaTela(string? cpf, bool inteiro)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return "—";
        if (inteiro) return cpf;
        return cpf.Length >= 9 ? $"•••.{cpf.Substring(3, 3)}.•••-••" : "•••";
    }

    // ── AS LINHAS DA TELA ─────────────────────────────────────────────────────────────

    // Alguém que CONSEGUIU se inscrever num torneio da parceria, com o que o ranking disse a
    // respeito. `Consulta` nula = a inscrição entrou antes de existir registro de consulta
    // (ver o aviso de corte no topo da tela) ou por um caminho que não passa pela validação.
    public record Inscrito(
        int JogadorId,
        string Nome,
        string? Cpf,
        IReadOnlyList<string> Categorias,
        ConsultaAoRankingRs? Consulta)
    {
        public string Situacao => RotuloDoResultado(Consulta?.Resultado);
        public bool Conferido => Consulta != null && FoiConferida(Consulta);
        public bool Aprovado => Consulta != null && PassouPeloFiltro(Consulta);

        // ── POR QUE ESTA PESSOA NÃO FOI CONFERIDA ─────────────────────────────────────
        //
        // ⚠️ Três motivos MUITO diferentes, e a tela precisa dizer qual é qual. A primeira
        // versão desta página narrava um deles como se fosse quase sempre o caso ("categoria
        // sem correspondência") — e no primeiro torneio real a causa era outra: 20 das 22
        // pessoas se inscreveram ANTES de o registro existir. Numa página lida pelo parceiro,
        // atribuir a causa errada é pior que não explicar: ele lê a cobertura baixa como
        // integração ruim, quando o que houve foi o registro ter nascido depois.
        //
        // Sem registro nenhum: a inscrição é anterior à tabela de consultas. Não diz nada
        // sobre o ranking ter sido ouvido — provavelmente foi, e a gente é que não guardava.
        public bool SemRegistro => Consulta == null;

        // Perguntar era impossível: a categoria do torneio não casa com nenhuma do ranking.
        public bool SemCorrespondencia => Consulta?.Resultado == ResultadoDaConsulta.SemDePara;

        // Perguntamos e não veio resposta útil — servidor fora do ar, ou integração sem chave.
        public bool SemRespostaDoRanking => Consulta?.Resultado == ResultadoDaConsulta.SemResposta;
    }

    // Alguém que o ranking barrou. ⚠️ Barrado NÃO é inscrito: a recusa acontece antes de a
    // inscrição existir. Quem foi liberado pelo organizador e voltou a se inscrever aparece
    // nas duas listas — e está certo, são dois fatos diferentes sobre a mesma pessoa.
    public record Barrado(
        BloqueioDoRanking Bloqueio,
        string TorneioNome,
        string CategoriaNome)
    {
        public string Decisao => RotuloDaDecisao(Bloqueio);
        public bool AindaBarrado => Bloqueio.Situacao != SituacaoDoBloqueio.Liberado;
    }

    public record LinhaDoTorneio(
        Torneio Torneio,
        IReadOnlyList<Inscrito> Inscritos,
        IReadOnlyList<Barrado> Barrados,
        decimal Valor,
        AcertoRankingRs? Acerto)
    {
        public bool InscricoesAbertas => Torneio.Status == "Inscrições Abertas";
        public bool Acertado => Acerto != null;

        public int Conferidos => Inscritos.Count(i => i.Conferido);
        public int Aprovados => Inscritos.Count(i => i.Aprovado);
        public int SemConferencia => Inscritos.Count - Conferidos;

        // A repartição do número acima. ⚠️ Os três somam exatamente `SemConferencia` — há
        // teste guardando isso, porque um balde que não fecha a conta é pior que nenhum:
        // a tela passaria a explicar só uma parte do buraco e o resto sumiria.
        public int SemRegistro => Inscritos.Count(i => i.SemRegistro);
        public int SemCorrespondencia => Inscritos.Count(i => i.SemCorrespondencia);
        public int SemRespostaDoRanking => Inscritos.Count(i => i.SemRespostaDoRanking);

        // Barrado que o organizador manteve: a recusa produziu efeito de verdade. É o número
        // que prova que a integração fez alguma coisa neste torneio.
        public int RecusasQueFicaramDePe => Barrados.Count(b => b.AindaBarrado);
    }

    // ── OS TOTAIS DO TOPO ─────────────────────────────────────────────────────────────

    public record Totais(
        int Torneios,
        int TorneiosComInscricaoAberta,
        int Inscritos,
        int Conferidos,
        int Aprovados,
        int SemConferencia,
        int SemRegistro,
        int SemCorrespondencia,
        int SemRespostaDoRanking,
        int Barrados,
        int RecusasQueFicaramDePe,
        int LiberadosPeloOrganizador,
        decimal ValorEmAberto,
        decimal ValorJaAcertado)
    {
        public decimal ValorTotal => ValorEmAberto + ValorJaAcertado;

        // Que fatia dos inscritos o ranking realmente conferiu. É a régua mais honesta da
        // parceria e a que aponta o próximo passo: cobertura baixa quase sempre é categoria
        // sem de-para, não gente escapando.
        //
        // ⚠️ Sem inscrito nenhum a resposta é "não se aplica", e não 0% — 0% diria que a
        // integração falhou em conferir gente que não existe.
        public int? CoberturaEmPorcento =>
            Inscritos == 0 ? null : (int)Math.Round(100m * Conferidos / Inscritos);
    }

    public static Totais Somar(IEnumerable<LinhaDoTorneio> linhas)
    {
        var lista = linhas.ToList();
        var barrados = lista.SelectMany(l => l.Barrados).ToList();

        return new Totais(
            Torneios: lista.Count,
            TorneiosComInscricaoAberta: lista.Count(l => l.InscricoesAbertas),
            Inscritos: lista.Sum(l => l.Inscritos.Count),
            Conferidos: lista.Sum(l => l.Conferidos),
            Aprovados: lista.Sum(l => l.Aprovados),
            SemConferencia: lista.Sum(l => l.SemConferencia),
            SemRegistro: lista.Sum(l => l.SemRegistro),
            SemCorrespondencia: lista.Sum(l => l.SemCorrespondencia),
            SemRespostaDoRanking: lista.Sum(l => l.SemRespostaDoRanking),
            Barrados: barrados.Count,
            RecusasQueFicaramDePe: barrados.Count(b => b.AindaBarrado),
            LiberadosPeloOrganizador: barrados.Count(b => !b.AindaBarrado),
            // ⚠️ O dinheiro é somado da MESMA fotografia que o /Admin/RankingRs usa: em aberto
            // é o valor calculado agora, já acertado é o que ficou gravado no dia do acerto.
            // Recalcular o que já foi pago faria o total mudar sozinho toda vez que alguém se
            // inscrevesse num torneio antigo.
            ValorEmAberto: lista.Where(l => !l.Acertado).Sum(l => l.Valor),
            ValorJaAcertado: lista.Where(l => l.Acertado).Sum(l => l.Acerto!.Valor));
    }

    // Uma pessoa aparece uma vez por torneio, mesmo jogando duas categorias — a mesma régua do
    // dinheiro (Services/AcertoComORankingRs). Entre as categorias dela, vale a consulta que
    // foi conferida: quem foi perguntado numa das suas categorias foi perguntado.
    //
    // ⚠️ O casamento inscrito × consulta é por CPF, e não por JogadorId, porque na hora da
    // consulta a pessoa podia ainda não ter linha em `Jogador`.
    //
    // ⚠️ E É POR CPF **E CATEGORIA EM QUE ELA REALMENTE ENTROU** — este par, e não só o CPF.
    // O motivo apareceu rodando o app: a consulta acontece ANTES de a inscrição existir, e
    // várias regras podem recusá-la depois (uma categoria por jogador, inscrição repetida,
    // vagas). Sobra no banco um "Aprovado" de uma categoria em que a pessoa nunca entrou —
    // e, casando só por CPF, esse aprovado grudava nela na categoria que ela de fato joga,
    // que pode não ter sido conferida. A tela diria "aprovado pelo ranking" sobre uma
    // categoria que ninguém perguntou. Numa página que o parceiro lê, isso é o defeito mais
    // caro possível: não quebra nada, e afirma o que não aconteceu.
    public static List<Inscrito> Juntar(
        IEnumerable<AcertoComORankingRs.PessoaCobrada> cobradas,
        IReadOnlyDictionary<int, string> cpfDoJogador,
        IReadOnlyDictionary<int, HashSet<int>> categoriasDoJogador,
        IEnumerable<ConsultaAoRankingRs> consultas)
    {
        var porCpf = consultas
            .GroupBy(c => c.Cpf)
            .ToDictionary(g => g.Key, g => g.ToList());

        return cobradas.Select(p =>
        {
            cpfDoJogador.TryGetValue(p.JogadorId, out var cpf);
            categoriasDoJogador.TryGetValue(p.JogadorId, out var minhasCategorias);

            ConsultaAoRankingRs? consulta = null;
            if (!string.IsNullOrEmpty(cpf) && porCpf.TryGetValue(cpf, out var dela))
            {
                consulta = dela
                    .Where(c => minhasCategorias != null && minhasCategorias.Contains(c.CategoriaId))
                    // Conferida ganha de não-conferida; entre duas iguais, a mais recente. Sem
                    // isso, quem jogou duas categorias e foi conferido só numa apareceria como
                    // "não conferido" na sorte do agrupamento.
                    .OrderByDescending(FoiConferida)
                    .ThenByDescending(c => c.CriadoEm)
                    .FirstOrDefault();
            }

            return new Inscrito(p.JogadorId, p.Nome, cpf, p.Categorias, consulta);
        }).ToList();
    }
}
