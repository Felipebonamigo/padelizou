using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O ROBÔ QUE MONTA A CHAVE — um só, para as duas telas que finalizam partida.
//
// ⚠️ Isto aqui existe por causa do Interno de 05/08/2026. Havia DUAS implementações do mesmo
// robô, uma em cada controller, e QUAL DELAS RODAVA DEPENDIA DA TELA que o organizador usou
// pra encerrar o jogo:
//
//   • encerrou pela Mesa de Controle ou pelo card da lista  → TorneiosController.Placar
//   • encerrou pela tela cheia do Controle de Placar        → PartidasController
//
// As duas montavam o mesmo confronto (o pareamento sempre saiu de ChaveamentoMataMata), mas
// a cópia do PartidasController AGENDAVA NA MÃO: `HorarioPrevisto = DateTime.Now.AddHours(2)`
// pra todos os jogos da rodada, sem quadra e sem conferir se alguém já estava marcado naquele
// horário. Ou seja: a fase seguinte nascia com a rodada inteira no mesmo minuto, "quadra a
// definir", e a mesma pessoa podia ser chamada pra dois jogos — dependendo apenas de por onde
// o placar tinha sido lançado. É o tipo de defeito que nunca aparece testando: as duas telas
// funcionam, só não funcionam igual.
//
// A regra deste arquivo é: quem quiser criar fase de mata-mata passa por aqui. Duas cópias de
// uma regra de chaveamento elegem dois campeões diferentes no primeiro caso que divergir — e
// no Interno divergiram.
public class RoboDoChaveamento
{
    private readonly DbPadelContext _context;

    public RoboDoChaveamento(DbPadelContext context) => _context = context;

    // ===================================================================================
    // ROBÔ 1: FIM DA FASE DE GRUPOS → primeira rodada do mata-mata
    // ===================================================================================
    public async Task MontarMataMataDosGruposAsync(int categoriaId, int? torneioId)
    {
        var categoria = await _context.Categorias
            .Include(c => c.GruposTorneio)
                .ThenInclude(g => g.Duplas)
            .FirstOrDefaultAsync(c => c.Id == categoriaId);

        if (categoria == null) return;

        // ⚠️ A FASE DE GRUPOS PRECISA TER ACABADO — e quem confere é o robô, não quem chama.
        //
        // A classificação (ClassificacaoDeGrupos) responde com o que tem: chamada no meio da
        // fase de grupos ela devolve um pódio provisório e o mata-mata nasce dali, montado
        // sobre jogos que ainda nem aconteceram. Enquanto esta guarda ficou no CHAMADOR, uma
        // das duas telas a tinha e a outra podia não ter — que é o mesmo defeito que este
        // arquivo existe pra fechar.
        bool aindaTemJogoDeGrupo = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId
            && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
            && p.Status != "Finalizada");
        if (aindaTemJogoDeGrupo) return;

        var partidasFinalizadas = await _context.Partidas
            .Where(p => p.CategoriaId == categoriaId
                     && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
                     && p.Status == "Finalizada")
            .ToListAsync();

        // Evita gerar a chave duas vezes (ex: dois finalizamentos quase simultâneos).
        bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));
        if (mataMataJaGerado) return;

        var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();

        // Quantos passam de cada grupo: 2 é a regra de sempre; a categoria de TIMES usa o
        // número que o organizador definiu ao criá-la.
        int classificamPorGrupo = Math.Max(1, categoria.ClassificadosPorGrupo ?? 2);

        // 1. O ranking final de cada grupo, pela régua única (Services/ClassificacaoDeGrupos)
        //    — a mesma que a tela de classificação e a detecção de bye usam.
        var duplasDosGrupos = grupos.SelectMany(g => g.Duplas).ToList();
        var classificados = ClassificacaoDeGrupos.Calcular(
            duplasDosGrupos, partidasFinalizadas, classificamPorGrupo);

        // 2. Motor único de chaveamento: TODO classificado avança; o quadro cresce pra caber
        //    todo mundo e os MELHORES pegam bye (pulam a primeira rodada). Os byes não ganham
        //    partida aqui — é a ausência dela que o robô de avanço lê depois
        //    (Services/AvancoDaChave) pra somá-los aos vencedores.
        var (nomeFase, confrontos, _) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, classificamPorGrupo);
        if (confrontos.Count == 0) return;

        var jogosDoMataMata = confrontos
            .Select(confronto => new Partida
            {
                TorneioId = torneioId,
                CategoriaId = categoriaId,
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                Status = "Agendada", // Nasce agendada para ir para a Mesa de Controle!
                Fase = nomeFase,
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() // NOT NULL no banco
            })
            .ToList();

        // Nasce agendada E com hora: o mata-mata emenda no fim da fase de grupos.
        await AgendarNaGradeAsync(jogosDoMataMata, torneioId);

        _context.Partidas.AddRange(jogosDoMataMata);
        await _context.SaveChangesAsync();
    }

    // ===================================================================================
    // ROBÔ 2: PROGRESSÃO — Primeira Rodada → Oitavas → Quartas → Semifinal → Final
    // ===================================================================================
    public async Task AvancarFaseAsync(int categoriaId, int? torneioId, string faseConcluida)
    {
        // Fase que não encadeia (grupos, Americano, Final) para aqui.
        if (ChaveamentoMataMata.ProximaFase(faseConcluida) == null) return;

        // Vencedores da fase + quem passou direto (bye), com a fase completa conferida lá
        // dentro. Vazio = ainda tem jogo pendente. Ver Services/AvancoDaChave.
        var avancam = await AvancoDaChave.QuemAvancaAsync(_context, categoriaId, faseConcluida);
        if (avancam.Count < 2) return;

        // Com bye o quadro encolhe mais devagar: a primeira rodada de uma chave de 24 entrega
        // 16 (8 vencedores + 8 byes), que são Oitavas — e não as Quartas que o encadeamento
        // por NOME sugeriria. Quem manda é quanta gente sobrou.
        var proximaFase = ChaveamentoMataMata.NomeFase(avancam.Count);

        // Nunca gera a próxima fase em duplicidade (dois finalizamentos quase simultâneos).
        if (await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase)) return;

        var novos = ChaveamentoMataMata.ParearVencedores(avancam)
            // Codigo é obrigatório no banco (NOT NULL) — sem ele o INSERT do robô falha.
            .Select(confronto => new Partida
            {
                TorneioId = torneioId,
                CategoriaId = categoriaId,
                Fase = proximaFase,
                Status = "Agendada",
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
            })
            .ToList();

        await AgendarNaGradeAsync(novos, torneioId);

        _context.Partidas.AddRange(novos);
        await _context.SaveChangesAsync();
    }

    // ===================================================================================
    // ROBÔ 3: TORNEIO AMERICANO → a final, quando todas as rodadas acabam
    // ===================================================================================
    //
    // ⚠️ Este robô morava SÓ no PartidasController, o que quer dizer que um Americano
    // encerrado pela Mesa de Controle — a tela do dia de torneio — terminava as rodadas e
    // ficava parado, esperando uma final que ninguém ia criar.
    public async Task MontarFinalDoAmericanoAsync(int categoriaId, int? torneioId)
    {
        if (torneioId == null) return;

        bool temRodadaPendente = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano") && p.Status != "Finalizada");
        if (temRodadaPendente) return;

        bool finalJaGerada = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId && p.Fase == "Final");
        if (finalJaGerada) return;

        var partidas = await _context.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano"))
            .ToListAsync();

        if (partidas.Count == 0) return;

        // No Americano o placar é individual: cada jogador leva os games da dupla dele.
        var pontos = new Dictionary<int, int>();
        void Somar(int jogadorId, int games) => pontos[jogadorId] = pontos.GetValueOrDefault(jogadorId) + games;
        foreach (var p in partidas)
        {
            // As duplas do americano são sorteadas pelo sistema, então Jogador2Id nunca é
            // nulo aqui — mas a checagem evita quebrar se algum dado vier torto.
            Somar(p.Dupla1.Jogador1Id, p.GamesDupla1 ?? 0);
            if (p.Dupla1.Jogador2Id != null) Somar(p.Dupla1.Jogador2Id.Value, p.GamesDupla1 ?? 0);
            Somar(p.Dupla2.Jogador1Id, p.GamesDupla2 ?? 0);
            if (p.Dupla2.Jogador2Id != null) Somar(p.Dupla2.Jogador2Id.Value, p.GamesDupla2 ?? 0);
        }

        // Desempate por Id no fim pelo mesmo motivo da classificação de grupos: sem ordem
        // TOTAL, quem entra no top 4 passa a depender da ordem em que o dicionário devolveu.
        var top4 = pontos
            .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Take(4).Select(kv => kv.Key).ToList();
        if (top4.Count < 4) return; // não tem gente suficiente pra formar a final

        // Cruzamento: 1º + 4º × 2º + 3º.
        var duplaFinal1 = new Dupla { CategoriaId = categoriaId, Jogador1Id = top4[0], Jogador2Id = top4[3] };
        var duplaFinal2 = new Dupla { CategoriaId = categoriaId, Jogador1Id = top4[1], Jogador2Id = top4[2] };
        _context.Duplas.Add(duplaFinal1);
        _context.Duplas.Add(duplaFinal2);
        await _context.SaveChangesAsync();

        var final = new Partida
        {
            TorneioId = torneioId,
            CategoriaId = categoriaId,
            Dupla1Id = duplaFinal1.Id,
            Dupla2Id = duplaFinal2.Id,
            Fase = "Final",
            Status = "Agendada",
            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
        };

        // ⚠️ Entra na GRADE como qualquer outra fase. A versão antiga cravava
        // `DateTime.Now.AddHours(2)`: a final do Americano nascia num horário inventado, sem
        // quadra e podendo cair em cima de outro jogo.
        await AgendarNaGradeAsync(new List<Partida> { final }, torneioId);

        _context.Partidas.Add(final);
        await _context.SaveChangesAsync();
    }

    // ===================================================================================
    // A GRADE
    // ===================================================================================

    // TODO jogo do torneio nasce com horário previsto — inclusive os do mata-mata, que só
    // existem depois que a fase de grupos acaba. Sem isso o jogador via "a definir" na fase
    // que mais importa, e a Mesa de Controle não tinha ordem nenhuma pra seguir.
    //
    // A rodada nova abre uma rodada depois do fim da fase que a alimenta — a da PRÓPRIA
    // categoria — e ocupa as quadras que estiverem livres dali em diante.
    //
    // ⚠️ Antes o âncora era o último jogo marcado do TORNEIO INTEIRO, e isso enfileirava as
    // categorias uma atrás da outra: com 5 quadras e 5 categorias, cada semifinal esperava a
    // semifinal alheia acabar e quatro quadras ficavam paradas. Era o preço de o encaixe não
    // saber o que já estava marcado — agora ele sabe (`jaMarcados`), então dá pra emendar em
    // paralelo sem chamar ninguém pra duas quadras ao mesmo tempo.
    public async Task AgendarNaGradeAsync(List<Partida> jogos, int? torneioId)
    {
        if (jogos.Count == 0 || torneioId == null) return;

        var torneio = await _context.Torneios.FindAsync(torneioId.Value);

        // Torneio apagado enquanto os jogos rodavam: sem ele não há expediente nem tempo de
        // partida pra montar horário, e insistir estoura DENTRO do salvamento do placar — a
        // Mesa de Controle daria erro no meio do torneio por causa de outro torneio.
        if (torneio == null) return;

        // Torneio por ordem de liberação não tem grade: o mata-mata entra na fila como todo o
        // resto, sem hora. Ver Torneio.SemHorarioPrevisto.
        if (torneio.SemHorarioPrevisto) return;

        var jaMarcados = await _context.Partidas
            .Where(p => p.TorneioId == torneioId && p.HorarioPrevisto != null)
            .ToListAsync();

        // A fase que alimenta esta é a da MESMA categoria: é dela que sai quem vai jogar, e é
        // dela que a folga tem que partir. Uma rodada de folga, não o minuto em que o último
        // jogo acaba — quem joga a semifinal das 22h é quem disputa a final, e colar uma fase
        // na outra é chamar a mesma dupla de volta sem descanso.
        //
        // Vira o dia na abertura dos DIAS SEGUINTES: o mata-mata quase sempre cai no domingo,
        // que começa cedo — não às 18h da sexta em que o torneio abriu.
        int categoriaId = jogos[0].CategoriaId;
        var fimDaFaseAnterior = jaMarcados
            .Where(p => p.CategoriaId == categoriaId)
            .Select(p => p.HorarioPrevisto!.Value)
            .DefaultIfEmpty()
            .Max();

        var inicio = fimDaFaseAnterior == default
            ? torneio.AberturaDaGrade
            : GradeDeJogos.AberturaDaProximaFase(fimDaFaseAnterior, torneio.HoraFimDoDia,
                                    torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos);

        int folga = GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras);

        // Vaga que já tem dono sai da lista, senão a grade ofereceria cinco quadras num
        // horário em que três já estão jogando. Pede-se com sobra (`+ jaMarcados.Count`)
        // justamente porque parte vai ser descontada.
        var horarios = GradeDeJogos.Descontando(
                GradeDeJogos.Horarios(
                    inicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                    torneio.TempoPrevistoPartidaMinutos,
                    jogos.Count + folga + jaMarcados.Count,
                    aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes),
                jaMarcados.Select(p => p.HorarioPrevisto!.Value))
            .Take(jogos.Count + folga)
            .ToList();

        // Encaixe ciente de conflito: semifinais de chaves diferentes podem dividir o horário,
        // mas a mesma PESSOA nunca joga em duas quadras ao mesmo tempo — vale pra quem chegou
        // longe na categoria dele e na chave direta ao mesmo tempo.
        GradeDeJogos.Encaixar(jogos, horarios, await OcupantesPorDuplaAsync(torneioId.Value),
            await QuadrasEmUsoAsync(torneioId.Value), jaMarcados);
    }

    // Os nomes das quadras do torneio, na ordem — é o que transforma "a definir" em "Quadra C"
    // na tela do jogador. Torneio que não cadastrou quadra devolve lista vazia, e a grade segue
    // sem nomear (ver GradeDeJogos.Encaixar).
    public async Task<List<string>> QuadrasDoTorneioAsync(int torneioId) =>
        await _context.Quadras
            .Where(q => q.TorneioId == torneioId)
            .OrderBy(q => q.Nome)
            .Select(q => q.Nome)
            .ToListAsync();

    // As quadras que o torneio está DE FATO usando: as que já estão escritas nos jogos
    // marcados, completadas pelo cadastro quando faltam nomes pra encher a grade.
    // A regra e o porquê estão em Services/NomesDeQuadra.
    public async Task<List<string>> QuadrasEmUsoAsync(int torneioId)
    {
        var nosJogos = await _context.Partidas
            .Where(p => p.TorneioId == torneioId && p.NomeQuadra != null && p.NomeQuadra != "")
            .Select(p => p.NomeQuadra!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var quantidade = await _context.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => t.QuantidadeQuadras)
            .FirstOrDefaultAsync();

        return NomesDeQuadra.Disponiveis(nosJogos, await QuadrasDoTorneioAsync(torneioId), quantidade);
    }

    public async Task<Dictionary<int, int[]>> OcupantesPorDuplaAsync(int torneioId) =>
        OcupantesPorDupla(await _context.Duplas
            .Where(d => d.Categoria.TorneioId == torneioId)
            .ToListAsync());

    // Quem de fato ocupa a quadra quando cada dupla joga: as DUAS pessoas dela.
    //
    // Sem isto a grade compara duplas, e a mesma pessoa inscrita na categoria dela E numa
    // chave direta paralela seria marcada em duas quadras no mesmo horário — duas duplas de
    // Ids diferentes, o mesmo sujeito.
    //
    // Time fica FORA do mapa de propósito (cai no Id da dupla, como sempre foi): lá o
    // Jogador1Id é o organizador em todos os times, e comparar por pessoa faria todo time
    // conflitar com todo time, empurrando a grade inteira pra frente.
    public static Dictionary<int, int[]> OcupantesPorDupla(IEnumerable<Dupla> duplas) =>
        duplas
            .Where(d => !d.EhTime && d.Jogador2Id != null)
            .ToDictionary(d => d.Id, d => new[] { d.Jogador1Id, d.Jogador2Id!.Value });

    public static Dictionary<int, int[]> OcupantesPorDupla(Torneio torneio) =>
        OcupantesPorDupla(torneio.Categorias.SelectMany(c => c.Duplas));
}
