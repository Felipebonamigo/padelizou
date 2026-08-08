// Os botões − e + do placar na lista de jogos AO VIVO, e o salvamento ao sair do campo.
//
// Digitar no campo exige mirar num alvo de dois caracteres, com o celular numa mão e a
// quadra rolando. Pior: quem edita e não aperta "Salvar placares" acha que marcou e não
// marcou — "editei e não salvou" é o pior jeito de um placar falhar, porque só se descobre
// quando alguém reclama do resultado.
//
// Por isso cada toque no − ou + JÁ ENVIA o placar. Um toque = um game gravado.
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
// ⚠️ E O ENVIO NÃO RECARREGA MAIS A PÁGINA (Carol, 08/08/2026: "ao clicar no + pra adicionar
// um game, a tela ia lá pra cima e recarregava"). O formulário era submetido de verdade: POST,
// redirect, página nova — e a página nova nasce no TOPO. Com 5 cards abertos, marcar um game
// no último jogo custava rolar a lista inteira de volta, a cada game, a noite toda. De quebra
// o reload REINICIA o <iframe> da transmissão, que é o defeito que a atualização automática
// acabou de deixar de causar (ver js/jogos-ao-vivo-atualiza.js).
//
// Agora o mesmo formulário vai por `fetch`: o servidor recebe exatamente o que recebia antes
// (é o `FormData` do form inteiro, com o token do antiforgery dentro), e a tela não sai do
// lugar. O que a pessoa vê é o card dizendo "salvando…" e depois "salvo".
//
// A view não escreve JavaScript nenhum: ela põe os botões com data-passo e este arquivo faz
// o resto — o mesmo padrão de confirmar.js.
(function () {
    "use strict";

    var ESPERA_MS = 450;   // junta toques seguidos num POST só (3 toques rápidos = 1 envio)

    function formulario() {
        return document.getElementById("pdzPlacaresAoVivo");
    }

    // O aviso mora no card que está sendo editado — é pra lá que a pessoa está olhando.
    function avisar(card, estado, texto) {
        if (!card) return;
        var onde = card.querySelector(".pdz-live-salvo");
        if (!onde) return;

        onde.className = "pdz-live-salvo" + (estado ? " pdz-live-salvo-" + estado : "");
        onde.textContent = texto || "";
    }

    var enviando = false;
    var pendente = false;
    var agendado = null;
    var cardsMexidos = [];

    // Escreve na tela o placar que o servidor gravou. Só mexe no campo que a pessoa NÃO está
    // editando naquele instante — corrigir por baixo do dedo dela apagaria o que ela está
    // digitando agora, que é o oposto do que este arquivo existe pra evitar.
    function aplicarPlacarDoServidor(placares) {
        if (!placares || !placares.length) return;

        placares.forEach(function (linha) {
            var card = document.querySelector('.pdz-live-card[data-partida-id="' + linha.partidaId + '"]');
            if (!card) return;

            var campos = card.querySelectorAll(".pdz-live-input");
            [linha.games1, linha.games2].forEach(function (valor, i) {
                var campo = campos[i];
                if (!campo || campo === document.activeElement) return;
                if (String(valor) !== campo.value) campo.value = valor;
            });
        });
    }

    function marcarTodos(estado, texto) {
        cardsMexidos.forEach(function (card) { avisar(card, estado, texto); });
    }

    function enviarAgora() {
        var form = formulario();
        if (!form) return;

        // Um POST por vez. Toque que chega no meio do envio não é perdido: ele marca
        // `pendente` e o próximo envio sai com o valor mais novo da tela.
        if (enviando) { pendente = true; return; }

        enviando = true;
        // ⚠️ Trava a atualização automática enquanto o placar está indo: ela troca o
        // cabeçalho do card pelo HTML do servidor, e o servidor ainda não sabe deste game —
        // o número recém-marcado voltaria pro valor velho na frente da pessoa.
        window.pdzSalvandoPlacar = true;
        marcarTodos("indo", "salvando…");

        window.fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            credentials: "same-origin",
            // O cabeçalho é o que pede a resposta em JSON — e é o JSON que prova que salvou.
            headers: { "X-Requested-With": "XMLHttpRequest" },
        })
            .then(function (resposta) {
                // ⚠️ `resposta.ok` NÃO basta: sessão vencida responde 302 pra tela de login, o
                // `fetch` segue o desvio e entrega 200 com o HTML do login. Sem esta checagem,
                // "não salvou nada" apareceria como "salvo".
                var tipo = resposta.headers.get("content-type") || "";
                if (!resposta.ok || tipo.indexOf("json") === -1) throw new Error(resposta.status);
                return resposta.json();
            })
            .then(function (dados) {
                // O SERVIDOR TEM A ÚLTIMA PALAVRA sobre o número. Ele corrige o que recebeu (o
                // teto da fase manda: numa soma de 5, um 6 vira 4) e recusa jogo que saiu do
                // ar. Com a recarga, a tela voltava do servidor já certa; sem ela, seria a
                // tela mentindo sobre o que está gravado.
                aplicarPlacarDoServidor(dados && dados.placares);
                marcarTodos("ok", "salvo");
                // O "salvo" some sozinho; o card volta a ser só o card.
                window.setTimeout(function () { marcarTodos("", ""); cardsMexidos = []; }, 2500);
            })
            .catch(function () {
                // ⚠️ Falha PRECISA aparecer. O placar continua na tela (não se apaga o que a
                // pessoa marcou), mas ela tem que saber que o servidor não recebeu — senão é
                // o "editei e não salvou" de novo, agora silencioso porque não há recarga
                // pra denunciar.
                marcarTodos("erro", "não salvou — toque de novo");
            })
            .then(function () {
                enviando = false;
                window.pdzSalvandoPlacar = pendente;
                if (pendente) { pendente = false; enviarAgora(); }
            });
    }

    function agendarEnvio(campo) {
        var card = campo && campo.closest ? campo.closest(".pdz-live-card") : null;
        if (card && cardsMexidos.indexOf(card) === -1) cardsMexidos.push(card);

        window.clearTimeout(agendado);
        agendado = window.setTimeout(enviarAgora, ESPERA_MS);
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
        agendarEnvio(campo);
    }

    // No DOCUMENTO: a lista de jogos é remontada por filtro, por troca de aba e pela
    // atualização automática, e ligar botão por botão faria o que nascesse depois parar de
    // funcionar — calado.
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
    //
    // ⚠️ Aqui existia uma trava (`cliqueEmAcao`) pra NÃO salvar quando o `change` vinha de um
    // clique em botão: a ordem dos eventos é pointerdown → blur → change → click, e o salvar
    // recarregava a página no meio, ENGOLINDO o clique em "Finalizar" — a pessoa clicava,
    // nada acontecia, clicava de novo. Sem recarga não há clique a engolir, e a trava passou
    // a custar caro: o game digitado antes de tocar em qualquer botão ficaria sem salvar.
    document.addEventListener("change", function (e) {
        var campo = e.target;
        if (!campo.classList || !campo.classList.contains("pdz-live-input")) return;

        agendarEnvio(campo);
    });

    // O botão "Salvar placares" e o Enter no campo passam pelo MESMO caminho: sem isto eles
    // continuariam submetendo o formulário de verdade, com recarga, topo da página e vídeo
    // reiniciado — o defeito que este arquivo acabou de tirar do −/+.
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (!form || form.id !== "pdzPlacaresAoVivo") return;
        if (!window.fetch) return;   // navegador sem fetch: deixa o envio de sempre acontecer

        e.preventDefault();

        // Salvar a mão pede resposta em todos os cards, e não só nos que foram tocados.
        cardsMexidos = Array.prototype.slice.call(document.querySelectorAll(".pdz-live-card"));
        window.clearTimeout(agendado);
        enviarAgora();
    });
})();
