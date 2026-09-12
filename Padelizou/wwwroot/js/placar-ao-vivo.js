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
// Agora o mesmo formulário vai por `fetch` e a tela não sai do lugar: o que a pessoa vê é o
// card dizendo "salvando…" e depois "salvo".
//
// ⚠️ E O QUE VIAJA É SÓ O CARD QUE FOI TOCADO (12/09/2026). O corpo era o `FormData` do
// formulário INTEIRO — que, por causa do `form="pdzPlacaresAoVivo"` nos campos de cada card, é
// TODA quadra no ar. Com dois marcadores no mesmo torneio, o toque de um reescrevia a quadra
// do outro com o número que ESTA tela tinha, até 20 segundos velho: o game do vizinho voltava
// pro valor anterior sem erro em lugar nenhum, e quem perdia o trabalho era quem não tocou em
// nada. Ver `corpoDoLote` e Padelizou.Tests/js/conferir-placar-ao-vivo.js.
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

    // OS CARDS COM TOQUE AINDA NÃO CONFIRMADO PELO SERVIDOR — a fila deste aparelho.
    //
    // ⚠️ ELA É O QUE VAI NO POST, e nada além dela (12/09/2026). O formulário do lote reúne
    // TODAS as quadras no ar: os campos moram dentro dos cards e se ligam a ele por
    // `form="pdzPlacaresAoVivo"`, então `new FormData(form)` — que era o corpo daqui — mandava
    // o torneio inteiro a cada toque, com o valor que ESTA tela tinha. Com dois marcadores
    // trabalhando, o toque de um reescrevia a quadra do outro com um placar de até 20 segundos
    // atrás (o intervalo da atualização automática é quem traz o do vizinho). Quem via o game
    // sumir era justamente quem não tocou em nada, e não havia erro em lugar nenhum.
    var mexidos = [];

    function marcarMexido(card) {
        if (!card || mexidos.indexOf(card) !== -1) return;
        mexidos.push(card);
        // A bandeira fica no CARD porque quem também precisa dela é a atualização automática
        // (js/jogos-ao-vivo-atualiza.js): entre o dedo e o POST há o meio segundo do debounce,
        // e trocar o cabeçalho nesse vão devolve o número velho — que é o que o POST lê em
        // seguida. O toque sumiria inteiro.
        card.setAttribute("data-pdz-mexido", "");
    }

    // A marca só sai quando o servidor confirmou E ninguém tocou no card de novo no meio do
    // caminho.
    function confirmar(card) {
        if (mexidos.indexOf(card) === -1) card.removeAttribute("data-pdz-mexido");
    }

    // Escreve na tela o placar que o servidor gravou. Só mexe no campo que a pessoa NÃO está
    // editando naquele instante — corrigir por baixo do dedo dela apagaria o que ela está
    // digitando agora, que é o oposto do que este arquivo existe pra evitar.
    function aplicarPlacarDoServidor(placares) {
        if (!placares || !placares.length) return;

        placares.forEach(function (linha) {
            var card = document.querySelector('.pdz-live-card[data-partida-id="' + linha.partidaId + '"]');
            if (!card) return;

            // ⚠️ O VERDE ANDA JUNTO COM O NÚMERO (11/09/2026). A classe só nascia no HTML
            // do servidor, então um 9 x 8 corrigido pra 8 x 8 ficava com o empate pintado de
            // verde: a atualização automática arrumaria no tique seguinte, mas ela NÃO roda
            // com o cursor dentro do campo (`estaOcupado` em jogos-ao-vivo-atualiza.js) — quem
            // digita em vez de tocar no −/+ ficava com a cor errada por tempo indeterminado.
            //
            // ⚠️ Quem VENCEU é decidido pelo SERVIDOR e vem na resposta: a régua tem soma ×
            // "até" e o desempate do "vencer por dois" (Services/QuemVenceu.LadoJaDecidido).
            // Comparar games1 com games2 aqui seria a segunda cópia da régua — o mesmo erro do
            // `limiteGames: 9` que já viveu cravado neste arquivo.
            var lados = card.querySelectorAll(".pdz-live-placar");
            if (lados.length === 2 && typeof linha.vencedor === "number") {
                lados[0].classList.toggle("pdz-live-placar-venceu", linha.vencedor === 1);
                lados[1].classList.toggle("pdz-live-placar-venceu", linha.vencedor === 2);
            }

            // ⚠️ OS CAMPOS SÃO ACHADOS POR `name`, E NÃO POR ÍNDICE (12/09/2026). Aqui era
            // `campos[0]`/`campos[1]` sobre TODOS os `.pdz-live-input` do card — e o card
            // acabou de ganhar dois campos novos, os pontos do tie-break. Índice é um
            // acoplamento com a ORDEM do HTML: no dia em que o bloco do tie-break subisse pra
            // cima do placar, o game do jogo passaria a ser escrito no campo de pontos, calado.
            // O `name` é o mesmo que o POST já usa.
            function campoDo(nome) {
                return card.querySelector('.pdz-live-input[name="' + nome + '"]');
            }

            aplicarNumero(campoDo("games1"), linha.games1, linha.teto1);
            aplicarNumero(campoDo("games2"), linha.games2, linha.teto2);

            // O TIE-BREAK (12/09/2026). Os pontos e a EXISTÊNCIA do bloco vêm prontos do
            // servidor: "este jogo está em tie-break?" é pergunta de régua
            // (Services/TieBreakDoJogo), e a resposta vira no instante em que o 9º game é
            // escrito — reescrever a conta aqui seria a segunda cópia, o erro do `limiteGames: 9`.
            if (linha.tieBreak) {
                aplicarNumero(campoDo("pontos1"), linha.tieBreak.pontos1, null);
                aplicarNumero(campoDo("pontos2"), linha.tieBreak.pontos2, null);

                aplicarTieBreak(card, linha.tieBreak);
            }
        });
    }

    // Um número que o servidor mandou, aplicado a um campo da tela.
    //
    // ⚠️ O TETO É ATUALIZADO MESMO COM O CAMPO EM FOCO, ao contrário do valor: o valor não se
    // mexe embaixo do dedo de quem está digitando, mas o limite não é digitação — é regra, e
    // ela muda com o placar (num jogo até 4, o 3x3 estende pra 5). Deixar o `max` velho
    // travaria o "+" no game do desempate.
    function aplicarNumero(campo, valor, teto) {
        if (!campo || typeof valor !== "number") return;

        if (typeof teto === "number") campo.setAttribute("max", teto);

        if (campo === document.activeElement) return;
        if (String(valor) !== campo.value) campo.value = valor;
    }

    // O BLOCO DO TIE-BREAK OBEDECENDO AO SERVIDOR (12/09/2026).
    //
    // Ele existe no HTML de todo jogo cuja fase comporta tie-break, escondido — existir é uma
    // coisa, APARECER é outra. O motivo de não esperar a atualização automática: ela não roda com
    // o cursor dentro de um campo (`estaOcupado` em jogos-ao-vivo-atualiza.js), e quem marca o 8º
    // game está com o dedo no campo do placar. Acendendo o bloco na resposta do próprio POST, o
    // tie-break aparece no toque que o criou.
    //
    // ⚠️ Nenhuma decisão é tomada aqui: "está em tie-break?", "já dá pra fechar?", "com que
    // placar?" e até o TEXTO da etiqueta vêm prontos de Services/TieBreakDoJogo. Reescrever essa
    // régua em JavaScript seria a segunda cópia — o erro do `limiteGames: 9` cravado.
    function aplicarTieBreak(card, tb) {
        var bloco = card.querySelector("[data-tiebreak]");
        if (bloco) bloco.hidden = !tb.emAndamento;

        // A linha "tie-break 7-5" é o DEPOIS: entra quando o bloco sai, e só em jogo que teve
        // contagem.
        var feito = card.querySelector("[data-tiebreak-feito]");
        if (feito) {
            feito.hidden = tb.emAndamento || !tb.houve;
            var etiqueta = feito.querySelector(".pdz-live-tiebreak-feito-texto");
            if (etiqueta && tb.etiqueta) etiqueta.textContent = tb.etiqueta;
        }

        // O botão de fechar só existe pra quem marca placar.
        var fechar = card.querySelector("[data-fechar-tiebreak]");
        if (!fechar) return;

        var temFechamento = typeof tb.fecha1 === "number" && typeof tb.fecha2 === "number";
        fechar.hidden = !tb.emAndamento || !temFechamento;

        if (temFechamento) {
            fechar.setAttribute("data-games1", tb.fecha1);
            fechar.setAttribute("data-games2", tb.fecha2);
            var rotulo = fechar.querySelector(".pdz-live-tiebreak-fechar-texto");
            if (rotulo) rotulo.textContent = "Fechar o tie-break em " + tb.fecha1 + " x " + tb.fecha2;
        }
    }

    function marcarTodos(lote, estado, texto) {
        lote.forEach(function (card) { avisar(card, estado, texto); });
    }

    // O CORPO DO POST: os campos do próprio formulário (o token do antiforgery, o id do
    // torneio, o recorte da tela) mais os campos DOS CARDS DO LOTE — e só deles.
    var CAMPOS_DO_CARD = ["partidaId", "games1", "games2", "pontos1", "pontos2"];

    function corpoDoLote(form, lote) {
        var dados = new FormData();

        // `form.querySelectorAll` pega só o que está DENTRO do formulário — os campos dos cards
        // estão fora dele, ligados pelo atributo `form`, e é exatamente o que não pode entrar
        // sozinho.
        Array.prototype.forEach.call(form.querySelectorAll("input"), function (campo) {
            if (campo.name) dados.append(campo.name, campo.value);
        });

        // ⚠️ UM CAMPO DE CADA NOME POR CARD, na mesma ordem em todos: o servidor casa
        // `partidaId[]` com `games1[]` e `pontos1[]` por ÍNDICE, e uma entrada a mais em
        // qualquer um dos arrays grava o placar de um jogo no outro.
        //
        // O `.pdz-live-input` vem primeiro de propósito: é o campo que a pessoa toca e o mesmo
        // que a resposta do servidor reescreve (ver aplicarPlacarDoServidor). O segundo
        // seletor é pro `partidaId`, que é escondido e não tem a classe.
        lote.forEach(function (card) {
            CAMPOS_DO_CARD.forEach(function (nome) {
                var campo = card.querySelector('.pdz-live-input[name="' + nome + '"]')
                    || card.querySelector('[name="' + nome + '"]');
                if (campo) dados.append(nome, campo.value);
            });
        });

        return dados;
    }

    function enviarAgora() {
        var form = formulario();
        if (!form) return;

        // Um POST por vez. Toque que chega no meio do envio não é perdido: ele entra em
        // `mexidos`, marca `pendente` e o envio seguinte sai com o valor mais novo da tela.
        if (enviando) { pendente = true; return; }

        // O que vai NESTE POST sai da fila agora: um toque que chegue enquanto ele viaja
        // reentra por cima, e é a fila que decide se a marca do card pode sair no fim.
        var lote = mexidos.slice();
        if (lote.length === 0) return;
        mexidos = [];

        enviando = true;
        // ⚠️ Trava a atualização automática enquanto o placar está indo: ela troca o
        // cabeçalho do card pelo HTML do servidor, e o servidor ainda não sabe deste game —
        // o número recém-marcado voltaria pro valor velho na frente da pessoa.
        window.pdzSalvandoPlacar = true;
        marcarTodos(lote, "indo", "salvando…");

        window.fetch(form.action, {
            method: "POST",
            body: corpoDoLote(form, lote),
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
                marcarTodos(lote, "ok", "salvo");
                lote.forEach(confirmar);
                // O "salvo" some sozinho; o card volta a ser só o card.
                window.setTimeout(function () { marcarTodos(lote, "", ""); }, 2500);
            })
            .catch(function () {
                // ⚠️ Falha PRECISA aparecer. O placar continua na tela (não se apaga o que a
                // pessoa marcou), mas ela tem que saber que o servidor não recebeu — senão é
                // o "editei e não salvou" de novo, agora silencioso porque não há recarga
                // pra denunciar.
                marcarTodos(lote, "erro", "não salvou — toque de novo");
                // NADA é descartado: o card volta pra fila, e o toque seguinte (ou o "Salvar
                // placares") tenta de novo com o que está na tela.
                lote.forEach(marcarMexido);
            })
            .then(function () {
                enviando = false;
                window.pdzSalvandoPlacar = false;
                // ⚠️ Quem manda reenviar é o TOQUE que chegou no meio do caminho, e nunca a
                // fila em si: o card que voltou pra fila por falha de rede também está nela, e
                // reenviar por isso seria um laço apertado contra o servidor justamente na hora
                // em que a internet do clube está ruim. O que tira o card de lá é o toque
                // seguinte (ou o "Salvar placares"), com o aviso "não salvou" na tela até lá.
                if (pendente) { pendente = false; enviarAgora(); }
            });
    }

    function agendarEnvio(campo) {
        marcarMexido(campo && campo.closest ? campo.closest(".pdz-live-card") : null);

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

        // ⚠️ O TETO VEM DO `max` DO CAMPO, e o `max` vem do SERVIDOR — nunca de uma conta feita
        // aqui (21/08/2026). Antes era `99` cravado, e num torneio até 9 dava pra ficar
        // apertando "+" até 99: o servidor cortava no salvar, mas a tela passava segundos
        // exibindo um placar que não existe — e é por ela que o organizador decide se o jogo
        // acabou.
        //
        // A régua NÃO é reescrita aqui, de propósito: ela tem soma × "até", o desempate do
        // "vencer por dois" e teto por lado (Services/FormatoDaPartida). Copiar isso pro
        // JavaScript é exatamente como o `limiteGames: 9` cravado sobreviveu tanto tempo.
        // O `max` é reescrito a cada resposta (ver aplicarPlacarDoServidor), porque o teto MUDA
        // com o placar: num jogo até 4, o 3x3 estende o limite pra 5.
        var teto = parseInt(campo.getAttribute("max"), 10);
        if (isNaN(teto)) teto = 99;

        if (novo < 0) novo = 0;
        if (novo > teto) novo = teto;
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

    // FECHAR O TIE-BREAK (12/09/2026): escreve o último game (9 x 8) nos campos de games do
    // próprio card e manda o lote — games novos e pontos do tie-break no MESMO POST, porque
    // estão no mesmo formulário.
    //
    // ⚠️ O placar do fechamento vem do SERVIDOR (`data-games1`/`data-games2`): qual game o
    // tie-break escreve é pergunta de régua (Services/TieBreakDoJogo.GamesAoFechar), que sabe
    // do limite da fase — num jogo até 5 o fechamento é 5x4, e não 9x8.
    //
    // ⚠️ E NÃO FINALIZA NADA. Encerrar continua sendo o botão Finalizar, com a confirmação
    // dele: o que este botão faz é marcar o game que a quadra acabou de jogar.
    document.addEventListener("click", function (e) {
        var botao = e.target.closest ? e.target.closest("[data-fechar-tiebreak]") : null;
        if (!botao || botao.disabled) return;

        e.preventDefault();

        var card = botao.closest(".pdz-live-card");
        if (!card) return;

        ["games1", "games2"].forEach(function (nome, i) {
            var campo = card.querySelector('.pdz-live-input[name="' + nome + '"]');
            var valor = botao.getAttribute(i === 0 ? "data-games1" : "data-games2");
            // `if (valor)` e não `!== null`: atributo ausente no HTML vem como string VAZIA, e
            // `campo.value = ""` APAGARIA o placar do jogo. O fechamento nunca é zero (o lado
            // perdedor fica com `Games - 1`, que num tie-break é pelo menos 2).
            if (campo && valor) campo.value = valor;
        });

        // ⚠️ O botão NÃO é desabilitado aqui, e isso é escolha: o fechamento escreve um placar
        // ABSOLUTO (9 x 8), então o segundo toque dá exatamente no mesmo lugar — a mesma razão
        // pela qual a fila da Mesa guarda placar inteiro e não "+1". Desabilitar deixaria o botão
        // MORTO quando o POST falha, que é justamente quando a pessoa precisa tocar de novo.
        marcarMexido(card);
        window.clearTimeout(agendado);
        enviarAgora();
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
        window.clearTimeout(agendado);

        // ⚠️ O BOTÃO SALVA O QUE FOI EDITADO, e não a tela inteira (12/09/2026). Ele era o
        // caminho mais direto pro defeito que este arquivo acabou de tirar do −/+: mandar
        // "por garantia" o placar das quadras que ninguém tocou é reescrever o trabalho do
        // outro marcador com um número velho. Sem card mexido não há o que salvar — e dizer
        // isso é mais honesto que um "salvo" que não gravou nada.
        if (mexidos.length === 0) {
            var todos = Array.prototype.slice.call(document.querySelectorAll(".pdz-live-card"));
            marcarTodos(todos, "ok", "nada mudou");
            window.setTimeout(function () { marcarTodos(todos, "", ""); }, 2500);
            return;
        }

        enviarAgora();
    });
})();
