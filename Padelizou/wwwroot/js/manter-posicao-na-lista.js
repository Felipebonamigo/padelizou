// A TELA FICA ONDE ESTÁ — a lista não volta pro topo a cada clique (Felipe, 10/09/2026).
//
// 🗣️ Num print da lista do Er rolada até as quartas de domingo: *"quando eu trocar aqui, ele tem
// q permanecer no mesmo local da tela, esta indo para o inicio"*.
//
// Toda ação do organizador é POST → redirect → GET, e a página nova nasce no começo. Numa lista de
// 97 jogos, arrumar a ordem de sete semifinais custava sete rolagens até achar de novo a linha em
// que se estava mexendo. É a MESMA queixa de 08/08 que criou o js/jogos-abas.js ("ele tem que se
// manter na tela que eu estou editando"), e por isso é a mesma peça: sessionStorage.
//
// ⚠️ POR OPT-IN (`data-manter-posicao` no formulário), e não em todo POST da página: uma ação que
// leva pra OUTRA tela, ou que muda a lista inteira (o "Recalcular horários"), não quer voltar pra
// uma posição que já não quer dizer nada. Quem sabe disso é quem escreveu o botão.
//
// sessionStorage, não localStorage: é o estado DESTA sessão de trabalho, não uma preferência — e a
// posição é lida UMA vez e apagada, senão qualquer visita seguinte à página seria arrastada pra um
// lugar escolhido em outro momento.
(function () {
    "use strict";

    // Por página: quem opera dois torneios no mesmo dia não herda a rolagem de um no outro.
    var chave = "pdz-posicao-na-lista:" + window.location.pathname;

    // sessionStorage pode ser PROIBIDO (navegação privada com cookies bloqueados) e aí o próprio
    // acesso estoura. Falhar aqui não pode derrubar nada: sem memória, a página só nasce no topo,
    // que é como ela já nascia.
    function guardar(valor) {
        try { window.sessionStorage.setItem(chave, valor); } catch (e) { /* sem memória */ }
    }

    function pegarEApagar() {
        try {
            var valor = window.sessionStorage.getItem(chave);
            window.sessionStorage.removeItem(chave);
            return valor;
        } catch (e) { return null; }
    }

    // A MESMA MEMÓRIA, EMPRESTADA A QUEM RECARREGA POR CONTA PRÓPRIA (12/09/2026). 🗣️ Felipe:
    // *"quando atualizar, mantem na altura q tava a pagina no scroll"*. O atualizador automático
    // (js/jogos-ao-vivo-atualiza.js) ainda recarrega no caso em que não consegue remendar a tela,
    // e ali não há `submit` nenhum pra ouvir. Fica exposto AQUI, e não copiado lá, porque a chave
    // é uma só: duas cópias da string viram duas memórias diferentes no dia em que uma mudar.
    //
    // ⚠️ DUAS MEMÓRIAS DIFERENTES, E A ESCOLHA É DE QUEM ESCREVE O BOTÃO (12/09/2026):
    //
    //   `data-manter-posicao`              → a ALTURA em pixels. Serve quando a lista continua do
    //                                        mesmo tamanho (marcar um check-in, trocar um horário).
    //   `data-manter-posicao="#algumId"`   → trazer AQUELE ELEMENTO de volta pra tela.
    //
    // A segunda existe porque a primeira foi MEDIDA falhando no filtro: com "Meus jogos" ligado a
    // lista cai de 97 jogos pra 3, o documento encolhe, e o navegador trunca a rolagem no fim da
    // página nova. Medido no celular de 390px: a barra de filtros estava a **27px** do topo da
    // tela antes do clique e voltava a **315px** — ou seja, no começo da página, que é
    // exatamente a queixa. Altura em pixels só quer dizer alguma coisa enquanto a página tem o
    // mesmo tamanho; filtrar é justamente a ação que muda o tamanho dela.
    window.pdzGuardarPosicaoNaLista = function (alvo) {
        guardar(alvo || String(window.scrollY || window.pageYOffset || 0));
    };

    // O valor do atributo, quando tem um, é o seletor a trazer de volta pra tela.
    function guardarPor(elemento) {
        window.pdzGuardarPosicaoNaLista(elemento.getAttribute("data-manter-posicao") || null);
    }

    document.addEventListener("submit", function (evento) {
        var formulario = evento.target;
        if (!formulario || !formulario.hasAttribute || !formulario.hasAttribute("data-manter-posicao")) return;

        guardarPor(formulario);
    }, true);

    // ⚠️ E NO CLIQUE DE UM LINK TAMBÉM (12/09/2026). 🗣️ Felipe: *"quando eu clico em meu jogos, a
    // pagina sobe la para o inicio tambem, tinha q aparece na aba meus jogos ja"*. O "Meus jogos"
    // é um `<a>` de propósito — liga/desliga de um toque —, e link não dispara `submit`: o clique
    // passava batido e a página renascia no começo, com a barra de pagamento na tela e a lista de
    // jogos lá embaixo. A régua de opt-in é a mesma; só o evento muda.
    document.addEventListener("click", function (evento) {
        // Abrir em nova aba (ctrl/cmd, shift, botão do meio) NÃO é sair desta tela. Guardar aqui
        // deixaria uma altura órfã, que ninguém consome, pra atropelar a próxima visita.
        if (evento.defaultPrevented || evento.button !== 0) return;
        if (evento.metaKey || evento.ctrlKey || evento.shiftKey || evento.altKey) return;

        // `closest` e não `evento.target`: no celular o dedo encosta no <i> de dentro do botão.
        var alvo = evento.target;
        var link = alvo && alvo.closest ? alvo.closest("a[data-manter-posicao]") : null;
        if (!link) return;

        guardarPor(link);
    }, true);

    // No `load`, e não no DOMContentLoaded: o navegador ainda pula pra âncora do endereço
    // (#jogosDoTorneio) e o js/jogos-abas.js ainda troca a aba — as duas coisas mexem na altura da
    // página. O rAF põe a rolagem depois de tudo isso ter acontecido.
    window.addEventListener("load", function () {
        var guardada = pegarEApagar();
        if (guardada === null) return;

        window.requestAnimationFrame(function () {
            // ⚠️ O valor foi escrito por nós, mas é LIDO de volta do sessionStorage, que é da
            // origem inteira. `querySelector` com string de fora aceita seletor de qualquer
            // forma; aqui só passa `#id` simples, que é tudo o que este arquivo escreve.
            if (/^#[A-Za-z][\w-]*$/.test(guardada)) {
                var alvo = document.querySelector(guardada);
                if (alvo) alvo.scrollIntoView();
                return;
            }

            var y = parseInt(guardada, 10);
            if (!isNaN(y) && y > 0) window.scrollTo(0, y);
        });
    });
})();
