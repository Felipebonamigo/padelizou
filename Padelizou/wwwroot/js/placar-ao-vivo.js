// Os botões − e + do placar na lista de jogos AO VIVO, e o salvamento ao sair do campo.
//
// Digitar no campo exige mirar num alvo de dois caracteres, com o celular numa mão e a
// quadra rolando. Pior: quem edita e não aperta "Salvar placares" acha que marcou e não
// marcou — "editei e não salvou" é o pior jeito de um placar falhar, porque só se descobre
// quando alguém reclama do resultado.
//
// Por isso cada toque no − ou + JÁ ENVIA o formulário. Um toque = um game gravado.
//
// ⚠️ E DIGITAR TAMBÉM SALVA, ao sair do campo (Felipe, 08/08/2026): antes, quem digitava o
// número precisava ainda apertar Enter ou achar o "Salvar placares" — e sair do campo sem
// isso perdia o que tinha acabado de digitar, calado. Exatamente a falha que os botões −/+
// existiam pra evitar, sobrevivendo no caminho de quem prefere digitar.
//
// O evento é o `change`, e não o `blur`: ele só dispara quando o valor REALMENTE mudou, então
// entrar e sair de um campo sem editar não manda POST nenhum. Os botões −/+ escrevem o valor
// por código, o que não dispara `change` — não há envio em dobro.
//
// A view não escreve JavaScript nenhum: ela põe os botões com data-passo e este arquivo faz
// o resto — o mesmo padrão de confirmar.js.
(function () {
    "use strict";

    // Um envio só. Sem isto, sair do campo com Enter mandaria dois POSTs (o `change` daqui e
    // a submissão implícita do navegador), e no 3G do clube os dois chegam.
    var enviando = false;

    function enviar(campo) {
        if (enviando) return;

        // `form` por atributo: os campos moram dentro dos cards e o formulário fica fora
        // deles (formulário aninhado seria HTML inválido por causa do palpitrômetro).
        var form = campo.form || document.getElementById("pdzPlacaresAoVivo");
        if (!form) return;

        enviando = true;

        // Trava o duplo toque: até a página recarregar, mais um clique enviaria um segundo
        // POST com o mesmo número — e no 3G do clube isso acontece.
        Array.prototype.forEach.call(document.querySelectorAll(".pdz-live-passo"), function (b) {
            b.disabled = true;
        });

        if (typeof form.requestSubmit === "function") form.requestSubmit();
        else form.submit();
    }

    function passo(botao) {
        var contador = botao.closest(".pdz-live-contador");
        if (!contador) return;

        var campo = contador.querySelector("input[type=number]");
        if (!campo) return;

        var atual = parseInt(campo.value, 10);
        if (isNaN(atual)) atual = 0;

        var novo = atual + parseInt(botao.getAttribute("data-passo"), 10);

        // O teto de verdade é do servidor (a fase manda: jogo até 4 não chega a 9). Aqui é só
        // pra não mandar número negativo nem absurdo.
        if (novo < 0) novo = 0;
        if (novo > 99) novo = 99;
        if (novo === atual) return;

        campo.value = novo;
        enviar(campo);
    }

    // No DOCUMENTO: a lista de jogos é remontada por filtro e por troca de aba, e ligar
    // botão por botão faria o que nascesse depois parar de funcionar — calado.
    document.addEventListener("click", function (e) {
        var botao = e.target.closest ? e.target.closest(".pdz-live-passo") : null;
        if (!botao || botao.disabled) return;

        e.preventDefault();
        passo(botao);
    });

    // Tocar no campo já SELECIONA o número. Sem isto o cursor cai ao lado do "0" e a pessoa
    // digita "04" — ou apaga primeiro e digita depois, dois toques pra marcar um game, de pé
    // e com a quadra rolando. Vale pro clique e pro foco por teclado.
    function selecionar(e) {
        var campo = e.target;
        if (!campo.classList || !campo.classList.contains("pdz-live-input")) return;

        // `setTimeout` porque o clique posiciona o cursor DEPOIS do focus — selecionar antes
        // disso não adianta, o próprio navegador desfaz.
        window.setTimeout(function () { campo.select(); }, 0);
    }

    document.addEventListener("focusin", selecionar);
    document.addEventListener("click", selecionar);

    // Sair do campo com o número mudado JÁ SALVA — sem Enter e sem procurar botão nenhum.
    // No DOCUMENTO pelo mesmo motivo dos botões: a lista é remontada por filtro e por troca
    // de aba, e ligar campo por campo faria o que nascesse depois parar de salvar, calado.
    document.addEventListener("change", function (e) {
        var campo = e.target;
        if (!campo.classList || !campo.classList.contains("pdz-live-input")) return;
        enviar(campo);
    });
})();
