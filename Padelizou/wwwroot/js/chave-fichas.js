// AS FICHAS DA PRÉVIA DO MATA-MATA ("OITAVAS · QUARTAS · SEMIFINAL · FINAL") ROLAM O TRILHO.
//
// 🗣️ Felipe, 12/09/2026, com o print da prévia no celular: *"Esse botao de oitavas quartas semi
// e final, as vezes n funciona"*. Ele estava certo, e eram DOIS defeitos somados — por isso o
// "às vezes". Os dois medidos no Chromium a 412px, com o `site.css` de verdade:
//
//   1. O partial é desenhado UMA VEZ POR CATEGORIA, e o `id` da rodada não tinha a categoria:
//      sete categorias escreviam os mesmos quatro ids. `href="#..."` acha o PRIMEIRO do
//      documento, que mora num painel escondido (`display: none`) — e rolar pra um elemento
//      invisível não faz nada. `scrollLeft` ficava em 0 nos quatro cliques. O id virou único
//      (ver _ChaveProjetadaArvore.cshtml), e é por isso que a busca aqui é DENTRO do quadro.
//
//   2. O ENCAIXE DESFAZIA O PULO. O trilho é `scroll-snap-type: x mandatory` e a navegação por
//      âncora alinha com `inline: nearest` — o mínimo pra caber. A rodada ocupa 66% da tela,
//      então esse mínimo para ENTRE dois pontos de encaixe e o snap puxa de volta pro anterior:
//      QUARTAS não saía do lugar, SEMIFINAL parava nas QUARTAS, FINAL parava na SEMIFINAL.
//
// ⚠️ `preventDefault()` É O CONSERTO DE 2, e não um detalhe: rolando o trilho na mão a parada é
// EXATAMENTE o ponto de encaixe, e o snap não tem o que desfazer. De quebra, some o pulo
// VERTICAL da âncora, que levava a própria barra de fichas pra fora da tela (medido: scrollY
// 0 → 639) — quem clicava numa rodada perdia o botão da seguinte.
//
// O `href` continua no HTML: sem JS ele ainda leva à rodada certa (imprecisa pelo encaixe, mas
// no lugar certo da página), e é o que dá sentido de link pra quem navega pelo teclado.
(function () {
    "use strict";

    var quadros = document.querySelectorAll(".pdz-chd");

    Array.prototype.forEach.call(quadros, function (quadro) {
        var trilho = quadro.querySelector(".pdz-chd-trilho");
        if (!trilho) return;

        Array.prototype.forEach.call(quadro.querySelectorAll(".pdz-chd-ficha"), function (ficha) {
            ficha.addEventListener("click", function (evento) {
                // ⚠️ A BUSCA É NO QUADRO, e nunca `document.getElementById`: o id é único hoje,
                // mas quem procura no documento inteiro volta a achar o painel escondido no dia
                // em que uma tela nova desenhar duas prévias. Aqui, o pior caso é não achar.
                var alvo = quadro.querySelector(ficha.getAttribute("href") || "");
                if (!alvo) return;

                evento.preventDefault();

                // Relativo à TELA, e não `offsetLeft`: a conta continua certa com o trilho já
                // rolado (voltar pras oitavas) e não depende de quem é o `offsetParent`.
                trilho.scrollTo({
                    left: trilho.scrollLeft
                        + alvo.getBoundingClientRect().left
                        - trilho.getBoundingClientRect().left,
                    behavior: "smooth"
                });
            });
        });
    });
})();
