// COBRAR QUEM NÃO PAGOU — fila de WhatsApp, uma conversa por clique.
//
// Pedido do Felipe (07/09/2026): "tem como ter uma opção do usuário mesmo cobrar todos os
// jogadores que não pagaram de uma vez?".
//
// ⚠️ NÃO MANDA NADA SOZINHO. O botão abre, um por vez, os MESMOS links "Cobrar" que já existem
// em cada linha da aba Pagamentos (.pdz-cobrar-link) — continua sendo O ORGANIZADOR mandando,
// do PRÓPRIO WhatsApp, e é ele quem aperta enviar lá dentro. Só sem precisar caçar cada linha.
//
// ⚠️ POR QUE FILA, E NÃO ABRIR TODOS DE UMA VEZ: o navegador bloqueia pop-up múltiplo disparado
// sem clique novo entre eles, e mandar N mensagens ao mesmo tempo pelo mesmo número pareceria
// robô pro WhatsApp do organizador — foi assim que a Meta restringiu o número em 03/08/2026.
// Um clique, uma conversa.
//
// 🔁 REVISTO EM 08/09/2026, depois de o Felipe perguntar *"ele envia pro whats? dá a impressão
// que sim, mas parece que não está enviando"*. Três coisas faltavam, e todas faziam o
// funcionamento correto parecer defeito:
//
//   1. o botão se chamava "Cobrar todos (N)" — nome de disparo em massa (consertado na view);
//   2. pop-up bloqueado falhava CALADO: o clique não abria nada e não dizia por quê;
//   3. recarregar a página zerava a contagem, sem memória de quem já tinha sido cobrado.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var botao = document.getElementById('pdzCobrarTodos');
        if (!botao) return;

        var links = Array.prototype.slice.call(document.querySelectorAll('.pdz-cobrar-link'));
        if (links.length === 0) return;

        var aviso = document.getElementById('pdzCobrarAviso');
        var recomecar = document.getElementById('pdzCobrarRecomecar');

        // A memória é POR TORNEIO: uma chave só faria a cobrança de um marcar como cobrado o
        // inscrito de outro. localStorage, e não servidor — é conveniência de quem está com a
        // tela aberta, não estado do torneio que outra pessoa precise ver.
        var chave = 'pdz-cobrados-' + (botao.getAttribute('data-pdz-torneio') || '0');

        // ⚠️ TODO ACESSO PROTEGIDO. Em aba anônima, com armazenamento desligado ou cota cheia, o
        // localStorage LANÇA em vez de devolver vazio — e derrubar o script aqui deixaria o
        // botão inerte, que é justamente o defeito que este arquivo veio consertar. Sem memória
        // ele volta a funcionar como antes: a fila zera ao recarregar, e só.
        function lidos() {
            try {
                var bruto = window.localStorage.getItem(chave);
                return bruto ? JSON.parse(bruto) : [];
            } catch (e) {
                return [];
            }
        }

        function gravar(ids) {
            try {
                window.localStorage.setItem(chave, JSON.stringify(ids));
            } catch (e) {
                /* sem memória: segue a vida, a fila só não sobrevive ao recarregamento */
            }
        }

        var cobrados = lidos();

        function jaCobrado(link) {
            return cobrados.indexOf(link.getAttribute('data-pdz-dupla')) >= 0;
        }

        // Os que ainda faltam, na ordem da tela.
        function pendentes() {
            return links.filter(function (link) { return !jaCobrado(link); });
        }

        function mostrarAviso(texto) {
            if (!aviso) return;
            aviso.textContent = texto;
            aviso.classList.toggle('d-none', !texto);
        }

        function atualizar() {
            var faltam = pendentes().length;
            var jaMexeu = cobrados.length > 0;

            if (recomecar) recomecar.classList.toggle('d-none', !jaMexeu);

            if (faltam === 0) {
                botao.textContent = 'Todo mundo foi cobrado';
                botao.disabled = true;
                return;
            }

            botao.disabled = false;
            botao.innerHTML = '<i class="bi bi-whatsapp"></i> Abrir cobrança '
                + (links.length - faltam + 1) + ' de ' + links.length;
        }

        botao.addEventListener('click', function () {
            var fila = pendentes();
            if (fila.length === 0) return;

            var proximo = fila[0];
            var aba = window.open(proximo.href, '_blank', 'noopener');

            // ⚠️ ABA BLOQUEADA NÃO CONTA COMO COBRADO. `window.open` devolve null quando o
            // navegador barra o pop-up; avançar a fila aqui pularia a pessoa em silêncio — pior
            // que o defeito original, porque o organizador acharia que cobrou.
            if (!aba) {
                mostrarAviso('O navegador bloqueou a janela. Libere os pop-ups deste site e clique de novo — '
                    + 'ninguém foi cobrado ainda.');
                return;
            }

            mostrarAviso('');
            cobrados.push(proximo.getAttribute('data-pdz-dupla'));
            gravar(cobrados);
            atualizar();
        });

        if (recomecar) {
            recomecar.addEventListener('click', function () {
                cobrados = [];
                gravar(cobrados);
                mostrarAviso('');
                atualizar();
            });
        }

        atualizar();
    });
})();
