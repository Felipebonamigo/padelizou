using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers;

// O mural de Desafios: dupla anuncia a semana, outra dupla desafia, o placar fecha e vale
// ranking. Espec completa em DESAFIOS.md, na raiz do repo — mudou a regra, muda LÁ primeiro.
//
// ⚠️ TODA ação começa por `PortaDosDesafios.PodeUsarAsync`. Enquanto o módulo está em
// construção, esconder o item do menu é cortesia; sem esta checagem, /Desafios continuaria
// respondendo pra quem digitasse o endereço na barra.
//
// ⚠️ E nenhuma regra de estado mora aqui: quem responde "pode aceitar?", "pode lançar placar?"
// e "esse prazo já venceu?" é Services/EstadoDoDesafio. São quatro pessoas mexendo na mesma
// linha por caminhos diferentes, e a mesma pergunta é feita na tela e no POST — regra em dois
// lugares diverge, e numa que decide "esse placar conta?" divergir significa ranking errado.
[Authorize]
public class DesafiosController : Controller
{
    private readonly DbPadelContext _context;
    private readonly PortaDosDesafios _porta;
    private readonly FechamentoDoDesafio _fechamento;
    // A montagem do ranking mora fora daqui porque a MESMA tabela também é uma aba do hub de
    // ranking (/Jogadores/Ranking) — ver Services/TelaDoRankingDeDesafios.
    private readonly TelaDoRankingDeDesafios _telaDoRanking;
    // Pela fila de avisos, nunca SMTP inline: publicar mandando e-mail dentro da requisição foi
    // o que deixou "Publicando o torneio…" demorando minutos.
    private readonly IPushNotificationService _push;

    public DesafiosController(DbPadelContext context, PortaDosDesafios porta,
        FechamentoDoDesafio fechamento, TelaDoRankingDeDesafios telaDoRanking,
        IPushNotificationService push)
    {
        _context = context;
        _porta = porta;
        _fechamento = fechamento;
        _telaDoRanking = telaDoRanking;
        _push = push;
    }

    private int MeuId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<bool> PortaAbertaAsync() => await _porta.PodeUsarAsync(MeuId());

    // ─────────────────────────── O MURAL ───────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(int? categoria, int? cidade)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var catalogoDeCidades = await _context.Cidades.ToListAsync();

        // ⚠️ `a.ValeAte == null ||` NÃO PODE FALTAR. Desde que a coluna virou nulável ("até
        // alguém aceitar"), `a.ValeAte >= agora` sozinho é FALSO pro anúncio sem prazo — e o
        // defeito seria mudo: o anúncio existiria, publicado, e simplesmente não apareceria no
        // mural pra ninguém. Vale pra toda consulta que pergunta "ainda está de pé?".
        var anuncios = await AnunciosComTudo()
            .Where(a => a.Status == AnuncioDeDesafio.Publicado
                && a.Jogador2Id != null
                && (a.ValeAte == null || a.ValeAte >= agora))
            // Sem prazo vai pro fim da lista: quem marcou uma semana tem urgência, e é essa
            // dupla que precisa ser vista antes de o prazo dela passar.
            .OrderBy(a => a.ValeAte == null)
            .ThenBy(a => a.ValeAte)
            .ThenByDescending(a => a.CriadoEm)
            .ToListAsync();

        // Filtro na MEMÓRIA porque a regra tem um lado que SQL não expressa bem: lista vazia
        // significa "aceita qualquer um". Um `Any(...)` cru esconderia justamente as duplas
        // mais fáceis de desafiar — as que não restringiram nada.
        if (categoria is > 0)
            anuncios = anuncios
                .Where(a => a.Categorias.Count == 0 || a.Categorias.Any(c => c.CategoriaPadraoId == categoria))
                .ToList();

        if (cidade is > 0)
        {
            // ⚠️ `IdsDaMesma`, e não `== cidade`. "Gravataí", "GRAVATAI" e "Gravatai" são três
            // LINHAS do catálogo e uma cidade só; o filtro que a tela mostra é uma opção
            // agrupada, e comparar com o id dela sozinho esconderia a dupla que escolheu outra
            // grafia. O defeito seria mudo: a tela abre, só vem gente a menos.
            var mesmaCidade = CidadesSemRepetir.IdsDaMesma(cidade.Value, catalogoDeCidades);
            anuncios = anuncios
                .Where(a => a.Cidades.Count == 0 || a.Cidades.Any(c => mesmaCidade.Contains(c.CidadeId)))
                .ToList();
        }

        var retrospecto = await RetrospectoAsync(anuncios, agora);
        var jaDesafiei = await AnunciosQueJaDesafieiAsync(meuId, anuncios.Select(a => a.Id).ToList());
        var cinturoes = await CinturoesPorDuplaAsync();

        var cartoes = anuncios.Select(a => new CartaoDoMural(
            a,
            a.Categorias.Select(c => c.CategoriaPadrao.Nome).OrderBy(n => n).ToList(),
            a.Cidades.Select(c => c.Cidade.Nome).OrderBy(n => n).ToList(),
            a.Clubes.Select(c => c.Clube.Nome).OrderBy(n => n).ToList(),
            retrospecto.GetValueOrDefault(ChaveDaDupla.De(a.Jogador1Id, a.Jogador2Id!.Value)).Jogos,
            retrospecto.GetValueOrDefault(ChaveDaDupla.De(a.Jogador1Id, a.Jogador2Id!.Value)).Vitorias,
            EMeu: a.Jogador1Id == meuId || a.Jogador2Id == meuId,
            JaDesafiei: jaDesafiei.Contains(a.Id),
            Cinturoes: cinturoes.GetValueOrDefault(
                ChaveDaDupla.De(a.Jogador1Id, a.Jogador2Id!.Value)) ?? new List<string>())).ToList();

        var meuAnuncio = await MeuAnuncioVivoAsync(meuId, agora, incluirRascunho: true);

        var vm = new MuralVM(
            cartoes,
            meuAnuncio,
            meuAnuncio?.ConviteToken is { Length: > 0 } t
                ? Url.Action(nameof(Convite), "Desafios", new { token = t }, Request.Scheme)
                : null,
            await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync(),
            CidadesSemRepetir.Agrupar(catalogoDeCidades),
            categoria,
            cidade,
            meuId,
            _porta.EmConstrucao);

        return View(vm);
    }

    // ─────────────────────────── PUBLICAR O ANÚNCIO ───────────────────────────

    [HttpGet]
    public async Task<IActionResult> Publicar()
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var meuId = MeuId();
        var agora = DateTime.Now;

        if (await MeuAnuncioVivoAsync(meuId, agora, incluirRascunho: true) != null)
        {
            TempData["Erro"] = "Você já tem um anúncio nesta semana. Cancele o atual pra publicar outro.";
            return RedirectToAction(nameof(Index));
        }

        return View(await MontarFormularioAsync(meuId, agora));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(int[] categorias, int[] cidades, int[] clubes,
        string? observacao, string prazo, int? parceiroId)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var meuId = MeuId();
        var agora = DateTime.Now;

        if (await MeuAnuncioVivoAsync(meuId, agora, incluirRascunho: true) != null)
        {
            TempData["Erro"] = "Você já tem um anúncio nesta semana.";
            return RedirectToAction(nameof(Index));
        }

        // O parceiro escolhido direto na tela. Null = ninguém escolhido, e a dupla vai fechar
        // pelo link (o caminho antigo, que continua valendo pra quem não sabe o nome de quem
        // vai jogar ainda).
        var parceiro = await ParceiroEscolhidoAsync(parceiroId, meuId);
        if (parceiroId is > 0 && parceiro == null)
        {
            TempData["Erro"] = "Não deu pra incluir essa pessoa como parceiro. "
                + "Ela pode ter desligado os convites de jogo — nesse caso, mande o link.";
            return RedirectToAction(nameof(Publicar));
        }

        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = meuId,
            // ⚠️ DOIS CAMINHOS PRA FECHAR A DUPLA (decisão do Felipe, 12/08/2026):
            //
            //  · PARCEIRO ESCOLHIDO na tela → o anúncio já nasce PUBLICADO, sem pedir licença.
            //    `ParceiroConfirmouEm` fica nulo, e é isso que a tela usa pra dizer aos dois
            //    lados o que está pendente. O parceiro é AVISADO na hora e pode remover.
            //  · NINGUÉM ESCOLHIDO → nasce Rascunho com token, e só vai pro mural quando o
            //    parceiro aceitar pelo link.
            Jogador2Id = parceiro?.Id,
            ParceiroConfirmouEm = null,
            ConviteToken = parceiro == null ? ConviteDoAnuncio.NovoToken() : null,
            Status = parceiro == null ? AnuncioDeDesafio.Rascunho : AnuncioDeDesafio.Publicado,
            ValeAte = PrazoEscolhido(prazo, agora),
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            CriadoEm = agora
        };

        foreach (var id in categorias.Distinct())
            anuncio.Categorias.Add(new AnuncioDesafioCategoria { CategoriaPadraoId = id });
        foreach (var id in cidades.Distinct())
            anuncio.Cidades.Add(new AnuncioDesafioCidade { CidadeId = id });
        foreach (var id in clubes.Distinct())
            anuncio.Clubes.Add(new AnuncioDesafioClube { ClubeId = id });

        _context.AnunciosDeDesafio.Add(anuncio);
        await _context.SaveChangesAsync();

        if (parceiro == null)
        {
            TempData["Sucesso"] = "Desafio criado! Agora mande o link pro seu parceiro confirmar — "
                + "só depois disso ele vai pro mural.";
            return RedirectToAction(nameof(Index));
        }

        // ⚠️ O AVISO NÃO É CORTESIA AQUI: é o que dá sentido a "o parceiro pode remover". A
        // pessoa foi posta num anúncio público que diz a categoria, os clubes e a semana em que
        // ela vai jogar — descobrir isso por acaso, dias depois, seria a mesma coisa que não
        // poder remover. Por isso vai no WhatsApp: é pessoal, é acionável e perde valor amanhã.
        var eu = await _context.Jogadores.FindAsync(meuId);
        var aviso = AvisoDoDesafio.ParceiroIncluido(NomeBonito.ComApelido(eu?.Nome, eu?.Apelido));
        await _push.EnviarParaJogadorAsync(parceiro.Id, aviso.Titulo, aviso.Corpo,
            "/Desafios", AlcanceDoAviso.AppEWhatsApp);

        // Sem gênero na frase: o nome não diz como a pessoa se trata.
        TempData["Sucesso"] = $"Desafio criado com {NomeBonito.Curto(parceiro.Nome)}! "
            + "Já está no mural — avisamos, e quem não quiser participar pode sair da dupla.";
        return RedirectToAction(nameof(Index));
    }

    // Quem pode ser posto numa dupla sem ter pedido.
    //
    // ⚠️ `AceitaConvitesJogo` é respeitado, e não é regra nova: é o MESMO interruptor que já
    // decide quem entra na lista de convite do MarcarJogo e do Grupo. Quem desligou disse que
    // não quer ser puxado pra jogo por outra pessoa — e escolher essa gente direto seria usar a
    // porta que ela fechou. Pra ela, sobra o link, que ela aceita se quiser.
    //
    // Conta excluída (LGPD) também não entra: é o filtro que o BuscaJogador já aplica.
    private async Task<Jogador?> ParceiroEscolhidoAsync(int? parceiroId, int meuId)
    {
        if (parceiroId is not > 0 || parceiroId == meuId) return null;

        return await _context.Jogadores
            .FirstOrDefaultAsync(j => j.Id == parceiroId
                && j.ExcluidoEm == null
                && j.AceitaConvitesJogo);
    }

    // "Esta semana" | "Semana que vem" | "Até alguém aceitar" (nulo = não vence no relógio).
    // Escolha desconhecida cai na semana atual, que é o padrão de menor surpresa: um anúncio
    // que vence sozinho no domingo, e não um que fica no mural pra sempre.
    private static DateTime? PrazoEscolhido(string? prazo, DateTime agora) => prazo switch
    {
        PrazoDoAnuncio.ProximaSemana => SemanaDoDesafio.FimDaSemanaSeguinte(agora),
        PrazoDoAnuncio.AteAlguemAceitar => null,
        _ => SemanaDoDesafio.FimDaSemanaDe(agora),
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarAnuncio(int id)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var meuId = MeuId();
        var anuncio = await _context.AnunciosDeDesafio.FirstOrDefaultAsync(a => a.Id == id);
        if (anuncio == null) return NotFound();
        if (anuncio.Jogador1Id != meuId && anuncio.Jogador2Id != meuId) return Forbid();

        var souOParceiroNaoConfirmado = anuncio.Jogador2Id == meuId && anuncio.ParceiroConfirmouEm == null;

        anuncio.Status = AnuncioDeDesafio.Cancelado;
        anuncio.ConviteToken = null;
        await _context.SaveChangesAsync();

        // O outro lado da dupla precisa saber que o anúncio saiu do ar — ainda mais quando quem
        // removeu foi o parceiro que nunca pediu pra entrar. Sem o aviso, quem publicou
        // continuaria esperando desafio de um anúncio que já não existe.
        var souOCriador = anuncio.Jogador1Id == meuId;
        var outro = souOCriador ? anuncio.Jogador2Id : anuncio.Jogador1Id;
        if (outro != null)
        {
            var eu = await _context.Jogadores.FindAsync(meuId);
            var nome = NomeBonito.ComApelido(eu?.Nome, eu?.Apelido);
            // O texto muda conforme quem saiu: "não quis participar da dupla" sobre o dono do
            // anúncio seria uma frase sem sentido pra quem a recebe.
            var aviso = souOCriador
                ? AvisoDoDesafio.CriadorRemoveuODesafio(nome)
                : AvisoDoDesafio.ParceiroSaiuDaDupla(nome);

            await _push.EnviarParaJogadorAsync(outro.Value, aviso.Titulo, aviso.Corpo,
                "/Desafios", AlcanceDoAviso.SoApp);
        }

        // ⚠️ Nada de desafio morre junto. O anúncio some do mural; o jogo já combinado é
        // compromisso entre quatro pessoas e sobrevive (Desafio.AnuncioDeDesafioId é SetNull).
        TempData["Sucesso"] = souOParceiroNaoConfirmado
            ? "Pronto, você saiu da dupla e o desafio saiu do mural."
            : "Desafio removido. Os jogos que você já aceitou continuam de pé.";
        return RedirectToAction(nameof(Index));
    }

    // O parceiro que foi ESCOLHIDO na tela dizendo "tudo certo". Não destrava nada — o anúncio
    // já está no mural —, mas encerra a cobrança nas duas telas e registra que ele topou.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarParceria(int id)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var meuId = MeuId();
        var anuncio = await _context.AnunciosDeDesafio.FirstOrDefaultAsync(a => a.Id == id);
        if (anuncio == null) return NotFound();

        // Só o parceiro confirma, e só uma vez.
        if (anuncio.Jogador2Id != meuId || anuncio.ParceiroConfirmouEm != null) return Forbid();

        anuncio.ParceiroConfirmouEm = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Confirmado! Bom jogo.";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────── O CONVITE DO PARCEIRO ───────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Convite(string token)
    {
        // Sem login não dá pra aceitar: quem entra na dupla é sempre quem clicou COM A PRÓPRIA
        // CONTA. Manda pro login guardando a volta.
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action(nameof(Convite), new { token }) });

        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var anuncio = await AnunciosComTudo().FirstOrDefaultAsync(a => a.ConviteToken == token);
        var valido = ConviteDoAnuncio.Valido(anuncio, token, agora)
            && anuncio != null && ConviteDoAnuncio.PodeAceitar(anuncio, MeuId());

        var motivo = valido
            ? ""
            : anuncio != null && !ConviteDoAnuncio.PodeAceitar(anuncio, MeuId())
                ? "Esse é o seu próprio anúncio — mande o link pro seu parceiro."
                : ConviteDoAnuncio.MotivoDeNaoValer(anuncio, agora);

        return View(new ConviteVM(
            anuncio,
            token,
            valido,
            motivo,
            anuncio?.Categorias.Select(c => c.CategoriaPadrao.Nome).OrderBy(n => n).ToList() ?? new List<string>(),
            anuncio?.Clubes.Select(c => c.Clube.Nome).OrderBy(n => n).ToList() ?? new List<string>()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AceitarConvite(string token)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var anuncio = await _context.AnunciosDeDesafio.FirstOrDefaultAsync(a => a.ConviteToken == token);

        if (!ConviteDoAnuncio.Valido(anuncio, token, agora) || !ConviteDoAnuncio.PodeAceitar(anuncio!, meuId))
        {
            TempData["Erro"] = ConviteDoAnuncio.MotivoDeNaoValer(anuncio, agora);
            return RedirectToAction(nameof(Index));
        }

        anuncio!.Jogador2Id = meuId;
        // Quem entra pelo LINK disse sim de verdade — diferente de quem foi escolhido na tela.
        anuncio.ParceiroConfirmouEm = agora;
        anuncio.Status = AnuncioDeDesafio.Publicado;
        // Link usado não fecha uma segunda dupla.
        anuncio.ConviteToken = null;
        await _context.SaveChangesAsync();

        var eu = await _context.Jogadores.FindAsync(meuId);
        await _push.EnviarParaJogadorAsync(anuncio.Jogador1Id,
            "Sua dupla está fechada! 🥊",
            $"{NomeBonito.ComApelido(eu?.Nome, eu?.Apelido)} confirmou. Seu anúncio já está no mural.",
            "/Desafios", AlcanceDoAviso.SoApp);

        TempData["Sucesso"] = "Dupla fechada! O anúncio de vocês já está no mural.";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────── DESAFIAR ───────────────────────────

    [HttpGet]
    public async Task<IActionResult> Desafiar(int id)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();

        var alvo = await AnunciosComTudo().FirstOrDefaultAsync(a => a.Id == id);
        if (alvo == null || !SemanaDoDesafio.NoMural(alvo, agora)) return NotFound();

        // Pra desafiar é preciso ter a PRÓPRIA dupla anunciada. É o que dá identidade ao
        // desafiante sem inventar um segundo fluxo de escolher parceiro — e é o que faz o
        // mural crescer: quem quer desafiar publica.
        var meu = await MeuAnuncioComTudoAsync(meuId, agora);
        if (meu == null)
        {
            TempData["Erro"] = "Pra desafiar, publique o anúncio da sua dupla primeiro.";
            return RedirectToAction(nameof(Publicar));
        }

        if (alvo.Jogador1Id == meuId || alvo.Jogador2Id == meuId)
        {
            TempData["Erro"] = "Esse anúncio é seu.";
            return RedirectToAction(nameof(Index));
        }

        var categorias = await CategoriasPossiveisAsync(meu, alvo);
        if (categorias.Count == 0)
        {
            TempData["Erro"] = "Nenhuma categoria em comum com essa dupla.";
            return RedirectToAction(nameof(Index));
        }

        return View(new DesafiarVM(
            alvo,
            categorias,
            await ClubesPossiveisAsync(meu, alvo),
            DuplaNaTela.Nome(alvo.Jogador1, alvo.Jogador2),
            QuandoSugerido(agora),
            SemanaDoDesafio.LimiteParaJogar(meu, alvo, agora)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desafiar(int anuncioId, int categoriaPadraoId, int clubeId,
        DateTime dataHora, string formato, string? observacao)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();

        var alvo = await AnunciosComTudo().FirstOrDefaultAsync(a => a.Id == anuncioId);
        if (alvo == null || !SemanaDoDesafio.NoMural(alvo, agora)) return NotFound();
        if (alvo.Jogador1Id == meuId || alvo.Jogador2Id == meuId) return Forbid();

        var meu = await MeuAnuncioComTudoAsync(meuId, agora);
        if (meu == null)
        {
            TempData["Erro"] = "Pra desafiar, publique o anúncio da sua dupla primeiro.";
            return RedirectToAction(nameof(Publicar));
        }

        // ⚠️ Categoria e clube são reconferidos contra a REGRA, nunca aceitos do formulário: a
        // lista veio da tela, e a tela é do navegador de quem envia. É a mesma disciplina do
        // preço da quadra, que sempre sai do banco.
        var categorias = await CategoriasPossiveisAsync(meu, alvo);
        var clubes = await ClubesPossiveisAsync(meu, alvo);

        if (categorias.All(c => c.Id != categoriaPadraoId) || clubes.All(c => c.Id != clubeId))
        {
            TempData["Erro"] = "Essa dupla não aceita desafio nessa categoria ou nesse clube.";
            return RedirectToAction(nameof(Desafiar), new { id = anuncioId });
        }

        // O jogo tem que caber na semana que as DUAS duplas anunciaram — desafiar pra daqui a
        // três meses não é desafio da semana, é agenda.
        // O menor prazo entre os dois anúncios — e o teto de 30 dias quando nenhum tem prazo
        // ("até alguém aceitar" não é "aceito jogar em abril"). Ver SemanaDoDesafio.
        var limite = SemanaDoDesafio.LimiteParaJogar(meu, alvo, agora);
        if (dataHora <= agora || dataHora > limite)
        {
            TempData["Erro"] = $"Escolha um horário entre agora e {limite:dd/MM 'às' HH'h'mm} — "
                + "é até onde vale o anúncio das duas duplas.";
            return RedirectToAction(nameof(Desafiar), new { id = anuncioId });
        }

        var desafio = new Desafio
        {
            AnuncioDeDesafioId = alvo.Id,
            DesafianteJogador1Id = meu.Jogador1Id,
            DesafianteJogador2Id = meu.Jogador2Id!.Value,
            DesafiadoJogador1Id = alvo.Jogador1Id,
            DesafiadoJogador2Id = alvo.Jogador2Id!.Value,
            CategoriaPadraoId = categoriaPadraoId,
            ClubeId = clubeId,
            DataHora = dataHora,
            Formato = FormatoDoDesafio.Valido(formato),
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Status = Desafio.Proposto,
            PropostoEm = agora
        };

        _context.Desafios.Add(desafio);
        await _context.SaveChangesAsync();

        var minhaDupla = DuplaNaTela.Nome(meu.Jogador1, meu.Jogador2);
        var categoriaNome = categorias.First(c => c.Id == categoriaPadraoId).Nome;
        var clubeNome = clubes.First(c => c.Id == clubeId).Nome;

        // A dupla desafiada TEM o cinturão desta categoria? Então o aviso muda de tom: não é
        // "um desafio chegou", é "seu reinado está em risco" — mesma comparação por chave do
        // Cinturao.Efeito, feita aqui só pra escolher o texto.
        var dono = await _context.ReinadosNoCinturao.FirstOrDefaultAsync(r =>
            r.CategoriaPadraoId == categoriaPadraoId && r.TerminouEm == null);
        bool peloCinturao = dono != null
            && ChaveDaDupla.De(dono.Jogador1Id, dono.Jogador2Id)
               == ChaveDaDupla.De(desafio.DesafiadoJogador1Id, desafio.DesafiadoJogador2Id);

        var aviso = peloCinturao
            ? AvisoDoDesafio.RecebidoPeloCinturao(minhaDupla, categoriaNome, clubeNome, dataHora)
            : AvisoDoDesafio.Recebido(minhaDupla, categoriaNome, clubeNome, dataHora);

        // O ÚNICO aviso destes que vai pro WhatsApp: é pessoal, morre em 48h e a pessoa tem
        // que fazer alguma coisa por causa dele — os três critérios do AlcanceDoAviso.
        foreach (var jogadorId in desafio.LadoDesafiado)
            await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                "/Desafios/Meus", AlcanceDoAviso.AppEWhatsApp);

        TempData["Sucesso"] = "Desafio enviado! Eles têm 48h pra responder.";
        return RedirectToAction(nameof(Meus));
    }

    // ─────────────────────────── O RANKING ───────────────────────────

    [HttpGet]
    public async Task<IActionResult> Ranking(int? categoria, int? clube)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        return View(await _telaDoRanking.MontarAsync(
            MeuId(), _porta.EmConstrucao, DateTime.Now, categoria, clube));
    }

    // ─────────────────────────── MEUS DESAFIOS ───────────────────────────

    [HttpGet]
    public async Task<IActionResult> Meus()
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();

        var desafios = await DesafiosComTudo()
            .Where(d => d.DesafianteJogador1Id == meuId || d.DesafianteJogador2Id == meuId
                || d.DesafiadoJogador1Id == meuId || d.DesafiadoJogador2Id == meuId)
            .OrderByDescending(d => d.DataHora)
            .ToListAsync();

        var linhas = desafios.Select(d => Linha(d, meuId, agora)).ToList();

        // A proposta vencida cai no histórico, não fica pendurada em "recebidos" — o relógio
        // já respondeu por ela (EstadoDoDesafio.PropostaVencida).
        bool Pendente(LinhaDeDesafio l) =>
            l.Desafio.Status == Desafio.Proposto && !EstadoDoDesafio.PropostaVencida(l.Desafio, agora);

        return View(new MeusDesafiosVM(
            linhas.Where(l => Pendente(l) && !l.SouDesafiante).OrderBy(l => l.Desafio.DataHora).ToList(),
            linhas.Where(l => Pendente(l) && l.SouDesafiante).OrderBy(l => l.Desafio.DataHora).ToList(),
            linhas.Where(l => l.Desafio.Status is Desafio.Aceito or Desafio.PlacarLancado)
                .OrderBy(l => l.Desafio.DataHora).ToList(),
            linhas.Where(l => l.Desafio.Status is Desafio.Confirmado or Desafio.Recusado
                    or Desafio.Cancelado or Desafio.EmDisputa or Desafio.SemComparecimento
                || (l.Desafio.Status == Desafio.Proposto && EstadoDoDesafio.PropostaVencida(l.Desafio, agora)))
                .ToList()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Responder(int id, bool aceitar)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var desafio = await DesafiosComTudo().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NotFound();

        if (!EstadoDoDesafio.PodeResponder(desafio, meuId, agora))
        {
            TempData["Erro"] = EstadoDoDesafio.PropostaVencida(desafio, agora)
                ? "Esse desafio expirou — passaram das 48h."
                : "Esse desafio não está mais aguardando resposta.";
            return RedirectToAction(nameof(Meus));
        }

        desafio.Status = aceitar ? Desafio.Aceito : Desafio.Recusado;
        desafio.RespondidoEm = agora;

        // ⚠️ O anúncio SEM PRAZO ("até alguém aceitar") não vence no relógio — quem o tira do
        // mural é este aceite. Sem isto ele ficaria lá pra sempre, e o mural viraria uma lista
        // de duplas que já arrumaram jogo.
        //
        // Só o do LADO DESAFIADO: o anúncio de quem desafiou continua valendo, porque a condição
        // que ele escreveu ("até alguém aceitar o MEU") não aconteceu.
        if (aceitar) await FecharAnuncioSemPrazoAsync(desafio.AnuncioDeDesafioId);

        await _context.SaveChangesAsync();

        var minhaDupla = DuplaNaTela.Nome(desafio.DesafiadoJogador1, desafio.DesafiadoJogador2);
        var aviso = aceitar
            ? AvisoDoDesafio.Aceito(minhaDupla, desafio.Clube.Nome, desafio.DataHora)
            : AvisoDoDesafio.Recusado(minhaDupla);

        foreach (var jogadorId in desafio.LadoDesafiante)
            await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                "/Desafios/Meus", AlcanceDoAviso.SoApp);

        TempData["Sucesso"] = aceitar ? "Desafio aceito! Bom jogo." : "Desafio recusado.";
        return RedirectToAction(nameof(Meus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var meuId = MeuId();
        var desafio = await DesafiosComTudo().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NotFound();

        if (!EstadoDoDesafio.PodeCancelar(desafio, meuId))
        {
            TempData["Erro"] = "Esse desafio não pode mais ser cancelado.";
            return RedirectToAction(nameof(Meus));
        }

        desafio.Status = Desafio.Cancelado;
        await _context.SaveChangesAsync();

        var eu = await _context.Jogadores.FindAsync(meuId);
        var aviso = AvisoDoDesafio.Cancelado(NomeBonito.ComApelido(eu?.Nome, eu?.Apelido), desafio.DataHora);

        // Só o OUTRO lado é avisado: quem cancelou sabe o que fez.
        var outroLado = EstadoDoDesafio.EhDoLadoDesafiante(desafio, meuId)
            ? desafio.LadoDesafiado
            : desafio.LadoDesafiante;

        foreach (var jogadorId in outroLado)
            await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                "/Desafios/Meus", AlcanceDoAviso.SoApp);

        TempData["Sucesso"] = "Desafio cancelado.";
        return RedirectToAction(nameof(Meus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LancarPlacar(int id, int? setsDesafiante, int? setsDesafiado,
        int? gamesDesafiante, int? gamesDesafiado)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var desafio = await DesafiosComTudo().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NotFound();

        if (!EstadoDoDesafio.PodeLancarPlacar(desafio, meuId, agora))
        {
            TempData["Erro"] = desafio.Status == Desafio.Aceito
                ? "O placar só pode ser lançado depois da hora marcada."
                : "Esse desafio não está aguardando placar.";
            return RedirectToAction(nameof(Meus));
        }

        // Set só existe no melhor de 3 — nos outros formatos um número aqui viraria um vencedor
        // decidido por campo que a tela nem mostrou.
        if (!FormatoDoDesafio.PedeSets(desafio.Formato))
        {
            setsDesafiante = null;
            setsDesafiado = null;
        }

        var motivo = EstadoDoDesafio.MotivoParaNaoLancar(setsDesafiante, setsDesafiado,
            gamesDesafiante, gamesDesafiado);
        if (motivo != null)
        {
            TempData["Erro"] = motivo;
            return RedirectToAction(nameof(Meus));
        }

        desafio.SetsDesafiante = setsDesafiante;
        desafio.SetsDesafiado = setsDesafiado;
        desafio.GamesDesafiante = gamesDesafiante;
        desafio.GamesDesafiado = gamesDesafiado;
        desafio.LancadoPorId = meuId;
        desafio.LancadoEm = agora;
        desafio.Status = Desafio.PlacarLancado;
        await _context.SaveChangesAsync();

        var eu = await _context.Jogadores.FindAsync(meuId);
        var aviso = AvisoDoDesafio.PlacarLancado(
            NomeBonito.ComApelido(eu?.Nome, eu?.Apelido), PlacarDoDesafio.Texto(desafio));

        var outroLado = EstadoDoDesafio.EhDoLadoDesafiante(desafio, meuId)
            ? desafio.LadoDesafiado
            : desafio.LadoDesafiante;

        foreach (var jogadorId in outroLado)
            await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                "/Desafios/Meus", AlcanceDoAviso.SoApp);

        TempData["Sucesso"] = "Placar lançado! Se a outra dupla não responder em 72h, ele vale.";
        return RedirectToAction(nameof(Meus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResponderPlacar(int id, bool confirmar, string? motivo)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var desafio = await DesafiosComTudo().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NotFound();

        if (!EstadoDoDesafio.PodeResponderPlacar(desafio, meuId, agora))
        {
            TempData["Erro"] = EstadoDoDesafio.PlacarValeuPeloRelogio(desafio, agora)
                ? "O prazo de 72h passou — esse placar já valeu."
                : "Você não pode responder a esse placar.";
            return RedirectToAction(nameof(Meus));
        }

        if (confirmar)
        {
            // Fechar é gravar o resultado E congelar os pontos, e isso mora num lugar só —
            // o mesmo que o relógio usa quando ninguém responde (Services/FechamentoDoDesafio).
            await _fechamento.FecharAsync(desafio, meuId, agora);

            var aviso = AvisoDoDesafio.PlacarConfirmado(PlacarDoDesafio.Texto(desafio));
            foreach (var jogadorId in desafio.Envolvidos.Where(j => j != meuId).Distinct())
                await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                    "/Desafios/Meus", AlcanceDoAviso.AppSemEmail);

            TempData["Sucesso"] = "Placar confirmado.";
        }
        else
        {
            // ⚠️ Contestado NÃO vira "o outro venceu": ninguém pontua, e a linha fica pro admin
            // resolver. Duas duplas discordando é problema humano, e o sistema não escolhe lado.
            desafio.Status = Desafio.EmDisputa;
            desafio.MotivoDaDisputa = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            await _context.SaveChangesAsync();

            var eu = await _context.Jogadores.FindAsync(meuId);
            var aviso = AvisoDoDesafio.PlacarContestado(NomeBonito.ComApelido(eu?.Nome, eu?.Apelido));
            foreach (var jogadorId in desafio.Envolvidos.Where(j => j != meuId).Distinct())
                await _push.EnviarParaJogadorAsync(jogadorId, aviso.Titulo, aviso.Corpo,
                    "/Desafios/Meus", AlcanceDoAviso.SoApp);

            TempData["Sucesso"] = "Placar contestado. Ninguém pontua até isso ser resolvido.";
        }

        return RedirectToAction(nameof(Meus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarFalta(int id)
    {
        if (!await PortaAbertaAsync()) return NotFound();

        var agora = DateTime.Now;
        var meuId = MeuId();
        var desafio = await DesafiosComTudo().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NotFound();

        if (!EstadoDoDesafio.PodeRegistrarFalta(desafio, meuId, agora))
        {
            TempData["Erro"] = "Não dá pra registrar falta nesse desafio.";
            return RedirectToAction(nameof(Meus));
        }

        desafio.Status = Desafio.SemComparecimento;
        var (desafiante, desafiado) = PontosDoDesafio.SemComparecimento();
        desafio.PontosDesafiante = desafiante;
        desafio.PontosDesafiado = desafiado;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Registrado. Jogo que não aconteceu não vale ponto pra ninguém.";
        return RedirectToAction(nameof(Meus));
    }

    // ─────────────────────────── Consultas ───────────────────────────

    private IQueryable<AnuncioDeDesafio> AnunciosComTudo() =>
        _context.AnunciosDeDesafio
            .Include(a => a.Jogador1)
            .Include(a => a.Jogador2)
            .Include(a => a.Categorias).ThenInclude(c => c.CategoriaPadrao)
            .Include(a => a.Cidades).ThenInclude(c => c.Cidade)
            .Include(a => a.Clubes).ThenInclude(c => c.Clube);

    private IQueryable<Desafio> DesafiosComTudo() =>
        _context.Desafios
            .Include(d => d.DesafianteJogador1)
            .Include(d => d.DesafianteJogador2)
            .Include(d => d.DesafiadoJogador1)
            .Include(d => d.DesafiadoJogador2)
            .Include(d => d.CategoriaPadrao)
            .Include(d => d.Clube);

    // O anúncio que vale pra mim AGORA. `incluirRascunho` porque a tela do mural precisa
    // mostrar o meu mesmo antes de o parceiro confirmar — é lá que fica o link do convite.
    private async Task<AnuncioDeDesafio?> MeuAnuncioVivoAsync(int meuId, DateTime agora, bool incluirRascunho)
    {
        var statusQueValem = incluirRascunho
            ? new[] { AnuncioDeDesafio.Publicado, AnuncioDeDesafio.Rascunho }
            : new[] { AnuncioDeDesafio.Publicado };

        // ⚠️ Mesma armadilha do mural: sem o `== null`, o anúncio "até alguém aceitar" não seria
        // encontrado — e a pessoa poderia publicar um segundo, além de não conseguir desafiar
        // ninguém (é este método que dá a identidade da dupla desafiante).
        return await AnunciosComTudo()
            .Where(a => (a.Jogador1Id == meuId || a.Jogador2Id == meuId)
                && statusQueValem.Contains(a.Status)
                && (a.ValeAte == null || a.ValeAte >= agora))
            .OrderByDescending(a => a.CriadoEm)
            .FirstOrDefaultAsync();
    }

    // Pra desafiar, só serve anúncio COMPLETO: publicado e com o parceiro dentro.
    private async Task<AnuncioDeDesafio?> MeuAnuncioComTudoAsync(int meuId, DateTime agora)
    {
        var meu = await MeuAnuncioVivoAsync(meuId, agora, incluirRascunho: false);
        return meu?.Jogador2Id == null ? null : meu;
    }

    // As categorias que as DUAS duplas aceitam. Lista vazia de um lado = ele aceita todas, e é
    // por isso que a interseção não pode ser um `Intersect` seco.
    private async Task<List<CategoriaPadrao>> CategoriasPossiveisAsync(AnuncioDeDesafio meu, AnuncioDeDesafio alvo)
    {
        var catalogo = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();

        var minhas = meu.Categorias.Select(c => c.CategoriaPadraoId).ToHashSet();
        var delas = alvo.Categorias.Select(c => c.CategoriaPadraoId).ToHashSet();

        return catalogo
            .Where(c => (minhas.Count == 0 || minhas.Contains(c.Id))
                && (delas.Count == 0 || delas.Contains(c.Id)))
            .ToList();
    }

    // Os clubes possíveis.
    //
    // ⚠️ A CIDADE NÃO ENTRA NESTA CONTA, e isso é conhecido: `Clube.CidadeId` existe e nada
    // preenche a coluna (ver DESAFIOS.md, "Buracos conhecidos"). Filtrar clube por cidade hoje
    // devolveria lista vazia, e o defeito seria mudo — a tela abre e não vem nenhum clube. A
    // cidade filtra o MURAL (que lê a cidade digitada pelas pessoas); o clube sai daqui.
    private async Task<List<Clube>> ClubesPossiveisAsync(AnuncioDeDesafio meu, AnuncioDeDesafio alvo)
    {
        var minhas = meu.Clubes.Select(c => c.ClubeId).ToHashSet();
        var delas = alvo.Clubes.Select(c => c.ClubeId).ToHashSet();

        var query = _context.Clubes.AsQueryable();

        // Interseção quando os dois restringiram; a lista de quem restringiu quando só um o
        // fez; o catálogo inteiro quando nenhum dos dois se importou.
        if (minhas.Count > 0 && delas.Count > 0)
        {
            var comuns = minhas.Intersect(delas).ToList();
            query = query.Where(c => comuns.Contains(c.Id));
        }
        else if (minhas.Count > 0) query = query.Where(c => minhas.Contains(c.Id));
        else if (delas.Count > 0) query = query.Where(c => delas.Contains(c.Id));

        return await query.OrderBy(c => c.Nome).ToListAsync();
    }

    // Quantos desafios cada dupla do mural já fechou, e quantos venceu.
    //
    // ⚠️ Lê pelo MESMO filtro do ranking (RankingDeDesafios.QueContam): o cartão do mural e a
    // tabela do ranking não podem discordar sobre o retrospecto da mesma dupla — quem vê "8V" no
    // cartão e "6V" na tabela deixa de acreditar nos dois.
    private async Task<Dictionary<string, (int Jogos, int Vitorias)>> RetrospectoAsync(
        List<AnuncioDeDesafio> anuncios, DateTime agora)
    {
        var pessoas = anuncios
            .SelectMany(a => new[] { a.Jogador1Id, a.Jogador2Id ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (pessoas.Count == 0) return new();

        var confirmados = await RankingDeDesafios
            .QueContam(_context.Desafios.AsNoTracking(), agora)
            .Where(d => pessoas.Contains(d.DesafianteJogador1Id) || pessoas.Contains(d.DesafiadoJogador1Id))
            .ToListAsync();

        var conta = new Dictionary<string, (int Jogos, int Vitorias)>();

        foreach (var d in confirmados)
        {
            var vencedor = EstadoDoDesafio.LadoVencedor(d);

            Somar(ChaveDaDupla.De(d.DesafianteJogador1Id, d.DesafianteJogador2Id), vencedor == 1);
            Somar(ChaveDaDupla.De(d.DesafiadoJogador1Id, d.DesafiadoJogador2Id), vencedor == 2);
        }

        return conta;

        void Somar(string chave, bool venceu)
        {
            var atual = conta.GetValueOrDefault(chave);
            conta[chave] = (atual.Jogos + 1, atual.Vitorias + (venceu ? 1 : 0));
        }
    }

    // O anúncio "até alguém aceitar" achou jogo e sai do mural. Anúncio com data segue como
    // estava: a dupla escolheu ficar disponível a semana toda, e pode jogar mais de uma vez.
    private async Task FecharAnuncioSemPrazoAsync(int? anuncioId)
    {
        if (anuncioId == null) return;

        var anuncio = await _context.AnunciosDeDesafio.FindAsync(anuncioId.Value);
        if (anuncio == null || !SemanaDoDesafio.SemPrazo(anuncio)) return;
        if (anuncio.Status != AnuncioDeDesafio.Publicado) return;

        anuncio.Status = AnuncioDeDesafio.Fechado;
    }

    // Quais categorias cada dupla tem na mão hoje, pra o cartão do mural mostrar o selo.
    private async Task<Dictionary<string, List<string>>> CinturoesPorDuplaAsync()
    {
        var donos = await _context.ReinadosNoCinturao
            .AsNoTracking()
            .Include(r => r.CategoriaPadrao)
            .Where(r => r.TerminouEm == null)
            .ToListAsync();

        return donos
            .GroupBy(r => ChaveDaDupla.De(r.Jogador1Id, r.Jogador2Id))
            .ToDictionary(g => g.Key, g => g.Select(r => r.CategoriaPadrao.Nome).OrderBy(n => n).ToList());
    }

    private async Task<HashSet<int>> AnunciosQueJaDesafieiAsync(int meuId, List<int> anuncioIds)
    {
        if (anuncioIds.Count == 0) return new();

        var ids = await _context.Desafios
            .AsNoTracking()
            .Where(d => d.AnuncioDeDesafioId != null
                && anuncioIds.Contains(d.AnuncioDeDesafioId.Value)
                && (d.DesafianteJogador1Id == meuId || d.DesafianteJogador2Id == meuId)
                && (d.Status == Desafio.Proposto || d.Status == Desafio.Aceito))
            .Select(d => d.AnuncioDeDesafioId!.Value)
            .ToListAsync();

        return ids.ToHashSet();
    }

    private LinhaDeDesafio Linha(Desafio d, int meuId, DateTime agora)
    {
        var souDesafiante = EstadoDoDesafio.EhDoLadoDesafiante(d, meuId);

        var adversarios = souDesafiante
            ? DuplaNaTela.Nome(d.DesafiadoJogador1, d.DesafiadoJogador2)
            : DuplaNaTela.Nome(d.DesafianteJogador1, d.DesafianteJogador2);

        return new LinhaDeDesafio(
            d,
            souDesafiante,
            adversarios,
            EstadoDoDesafio.StatusNaTela(d, agora),
            EstadoDoDesafio.PodeResponder(d, meuId, agora),
            EstadoDoDesafio.PodeCancelar(d, meuId),
            EstadoDoDesafio.PodeLancarPlacar(d, meuId, agora),
            EstadoDoDesafio.PodeResponderPlacar(d, meuId, agora),
            EstadoDoDesafio.TempoRestanteParaResponder(d, agora),
            EstadoDoDesafio.TempoRestanteParaContestar(d, agora));
    }

    // O formulário nasce marcado com o que a pessoa já respondeu no perfil — mas o que é
    // gravado é uma cópia no anúncio. Ver o comentário de AnuncioDeDesafio.
    private async Task<PublicarAnuncioVM> MontarFormularioAsync(int meuId, DateTime agora)
    {
        return new PublicarAnuncioVM(
            await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync(),
            CidadesSemRepetir.Agrupar(await _context.Cidades.ToListAsync()),
            await _context.Clubes.ParaEscolher().ToListAsync(),
            await _context.JogadorCategorias.Where(x => x.JogadorId == meuId)
                .Select(x => x.CategoriaPadraoId).ToListAsync(),
            await _context.JogadorCidades.Where(x => x.JogadorId == meuId)
                .Select(x => x.CidadeId).ToListAsync(),
            await _context.JogadorClubes.Where(x => x.JogadorId == meuId)
                .Select(x => x.ClubeId).ToListAsync(),
            SemanaDoDesafio.FimDaSemanaSugerido(agora),
            SemanaDoDesafio.SemanaJaEstaAcabando(agora),
            await ParceirosPossiveisAsync(meuId));
    }

    // Quem pode ser escolhido como parceiro na tela.
    //
    // ⚠️ O filtro é o MESMO `AceitaConvitesJogo` que o Marcar Jogo e o Grupo já usam pra montar
    // lista de convite. Quem desligou disse que não quer ser puxado pra jogo por outra pessoa —
    // e colocá-lo numa dupla sem pedir seria entrar pela porta que ele fechou. Pra ele, sobra o
    // link. Conta excluída também fica de fora, pela régua do BuscaJogador.
    private async Task<List<Jogador>> ParceirosPossiveisAsync(int meuId) =>
        await _context.Jogadores
            .AsNoTracking()
            .Where(j => j.Id != meuId && j.ExcluidoEm == null && j.AceitaConvitesJogo)
            .OrderBy(j => j.Nome)
            .ToListAsync();

    // Sugestão de horário: amanhã às 20h, que é quando o padel amador acontece. Serve pra o
    // campo não nascer vazio — a pessoa muda em dois toques.
    private static DateTime QuandoSugerido(DateTime agora) =>
        agora.Date.AddDays(1).AddHours(20);
}
