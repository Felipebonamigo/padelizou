// Máscaras de CPF e celular aplicadas em todo o site.
//
// Basta marcar o input com data-mascara="cpf" ou data-mascara="celular" — não precisa de
// código por tela. A máscara é só visual: o servidor limpa tudo que não é dígito de qualquer
// jeito (Documentos.SomenteDigitos), porque JS desligado ou colar texto de fora não pode
// resultar em CPF de 14 caracteres indo pro banco, onde a coluna aceita 11.
(function () {
    'use strict';

    function apenasDigitos(valor) {
        return (valor || '').replace(/\D/g, '');
    }

    function formatarCpf(valor) {
        var d = apenasDigitos(valor).slice(0, 11);
        if (d.length > 9) return d.slice(0, 3) + '.' + d.slice(3, 6) + '.' + d.slice(6, 9) + '-' + d.slice(9);
        if (d.length > 6) return d.slice(0, 3) + '.' + d.slice(3, 6) + '.' + d.slice(6);
        if (d.length > 3) return d.slice(0, 3) + '.' + d.slice(3);
        return d;
    }

    // Aceita fixo (10 dígitos) e celular (11), que é o que aparece num cadastro de jogador.
    function formatarCelular(valor) {
        var d = apenasDigitos(valor).slice(0, 11);
        if (d.length === 0) return '';
        if (d.length <= 2) return '(' + d;
        if (d.length <= 6) return '(' + d.slice(0, 2) + ') ' + d.slice(2);
        if (d.length <= 10) return '(' + d.slice(0, 2) + ') ' + d.slice(2, 6) + '-' + d.slice(6);
        return '(' + d.slice(0, 2) + ') ' + d.slice(2, 7) + '-' + d.slice(7);
    }

    var formatadores = { cpf: formatarCpf, celular: formatarCelular };

    function aplicar(input) {
        var formatar = formatadores[input.dataset.mascara];
        if (!formatar) return;

        // maxlength pensado pra dígitos crus cortaria a máscara no meio ("123.456.789-0").
        input.removeAttribute('maxlength');
        input.value = formatar(input.value);

        input.addEventListener('input', function () {
            // Digitar no meio do texto reposicionaria o cursor no fim a cada tecla; só
            // reformata quando o cursor está no final, que é o caso normal de digitação.
            var noFim = input.selectionStart === input.value.length;
            var formatado = formatar(input.value);
            if (formatado === input.value) return;

            input.value = formatado;
            if (!noFim) {
                var pos = input.value.length;
                input.setSelectionRange(pos, pos);
            }
        });
    }

    function iniciar() {
        document.querySelectorAll('[data-mascara]').forEach(aplicar);

        // O valor enviado vai sempre limpo, pra máscara ser só visual.
        document.querySelectorAll('form').forEach(function (form) {
            form.addEventListener('submit', function () {
                form.querySelectorAll('[data-mascara]').forEach(function (input) {
                    input.value = apenasDigitos(input.value);
                });
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})();
