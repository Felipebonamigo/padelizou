// O QUE ESTA TROCA FAZ COM O CONFERIR GRADE — mostrado no modal, antes do Trocar.
//
// 🗣️ Felipe, 10/09/2026: *"veja para avisar se o jogo q eu trocar altera algo do 'conferir grade',
// por exemplo, se vai atrapalhar o impedimento, restrição ou jogos seguidos"*.
//
// A conta é do servidor (Services/ImpactoDaTroca, a mesma régua do Conferir grade e do reparo).
// Aqui só se pergunta e se pinta. ⚠️ NÃO BLOQUEIA o botão de propósito: quem decide é o
// organizador — às vezes ele troca sabendo que piora, porque combinou com a dupla no telefone.
(function () {
    "use strict";

    var select = document.getElementById("trocaJogoB");
    var jogoA = document.getElementById("trocaJogoA");
    var aviso = document.getElementById("trocaImpacto");
    if (!select || !jogoA || !aviso) return;

    var CORES = {
        perigo: { classe: "alert-danger", icone: "bi-exclamation-octagon-fill" },
        atencao: { classe: "alert-warning", icone: "bi-exclamation-triangle-fill" },
        igual: { classe: "alert-secondary", icone: "bi-check2" },
        melhora: { classe: "alert-success", icone: "bi-stars" },
        previa: { classe: "alert-secondary", icone: "bi-hourglass-split" },
        impossivel: { classe: "alert-danger", icone: "bi-slash-circle" },
    };

    // A última consulta manda: escolher rápido duas vezes não pode deixar a resposta velha na tela.
    var consulta = 0;

    function esconder() {
        aviso.className = "alert py-2 px-3 mt-2 mb-0 small d-none";
        aviso.textContent = "";
    }

    function mostrar(grau, texto) {
        var cor = CORES[grau] || CORES.igual;
        aviso.className = "alert py-2 px-3 mt-2 mb-0 small " + cor.classe;
        aviso.innerHTML = '<i class="bi ' + cor.icone + '"></i> ' + texto;
    }

    select.addEventListener("change", function () {
        if (!select.value || !jogoA.value) return esconder();

        var url = select.dataset.impactoUrl;
        if (!url) return esconder();

        var meu = ++consulta;
        mostrar("igual", "Conferindo o que muda…");

        var separador = url.indexOf("?") >= 0 ? "&" : "?";
        fetch(url + separador + "jogoA=" + encodeURIComponent(jogoA.value)
                  + "&jogoB=" + encodeURIComponent(select.value), { credentials: "same-origin" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (d) {
                if (meu !== consulta) return;              // chegou depois de outra escolha
                // Sem resposta não se inventa: melhor a tela calada que um "tudo certo" falso.
                if (!d) return esconder();
                mostrar(d.grau, d.texto);
            })
            .catch(function () { if (meu === consulta) esconder(); });
    });

    // Modal reaberto em outro jogo: a resposta do anterior não vale mais.
    var modal = document.getElementById("modalTrocarHorario");
    if (modal) modal.addEventListener("show.bs.modal", esconder);
})();
