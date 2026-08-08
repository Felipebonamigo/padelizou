// A lista de jogos se atualiza sozinha, pra três pessoas operarem o mesmo torneio.
//
// No Interno de 05/08/2026 eram três aparelhos (celular, iPad, notebook) mexendo na mesma
// Mesa. Quem finalizava num deles não aparecia nos outros: as outras duas telas seguiam
// mostrando o jogo em quadra, e mais de uma vez a mesma partida foi colocada no ar duas
// vezes porque o segundo operador não via o que o primeiro tinha feito.
//
// ⚠️ RECARREGAR A PÁGINA INTEIRA NÃO SERVE (Felipe, 08/08/2026: "o youtube está parando
// sozinho aqui do nada"). No Americano das Gurias as duas quadras estavam com transmissão
// embutida no cartão, e todo recarregamento REINICIA o <iframe>: de 20 em 20 segundos o vídeo
// voltava pro estado parado. De quebra, a página renasce na aba AO VIVO sempre que existe jogo
// em quadra, então quem estava em Agendadas era jogado pra outra tela a cada tique.
//
// Então a página não recarrega mais: ela BUSCA a versão nova do servidor e troca só os
// pedaços — o cabeçalho de cada cartão (placar, cronômetro, botões) e as abas sem vídeo. O
// <iframe> fica onde está, intocado. Mover ou reescrever um iframe é o mesmo que recarregá-lo,
// então a regra é simples: nada que contenha vídeo é substituído.
//
// A verdade continua sendo a do servidor — o HTML vem dele, inteiro, como sempre. Não há uma
// segunda cópia da regra em JavaScript que possa divergir do banco.
//
// ⚠️ Cuidados que fazem a diferença entre ajudar e atrapalhar:
//   • NÃO atualiza enquanto alguém está digitando ou com um modal aberto. Trocar HTML por
//     baixo de quem está marcando placar apaga o que a pessoa acabou de digitar — seria pior
//     que o problema original.
//   • NÃO atualiza com a aba em segundo plano: o organizador deixa a Mesa aberta no notebook
//     e mexe no celular; gastar bateria e 3G do clube ali não ajuda ninguém.
//   • Só onde há jogo AO VIVO. Torneio parado não precisa de nada disso.
//   • Rede que falhou é silêncio, não erro: tenta de novo no tique seguinte.
(function () {
    "use strict";

    var SEGUNDOS = 20;

    // Os pedaços SEM vídeo, trocados por inteiro. Os modais entram porque a lista de jogos
    // candidatos deles envelhece junto (o "trocar com qual jogo?" não pode oferecer uma
    // partida que já entrou em quadra).
    var BLOCOS = ["#agendadas", "#finalizadas", "#classificacao", "#modalTrocarHorario", "#modalTrocarQuadra"];

    function estaOcupado() {
        var ativo = document.activeElement;
        if (ativo && /^(INPUT|TEXTAREA|SELECT)$/.test(ativo.tagName)) return true;

        // Modal aberto (confirmação, trocar horário, mudar quadra) = decisão em curso.
        if (document.querySelector(".modal.show")) return true;

        // Texto selecionado costuma ser alguém lendo/copiando um nome.
        var selecao = window.getSelection && window.getSelection();
        if (selecao && String(selecao).length > 0) return true;

        return false;
    }

    function cartoes(raiz) {
        return Array.prototype.slice.call(raiz.querySelectorAll(".pdz-live-card"));
    }

    // Quais jogos estão em quadra, na ordem. Se isto mudou, a estrutura da tela mudou (jogo
    // novo no ar, jogo finalizado) — e aí não há vídeo a preservar: o cartão que sumiu levou o
    // dele junto. Recarregar é mais honesto que remontar cartão por cartão.
    function assinatura(raiz) {
        return cartoes(raiz).map(function (c) { return c.getAttribute("data-partida-id"); }).join(",");
    }

    function temJogoAoVivo() {
        return document.querySelector(".pdz-live-card") !== null;
    }

    function trocar(atual, fresco) {
        if (atual && fresco && atual.innerHTML !== fresco.innerHTML) atual.innerHTML = fresco.innerHTML;
    }

    function aplicar(novo) {
        // 1. O cabeçalho de cada cartão AO VIVO: placar, cronômetro, quadra e botões. A
        //    transmissão (.pdz-live-video) é irmã dele e não é tocada.
        cartoes(document).forEach(function (atual) {
            var id = atual.getAttribute("data-partida-id");
            var fresco = novo.querySelector('.pdz-live-card[data-partida-id="' + id + '"]');
            if (!fresco) return;
            trocar(atual.querySelector(".pdz-live-header"), fresco.querySelector(".pdz-live-header"));
        });

        // 2. As abas sem vídeo, inteiras.
        BLOCOS.forEach(function (seletor) {
            trocar(document.querySelector(seletor), novo.querySelector(seletor));
        });

        // 3. Os números das pílulas ("Agendadas (8)"). Só o CONTEÚDO de cada botão: a classe
        //    `active` mora no próprio botão, e trocar o botão trocaria a aba debaixo de quem
        //    está lendo — exatamente o que este arquivo existe pra não fazer.
        Array.prototype.forEach.call(novo.querySelectorAll("#jogosTabs .nav-link"), function (fresco) {
            var alvo = fresco.getAttribute("data-bs-target");
            if (!alvo) return;
            trocar(document.querySelector('#jogosTabs .nav-link[data-bs-target="' + alvo + '"]'), fresco);
        });
    }

    var buscando = false;

    function tique() {
        if (buscando || document.hidden || estaOcupado() || !temJogoAoVivo()) return;

        buscando = true;
        window.fetch(window.location.href, { credentials: "same-origin" })
            .then(function (resposta) {
                return resposta.ok ? resposta.text() : Promise.reject(resposta.status);
            })
            .then(function (html) {
                var novo = new DOMParser().parseFromString(html, "text/html");

                // Sem a lista na resposta, o que voltou não é esta tela (sessão caiu, portão de
                // acesso, erro): não dá pra remendar meia página com HTML de outra.
                if (!novo.getElementById("jogosTabsContent")) return;

                // A pessoa começou a mexer enquanto a resposta vinha.
                if (estaOcupado()) return;

                if (assinatura(novo) !== assinatura(document)) {
                    window.location.reload();
                    return;
                }

                aplicar(novo);
            })
            .catch(function () { /* rede do clube caiu: o próximo tique tenta de novo */ })
            .then(function () { buscando = false; });
    }

    if (temJogoAoVivo() && window.fetch) window.setInterval(tique, SEGUNDOS * 1000);
})();
