
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

public class JogadoresController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;
    private readonly IPushNotificationService _push;

    // O ranking do parceiro saiu daqui em 08/08/2026, a pedido dele: o perfil não mostra mais
    // a posição da pessoa lá, então esta tela não fala mais com aquela API (ver Perfil).
    public JogadoresController(DbPadelContext context, IEstatisticasService estatisticas,
        IPushNotificationService push)
    {
        _context = context;
        _estatisticas = estatisticas;
        _push = push;
    }

    // Quem fez a coisa, pro texto do aviso. Uma consulta só, projetada — carregar o Jogador
    // inteiro pra pegar nome e apelido traria foto, CPF e telefone junto, sem necessidade.
    private async Task<(string? Nome, string? Apelido)> QuemSouAsync(int jogadorId) =>
        await _context.Jogadores
            .Where(j => j.Id == jogadorId)
            .Select(j => new ValueTuple<string?, string?>(j.Nome, j.Apelido))
            .FirstOrDefaultAsync();

    // `desafios` vem por [FromServices], e não pelo construtor, pra não obrigar as ~20 outras
    // ações deste controller (e cada teste que as monta) a conhecer uma dependência que só o
    // perfil usa. Mesmo arranjo do IPadelimetroService lá embaixo.
    [HttpGet]
    public async Task<IActionResult> Perfil(int id, [FromServices] PortaDosDesafios desafios)
    {
        // Busca o jogador (com clubes e dias/horários preferidos, pro bloco "joga em")
        var jogador = await _context.Jogadores
            .Include(j => j.JogadorClubes).ThenInclude(c => c.Clube)
            .Include(j => j.JogadorDiasHorarios)
            .FirstOrDefaultAsync(j => j.Id == id);
        if (jogador == null) return NotFound();

        int? meuId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        ViewBag.MeuId = meuId;

        bool souEuMesmo = meuId.HasValue && meuId.Value == id;

        // ⚠️ DUAS COISAS DIFERENTES MORAVAM NA MESMA CHAVE, e por isso o perfil inteiro
        // sumia pra quem só queria esconder o telefone (decisão do Felipe, 10/08/2026):
        //
        // · CONTA EXCLUÍDA (LGPD) — aqui o perfil FECHA MESMO. Quem pediu pra sair deixou de
        //   ser identificável; o que sobra dela são os resultados dos jogos, que são dado de
        //   quatro pessoas e seguem nas chaves (ver Services/ExclusaoDeConta).
        // · PERFIL PRIVADO — é sobre CONTATO: esconde Instagram e WhatsApp, e mais nada.
        //   Título, pontos, ranking e clube continuam públicos, porque já estão no ranking e
        //   na chave do torneio; escondê-los aqui não esconderia nada de ninguém.
        //
        // A exclusão liga `PerfilPrivado` junto (o contato tem que sumir também), então o
        // que decide o bloqueio total é o `ExcluidoEm` — não a chave que a pessoa aperta.
        if (jogador.Excluido && !souEuMesmo)
        {
            ViewBag.PerfilBloqueado = true;
            return View((jogador, new List<Dupla>()));
        }

        // A regra mora em Services/ContatoDoJogador desde 10/08/2026, porque o aviso de jogo
        // também mostra WhatsApp agora — e duas cópias dela discordariam um dia.
        ViewBag.ContatoEscondido = !ContatoDoJogador.PodeVerContato(jogador, meuId);

        // Busca todas as duplas em que este jogador participou
        var historicoDuplas = await _context.Duplas
            .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
            .Where(d => d.Jogador1Id == id || d.Jogador2Id == id)
            .OrderByDescending(d => d.Categoria.Torneio.DataInicio)
            .ToListAsync();

        // Cálculos de Estatísticas (via serviço central, inclui "caiu na chave")
        var resumo = await _estatisticas.ObterResumoJogadorAsync(id);
        ViewBag.Pontos = resumo.Pontos;
        ViewBag.TotalTorneios = resumo.TotalTorneios;
        ViewBag.Titulos = resumo.Titulos;
        ViewBag.Finais = resumo.Finais;
        ViewBag.Semis = resumo.Semis;
        ViewBag.Quartas = resumo.Quartas;
        ViewBag.CaiuNaChave = resumo.CaiuNaChave;
        ViewBag.Vitorias = resumo.Vitorias;

        // Prateleira de troféus: um por MATERIAL da categoria (ver Services/TrofeuDeMaterial).
        // Sai do histórico que já foi carregado acima — o total de títulos sozinho tratava um
        // título na Open e um na 7ª como a mesma coisa.
        ViewBag.TrofeusPorMaterial = TrofeuDeMaterial.Contar(
            historicoDuplas.Select(d => (
                (string?)d.Categoria.Nome,
                (string?)d.UltimaFase,
                FormatoDoTorneio.EhAmericano(d.Categoria.Torneio?.Formato))));

        // Categoria prevista (nível comprovado): categoria mais forte em que o jogador
        // chegou à final/foi campeão. Base da regra anti-sandbagging. Null se ainda não comprovou.
        ViewBag.CategoriaPrevista = await _estatisticas.ObterNivelComprovadoJogadorAsync(id);

        // ---- Padelímetro (fase 1: só MOSTRAR, nada trava — RANKING.md) ----
        // A régua é única, mas o RÓTULO da faixa depende da escada: o mesmo 500 é "5ª" na
        // masculina e "2ª" na feminina. A escada vem da inscrição não-mista mais recente
        // (mista fica fora da escada de propósito); sem nenhuma, masculina por padrão.
        if (jogador.Padelimetro is int nivelPadelimetro)
        {
            var categoriaRecente = historicoDuplas
                .Select(d => d.Categoria.Nome)
                .FirstOrDefault(n => !FaixasDePadelimetro.ForaDaEscada(n));
            bool reguaFeminina = FaixasDePadelimetro.EhFeminina(categoriaRecente);

            ViewBag.PadelimetroFaixa = FaixasDePadelimetro.DoNivel(nivelPadelimetro, reguaFeminina);
            ViewBag.PadelimetroFalta = FaixasDePadelimetro.FaltaPraSubir(nivelPadelimetro, reguaFeminina);
            ViewBag.PadelimetroEmCalibracao = Padelimetro.EmCalibracao(jogador.JogosDePadelimetro);
            ViewBag.PadelimetroExtrato = await _context.HistoricosDePadelimetro
                .Where(h => h.JogadorId == id)
                .OrderByDescending(h => h.CriadoEm).ThenByDescending(h => h.Id)
                .Take(8)
                .ToListAsync();
        }

        // ---- Desafios (fase 2 do DESAFIOS.md) ----
        //
        // ⚠️ Passa pela MESMA porta do módulo: enquanto ele está em construção, nem o dono do
        // perfil vê a linha — senão o retrospecto de uma feature invisível apareceria num perfil
        // que é público. E o filtro do que conta sai de RankingDeDesafios.QueContam, o mesmo do
        // ranking e do mural: três leituras discordando sobre o retrospecto da mesma pessoa é
        // como se deixa de acreditar nas três.
        if (await desafios.PodeUsarAsync(meuId))
        {
            var confirmadosDela = await RankingDeDesafios
                .QueContam(_context.Desafios.AsNoTracking(), DateTime.Now)
                .Where(d => d.DesafianteJogador1Id == id || d.DesafianteJogador2Id == id
                    || d.DesafiadoJogador1Id == id || d.DesafiadoJogador2Id == id)
                .ToListAsync();

            ViewBag.ResumoDeDesafios = RankingDeDesafios.DoJogador(confirmadosDela, id);

            // 🥊 Os cinturões que esta pessoa tem HOJE. É o selo mais alto do perfil — e é da
            // DUPLA: some no dia em que ela perde na quadra ou deixa de defender.
            ViewBag.CinturoesDoJogador = await _context.ReinadosNoCinturao
                .AsNoTracking()
                .Include(r => r.CategoriaPadrao)
                .Where(r => r.TerminouEm == null && (r.Jogador1Id == id || r.Jogador2Id == id))
                .Select(r => r.CategoriaPadrao.Nome)
                .ToListAsync();
        }

        // Conquistas/badges: público, aparece pra qualquer visitante do perfil
        ViewBag.Conquistas = await _estatisticas.ObterConquistasAsync(id);

        // Os dois números da rede. Ficam DEPOIS da saída do perfil privado, como o resto:
        // quantas pessoas seguem alguém também é dado de quem fechou o perfil.
        ViewBag.QuantosSeguidores = await _context.SeguidoresJogador.CountAsync(s => s.SeguidoId == id);
        ViewBag.QuantosSeguindo = await _context.SeguidoresJogador.CountAsync(s => s.SeguidorId == id);

        // Evolução de pontos mês a mês (gráfico do perfil).
        ViewBag.Evolucao = await _estatisticas.ObterEvolucaoJogadorAsync(id);

        // Elogios recebidos, agregados por tipo (só os tipos que têm pelo menos 1).
        var elogiosRecebidos = await _context.Elogios
            .Where(e => e.ParaJogadorId == id)
            .GroupBy(e => e.Tipo)
            .Select(g => new { Tipo = g.Key, Quantidade = g.Count(), DeJogadorIds = g.Select(e => e.DeJogadorId) })
            .ToListAsync();
        ViewBag.Elogios = elogiosRecebidos
            .Select(g => CatalogoElogios.Obter(g.Tipo) is { } t
                ? new ElogioResumoVM
                {
                    Codigo = t.Codigo,
                    Titulo = t.Titulo,
                    Icone = t.Icone,
                    Quantidade = g.Quantidade,
                    EuDei = meuId.HasValue && g.DeJogadorIds.Contains(meuId.Value),
                }
                : null)
            .Where(v => v != null)
            .OrderByDescending(v => v!.Quantidade)
            .ToList();
        ViewBag.CatalogoElogios = CatalogoElogios.Todos;

        // Comentários no perfil: públicos, quem pode apagar é o autor, o dono do perfil ou um admin.
        ViewBag.Comentarios = await _context.ComentariosPerfil
            .Include(c => c.Autor)
            .Where(c => c.PerfilId == id)
            .OrderByDescending(c => c.CriadoEm)
            .ToListAsync();
        bool souAdmin = User.FindFirstValue("IsAdmin") == "true";
        ViewBag.PodeModerarComentarios = souEuMesmo || souAdmin;

        // O meu comentário volta pro formulário: é um por perfil, então comentar de novo é
        // editar — e um campo vazio faria parecer que dá pra escrever um segundo.
        ViewBag.MeuComentario = meuId.HasValue
            ? ((List<Padelizou.Models.ComentarioPerfil>)ViewBag.Comentarios)
                .FirstOrDefault(c => c.AutorId == meuId.Value)
            : null;

        if (souEuMesmo)
        {
            // É o próprio perfil: mostra parceiros de sempre e os confrontos (jogou contra / rivais)
            var confrontos = await _estatisticas.ObterConfrontosAsync(id);
            var parceiros = await _estatisticas.ObterParceirosAsync(id);
            ViewBag.Confrontos = confrontos;
            ViewBag.Parceiros = parceiros;
            // Destaques reaproveitam as listas já carregadas (sem recarregar partidas).
            ViewBag.Destaques = EstatisticasService.MontarDestaques(parceiros, confrontos);
        }
        else if (meuId.HasValue)
        {
            // É o perfil de outra pessoa: mostra o confronto entre eu e ela
            ViewBag.MeuConfronto = await _estatisticas.ObterHeadToHeadAsync(meuId.Value, id);
            ViewBag.EstouSeguindo = await _context.SeguidoresJogador
                .AnyAsync(s => s.SeguidorId == meuId.Value && s.SeguidoId == id);
        }

        // ⚠️ AQUI SE CONSULTAVA A POSIÇÃO DA PESSOA NO RANKING DO PARCEIRO, pra um selo no
        // cabeçalho do perfil. Saiu em 08/08/2026 A PEDIDO DELES (ver Services/MarcaDoRanking):
        // o dado é deles, e num perfil ele não presta serviço a ninguém além de publicar o
        // ranking do parceiro de graça. Onde continua valendo é na inscrição do torneio que
        // contratou a conferência — lá o dado decide alguma coisa.
        //
        // A CHAMADA saiu junto, e não só a tela: o perfil é das páginas mais visitadas do
        // site, então esconder o selo e seguir consultando gastaria a cota deles pra jogar a
        // resposta no lixo.

        return View((jogador, historicoDuplas));
    }

    // Pra onde volta quem apertou o botão de seguir. São três telas com o mesmo botão e
    // nenhuma delas deveria "sumir" depois do clique:
    // - a busca manda a URL inteira (`voltarPara`), porque o que ela precisa preservar são os
    //   filtros e a página — cair no perfil de quem foi seguido apagaria o resultado montado;
    // - a rede manda só o id do dono da lista;
    // - o perfil não manda nada e volta pra si mesmo.
    //
    // ⚠️ `IsLocalUrl` não é ornamento: sem ele o formulário viraria trampolim pra jogar quem
    // clica em site de fora.
    private IActionResult VoltarDoSeguir(int id, int? voltarParaRede, string? voltarPara)
    {
        if (!string.IsNullOrWhiteSpace(voltarPara) && Url.IsLocalUrl(voltarPara))
            return LocalRedirect(voltarPara);

        return voltarParaRede is int redeDe
            ? RedirectToAction(nameof(Rede), new { id = redeDe })
            : RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seguir(int id, int? voltarParaRede = null, string? voltarPara = null)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (meuId != id)
        {
            var jaSigo = await _context.SeguidoresJogador.AnyAsync(s => s.SeguidorId == meuId && s.SeguidoId == id);
            if (!jaSigo)
            {
                _context.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = meuId, SeguidoId = id });
                await _context.SaveChangesAsync();

                // ⚠️ Só quando o vínculo NASCE. Deixar de seguir e seguir de novo é o caminho
                // óbvio de transformar isto em cutucão repetido na mesma pessoa.
                var eu = await QuemSouAsync(meuId);
                var texto = AvisoSocial.NovoSeguidor(eu.Nome, eu.Apelido);

                // O destino é o perfil de QUEM SEGUIU, não o meu: a pergunta que o aviso
                // levanta é "quem é essa pessoa?", e é lá que ela se responde (e que dá pra
                // seguir de volta).
                await _push.EnviarParaJogadorAsync(id, texto.Titulo, texto.Corpo,
                    Url.Action("Perfil", "Jogadores", new { id = meuId }), AlcanceDoAviso.AppSemEmail);
            }
        }

        return VoltarDoSeguir(id, voltarParaRede, voltarPara);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeixarDeSeguir(int id, int? voltarParaRede = null, string? voltarPara = null)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var vinculo = await _context.SeguidoresJogador
            .FirstOrDefaultAsync(s => s.SeguidorId == meuId && s.SeguidoId == id);
        if (vinculo != null)
        {
            _context.SeguidoresJogador.Remove(vinculo);
            await _context.SaveChangesAsync();
        }

        // Deixar de seguir NÃO avisa ninguém, de propósito: é a única das quatro ações do
        // perfil que a outra pessoa preferia não saber, e avisar transformaria um botão
        // discreto num recado constrangedor.
        return VoltarDoSeguir(id, voltarParaRede, voltarPara);
    }

    // QUEM TE SEGUE e QUEM VOCÊ SEGUE, as duas listas na mesma tela.
    //
    // Seguir existe desde o começo e nunca teve onde ser visto: dava pra apertar o botão no
    // perfil de alguém e pronto — nem quem seguia sabia a própria lista, nem quem era seguido
    // sabia por quem. É público de propósito (o perfil e o histórico já são), com a MESMA
    // trava do perfil privado.
    [HttpGet]
    public async Task<IActionResult> Rede(int id, string? aba = null)
    {
        var jogador = await _context.Jogadores
            .Where(j => j.Id == id)
            .Select(j => new { j.Id, j.Nome, j.Apelido, j.ExcluidoEm })
            .FirstOrDefaultAsync();
        if (jogador == null) return NotFound();

        int? meuId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        bool souEuMesmo = meuId.HasValue && meuId.Value == id;

        // A trava aqui é a MESMA do perfil, e por isso é o `ExcluidoEm`: conta excluída tem o
        // perfil fechado, e a rede seria uma porta lateral pro que ele acabou de fechar.
        // Perfil privado NÃO entra nessa conta — quem esconde o telefone não some do site.
        if (jogador.ExcluidoEm != null && !souEuMesmo) return RedirectToAction(nameof(Perfil), new { id });

        // Quem EU sigo, pra saber onde cabe o "Seguir de volta". Vazio pra visitante deslogado
        // — ele não segue ninguém, e nenhum botão faz sentido pra ele.
        var euSigo = meuId.HasValue
            ? (await _context.SeguidoresJogador
                .Where(s => s.SeguidorId == meuId.Value)
                .Select(s => s.SeguidoId)
                .ToListAsync()).ToHashSet()
            : new HashSet<int>();

        // Mais recente primeiro, com o nome como desempate: CriadoEm nasceu depois de parte
        // dos vínculos, e sem o segundo critério a lista trocaria de ordem entre dois cliques.
        var seguidores = await _context.SeguidoresJogador
            .Where(s => s.SeguidoId == id)
            .OrderByDescending(s => s.CriadoEm).ThenBy(s => s.Seguidor.Nome)
            .Select(s => new PessoaNaRedeVM
            {
                Id = s.Seguidor.Id,
                Nome = s.Seguidor.Nome,
                Apelido = s.Seguidor.Apelido,
                Foto = s.Seguidor.FotoPerfil,
                Cidade = s.Seguidor.Cidade,
                Estado = s.Seguidor.Estado,
                Desde = s.CriadoEm,
            })
            .ToListAsync();

        var seguindo = await _context.SeguidoresJogador
            .Where(s => s.SeguidorId == id)
            .OrderByDescending(s => s.CriadoEm).ThenBy(s => s.Seguido.Nome)
            .Select(s => new PessoaNaRedeVM
            {
                Id = s.Seguido.Id,
                Nome = s.Seguido.Nome,
                Apelido = s.Seguido.Apelido,
                Foto = s.Seguido.FotoPerfil,
                Cidade = s.Seguido.Cidade,
                Estado = s.Seguido.Estado,
                Desde = s.CriadoEm,
            })
            .ToListAsync();

        foreach (var p in seguidores.Concat(seguindo)) p.EuSigo = euSigo.Contains(p.Id);

        return View(new RedeDoJogadorVM
        {
            JogadorId = jogador.Id,
            NomeDoJogador = NomeBonito.ComApelido(jogador.Nome, jogador.Apelido),
            SouEu = souEuMesmo,
            Seguidores = seguidores,
            Seguindo = seguindo,
            AbrirEmSeguindo = string.Equals(aba, "seguindo", StringComparison.OrdinalIgnoreCase),
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DarElogio(int id, string tipo)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var escolhido = CatalogoElogios.Obter(tipo);
        if (meuId == id || escolhido == null) return RedirectToAction("Perfil", new { id });

        // É UM elogio por pessoa, e ele é trocável: clicar em outro muda a escolha em vez de
        // empilhar. Antes dava pra marcar os 18 no mesmo perfil, e aí o número do badge dizia
        // "quem clicou mais" em vez de "quantas pessoas acham isso".
        var meuElogio = await _context.Elogios
            .FirstOrDefaultAsync(e => e.DeJogadorId == meuId && e.ParaJogadorId == id);

        if (meuElogio == null)
        {
            _context.Elogios.Add(new Elogio { DeJogadorId = meuId, ParaJogadorId = id, Tipo = tipo });
            await _context.SaveChangesAsync();

            // ⚠️ Avisa só o elogio NOVO. A troca abaixo não avisa: é a MESMA pessoa mexendo no
            // MESMO elogio, e mandar "fulano te elogiou" a cada troca contaria a mesma coisa
            // três vezes — além de deixar o mural do outro à mercê de quem fica trocando.
            var eu = await QuemSouAsync(meuId);
            var texto = AvisoSocial.Elogio(eu.Nome, eu.Apelido, escolhido.Titulo);
            await _push.EnviarParaJogadorAsync(id, texto.Titulo, texto.Corpo,
                Url.Action("Perfil", "Jogadores", new { id }), AlcanceDoAviso.AppSemEmail);
        }
        else if (meuElogio.Tipo != tipo)
        {
            // Trocar calado faria o elogio anterior sumir da tela sem explicação — a pessoa
            // pensaria que perdeu o clique, e clicaria de novo.
            var anterior = CatalogoElogios.Obter(meuElogio.Tipo)?.Titulo ?? meuElogio.Tipo;
            meuElogio.Tipo = tipo;
            meuElogio.CriadoEm = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = $"Trocamos seu elogio de \"{anterior}\" para \"{escolhido.Titulo}\".";
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverElogio(int id)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Sem casar pelo tipo: é um só, e o pedido aqui é "tira o meu". Casar pelo tipo faria
        // o botão não fazer nada quando a página estivesse aberta desde antes de uma troca.
        var elogio = await _context.Elogios
            .FirstOrDefaultAsync(e => e.DeJogadorId == meuId && e.ParaJogadorId == id);
        if (elogio != null)
        {
            _context.Elogios.Remove(elogio);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ComentarPerfil(int id, string texto)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        texto = (texto ?? "").Trim();

        // ⚠️ O aviso sai só do comentário NOVO, e por isso a decisão é tomada aqui dentro e
        // usada depois do SaveChanges: editar pra corrigir um "voce" sem acento não é notícia
        // nenhuma pro dono do perfil, e avisaria de novo a cada letra trocada.
        bool avisarDoComentario = false;

        if (meuId == id)
        {
            TempData["Erro"] = "Você não pode comentar no seu próprio perfil.";
        }
        else if (string.IsNullOrWhiteSpace(texto))
        {
            TempData["Erro"] = "Escreva alguma coisa antes de comentar.";
        }
        else if (texto.Length > 500)
        {
            TempData["Erro"] = "Comentário muito longo (máximo 500 caracteres).";
        }
        else if (FiltroPalavroes.EhOfensivo(texto))
        {
            TempData["Erro"] = "Esse comentário parece ter linguagem ofensiva — não é permitido aqui. Revise e tente de novo.";
        }
        else
        {
            // Um comentário por pessoa em cada perfil: comentar de novo EDITA o que ela já
            // escreveu. Sem isso, quem quisesse corrigir uma frase acabava com dois textos
            // parecidos no perfil do outro — e só o dono do perfil podia apagar o primeiro.
            var meuComentario = await _context.ComentariosPerfil
                .FirstOrDefaultAsync(c => c.AutorId == meuId && c.PerfilId == id);

            if (meuComentario == null)
            {
                _context.ComentariosPerfil.Add(new ComentarioPerfil { AutorId = meuId, PerfilId = id, Texto = texto });
                avisarDoComentario = true;
            }
            else if (meuComentario.Texto != texto)
            {
                meuComentario.Texto = texto;
                meuComentario.CriadoEm = DateTime.Now;

                // Texto editado é texto novo: a denúncia anterior era sobre o que estava
                // escrito antes, e mantê-la deixaria o admin julgando uma frase que não
                // existe mais. Se o novo texto também for ofensivo, denuncia-se de novo.
                meuComentario.DenunciadoEm = null;
                meuComentario.DenunciadoPorId = null;

                TempData["Sucesso"] = "Comentário atualizado.";
            }

            await _context.SaveChangesAsync();

            if (avisarDoComentario)
            {
                // O aviso sai DEPOIS de gravar: comentário que não entrou no banco não pode
                // gerar recado dizendo que entrou.
                var eu = await QuemSouAsync(meuId);
                var aviso = AvisoSocial.Comentario(eu.Nome, eu.Apelido, texto);
                await _push.EnviarParaJogadorAsync(id, aviso.Titulo, aviso.Corpo,
                    Url.Action("Perfil", "Jogadores", new { id }), AlcanceDoAviso.AppSemEmail);
            }
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DenunciarComentario(int comentarioId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var comentario = await _context.ComentariosPerfil.FindAsync(comentarioId);
        if (comentario == null) return NotFound();

        // O autor não denuncia o próprio comentário — ele pode apagá-lo direto.
        // Denúncia repetida não sobrescreve a primeira: a fila do admin ordena por
        // DenunciadoEm, e re-carimbar empurraria o comentário pro fim da fila.
        if (comentario.AutorId != meuId && comentario.DenunciadoEm == null)
        {
            comentario.DenunciadoEm = DateTime.Now;
            comentario.DenunciadoPorId = meuId;
            await _context.SaveChangesAsync();
        }

        TempData["Sucesso"] = "Obrigado pelo aviso — um administrador vai revisar esse comentário.";
        return RedirectToAction("Perfil", new { id = comentario.PerfilId });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverComentario(int comentarioId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        bool souAdmin = User.FindFirstValue("IsAdmin") == "true";
        var comentario = await _context.ComentariosPerfil.FindAsync(comentarioId);
        int perfilId = comentario?.PerfilId ?? 0;

        // Só o autor, o dono do perfil ou um admin pode apagar — nunca um terceiro qualquer.
        if (comentario != null && (comentario.AutorId == meuId || comentario.PerfilId == meuId || souAdmin))
        {
            _context.ComentariosPerfil.Remove(comentario);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id = perfilId });
    }

    // Busca de jogadores: nome + categoria + cidade/estado + clube, tudo combinável.
    // É a tela de "achar parceiro" — por isso os filtros são os mesmos critérios que
    // alguém usa pra escolher com quem jogar.
    [HttpGet]
    public async Task<IActionResult> Buscar(string? q, int? categoriaId, string? estado, string? cidade, int? clubeId, int pagina = 1)
    {
        var vm = new BuscaJogadoresVM
        {
            Termo = q,
            CategoriaId = categoriaId,
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpper(),
            Cidade = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim(),
            ClubeId = clubeId,
            MeuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null,
        };

        // Opções dos selects. As cidades já saem filtradas pelo estado escolhido.
        var (estados, cidades) = await _estatisticas.ObterLocaisDisponiveisAsync(vm.Estado);
        vm.Estados = estados;
        vm.Cidades = cidades;
        // Escrita igual à da lista, senão a cidade escolhida não fica marcada no select.
        vm.Cidade = CidadesSemRepetir.Canonizar(new[] { vm.Cidade }, cidades).FirstOrDefault();
        vm.Categorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();
        vm.Clubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

        // Sem filtro nenhum a busca lista TODO MUNDO (pedido do Felipe, 29/07/2026): quem
        // abre a tela querendo "ver quem tem por aqui" não deveria precisar adivinhar um
        // nome primeiro. A paginação logo abaixo é o que torna isso barato.
        var query = _context.Jogadores.AsQueryable();

        // Nome, apelido ou CPF — mesma regra do resto do sistema (Services/BuscaJogador).
        query = BuscaJogador.Filtrar(query, vm.Termo);

        // ⚠️ PERFIL PRIVADO NÃO FILTRA NADA AQUI, e isso é decisão do Felipe (10/08/2026):
        // "todos os dados públicos aparecem mesmo para quem tem perfil privado — se foi
        // campeão, pontos do ranking, etc.; o perfil privado é pra evitar ver o Instagram, o
        // WhatsApp da pessoa". Resultado de padel é resultado de padel: ele já está no
        // ranking, na chave do torneio e no histórico do parceiro. A chave é sobre CONTATO.
        //
        // Já saiu daqui uma vez uma trava que escondia cidade/categoria/clube de quem é
        // privado e tirava a pessoa dos filtros — é o engano fácil de cometer de novo, e o
        // teste `Perfil_privado_nao_muda_a_busca` existe pra barrar a volta dele.

        if (vm.Estado != null)
            query = query.Where(j => j.Estado != null && j.Estado.ToUpper() == vm.Estado);

        // ⚠️ Casa com TODAS as grafias da cidade escolhida, não com o texto exato: a opção do
        // select virou uma só ("Gravataí"), e comparar por igualdade esconderia da busca quem
        // digitou "GRAVATAI" no cadastro — exatamente quem esta tela existe pra achar.
        if (vm.Cidade != null)
        {
            var grafias = (await _estatisticas.ObterGrafiasDasCidadesAsync(new[] { vm.Cidade }))
                .Select(g => g.ToUpper()).ToList();
            query = query.Where(j => j.Cidade != null && grafias.Contains(j.Cidade.ToUpper()));
        }

        // Categoria e clube vivem em tabelas de ligação, e "sem nenhuma linha" significa
        // "aceita qualquer uma" (ver JogadorCategoria/JogadorClube) — quem não declarou
        // preferência entra no resultado em vez de sumir da busca.
        if (vm.CategoriaId != null)
        {
            query = query.Where(j =>
                !_context.JogadorCategorias.Any(c => c.JogadorId == j.Id)
                || _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == vm.CategoriaId));
        }

        if (vm.ClubeId != null)
        {
            query = query.Where(j =>
                !_context.JogadorClubes.Any(c => c.JogadorId == j.Id)
                || _context.JogadorClubes.Any(c => c.JogadorId == j.Id && c.ClubeId == vm.ClubeId));
        }

        vm.TotalEncontrado = await query.CountAsync();

        // Página fora do intervalo não dá erro: vai pra mais próxima que existe. Link velho
        // de "página 9" continua funcionando depois que a base encolher.
        vm.TotalPaginas = Math.Max(1, (int)Math.Ceiling(vm.TotalEncontrado / (double)BuscaJogadoresVM.TamanhoDaPagina));
        vm.Pagina = Math.Clamp(pagina, 1, vm.TotalPaginas);

        // A página corta pela ordem ALFABÉTICA (que o banco sabe ordenar); o selo "combina"
        // e os pontos reordenam só DENTRO da página — pontos vêm de um cálculo em memória e
        // ordenar o total por eles obrigaria a carregar todo mundo, desfazendo a paginação.
        var jogadores = await query
            .Include(j => j.Time)
            .OrderBy(j => j.Nome)
            .ThenBy(j => j.Id)
            .Skip((vm.Pagina - 1) * BuscaJogadoresVM.TamanhoDaPagina)
            .Take(BuscaJogadoresVM.TamanhoDaPagina)
            .ToListAsync();

        var ids = jogadores.Select(j => j.Id).ToList();
        var pontos = await _estatisticas.ObterPontosPorJogadorAsync(ids);

        // Categorias e clubes de todos os achados em duas consultas, não uma por jogador.
        var catsPorJogador = (await _context.JogadorCategorias
                .Where(c => ids.Contains(c.JogadorId))
                .Select(c => new { c.JogadorId, Nome = c.CategoriaPadrao.Nome })
                .ToListAsync())
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());

        var clubesPorJogador = (await _context.JogadorClubes
                .Where(c => ids.Contains(c.JogadorId))
                .Select(c => new { c.JogadorId, Nome = c.Clube.Nome })
                .ToListAsync())
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());

        // Quem DECLAROU a categoria/clube filtrado sobe pro topo com selo. Sem isso o
        // filtro pareceria quebrado: como quase ninguém preenche preferência, todo mundo
        // entra pela regra do "sem linha = aceita qualquer um".
        var declararamCategoria = vm.CategoriaId == null
            ? new HashSet<int>()
            : (await _context.JogadorCategorias
                .Where(c => ids.Contains(c.JogadorId) && c.CategoriaPadraoId == vm.CategoriaId)
                .Select(c => c.JogadorId).ToListAsync()).ToHashSet();

        var declararamClube = vm.ClubeId == null
            ? new HashSet<int>()
            : (await _context.JogadorClubes
                .Where(c => ids.Contains(c.JogadorId) && c.ClubeId == vm.ClubeId)
                .Select(c => c.JogadorId).ToListAsync()).ToHashSet();

        bool filtraPreferencia = vm.CategoriaId != null || vm.ClubeId != null;

        // Quem EU já sigo, entre os desta página. Uma consulta só, restrita aos 30 da página:
        // é o que decide o botão do card sem carregar a minha lista de seguidos inteira.
        var jaSigo = vm.MeuId == null
            ? new HashSet<int>()
            : (await _context.SeguidoresJogador
                .Where(s => s.SeguidorId == vm.MeuId.Value && ids.Contains(s.SeguidoId))
                .Select(s => s.SeguidoId).ToListAsync()).ToHashSet();

        vm.Resultados = jogadores.Select(j => new JogadorEncontradoVM
        {
            Jogador = j,
            EuSigo = jaSigo.Contains(j.Id),
            Pontos = pontos.GetValueOrDefault(j.Id),
            Time = j.Time?.Nome,
            Categorias = catsPorJogador.GetValueOrDefault(j.Id) ?? new List<string>(),
            Clubes = clubesPorJogador.GetValueOrDefault(j.Id) ?? new List<string>(),
            Declarou = filtraPreferencia
                && (vm.CategoriaId == null || declararamCategoria.Contains(j.Id))
                && (vm.ClubeId == null || declararamClube.Contains(j.Id)),
        })
        .OrderByDescending(r => r.Declarou)
        .ThenByDescending(r => r.Pontos)
        .ThenBy(r => r.Jogador.Nome)
        .ToList();

        vm.FiltraPreferencia = filtraPreferencia;
        vm.QtdDeclarou = vm.Resultados.Count(r => r.Declarou);

        return View(vm);
    }

    // Histórico completo de confrontos entre o jogador logado e um adversário.
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Confronto(int oponenteId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (meuId == oponenteId) return RedirectToAction(nameof(Perfil), new { id = meuId });

        if (!await _context.Jogadores.AnyAsync(j => j.Id == oponenteId)) return NotFound();

        var h2h = await _estatisticas.ObterHeadToHeadAsync(meuId, oponenteId);
        return View(h2h);
    }
    [HttpGet]
    public async Task<IActionResult> Ranking(int? clubeId, int? torneioId, string[]? cidade, string? estado, string? periodo,
        [FromServices] IPadelimetroService padelimetro,
        [FromServices] IRankingAmericanoService rankingAmericano)
    {
        // 1. RANKING POR CLUBE
        if (clubeId.HasValue)
        {
            var duplasDoClube = await _context.Duplas
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.Torneio.ClubeId == clubeId)
                .ToListAsync();

            // Se quiser ver por clube, a View deve ser "RankingPorClube"
            return View("RankingPorClube", duplasDoClube);
        }

        // 2. RANKING CONSOLIDADO (padrão): tudo calculado a partir de resultados de
        //    torneio. Substitui o antigo "ranking global" baseado em PontuacaoGlobal
        //    (campo manual que nada de torneio atualizava).
        // O seletor "ver ranking de um torneio" é PÚBLICO, então ele obedece a mesma régua da
        // vitrine: torneio que ainda espera aprovação, oculto ou cancelado não pode aparecer
        // aqui. Sem esse filtro a lista mostrava TUDO que existe no banco — foi assim que um
        // torneio de teste cancelado apareceu na tela pra qualquer visitante.
        ViewBag.TorneiosList = (await _context.Torneios
                .OrderByDescending(t => t.DataInicio)
                .ToListAsync())
            .Where(t => PermissaoDeOrganizador.ApareceNaVitrine(t)
                        && !CancelamentoDoTorneio.EstaCancelado(t.Status))
            .ToList();

        var hub = await _estatisticas.ObterRankingHubAsync(cidade, estado, periodo);

        // Opções dos selects de cidade/estado (cidades já filtradas pelo estado escolhido).
        // ⚠️ `somenteQuemJogouTorneio`: esta página é ranking, e ranking aqui só existe a
        // partir de resultado de torneio. Cidade sem ninguém que jogou é opção que só sabe
        // devolver tabela vazia — e era por essa porta que entravam na lista os apelidos e as
        // grafias soltas que cada um digita no cadastro.
        var (estados, cidades) = await _estatisticas.ObterLocaisDisponiveisAsync(estado, somenteQuemJogouTorneio: true);
        hub.EstadosDisponiveis = estados;
        hub.CidadesDisponiveis = cidades;

        // O que veio na URL passa a ser escrito como a lista escreve: link antigo com
        // `?cidade=GRAVATAI` mostraria o chip "GRAVATAI" e o select ofereceria "Gravataí" ao
        // lado — a mesma cidade duas vezes na mesma linha.
        hub.Cidades = CidadesSemRepetir.Canonizar(hub.Cidades, cidades);

        // Abas Padelímetro e Ranking Americano (RANKING.md): as duas respeitam o mesmo filtro
        // regional do hub. O Americano é ranking PRÓPRIO — não soma com o oficial, e por isso
        // vem de um serviço separado em vez de virar mais uma consulta do EstatisticasService.
        var doLocal = await _estatisticas.ObterJogadoresDoLocalAsync(cidade, estado);
        hub.Padelimetro = await padelimetro.ListarRankingAsync(doLocal);
        var americano = await rankingAmericano.ListarAsync(doLocal);
        hub.AmericanoIndividual = americano.Individual;
        hub.AmericanoDuplas = americano.Duplas;

        // 3. RANKING DE UM TORNEIO: exibido embutido NESTA mesma página (não abre outra tela).
        if (torneioId.HasValue)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio != null)
            {
                hub.TorneioSelecionadoId = torneio.Id;
                hub.TorneioSelecionadoNome = torneio.Nome;
                hub.RankingTorneio = await _estatisticas.ObterRankingDoTorneioAsync(torneio.Id);
            }
        }

        return View("Ranking", hub);
    }

}
