using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace padelizou.Controllers
{
    // A tela /Admin/Leads: o caderno de quem-trouxe-quem do Programa de Parceiros.
    //
    // O programa (PARCEIROS.md) paga 30% da primeira venda e 10% por 12 meses a quem traz
    // organizador, professor ou clube. A regra que sustenta tudo isso é uma só: **o contato
    // vale pra quem registrou primeiro, e o registro tem que ser ANTES da conversa**. Sem um
    // lugar que carimbe a hora, essa regra é a palavra de um amigo contra a de outro.
    //
    // Por que no painel admin e não numa tela do próprio parceiro: com dois ou três parceiros,
    // o registro chega por WhatsApp e o Felipe lança. Uma tela por parceiro exigiria um quinto
    // perfil de acesso (PoderesNoSistema) pra resolver um problema que ainda não existe —
    // quando existir, as REGRAS já estarão em Services/LeadsComerciais, e essa tela só chama
    // as mesmas funções.
    //
    // ⚠️ Nada aqui calcula ou paga comissão. Isto responde "de quem é o cliente"; o quanto ele
    // rende sai do Pagamento (Comissao/RecebedorId), e ainda não foi construído.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> Leads()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var leads = await _context.LeadsComerciais
                .Include(l => l.Parceiro)
                .Include(l => l.Cliente)
                .ToListAsync();

            var agora = DateTime.Now;

            // Ordem de trabalho, não ordem de banco: o que está aberto e perto de vencer é o
            // que precisa de uma ligação hoje. Ganho e perdido são histórico.
            ViewBag.Agora = agora;
            var ordenados = leads
                .OrderBy(l => LeadsComerciais.Situacao(l, agora) switch
                {
                    LeadsComerciais.Aberto => 0,
                    LeadsComerciais.Vencido => 1,
                    LeadsComerciais.Ganho => 2,
                    _ => 3,
                })
                .ThenBy(l => LeadsComerciais.Situacao(l, agora) == LeadsComerciais.Aberto
                    ? LeadsComerciais.VenceEm(l)
                    : DateTime.MaxValue)
                .ThenByDescending(l => l.RegistradoEm)
                .ToList();

            return View(ordenados);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarLead(int parceiroId, string contato, string telefone,
            string tipo, string? observacao)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return Forbid();

            var parceiro = await _context.Jogadores.FindAsync(parceiroId);
            if (parceiro == null)
            {
                TempData["Erro"] = "Escolha o parceiro que está indicando — é dele que a comissão vai ser.";
                return RedirectToAction("Leads");
            }

            contato = (contato ?? "").Trim();
            observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            var digitos = LeadsComerciais.SoDigitos(telefone);

            // Os candidatos vêm do banco JÁ filtrados pelo telefone — carregar a tabela inteira
            // pra achar duplicata funcionaria hoje, com dez linhas, e não daqui a um ano.
            var mesmoTelefone = await _context.LeadsComerciais
                .Where(l => l.Telefone == digitos)
                .ToListAsync();

            var nomes = await NomesDosParceirosAsync(mesmoTelefone);

            var problema = LeadsComerciais.ProblemaParaRegistrar(
                contato, telefone, tipo, mesmoTelefone, DateTime.Now,
                id => nomes.GetValueOrDefault(id, "Outro parceiro"));

            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Leads");
            }

            if (observacao != null && observacao.Length > LeadsComerciais.TamanhoMaximoObservacao)
                observacao = observacao[..LeadsComerciais.TamanhoMaximoObservacao];

            _context.LeadsComerciais.Add(new LeadComercial
            {
                ParceiroId = parceiro.Id,
                Contato = contato,
                Telefone = digitos,
                Tipo = tipo,
                Observacao = observacao,
                RegistradoEm = DateTime.Now,
                RegistradoPorId = admin.Id,
                Status = LeadsComerciais.Aberto,
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Contato \"{contato}\" registrado para {parceiro.ComoChamar}. "
                + $"É dele até {DateTime.Now.AddDays(LeadsComerciais.DiasDeValidade):dd/MM/yyyy}.";
            return RedirectToAction("Leads");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeadGanho(int leadId, int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var lead = await _context.LeadsComerciais.Include(l => l.Parceiro)
                .FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead == null) return NotFound();

            if (lead.Status != LeadsComerciais.Aberto)
            {
                TempData["Erro"] = "Esse contato já foi fechado.";
                return RedirectToAction("Leads");
            }

            var cliente = await _context.Jogadores.FindAsync(jogadorId);
            if (cliente == null)
            {
                TempData["Erro"] = "Jogador não encontrado.";
                return RedirectToAction("Leads");
            }

            // Fechar VENCIDO é permitido de propósito: o prazo existe pra liberar o contato pra
            // outro parceiro, não pra anular uma venda que aconteceu. Se ninguém mais registrou
            // nesse meio-tempo, quem trabalhou o contato foi ele.
            lead.Status = LeadsComerciais.Ganho;
            lead.FechadoEm = DateTime.Now;
            lead.ClienteId = cliente.Id;
            await _context.SaveChangesAsync();

            // O que o parceiro ganha por isso não é decidido aqui — a régua está no contrato, e
            // o cálculo vai ler Pagamento.Comissao pelo RecebedorId deste cliente.
            TempData["Sucesso"] = $"\"{lead.Contato}\" fechado como {cliente.ComoChamar}, "
                + $"indicado por {lead.Parceiro.ComoChamar}. A comissão passa a contar dos pagamentos dele.";
            return RedirectToAction("Leads");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeadPerdido(int leadId, string? motivo)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var lead = await _context.LeadsComerciais.FindAsync(leadId);
            if (lead == null) return NotFound();

            if (lead.Status == LeadsComerciais.Ganho)
            {
                TempData["Erro"] = "Esse contato já virou cliente — não dá pra marcar como perdido.";
                return RedirectToAction("Leads");
            }

            motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            if (motivo != null && motivo.Length > LeadsComerciais.TamanhoMaximoObservacao)
                motivo = motivo[..LeadsComerciais.TamanhoMaximoObservacao];

            lead.Status = LeadsComerciais.Perdido;
            lead.FechadoEm = DateTime.Now;
            lead.MotivoPerda = motivo;
            await _context.SaveChangesAsync();

            // Perdido libera o telefone na hora: quem não fechou não segura o contato até o
            // fim dos 90 dias.
            TempData["Sucesso"] = $"\"{lead.Contato}\" marcado como perdido. O telefone está livre pra outro parceiro registrar.";
            return RedirectToAction("Leads");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirLead(int leadId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var lead = await _context.LeadsComerciais.FindAsync(leadId);
            if (lead == null) return NotFound();

            // Apagar é pra erro de digitação, e não pra desfazer uma disputa: um contato tirado
            // da frente devolve o telefone pro próximo que registrar. Por isso o aviso da tela
            // diz o nome de quem perde o registro.
            var contato = lead.Contato;
            _context.LeadsComerciais.Remove(lead);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Registro de \"{contato}\" apagado.";
            return RedirectToAction("Leads");
        }

        // Os nomes de quem já registrou aquele telefone, numa consulta só — a mensagem de
        // recusa precisa dizer QUEM segura o contato, e sem isso seria uma ida ao banco por
        // linha candidata.
        private async Task<Dictionary<int, string>> NomesDosParceirosAsync(List<LeadComercial> leads)
        {
            if (leads.Count == 0) return new Dictionary<int, string>();

            var ids = leads.Select(l => l.ParceiroId).Distinct().ToList();
            return await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);
        }
    }
}
