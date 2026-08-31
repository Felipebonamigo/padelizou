// Sugestão de horário nos campos de data-e-hora, aplicada em todo o site.
//
// Basta marcar o input: <input type="datetime-local" data-hora-sugerida /> — não precisa
// de código por tela. Vale também pro par `type="date"` + `type="time"`, que é como a tela
// de Adicionar Aula passou a pedir data e hora (25/08/2026): o `datetime-local` abre a
// rodinha de rolagem no Android, e o professor precisa do CALENDÁRIO pra ver o dia da semana.
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

    // A mesma próxima hora cheia, partida nos dois campos do HTML.
    //
    // ⚠️ AS DUAS PARTES SAEM DO MESMO INSTANTE, e não de duas contas separadas: às 23:30 a
    // próxima hora cheia é 00:00 de AMANHÃ. Com o campo de data preenchido por um "hoje"
    // calculado à parte, a sugestão sairia com o dia de ontem e a hora de hoje — uma aula à
    // meia-noite no dia errado, sugerida pelo próprio sistema.
    function partesDaProximaHoraCheia() {
        var texto = proximaHoraCheia();
        var pedacos = texto.split('T');
        return { completo: texto, data: pedacos[0], hora: pedacos[1] };
    }

    function iniciar() {
        var sugestao = partesDaProximaHoraCheia();

        document.querySelectorAll('input[data-hora-sugerida]').forEach(function (input) {
            // Valor já posto pela tela (edição, volta do validador) vale mais que a sugestão.
            if (input.value) return;

            if (input.type === 'datetime-local') input.value = sugestao.completo;
            else if (input.type === 'date') input.value = sugestao.data;
            else if (input.type === 'time') input.value = sugestao.hora;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})();
