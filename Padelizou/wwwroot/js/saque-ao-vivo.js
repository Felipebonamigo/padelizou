// O toque que passa a bolinha do saque pra outra dupla, no card AO VIVO.
//
// O saque vira muitas vezes num jogo só (🗣️ Felipe, 11/09/2026: "normalmente se vira após o 3º
// game, depois de 2 em 2, até finalizar a partida"), e até hoje cada troca custava abrir o
// Controle de Partida, escolher, salvar e voltar. Quatro telas por game é o mesmo motivo que
// fez o campo viver vazio e a bolinha nunca aparecer pra ninguém.
//
// ⚠️ O HTML JÁ FUNCIONA SEM ESTE ARQUIVO: cada bolinha é um <form> de verdade (ver
// Views/Torneios/_BolinhaDoSaque.cshtml), então sem JavaScript o toque continua trocando o
// saque por POST + volta. O que este arquivo tira é a RECARGA — que reiniciaria o <iframe> da
// transmissão e jogaria a tela pro topo da lista, os dois defeitos que o −/+ do placar já
// deixou de causar (ver js/placar-ao-vivo.js e js/jogos-ao-vivo-atualiza.js).
//
// A view não escreve JavaScript nenhum: ela põe o formulário e este arquivo faz o resto — o
// mesmo padrão do confirmar.js e do placar-ao-vivo.js.
(function () {
    "use strict";

    function card(de) {
        return de && de.closest ? de.closest(".pdz-live-card") : null;
    }

    // O aviso mora no card que está sendo mexido — é pra lá que a pessoa está olhando. É o
    // mesmo lugar onde o placar diz "salvo" (.pdz-live-salvo).
    function avisar(onde, estado, texto) {
        var alvo = onde && onde.querySelector(".pdz-live-salvo");
        if (!alvo) return;
        alvo.className = "pdz-live-salvo" + (estado ? " pdz-live-salvo-" + estado : "");
        alvo.textContent = texto || "";
    }

    // Acende a bola de uma dupla e apaga a da outra, DENTRO do card. Quem manda é a resposta
    // do servidor, nunca o que foi clicado: é ele que diz o que ficou gravado.
    function pintar(noCard, duplaSacandoId) {
        var formas = noCard.querySelectorAll(".pdz-saque-forma");
        Array.prototype.forEach.call(formas, function (forma) {
            var campo = forma.querySelector('input[name="duplaId"]');
            var botao = forma.querySelector(".pdz-saque-toque");
            var bola = forma.querySelector(".pdz-bolinha");
            if (!campo || !botao || !bola) return;

            var sacando = String(campo.value) === String(duplaSacandoId);
            botao.setAttribute("aria-pressed", sacando ? "true" : "false");
            botao.title = sacando ? "Está sacando" : "Passar o saque pra esta dupla";
            botao.setAttribute("aria-label", botao.title);
            bola.classList.toggle("pdz-bolinha-quica", sacando);
            bola.classList.toggle("pdz-bolinha-apagada", !sacando);
        });
    }

    function enviar(forma) {
        var noCard = card(forma);
        var campo = forma.querySelector('input[name="duplaId"]');
        if (!campo) return;

        // A bola MUDA DE LADO NA HORA. No 3G do clube a resposta demora, e um alvo que não
        // responde ao toque é tocado de novo — e de novo. Se o servidor recusar, o `catch`
        // devolve a bola pro lado de onde ela saiu e o card diz o que houve.
        var antes = null;
        var aceso = noCard && noCard.querySelector(".pdz-bolinha-quica");
        if (aceso) {
            var formaAcesa = aceso.closest(".pdz-saque-forma");
            var campoAceso = formaAcesa && formaAcesa.querySelector('input[name="duplaId"]');
            if (campoAceso) antes = campoAceso.value;
        }
        if (noCard) pintar(noCard, campo.value);

        // ⚠️ Trava a atualização automática enquanto o saque está indo: ela troca o cabeçalho
        // do card pelo HTML do servidor a cada 20s, e o servidor ainda não sabe desta troca —
        // a bolinha voltaria pro lado velho na frente de quem acabou de mover. É a mesma
        // ideia da bandeira do placar (js/jogos-ao-vivo-atualiza.js, estaOcupado).
        //
        // ⚠️ BANDEIRA PRÓPRIA, E NÃO A `pdzSalvandoPlacar` DO PLACAR. Compartilhar a dela
        // abriria uma corrida: um game salvando junto com uma troca de saque, e quem
        // terminasse primeiro baixaria a bandeira do outro — a atualização automática entraria
        // no meio do salvamento que continua em pé e devolveria o placar velho pra tela, que é
        // exatamente o que essas bandeiras existem pra impedir.
        window.pdzTrocandoSaque = true;
        avisar(noCard, "indo", "salvando…");

        window.fetch(forma.action, {
            method: "POST",
            body: new FormData(forma),
            credentials: "same-origin",
            // O cabeçalho é o que pede a resposta em JSON — e é o JSON que prova que salvou.
            headers: { "X-Requested-With": "XMLHttpRequest" },
        })
            .then(function (resposta) {
                // ⚠️ `resposta.ok` NÃO basta: sessão vencida responde 302 pra tela de login, o
                // `fetch` segue o desvio e entrega 200 com o HTML do login. Sem esta checagem,
                // "não trocou nada" apareceria como trocado.
                var tipo = resposta.headers.get("content-type") || "";
                if (!resposta.ok || tipo.indexOf("json") === -1) throw new Error(resposta.status);
                return resposta.json();
            })
            .then(function (dados) {
                if (noCard) pintar(noCard, dados && dados.duplaSacandoId);
                avisar(noCard, "ok", "saque trocado");
                window.setTimeout(function () { avisar(noCard, "", ""); }, 2000);
            })
            .catch(function () {
                // Falha PRECISA aparecer, e a bola volta pro lado certo: deixá-la do lado novo
                // seria a tela mentindo sobre o que está gravado — e é a tela do torcedor.
                if (noCard && antes !== null) pintar(noCard, antes);
                avisar(noCard, "erro", "não trocou — toque de novo");
            })
            .then(function () { window.pdzTrocandoSaque = false; });
    }

    // No DOCUMENTO: a lista de jogos é remontada por filtro, por troca de aba e pela
    // atualização automática, e ligar formulário por formulário faria o que nascesse depois
    // parar de funcionar — calado.
    document.addEventListener("submit", function (e) {
        var forma = e.target;
        if (!forma || !forma.classList || !forma.classList.contains("pdz-saque-forma")) return;
        if (!window.fetch) return;   // navegador sem fetch: deixa o POST de sempre acontecer

        e.preventDefault();

        // Tocar na bola que JÁ está sacando não é uma troca: seria um POST pra gravar o que já
        // está gravado, e um piscar de "salvando…" sem nada ter mudado.
        var botao = forma.querySelector(".pdz-saque-toque");
        if (botao && botao.getAttribute("aria-pressed") === "true") return;

        enviar(forma);
    });
})();
