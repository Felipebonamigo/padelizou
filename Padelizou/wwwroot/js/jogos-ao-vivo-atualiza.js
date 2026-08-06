// A lista de jogos se atualiza sozinha, pra três pessoas operarem o mesmo torneio.
//
// No Interno de 05/08/2026 eram três aparelhos (celular, iPad, notebook) mexendo na mesma
// Mesa. Quem finalizava num deles não aparecia nos outros: as outras duas telas seguiam
// mostrando o jogo em quadra, e mais de uma vez a mesma partida foi colocada no ar duas
// vezes porque o segundo operador não via o que o primeiro tinha feito.
//
// A página é servida inteira pelo servidor, então a forma honesta de sincronizar é recarregar
// — sem inventar uma segunda verdade em JavaScript que possa divergir do banco.
//
// ⚠️ Três cuidados que fazem a diferença entre ajudar e atrapalhar:
//   • NÃO recarrega enquanto alguém está digitando ou com um modal aberto. Recarregar por
//     baixo de quem está marcando placar apaga o que a pessoa acabou de digitar — seria pior
//     que o problema original.
//   • NÃO recarrega com a aba em segundo plano: o organizador deixa a Mesa aberta no
//     notebook e mexe no celular; recarregar ali é gasto de bateria e de 3G do clube.
//   • Só onde há jogo AO VIVO. Torneio parado não precisa de nada disso.
(function () {
    "use strict";

    var SEGUNDOS = 20;

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

    function temJogoAoVivo() {
        return document.querySelector(".pdz-live-card") !== null;
    }

    function tique() {
        if (document.hidden || estaOcupado() || !temJogoAoVivo()) return;

        // `location.reload()` sem argumento reusa o cache do navegador pro que não mudou —
        // o HTML vem do servidor de novo, que é o que interessa.
        window.location.reload();
    }

    if (temJogoAoVivo()) window.setInterval(tique, SEGUNDOS * 1000);
})();
