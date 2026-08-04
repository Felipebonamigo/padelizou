// Confirmação do Padelizou no lugar do confirm() do navegador.
//
// A view não escreve JavaScript nenhum: põe `data-confirmar` no <form> (ou no <a>) e este
// arquivo faz o resto. Marcação em Views/Shared/_ModalConfirmar.cshtml.
//
// Por que interceptar no DOCUMENTO e não em cada formulário: metade das telas monta HTML
// depois (aba de inscritos, lista de jogos, modais). Ligando um por um, tudo que nascesse
// depois voltaria a submeter sem perguntar — silenciosamente, que é o pior jeito de falhar.
(function () {
    "use strict";

    var modal = null;      // instância do Bootstrap, criada na primeira vez
    var alvo = null;       // o form ou link esperando resposta

    function elementos() {
        var raiz = document.getElementById("pdzConfirmarModal");
        if (!raiz) return null;
        return {
            raiz: raiz,
            icone: raiz.querySelector(".pdz-confirmar-icone"),
            titulo: raiz.querySelector(".pdz-confirmar-titulo"),
            texto: raiz.querySelector(".pdz-confirmar-texto"),
            detalhe: raiz.querySelector(".pdz-confirmar-detalhe"),
            ok: raiz.querySelector(".pdz-confirmar-ok"),
            cancelar: raiz.querySelector(".pdz-confirmar-cancelar")
        };
    }

    function abrir(elemento) {
        var el = elementos();

        // Sem o modal na página (view que não incluiu o partial), cai no confirm() do
        // navegador. Feio, mas perguntar feio é muito melhor que não perguntar.
        if (!el || typeof bootstrap === "undefined") {
            return window.confirm(elemento.dataset.confirmar) ? seguir(elemento) : null;
        }

        var d = elemento.dataset;
        var perigo = d.confirmarTom === "perigo";

        el.titulo.textContent = d.confirmarTitulo || "Tem certeza?";
        el.texto.textContent = d.confirmar || "";
        el.icone.dataset.tom = perigo ? "perigo" : "padrao";
        el.icone.innerHTML = '<i class="bi ' + (perigo ? "bi-exclamation-lg" : "bi-question-lg") + '"></i>';

        if (d.confirmarDetalhe) {
            el.detalhe.textContent = d.confirmarDetalhe;
            el.detalhe.classList.remove("d-none");
        } else {
            el.detalhe.classList.add("d-none");
        }

        el.ok.textContent = d.confirmarOk || (perigo ? "Sim, pode apagar" : "Confirmar");
        el.ok.classList.toggle("pdz-confirmar-ok-perigo", perigo);

        alvo = elemento;
        modal = modal || new bootstrap.Modal(el.raiz);
        modal.show();
    }

    // Deixa passar de verdade. A marca `confirmado` é o que impede o laço infinito: o submit
    // daqui volta a cair no mesmo ouvinte, e sem ela abriria o modal outra vez, pra sempre.
    function seguir(elemento) {
        if (elemento.tagName === "A") {
            window.location.href = elemento.href;
            return;
        }
        elemento.dataset.confirmado = "1";
        if (typeof elemento.requestSubmit === "function") elemento.requestSubmit();
        else elemento.submit();
    }

    document.addEventListener("submit", function (e) {
        var form = e.target.closest ? e.target.closest("form[data-confirmar]") : null;
        if (!form || form.dataset.confirmado === "1") return;
        e.preventDefault();
        abrir(form);
    });

    document.addEventListener("click", function (e) {
        var link = e.target.closest ? e.target.closest("a[data-confirmar]") : null;
        if (!link) return;
        e.preventDefault();
        abrir(link);
    });

    document.addEventListener("DOMContentLoaded", function () {
        var el = elementos();
        if (!el) return;

        el.ok.addEventListener("click", function () {
            var oQue = alvo;
            alvo = null;
            modal.hide();
            if (oQue) seguir(oQue);
        });

        // Fechar por fora (Esc, clique no fundo, Cancelar) descarta o alvo — senão o próximo
        // "Confirmar" dispararia a ação que a pessoa já tinha desistido de fazer.
        el.raiz.addEventListener("hidden.bs.modal", function () { alvo = null; });

        // O foco começa em Cancelar: Enter sem ler não pode ser o caminho fácil.
        el.raiz.addEventListener("shown.bs.modal", function () { el.cancelar.focus(); });
    });
})();
