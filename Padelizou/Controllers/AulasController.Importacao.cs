using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // IMPORTAR AULAS DO GOOGLE AGENDA (pedido do Felipe, 27/08/2026).
    //
    // O sentido inverso de tudo que a integração fazia: o professor que sempre marcou as aulas
    // direto no Google traz o que já está lá pra dentro do Padelizou. É o espelho do
    // `SincronizarGoogle` — lá o sistema empurra aula que ficou fora da agenda; aqui ele puxa
    // evento que nunca virou aula.
    //
    // NÃO É AUTOMÁTICO, de propósito. O que a listagem devolve é o calendário PESSOAL do
    // professor (`"primary"`, o mesmo dos três métodos de escrita): dentista, aniversário e
    // aula vêm misturados, e ninguém além dele sabe qual é qual. Importar sozinho criaria
    // aula com preço — que entra em relatório e financeiro — a partir da consulta no dentista.
    // Por isso a tela de conferência: ele marca o que é aula, escolhe local e preço do LOTE
    // (uma vez, não 27 vezes num celular), e o que precisar de exceção ajusta depois na
    // edição de aula que já existe.
    //
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> ImportarDoGoogle(DateTime? de, DateTime? ate)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (!await _googleCalendarService.EstaConectadoAsync(professorId.Value))
            {
                TempData["Erro"] = "Conecte sua Google Agenda antes de importar.";
                return RedirectToAction("MinhaAgenda");
            }

            var (inicio, fim) = JanelaDaImportacao(de, ate);

            var eventos = await _googleCalendarService.ListarEventosAsync(professorId.Value, inicio, fim);

            // ⚠️ NULL NÃO É LISTA VAZIA. Null = a leitura falhou — quase sempre o token que o
            // Google matou no refresh, a falha mais muda deste sistema (o arquivo é apagado e
            // `EstaConectadoAsync` pode até já dizer false na próxima). Tratar como vazio
            // mostraria "sua agenda não tem nada" pra quem tem a semana lotada.
            if (eventos == null)
            {
                TempData["Erro"] = "Não deu pra ler sua Google Agenda. "
                    + "Tente reconectar a conta — a autorização pode ter expirado.";
                return RedirectToAction("MinhaAgenda");
            }

            // Corta o que já é do Padelizou: evento cujo id está em `Aula.GoogleEventId` é o
            // próprio sistema se reconhecendo no espelho (aula enviada pro Google, ou já
            // importada antes). A conferência olha TODAS as aulas do professor, não só o
            // período — a aula fixa importada mês passado tem o mesmo id recorrente.
            var jaImportados = await IdsJaImportadosAsync(professorId.Value);

            var vm = new ImportarDoGoogleVM
            {
                Eventos = eventos.Where(e => !jaImportados.Contains(e.Id)).ToList(),
                Locais = await LocaisAtivosAsync(professorId.Value),
                De = inicio,
                Ate = fim,
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> ImportarDoGoogleConfirmar(
            string[] eventos, int localAulaId, decimal preco, DateTime de, DateTime ate)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (preco < 0)
            {
                TempData["Erro"] = "Valor negativo não dá: use zero pra aula sem cobrança.";
                return RedirectToAction("ImportarDoGoogle", new { de, ate });
            }

            // O local tem que ser DO PROFESSOR LOGADO e ativo — POST montado à mão não grava
            // aula na quadra de outro. É a checagem de dono da Regra 0.
            var local = await _context.LocaisAula
                .FirstOrDefaultAsync(l => l.Id == localAulaId && l.ProfessorId == professorId && l.Ativo);
            if (local == null)
            {
                TempData["Erro"] = "Escolha um local seu pra receber as aulas.";
                return RedirectToAction("ImportarDoGoogle", new { de, ate });
            }

            // ⚠️ A REGRA DO RemoverNaoPagos: o formulário diz o que o professor QUIS importar;
            // quem manda é a fonte no instante do clique. A lista é relida do GOOGLE (id
            // forjado no POST não vira aula às cegas) e a deduplicação é reconferida no BANCO
            // (o duplo-clique no confirmar, ou duas abas, não gravam a mesma aula duas vezes).
            var doGoogle = await _googleCalendarService.ListarEventosAsync(professorId.Value, de, ate);
            if (doGoogle == null)
            {
                TempData["Erro"] = "Não deu pra ler sua Google Agenda. "
                    + "Tente reconectar a conta — a autorização pode ter expirado.";
                return RedirectToAction("MinhaAgenda");
            }

            var marcados = eventos.Distinct().ToHashSet();
            var jaImportados = await IdsJaImportadosAsync(professorId.Value);

            var novas = new List<Aula>();
            foreach (var evento in doGoogle)
            {
                if (!marcados.Contains(evento.Id)) continue;
                if (jaImportados.Contains(evento.Id)) continue;

                novas.Add(new Aula
                {
                    ProfessorId = professorId.Value,
                    LocalAulaId = local.Id,
                    DataHora = evento.Inicio,
                    DuracaoMinutos = (int)(evento.Fim - evento.Inicio).TotalMinutes,
                    Preco = preco,
                    Status = PoliticaAula.Confirmada,
                    // O título do evento vira o aluno AVULSO — sem conta, como o AdicionarManual
                    // já faz. Ligar à conta de verdade é o VincularAlunoAConta, que já existe.
                    NomeAlunoAvulso = evento.Titulo,
                    // O casamento: é o que tira o evento da lista de candidatos pra sempre, e o
                    // que faz editar a aula depois atualizar ESTE evento em vez de criar outro.
                    GoogleEventId = evento.Id,
                });
            }

            if (novas.Count > 0)
            {
                _context.Aulas.AddRange(novas);
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = novas.Count switch
            {
                0 => "Nada pra importar — os eventos marcados já estavam no Padelizou.",
                1 => "1 aula importada da sua Google Agenda.",
                _ => $"{novas.Count} aulas importadas da sua Google Agenda.",
            };
            return RedirectToAction("MinhaAgenda");
        }

        // Padrão: os próximos 30 dias. Passado é escolha explícita do professor na tela —
        // aula passada importada entra em relatório e financeiro, e mudar número de dinheiro
        // não pode ser efeito colateral de um padrão.
        private static (DateTime, DateTime) JanelaDaImportacao(DateTime? de, DateTime? ate)
        {
            var inicio = (de ?? DateTime.Today).Date;
            var fim = (ate ?? inicio.AddDays(30)).Date.AddDays(1);   // inclusivo na tela
            if (fim <= inicio) fim = inicio.AddDays(30);
            return (inicio, fim);
        }

        // Todos os ids de evento que já têm aula DESTE professor — de qualquer data, porque o
        // id de um evento recorrente se repete e a aula pode estar fora da janela da tela.
        // Um HashSet em memória, e não um Contains traduzido: professor tem centenas de aulas,
        // não milhões, e a consulta simples não tem tradução pra errar (lição do InMemory).
        private async Task<HashSet<string>> IdsJaImportadosAsync(int professorId) =>
            (await _context.Aulas
                .Where(a => a.ProfessorId == professorId && a.GoogleEventId != null)
                .Select(a => a.GoogleEventId!)
                .ToListAsync())
            .ToHashSet();

        private async Task<List<LocalAula>> LocaisAtivosAsync(int professorId) =>
            await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .OrderBy(l => l.Nome)
                .ToListAsync();
    }
}
