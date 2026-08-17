using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O MELHOR JOGADOR DO TORNEIO, eleito por quem jogou.
//
// A cédula são os CAMPEÕES de cada categoria; o eleitorado é TODO MUNDO que esteve na chave
// (decisão do Felipe, 11/08/2026). O desenho é esse por dois motivos, e os dois importam:
//
//   · é a única coisa que traz o inscrito COMUM de volta ao site depois que o torneio acaba —
//     ele perdeu na segunda rodada e não tem mais nada pra fazer aqui até o próximo;
//   · eleitorado grande é o que torna a eleição difícil de combinar. Com os campeões votando
//     entre si seriam 5 a 10 votos, empate viraria o caso normal, e quatro amigos campeões de
//     categorias diferentes decidiriam o MVP entre eles — o mesmo furo do Americano que o
//     anti-farm dos Desafios existe pra fechar.
//
// ⚠️ A JANELA É LIDA DO RELÓGIO, NUNCA GRAVADA. Não há coluna "votação encerrada" nem job que
// feche nada: a votação está aberta enquanto o torneio está finalizado E faz menos de
// `DiasParaVotar` dias que a última bola foi jogada. É a mesma lição do "Expirado NÃO É STATUS"
// dos Desafios e do "Vencido" dos leads — estado gravado por job fica errado quando o job falha,
// e ninguém descobre.
//
// ⚠️ E é isso que faz o recurso valer "pros próximos torneios" sem nenhum caso especial: torneio
// que acabou mês passado já nasce com a janela fechada, sem migração de dados, sem flag, sem
// data de corte escrita no código.
public static class MvpDoTorneio
{
    // Uma semana. O torneio acaba no domingo e a semana inteira serve pra votar — quem jogou
    // volta ao site pelo menos uma vez nesse intervalo. Prazo curto demais elege quem estava
    // com o celular na mão no domingo à noite.
    public const int DiasParaVotar = 7;

    // Abaixo disto não se proclama MVP. "Melhor jogador do torneio, com 1 voto" é uma frase que
    // não se sustenta — mesma régua do "1º lugar com 0 pontos" que saiu do ranking e do card do
    // ano que não nasce com menos de 3 jogos.
    public const int VotosMinimos = 3;

    // Quando a última bola foi jogada. É daqui que a janela conta.
    //
    // ⚠️ Sai da PARTIDA e não de um campo do torneio, porque campo desses não existe: o
    // `Status = "Finalizado"` é carimbado em seis lugares diferentes e nenhum deles guarda a
    // hora. Acrescentar a coluna obrigaria a mexer nos seis — e bastaria esquecer um pra a
    // votação nunca abrir naquele caminho, caladinho.
    public static DateTime? UltimoJogo(IEnumerable<DateTime?> fimDasPartidas)
    {
        var datas = fimDasPartidas.Where(d => d != null).Select(d => d!.Value).ToList();
        return datas.Count == 0 ? null : datas.Max();
    }

    public static DateTime? FechaEm(DateTime? ultimoJogo) => ultimoJogo?.AddDays(DiasParaVotar);

    // A votação aceita voto agora?
    //
    // Três condições: o organizador QUER MVP neste torneio, o torneio está FINALIZADO (a
    // votação é depois que acaba, como pedido) e estamos dentro da janela.
    //
    // ⚠️ `usaVotacao` é PARÂMETRO OBRIGATÓRIO, e não um filtro de quem chama. São quatro
    // lugares que perguntam pela votação (a tela, o POST do voto, o botão da página do torneio
    // e a apuração); um deles esquecer o interruptor não daria erro nenhum — daria votação
    // acontecendo num torneio que a desligou. Aqui é impossível esquecer: a assinatura obriga.
    // É o mesmo arranjo do `PontosDoTorneio.Pontos`, que exige o status pelo mesmo motivo.
    //
    // ⚠️ Não há um teste separado pra "cancelado", e isso é decisão e não esquecimento:
    // `Status` é UMA coluna, então torneio cancelado vale "Cancelado" e nunca "Finalizado" —
    // exigir o segundo já exclui o primeiro. Um `EstaCancelado` a mais aqui seria linha morta
    // com cara de regra, que é pior do que não ter: o próximo leitor confia nela.
    public static bool Aberta(bool usaVotacao, string? statusDoTorneio, DateTime? ultimoJogo,
        DateTime agora, string? formato)
    {
        if (!usaVotacao) return false;
        if (!ElegeMvp(formato)) return false;

        return DentroDaJanela(statusDoTorneio, ultimoJogo, agora);
    }

    // ⚠️ AMERICANO NÃO ELEGE MVP (decisão do Felipe, 16/08/2026) — "é apenas para os torneios
    // normais". A razão estava no desenho desde o começo e agora virou regra: no rodízio a
    // cédula encolhe a quase nada (todo mundo joga com todo mundo, não há final nem dupla
    // fixa), e num sábado de 8 pessoas "o melhor do torneio" seria escolhido por quatro amigos
    // entre eles. O Americano já tem trilha própria — o Ranking Americano.
    //
    // ⚠️ Vale pros DOIS do rodízio (individual e de duplas): a régua é `FormatoDoTorneio
    // .EhAmericano`, o mesmo dono que o resto do sistema usa pra essa pergunta.
    public static bool ElegeMvp(string? formato) => !FormatoDoTorneio.EhAmericano(formato);

    // A JANELA PURA: o torneio acabou e ainda faz menos de 7 dias do último jogo. Separada de
    // propósito — quem pergunta por ela não é só o MVP: a ENQUETE do clube usa a mesma janela
    // e NÃO obedece nem ao interruptor nem ao formato (ver Services/EnqueteDoTorneio).
    public static bool DentroDaJanela(string? statusDoTorneio, DateTime? ultimoJogo, DateTime agora)
    {
        if (statusDoTorneio != StatusFinalizado) return false;

        var fecha = FechaEm(ultimoJogo);
        return fecha != null && agora < fecha.Value;
    }

    // Já dá pra mostrar o resultado? Só depois que fecha — ver `Apurar`.
    //
    // ⚠️ Desligar a votação some com o RESULTADO também, e não só com a cédula. Um torneio que
    // já elegeu MVP e depois teve a votação desligada volta a não ter MVP nenhum na tela: o
    // interruptor é sobre o torneio TER isso, não sobre "parar de receber voto". Os votos
    // continuam gravados — religar devolve o resultado inteiro, e nada é apagado.
    //
    // ⚠️ O FORMATO entra aqui também, e não só na `Aberta`: sem isso, um Americano que já
    // acabou apareceria com o RESULTADO da votação ("este torneio não elegeu um MVP") — uma
    // tela falando de uma eleição que nunca existiu naquele formato.
    public static bool Encerrada(bool usaVotacao, string? statusDoTorneio, DateTime? ultimoJogo,
        DateTime agora, string? formato) =>
        usaVotacao
        && ElegeMvp(formato)
        && statusDoTorneio == StatusFinalizado
        && FechaEm(ultimoJogo) is { } fecha
        && agora >= fecha;

    public const string StatusFinalizado = "Finalizado";

    // Quem ganhou. Devolve TODOS os empatados no topo, de propósito: inventar um critério de
    // desempate ("quem recebeu o voto primeiro") faria o sistema escolher um MVP por um motivo
    // que ninguém combinou. Dois MVPs é uma resposta honesta; um MVP arbitrário não é.
    //
    // ⚠️ A ordenação é TOTAL (votos → nome → id) pela razão de sempre: ordenação parcial faz a
    // lista trocar de ordem entre dois carregamentos da MESMA página, e ninguém reporta isso
    // como defeito — só desconfia da tela.
    public static List<CandidatoAMvp> Apurar(IEnumerable<CandidatoAMvp> candidatos)
    {
        var ordenados = candidatos
            .OrderByDescending(c => c.Votos)
            .ThenBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.JogadorId)
            .ToList();

        var topo = ordenados.FirstOrDefault();
        if (topo == null || topo.Votos < VotosMinimos) return new List<CandidatoAMvp>();

        return ordenados.Where(c => c.Votos == topo.Votos).ToList();
    }

    // ── O que vem do banco ────────────────────────────────────────────────────────────────

    // Monta a votação inteira de um torneio, do ponto de vista de quem está olhando.
    // `olhandoId` nulo = visitante deslogado (vê a tela, não vota).
    public static async Task<VotacaoDeMvp?> DoTorneioAsync(
        DbPadelContext contexto, int torneioId, int? olhandoId, DateTime agora)
    {
        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            // ⚠️ Só colunas do próprio torneio: navegação obrigatória em projeção vira INNER
            // JOIN e some com a linha inteira — foi o que fez o card de campeão sumir.
            .Select(t => new { t.Id, t.Nome, t.Status, t.UsaVotacaoDeMvp, t.Formato })
            .FirstOrDefaultAsync();

        if (torneio == null) return null;

        // A hora do último jogo. Mesma escada de datas do ranking: o jogo aconteceu quando
        // terminou; sem isso, cai pro início real e por fim pro previsto.
        var fins = await contexto.Partidas
            .AsNoTracking()
            .Where(p => p.TorneioId == torneioId && p.VencedorId != null)
            .Select(p => p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto)
            .ToListAsync();

        var ultimoJogo = UltimoJogo(fins);
        var aberta = Aberta(torneio.UsaVotacaoDeMvp, torneio.Status, ultimoJogo, agora, torneio.Formato);
        var encerrada = Encerrada(torneio.UsaVotacaoDeMvp, torneio.Status, ultimoJogo, agora, torneio.Formato);

        var candidatos = await CandidatosAsync(contexto, torneioId);
        var votos = await contexto.VotosDeMvp
            .AsNoTracking()
            .Where(v => v.TorneioId == torneioId)
            .Select(v => new { v.VotanteId, v.CandidatoId })
            .ToListAsync();

        var porCandidato = votos.GroupBy(v => v.CandidatoId).ToDictionary(g => g.Key, g => g.Count());

        // ⚠️ A CONTAGEM SÓ É PREENCHIDA DEPOIS QUE FECHA. Placar parcial em votação aberta
        // muda o voto de quem ainda não votou — vira efeito manada, e quem abriu a tela
        // primeiro decide a eleição pelos outros. Enquanto está aberta a tela mostra só
        // QUANTOS votaram, que é informação de participação, não de resultado.
        foreach (var candidato in candidatos)
        {
            candidato.Votos = encerrada ? porCandidato.GetValueOrDefault(candidato.JogadorId) : 0;
        }

        var eleitores = await EleitoresAsync(contexto, torneioId);

        return new VotacaoDeMvp
        {
            TorneioId = torneio.Id,
            Torneio = torneio.Nome,
            Aberta = aberta,
            Encerrada = encerrada,
            FechaEm = FechaEm(ultimoJogo),
            StatusDoTorneio = torneio.Status,
            UltimoJogo = ultimoJogo,
            Candidatos = candidatos,
            TotalDeVotos = votos.Count,
            Eleitores = eleitores.Count,
            EuId = olhandoId,
            SouEleitor = olhandoId != null && eleitores.Contains(olhandoId.Value),
            MeuVoto = olhandoId == null
                ? null
                : votos.FirstOrDefault(v => v.VotanteId == olhandoId.Value)?.CandidatoId,
            Vencedores = encerrada ? Apurar(candidatos) : new List<CandidatoAMvp>(),
        };
    }

    // A cédula: os campeões de cada categoria, como PESSOAS.
    //
    // ⚠️ Linha de TIME fica de fora. O `Jogador1Id` dela é o organizador que cadastrou o time,
    // e ele viraria candidato a melhor jogador de um torneio que talvez nem tenha jogado — a
    // mesma armadilha que já tirou a dupla-TIME de todo somatório de ranking do sistema.
    public static async Task<List<CandidatoAMvp>> CandidatosAsync(DbPadelContext contexto, int torneioId)
    {
        var campeas = await contexto.Duplas
            .AsNoTracking()
            .Where(d => d.Categoria.TorneioId == torneioId
                     && d.UltimaFase == CampeoesDoTorneio.FaseDeCampeao
                     && d.NomeTime == null)
            // Projeção CHATA e plana de propósito: um anônimo aninhado dentro de um ternário
            // (`J2 = d.Jogador2Id == null ? null : new { ... }`) é o tipo de expressão que o
            // provedor ou não traduz, ou traduz avaliando em memória depois de trazer a tabela.
            // Aqui cada coluna é uma coluna.
            .Select(d => new
            {
                Categoria = d.Categoria.Nome,
                J1Id = d.Jogador1Id,
                J1Nome = d.Jogador1.Nome,
                J1Apelido = d.Jogador1.Apelido,
                J1Foto = d.Jogador1.FotoPerfil,
                J2Id = d.Jogador2Id,
                J2Nome = d.Jogador2 == null ? null : d.Jogador2.Nome,
                J2Apelido = d.Jogador2 == null ? null : d.Jogador2.Apelido,
                J2Foto = d.Jogador2 == null ? null : d.Jogador2.FotoPerfil,
            })
            .ToListAsync();

        var candidatos = new Dictionary<int, CandidatoAMvp>();

        void Somar(int id, string nome, string? apelido, string? foto, string categoria)
        {
            // ⚠️ A MESMA PESSOA pode ser campeã em DUAS categorias do mesmo torneio (é comum:
            // joga a dela e a mista). Sem esta junção ela apareceria duas vezes na cédula, e os
            // votos dela se dividiriam entre as duas linhas — perdendo pra quem tem uma só.
            if (candidatos.TryGetValue(id, out var existente))
            {
                existente.Categorias.Add(categoria);
                return;
            }

            candidatos[id] = new CandidatoAMvp
            {
                JogadorId = id,
                Nome = NomeBonito.ComApelido(nome, apelido),
                Foto = foto,
                Categorias = new List<string> { categoria },
            };
        }

        foreach (var c in campeas)
        {
            Somar(c.J1Id, c.J1Nome, c.J1Apelido, c.J1Foto, c.Categoria);

            // Jogador2 nulo é o campeão do Americano, que é UMA pessoa (o parceiro troca a cada
            // rodada) — não é dupla incompleta.
            if (c.J2Id != null) Somar(c.J2Id.Value, c.J2Nome ?? "", c.J2Apelido, c.J2Foto, c.Categoria);
        }

        return candidatos.Values
            .OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.JogadorId)
            .ToList();
    }

    // Quem tem direito a votar: quem esteve NA CHAVE deste torneio.
    //
    // ⚠️ `ForaDoSorteio.EstaNaChave` e `NomeTime == null`, a mesma régua do ranking e do resumo
    // do perfil: quem ficou na lista de espera ou sem parceiro se inscreveu e não jogou. Deixar
    // essa gente votar transformaria a eleição num concurso de quem cadastra mais amigos.
    public static async Task<HashSet<int>> EleitoresAsync(DbPadelContext contexto, int torneioId)
    {
        var duplas = await contexto.Duplas
            .AsNoTracking()
            .Where(ForaDoSorteio.EstaNaChave)
            .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null)
            .Select(d => new { d.Jogador1Id, d.Jogador2Id })
            .ToListAsync();

        var ids = new HashSet<int>();
        foreach (var d in duplas)
        {
            ids.Add(d.Jogador1Id);
            if (d.Jogador2Id != null) ids.Add(d.Jogador2Id.Value);
        }
        return ids;
    }

    // Registra (ou troca) o voto. Devolve o motivo da recusa, ou null quando deu certo.
    //
    // ⚠️ TODA a validação acontece AQUI, e não na tela. A tela só esconde o botão; quem recusa é
    // o servidor — um POST montado à mão não passa por nenhuma view.
    public static async Task<string?> VotarAsync(
        DbPadelContext contexto, int torneioId, int votanteId, int candidatoId, DateTime agora)
    {
        var votacao = await DoTorneioAsync(contexto, torneioId, votanteId, agora);
        if (votacao == null) return "Torneio não encontrado.";

        if (!votacao.Aberta)
            return votacao.Encerrada
                ? "A votação do MVP deste torneio já encerrou."
                // Cobre os dois "ainda não": torneio em andamento e organizador que desligou a
                // votação. A frase é a mesma de propósito — quem vota não precisa saber qual
                // dos dois é, e dizer "o organizador desligou" convida a cobrança dele.
                : "A votação do MVP não está aberta neste torneio.";

        if (!votacao.SouEleitor)
            return "Só quem jogou este torneio vota no MVP.";

        if (!votacao.Candidatos.Any(c => c.JogadorId == candidatoId))
            return "Esse jogador não é campeão de nenhuma categoria deste torneio.";

        // ⚠️ Ninguém vota em si mesmo. Com o campeão votando nele próprio, a eleição vira uma
        // corrida de quem tem mais parceiros campeões — e o voto de quem só jogou vale menos.
        if (candidatoId == votanteId)
            return "Você não pode votar em você mesmo.";

        var existente = await contexto.VotosDeMvp
            .FirstOrDefaultAsync(v => v.TorneioId == torneioId && v.VotanteId == votanteId);

        if (existente != null)
        {
            // Trocar a escolha, nunca somar outra — mesma régua do Elogio. O índice único do
            // banco garante isso mesmo em dois cliques simultâneos.
            existente.CandidatoId = candidatoId;
            existente.CriadoEm = agora;
        }
        else
        {
            contexto.VotosDeMvp.Add(new VotoDeMvp
            {
                TorneioId = torneioId,
                VotanteId = votanteId,
                CandidatoId = candidatoId,
                CriadoEm = agora,
            });
        }

        await contexto.SaveChangesAsync();
        return null;
    }

    // Em quantos torneios este jogador FOI ELEITO o MVP. Serve à conquista do perfil.
    //
    // A regra não mora aqui: quem decide "eleito" é o mesmo par de sempre — `Encerrada` (a
    // janela fechou?) e `Apurar` (empate proclama todos, mínimo de votos). Esta função só
    // olha menos dados que a `DoTorneioAsync`: parte dos torneios onde ele RECEBEU voto (que
    // são poucos), e apura só com quem tem voto — candidato com zero voto nunca muda quem
    // ganha, porque o topo precisa de pelo menos `VotosMinimos`.
    public static async Task<int> VezesEleitoMvpAsync(DbPadelContext contexto, int jogadorId, DateTime agora)
    {
        var torneiosComVotoNele = await contexto.VotosDeMvp
            .AsNoTracking()
            .Where(v => v.CandidatoId == jogadorId)
            .Select(v => v.TorneioId)
            .Distinct()
            .ToListAsync();

        int vezes = 0;
        foreach (var torneioId in torneiosComVotoNele)
        {
            var torneio = await contexto.Torneios
                .AsNoTracking()
                .Where(t => t.Id == torneioId)
                .Select(t => new { t.Status, t.UsaVotacaoDeMvp, t.Formato })
                .FirstOrDefaultAsync();
            if (torneio == null) continue;

            var fins = await contexto.Partidas
                .AsNoTracking()
                .Where(p => p.TorneioId == torneioId && p.VencedorId != null)
                .Select(p => p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto)
                .ToListAsync();

            // Votação ainda aberta (ou desligada) não tem eleito — a mesma razão de a tela
            // não mostrar parcial: MVP só existe depois que fecha.
            if (!Encerrada(torneio.UsaVotacaoDeMvp, torneio.Status, UltimoJogo(fins), agora, torneio.Formato))
                continue;

            var votos = await contexto.VotosDeMvp
                .AsNoTracking()
                .Where(v => v.TorneioId == torneioId)
                .GroupBy(v => v.CandidatoId)
                .Select(g => new { CandidatoId = g.Key, Votos = g.Count() })
                .ToListAsync();

            var eleitos = Apurar(votos.Select(v => new CandidatoAMvp
            {
                JogadorId = v.CandidatoId,
                Votos = v.Votos,
            }));

            if (eleitos.Any(e => e.JogadorId == jogadorId)) vezes++;
        }

        return vezes;
    }

    // ─────────────────────── O AVISO DE "VOTE NO MVP" ───────────────────────

    public const string TituloDoAviso = "Vote no MVP do torneio";

    // O convite leva a enquete junto: é o mesmo aviso que traz a pessoa pra página onde as
    // duas coisas moram, e um segundo push só pra nota seria ruído.
    //
    // ⚠️ `temMvp` NÃO é enfeite: no Americano não há eleição (ver ElegeMvp), e prometer
    // "escolha o melhor jogador" levaria a pessoa a uma tela que não tem cédula nenhuma —
    // o jeito mais rápido de ensinar que o nosso aviso mente.
    public static string CorpoDoAviso(string nomeDoTorneio, bool temMvp = true) =>
        temMvp
            ? $"O {nomeDoTorneio} acabou! Escolha o melhor jogador entre os campeões e conte "
              + $"como foi o torneio — vale por {DiasParaVotar} dias."
            : $"O {nomeDoTorneio} acabou! Conte como foi: dê sua nota pro clube e pra "
              + $"organização — leva cinco segundos e vale por {DiasParaVotar} dias.";

    // O título acompanha o corpo, pelo mesmo motivo.
    public static string TituloDoAvisoPara(bool temMvp) =>
        temMvp ? TituloDoAviso : "Como foi o torneio?";

    // O canal do aviso. Notificação do app e caixa de entrada; **sem e-mail**.
    //
    // ⚠️ A pergunta do `AlcanceDoAviso` é "a pessoa faz alguma coisa por causa disto?" — e aqui
    // faz (vota). Mesmo assim o e-mail fica de fora, por causa do TAMANHO: este aviso sai pra
    // TODO MUNDO que jogou, de uma vez. Um torneio real tem 45 a 110 duplas, ou seja 90 a 220
    // e-mails num único minuto — a rajada exata que já queimou a cota duas vezes e levou junto
    // duas recuperações de senha. Perder um voto de MVP não custa nada a ninguém; perder o
    // "esqueci minha senha" custa a conta.
    //
    // ⚠️ E WhatsApp está fora de questão: não é pessoal (é o mesmo recado pra 200 pessoas), que
    // é precisamente o que a Meta chama de spam e o que restringiu o número em 04/08.
    public const AlcanceDoAviso CanalDoAviso = AlcanceDoAviso.AppSemEmail;

    // A madrugada de verdade, e SÓ ela.
    //
    // ⚠️ De propósito NÃO se usa a janela civilizada do lembrete de pagamento (9h–21h): torneio
    // de padel terminando às 22h ou 23h é o normal, e segurar o aviso até as 9h da manhã perde
    // exatamente o momento em que ele vale — todo mundo ainda no clube, falando do torneio que
    // acabou de acabar. O que esta janela protege é o outro caso: um admin arrumando dado às 3h
    // e finalizando um torneio antigo não acorda 200 pessoas.
    public const int PrimeiraHoraDoAviso = 7;
    public const int UltimaHoraDoAviso = 1;   // exclusivo: 1h da manhã já é tarde demais

    public static bool HoraDeAvisar(DateTime agora) =>
        agora.Hour >= PrimeiraHoraDoAviso || agora.Hour < UltimaHoraDoAviso;

    // Esta tela deve aparecer na página do torneio?
    //
    // Aparece enquanto a votação está aberta E depois que encerra (pra mostrar o eleito). O que
    // não existe é a tela de um torneio que nunca chegou a ter votação — ela abriria dizendo
    // apenas que não há nada, que é ruído na página de todo torneio em andamento.
    public static bool TemVotacao(bool usaVotacao, string? statusDoTorneio, DateTime? ultimoJogo,
        DateTime agora, string? formato) =>
        Aberta(usaVotacao, statusDoTorneio, ultimoJogo, agora, formato)
        || Encerrada(usaVotacao, statusDoTorneio, ultimoJogo, agora, formato);
}

// Um nome na cédula.
public sealed class CandidatoAMvp
{
    public int JogadorId { get; set; }
    public string Nome { get; set; } = "";
    public string? Foto { get; set; }

    // Em quantas categorias essa pessoa foi campeã neste torneio — quase sempre uma.
    public List<string> Categorias { get; set; } = new();

    // ⚠️ Zero enquanto a votação está aberta, de propósito (ver DoTorneioAsync).
    public int Votos { get; set; }
}

public sealed class VotacaoDeMvp
{
    public int TorneioId { get; set; }
    public string Torneio { get; set; } = "";

    public bool Aberta { get; set; }
    public bool Encerrada { get; set; }
    public DateTime? FechaEm { get; set; }

    public List<CandidatoAMvp> Candidatos { get; set; } = new();

    public int TotalDeVotos { get; set; }
    public int Eleitores { get; set; }

    public bool SouEleitor { get; set; }
    public int? MeuVoto { get; set; }

    // Quem está olhando. Fica aqui pra a VIEW não precisar ler claim nenhuma — tela que
    // interpreta credencial é tela que decide permissão, e quem decide isso é o serviço.
    public int? EuId { get; set; }

    // Vazio enquanto aberta, e vazio depois de encerrada quando ninguém alcançou o mínimo.
    public List<CandidatoAMvp> Vencedores { get; set; } = new();

    public bool TemVencedor => Vencedores.Count > 0;
    public bool Empatou => Vencedores.Count > 1;

    // O estado do torneio, pra quem precisa responder outra pergunta sobre a MESMA janela —
    // hoje a enquete do clube, que vale até onde o MVP não existe (Americano). Sem isto o
    // controller teria que ir ao banco de novo pelas mesmas duas informações.
    public string? StatusDoTorneio { get; set; }
    public DateTime? UltimoJogo { get; set; }

    // Este torneio TEM eleição de MVP? Falso no Americano e em quem desligou o interruptor —
    // e é o que faz a tela decidir entre "MVP do torneio" e só a enquete.
    public bool TemVotacao => Aberta || Encerrada;
}
