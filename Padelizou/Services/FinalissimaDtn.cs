using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A FINALÍSSIMA DTN E O TROFÉU (Treta, 25/11/2026) — ver TRETA.md.
//
// 🗣️ Organizador do Treta:
//  • "troca o do mesmo lado (direita com direita), tem que manter posição do torneio" —
//    Branco (E₁, D₁) e Verde (E₂, D₂) viram E₁ + D₂ × E₂ + D₁;
//  • "a dupla chega em um consenso" — a dupla vencedora escolhe UM dos perdedores; é uma
//    escolha por dupla, então não há empate nem contagem de votos;
//  • módulo sem par (o Preto) não tem Finalíssima: o troféu sai da própria final DTN.
//
// ⚠️ O LADO NÃO É CONFIÁVEL NO BANCO: `Dupla.LadoJogador1` só existe pro jogador 1, e o do
// parceiro vem do perfil, que pode estar vazio ou "Ambos". Por isso a troca é PROPOSTA e quem
// organiza confirma — nunca nasce sozinha.
public class FinalissimaDtn
{
    private readonly DbPadelContext _context;

    public FinalissimaDtn(DbPadelContext context) => _context = context;

    // ── A troca ─────────────────────────────────────────────────────────────────────────

    // Quem joga na DIREITA em cada perdedora. `Certa` = os dois lados vieram de dado gravado
    // sem ambiguidade; falso = é um chute que o organizador precisa olhar.
    public record Proposta(int DireitaDaPrimeira, int DireitaDaSegunda, bool Certa);

    public static Proposta ProporTroca(Dupla primeira, Dupla segunda)
    {
        var (d1, certa1) = QuemJogaNaDireita(primeira);
        var (d2, certa2) = QuemJogaNaDireita(segunda);
        return new Proposta(d1, d2, certa1 && certa2);
    }

    private static (int Direita, bool Certa) QuemJogaNaDireita(Dupla dupla)
    {
        int j1 = dupla.Jogador1Id;
        int j2 = dupla.Jogador2Id ?? j1;
        var lado1 = LadoNaQuadra.Efetivo(dupla.LadoJogador1, dupla.Jogador1?.LadoQuadra);
        var lado2 = LadoNaQuadra.Normalizar(dupla.Jogador2?.LadoQuadra);

        if (lado1 == LadoNaQuadra.Direita) return (j1, lado2 != LadoNaQuadra.Direita);
        if (lado1 == LadoNaQuadra.Esquerda) return (j2, lado2 != LadoNaQuadra.Esquerda);
        if (lado2 == LadoNaQuadra.Direita) return (j2, true);
        if (lado2 == LadoNaQuadra.Esquerda) return (j1, true);
        return (j1, false);
    }

    // As duas duplas da Finalíssima, a partir de quem joga na direita em cada perdedora:
    // a esquerda de uma com a direita da outra.
    public static ((int Esquerda, int Direita) Dupla1, (int Esquerda, int Direita) Dupla2) Montar(
        Dupla primeira, int direitaDaPrimeira, Dupla segunda, int direitaDaSegunda)
    {
        int Parceiro(Dupla d, int direita) => d.Jogador1Id == direita ? d.Jogador2Id ?? d.Jogador1Id : d.Jogador1Id;
        return ((Parceiro(primeira, direitaDaPrimeira), direitaDaSegunda),
                (Parceiro(segunda, direitaDaSegunda), direitaDaPrimeira));
    }

    // ── O que está pendente ──────────────────────────────────────────────────────────────

    // Um par de módulos cujas DUAS finais DTN acabaram e que ainda não tem Finalíssima.
    public record Pendente(Categoria Modulo1, Categoria Modulo2, Dupla Perdedora1, Dupla Perdedora2, Proposta Proposta);

    public async Task<List<Pendente>> PendentesAsync(int torneioId)
    {
        var pendentes = new List<Pendente>();
        foreach (var (m1, m2) in await ParesAsync(torneioId))
        {
            if (await FinalissimaDoParAsync(m1.Id) != null) continue;
            var p1 = await PerdedoraDaFinalDtnAsync(m1.Id);
            var p2 = await PerdedoraDaFinalDtnAsync(m2.Id);
            if (p1 == null || p2 == null) continue;
            pendentes.Add(new Pendente(m1, m2, p1, p2, ProporTroca(p1, p2)));
        }
        return pendentes;
    }

    // Há par de módulos ainda sem Finalíssima? Segura o "Finalizado" do torneio: sem isto, a
    // última final DTN fecharia o torneio antes de a Finalíssima nascer (ela depende do
    // organizador confirmar a troca), e o MVP abriria com um jogo ainda por vir.
    public async Task<bool> FaltaFinalissimaAsync(int torneioId)
    {
        foreach (var (m1, _) in await ParesAsync(torneioId))
            if (await FinalissimaDoParAsync(m1.Id) == null) return true;
        return false;
    }

    // Os pares de módulos, cada um uma vez só, com o de menor Id na frente (é ele a origem da
    // Finalíssima). Marcar o par de um lado basta.
    private async Task<List<(Categoria, Categoria)>> ParesAsync(int torneioId)
    {
        var modulos = await _context.Categorias.AsNoTracking()
            .Where(c => c.TorneioId == torneioId && c.PapelDaChave == null && c.DuplasNoDtnPorGrupo > 0)
            .ToListAsync();
        var porId = modulos.ToDictionary(c => c.Id);
        var pares = new HashSet<(int, int)>();
        foreach (var m in modulos)
        {
            if (m.ParDaFinalissimaId is int par && par != m.Id && porId.ContainsKey(par))
                pares.Add((Math.Min(m.Id, par), Math.Max(m.Id, par)));
        }
        return pares.OrderBy(p => p.Item1).Select(p => (porId[p.Item1], porId[p.Item2])).ToList();
    }

    private Task<Categoria?> FinalissimaDoParAsync(int moduloDeOrigemId) =>
        _context.Categorias.AsNoTracking().FirstOrDefaultAsync(c =>
            c.CategoriaDeOrigemId == moduloDeOrigemId && c.PapelDaChave == ChaveDtn.Finalissima);

    // A cópia que PERDEU a final da DTN do módulo — nula enquanto a final não acabou.
    private async Task<Dupla?> PerdedoraDaFinalDtnAsync(int moduloId)
    {
        var final = await _context.Partidas.AsNoTracking()
            .Where(p => p.Categoria.CategoriaDeOrigemId == moduloId
                     && p.Categoria.PapelDaChave == ChaveDtn.Dtn
                     && p.Fase == "Final" && p.Status == "Finalizada" && p.VencedorId != null)
            .Select(p => new { p.Dupla1Id, p.Dupla2Id, p.VencedorId })
            .FirstOrDefaultAsync();
        if (final == null) return null;
        int perdedoraId = final.VencedorId == final.Dupla1Id ? final.Dupla2Id : final.Dupla1Id;
        return await _context.Duplas.AsNoTracking()
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .FirstOrDefaultAsync(d => d.Id == perdedoraId);
    }

    // ── Criar ───────────────────────────────────────────────────────────────────────────

    // Devolve o motivo da recusa, ou null quando criou. Quem chama já conferiu que é organizador.
    public async Task<string?> CriarAsync(int torneioId, int modulo1Id, int direitaDaPrimeira, int direitaDaSegunda,
        RoboDoChaveamento robo)
    {
        var pendente = (await PendentesAsync(torneioId)).FirstOrDefault(p => p.Modulo1.Id == modulo1Id);
        if (pendente == null) return "Essa Finalíssima já foi criada, ou as finais DTN ainda não acabaram.";

        if (!Joga(pendente.Perdedora1, direitaDaPrimeira) || !Joga(pendente.Perdedora2, direitaDaSegunda))
            return "Escolha, em cada dupla, um dos dois atletas pra jogar na direita.";

        var (nova1, nova2) = Montar(pendente.Perdedora1, direitaDaPrimeira, pendente.Perdedora2, direitaDaSegunda);

        var finalissima = new Categoria
        {
            TorneioId = torneioId,
            Nome = "Finalíssima DTN",
            Codigo = ChaveDtn.CodigoDaDtn(pendente.Modulo1.Codigo) + "F",
            ChaveDireta = true,
            PapelDaChave = ChaveDtn.Finalissima,
            CategoriaDeOrigemId = pendente.Modulo1.Id,
            ClubeId = pendente.Modulo1.ClubeId,
        };
        _context.Categorias.Add(finalissima);

        // Jogador 1 = quem fica na esquerda; o lado gravado é o da posição no torneio.
        var dupla1 = new Dupla
        {
            Categoria = finalissima, Jogador1Id = nova1.Esquerda, Jogador2Id = nova1.Direita,
            LadoJogador1 = LadoNaQuadra.Esquerda, Pago = true,
        };
        var dupla2 = new Dupla
        {
            Categoria = finalissima, Jogador1Id = nova2.Esquerda, Jogador2Id = nova2.Direita,
            LadoJogador1 = LadoNaQuadra.Esquerda, Pago = true,
        };
        _context.Duplas.AddRange(dupla1, dupla2);
        await _context.SaveChangesAsync();

        var jogo = new Partida
        {
            TorneioId = torneioId,
            CategoriaId = finalissima.Id,
            Dupla1Id = dupla1.Id,
            Dupla2Id = dupla2.Id,
            Fase = "Final",
            Status = "Agendada",
            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
        };
        var jogos = new List<Partida> { jogo };
        await robo.AgendarNaGradeAsync(jogos, torneioId);
        _context.Partidas.Add(jogo);

        // O torneio pode ter sido carimbado "Finalizado" antes desta regra existir pra ele; com
        // um jogo novo por vir, ele não acabou.
        var torneio = await _context.Torneios.FindAsync(torneioId);
        if (torneio != null && torneio.Status == "Finalizado") torneio.Status = "Em Andamento";

        await _context.SaveChangesAsync();
        return null;
    }

    private static bool Joga(Dupla dupla, int jogadorId) =>
        dupla.Jogador1Id == jogadorId || dupla.Jogador2Id == jogadorId;

    // ── O troféu ────────────────────────────────────────────────────────────────────────

    // O jogo que decide o troféu: a final da Finalíssima, ou a final da DTN de um módulo SEM par.
    public record Decisivo(Categoria Categoria, IReadOnlyList<int> Vencedores, IReadOnlyList<int> Perdedores);

    public async Task<List<Decisivo>> DecisivosAsync(int torneioId)
    {
        var categorias = await _context.Categorias.AsNoTracking()
            .Where(c => c.TorneioId == torneioId && c.PapelDaChave != null)
            .ToListAsync();
        var comPar = (await ParesAsync(torneioId)).SelectMany(p => new[] { p.Item1.Id, p.Item2.Id }).ToHashSet();

        var decisivos = new List<Decisivo>();
        foreach (var cat in categorias)
        {
            bool decide = cat.PapelDaChave == ChaveDtn.Finalissima
                || (cat.PapelDaChave == ChaveDtn.Dtn && cat.CategoriaDeOrigemId is int origem && !comPar.Contains(origem));
            if (!decide) continue;

            var final = await _context.Partidas.AsNoTracking()
                .Where(p => p.CategoriaId == cat.Id && p.Fase == "Final" && p.Status == "Finalizada" && p.VencedorId != null)
                .Select(p => new
                {
                    p.VencedorId, p.Dupla1Id,
                    V1 = p.Dupla1.Jogador1Id, V2 = p.Dupla1.Jogador2Id,
                    W1 = p.Dupla2.Jogador1Id, W2 = p.Dupla2.Jogador2Id,
                })
                .FirstOrDefaultAsync();
            if (final == null) continue;

            var lado1 = new[] { final.V1, final.V2 ?? final.V1 }.Distinct().ToList();
            var lado2 = new[] { final.W1, final.W2 ?? final.W1 }.Distinct().ToList();
            bool venceuALado1 = final.VencedorId == final.Dupla1Id;
            decisivos.Add(new Decisivo(cat, venceuALado1 ? lado1 : lado2, venceuALado1 ? lado2 : lado1));
        }
        return decisivos;
    }

    // Devolve o motivo da recusa, ou null quando gravou. `ehOrganizador` vem da régua do
    // controller (EhOrganizadorAsync).
    //
    // ⚠️ A dupla vencedora registra UMA vez — a escolha é dela, em consenso; trocar depois é com
    // quem organiza (um dos dois não pode desfazer sozinho o que os dois combinaram).
    public async Task<string?> RegistrarTrofeuAsync(int torneioId, int categoriaId, int jogadorId,
        int quemPede, bool ehOrganizador)
    {
        var decisivo = (await DecisivosAsync(torneioId)).FirstOrDefault(d => d.Categoria.Id == categoriaId);
        if (decisivo == null) return "O jogo que decide o troféu ainda não acabou.";

        bool venceu = decisivo.Vencedores.Contains(quemPede);
        if (!ehOrganizador && !venceu) return "Quem escolhe é a dupla vencedora (ou a organização).";
        if (!ehOrganizador && decisivo.Categoria.TrofeuDtnJogadorId != null)
            return "O troféu já foi registrado. Pra trocar, fale com a organização.";
        if (!decisivo.Perdedores.Contains(jogadorId)) return "O troféu vai pra um dos dois atletas que perderam.";

        var categoria = await _context.Categorias.FindAsync(categoriaId);
        if (categoria == null) return "Categoria não encontrada.";
        categoria.TrofeuDtnJogadorId = jogadorId;
        await _context.SaveChangesAsync();
        return null;
    }

    // ── A FINAL DTN TROCADA, NO MÓDULO SEM FINALÍSSIMA (27/11/2026) ──────────────────────
    //
    // 🗣️ Felipe, pelo Treta: "e final do módulo preto também inverte as duplas. Mesmo sistema da
    // finalíssima". O módulo SEM par de Finalíssima decide o troféu na própria final DTN — e ela
    // também é jogada com os parceiros trocados (direita com direita). O robô cria a final como
    // sempre; até ela ser finalizada (inclusive reaberta), quem organiza confirma a troca e o MESMO jogo (hora, quadra)
    // passa a ser entre as duplas mistas.
    //
    // "Ainda não trocada" = as duas duplas do jogo são as cópias do módulo (mesmos dois
    // jogadores de uma dupla inscrita). Depois da troca, nenhuma é — e o card some.
    public record FinalParaTrocar(Categoria Dtn, Partida Final, Dupla Dupla1, Dupla Dupla2, Proposta Proposta);

    public async Task<List<FinalParaTrocar>> FinaisParaTrocarAsync(int torneioId)
    {
        var comPar = (await ParesAsync(torneioId)).SelectMany(p => new[] { p.Item1.Id, p.Item2.Id }).ToHashSet();
        var dtns = await _context.Categorias.AsNoTracking()
            .Where(c => c.TorneioId == torneioId && c.PapelDaChave == ChaveDtn.Dtn && c.CategoriaDeOrigemId != null)
            .ToListAsync();

        var lista = new List<FinalParaTrocar>();
        foreach (var dtn in dtns.Where(d => !comPar.Contains(d.CategoriaDeOrigemId!.Value)))
        {
            var final = await _context.Partidas.AsNoTracking()
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                // Até FINALIZAR, e não só por começar (28/11/2026): 🗣️ Rafael reabriu a final pra
                // trocar e o card tinha sumido. Reabrir põe o jogo AO VIVO com o placar; a troca
                // mexe só nas duplas, e o placar fica pra quem organiza corrigir.
                .FirstOrDefaultAsync(p => p.CategoriaId == dtn.Id && p.Fase == "Final"
                    && p.Status != "Finalizada" && p.VencedorId == null);
            if (final == null) continue;

            int moduloId = dtn.CategoriaDeOrigemId!.Value;
            bool ehCopia(Dupla d) => _context.Duplas.Any(o => o.CategoriaId == moduloId
                && o.Jogador1Id == d.Jogador1Id && o.Jogador2Id == d.Jogador2Id);
            if (!ehCopia(final.Dupla1) || !ehCopia(final.Dupla2)) continue;

            lista.Add(new FinalParaTrocar(dtn, final, final.Dupla1, final.Dupla2, ProporTroca(final.Dupla1, final.Dupla2)));
        }
        return lista;
    }

    // Devolve o motivo da recusa, ou null quando trocou. Quem chama já conferiu que é organizador.
    public async Task<string?> TrocarFinalAsync(int torneioId, int finalId, int direitaDaPrimeira, int direitaDaSegunda)
    {
        var alvo = (await FinaisParaTrocarAsync(torneioId)).FirstOrDefault(f => f.Final.Id == finalId);
        if (alvo == null) return "Essa final já foi trocada, já foi finalizada, ou não é de DTN sem Finalíssima.";
        if (!Joga(alvo.Dupla1, direitaDaPrimeira) || !Joga(alvo.Dupla2, direitaDaSegunda))
            return "Escolha, em cada dupla, um dos dois atletas pra jogar na direita.";

        var (nova1, nova2) = Montar(alvo.Dupla1, direitaDaPrimeira, alvo.Dupla2, direitaDaSegunda);
        var dupla1 = new Dupla
        {
            CategoriaId = alvo.Dtn.Id, Jogador1Id = nova1.Esquerda, Jogador2Id = nova1.Direita,
            LadoJogador1 = LadoNaQuadra.Esquerda, Pago = true,
        };
        var dupla2 = new Dupla
        {
            CategoriaId = alvo.Dtn.Id, Jogador1Id = nova2.Esquerda, Jogador2Id = nova2.Direita,
            LadoJogador1 = LadoNaQuadra.Esquerda, Pago = true,
        };
        _context.Duplas.AddRange(dupla1, dupla2);
        await _context.SaveChangesAsync();

        // O MESMO jogo: hora, quadra e código ficam; só as duplas mudam.
        var final = await _context.Partidas.FirstAsync(p => p.Id == finalId);
        final.Dupla1Id = dupla1.Id;
        final.Dupla2Id = dupla2.Id;
        await _context.SaveChangesAsync();
        return null;
    }

    // ── SUBSTITUIR DUPLA NA DTN (28/11/2026) ──────────────────────────────────────────────
    //
    // 🗣️ Rafael, no dia do Treta: "tivemos W.O. no módulo preto e elas foram pro DTN como pior
    // dupla"; Felipe escolheu que quem organiza troca na mão (não uma regra automática de W.O.).
    // Troca os ATLETAS da cópia que está na DTN pelos de outra dupla do módulo: o jogo, a hora, a
    // quadra e o lugar na chave ficam. Só pra cópia que ainda NÃO jogou nada na DTN — mexer em quem
    // já jogou reescreveria resultado.
    public record Substituivel(Categoria Dtn, IReadOnlyList<Dupla> NaDtn, IReadOnlyList<Dupla> DoModulo);

    public async Task<List<Substituivel>> SubstituiveisAsync(int torneioId)
    {
        var dtns = await _context.Categorias.AsNoTracking()
            .Where(c => c.TorneioId == torneioId && c.PapelDaChave == ChaveDtn.Dtn && c.CategoriaDeOrigemId != null)
            .ToListAsync();
        var lista = new List<Substituivel>();
        foreach (var dtn in dtns)
        {
            var naDtn = await _context.Duplas.AsNoTracking().Include(d => d.Jogador1).Include(d => d.Jogador2)
                .Where(d => d.CategoriaId == dtn.Id).ToListAsync();
            if (naDtn.Count == 0) continue;
            var jaJogaram = await JaJogaramAsync(dtn.Id);
            var trocaveis = naDtn.Where(d => !jaJogaram.Contains(d.Id)).ToList();
            if (trocaveis.Count == 0) continue;

            var pares = naDtn.Select(d => (d.Jogador1Id, d.Jogador2Id)).ToHashSet();
            var doModulo = (await _context.Duplas.AsNoTracking().Include(d => d.Jogador1).Include(d => d.Jogador2)
                    .Where(d => d.CategoriaId == dtn.CategoriaDeOrigemId!.Value).ToListAsync())
                .Where(d => !pares.Contains((d.Jogador1Id, d.Jogador2Id)) && d.Jogador2Id != null)
                .OrderBy(d => d.Id)
                .ToList();
            if (doModulo.Count == 0) continue;
            lista.Add(new Substituivel(dtn, trocaveis, doModulo));
        }
        return lista;
    }

    // Duplas da DTN com jogo já começado ou acabado.
    private async Task<HashSet<int>> JaJogaramAsync(int dtnId) =>
        (await _context.Partidas.AsNoTracking()
            .Where(p => p.CategoriaId == dtnId && p.Status != "Agendada")
            .Select(p => new { p.Dupla1Id, p.Dupla2Id }).ToListAsync())
        .SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).ToHashSet();

    // Devolve o motivo da recusa, ou null quando trocou. Quem chama já conferiu que é organizador.
    public async Task<string?> SubstituirNaDtnAsync(int torneioId, int copiaId, int duplaDoModuloId)
    {
        var alvo = (await SubstituiveisAsync(torneioId))
            .FirstOrDefault(s => s.NaDtn.Any(d => d.Id == copiaId));
        if (alvo == null) return "Essa dupla não está na DTN, ou já jogou — não dá mais pra trocar.";
        var nova = alvo.DoModulo.FirstOrDefault(d => d.Id == duplaDoModuloId);
        if (nova == null) return "Escolha uma dupla do módulo que ainda não esteja na DTN.";

        var copia = await _context.Duplas.FirstAsync(d => d.Id == copiaId);
        copia.Jogador1Id = nova.Jogador1Id;
        copia.Jogador2Id = nova.Jogador2Id;
        copia.LadoJogador1 = nova.LadoJogador1;
        await _context.SaveChangesAsync();
        return null;
    }

    // ── A tela (Details) ────────────────────────────────────────────────────────────────

    // O que o card do Treta mostra. Pendentes só pra quem organiza; troféus pra todo mundo,
    // com o formulário de quem pode escolher (a dupla vencedora enquanto não registrou, e
    // quem organiza sempre).
    public record LinhaDoTrofeu(Decisivo Decisivo, bool PodeEscolher);
    public record Painel(IReadOnlyList<Pendente> Pendentes, IReadOnlyList<LinhaDoTrofeu> Trofeus,
        IReadOnlyDictionary<int, string> Nomes, IReadOnlyList<FinalParaTrocar>? FinaisParaTrocar = null,
        IReadOnlyList<Substituivel>? Substituiveis = null);

    public async Task<Painel?> PainelAsync(int torneioId, int? jogadorLogadoId, bool ehOrganizador)
    {
        if (!await _context.Categorias.AnyAsync(c => c.TorneioId == torneioId
                && (c.PapelDaChave != null || c.DuplasNoDtnPorGrupo > 0)))
            return null;

        var pendentes = ehOrganizador ? await PendentesAsync(torneioId) : new List<Pendente>();
        var finaisParaTrocar = ehOrganizador ? await FinaisParaTrocarAsync(torneioId) : new List<FinalParaTrocar>();
        var substituiveis = ehOrganizador ? await SubstituiveisAsync(torneioId) : new List<Substituivel>();
        var trofeus = (await DecisivosAsync(torneioId))
            .Select(d => new LinhaDoTrofeu(d, ehOrganizador
                || (jogadorLogadoId is int eu && d.Vencedores.Contains(eu) && d.Categoria.TrofeuDtnJogadorId == null)))
            .ToList();
        if (pendentes.Count == 0 && trofeus.Count == 0 && finaisParaTrocar.Count == 0 && substituiveis.Count == 0) return null;

        var ids = pendentes.SelectMany(p => new[] { p.Perdedora1.Jogador1Id, p.Perdedora1.Jogador2Id ?? 0,
                                                    p.Perdedora2.Jogador1Id, p.Perdedora2.Jogador2Id ?? 0 })
            .Concat(trofeus.SelectMany(t => t.Decisivo.Vencedores.Concat(t.Decisivo.Perdedores)))
            .Concat(trofeus.Select(t => t.Decisivo.Categoria.TrofeuDtnJogadorId ?? 0))
            .Concat(finaisParaTrocar.SelectMany(f => new[] { f.Dupla1.Jogador1Id, f.Dupla1.Jogador2Id ?? 0,
                                                            f.Dupla2.Jogador1Id, f.Dupla2.Jogador2Id ?? 0 }))
            .Where(id => id > 0).Distinct().ToList();
        var nomes = await _context.Jogadores.AsNoTracking()
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.NomeNaTela);

        return new Painel(pendentes, trofeus, nomes, finaisParaTrocar, substituiveis);
    }
}
