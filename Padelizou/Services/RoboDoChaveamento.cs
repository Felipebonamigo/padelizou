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
    // ROBÔ 3: TORNEIO AMERICANO → o desfecho, quando todas as rodadas acabam
    // ===================================================================================
    //
    // ⚠️ Este robô morava SÓ no PartidasController, o que quer dizer que um Americano
    // encerrado pela Mesa de Controle — a tela do dia de torneio — terminava as rodadas e
    // ficava parado, esperando uma final que ninguém ia criar.
    //
    // ⚠️ ATÉ 06/08/2026 ELE MONTAVA UMA FINAL SEMPRE, cruzando os 4 primeiros (1º+4º × 2º+3º),
    // mesmo sem empate nenhum e mesmo com a opção de desempate desligada. Isso dava ao torneio
    // DOIS campeões diferentes: a tela de Classificação (que soma só as rodadas) coroava o
    // líder em games, e a conquista do perfil coroava quem vencesse a tal Final. No ensaio de
    // 8 jogadores, a líder com 56 games ficou sem título e o 2º colocado levou.
    //
    // A regra agora é a do formato: vence quem fez mais games, e só há partida extra se DOIS
    // OU MAIS empatarem na liderança (ver Services/FimDoAmericano).
    public async Task MontarFinalDoAmericanoAsync(int categoriaId, int? torneioId)
    {
        if (torneioId == null) return;

        bool temRodadaPendente = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano") && p.Status != "Finalizada");
        if (temRodadaPendente) return;

        // Desfecho já resolvido? Vale tanto a Final antiga (torneios criados antes desta
        // correção, que podem tê-la agendada) quanto o desempate novo.
        bool jaTemDesfecho = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId
            && (p.Fase == "Final" || p.Fase == TabelaDoAmericano.FaseDesempate));
        if (jaTemDesfecho) return;

        // Campeão já carimbado: sem isto, reabrir e refinalizar a última rodada carimbaria
        // outro (ver DesfazerDoJogo — reabrir apaga as fases posteriores, não este carimbo).
        bool jaTemCampeao = await _context.Duplas.AnyAsync(d =>
            d.CategoriaId == categoriaId && d.UltimaFase == "Campeao");
        if (jaTemCampeao) return;

        var partidas = await _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano"))
            .ToListAsync();

        if (partidas.Count == 0) return;

        var torneio = await _context.Torneios.FindAsync(torneioId.Value);
        var categoria = await _context.Categorias.FindAsync(categoriaId);

        // AMERICANO DE DUPLAS: a dupla é fixa, então quem se coroa (ou desempata) é a DUPLA.
        // O caminho individual abaixo não serve — a classificação dele é por pessoa, e o
        // carimbo dele é numa linha solo.
        if (torneio?.Formato == "AmericanoDuplas")
        {
            await FecharAmericanoDeDuplasAsync(categoriaId, torneio, partidas);
            return;
        }

        // Torneio dividido em grupos e o GRUPO FINAL ainda não existe? Então acabou foi a fase
        // de grupos: monta o grupo final com os primeiros de cada, e o título se decide lá.
        //
        // A ordem importa: enquanto o grupo final não terminar, ninguém é coroado.
        bool dividido = (categoria?.GruposAmericano ?? 1) > 1;
        bool grupoFinalExiste = partidas.Any(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase));

        if (dividido && !grupoFinalExiste)
        {
            await MontarGrupoFinalDoAmericanoAsync(categoria!, torneioId.Value, partidas);
            return;
        }

        // Quem decide o título: o grupo final quando existe, senão as rodadas do grupo único.
        var queDecidem = grupoFinalExiste
            ? partidas.Where(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase))
            : partidas.Where(p => FaseDoAmericano.EhDaFaseDeGrupos(p.Fase));

        var classificacao = TabelaDoAmericano.Montar(queDecidem.Where(p => p.Status == "Finalizada"));
        var decisao = FimDoAmericano.Decidir(classificacao, torneio?.DesempateAmericano ?? false);

        if (decisao.Tipo == FimDoAmericano.Desfecho.CampeaoDireto && decisao.Campeao != null)
        {
            await CoroarNoAmericanoAsync(categoriaId, decisao.Campeao.Id, torneio);
            return;
        }

        // Empate que uma partida não resolve (3+), ou torneio que não previu desempate: o
        // sistema NÃO inventa critério nem inventa campeão. As rodadas acabaram, então o
        // torneio encerra — o título fica com o organizador.
        if (decisao.Tipo == FimDoAmericano.Desfecho.OrganizadorDecide)
        {
            if (torneio != null) torneio.Status = "Finalizado";
            await _context.SaveChangesAsync();
            return;
        }

        // Sobrou o empate de DOIS num torneio que previu desempate. Quem monta a partida é o
        // organizador, na tela dele (TorneiosController.DesempateAmericano): ele precisa
        // escolher o parceiro de cada empatado, e isso o robô não tem como adivinhar.
        return;
    }

    // Fase de grupos encerrada num Americano dividido: os primeiros de CADA grupo formam o
    // grupo final, que é outro Americano — e é lá que o título se decide.
    //
    // ⚠️ Quantos passam saiu de `Categoria.PassamPorGrupo`, gravado no sorteio, e NÃO é
    // recalculado aqui. Recalcular abriria a porta pro número mudar entre o que foi anunciado
    // ao organizador e o que acontece — e ele já contou pros jogadores quantos passam.
    //
    // ⚠️ A classificação de cada grupo é montada com as partidas DAQUELE grupo. Somar o
    // torneio inteiro misturaria gente que nunca se enfrentou.
    private async Task MontarGrupoFinalDoAmericanoAsync(
        Categoria categoria, int torneioId, List<Partida> partidas)
    {
        int passam = categoria.PassamPorGrupo;
        if (passam < 1) return;   // divisão sem classificação não monta grupo final

        var grupos = partidas
            .Where(p => FaseDoAmericano.EhDaFaseDeGrupos(p.Fase))
            .Select(p => FaseDoAmericano.GrupoDe(p.Fase))
            .Where(g => g != null)
            .Distinct()
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToList();

        var classificados = new List<int>();
        foreach (var grupo in grupos)
        {
            var doGrupo = partidas.Where(p => FaseDoAmericano.EhDoGrupo(p.Fase, grupo)
                                              && p.Status == "Finalizada");
            var tabela = TabelaDoAmericano.Montar(doGrupo);

            // Empate na fronteira do corte fica com o critério estável da tabela (games e,
            // em seguida, o Id). Um sorteio ESTÁVEL vale mais que um que muda entre duas telas
            // — é a mesma lição do chaveamento por grupos.
            classificados.AddRange(tabela.Take(passam).Select(l => l.Jogador.Id));
        }

        // Sem gente suficiente (grupo que não terminou, dado torto), não se monta meia final.
        if (classificados.Count < 4) return;

        var rodadas = RodadasAmericano.Montar(classificados);
        if (rodadas.Count == 0) return;

        var novas = new List<Partida>();
        for (int rodada = 1; rodada <= rodadas.Count; rodada++)
        {
            foreach (var confronto in rodadas[rodada - 1])
            {
                var d1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.A1, Jogador2Id = confronto.A2 };
                var d2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.B1, Jogador2Id = confronto.B2 };
                _context.Duplas.AddRange(d1, d2);
                await _context.SaveChangesAsync();   // precisa dos Ids antes da Partida

                novas.Add(new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoria.Id,
                    Dupla1Id = d1.Id,
                    Dupla2Id = d2.Id,
                    Fase = FaseDoAmericano.RodadaDoGrupoFinal(rodada),
                    Status = "Agendada",
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                });
            }
        }

        // Entra na GRADE como qualquer outra fase: horário e quadra saem das mesmas regras do
        // resto do torneio, em vez de um horário inventado.
        await AgendarNaGradeAsync(novas, torneioId);

        _context.Partidas.AddRange(novas);
        await _context.SaveChangesAsync();
    }

    // Fim do AMERICANO DE DUPLAS: vence a dupla que somou mais games. Só há partida extra se
    // DUAS empatarem na liderança num torneio que previu desempate — e aqui o robô cria essa
    // partida sozinho, porque as duas duplas já existem (no individual quem monta é o
    // organizador: cada empatado ainda precisa escolher um parceiro).
    private async Task FecharAmericanoDeDuplasAsync(int categoriaId, Torneio torneio, List<Partida> partidas)
    {
        var classificacao = TabelaDoAmericanoDeDuplas.Montar(partidas.Where(p => p.Status == "Finalizada"));
        if (classificacao.Count == 0) return;

        var empatadas = TabelaDoAmericanoDeDuplas.EmpatadasNaLideranca(classificacao);

        // Líder isolada é o caso normal. O carimbo vai na dupla DE VERDADE — o título é dos
        // dois, igual ao campeão do mata-mata (e diferente do individual, que coroa uma linha
        // sem parceiro).
        if (empatadas.Count < 2)
        {
            classificacao[0].Dupla.UltimaFase = "Campeao";
            torneio.Status = "Finalizado";
            await _context.SaveChangesAsync();
            return;
        }

        // Se o empate PODE virar partida quem diz é a régua que já existia
        // (TabelaDoAmericano.ProblemaParaDesempatar) — `rodadasPendentes: 0` porque este
        // ponto só é alcançado com tudo jogado.
        var problema = TabelaDoAmericano.ProblemaParaDesempatar(
            torneio.DesempateAmericano, rodadasPendentes: 0, quantosEmpatados: empatadas.Count);

        // Empate de 3+ ou desempate não previsto: o sistema não inventa critério nem campeão.
        // As rodadas acabaram, então o torneio encerra — o título fica com o organizador.
        if (problema != null)
        {
            torneio.Status = "Finalizado";
            await _context.SaveChangesAsync();
            return;
        }

        var desempate = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoriaId,
            Dupla1Id = empatadas[0].Id,
            Dupla2Id = empatadas[1].Id,
            Fase = TabelaDoAmericano.FaseDesempate,
            Status = "Agendada",
            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
        };

        await AgendarNaGradeAsync(new List<Partida> { desempate }, torneio.Id);
        _context.Partidas.Add(desempate);
        await _context.SaveChangesAsync();
    }

    // No Americano o campeão é UMA PESSOA, não uma dupla — o parceiro muda a cada rodada.
    //
    // O carimbo continua sendo `Dupla.UltimaFase = "Campeao"`, que é o que o perfil e as
    // estatísticas já leem, mas numa linha SEM parceiro: carimbar uma das duplas de rodada
    // daria o título também a quem calhou de jogar junto naquele jogo. `EstatisticasService`
    // percorre `{ Jogador1, Jogador2 }` pulando nulo, então uma linha solo coroa exatamente
    // uma pessoa. A tela de Inscritos do Americano lê `InscricaoAmericana` e não Duplas, então
    // esta linha não aparece como inscrição fantasma.
    // Público porque o ORGANIZADOR também coroa: quando o empate no título é de 3 ou mais,
    // uma partida não resolve e ele decide na tela (TorneiosController.CoroarCampeaoAmericano).
    // Duplicar o carimbo lá seria a segunda cópia da regra que decide campeão — e este projeto
    // já tem a cicatriz de ter feito isso com "quem venceu".
    public async Task CoroarNoAmericanoAsync(int categoriaId, int jogadorId, Torneio? torneio)
    {
        _context.Duplas.Add(new Dupla
        {
            CategoriaId = categoriaId,
            Jogador1Id = jogadorId,
            Jogador2Id = null,
            UltimaFase = "Campeao",
        });

        if (torneio != null) torneio.Status = "Finalizado";
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
                                    torneio.HoraInicioDiasSeguintes, VagasDaGrade.Duracao(torneio));

        // As vagas livres da grade, já descontando os jogos que têm dono. A receita mora em
        // Services/VagasDaGrade — eram três cópias com contas diferentes até 21/08/2026.
        var horarios = VagasDaGrade.Montar(torneio, inicio, jogos.Count, jaMarcados);

        // Encaixe ciente de conflito: semifinais de chaves diferentes podem dividir o horário,
        // mas a mesma PESSOA nunca joga em duas quadras ao mesmo tempo — vale pra quem chegou
        // longe na categoria dele e na chave direta ao mesmo tempo.
        //
        // A preferência de quadra entra aqui também, e é justamente aqui que ela mais importa:
        // as fases que este método agenda são a semi e a FINAL, que é o jogo que o organizador
        // quer na quadra boa.
        GradeDeJogos.Encaixar(jogos, horarios, VagasDaGrade.Duracao(torneio),
            await OcupantesPorDuplaAsync(torneioId.Value),
            await QuadrasEmUsoAsync(torneioId.Value), jaMarcados,
            await QuadrasPreferidasAsync(torneioId.Value));
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

    // A quadra que cada categoria PREFERE, por nome — CategoriaId → nomes das quadras.
    // Escolha do organizador (Models/QuadraDaCategoria); quem decide o que fazer com ela é
    // Services/PreferenciaDeQuadra. Torneio sem escolha nenhuma devolve mapa vazio, e aí a
    // grade se comporta exatamente como antes desta opção existir.
    //
    // ⚠️ Nome, e não Id, porque é assim que a grade fala (Partida.NomeQuadra é texto). A
    // consequência: quadra RENOMEADA depois do sorteio deixa de casar com o nome que os jogos
    // guardaram, e a preferência dela simplesmente não se aplica àquele horário — o mesmo
    // desencontro que NomesDeQuadra descreve, e pelo mesmo motivo.
    public async Task<Dictionary<int, string[]>> QuadrasPreferidasAsync(int torneioId) =>
        (await _context.QuadrasDaCategoria
            .Where(q => q.Quadra.TorneioId == torneioId)
            .Select(q => new { q.CategoriaId, q.Quadra.Nome })
            .ToListAsync())
        .GroupBy(q => q.CategoriaId)
        .ToDictionary(g => g.Key, g => g.Select(q => q.Nome).ToArray());

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
