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
                        PrimeiroNome = f.Jogador.Nome,
                        Cidade = f.Jogador.Cidade,
                        Nota = f.Nota,
                        Texto = f.Texto
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
            ViewBag.Codigo = codigo;

            // Devolve o status original: se respondesse 200, buscador e monitoramento passariam a
            // tratar página inexistente como página boa.
            Response.StatusCode = codigo;
            return View();
        }
    }
}
