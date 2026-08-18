using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A ENQUETE DE DEPOIS DO TORNEIO: quem jogou dá nota pro clube, pra organização e pro sistema,
// e escreve sobre cada um se quiser.
//
// Por que ela existe AGORA: o "Melhor Clube do ano" de 2027 precisa de um ano de avaliações, e
// esse relógio só começa quando a coleta começa. Se ela nascer em janeiro, o prêmio abre magro
// — a mesma razão que fez o MVP sair em 2026.
//
// Decisões de desenho:
// - A enquete mora na tela do MVP e usa a MESMA janela de 7 dias (o dono da janela é
//   MvpDoTorneio.DentroDaJanela).
// - ⚠️ **O AMERICANO NÃO AVALIA** (decisão do Felipe, 17/08/2026): a enquete acompanha o MVP
//   no que diz respeito ao FORMATO. A coleta do "Melhor Clube do ano" passa a sair só dos
//   torneios normais — menos dado, e essa é a escolha dele, feita sabendo do trade-off.
// - Mas ela continua NÃO obedecendo ao interruptor `UsaVotacaoDeMvp`: esse é do organizador
//   sobre a DISPUTA entre jogadores, e desligar a eleição não é dizer "não quero que falem do
//   meu clube". Torneio normal com MVP desligado avalia igual.
// - Quem responde é quem JOGOU (a régua do eleitorado do MVP, um dono só).
// - A média só aparece com 3+ respostas — "5,0 estrelas (1 avaliação)" é uma pessoa falando
//   com voz de consenso, o mesmo furo do "1º lugar com 0 pontos".
//
// ✍️ OS COMENTÁRIOS (18/08/2026, decisão do Felipe). Três textos opcionais — clube,
// organização e sistema — e UMA escolha só pra resposta inteira: assinada ou anônima.
//
//   · ASSINADA vai pro mural do torneio na hora, com nome e foto. A pessoa escolheu isso.
//   · ANÔNIMA chega só a quem recebe (organizador, dono do clube e Padelizou) e **não** entra
//     no mural sozinha. Se um deles achar que vale pros outros jogadores, publica — e aí sai
//     SEM o nome, que é exatamente o que foi combinado com quem escreveu. A tela diz isso
//     ANTES de a pessoa escrever, senão a publicação seria uma surpresa.
//
// ⚠️ O texto sobre o SISTEMA não fica na avaliação: vira uma linha em `FeedbackSite`, a caixa
// que já existe pra "o que você achou do Padelizou", com a moderação e o NPS dela. Uma segunda
// caixa de opinião sobre o sistema seria a regra duplicada de sempre — duas listas, e o admin
// lendo só uma.
public static class EnqueteDoTorneio
{
    public const int NotaMinima = 1;
    public const int NotaMaxima = 5;

    // Mesmo espírito do MvpDoTorneio.VotosMinimos: abaixo disso não há "média", há uma pessoa.
    public const int RespostasParaMostrarMedia = 3;

    // Cabe um parágrafo de verdade e não cabe um textão. O mesmo número está no `HasMaxLength`
    // do DbPadelContext e no `maxlength` da tela — as três réguas TÊM que ser a mesma, porque
    // o Postgres recusa `varchar` grande demais em vez de cortar.
    public const int TamanhoMaximoDoComentario = 600;

    // A janela é A MESMA do MVP, mais a régua do formato — e o `formato` é PARÂMETRO
    // OBRIGATÓRIO pela mesma razão que ele é obrigatório lá: são quatro lugares que perguntam
    // pela enquete (a tela, o POST da nota, o botão da página do torneio e o varredor do
    // aviso), e um deles esquecer não daria erro nenhum — daria enquete aberta num Americano.
    //
    // ⚠️ O que NÃO entra aqui é o interruptor `UsaVotacaoDeMvp`: ver o cabeçalho da classe.
    public static bool Aberta(string? statusDoTorneio, DateTime? ultimoJogo, DateTime agora,
        string? formato) =>
        FormatoDoTorneio.TemPosTorneio(formato)
        && MvpDoTorneio.DentroDaJanela(statusDoTorneio, ultimoJogo, agora);

    public static bool MediaVisivel(int respostas) => respostas >= RespostasParaMostrarMedia;

    // ⚠️ A nota do SISTEMA é opcional e as outras duas não. Não é distração: a coluna nasceu
    // depois, e as respostas de agosto/2026 não têm o que dizer sobre uma pergunta que não
    // existia. Nulo passa; zero (que é o que um formulário antigo mandaria) não.
    public static string? ProblemaComNotas(int notaClube, int notaOrganizacao, int? notaSistema)
    {
        if (ForaDaEscala(notaClube) || ForaDaEscala(notaOrganizacao)
            || (notaSistema != null && ForaDaEscala(notaSistema.Value)))
        {
            return $"A nota vai de {NotaMinima} a {NotaMaxima} estrelas.";
        }
        return null;
    }

    private static bool ForaDaEscala(int nota) => nota < NotaMinima || nota > NotaMaxima;

    // O que barra um texto. Vazio nunca é problema — os três comentários são opcionais.
    //
    // ⚠️ O FILTRO DE PALAVRÕES SÓ VALE PRO QUE VAI DIRETO AO MURAL, e a assimetria é o ponto:
    // a resposta anônima é conversa privada com quem organiza — recusar ali a crítica escrita
    // com raiva seria calar exatamente o que a enquete existe pra ouvir. Ela só alcança outras
    // pessoas se um humano publicar, e aí o humano leu.
    public static string? ProblemaComComentario(string? texto, bool vaiAoMural)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        if (texto.Trim().Length > TamanhoMaximoDoComentario)
            return $"Deixe cada comentário em até {TamanhoMaximoDoComentario} caracteres.";

        if (vaiAoMural && FiltroPalavroes.EhOfensivo(texto))
        {
            return "Esse texto tem palavra que não entra num comentário público. "
                 + "Reescreva, ou marque a resposta como anônima — aí ela vai só pra quem organiza.";
        }

        return null;
    }

    // Registra (ou troca) a resposta. Devolve o motivo da recusa, ou null quando deu certo.
    // ⚠️ TODA a validação acontece AQUI — a tela só esconde o que não cabe, e POST montado à
    // mão não passa por view nenhuma (a mesma régua do VotarAsync).
    public static async Task<string?> AvaliarAsync(
        DbPadelContext contexto, int torneioId, int jogadorId,
        RespostaDaEnquete resposta, DateTime agora)
    {
        if (ProblemaComNotas(resposta.NotaClube, resposta.NotaOrganizacao, resposta.NotaSistema)
            is { } problemaNota) return problemaNota;

        var vaiAoMural = !resposta.Anonimo;
        foreach (var texto in new[] { resposta.ComentarioClube, resposta.ComentarioOrganizacao })
        {
            if (ProblemaComComentario(texto, vaiAoMural) is { } problema) return problema;
        }
        // O do sistema nunca vai a mural nenhum sozinho (a home só publica o que o admin
        // liberar), mas o limite de tamanho vale igual — a coluna é a mesma.
        if (ProblemaComComentario(resposta.ComentarioSistema, vaiAoMural: false) is { } problemaSistema)
            return problemaSistema;

        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Status, t.Formato })
            .FirstOrDefaultAsync();
        if (torneio == null) return "Torneio não encontrado.";

        var fins = await contexto.Partidas
            .AsNoTracking()
            .Where(p => p.TorneioId == torneioId && p.VencedorId != null)
            .Select(p => p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto)
            .ToListAsync();

        // ⚠️ O formato é conferido AQUI, no servidor, e não só escondido na tela: quem quiser
        // avaliar um Americano precisa montar o POST à mão, e POST montado à mão não passa por
        // view nenhuma. Sem esta linha a régua nova seria só cosmética.
        if (!Aberta(torneio.Status, MvpDoTorneio.UltimoJogo(fins), agora, torneio.Formato))
            return "A avaliação deste torneio não está aberta — ela vale na semana seguinte ao fim.";

        var eleitores = await MvpDoTorneio.EleitoresAsync(contexto, torneioId);
        if (!eleitores.Contains(jogadorId))
            return "Só quem jogou este torneio avalia o clube e a organização.";

        var existente = await contexto.AvaliacoesDeTorneio
            .FirstOrDefaultAsync(a => a.TorneioId == torneioId && a.JogadorId == jogadorId);

        if (existente != null)
        {
            // Trocar a resposta, nunca somar outra — o índice único do banco garante isso
            // mesmo em dois cliques simultâneos.
            existente.NotaClube = resposta.NotaClube;
            existente.NotaOrganizacao = resposta.NotaOrganizacao;
            existente.NotaSistema = resposta.NotaSistema;
            existente.ComentarioClube = Limpar(resposta.ComentarioClube);
            existente.ComentarioOrganizacao = Limpar(resposta.ComentarioOrganizacao);
            existente.Anonimo = resposta.Anonimo;
            existente.AtualizadoEm = agora;
            AjustarPublicacao(existente, agora);
        }
        else
        {
            existente = new AvaliacaoDoTorneio
            {
                TorneioId = torneioId,
                JogadorId = jogadorId,
                NotaClube = resposta.NotaClube,
                NotaOrganizacao = resposta.NotaOrganizacao,
                NotaSistema = resposta.NotaSistema,
                ComentarioClube = Limpar(resposta.ComentarioClube),
                ComentarioOrganizacao = Limpar(resposta.ComentarioOrganizacao),
                Anonimo = resposta.Anonimo,
                CriadoEm = agora,
            };
            AjustarPublicacao(existente, agora);
            contexto.AvaliacoesDeTorneio.Add(existente);
        }

        await GravarOTextoDoSistemaAsync(contexto, torneioId, jogadorId, resposta, agora);

        await contexto.SaveChangesAsync();
        return null;
    }

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    // QUANDO O TEXTO ENTRA NO MURAL. Assinado entra na hora; anônimo espera um humano.
    //
    // ⚠️ Duas coisas que parecem detalhe e não são:
    //   · trocar de assinado pra anônimo TIRA do mural. É a pessoa voltando atrás, e o texto
    //     dela não pode continuar exposto com o nome porque já estava lá.
    //   · o que um moderador ESCONDEU não volta sozinho quando a pessoa reenvia a resposta.
    //     Sem isso, editar uma vírgula seria o jeito de furar a moderação. O sinal de que
    //     houve mão humana é `PublicadoPorId` preenchido com `PublicadoEm` nulo.
    private static void AjustarPublicacao(AvaliacaoDoTorneio avaliacao, DateTime agora)
    {
        var temTexto = avaliacao.ComentarioClube != null || avaliacao.ComentarioOrganizacao != null;

        if (avaliacao.Anonimo || !temTexto)
        {
            avaliacao.PublicadoEm = null;
            return;
        }

        var escondidoPorAlguem = avaliacao.PublicadoPorId != null && avaliacao.PublicadoEm == null;
        if (escondidoPorAlguem) return;

        avaliacao.PublicadoEm ??= agora;
    }

    // O TEXTO SOBRE O PADELIZOU VAI PRA CAIXA DE FEEDBACK, não pra cá.
    //
    // ⚠️ E vai SEM NOTA. A escala de lá é 0-10 (NPS: 9-10 promotor, 0-6 detrator) e a daqui é
    // 1-5 estrelas. Converter uma na outra inventaria opinião que ninguém deu — cinco estrelas
    // viraria "10, promotor" e três estrelas viraria "6, detrator", quando três estrelas é o
    // meio da escala. A nota do sistema fica na avaliação, na escala em que foi dada.
    private static async Task GravarOTextoDoSistemaAsync(
        DbPadelContext contexto, int torneioId, int jogadorId, RespostaDaEnquete resposta, DateTime agora)
    {
        var texto = Limpar(resposta.ComentarioSistema);

        var existente = await contexto.FeedbacksSite
            .FirstOrDefaultAsync(f => f.TorneioId == torneioId && f.JogadorId == jogadorId);

        if (texto == null)
        {
            // Apagou o que tinha escrito. Some — a menos que o admin já tenha publicado na
            // home: fazer um depoimento sumir de lá em silêncio é pior que deixá-lo.
            if (existente != null && !existente.Exibir) contexto.FeedbacksSite.Remove(existente);
            return;
        }

        if (existente != null)
        {
            existente.Texto = texto;
            existente.Anonimo = resposta.Anonimo;
            return;
        }

        contexto.FeedbacksSite.Add(new FeedbackSite
        {
            JogadorId = jogadorId,
            TorneioId = torneioId,
            Texto = texto,
            Nota = null,
            Anonimo = resposta.Anonimo,
            CriadoEm = agora,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // QUEM LÊ O QUE FOI ESCRITO
    // ─────────────────────────────────────────────────────────────────────────────────────

    // O mural público do torneio: só o que está publicado.
    public static Task<List<ComentarioDoTorneio>> PublicadosAsync(DbPadelContext contexto, int torneioId) =>
        LerAsync(contexto, a => a.TorneioId == torneioId && a.PublicadoEm != null);

    // A visão de quem recebe (organizador, dono do clube, Padelizou): tudo que tem texto,
    // publicado ou não. É aqui que o anônimo aparece — sem nome, como foi combinado.
    public static Task<List<ComentarioDoTorneio>> ParaModerarAsync(DbPadelContext contexto, int torneioId) =>
        LerAsync(contexto, a => a.TorneioId == torneioId
                             && (a.ComentarioClube != null || a.ComentarioOrganizacao != null));

    // O que disseram do CLUBE, atravessando os torneios que ele sediou — a tela do dono.
    public static Task<List<ComentarioDoTorneio>> DoClubeAsync(DbPadelContext contexto, int clubeId, int limite = 20) =>
        LerAsync(contexto, a => a.Torneio.ClubeId == clubeId && a.ComentarioClube != null, limite);

    private static async Task<List<ComentarioDoTorneio>> LerAsync(
        DbPadelContext contexto,
        System.Linq.Expressions.Expression<Func<AvaliacaoDoTorneio, bool>> filtro,
        int limite = 200)
    {
        var linhas = await contexto.AvaliacoesDeTorneio
            .AsNoTracking()
            .Where(filtro)
            .OrderByDescending(a => a.CriadoEm)
            .Take(limite)
            .Select(a => new
            {
                a.Id,
                a.TorneioId,
                TorneioNome = a.Torneio.Nome,
                a.NotaClube,
                a.NotaOrganizacao,
                a.ComentarioClube,
                a.ComentarioOrganizacao,
                a.Anonimo,
                a.PublicadoEm,
                a.CriadoEm,
                AutorNome = a.Jogador.Nome,
                AutorApelido = a.Jogador.Apelido,
                AutorFoto = a.Jogador.FotoPerfil,
                AutorId = a.JogadorId,
            })
            .ToListAsync();

        // ⚠️ O NOME SÓ É MONTADO PRA QUEM ASSINOU. A projeção acima traz as colunas porque a
        // consulta é uma só, mas o objeto que sai daqui — e que as views recebem — não carrega
        // autor nenhum quando a resposta é anônima. Assim nenhuma tela pode "esquecer" de
        // esconder: não há o que esconder.
        return linhas.Select(l => new ComentarioDoTorneio
        {
            AvaliacaoId = l.Id,
            TorneioId = l.TorneioId,
            Torneio = l.TorneioNome,
            NotaClube = l.NotaClube,
            NotaOrganizacao = l.NotaOrganizacao,
            SobreOClube = l.ComentarioClube,
            SobreAOrganizacao = l.ComentarioOrganizacao,
            Anonimo = l.Anonimo,
            Publicado = l.PublicadoEm != null,
            Quando = l.CriadoEm,
            AutorId = l.Anonimo ? null : l.AutorId,
            Autor = l.Anonimo ? null : NomeBonito.ComApelido(l.AutorNome, l.AutorApelido),
            FotoDoAutor = l.Anonimo ? null : l.AutorFoto,
        }).ToList();
    }

    // Quem pode ler o que é anônimo e decidir o que vira público: o organizador do torneio, o
    // dono (ou administrador) do clube que sediou, e o Padelizou.
    //
    // ⚠️ Mora AQUI, e não no controller, porque duas telas fazem a mesma pergunta — a gestão
    // do torneio e o painel do clube. Duas cópias divergiriam no dia em que uma ganhasse o
    // administrador do clube e a outra não.
    public static async Task<bool> PodeModerarAsync(DbPadelContext contexto, int torneioId, int? jogadorId)
    {
        if (jogadorId is not int quem || quem <= 0) return false;

        if (await contexto.TorneioOrganizadores.AnyAsync(o => o.TorneioId == torneioId && o.JogadorId == quem))
            return true;

        if (await contexto.Jogadores.AnyAsync(j => j.Id == quem && (j.IsAdminRaiz || j.IsAdminGeral)))
            return true;

        var clubeId = await contexto.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => (int?)t.ClubeId)
            .FirstOrDefaultAsync();
        if (clubeId == null) return false;

        return await contexto.Clubes.AnyAsync(c => c.Id == clubeId.Value && c.DonoId == quem)
            || await contexto.ClubeAdministradores.AnyAsync(a => a.ClubeId == clubeId.Value && a.JogadorId == quem);
    }

    // Põe no mural, ou tira. Devolve o motivo da recusa, ou null quando deu certo.
    //
    // ⚠️ A PERMISSÃO É CONFERIDA AQUI DENTRO, com o id que veio da sessão — publicar o texto
    // de outra pessoa é o tipo de poder que não pode depender de a tela ter escondido o botão.
    public static async Task<string?> PublicarAsync(
        DbPadelContext contexto, int avaliacaoId, int quemId, bool publicar, DateTime agora)
    {
        var avaliacao = await contexto.AvaliacoesDeTorneio.FirstOrDefaultAsync(a => a.Id == avaliacaoId);
        if (avaliacao == null) return "Avaliação não encontrada.";

        if (!await PodeModerarAsync(contexto, avaliacao.TorneioId, quemId))
            return "Só quem organiza o torneio ou cuida do clube publica um comentário.";

        if (avaliacao.ComentarioClube == null && avaliacao.ComentarioOrganizacao == null)
            return "Essa avaliação não tem texto pra publicar.";

        avaliacao.PublicadoEm = publicar ? agora : null;
        avaliacao.PublicadoPorId = quemId;
        await contexto.SaveChangesAsync();
        return null;
    }

    // O resumo pro organizador (e pro futuro "Melhor Clube do ano"): médias e quantos
    // responderam. As médias vêm nulas enquanto não há resposta o bastante pra ser "média".
    public static async Task<ResumoDaEnquete> ResumoAsync(DbPadelContext contexto, int torneioId)
    {
        var notas = await contexto.AvaliacoesDeTorneio
            .AsNoTracking()
            .Where(a => a.TorneioId == torneioId)
            .Select(a => new
            {
                a.NotaClube,
                a.NotaOrganizacao,
                a.NotaSistema,
                TemTexto = a.ComentarioClube != null || a.ComentarioOrganizacao != null,
                Publicado = a.PublicadoEm != null,
            })
            .ToListAsync();

        var resumo = new ResumoDaEnquete
        {
            Respostas = notas.Count,
            ComTexto = notas.Count(n => n.TemTexto),
            EsperandoDecisao = notas.Count(n => n.TemTexto && !n.Publicado),
            RespostasSobreOSistema = notas.Count(n => n.NotaSistema != null),
        };

        if (MediaVisivel(notas.Count))
        {
            resumo.MediaClube = Math.Round(notas.Average(n => n.NotaClube), 1);
            resumo.MediaOrganizacao = Math.Round(notas.Average(n => n.NotaOrganizacao), 1);

            // ⚠️ A média do sistema tem contagem PRÓPRIA: quem respondeu antes de a pergunta
            // existir não deu nota nenhuma ao Padelizou, e três respostas velhas não fazem uma
            // média nova. Ela só aparece quando 3+ pessoas deram nota AO SISTEMA.
            var doSistema = notas.Where(n => n.NotaSistema != null).Select(n => n.NotaSistema!.Value).ToList();
            if (MediaVisivel(doSistema.Count)) resumo.MediaSistema = Math.Round(doSistema.Average(), 1);
        }

        return resumo;
    }
}

// O que veio do formulário. Existe pra assinatura do AvaliarAsync não virar sete parâmetros
// soltos — três notas, três textos e um booleano em fila são um convite a trocar dois de lugar
// (e `int, int, int?` não dá erro de compilação quando isso acontece).
public sealed class RespostaDaEnquete
{
    public int NotaClube { get; set; }
    public int NotaOrganizacao { get; set; }
    public int? NotaSistema { get; set; }

    public string? ComentarioClube { get; set; }
    public string? ComentarioOrganizacao { get; set; }
    public string? ComentarioSistema { get; set; }

    public bool Anonimo { get; set; }
}

// Um comentário como as telas veem. ⚠️ `Autor` e `FotoDoAutor` chegam NULOS quando a resposta
// é anônima — a view não tem escolha a fazer, e por isso não tem como errar.
public sealed class ComentarioDoTorneio
{
    public int AvaliacaoId { get; set; }
    public int TorneioId { get; set; }
    public string Torneio { get; set; } = "";

    public int NotaClube { get; set; }
    public int NotaOrganizacao { get; set; }

    public string? SobreOClube { get; set; }
    public string? SobreAOrganizacao { get; set; }

    public bool Anonimo { get; set; }
    public bool Publicado { get; set; }
    public DateTime Quando { get; set; }

    public int? AutorId { get; set; }
    public string? Autor { get; set; }
    public string? FotoDoAutor { get; set; }
}

public sealed class ResumoDaEnquete
{
    public int Respostas { get; set; }
    public double? MediaClube { get; set; }
    public double? MediaOrganizacao { get; set; }

    public double? MediaSistema { get; set; }
    public int RespostasSobreOSistema { get; set; }

    public int ComTexto { get; set; }
    public int EsperandoDecisao { get; set; }

    public bool TemMedia => MediaClube != null;
}
