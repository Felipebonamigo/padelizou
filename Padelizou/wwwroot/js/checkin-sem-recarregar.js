// MARCAR PRESENÇA NÃO RECARREGA MAIS A PÁGINA (15/09/2026).
//
// 🗣️ Felipe: *"no checkin, ao clicar para marcar, nao deveria atualizar a pagina inteira, como
// estava acontecendo, isso foi alterado ?"* — não tinha sido — e, em seguida: *"sim, faça. o
// jogo tem q subir na hora"*.
//
// 🕳️ CADA BOLINHA ERA UM POST → 302 → GET DA PÁGINA INTEIRA. No torneio de 97 jogos isso é mais
// de 1MB por clique, e são QUATRO cliques por jogo. O que existia era o `data-manter-posicao`,
// que devolve a rolagem pra mesma altura depois da recarga: disfarce do sintoma, não a cura —
// a página some e renasce, o <iframe> da transmissão reinicia junto, e na internet do clube
// cada check custa a espera de uma tela inteira.
//
// ✅ O clique não navega: a bolinha pinta na hora, o POST vai por `fetch`, e a LISTA NOVA é
// pedida uma vez só, no fim da rajada de cliques. É ela que faz o jogo completo subir pro topo
// do horário — a ordem continua sendo a do servidor (Services/OrdemNoHorario), e não uma
// segunda conta em JavaScript que possa discordar do banco.
//
// ⚠️ TRÊS CUIDADOS, E CADA UM DELES JÁ FOI DEFEITO NESTE REPOSITÓRIO:
//
//   • **`resposta.ok` NÃO é prova de que gravou.** Sessão vencida responde 302 pra tela de
//     login, o `fetch` segue o desvio e entrega 200 com o HTML do login — "salvo" sem ter
//     salvo nada (é o mesmo tombo documentado no js/placar-ao-vivo.js). Quem prova aqui é o
//     **204**, que o MarcarCheckIn só devolve pra quem chamou com `X-Requested-With`.
//   • **A lista não pode ser pedida no meio da rajada.** Quem marca os quatro jogadores de um
//     jogo dispara quatro POSTs em dois segundos; uma lista pedida entre eles volta sem os
//     cliques que ainda estão no ar, e a bolinha recém-pintada PISCA de volta pra cinza. Por
//     isso a bandeira `window.pdzMarcandoCheckIn` (que o js/jogos-ao-vivo-atualiza.js lê em
//     `estaOcupado`, igual ao `pdzSalvandoPlacar`) e o pedido único no fim.
//   • **Falha PRECISA aparecer.** Sem a recarga não há nada denunciando o que não gravou: a
//     bolinha volta ao que era, em vermelho, com o aviso no rótulo — e fica assim até alguém
//     tocar de novo. Nunca um check que a tela mostra e o banco não tem.
//
// ⚠️ E O FORMULÁRIO CONTINUA DE PÉ: quem não tem `fetch` (WebView velho, script que não
// carregou) clica e o POST de sempre acontece, com o `data-manter-posicao` devolvendo a
// rolagem. Barrar o clique sem ter como enviar seria o pior dos mundos.
(function () {
    "use strict";

    // Insistir por ~5 segundos quando o atualizador estiver ocupado; depois disso o relógio de
    // 20 em 20 segundos dele resolve sozinho, e martelar mais não ajuda ninguém.
    var TENTATIVAS = 10;
    var ESPERA = 500;

    // ⚠️ A ESPERA QUE AGRUPA A RAJADA (medida no navegador, 15/09/2026). Marcar as quatro
    // bolinhas de um jogo com 120ms entre os toques custava TRÊS buscas da lista inteira —
    // 368kB cada — porque num servidor rápido o POST volta antes do toque seguinte, e cada
    // volta pedia a lista. Meio segundo depois do ÚLTIMO toque agrupa os quatro em uma busca
    // só, e ninguém percebe a diferença: o jogo sobe do mesmo jeito, na hora.
    var ESPERA_DA_LISTA = 500;

    var AVISO = "não salvou — toque pra tentar de novo";

    // Quantos POSTs de check-in estão no ar. Enquanto for maior que zero a tela não pode ser
    // trocada pelo HTML do servidor: ele ainda não sabe dos cliques que estão viajando.
    var emVoo = 0;
    var pediramALista = false;
    var relogioDaLista = 0;

    function agendarALista() {
        if (relogioDaLista) window.clearTimeout(relogioDaLista);
        relogioDaLista = window.setTimeout(function () {
            relogioDaLista = 0;
            pedirAListaNova(TENTATIVAS);
        }, ESPERA_DA_LISTA);
    }

    function pedirAListaNova(restam) {
        if (typeof window.pdzAtualizarAListaDeJogos !== "function") return;
        // `false` = a tela está ocupada agora (placar indo, modal aberto, outra busca em curso).
        if (window.pdzAtualizarAListaDeJogos()) return;
        if (restam > 0) window.setTimeout(function () { pedirAListaNova(restam - 1); }, ESPERA);
    }

    // O rótulo do OUTRO estado viaja num atributo escrito pelo Razor, e os dois trocam de lugar
    // a cada pintura. É o que evita uma segunda cópia do texto aqui dentro — quem escolhe as
    // palavras é a view, em um lugar só (_BotaoDoCheckIn.cshtml).
    function trocarORotulo(botao) {
        var outro = botao.getAttribute("data-rotulo-outro");
        if (outro === null) return;
        botao.setAttribute("data-rotulo-outro", botao.getAttribute("aria-label") || "");
        botao.setAttribute("aria-label", outro);
        botao.setAttribute("title", outro);
    }

    function pintar(form, botao, chegou) {
        botao.classList.toggle("pdz-jl-checkin-chegou", chegou);

        var icone = botao.querySelector("i");
        if (icone) {
            icone.classList.toggle("bi-check-circle-fill", chegou);
            icone.classList.toggle("bi-circle", !chegou);
        }

        // ⚠️ O campo escondido carrega a intenção do PRÓXIMO clique — é o contrário do estado
        // que está na tela, exatamente como o Razor o escreve. Sem virar isto, o segundo toque
        // mandaria marcar de novo em vez de desfazer.
        var campo = form.querySelector('input[name="presente"]');
        if (campo) campo.value = chegou ? "false" : "true";

        trocarORotulo(botao);
    }

    function avisar(botao, erro) {
        botao.classList.toggle("pdz-jl-checkin-erro", erro);
        if (erro) {
            botao.setAttribute("aria-label", AVISO);
            botao.setAttribute("title", AVISO);
        }
    }

    function enviar(form, botao) {
        // O aviso é de UM clique: tocar de novo limpa o vermelho e tenta outra vez.
        avisar(botao, false);

        // A lista que estava agendada espera este clique: pedi-la agora traria uma resposta que
        // ainda não sabe dele. Ela é reagendada quando o POST voltar.
        if (relogioDaLista) { window.clearTimeout(relogioDaLista); relogioDaLista = 0; }

        var campo = form.querySelector('input[name="presente"]');
        var chegou = campo ? campo.value === "true" : true;

        // Lido ANTES de pintar: a pintura já vira o campo escondido pro próximo clique.
        var dados = new FormData(form);

        pintar(form, botao, chegou);

        emVoo++;
        window.pdzMarcandoCheckIn = true;

        window.fetch(form.action, {
            method: "POST",
            body: dados,
            credentials: "same-origin",
            // É este cabeçalho que faz o servidor responder 204 em vez da página inteira.
            headers: { "X-Requested-With": "XMLHttpRequest" },
        })
            .then(function (resposta) {
                if (resposta.status !== 204) throw new Error(resposta.status);
                pediramALista = true;
            })
            .catch(function () {
                pintar(form, botao, !chegou);
                avisar(botao, true);
            })
            .then(function () {
                emVoo--;
                if (emVoo > 0) return;      // a rajada continua: a tela espera o último

                window.pdzMarcandoCheckIn = false;
                if (!pediramALista) return;
                pediramALista = false;
                agendarALista();
            });
    }

    // ⚠️ NO CLIQUE, E NÃO NO `submit`. O js/manter-posicao-na-lista.js ouve `submit` pra guardar
    // a altura da rolagem, e uma altura guardada que ninguém consome atropela a PRÓXIMA visita à
    // página. Barrando o clique, o `submit` nunca acontece — e o atributo continua valendo pra
    // quem cair no caminho sem JavaScript.
    document.addEventListener("click", function (evento) {
        if (evento.defaultPrevented || evento.button !== 0) return;
        if (!window.fetch) return;

        // `closest` e não `evento.target`: no celular o dedo encosta no <i> de dentro do botão.
        var alvo = evento.target;
        var botao = alvo && alvo.closest ? alvo.closest("form[data-pdz-checkin] button") : null;
        if (!botao) return;

        var form = botao.closest("form");
        if (!form) return;

        evento.preventDefault();
        enviar(form, botao);
    });
})();
