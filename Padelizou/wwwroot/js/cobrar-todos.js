// COBRAR TODOS QUE NÃO PAGARAM — fila de WhatsApp, um clique de cada vez.
//
// Pedido do Felipe (07/09/2026): "tem como ter uma opção do usuário mesmo cobrar todos os
// jogadores que não pagaram de uma vez?".
//
// ⚠️ NÃO MANDA NADA SOZINHO. O botão abre, em sequência, os MESMOS links "Cobrar" que já
// existem em cada linha da aba Pagamentos (.pdz-cobrar-link) — continua sendo O ORGANIZADOR
// mandando, do PRÓPRIO WhatsApp, um por um. Só sem precisar caçar cada linha na lista.
//
// ⚠️ POR QUE FILA, E NÃO ABRIR TODOS DE UMA VEZ: o navegador bloqueia pop-up múltiplo
// disparado sem clique novo entre eles, e mandar N mensagens ao mesmo tempo pelo mesmo número
// pareceria robô pro WhatsApp do organizador. Um clique, uma conversa — o botão avisa quantos
// faltam e o organizador decide o próprio ritmo.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var botao = document.getElementById('pdzCobrarTodos');
        if (!botao) return;

        var links = Array.prototype.slice.call(document.querySelectorAll('.pdz-cobrar-link'));
        if (links.length === 0) return;

        var indice = 0;

        function atualizar() {
            var restantes = links.length - indice;
            if (restantes <= 0) {
                botao.textContent = 'Todo mundo foi cobrado';
                botao.disabled = true;
                return;
            }
            botao.innerHTML = '<i class="bi bi-whatsapp"></i> '
                + (indice === 0 ? 'Cobrar todos (' + restantes + ')' : 'Cobrar próximo (' + restantes + ' restantes)');
        }

        botao.addEventListener('click', function () {
            if (indice >= links.length) return;
            window.open(links[indice].href, '_blank', 'noopener');
            indice++;
            atualizar();
        });
    });
})();
