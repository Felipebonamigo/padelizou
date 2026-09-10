// O CINTO DAS ABAS RECOLHIDAS — só entra em campo onde o `:has()` não existe.
//
// Depois que a chave é publicada, "Pagamentos e impedimentos" e "Planejamento de quadras" saem
// da barra do torneio (`.pdz-aba-recolhida { display: none }` no site.css) e viram atalho
// dentro do Painel de Controle. Mas o botão CONTINUA no DOM: treze redirects do servidor
// voltam com o fragmento `#pagamentos`, e o script do fim do Details.cshtml abre a aba
// procurando `#torneioTabs [data-bs-target="..."]`. Apagado o botão, o organizador cairia na
// aba padrão toda vez que mexesse num impedimento.
//
// Quem devolve a aba à barra quando ela é a ativa é o CSS:
//     .pdz-aba-recolhida:has(> .nav-link.active) { display: block; }
// e ele é o caminho BOM — declarativo, e resolvido ANTES da primeira pintura, então a barra
// não salta depois de desenhada.
//
// ⚠️ O RISCO QUE ESTE ARQUIVO COBRE: o app do Padelizou **é o próprio site** dentro de uma
// casca TWA (ANDROID.md:3), isto é, roda no WebView do aparelho — que num Android velho pode
// ser antigo. As duas linhas do site.css são REGRAS SEPARADAS: a de `display: none` é válida
// em qualquer navegador e sobrevive; a do `:has()` é descartada inteira por quem não a
// entende. Resultado sem o cinto: a aba ativa fica INVISÍVEL — exatamente o estrago que a
// segunda regra existe pra impedir, e ainda por cima só no aparelho de quem instalou o app.
//
// ⚠️ E POR ISSO ELE SE CALA ONDE O `:has()` FUNCIONA. Dois mecanismos ligados ao mesmo tempo
// fazendo a mesma coisa é a segunda fonte da verdade que um dia diverge da primeira. Aqui a
// regra é: o CSS manda; este arquivo só assume quando o CSS não pode.
(function () {
    "use strict";

    var CLASSE = "pdz-aba-recolhida";

    function oCssResolveSozinho() {
        // `window.CSS` indefinido é o caso mais provável num WebView velho — e `supports()`
        // com um seletor que ele não conhece devolve false, que é o que queremos.
        return !!(window.CSS && window.CSS.supports && window.CSS.supports("selector(:has(*))"));
    }

    function ligar() {
        var barra = document.querySelector("#torneioTabs");
        if (!barra) return;

        // ⚠️ `shown.bs.tab`, e não um `click`: assim o cinto cobre os TRÊS caminhos de uma vez
        // — o clique na aba, o `.click()` dos atalhos do Painel de Controle, e o
        // `bootstrap.Tab...show()` que o script da hash dispara nos redirects.
        barra.addEventListener("shown.bs.tab", function (evento) {
            var itens = barra.querySelectorAll("li.nav-item");

            Array.prototype.forEach.call(itens, function (li) {
                var link = li.querySelector(".nav-link");
                if (!link) return;

                // Quem NASCEU recolhido fica marcado, porque daqui a pouco a classe sai e a
                // única forma de saber que ela deve voltar é esta lembrança. Sem isso, a aba
                // ativa nunca mais se recolheria e a barra iria juntando aba solta a cada
                // clique — a poluição que a mudança veio tirar.
                if (li.classList.contains(CLASSE)) li.dataset.pdzRecolhe = "1";
                if (li.dataset.pdzRecolhe !== "1") return;

                if (link === evento.target) li.classList.remove(CLASSE);
                else li.classList.add(CLASSE);
            });
        });
    }

    if (oCssResolveSozinho()) return;

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", ligar);
    else ligar();
})();
