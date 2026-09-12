// A lista de jogos se atualiza sozinha, pra três pessoas operarem o mesmo torneio.
//
// No Interno de 05/08/2026 eram três aparelhos (celular, iPad, notebook) mexendo na mesma
// Mesa. Quem finalizava num deles não aparecia nos outros: as outras duas telas seguiam
// mostrando o jogo em quadra, e mais de uma vez a mesma partida foi colocada no ar duas
// vezes porque o segundo operador não via o que o primeiro tinha feito.
//
// ⚠️ RECARREGAR A PÁGINA INTEIRA NÃO SERVE (Felipe, 08/08/2026: "o youtube está parando
// sozinho aqui do nada"). No Americano das Gurias as duas quadras estavam com transmissão
// embutida no cartão, e todo recarregamento REINICIA o <iframe>: de 20 em 20 segundos o vídeo
// voltava pro estado parado. De quebra, a página renasce na aba AO VIVO sempre que existe jogo
// em quadra, então quem estava em Agendadas era jogado pra outra tela a cada tique.
//
// Então a página não recarrega mais: ela BUSCA a versão nova do servidor e troca só os
// pedaços — o cabeçalho de cada cartão (placar, cronômetro, botões) e as abas sem vídeo. O
// <iframe> fica onde está, intocado. Mover ou reescrever um iframe é o mesmo que recarregá-lo,
// então a regra é simples: nada que contenha vídeo é substituído.
//
// A verdade continua sendo a do servidor — o HTML vem dele, inteiro, como sempre. Não há uma
// segunda cópia da regra em JavaScript que possa divergir do banco.
//
// ⚠️ Cuidados que fazem a diferença entre ajudar e atrapalhar:
//   • NÃO atualiza enquanto alguém está digitando ou com um modal aberto. Trocar HTML por
//     baixo de quem está marcando placar apaga o que a pessoa acabou de digitar — seria pior
//     que o problema original.
//   • NÃO atualiza com a aba em segundo plano: o organizador deixa a Mesa aberta no notebook
//     e mexe no celular; gastar bateria e 3G do clube ali não ajuda ninguém.
//   • Só onde há jogo AO VIVO. Torneio parado não precisa de nada disso.
//   • Rede que falhou é silêncio, não erro: tenta de novo no tique seguinte.
(function () {
    "use strict";

    var SEGUNDOS = 20;

    // Os pedaços SEM vídeo, trocados por inteiro. Os modais entram porque a lista de jogos
    // candidatos deles envelhece junto (o "trocar com qual jogo?" não pode oferecer uma
    // partida que já entrou em quadra). A barra de salvar placares entra porque ela CONTA os
    // jogos em quadra ("salve os 5 jogo(s) de uma vez") — e agora esse número muda sem
    // recarregamento nenhum pra corrigi-lo.
    var BLOCOS = ["#agendadas", "#finalizadas", "#classificacao", "#modalTrocarHorario", "#modalTrocarQuadra",
                  "#modalDefinirHorario", ".pdz-live-salvar-barra"];

    function estaOcupado() {
        // ⚠️ Placar indo pro servidor = não atualizar. O cabeçalho do card seria trocado pelo
        // HTML do servidor, que ainda não sabe do game recém-marcado — e o número voltaria
        // pro valor velho na frente de quem acabou de marcar. Quem levanta esta bandeira é o
        // js/placar-ao-vivo.js.
        if (window.pdzSalvandoPlacar) return true;

        // Mesma razão, outro dado: a troca de saque indo pro servidor (js/saque-ao-vivo.js).
        // Trocar o cabeçalho aqui devolveria a bolinha pro lado velho na frente de quem
        // acabou de mover ela.
        if (window.pdzTrocandoSaque) return true;

        var ativo = document.activeElement;
        if (ativo && /^(INPUT|TEXTAREA|SELECT)$/.test(ativo.tagName)) return true;

        // Modal aberto (confirmação, trocar horário, mudar quadra) = decisão em curso.
        if (document.querySelector(".modal.show")) return true;

        // Texto selecionado costuma ser alguém lendo/copiando um nome.
        var selecao = window.getSelection && window.getSelection();
        if (selecao && String(selecao).length > 0) return true;

        return false;
    }

    function cartoes(raiz) {
        return Array.prototype.slice.call(raiz.querySelectorAll(".pdz-live-card"));
    }

    // Quais jogos estão em quadra, na ordem. Se isto mudou, a estrutura da tela mudou: jogo
    // novo no ar, jogo finalizado.
    function assinatura(raiz) {
        return cartoes(raiz).map(function (c) { return c.getAttribute("data-partida-id"); }).join(",");
    }

    function temJogoAoVivo() {
        return document.querySelector(".pdz-live-card") !== null;
    }

    // A PESSOA ESTÁ MESMO OLHANDO OS CARTÕES AO VIVO? (12/09/2026)
    //
    // 🗣️ Felipe, três vezes no mesmo dia: *"as vezes to olhando as finalizadas e ele
    // automaticamente volta para tela do ao vivo"* · *"ao mudar algum filtro, as vezes sai da
    // tela que esta"* · *"estava mexendo na aba palpiteiros e sozinho foi para o aovivo, isso
    // nao pode acontecer, ele tem q se manter na tela q esta, a menos q o usuario clique em
    // algo"*.
    //
    // O recarregamento abaixo existe pra quem está lendo os cartões em quadra: o que ele lê
    // acabou de mudar. Pra quem está em Finalizadas, em Palpiteiros ou mexendo num filtro, é a
    // tela sumindo sozinha — e num sábado a lista de jogos em quadra muda o tempo todo.
    //
    // ⚠️ AS DUAS BARRAS PRECISAM ESTAR ABERTAS: a sub-aba `#aovivo` continua marcada como ativa
    // mesmo com a aba MÃE (Jogos) fechada, então perguntar só por ela devolveria "sim" pra quem
    // está em Palpiteiros — que é justamente o caso que ele relatou.
    function olhandoOAoVivo() {
        var paneJogos = document.querySelector("#jogosDoTorneio");
        // Em /Torneios/Jogos não existe aba mãe: a lista É a página.
        if (paneJogos && !document.querySelector("#jogosDoTorneio.active")) return false;
        return document.querySelector("#aovivo.active") !== null;
    }

    function trocar(atual, fresco) {
        if (atual && fresco && atual.innerHTML !== fresco.innerHTML) atual.innerHTML = fresco.innerHTML;
    }

    // ── JOGO QUE ENTRA OU SAI DE QUADRA APARECE E SOME SEM RECARREGAR (12/09/2026) ─────────
    //
    // 🗣️ Felipe: *"nao é possivel fazer com que a pagina nao precise recarregar inteira, apenas
    // os placares? e quando entrar ou sair um jogo do aovivo, ele apenas adicionar na tela sem
    // precisar carregar?"*
    //
    // Dá — e o que segurava era exatamente o <iframe> da transmissão: MOVER um iframe no DOM é o
    // mesmo que recarregá-lo, então por um mês a resposta pra "a lista mudou" foi recarregar a
    // página inteira. Mas INSERIR um cartão novo e REMOVER um que saiu não move ninguém: quem
    // continua em quadra não é tocado, e nem quem entra nem quem sai tem vídeo a preservar (o que
    // entra nasce agora; o que sai levou o dele junto).
    //
    // A ordem é a do servidor: cada cartão que falta entra ANTES do próximo cartão que já está na
    // tela, e no fim da fila quando não há próximo. Nenhum sobrevivente muda de lugar.
    //
    // ⚠️ O CONTRATO COM O RAZOR: `#pdzAoVivoCartoes` é a grade, e cada cartão é embrulhado por UMA
    // coluna que é filha direta dela — é a coluna que entra e sai. Ver _JogosDoTorneio.cshtml.
    // Sem a grade na página (tela antiga em cache, outro layout), devolve `false` e quem chamou
    // decide: aqui, recarregar do jeito de antes.
    function colunaDo(cartao) {
        return cartao ? cartao.parentNode : null;
    }

    function cartaoDe(raiz, id) {
        return raiz.querySelector('.pdz-live-card[data-partida-id="' + id + '"]');
    }

    function remendarAoVivo(novo) {
        var grade = document.querySelector("#pdzAoVivoCartoes");
        var gradeNova = novo.querySelector("#pdzAoVivoCartoes");
        if (!grade || !gradeNova) return false;

        var frescos = cartoes(gradeNova);

        // O ÚLTIMO jogo saiu de quadra: não sobra vídeo pra proteger, e o painel inteiro carrega
        // coisas que também mudam (o "Nenhum jogo rolando no momento", a barra de salvar
        // placares). Trocar o painel de uma vez é mais simples e não custa nada aqui.
        if (frescos.length === 0) {
            trocar(document.querySelector("#aovivo"), novo.querySelector("#aovivo"));
            return true;
        }

        // 1. Quem saiu de quadra sai da tela, com a coluna dele.
        cartoes(grade).forEach(function (atual) {
            if (cartaoDe(gradeNova, atual.getAttribute("data-partida-id"))) return;
            var coluna = colunaDo(atual);
            if (coluna && coluna.parentNode) coluna.parentNode.removeChild(coluna);
        });

        // 2. Quem entrou aparece, no lugar certo.
        for (var i = 0; i < frescos.length; i++) {
            var id = frescos[i].getAttribute("data-partida-id");
            if (cartaoDe(grade, id)) continue;

            var referencia = null;
            for (var j = i + 1; j < frescos.length && !referencia; j++) {
                referencia = colunaDo(cartaoDe(grade, frescos[j].getAttribute("data-partida-id")));
            }

            var colunaNova = colunaDo(frescos[i]);
            if (!colunaNova) return false;

            // `importNode`: o cartão vem do documento do DOMParser, e nó de outro documento não
            // se insere direto sem adoção.
            grade.insertBefore(document.importNode(colunaNova, true), referencia);
        }

        return true;
    }

    // O recarregamento que sobrou (a grade não existe na página) devolvia a pessoa pro TOPO de
    // uma lista de dezenas de jogos. 🗣️ Felipe: *"quando atualizar, mantem na altura q tava a
    // pagina no scroll"*. A memória é a mesma dos formulários, emprestada pelo
    // js/manter-posicao-na-lista.js — que carrega DEPOIS deste arquivo na página, mas muito antes
    // do primeiro tique, 20 segundos adiante.
    function recarregarMantendoARolagem() {
        if (typeof window.pdzGuardarPosicaoNaLista === "function") window.pdzGuardarPosicaoNaLista();
        window.location.reload();
    }

    function aplicar(novo) {
        // 1. O cabeçalho de cada cartão AO VIVO: placar, cronômetro, quadra e botões. A
        //    transmissão (.pdz-live-video) é irmã dele e não é tocada.
        cartoes(document).forEach(function (atual) {
            // ⚠️ Card com placar que NÃO chegou ao servidor fica intocado. Trocar o cabeçalho
            // aqui apagaria o game que a pessoa marcou (o servidor ainda tem o número velho) e
            // levaria junto o aviso "não salvou" — o erro sumiria da tela sem ter sido
            // resolvido. Quem marca esse estado é o js/placar-ao-vivo.js.
            if (atual.querySelector(".pdz-live-salvo-erro")) return;

            // ⚠️ E card com TOQUE AINDA NÃO ENTREGUE também (12/09/2026). Entre o dedo e o
            // POST há o meio segundo do debounce que junta a rajada de toques, e neste vão
            // nada aqui estava travado: trocar o cabeçalho devolvia o número velho — que é
            // exatamente o que o POST lê meio segundo depois. O game marcado sumia inteiro,
            // sem erro em lugar nenhum. Quem levanta a bandeira é o js/placar-ao-vivo.js.
            if (atual.hasAttribute("data-pdz-mexido")) return;

            var id = atual.getAttribute("data-partida-id");
            var fresco = novo.querySelector('.pdz-live-card[data-partida-id="' + id + '"]');
            if (!fresco) return;
            trocar(atual.querySelector(".pdz-live-header"), fresco.querySelector(".pdz-live-header"));
        });

        // 2. As abas sem vídeo, inteiras.
        BLOCOS.forEach(function (seletor) {
            trocar(document.querySelector(seletor), novo.querySelector(seletor));
        });

        // 3. Os números das pílulas ("Agendadas (8)"). Só o CONTEÚDO de cada botão: a classe
        //    `active` mora no próprio botão, e trocar o botão trocaria a aba debaixo de quem
        //    está lendo — exatamente o que este arquivo existe pra não fazer.
        Array.prototype.forEach.call(novo.querySelectorAll("#jogosTabs .nav-link"), function (fresco) {
            var alvo = fresco.getAttribute("data-bs-target");
            if (!alvo) return;
            trocar(document.querySelector('#jogosTabs .nav-link[data-bs-target="' + alvo + '"]'), fresco);
        });
    }

    var buscando = false;

    function tique() {
        if (buscando || document.hidden || estaOcupado() || !temJogoAoVivo()) return;

        buscando = true;
        window.fetch(window.location.href, { credentials: "same-origin" })
            .then(function (resposta) {
                return resposta.ok ? resposta.text() : Promise.reject(resposta.status);
            })
            .then(function (html) {
                var novo = new DOMParser().parseFromString(html, "text/html");

                // Sem a lista na resposta, o que voltou não é esta tela (sessão caiu, portão de
                // acesso, erro): não dá pra remendar meia página com HTML de outra.
                if (!novo.getElementById("jogosTabsContent")) return;

                // A pessoa começou a mexer enquanto a resposta vinha.
                if (estaOcupado()) return;

                if (assinatura(novo) !== assinatura(document) && !remendarAoVivo(novo)) {
                    // O remendo não deu: a grade `#pdzAoVivoCartoes` não está na página. Aí volta
                    // o comportamento de antes — e ele SÓ VALE PRA QUEM ESTÁ OLHANDO OS CARTÕES.
                    // Pra quem está em outra aba, o tique passa em silêncio: os blocos sem vídeo
                    // (Agendadas, Finalizadas, modais) seguem sendo atualizados abaixo, que é o
                    // que a tela dele mostra.
                    if (olhandoOAoVivo()) {
                        recarregarMantendoARolagem();
                        return;
                    }
                }

                aplicar(novo);
            })
            .catch(function () { /* rede do clube caiu: o próximo tique tenta de novo */ })
            .then(function () { buscando = false; });
    }

    if (temJogoAoVivo() && window.fetch) window.setInterval(tique, SEGUNDOS * 1000);
})();
