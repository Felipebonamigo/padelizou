// A TELA FICA ONDE ESTÁ — a lista não volta pro topo a cada clique (Felipe, 10/09/2026).
//
// 🗣️ Num print da lista do Er rolada até as quartas de domingo: *"quando eu trocar aqui, ele tem
// q permanecer no mesmo local da tela, esta indo para o inicio"*.
//
// Toda ação do organizador é POST → redirect → GET, e a página nova nasce no começo. Numa lista de
// 97 jogos, arrumar a ordem de sete semifinais custava sete rolagens até achar de novo a linha em
// que se estava mexendo. É a MESMA queixa de 08/08 que criou o js/jogos-abas.js ("ele tem que se
// manter na tela que eu estou editando"), e por isso é a mesma peça: sessionStorage.
//
// ⚠️ POR OPT-IN (`data-manter-posicao` no formulário), e não em todo POST da página: uma ação que
// leva pra OUTRA tela, ou que muda a lista inteira (o "Recalcular horários"), não quer voltar pra
// uma posição que já não quer dizer nada. Quem sabe disso é quem escreveu o botão.
//
// sessionStorage, não localStorage: é o estado DESTA sessão de trabalho, não uma preferência — e a
// posição é lida UMA vez e apagada, senão qualquer visita seguinte à página seria arrastada pra um
// lugar escolhido em outro momento.
(function () {
    "use strict";

    // Por página: quem opera dois torneios no mesmo dia não herda a rolagem de um no outro.
    var chave = "pdz-posicao-na-lista:" + window.location.pathname;

    // sessionStorage pode ser PROIBIDO (navegação privada com cookies bloqueados) e aí o próprio
    // acesso estoura. Falhar aqui não pode derrubar nada: sem memória, a página só nasce no topo,
    // que é como ela já nascia.
    function guardar(valor) {
        try { window.sessionStorage.setItem(chave, valor); } catch (e) { /* sem memória */ }
    }

    function pegarEApagar() {
        try {
            var valor = window.sessionStorage.getItem(chave);
            window.sessionStorage.removeItem(chave);
            return valor;
        } catch (e) { return null; }
    }

    document.addEventListener("submit", function (evento) {
        var formulario = evento.target;
        if (!formulario || !formulario.hasAttribute || !formulario.hasAttribute("data-manter-posicao")) return;

        guardar(String(window.scrollY || window.pageYOffset || 0));
    }, true);

    // No `load`, e não no DOMContentLoaded: o navegador ainda pula pra âncora do endereço
    // (#jogosDoTorneio) e o js/jogos-abas.js ainda troca a aba — as duas coisas mexem na altura da
    // página. O rAF põe a rolagem depois de tudo isso ter acontecido.
    window.addEventListener("load", function () {
        var guardada = pegarEApagar();
        if (guardada === null) return;

        var y = parseInt(guardada, 10);
        if (isNaN(y) || y <= 0) return;

        window.requestAnimationFrame(function () {
            window.scrollTo(0, y);
        });
    });
})();
