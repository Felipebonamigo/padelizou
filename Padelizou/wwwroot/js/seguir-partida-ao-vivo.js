// "Seguir este jogo": placar chegando no celular sem manter o site aberto (16/08/2026,
// Felipe: "algo parecido com o placar que o Google mostra na tela de bloqueio").
//
// ⚠️ DELEGAÇÃO, não um listener por botão: o cabeçalho do cartão AO VIVO se REDESENHA sozinho
// a cada 20s (js/jogos-ao-vivo-atualiza.js), então o `.pdz-live-seguir` de agora pode não ser
// o mesmo elemento DOM daqui a 20 segundos. Um listener preso ao botão original pararia de
// responder assim que o primeiro tique trocasse o HTML por baixo dele.
(function () {
    "use strict";

    function botao(alvo) {
        return alvo.closest ? alvo.closest(".pdz-live-seguir") : null;
    }

    async function alternar(btn) {
        var partidaId = btn.getAttribute("data-partida-id");
        var seguindoAgora = btn.getAttribute("data-seguindo") === "true";

        btn.disabled = true;

        try {
            if (!seguindoAgora) {
                // Seguir SEM push habilitado seria um botão que promete e não entrega — a
                // pessoa marcaria "Seguindo" e nunca receberia nada, sem saber por quê.
                // `ativarNotificacoesPush` já cuida de pedir permissão só quando falta
                // (concedida antes, `Notification.requestPermission()` nem abre caixa).
                var status = await statusNotificacoesPush();
                if (status !== "subscribed") {
                    var ok = await ativarNotificacoesPush();
                    if (!ok) { btn.disabled = false; return; }
                }
            }

            var url = "/Torneios/" + (seguindoAgora ? "PararDeSeguirPartidaAoVivo" : "SeguirPartidaAoVivo");
            var resposta = await fetch(url, {
                method: "POST",
                headers: cabecalhoAntifalsificacao({ "Content-Type": "application/x-www-form-urlencoded" }),
                body: "partidaId=" + encodeURIComponent(partidaId),
            });

            // Sessão vencida ou jogo que saiu do ar entre um tique e outro (404): a tela não
            // mente sobre o que não aconteceu — o botão volta pro estado de antes do clique.
            if (!resposta.ok) { btn.disabled = false; return; }

            var dados = await resposta.json();
            aplicarEstado(btn, dados.seguindo);
        } catch (e) {
            // Rede caiu no meio: mesma régua, o botão não finge que funcionou.
        } finally {
            btn.disabled = false;
        }
    }

    function aplicarEstado(btn, seguindo) {
        btn.setAttribute("data-seguindo", seguindo ? "true" : "false");
        btn.setAttribute("aria-pressed", seguindo ? "true" : "false");
        btn.classList.toggle("pdz-live-seguindo", seguindo);

        var icone = btn.querySelector("i");
        if (icone) icone.className = "bi " + (seguindo ? "bi-bell-fill" : "bi-bell");

        var texto = btn.querySelector(".pdz-live-seguir-texto");
        if (texto) texto.textContent = seguindo ? "Seguindo" : "Seguir este jogo";
    }

    document.addEventListener("click", function (e) {
        var btn = botao(e.target);
        if (btn) alternar(btn);
    });
})();
