using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Diagnostics;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    public class HomeController : Controller
    {
        // Quantos avisos novos cabem no card da Home. Três é o que dá pra ler de relance sem
        // empurrar o próximo jogo pra baixo da dobra — o resto está a um toque em "Ver todos".
        private const int QuantosAvisosNaHome = 3;

        private readonly DbPadelContext _context;
        private readonly IEstatisticasService _estatisticas;

        public HomeController(DbPadelContext context, IEstatisticasService estatisticas)
        {
            _context = context;
            _estatisticas = estatisticas;
        }

        public async Task<IActionResult> Index()
        {
            // Torneio oculto não aparece na home (mesma regra da listagem de Torneios —
            // antes a home ignorava o Oculto e vazava torneio restrito na vitrine).
            // Cancelado também não: a vitrine é pra quem pode se inscrever.
            //
            // ⚠️ E torneio SEM APROVAÇÃO também não (07/08/2026). A home é a vitrine mais
            // visível que existe: se a aprovação segurasse só a listagem, quem criasse um
            // torneio inventado apareceria na primeira tela do site mesmo assim.
            var ativos = await _context.Torneios
                .Where(t => !t.Oculto && t.AprovadoEm != null && t.Status != "Finalizado"
                            && t.Status != CancelamentoDoTorneio.Status)
                .OrderBy(t => t.DataInicio)
                .ToListAsync();

            var vm = new HomeVM
            {
                Abertos = ativos.Where(t => t.Status == "Inscrições Abertas").Take(6).ToList(),
                EmAndamento = ativos.Where(t => t.Status != "Inscrições Abertas").ToList(),
                TotalJogadores = await _context.Jogadores.CountAsync(),
                TorneiosRealizados = await _context.Torneios.CountAsync(t => t.Status == "Finalizado"),
                JogosDisputados = await _context.Partidas.CountAsync(p => p.VencedorId != null),

                // Só o que um admin publicou. O feedback nasce invisível e continua invisível
                // até alguém ler e liberar — a home nunca busca por "todos os feedbacks".
                Depoimentos = await _context.FeedbacksSite
                    .Where(f => f.Exibir)
                    .OrderByDescending(f => f.ExibidoEm)
                    .Take(6)
                    .Select(f => new DepoimentoVM
                    {
                        PrimeiroNome = f.Anonimo ? "" : f.Jogador.Nome,
                        Cidade = f.Anonimo ? null : f.Jogador.Cidade,
                        Nota = f.Nota,
                        Texto = f.Texto,
                        Anonimo = f.Anonimo
                    })
                    .ToListAsync(),
            };

            // O corte do primeiro nome fica fora da consulta: Split não traduz pra SQL.
            foreach (var d in vm.Depoimentos)
            {
                d.PrimeiroNome = d.PrimeiroNome.Split(' ')[0];
            }

            if (User.Identity?.IsAuthenticated == true
                && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var jogadorId))
            {
                await PreencherParteLogadaAsync(vm, jogadorId);
            }

            return View(vm);
        }

        // "Hoje no seu padel": tudo consulta pequena e indexada por jogador — a home é a
        // página mais aberta do sistema, não pode pesar.
        private async Task PreencherParteLogadaAsync(HomeVM vm, int jogadorId)
        {
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return; // cookie válido de conta que não existe mais

            vm.PrimeiroNome = jogador.Nome.Split(' ')[0];
            vm.Onboarding = await _estatisticas.ObterOnboardingAsync(jogadorId);

            // O campo SEXO nasceu em 08/08/2026, então todo mundo que já tinha conta está sem
            // ele. O convite pra preencher fica aqui, na Home, porque é a tela por onde todo
            // mundo passa — esperar a pessoa descobrir sozinha no perfil significaria descobrir
            // na hora de se inscrever numa Mista, que é tarde.
            //
            // Some sozinho quando ela preenche: aviso que não tem fim vira paisagem.
            vm.FaltaInformarSexo = !SexoDoJogador.Informou(jogador);

            // O QUE CHEGOU DESDE A ÚLTIMA VISITA, na primeira tela. A caixa de avisos é o
            // único canal que não depende de entrega nenhuma (push alcança 5 aparelhos em
            // 154, o e-mail já teve a cota estourada, o WhatsApp depende de um chip) — mas
            // até aqui ela só era encontrada por quem abrisse o menu e reparasse na bolinha.
            //
            // ⚠️ A Home NÃO marca como lido: quem marca é abrir /Notificacoes. Se marcasse,
            // o card sumiria na visita seguinte com a pessoa nunca tendo lido o aviso — e o
            // sino apagaria junto.
            vm.TotalAvisosNovos = await _context.AvisosDoJogador
                .CountAsync(a => a.JogadorId == jogadorId && a.LidaEm == null);

            if (vm.TotalAvisosNovos > 0)
            {
                vm.AvisosNovos = await _context.AvisosDoJogador
                    .Where(a => a.JogadorId == jogadorId && a.LidaEm == null)
                    .OrderByDescending(a => a.CriadoEm)
                    .ThenByDescending(a => a.Id)
                    .Take(QuantosAvisosNaHome)
                    .ToListAsync();
            }

            // Próximo jogo de torneio com hora e quadra definidas. A margem de 2h pra trás
            // cobre o jogo atrasado do dia: ainda é "o próximo" até alguém finalizar.
            var corte = DateTime.Now.AddHours(-2);
            vm.ProximoJogo = await _context.Partidas
                .Where(p => p.HorarioPrevisto != null && p.HorarioPrevisto >= corte
                         && p.Status != "Finalizada"
                         // ⚠️ Dupla-TIME fora. Na categoria de times o `Jogador1Id` é o
                         // ORGANIZADOR que cadastrou o time, não quem joga — sem este filtro
                         // o dono do torneio abria a Home e via "seu próximo jogo" de uma
                         // partida em que ele não entra em quadra, num torneio em que ele nem
                         // está inscrito. (E o adversário saía como "Bonamigo e", porque time
                         // não tem Jogador2.) A mesma armadilha já estava resolvida no aviso
                         // de "seu jogo é o próximo" — ver AvisosDoDiaDeJogo.JogadoresDa.
                         //
                         // Pela COLUNA `NomeTime`, não por `EhTime`: aquilo é propriedade
                         // calculada em C# e o EF não a traduz pra SQL — a consulta estouraria
                         // em runtime, que é o jeito mais caro de descobrir.
                         && p.Dupla1.NomeTime == null && p.Dupla2.NomeTime == null
                         && (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId ||
                             p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId))
                .OrderBy(p => p.HorarioPrevisto)
                .Select(p => new ProximoJogoVM
                {
                    TorneioId = p.Categoria.TorneioId,
                    Torneio = p.Categoria.Torneio.Nome,
                    Fase = p.Fase,
                    Categoria = p.Categoria.Nome,
                    Horario = p.HorarioPrevisto!.Value,
                    Quadra = p.NomeQuadra,
                    Adversarios = (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId)
                        // Apelido quando existir — é como o jogador reconhece o adversário.
                        ? (p.Dupla2.Jogador1.Apelido ?? p.Dupla2.Jogador1.Nome) + " e " + (p.Dupla2.Jogador2!.Apelido ?? p.Dupla2.Jogador2.Nome)
                        : (p.Dupla1.Jogador1.Apelido ?? p.Dupla1.Jogador1.Nome) + " e " + (p.Dupla1.Jogador2!.Apelido ?? p.Dupla1.Jogador2.Nome),
                })
                .FirstOrDefaultAsync();

            // Compromissos: aula que vou ter, aula que vou dar e quadra reservada.
            // (Jogo semanal de grupo fica de fora — é registrado depois que acontece.)
            var agora = DateTime.Now;
            var compromissos = new List<CompromissoVM>();

            compromissos.AddRange(await _context.Aulas
                .Where(a => a.AlunoId == jogadorId && a.DataHora >= agora
                         && (a.Status == "Pendente" || a.Status == "Confirmada"))
                .Select(a => new CompromissoVM
                {
                    Data = a.DataHora,
                    Titulo = "Aula com Prof. " + a.Professor.Nome,
                    Subtitulo = a.LocalAula.Nome + " — " + a.Status,
                    Icone = "bi-mortarboard",
                    Controller = "Aulas", Action = "MinhasAulas",
                })
                .ToListAsync());

            if (jogador.IsProfessor)
            {
                compromissos.AddRange(await _context.Aulas
                    .Where(a => a.ProfessorId == jogadorId && a.DataHora >= agora
                             && (a.Status == "Pendente" || a.Status == "Confirmada"))
                    .Select(a => new CompromissoVM
                    {
                        Data = a.DataHora,
                        Titulo = "Aula para " + (a.Aluno != null ? a.Aluno.Nome : a.NomeAlunoAvulso ?? "aluno avulso"),
                        Subtitulo = a.LocalAula.Nome,
                        Icone = "bi-person-video3",
                        Controller = "Aulas", Action = "MinhaAgenda",
                    })
                    .ToListAsync());
            }

            compromissos.AddRange(await _context.MarcacoesJogo
                .Where(m => m.JogadorId == jogadorId && m.Status == "Confirmada" && m.DataHora >= agora)
                .Select(m => new CompromissoVM
                {
                    Data = m.DataHora,
                    Titulo = "Quadra em " + m.Clube.Nome,
                    Subtitulo = m.QuadraClube.Nome + " — " + m.DuracaoMinutos + " min",
                    Icone = "bi-calendar2-week",
                    Controller = "MarcarJogo", Action = "MinhasMarcacoes",
                })
                .ToListAsync());

            vm.Compromissos = compromissos.OrderBy(c => c.Data).Take(3).ToList();

            vm.JogosDaSemana = await ObterJogosDaSemanaAsync(jogadorId);

            // Torneios em que estou dentro (dupla ou americano) e que ainda não terminaram.
            // Cancelado sai daqui junto com o finalizado — não é compromisso. Quem estava
            // inscrito não fica sabendo por sumiço: o cancelamento avisa um a um, e por
            // WhatsApp (ver TorneiosController.CancelarTorneio).
            var meusTorneios = await _context.Duplas
                .Where(d => (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                         && d.Categoria.Torneio.Status != "Finalizado"
                         && d.Categoria.Torneio.Status != CancelamentoDoTorneio.Status)
                .Select(d => new MeuTorneioVM
                {
                    Torneio = d.Categoria.Torneio,
                    Categoria = d.Categoria.Nome,
                    ListaDeEspera = d.EmListaDeEspera,
                })
                .ToListAsync();

            meusTorneios.AddRange(await _context.InscricoesAmericanas
                .Where(i => i.JogadorId == jogadorId && i.Categoria.Torneio.Status != "Finalizado"
                            && i.Categoria.Torneio.Status != CancelamentoDoTorneio.Status)
                .Select(i => new MeuTorneioVM
                {
                    Torneio = i.Categoria.Torneio,
                    Categoria = i.Categoria.Nome,
                    ListaDeEspera = i.EmListaDeEspera,
                })
                .ToListAsync());

            vm.MeusTorneios = meusTorneios
                .GroupBy(m => m.Torneio.Id).Select(g => g.First())
                .OrderBy(m => m.Torneio.Status == "Inscrições Abertas") // em andamento primeiro
                .ThenBy(m => m.Torneio.DataInicio)
                .ToList();

            // O que eu já acompanho não repete na vitrine geral.
            var meusIds = vm.MeusTorneios.Select(m => m.Torneio.Id).ToHashSet();
            vm.EmAndamento = vm.EmAndamento.Where(t => !meusIds.Contains(t.Id)).ToList();
            vm.Abertos = vm.Abertos.Where(t => !meusIds.Contains(t.Id)).ToList();

            await PreencherPapeisAsync(vm, jogador);
        }

        // Jogo fixo das panelinhas nos próximos 7 dias — o atalho que faltava pra confirmar
        // presença sem passar por Grupos → grupo → Jogo da Semana.
        //
        // ⚠️ Aqui NÃO se cria sessão. A criação é sob demanda na tela da semana, e a Home é a
        // página mais aberta do sistema: gerar linha a cada visita encheria o banco de sessões
        // de gente que só passou pela porta. Grupo sem sessão ainda aparece — com o horário
        // calculado do dia fixo — e a sessão nasce quando a pessoa clica.
        private async Task<List<JogoDaSemanaVM>> ObterJogosDaSemanaAsync(int jogadorId)
        {
            var meusGrupos = await _context.JogadoresGrupo
                .Where(jg => jg.JogadorId == jogadorId
                          && jg.GrupoPrivado.DiaSemanaFixo != null && jg.GrupoPrivado.HorarioFixo != null)
                .Select(jg => new
                {
                    jg.GrupoPrivado.Id,
                    jg.GrupoPrivado.Nome,
                    Clube = jg.GrupoPrivado.Clube != null ? jg.GrupoPrivado.Clube.Nome : null,
                    Dia = jg.GrupoPrivado.DiaSemanaFixo!.Value,
                    Hora = jg.GrupoPrivado.HorarioFixo!.Value,
                    jg.GrupoPrivado.VagasMaximas,
                })
                .ToListAsync();

            var jogos = meusGrupos
                .Select(g => new JogoDaSemanaVM
                {
                    GrupoId = g.Id,
                    Grupo = g.Nome,
                    Clube = g.Clube,
                    DataHora = SessaoGrupoService.ProximaOcorrencia(g.Dia, g.Hora),
                    Vagas = g.VagasMaximas,
                })
                .ToDictionary(j => (j.GrupoId, j.DataHora));

            // As sessões que já existem mandam no que aparece: elas trazem o meu RSVP, quantos
            // já confirmaram e — no caso do convidado avulso — um jogo de grupo em que eu nem
            // sou membro. Data fora do padrão (jogo remarcado) também só chega por aqui.
            var limite = DateTime.Now.AddDays(7);
            var minhasSessoes = await _context.ConfirmacoesSessao
                .Where(c => c.JogadorId == jogadorId
                         && c.Sessao.DataHora >= DateTime.Now && c.Sessao.DataHora <= limite)
                .Select(c => new
                {
                    c.Sessao.Id,
                    c.Sessao.GrupoId,
                    Grupo = c.Sessao.Grupo.Nome,
                    c.Sessao.DataHora,
                    Clube = c.Sessao.Grupo.Clube != null ? c.Sessao.Grupo.Clube.Nome : null,
                    c.Sessao.Grupo.VagasMaximas,
                    MeuStatus = c.Status,
                    c.Avulso,
                    Confirmados = c.Sessao.Confirmacoes.Count(x => x.Status == "Confirmado"),
                })
                .ToListAsync();

            foreach (var s in minhasSessoes)
            {
                jogos[(s.GrupoId, s.DataHora)] = new JogoDaSemanaVM
                {
                    GrupoId = s.GrupoId,
                    Grupo = s.Grupo,
                    DataHora = s.DataHora,
                    Clube = s.Clube,
                    MeuStatus = s.MeuStatus,
                    Confirmados = s.Confirmados,
                    Vagas = s.VagasMaximas,
                    Convidado = s.Avulso,
                };
            }

            return jogos.Values
                .Where(j => j.DataHora <= limite)
                .OrderBy(j => j.DataHora)
                .ToList();
        }

        // Painéis de quem também trabalha com padel. São independentes: quem é professor E
        // organizador E dono de clube vê os três empilhados, sem precisar caçar no menu.
        private async Task PreencherPapeisAsync(HomeVM vm, Jogador jogador)
        {
            var hoje = DateTime.Today;
            var amanha = hoje.AddDays(1);
            var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
            int jogadorId = jogador.Id;

            // ----- Professor -----
            if (jogador.IsProfessor)
            {
                var minhasAulas = await _context.Aulas
                    .Include(a => a.Aluno)
                    .Include(a => a.LocalAula)
                    .Where(a => a.ProfessorId == jogadorId)
                    .ToListAsync();

                vm.Professor = new PainelProfessorHomeVM
                {
                    SolicitacoesPendentes = minhasAulas.Count(a => a.Status == "Pendente" && a.DataHora >= hoje),
                    AulasHoje = minhasAulas.Count(a => a.DataHora >= hoje && a.DataHora < amanha
                                                    && a.Status != "Cancelada" && a.Status != "Recusada"),
                    AulasNaSemana = minhasAulas.Count(a => a.DataHora >= hoje && a.DataHora < hoje.AddDays(7)
                                                        && a.Status != "Cancelada" && a.Status != "Recusada"),
                    RecebidoNoMes = minhasAulas
                        .Where(a => a.Status == "Realizada" && a.DataHora >= inicioMes)
                        .Sum(a => a.Preco),
                    // Já confirmado e ainda por dar: é o que entra se ninguém desmarcar.
                    AReceber = minhasAulas
                        .Where(a => a.Status == "Confirmada" && a.DataHora >= DateTime.Now)
                        .Sum(a => a.Preco),
                    ProximasAulas = minhasAulas
                        .Where(a => a.DataHora >= DateTime.Now.AddHours(-1)
                                 && (a.Status == "Pendente" || a.Status == "Confirmada"))
                        .OrderBy(a => a.DataHora)
                        .Take(5)
                        .Select(a => new AulaDoDiaVM
                        {
                            AulaId = a.Id,
                            DataHora = a.DataHora,
                            Aluno = a.Aluno?.ComoChamar ?? a.NomeAlunoAvulso ?? "Aluno avulso",
                            Local = a.LocalAula.Nome,
                            Status = a.Status,
                            Preco = a.Preco,
                            CelularAluno = a.Aluno?.Celular ?? a.TelefoneAlunoAvulso,
                        })
                        .ToList(),
                };
            }

            // ----- Organizador de torneio -----
            var idsOrganizados = await _context.TorneioOrganizadores
                .Where(o => o.JogadorId == jogadorId)
                .Select(o => o.TorneioId)
                .ToListAsync();

            if (idsOrganizados.Count > 0)
            {
                // ⚠️ QUEM CONTA INSCRITO É `Services/QuantosInscritos` (Felipe, 08/08/2026:
                // "essa conta aqui de 30 inscritos não parece correta").
                //
                // A conta que morava aqui somava LINHAS DE `Duplas` com LINHAS DE
                // `InscricoesAmericanas` — e no Americano individual a tabela `Dupla` tem uma
                // linha por PARCERIA DE RODADA, não por inscrição. O "Americano das Gurias do
                // Padel", com 10 inscritas em 2 grupos de 5, gera 20 parcerias: 20 + 10 = os
                // **30 inscritos** que o painel anunciava. É o mesmo buraco que já tinha
                // mordido o Ranking Americano em 07/08, numa terceira cópia da mesma pergunta.
                //
                // Cada formato inscreve uma unidade diferente (pessoa, dupla, time) e a lista
                // de espera fica de fora — regras que já existem num lugar só. Por isso as
                // entidades são carregadas e a conta é feita em memória: repetir a régua numa
                // projeção do EF seria escrever a quarta cópia dela.
                var doOrganizador = await _context.Torneios
                    .Where(t => idsOrganizados.Contains(t.Id) && t.Status != "Finalizado")
                    .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
                    .ToListAsync();

                var inscricoesAmericanas = await _context.InscricoesAmericanas
                    .Where(i => idsOrganizados.Contains(i.Categoria.TorneioId))
                    .ToListAsync();

                var aoVivoPorTorneio = (await _context.Partidas
                        .Where(p => p.TorneioId != null && idsOrganizados.Contains(p.TorneioId.Value)
                                 && p.Status == "AoVivo")
                        .GroupBy(p => p.TorneioId!.Value)
                        .Select(g => new { TorneioId = g.Key, Quantos = g.Count() })
                        .ToListAsync())
                    .ToDictionary(x => x.TorneioId, x => x.Quantos);

                var torneios = doOrganizador.Select(t =>
                {
                    var idsDasCategorias = t.Categorias.Select(c => c.Id).ToHashSet();

                    return new TorneioOrganizadoVM
                    {
                        Id = t.Id,
                        Nome = t.Nome,
                        Status = t.Status,
                        Inscritos = QuantosInscritos.Contar(t, t.Categorias,
                            inscricoesAmericanas.Where(i => idsDasCategorias.Contains(i.CategoriaId))).Rotulo,
                        JogosAoVivo = aoVivoPorTorneio.GetValueOrDefault(t.Id),
                        PrecisaSortear = t.Status == "Chaves em Sorteio",
                    };
                }).ToList();

                if (torneios.Count > 0)
                {
                    var inicioSemana = hoje.AddDays(-7);
                    vm.Organizador = new PainelOrganizadorHomeVM
                    {
                        TorneiosAtivos = torneios.Count,
                        JogosAoVivo = torneios.Sum(t => t.JogosAoVivo),
                        InscricoesNaSemana = await _context.Duplas.CountAsync(d =>
                            idsOrganizados.Contains(d.Categoria.TorneioId)
                            && d.CriadoEm != null && d.CriadoEm >= inicioSemana),
                        Torneios = torneios
                            .OrderByDescending(t => t.JogosAoVivo)
                            .ThenByDescending(t => t.PrecisaSortear)
                            .ToList(),
                    };
                }
            }

            // ----- Dono/administrador de clube -----
            var idsClubes = await _context.Clubes
                .Where(c => c.DonoId == jogadorId)
                .Select(c => c.Id)
                .ToListAsync();

            idsClubes.AddRange(await _context.ClubeAdministradores
                .Where(a => a.JogadorId == jogadorId)
                .Select(a => a.ClubeId)
                .ToListAsync());

            idsClubes = idsClubes.Distinct().ToList();

            if (idsClubes.Count > 0)
            {
                var clubes = await _context.Clubes
                    .Where(c => idsClubes.Contains(c.Id))
                    .Select(c => new ClubeResumoHomeVM
                    {
                        Id = c.Id,
                        Nome = c.Nome,
                        Quadras = _context.QuadrasClube.Count(q => q.ClubeId == c.Id && q.Ativa),
                        ReservasHoje = _context.MarcacoesJogo.Count(m => m.ClubeId == c.Id
                            && m.Status == "Confirmada"
                            && m.DataHora >= hoje && m.DataHora < amanha),
                    })
                    .ToListAsync();

                vm.Clube = new PainelClubeHomeVM
                {
                    ReservasHoje = clubes.Sum(c => c.ReservasHoje),
                    QuadrasAtivas = clubes.Sum(c => c.Quadras),
                    Clubes = clubes.OrderBy(c => c.Nome).ToList(),
                };
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Irmã da Privacy, e as duas andam juntas no rodapé: a política diz o que fazemos com
        // os dados, os termos dizem as regras do serviço — quem responde pelo torneio, por que
        // um aviso que não chegou não vale como desculpa, e quanto custa a taxa.
        public IActionResult Termos()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Endereço que não existe (404) e afins. Sem isto, o navegador mostrava a própria tela de
        // erro — sem menu, sem identidade e sem caminho de volta, então a pessoa saía do site.
        // O código chega pela URL porque quem chama é o UseStatusCodePagesWithReExecute.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult NaoEncontrado(int codigo = 404)
        {
            // O `codigo` chega pela barra de endereço, então qualquer um digita o que quiser:
            // `?codigo=99999` fazia o Kestrel escrever o número cru na linha de status, e
            // `?codigo=200` fazia uma URL do site responder 200 exibindo esta tela — buscador e
            // monitoramento leriam "página boa". Só a faixa de erro passa; o resto vira 404.
            if (codigo < 400 || codigo > 599)
                codigo = 404;

            ViewBag.Codigo = codigo;

            // Devolve o status original: se respondesse 200, buscador e monitoramento passariam a
            // tratar página inexistente como página boa.
            Response.StatusCode = codigo;
            return View();
        }
    }
}
