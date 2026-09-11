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

        var partidasDeGrupo = await _context.Partidas
            .Where(p => p.CategoriaId == categoriaId
                     && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
            .ToListAsync();

        // ── O AVANÇO PARCIAL DOS GRUPOS (11/09/2026) ─────────────────────────────────────
        // 🗣️ Felipe: *"quando um grupo finalizar os 3 jogos, já coloque eles para a próxima fase
        // conforme a classificação, não precisa necessariamente terminar todos os jogos dos
        // outros grupos/chaves"*.
        //
        // ⚠️ SÓ COM O CRUZAMENTO DESENHADO, e por uma razão de régua: sem desenho, quem o 1º do
        // Grupo A enfrenta sai da campanha COMPARADA de todos os grupos (MontarPrimeiraFase
        // semeia o melhor contra o pior) — enquanto o Grupo D joga não dá pra saber se o 1º do A
        // é o melhor ou o pior primeiro colocado, e o jogo criado cedo teria que ser desfeito.
        // Com o desenho (Services/CruzamentoDoMataMata) a vaga é por COLOCAÇÃO, e fica conhecida
        // no instante em que os dois grupos dela fecham. Decisão do Felipe entre as três saídas
        // possíveis: vale só com desenho — sem ele o torneio (o Er) continua letra por letra.
        if (CruzamentoDoMataMata.Ler(categoria.CruzamentoDoMataMata) is { } desenho)
        {
            await MontarAberturaDesenhadaAsync(categoria, torneioId, desenho, partidasDeGrupo);
            return;
        }

        await MontarMataMataDosGruposSemDesenhoAsync(categoria, torneioId, partidasDeGrupo);
    }

    // O caminho de SEMPRE: a fase de grupos inteira fecha e o motor semeia a primeira rodada
    // pela campanha comparada dos grupos. É o que vale pra toda categoria sem cruzamento
    // desenhado — o Er inclusive.
    private async Task MontarMataMataDosGruposSemDesenhoAsync(
        Categoria categoria, int? torneioId, List<Partida> partidasDeGrupo)
    {
        int categoriaId = categoria.Id;

        // ⚠️ A FASE DE GRUPOS PRECISA TER ACABADO — e quem confere é o robô, não quem chama.
        //
        // A classificação (ClassificacaoDeGrupos) responde com o que tem: chamada no meio da
        // fase de grupos ela devolve um pódio provisório e o mata-mata nasce dali, montado
        // sobre jogos que ainda nem aconteceram. Enquanto esta guarda ficou no CHAMADOR, uma
        // das duas telas a tinha e a outra podia não ter — que é o mesmo defeito que este
        // arquivo existe pra fechar.
        if (partidasDeGrupo.Any(p => p.Status != "Finalizada")) return;

        var partidasFinalizadas = partidasDeGrupo.Where(p => p.Status == "Finalizada").ToList();

        // Evita gerar a chave duas vezes (ex: dois finalizamentos quase simultâneos).
        bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
            p.CategoriaId == categoriaId && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));
        if (mataMataJaGerado) return;

        var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();

        // Quantos passam de cada grupo — régua única em ClassificacaoDeGrupos.VagasPorGrupo:
        // 2 é a regra de sempre, e a categoria de TIMES usa o número que o organizador definiu.
        int classificamPorGrupo = ClassificacaoDeGrupos.VagasPorGrupo(categoria);

        // 1. O ranking final de cada grupo, pela régua única (Services/ClassificacaoDeGrupos)
        //    — a mesma que a tela de classificação e a detecção de bye usam.
        var duplasDosGrupos = grupos.SelectMany(g => g.Duplas).ToList();
        var classificados = ClassificacaoDeGrupos.Calcular(
            duplasDosGrupos, partidasFinalizadas, classificamPorGrupo);

        // 2. Motor único de chaveamento: TODO classificado avança; o quadro cresce pra caber
        //    todo mundo e os MELHORES pegam bye (pulam a primeira rodada). Os byes não ganham
        //    partida aqui — é a ausência dela que o robô de avanço lê depois
        //    (Services/AvancoDaChave) pra somá-los aos vencedores.
        //    O cruzamento desenhado à mão, quando existe, manda no lugar da semeadura — a
        //    decisão mora dentro do MontarPrimeiraFase, pra prévia e sorteio não divergirem.
        var (nomeFase, confrontos, _) = ChaveamentoMataMata.MontarPrimeiraFase(
            classificados, classificamPorGrupo, categoria.CruzamentoDoMataMata);
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

    // A ABERTURA DO MATA-MATA QUANDO O CRUZAMENTO FOI DESENHADO — grupo a grupo.
    //
    // O desenho diz o quadro inteiro por COLOCAÇÃO ("1A×2C|1B×2D;bye:1E"), então cada jogo
    // depende só dos SEUS dois grupos: fechados os dois, a vaga tem dono e o jogo pode ir pra
    // quadra enquanto o resto da categoria ainda joga.
    //
    // ⚠️ EM ORDEM DE QUADRO, como no avanço de fase: o jogo 2 só nasce depois do 1. A numeração
    // da fase é a ordem de criação (ReservasDeHorario.NumeroNaFase, por Id) e dela dependem o
    // desenho da chave, a procedência da prévia e a reserva de horário do organizador. Por isso
    // o laço PARA no primeiro confronto que ainda não dá pra montar em vez de pular pro seguinte.
    //
    // ⚠️ E NADA AVANÇA ENQUANTO ESTA FASE ESTIVER PELA METADE — a trava mora em AvancoDaChave
    // (sem a fase de grupos fechada não há bye nem vaga). Sem ela, um jogo de abertura terminado
    // sozinho viraria uma lista de UMA vaga, que o robô de progressão batizaria de "Final".
    private async Task MontarAberturaDesenhadaAsync(
        Categoria categoria, int? torneioId, CruzamentoDoMataMata.Mapa desenho,
        List<Partida> partidasDeGrupo)
    {
        // A régua única de quantas vagas cada grupo dá (ClassificacaoDeGrupos.VagasPorGrupo) —
        // o `?? 2` na mão tem gate mecânico desde 11/09/2026.
        int classificamPorGrupo = ClassificacaoDeGrupos.VagasPorGrupo(categoria);
        var duplasDosGrupos = categoria.GruposTorneio.SelectMany(g => g.Duplas).ToList();

        // O desenho serve pra ESTA categoria? A conferência é sobre o conjunto de vagas
        // (colocação × grupo), que não depende de resultado nenhum — então ela pode ser feita
        // com a classificação provisória. Desenho que não serve é descartado e quem decide é o
        // motor, como sempre: a categoria nunca fica sem mata-mata por causa de um texto torto.
        var provisorios = ClassificacaoDeGrupos.Calcular(
            duplasDosGrupos, partidasDeGrupo.Where(p => p.Status == "Finalizada").ToList(),
            classificamPorGrupo);
        if (CruzamentoDoMataMata.Conferir(desenho, provisorios) != null)
        {
            // Desenho que não serve NÃO adianta nada, mas também não pode atrapalhar: a
            // categoria volta a ser exatamente o que era antes de alguém desenhar — espera a
            // fase de grupos inteira e o motor semeia. (O `MontarPrimeiraFase` lá dentro faz a
            // mesma conferência e cai na mesma decisão: uma régua só.)
            if (partidasDeGrupo.Any(p => p.Status != "Finalizada")) return;

            await MontarMataMataDosGruposSemDesenhoAsync(categoria, torneioId, partidasDeGrupo);
            return;
        }

        string nomeFase = CruzamentoDoMataMata.NomeDaAbertura(desenho);

        // Quadro já passou da abertura: não se mexe mais nela. Não acontece hoje (nada avança
        // com a abertura pela metade), e é barato garantir.
        if (await _context.Partidas.AnyAsync(p =>
                p.CategoriaId == categoria.Id
                && p.Fase != nomeFase
                && ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))) return;

        int jaCriados = await _context.Partidas
            .CountAsync(p => p.CategoriaId == categoria.Id && p.Fase == nomeFase);
        if (jaCriados >= desenho.Confrontos.Count) return;

        // A classificação SÓ DOS GRUPOS QUE FECHARAM. Um grupo em andamento devolve pódio
        // provisório (ClassificacaoDeGrupos responde com o que tem), e um jogo criado a partir
        // dele seria desfeito no próximo placar — por isso ele fica de fora da conta, e a vaga
        // dele volta nula.
        var idsDeGrupoFechado = categoria.GruposTorneio
            .Where(g => GrupoFechado(g, partidasDeGrupo))
            .SelectMany(g => g.Duplas)
            .Select(d => d.Id)
            .ToHashSet();

        var prontos = ClassificacaoDeGrupos.Calcular(
            duplasDosGrupos.Where(d => idsDeGrupoFechado.Contains(d.Id)).ToList(),
            partidasDeGrupo.Where(p => p.Status == "Finalizada").ToList(),
            classificamPorGrupo);

        var novos = new List<Partida>();
        for (int i = jaCriados; i < desenho.Confrontos.Count; i++)
        {
            var confronto = desenho.Confrontos[i];
            if (CruzamentoDoMataMata.IdDaVaga(confronto.Lado1, prontos) is not int lado1
                || CruzamentoDoMataMata.IdDaVaga(confronto.Lado2, prontos) is not int lado2) break;

            novos.Add(new Partida
            {
                TorneioId = torneioId,
                CategoriaId = categoria.Id,
                Dupla1Id = lado1,
                Dupla2Id = lado2,
                Status = "Agendada",   // Nasce agendada para ir para a Mesa de Controle!
                Fase = nomeFase,
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()   // NOT NULL no banco
            });
        }

        if (novos.Count == 0) return;

        await AgendarNaGradeAsync(novos, torneioId);

        _context.Partidas.AddRange(novos);
        await _context.SaveChangesAsync();
    }

    // O grupo acabou? Grupo SEM jogo nenhum (uma dupla só) conta como fechado — senão a vaga
    // dele nunca resolveria e o mata-mata da categoria não sairia jamais.
    private static bool GrupoFechado(padelizou.Models.GrupoTorneio grupo, IReadOnlyList<Partida> partidasDeGrupo)
    {
        var doGrupo = grupo.Duplas.Select(d => d.Id).ToHashSet();

        return partidasDeGrupo
            .Where(p => doGrupo.Contains(p.Dupla1Id) || doGrupo.Contains(p.Dupla2Id))
            .All(p => p.Status == "Finalizada");
    }

    // ===================================================================================
    // ROBÔ 2: PROGRESSÃO — Primeira Rodada → Oitavas → Quartas → Semifinal → Final
    // ===================================================================================
    public async Task AvancarFaseAsync(int categoriaId, int? torneioId, string faseConcluida)
    {
        // Fase que não encadeia (grupos, Americano, Final) para aqui.
        if (ChaveamentoMataMata.ProximaFase(faseConcluida) == null) return;

        // As vagas da rodada seguinte, na ordem do quadro: vencedores desta fase (null onde o
        // jogo ainda não acabou) e, na primeira rodada, quem passou direto. Ver
        // Services/AvancoDaChave. Vazio = não há o que avançar.
        var vagas = await AvancoDaChave.VagasDaProximaFaseAsync(_context, categoriaId, faseConcluida);
        if (vagas.Count < 2) return;

        // Com bye o quadro encolhe mais devagar: a primeira rodada de uma chave de 24 entrega
        // 16 (8 vencedores + 8 byes), que são Oitavas — e não as Quartas que o encadeamento
        // por NOME sugeriria. Quem manda é quanta gente sobrou.
        var proximaFase = ChaveamentoMataMata.NomeFase(vagas.Count);

        // ── O AVANÇO PARCIAL (11/09/2026) ────────────────────────────────────────────────
        // 🗣️ Felipe: *"terminou a primeira quarta de final, esse que já classificou, já vai a
        // dupla para a semi, mesmo que as outras quartas não tenham finalizado"*.
        //
        // A rodada não nasce mais inteira: nasce jogo a jogo, à medida que as DUAS vagas de
        // cada confronto ganham dono (`ParearVencedores` cruza a vaga i com a n-1-i, então a
        // Semifinal 1 é "vencedor da Quartas 1 × última vaga" — e a última vaga costuma ser um
        // bye, que já tem dono desde o sorteio).
        //
        // ⚠️ EM ORDEM DE QUADRO, E ISSO NÃO É CAPRICHO. O número de um jogo dentro da fase é a
        // ordem de CRIAÇÃO (ReservasDeHorario.NumeroNaFase, por Id), e dela dependem o desenho
        // da chave (Services/OrdemDoQuadro), a procedência da prévia ("Vencedor Semifinal 2") e
        // a reserva de horário que o organizador fez no jogo previsto. Deixar a Semifinal 2
        // nascer antes da 1 por ter terminado primeiro faria as três apontarem pro jogo errado.
        // Por isso o laço PARA no primeiro confronto que ainda não dá pra montar, em vez de
        // pular pro seguinte: o preço de uma vaga adiantada seria a chave inteira mentindo.
        //
        // ⚠️ E É ESTE CONTADOR que impede a fase de nascer duas vezes — dois finalizamentos
        // quase simultâneos, ou o organizador reabrindo e refinalizando o mesmo jogo. Antes a
        // guarda era "a próxima fase já existe?", que não serve mais: agora ela existe pela
        // metade o tempo todo.
        int jaCriados = await _context.Partidas
            .CountAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase);

        var novos = new List<Partida>();
        for (int i = jaCriados; i < vagas.Count / 2; i++)
        {
            if (vagas[i] is not int lado1 || vagas[vagas.Count - 1 - i] is not int lado2) break;

            novos.Add(new Partida
            {
                TorneioId = torneioId,
                CategoriaId = categoriaId,
                Fase = proximaFase,
                Status = "Agendada",
                Dupla1Id = lado1,
                Dupla2Id = lado2,
                // Codigo é obrigatório no banco (NOT NULL) — sem ele o INSERT do robô falha.
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
            });
        }

        if (novos.Count == 0) return;

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

        // ⚠️ O "POR ORDEM DE LIBERAÇÃO" PASSA POR AQUI TAMBÉM (10/09/2026). Até hoje o robô saía
        // antes, e a rodada nova nascia SEM hora — enquanto o sorteio e o Refazer grade, desde
        // 09/09 (Services/OrdemDeLiberacao), dão hora a todo jogo e apagam só a QUADRA. Ficava a
        // fase de grupos com hora e a semifinal "por ordem" na mesma lista, e a prévia prometendo
        // uma hora que o jogo real nunca ganhava — foi no torneio do Er, que é por ordem, que a
        // troca de horário da eliminatória prevista não tinha como funcionar. Agora ele faz o
        // mesmo que os outros dois: calcula, carimba o clube e apaga a quadra no fim.

        var jaMarcados = await _context.Partidas
            .Where(p => p.TorneioId == torneioId && p.HorarioPrevisto != null)
            .ToListAsync();

        // ⚠️ O ROBÔ NÃO ENXERGA FASE QUE AINDA NÃO NASCEU — e é daí que vinha o defeito que o
        // Felipe reportou em 09/09/2026 (*"como que aqui tem jogo de chave e nas outras categorias
        // tem final?"*).
        //
        // 🕳️ MEDIDO num torneio de 16/8/4 duplas, 3 quadras: a categoria de 4 fecha os grupos às
        // 11h e a Semifinal dela nasce na hora, marcada pras 12h30. As Quartas da categoria de 8
        // só nascem às 11h30, quando os grupos DELA fecham — e caem em cima, às 12h30/13h. Nenhuma
        // barreira calculada sobre "o que já está marcado" resolve isso: no instante em que a
        // semifinal foi marcada, as quartas não existiam pra serem consultadas.
        //
        // ✅ Por isso a rodada nova NÃO é encaixada sozinha: ela entra junto com tudo que ainda
        // está "Agendada" e ficou FORA DE ORDEM por causa dela (Services/LevasDaGrade.ForaDeOrdem),
        // e o conjunto todo passa pela mesma régua de postos do sorteio. Nada de estimar quanto
        // tempo a outra categoria vai levar, e nada de travar o torneio esperando uma categoria que
        // desistiu: quem chega depois reordena o que está por vir.
        //
        // ⚠️ SÓ MEXE EM "Agendada", E SÓ EM POSTO MAIOR. Jogo FINALIZADO ou EM QUADRA não se
        // remarca — é a mesma linha que o "Refazer grade" não cruza —, e um jogo de posto menor ou
        // igual nunca está fora de ordem por causa de uma rodada que acabou de entrar.
        // ⚠️ POR ID: é a ordem da fila em que o sorteio gravou (ver RecalcularAGradeAsync, 10/09/2026).
        var todos = await _context.Partidas.Where(p => p.TorneioId == torneioId).OrderBy(p => p.Id).ToListAsync();
        var forasDeOrdem = LevasDaGrade.ForaDeOrdem(todos, OrdemDasFases.Posto(jogos[0].Fase));

        // ⚠️ NO "POR ORDEM", QUADRA ESCRITA É O BALCÃO CHAMANDO (10/09/2026, revisão adversarial do
        // ensaio do Er): nesse modo o jogo nasce sem quadra, e o "Agendada" que JÁ TEM quadra foi
        // chamado pelo balcão — as duplas estão caminhando pra ela. Reencaixá-lo zerava hora e
        // quadra e o empurrava pra depois, no minuto em que outra categoria fechava uma fase (a
        // Final chamada pra Arena 3 sumia da quadra quando a Semifinal da 3ª nascia). Fica onde
        // está, como o jogo em quadra: quem manda a partir da chamada é o balcão.
        if (torneio != null && OrdemDeLiberacao.Vale(torneio))
            forasDeOrdem = forasDeOrdem.Where(p => string.IsNullOrWhiteSpace(p.NomeQuadra)).ToList();

        // ── A RESERVA DO ORGANIZADOR (10/09/2026, Models/ReservaDeHorario) ─────────────────
        // 🗣️ *"permita também trocar de horário as eliminatórias, não apenas as de chave"*. A troca
        // feita numa eliminatória que ainda não existia mora numa reserva, e é AQUI que ela vira
        // jogo de verdade: a rodada nasce no horário e na quadra reservados, e não onde o encaixe a
        // poria. Vale também pro fora de ordem que está sendo reencaixado — sem isso a Final
        // reservada da A perdia a reserva no minuto em que a Semifinal da B nascesse, calada.
        //
        // ⚠️ A régua de validade é a MESMA da prévia (ReservasDeHorario.Vale): reserva que caiu
        // antes de a fase anterior desta categoria acabar não vale, e o jogo volta pra grade. Lida
        // ANTES de zerar os fora de ordem: é a hora que eles TÊM que diz quando a fase anterior
        // acabou.
        var reservas = await ReservasDeHorario.DoTorneio(_context, torneioId.Value).ToListAsync();
        var candidatos = jogos.Concat(forasDeOrdem).ToList();

        // O número do jogo dentro da fase: por Id nos que já existem, e pela ordem da lista na rodada
        // que está nascendo — é a ordem em que `AddRange` grava, logo a ordem dos Ids de amanhã.
        var numeroPorId = ReservasDeHorario.NumeroNaFase(todos);
        int NumeroDe(Partida p) => p.Id == 0 ? jogos.IndexOf(p) + 1 : numeroPorId.GetValueOrDefault(p.Id);
        DateTime? AbreARodadaDe(int posto, int categoriaId) =>
            LevasDaGrade.PisoDaCategoria(torneio, jaMarcados, posto, categoriaId);

        // A agenda das PESSOAS, pra reserva não pôr alguém em duas quadras no mesmo minuto
        // (revisão adversarial, 10/09/2026). Mesma régua por intervalo do GradeDeJogos.Encaixar
        // (`Cruza`: menos de uma partida de distância). Os fora de ordem ficam de fora da conta
        // porque vão ser remarcados de qualquer jeito.
        var ocupantesPorDupla = await OcupantesPorDuplaAsync(torneioId.Value);
        var duracaoDaPartida = TimeSpan.FromMinutes(VagasDaGrade.Duracao(torneio));
        var queFicamOndeEstao = jaMarcados.Except(forasDeOrdem).ToList();

        int[] Pessoas(Partida p) => Gente(p.Dupla1Id).Concat(Gente(p.Dupla2Id)).ToArray();
        int[] Gente(int duplaId) =>
            ocupantesPorDupla.TryGetValue(duplaId, out var pessoas) && pessoas.Length > 0 ? pessoas : new[] { -duplaId };
        bool PessoaOcupada(Partida jogo, DateTime quando)
        {
            var minhas = Pessoas(jogo);
            return queFicamOndeEstao.Any(p => !ReferenceEquals(p, jogo)
                && p.HorarioPrevisto is DateTime h
                && (h - quando).Duration() < duracaoDaPartida
                && Pessoas(p).Intersect(minhas).Any());
        }

        var (reservados, mortas) = ReservasDeHorario.Aplicar(candidatos, NumeroDe, reservas,
            p => AbreARodadaDe(OrdemDasFases.Posto(p.Fase), p.CategoriaId), PessoaOcupada);

        // A reserva de um jogo que já saiu dela (trocado depois de nascer) morre aqui — quem chama
        // grava junto com a rodada nova. Ver ReservasDeHorario.Aplicar.
        _context.ReservasDeHorario.RemoveRange(mortas);
        reservas = reservas.Except(mortas).ToList();

        // A rodada nova ainda não tem horário; os fora de ordem perdem o que tinham pra disputar as
        // vagas de novo, na ordem certa. Os reservados ficam com a hora da reserva.
        foreach (var jogo in forasDeOrdem.Except(reservados))
        {
            jogo.HorarioPrevisto = null;
            jogo.NomeQuadra = null;
        }

        var paraEncaixar = candidatos.Except(reservados).ToList();

        // Os que ficam de fora da conta e não podem ser atropelados: tudo que CONTINUA com hora.
        // Lido DEPOIS de zerar os fora de ordem de propósito — o EF devolve as mesmas instâncias
        // nas duas consultas, então um jogo recém-zerado já sai daqui sozinho. Fosse lido antes,
        // ele entraria como intocado E como candidato, e o encaixe reservaria a vaga dele contra
        // ele mesmo.
        //
        // Entram também os reservados da rodada nova (ainda não estão no banco) e os slots das
        // reservas de jogos que AINDA VÃO NASCER — senão o encaixe de hoje ocuparia a quadra que
        // a reserva de amanhã prometeu (ver ReservasDeHorario.AindaPorNascer).
        var fasesQueExistem = todos.Concat(jogos).Select(p => (p.CategoriaId, p.Fase)).ToHashSet();
        var intocados = jaMarcados.Where(p => p.HorarioPrevisto != null)
            .Union(reservados)
            .Concat(ReservasDeHorario.AindaPorNascer(reservas, fasesQueExistem,
                (categoriaId, fase) => AbreARodadaDe(OrdemDasFases.Posto(fase), categoriaId)))
            .ToList();

        // As restrições de horário do torneio. Mesmo motivo de buscar direto no banco em cada uma:
        // `torneio` aqui vem de um FindAsync, sem Categorias/Duplas incluídas.
        var concentracao = await ConcentracaoAsync(torneio);
        var sedes = await SedesAsync(torneioId.Value);
        var janelasProibidas = await JanelasProibidasPorDuplaAsync(torneio);
        var noiteDeSabado = await NoiteDeSabadoPorCategoriaAsync(torneio);

        // ⚠️ A ABERTURA É A DA GRADE, e não o fim da fase anterior: quem decide a hora de cada
        // posto agora é a régua, e dar a ela um piso adiantado esconderia a barreira que ela
        // acabou de calcular. O piso contra "marcar no passado" continua dentro dela.
        LevasDaGrade.Encaixar(torneio, paraEncaixar, torneio.AberturaDaGrade, intocados,
            new LevasDaGrade.Restricoes(
                ocupantesPorDupla,
                await QuadrasEmUsoAsync(torneioId.Value),
                await QuadrasPreferidasAsync(torneioId.Value),
                janelasProibidas,
                concentracao,
                noiteDeSabado,
                sedes));

        // A rodada nova também sai com o clube gravado (Partida.ClubeId) — ver OrdemDeLiberacao.
        // `candidatos`, e não `paraEncaixar`: o jogo reservado também precisa de clube. E no "por
        // ordem" a quadra vai embora DEPOIS do carimbo — quem decide onde é a Mesa, conforme vaga.
        OrdemDeLiberacao.CarimbarOClube(torneio, candidatos, sedes);
        OrdemDeLiberacao.ApagarAsQuadras(torneio, candidatos);
    }

    // O impedimento de horário pago na inscrição, pronto pra passar pro Encaixar. Ver
    // Services/JanelasDeImpedimento — `torneio` não vem com Categorias/Duplas incluído aqui
    // (só FindAsync), então busca direto em Duplas, mesmo padrão de OcupantesPorDuplaAsync.
    private async Task<Dictionary<int, (DateTime, DateTime)[]>> JanelasProibidasPorDuplaAsync(Torneio torneio) =>
        JanelasDeImpedimento.PorDupla(torneio, await _context.Duplas
            .Where(d => d.Categoria.TorneioId == torneio.Id)
            .ToListAsync());

    // A concentração ("os 2 jogos na sexta") e o "sem eliminatória no sábado à noite", 08/09/2026.
    // Mesmo motivo de buscar direto no banco: `torneio` aqui vem de um FindAsync, sem
    // Categorias/Duplas incluídas — usar as coleções vazias dele devolveria mapa vazio e a
    // restrição sumiria em silêncio justamente no caminho que agenda as fases seguintes.
    private async Task<ConcentracaoDeJogos.Concentracoes> ConcentracaoAsync(Torneio torneio) =>
        ConcentracaoDeJogos.De(torneio, await _context.Duplas
            .Where(d => d.Categoria.TorneioId == torneio.Id)
            .ToListAsync());

    private async Task<Dictionary<int, (DateTime Inicio, DateTime Fim)[]>> NoiteDeSabadoPorCategoriaAsync(Torneio torneio) =>
        EliminatoriaNoSabado.PorCategoria(torneio, await _context.Categorias
            .Where(c => c.TorneioId == torneio.Id)
            .ToListAsync());

    // O torneio em MAIS DE UM CLUBE. A régua e a consulta moram em Services/SedesDoTorneio —
    // aqui é só o atalho pra quem já tem o robô na mão. Quase todo torneio tem uma sede só, e
    // aí o mapa volta vazio e a grade se comporta exatamente como antes desta opção existir.
    public Task<SedesDoTorneio> SedesAsync(int torneioId) =>
        SedesDoTorneio.CarregarAsync(_context, torneioId);

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
    //
    // ⚠️ MEIA DUPLA TAMBÉM OCUPA QUADRA (09/09/2026). Aqui havia `d.Jogador2Id != null`, que
    // não era regra — era só o jeito de poder escrever `Jogador2Id!.Value` embaixo. Quando a
    // inscrição sozinha passou a entrar na chave, esse filtro a apagava do mapa de PESSOAS: o
    // jogador inscrito sozinho numa categoria e com parceiro noutra podia ser marcado em duas
    // quadras no mesmo horário, sem ninguém ver — a tela "Conferir grade" lê este mesmo mapa.
    public static Dictionary<int, int[]> OcupantesPorDupla(IEnumerable<Dupla> duplas) =>
        duplas
            .Where(d => !d.EhTime)
            .ToDictionary(
                d => d.Id,
                d => d.Jogador2Id is int parceiro
                    ? new[] { d.Jogador1Id, parceiro }
                    : new[] { d.Jogador1Id });

    public static Dictionary<int, int[]> OcupantesPorDupla(Torneio torneio) =>
        OcupantesPorDupla(torneio.Categorias.SelectMany(c => c.Duplas));
}
