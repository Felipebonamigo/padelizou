using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // A tela /Admin/Acesso: "não consigo entrar", respondido em uma busca.
    //
    // Nasceu de um caso real (18/08/2026): chegou um CPF no WhatsApp, a pessoa não sabia login,
    // e-mail nem senha, e a única forma de responder era abrir SSH e rodar SELECT no banco de
    // produção. Nenhuma tela do painel mostrava contato — a busca de /Admin/Organizadores acha
    // pelo CPF, mas imprime só o nome.
    //
    // ⚠️ SÓ LEITURA SOBRE A CONTA, e isso é uma escolha, não uma etapa que faltou. Uma tela de
    // suporte que também EDITA a conta alheia é a tela que, num dia corrido, troca o e-mail da
    // pessoa errada — e trocar o e-mail de uma conta é entregar a conta. O único desfecho que
    // ainda precisa de mão (o beco sem saída de quem tem senha e não tem e-mail) está escrito na
    // tela com todas as letras, pra ser feito de olho aberto.
    //
    // ⚠️ AS DUAS LIBERAÇÕES LÁ EMBAIXO (10/09/2026) NÃO ABREM MÃO DISSO, e vale dizer por quê: elas
    // devolvem uma TROCA, não escrevem um dado. O admin não digita o nome novo, não escolhe o nome
    // novo e nem chega a vê-lo — quem troca continua sendo a dona da conta, no perfil dela. Nada
    // aqui entrega conta a ninguém, que é o risco de que esta tela se cuida.
    //
    // De quebra, ser só-leitura mantém de graça a premissa do assistente do sistema: ele vê o
    // painel inteiro e não muda nada, e a trava é o VERBO HTTP (ver ObterJogadorAdminAsync).
    public partial class AdminController
    {
        // Quantas pessoas a busca por nome devolve antes de virar uma lista que ninguém lê.
        private const int LimiteDaBuscaDeAcesso = 10;

        [HttpGet]
        public async Task<IActionResult> Acesso(string? busca, int? jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var vm = new AcessoDoJogadorVM
            {
                Busca = busca,
                Procurou = !string.IsNullOrWhiteSpace(busca) || jogadorId != null,
            };

            // Veio da lista de homônimos: a pessoa foi escolhida a dedo. Procurar de novo pelo
            // termo casaria com os mesmos e devolveria a lista, num laço sem saída.
            if (jogadorId is int escolhido)
            {
                vm.Achado = await _context.Jogadores.FindAsync(escolhido);
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(busca)) return View(vm);

            // A MESMA régua da tela de teste de aviso: login, e-mail, nome, apelido ou CPF
            // completo — o que o admin tiver na mão. Uma busca própria aqui divergiria da outra
            // em um mês, e "o CPF acha lá e não acha aqui" é um defeito mudo.
            var achados = await BuscaJogador.ParaAcaoAdministrativaAsync(
                _context, busca, LimiteDaBuscaDeAcesso);

            if (achados.Count == 1) vm.Achado = achados[0];
            else vm.Candidatos = achados;

            return View(vm);
        }

        // ── A EXCEÇÃO DO SUPORTE: DEVOLVER UMA TROCA ─────────────────────────────────────
        //
        // `TrocaDeNome.Recusa` promete, em letras: *"Se precisa mesmo mudar, fale com a gente
        // pelo 'Reportar problema'"*. Do outro lado dessa frase não havia nada — a única saída
        // era SSH + UPDATE no banco de produção, que é justamente o buraco que esta tela nasceu
        // pra fechar em 18/08.
        //
        // ⚠️ NÃO EXISTE CÓDIGO DE "VOLTAR A TRAVAR", e é de propósito: `PodeTrocarNome` já lê
        // "carimbo nulo → pode", e o próprio salvamento da pessoa recarimba. Zerar o carimbo *é*
        // "mais uma vez, e só" — por isso aqui não nasceu coluna, flag nem migration.
        //
        // ⚠️ São DUAS ações, e não uma com parâmetro: as réguas são diferentes (o nome é uma vez
        // na vida; o apelido, a cada mês) e o que a pessoa precisa ouvir depois também é. Uma
        // ação só, com um `campo` de texto, ainda teria que se ramificar nas duas frases — e de
        // quebra ganharia um terceiro caminho, o do valor que não é nem um nem outro.
        //
        // A trava do assistente do sistema é o VERBO, e continua de pé: `ObterJogadorAdminAsync`
        // recusa qualquer POST pra quem só olha.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarTrocaDeNome(int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.NomeAlteradoEm = null;
            await _context.SaveChangesAsync();

            // Fica no log porque o carimbo SOME: depois de zerado, a conta não sabe mais dizer
            // que já tinha trocado uma vez, e sem esta linha nada registraria que houve exceção.
            _logger?.LogInformation(
                "Troca de NOME liberada para o jogador {JogadorId} pelo painel de acesso.", jogador.Id);

            TempData["Sucesso"] = $"{NomeBonito.Formatar(jogador.Nome)} pode corrigir o nome mais "
                + "uma vez, em Editar perfil. Assim que salvar, ele trava de novo sozinho — "
                + "liberar devolve uma troca, não abre a porteira.";
            return RedirectToAction(nameof(Acesso), new { jogadorId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarTrocaDeApelido(int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.ApelidoAlteradoEm = null;
            await _context.SaveChangesAsync();

            _logger?.LogInformation(
                "Troca de APELIDO liberada para o jogador {JogadorId} pelo painel de acesso.", jogador.Id);

            // ⚠️ A frase aqui é OUTRA, e a diferença importa: o apelido não trava pra sempre — ele
            // recomeça a carência de um mês. Repetir o texto do nome faria a pessoa entender que
            // acabou de gastar a última chance dela.
            TempData["Sucesso"] = $"{NomeBonito.Formatar(jogador.Nome)} pode trocar o apelido agora, "
                + $"em Editar perfil. A carência de {TrocaDeNome.MesesParaTrocarApelido} mês "
                + "recomeça na próxima troca.";
            return RedirectToAction(nameof(Acesso), new { jogadorId });
        }
    }
}
