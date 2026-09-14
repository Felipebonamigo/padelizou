// FINALIZAR E "VOLTAR PRA AGENDADO" SEM RECARREGAR A PÁGINA (14/09/2026).
//
// 🗣️ Felipe: *"apenas queria q o video nao travasse, nao mude o layout"*.
//
// 🕳️ Os dois botões do cartão AO VIVO eram POST comum: o navegador recarregava a página
// inteira, e RECARGA REINICIA TODO <iframe> da tela. Finalizar o jogo da Quadra 1 parava o
// vídeo de quem estava assistindo à Quadra 2 — sem nenhuma relação entre as duas coisas.
//
// É o mesmo caminho que o js/placar-ao-vivo.js e o js/saque-ao-vivo.js já abriram pro −/+ e pra
// bolinha do saque. Estes dois ficaram de fora, e eram os que mais doíam: o Finalizar é o botão
// que o organizador aperta a cada jogo.
//
// ⚠️ MAS A PROVA DE QUE DEU CERTO É OUTRA, E É O CORAÇÃO DESTE ARQUIVO. Nos outros dois a
// resposta é JSON, e exigir `content-type: json` é o que separa "salvou" de "a sessão caiu e
// isto é a tela de login" — o fetch SEGUE o 302 e entrega 200 com o HTML do login. Aqui o
// servidor responde com redirect pra PÁGINA, então JSON não existe: a prova é a LISTA DE JOGOS
// estar na resposta. Quem confere isso é o `pdzAplicarRespostaDeAcao`
// (js/jogos-ao-vivo-atualiza.js), e quando ele recusa a gente RECARREGA — que é exatamente o
// que teria acontecido sem a interceptação, e leva a pessoa pro login em vez de deixar a tela
// mentindo que finalizou.
(function () {
    "use strict";

    function avisar(texto) {
        var caixa = document.querySelector("#pdzAvisoDaAcao");
        if (!caixa) { window.alert(texto); return; }

        // Texto nosso, escrito aqui em cima — nada que venha do servidor ou da pessoa passa por
        // esta linha. Se um dia passar, ela precisa de escape antes.
        caixa.innerHTML = '<div class="alert alert-danger">' + texto + '</div>';
        if (caixa.scrollIntoView) caixa.scrollIntoView({ block: "center" });
    }

    document.addEventListener("submit", function (e) {
        var form = e.target && e.target.closest ? e.target.closest(".pdz-live-acao") : null;
        if (!form) return;

        // ⚠️ A PERGUNTA VEM PRIMEIRO. O confirmar.js abre o modal e só no "sim" reenvia com
        // requestSubmit(), que dispara ESTE ouvinte de novo, já com a marca. Interceptar antes
        // dela mandaria o POST sem perguntar nada — o Finalizar viraria um toque sem volta.
        if (form.hasAttribute("data-confirmar") && form.getAttribute("data-confirmado") !== "1") return;

        // Navegador sem fetch: o envio de sempre acontece, com recarga. Pior que o ideal, muito
        // melhor que um botão morto.
        if (!window.fetch) return;

        e.preventDefault();

        // ⚠️ A MARCA SAI AGORA, e não é arrumação. Sem recarga o formulário CONTINUA na tela, e
        // a marca que o confirmar.js deixou faria o PRÓXIMO Finalizar não perguntar nada — um
        // toque sem volta, aberto justamente por tirar a recarga.
        form.removeAttribute("data-confirmado");

        var botao = form.querySelector("[type=submit]");
        if (botao) botao.disabled = true;
        function liberar() { if (botao) botao.disabled = false; }

        // Segura a atualização automática enquanto a ação está indo: ela busca a página de 20 em
        // 20s, e uma resposta pedida ANTES deste POST chegando DEPOIS dele devolveria a tela pro
        // estado de antes — o jogo finalizado reaparecendo em quadra. Mesma bandeira do
        // js/placar-ao-vivo.js e do js/saque-ao-vivo.js.
        window.pdzAcaoEmCurso = true;
        function soltar() { window.pdzAcaoEmCurso = false; }

        window.fetch(form.action, {
            method: "POST",
            // O formulário INTEIRO: é ele que leva o carimbo antifalsificação e os campos
            // escondidos (partidaId, voltarPara, o placar que está na tela).
            body: new window.FormData(form),
            credentials: "same-origin",
        })
            .then(function (resposta) {
                if (!resposta.ok) {
                    var recusa = new Error("o servidor recusou (erro " + resposta.status + ")");
                    recusa.pdzMotivo = true;
                    throw recusa;
                }
                return resposta.text();
            })
            .then(function (html) {
                // O HTML da resposta JÁ É a página depois da ação — o fetch seguiu o redirect.
                // Remendar com ele é uma busca a menos e, principalmente, uma cópia a menos das
                // regras de quem pode ser trocado na tela.
                var aplicou = typeof window.pdzAplicarRespostaDeAcao === "function"
                    && window.pdzAplicarRespostaDeAcao(html);

                soltar();
                if (!aplicou) { window.location.reload(); return; }
                liberar();
            })
            .catch(function (erro) {
                soltar();
                avisar("Não deu pra concluir: "
                    + (erro && erro.pdzMotivo ? erro.message : "sem resposta do servidor")
                    + ". A tela pode estar desatualizada — recarregue a página pra conferir.");
                liberar();
            });
    });
})();
