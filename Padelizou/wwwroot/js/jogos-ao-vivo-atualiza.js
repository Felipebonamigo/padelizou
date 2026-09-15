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
//   • Só enquanto ainda há o que acontecer: jogo em quadra OU jogo agendado que pode entrar.
//     Torneio com tudo finalizado não precisa de nada disso, e para sozinho.
//   • ABRIR O APP mostra o agora: a volta pro primeiro plano busca na hora, sem esperar o ciclo.
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

        // E a ação inteira do cartão — Finalizar, "voltar pra agendado" — indo por fetch
        // (js/acao-do-cartao-ao-vivo.js). Aqui não é o cabeçalho, é a LISTA: uma busca pedida
        // ANTES do POST e chegando DEPOIS dele devolveria o jogo finalizado pra quadra, na
        // frente de quem acabou de encerrá-lo.
        if (window.pdzAcaoEmCurso) return true;

        // E a presença indo pro servidor (js/checkin-sem-recarregar.js, 15/09/2026). Quem marca
        // os quatro jogadores de um jogo dispara quatro POSTs em dois segundos; uma lista
        // trocada no meio da rajada volta sem os cliques que ainda estão no ar, e a bolinha
        // recém-pintada PISCA de volta pra cinza na frente de quem acabou de tocar nela.
        if (window.pdzMarcandoCheckIn) return true;

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

    // AINDA HÁ O QUE ACONTECER NESTE TORNEIO? (12/09/2026)
    //
    // 🗣️ Felipe: *"Pessoal que tem o app no celular, disse q ao abrir ele fica desatualizado as
    // vezes no aovivo"*.
    //
    // 🕳️ Até aqui o relógio só ligava com jogo JÁ em quadra na hora em que a página carregou —
    // e esta era a ÚNICA linha do arquivo que agendava alguma coisa. Quem abre o app de manhã,
    // antes da primeira partida, nunca ligava o relógio: medido no navegador, a tela ficou em
    // "Ao Vivo (0)" com um jogo em quadra no banco, e ficaria assim para sempre.
    //
    // ⚠️ MAS TAMBÉM NÃO É "SEMPRE": torneio com tudo finalizado não pode buscar a página de 20
    // em 20 segundos até a pessoa fechar a aba. A régua é "ainda há o que acontecer" — jogo em
    // quadra, ou jogo agendado que pode entrar. Quando o último acaba, os dois param sozinhos.
    function torneioEmAndamento() {
        return temJogoAoVivo() || document.querySelector("#agendadas .pdz-jl") !== null;
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

    // ── O PLAYER DA QUADRA NÃO MORRE QUANDO O JOGO DELA TROCA (14/09/2026) ───────────────
    //
    // 🗣️ Um espectador, pelo Felipe: *"as vezes o video do youtube trava no site"*.
    //
    // 🕳️ O <iframe> é do JOGO, mas a câmera é da QUADRA — o Services/TransmissaoDaQuadra.cs diz
    // com todas as letras: *"a câmera fica pendurada na quadra e transmite o dia inteiro — o
    // link é uma propriedade do LUGAR"*. Aí, quando o jogo acabava e o próximo entrava na mesma
    // quadra, o passo "quem saiu sai" levava a coluna COM o player dentro e o passo "quem entrou
    // aparece" trazia um player NOVO da MESMA transmissão.
    //
    // E o embed não tem `autoplay`: o player novo nasce PARADO, na miniatura da live — que numa
    // câmera de quadra é um quadro da própria quadra. Na tela isso não parece um cartão trocado,
    // parece o vídeo travado, com o play vermelho por cima. Uma vez por jogo daquela quadra.
    //
    // Então o cartão que sai é REAPROVEITADO quando o que entra traz a mesma transmissão: trocam-
    // se os filhos que não são o vídeo, e o <iframe> não é removido nem movido — a única forma de
    // ele não recarregar.
    //
    // ⚠️ SÓ PEGA A TROCA QUE ACONTECE NO MESMO TIQUE. Se a quadra ficar um tempo sem ninguém em
    // quadra, o cartão sai (é a verdade: não há jogo ali) e o próximo nasce com player novo. Quem
    // resolveria isso seria a transmissão morar num painel POR QUADRA, fora dos cartões.
    //
    // ⚠️ O PREÇO É A ORDEM: o cartão reaproveitado fica ONDE O ANTIGO ESTAVA, e não onde o
    // servidor o pôs — mover a coluna pra posição certa recarregaria o iframe, que é justamente
    // o que se está comprando. A ordem volta sozinha no próximo carregamento da página.
    function transmissaoDe(cartao) {
        var quadro = cartao.querySelector(".pdz-live-video iframe");
        return quadro ? quadro.getAttribute("src") : null;
    }

    function reaproveitar(atual, fresco) {
        var video = atual.querySelector(".pdz-live-video");

        // Some com tudo, menos o vídeo. Ele é o único que não pode sair e voltar.
        Array.prototype.slice.call(atual.children).forEach(function (filho) {
            if (filho !== video) atual.removeChild(filho);
        });

        // E repõe os filhos do cartão fresco EM VOLTA dele: o que vem antes do vídeo entra antes
        // dele; do vídeo em diante, anexado — e anexar não move quem já está.
        var referencia = video;
        Array.prototype.slice.call(fresco.children).forEach(function (filho) {
            if (filho.classList.contains("pdz-live-video")) { referencia = null; return; }
            atual.insertBefore(document.importNode(filho, true), referencia);
        });

        // Agora o cartão é o do jogo novo, pra todo mundo: pro remendo dos passos seguintes, pro
        // cabeçalho que o `aplicar` troca de 20 em 20s e pra hash `#jogo-N` que vem da aba Grupos.
        atual.setAttribute("data-partida-id", fresco.getAttribute("data-partida-id"));
        var idNovo = fresco.getAttribute("id");
        if (idNovo) atual.setAttribute("id", idNovo);
    }

    function remendarAoVivo(novo) {
        var grade = document.querySelector("#pdzAoVivoCartoes");
        var gradeNova = novo.querySelector("#pdzAoVivoCartoes");
        if (!grade || !gradeNova) return false;

        var frescos = cartoes(gradeNova);
        var atuais = cartoes(grade);

        // NENHUM CARTÃO DE UM LADO OU DO OUTRO: o painel inteiro carrega coisas que mudam junto
        // com o primeiro e com o último jogo — o "Nenhum jogo rolando no momento" e a barra de
        // salvar placares —, e não há vídeo a proteger: de um lado porque cartão nenhum existe,
        // do outro porque quem sai leva o dele junto. Trocar o painel de uma vez resolve os dois.
        //
        // ⚠️ O LADO VAZIO NA TELA é o caso de quem abriu o app antes do primeiro jogo (12/09):
        // sem ele, o primeiro cartão entraria na grade e o "Nenhum jogo rolando no momento"
        // ficaria em cima dele, dizendo o contrário do que a tela mostra.
        if (frescos.length === 0 || atuais.length === 0) {
            trocar(document.querySelector("#aovivo"), novo.querySelector("#aovivo"));
            return true;
        }

        // 1. A MESMA CÂMERA, OUTRO JOGO: o cartão que ia sair VIRA o que ia entrar, no lugar.
        //    Vem antes dos dois passos de baixo de propósito — depois de remover a coluna não há
        //    mais player a salvar.
        var saindo = atuais.filter(function (c) {
            return !cartaoDe(gradeNova, c.getAttribute("data-partida-id"));
        });
        frescos.forEach(function (fresco) {
            if (cartaoDe(grade, fresco.getAttribute("data-partida-id"))) return;

            var transmissao = transmissaoDe(fresco);
            if (!transmissao) return;

            for (var k = 0; k < saindo.length; k++) {
                if (transmissaoDe(saindo[k]) !== transmissao) continue;
                reaproveitar(saindo[k], fresco);
                saindo.splice(k, 1);   // um cartão só é reaproveitado uma vez
                return;
            }
        });

        // 2. Quem saiu de quadra sai da tela, com a coluna dele.
        atuais.forEach(function (atual) {
            if (cartaoDe(gradeNova, atual.getAttribute("data-partida-id"))) return;
            var coluna = colunaDo(atual);
            if (coluna && coluna.parentNode) coluna.parentNode.removeChild(coluna);
        });

        // 3. Quem entrou aparece, no lugar certo.
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

    // ── O FINALIZAR ENTREGA A PÁGINA AQUI, EM VEZ DE RECARREGAR (14/09/2026) ─────────────
    //
    // 🗣️ Felipe: *"apenas queria q o video nao travasse, nao mude o layout"*.
    //
    // "Finalizar" e "Voltar pra agendado" eram POST comum — recarga da página inteira, e recarga
    // reinicia TODO <iframe> da tela: encerrar o jogo da Quadra 1 parava o vídeo de quem estava
    // assistindo à Quadra 2, sem nenhuma relação entre as duas coisas. O
    // js/acao-do-cartao-ao-vivo.js manda o POST por fetch e entrega AQUI o HTML que voltou — que
    // é a MESMA página que o tique buscaria. Então são as mesmas regras de remendo, sem uma
    // segunda busca e, o que importa mais, sem uma segunda cópia delas pra divergir um dia.
    //
    // ⚠️ DEVOLVE `false` QUANDO A RESPOSTA NÃO É ESTA TELA. Sessão vencida responde 302 pro
    // login, o fetch SEGUE o desvio e entrega 200 com o HTML do login: `resposta.ok` diz que sim
    // e nada foi finalizado. Quem chamou recarrega — e a pessoa cai no login, em vez de ficar
    // com a lista de jogos remendada com pedaços de outra página.
    //
    // ⚠️ E COPIA O AVISO DO SERVIDOR, que o tique NÃO copia. O FinalizarPartida pode RECUSAR e
    // mesmo assim redirecionar, pondo o porquê em TempData["Erro"] — que é de UMA LEITURA SÓ, e
    // esta resposta acabou de consumi-lo. Sem esta linha o organizador aperta Finalizar, nada
    // acontece e nada explica. No tique seria o contrário: ele buscaria um aviso vazio de 20 em
    // 20 segundos e apagaria sozinho a mensagem que a pessoa ainda está lendo.
    window.pdzAplicarRespostaDeAcao = function (html) {
        var novo = new DOMParser().parseFromString(html, "text/html");
        if (!novo.getElementById("jogosTabsContent")) return false;

        if (assinatura(novo) !== assinatura(document) && !remendarAoVivo(novo)) return false;

        aplicar(novo);
        trocar(document.querySelector("#pdzAvisoDaAcao"), novo.querySelector("#pdzAvisoDaAcao"));
        return true;
    };

    var buscando = false;
    var ultimaBusca = 0;

    // Devolve `true` quando a busca REALMENTE saiu, e `false` quando a tela estava ocupada ou
    // já havia outra em curso. O relógio de 20s ignora a resposta — quem a usa é quem acabou de
    // gravar alguma coisa e precisa da lista nova agora (ver a porta no fim do arquivo).
    function tique() {
        if (buscando || document.hidden || estaOcupado() || !torneioEmAndamento()) return false;

        buscando = true;
        ultimaBusca = Date.now();
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

        return true;
    }

    // ABRIR O APP MOSTRA O AGORA, E NÃO O DE 20 SEGUNDOS ATRÁS (12/09/2026).
    //
    // O tique é barrado enquanto `document.hidden`, e isso é certo: o organizador deixa a Mesa
    // aberta no notebook e mexe no celular, e gastar bateria e 3G do clube ali não ajuda
    // ninguém. Só que ao VOLTAR ninguém acordava a tela — ela esperava o próximo ciclo.
    //
    // 🔬 Medido no navegador: app escondido de t=2s a t=50s com ZERO buscas (certo), volta em
    // t=51s, e a única busca só em t=57s — 7 segundos desatualizado depois de abrir, com teto
    // no ciclo inteiro de 20s. No celular é pior: Android e iOS CONGELAM o timer em segundo
    // plano, então o próximo tique pode demorar bem mais do que os 20s de um relógio que
    // continuou andando.
    //
    // ⚠️ COM UM MÍNIMO ENTRE BUSCAS: cada busca é a PÁGINA INTEIRA (mais de 1MB no torneio
    // grande). Alternar de aba no computador dispara `visibilitychange` a cada ida e volta, e
    // sem esta trava viraria uma rajada.
    var MINIMO_ENTRE_BUSCAS = 3000;

    function aoVoltarProPrimeiroPlano() {
        if (document.hidden) return;
        if (Date.now() - ultimaBusca < MINIMO_ENTRE_BUSCAS) return;
        tique();
    }

    // A PORTA PRA QUEM ACABOU DE GRAVAR (15/09/2026). 🗣️ Felipe, sobre o check-in: *"o jogo tem
    // q subir na hora"*. Quem faz o jogo completo subir pro topo do horário é a lista nova do
    // servidor (Services/OrdemNoHorario) — e não uma segunda conta de ordem em JavaScript, que
    // poderia discordar do banco. O js/checkin-sem-recarregar.js chama isto assim que o último
    // POST da rajada é confirmado; a resposta `false` diz "agora não deu", e ele insiste.
    if (window.fetch) window.pdzAtualizarAListaDeJogos = tique;

    if (torneioEmAndamento() && window.fetch) {
        window.setInterval(tique, SEGUNDOS * 1000);
        document.addEventListener("visibilitychange", aoVoltarProPrimeiroPlano);
    }
})();
