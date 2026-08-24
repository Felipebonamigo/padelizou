// Máscaras de documento e telefone aplicadas em todo o site.
//
// Basta marcar o input: data-mascara="cpf" | "cnpj" | "documento" | "celular" — não precisa
// de código por tela. A máscara é só visual: o servidor limpa tudo que não é dígito de
// qualquer jeito (Documentos.SomenteDigitos), porque JS desligado ou colar texto de fora não
// pode resultar em CPF de 14 caracteres indo pro banco, onde a coluna aceita 11.
//
// "documento" troca sozinho entre CPF e CNPJ conforme o tamanho — serve pros campos que
// aceitam os dois (o Asaas cobra tanto de pessoa física quanto de empresa).
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

    // 00.000.000/0000-00
    function formatarCnpj(valor) {
        var d = apenasDigitos(valor).slice(0, 14);
        if (d.length > 12) return d.slice(0, 2) + '.' + d.slice(2, 5) + '.' + d.slice(5, 8) + '/' + d.slice(8, 12) + '-' + d.slice(12);
        if (d.length > 8) return d.slice(0, 2) + '.' + d.slice(2, 5) + '.' + d.slice(5, 8) + '/' + d.slice(8);
        if (d.length > 5) return d.slice(0, 2) + '.' + d.slice(2, 5) + '.' + d.slice(5);
        if (d.length > 2) return d.slice(0, 2) + '.' + d.slice(2);
        return d;
    }

    // Até 11 dígitos é CPF; passou disso, é CNPJ. A troca acontece enquanto se digita.
    function formatarDocumento(valor) {
        return apenasDigitos(valor).length > 11 ? formatarCnpj(valor) : formatarCpf(valor);
    }

    // 00000-000
    function formatarCep(valor) {
        var d = apenasDigitos(valor).slice(0, 8);
        return d.length > 5 ? d.slice(0, 5) + '-' + d.slice(5) : d;
    }

    var formatadores = {
        cpf: formatarCpf,
        cnpj: formatarCnpj,
        documento: formatarDocumento,
        celular: formatarCelular,
        cep: formatarCep
    };

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

    // Pra telas que criam campo com data-mascara depois do carregamento da página (a turma
    // de AdicionarManual.cshtml, que nasce um bloco de telefone por aluno quando o professor
    // escolhe o tamanho) — sem isto o campo novo só ficaria digitado cru até o fim, porque
    // `iniciar()` só varre o que já existia no DOMContentLoaded.
    window.aplicarMascarasEm = function (container) {
        (container || document).querySelectorAll('[data-mascara]').forEach(aplicar);
    };

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
