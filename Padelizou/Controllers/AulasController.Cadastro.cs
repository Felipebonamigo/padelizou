using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // O que o professor cadastra pra existir pro aluno: locais, cidades, horÃ¡rios e a polÃ­tica
    // de cancelamento. Ã‰ a "escada do professor" de Services/CadastroDeProfessor.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> MeusLocais()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId)
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();

            return View(locais);
        }

        [HttpPost]
        public async Task<IActionResult> CriarLocal(string nome, string endereco, decimal precoPadrao, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            _context.LocaisAula.Add(new LocalAula
            {
                ProfessorId = professorId.Value,
                Nome = nome,
                Endereco = endereco,
                PrecoPadrao = precoPadrao,
                CustoPorAula = custoPorAula,
                Ativo = true
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCustoLocal(int id, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.CustoPorAula = custoPorAula;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        // Preço do pacote de aulas é só informativo por enquanto — o pagamento é combinado
        // direto com o professor (ex: Pix), sem cobrança nem controle de créditos pelo site.
        [HttpPost]
        public async Task<IActionResult> AtualizarPacoteLocal(int id, bool pacoteAtivo, int? pacoteQuantidadeAulas, decimal? pacotePreco)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.PacoteAtivo = pacoteAtivo;
                local.PacoteQuantidadeAulas = pacoteAtivo ? (pacoteQuantidadeAulas is null or <= 0 ? 4 : pacoteQuantidadeAulas) : null;
                local.PacotePreco = pacoteAtivo ? pacotePreco : null;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarLocal(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.Ativo = !local.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        [HttpGet]
        public async Task<IActionResult> MinhasCidades()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var vinculadas = (await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .Select(pc => pc.CidadeId)
                .ToListAsync())
                .ToHashSet();

            var itens = await _context.Cidades
                .OrderBy(c => c.Nome)
                .Select(c => new MinhaCidadeItem
                {
                    CidadeId = c.Id,
                    Nome = c.Nome,
                    Estado = c.Estado,
                    Vinculada = vinculadas.Contains(c.Id)
                })
                .ToListAsync();

            return View(itens);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCidades(List<int>? cidadeIds, string? novaCidadeNome, string? novaCidadeUf)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var idsSelecionados = (cidadeIds ?? new List<int>()).ToHashSet();

            if (!string.IsNullOrWhiteSpace(novaCidadeNome))
            {
                var nomeNormalizado = NomeDeCidade.Arrumar(novaCidadeNome);

                // A comparação de antes era só `ToLower()`: pegava a caixa e deixava o acento
                // passar, então "gravatai" criava uma cidade NOVA ao lado de "Gravataí". Agora
                // compara sem acento e sem caixa, do jeito que uma pessoa compararia.
                //
                // A conta roda na memória porque `Chave` não vira SQL. Cidades é catálogo — dezenas
                // de linhas, não milhares — então trazer a lista é mais barato que instalar
                // `unaccent` no banco só pra isto.
                var cidadesExistentes = await _context.Cidades.ToListAsync();
                var cidadeExistente = cidadesExistentes.FirstOrDefault(c => NomeDeCidade.Mesma(c.Nome, nomeNormalizado));

                if (cidadeExistente == null)
                {
                    cidadeExistente = new Cidade
                    {
                        Nome = nomeNormalizado,
                        Estado = NomeDeCidade.ArrumarEstado(novaCidadeUf)
                    };
                    _context.Cidades.Add(cidadeExistente);
                    await _context.SaveChangesAsync();
                }

                idsSelecionados.Add(cidadeExistente.Id);
            }

            var vinculosAtuais = await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .ToListAsync();

            var vinculosParaRemover = vinculosAtuais.Where(v => !idsSelecionados.Contains(v.CidadeId));
            _context.ProfessorCidades.RemoveRange(vinculosParaRemover);

            var idsJaVinculados = vinculosAtuais.Select(v => v.CidadeId).ToHashSet();
            foreach (var cidadeId in idsSelecionados.Where(id => !idsJaVinculados.Contains(id)))
            {
                _context.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = professorId.Value, CidadeId = cidadeId });
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Cidades atualizadas com sucesso.";
            return RedirectToAction("MinhasCidades");
        }

        [HttpGet]
        public async Task<IActionResult> MeusHorarios()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.Locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .ToListAsync();

            var horarios = await _context.HorariosDisponiveis
                .Include(h => h.LocalAula)
                .Where(h => h.ProfessorId == professorId)
                .OrderBy(h => h.DiaSemana)
                .ThenBy(h => h.HoraInicio)
                .ToListAsync();

            return View(horarios);
        }

        // Vários dias de uma vez ("segunda, quarta e sexta das 8h ao meio-dia") — um dia por
        // vez tornava o cadastro da semana uma novela de sete capítulos. A decisão de quais
        // criar/reativar/pular mora em Services/NovoHorarioDoProfessor, testável sem banco.
        [HttpPost]
        public async Task<IActionResult> CriarHorario(int localAulaId, int[] diasSemana, TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localAulaId && l.ProfessorId == professorId);
            if (local == null)
            {
                return RedirectToAction("MeusHorarios");
            }

            var duracao = duracaoMinutos <= 0 ? DuracaoPadraoMinutos : duracaoMinutos;
            var existentes = await _context.HorariosDisponiveis
                .Where(h => h.ProfessorId == professorId)
                .ToListAsync();

            var plano = NovoHorarioDoProfessor.Planejar(
                diasSemana ?? Array.Empty<int>(), horaInicio, horaFim, duracao, localAulaId, existentes);

            if (!plano.Valido)
            {
                TempData["ErroHorario"] = plano.Erro;
                return RedirectToAction("MeusHorarios");
            }

            foreach (var dia in plano.DiasParaCriar)
            {
                _context.HorariosDisponiveis.Add(new HorarioDisponivel
                {
                    ProfessorId = professorId.Value,
                    LocalAulaId = localAulaId,
                    DiaSemana = dia,
                    HoraInicio = horaInicio,
                    HoraFim = horaFim,
                    DuracaoMinutos = duracao,
                    Ativo = true
                });
            }

            foreach (var dia in plano.DiasParaReativar)
            {
                var pausado = existentes.First(h => h.LocalAulaId == localAulaId && h.DiaSemana == dia
                    && h.HoraInicio == horaInicio && h.HoraFim == horaFim);
                pausado.Ativo = true;
            }

            await _context.SaveChangesAsync();
            TempData["SucessoHorario"] = NovoHorarioDoProfessor.Resumo(plano);

            return RedirectToAction("MeusHorarios");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarHorario(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var horario = await _context.HorariosDisponiveis.FirstOrDefaultAsync(h => h.Id == id && h.ProfessorId == professorId);
            if (horario != null)
            {
                horario.Ativo = !horario.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusHorarios");
        }

        // Para alunos que combinaram a aula fora do sistema (não têm conta). A aula nasce
        // direto como "Confirmada" — sem o fluxo de solicitação/aceite.

        private const int MinSemanasRecorrencia = 2;
        private const int MaxSemanasRecorrencia = 26;

        // Configuração da política de cancelamento (tela do professor).
        [HttpPost]
        public async Task<IActionResult> SalvarPolitica(int horasMinimas, bool cobraFaltaSemAviso, string? textoPolitica)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var professor = await _context.Jogadores.FindAsync(professorId);
            if (professor == null) return NotFound();

            professor.HorasMinimasCancelamento = Math.Clamp(horasMinimas, 0, 168); // teto de 1 semana
            professor.CobraFaltaSemAviso = cobraFaltaSemAviso;
            professor.PoliticaCancelamentoTexto = string.IsNullOrWhiteSpace(textoPolitica) ? null : textoPolitica.Trim();

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Política de cancelamento atualizada.";
            return RedirectToAction("Dashboard");
        }

    }
}
