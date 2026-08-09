// Sugestão de horário nos campos de data-e-hora, aplicada em todo o site.
//
// Basta marcar o input: <input type="datetime-local" data-hora-sugerida /> — não precisa
// de código por tela.
//
// O motivo é o celular: a rodinha do iOS/Android abre no INSTANTE ATUAL (14:37), e aula,
// jogo e torneio começam em hora cheia — o professor girava minuto a minuto de 37 até 00
// toda vez que marcava uma aula (queixa real de 08/08/2026). Com o campo já preenchido
// com a PRÓXIMA hora cheia, a rodinha abre em cima de um horário plausível e o ajuste
// vira um giro curto de hora, não uma caça ao minuto.
//
// É opt-in de propósito: em telas onde o campo vazio FORÇA a pessoa a escolher (agendar
// um aviso, por exemplo), pré-preencher criaria envio acidental com o horário sugerido.
(function () {
    'use strict';

    function doisDigitos(n) {
        return String(n).padStart(2, '0');
    }

    function proximaHoraCheia() {
        var d = new Date();
        d.setHours(d.getHours() + 1, 0, 0, 0);
        return d.getFullYear() + '-' + doisDigitos(d.getMonth() + 1) + '-' + doisDigitos(d.getDate())
            + 'T' + doisDigitos(d.getHours()) + ':00';
    }

    function iniciar() {
        document.querySelectorAll('input[type="datetime-local"][data-hora-sugerida]').forEach(function (input) {
            // Valor já posto pela tela (edição, volta do validador) vale mais que a sugestão.
            if (!input.value) input.value = proximaHoraCheia();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})();
