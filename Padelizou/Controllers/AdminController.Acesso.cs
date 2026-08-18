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
    // ⚠️ SÓ LEITURA, e isso é uma escolha, não uma etapa que faltou. Uma tela de suporte que
    // também EDITA a conta alheia é a tela que, num dia corrido, troca o e-mail da pessoa
    // errada — e trocar o e-mail de uma conta é entregar a conta. O único desfecho que ainda
    // precisa de mão (o beco sem saída de quem tem senha e não tem e-mail) está escrito na tela
    // com todas as letras, pra ser feito de olho aberto.
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
    }
}
