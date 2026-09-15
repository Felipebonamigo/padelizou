// A ABA ESCOLHIDA FICA (Felipe, 08/08/2026): "quando eu salvo aqui, ele volta pra tela de ao
// vivo — ele tem que se manter na tela que eu estou editando".
//
// Toda ação do organizador é um POST que redireciona pra esta mesma página, e a página nasce
// na aba AO VIVO sempre que existe jogo em quadra (`defaultAoVivo`, em _JogosDoTorneio). Quem
// estava em Agendadas chamando jogo, mudando quadra ou trocando horário voltava, A CADA AÇÃO,
// pra uma aba que não era a dele — e tinha que caçar de novo, numa lista de dezenas, a linha
// em que estava mexendo. O mesmo valia pra atualização automática de 20 em 20 segundos.
//
// sessionStorage, não localStorage: é a escolha DESTA sessão de trabalho, não uma preferência
// permanente. Fechou a aba do navegador, o torneio volta a abrir onde o servidor achar melhor
// — que continua sendo o certo pra quem chega agora.
//
// Vale nas duas telas que mostram a lista: a página do torneio (Details) e /Torneios/Jogos.
//
// ══════════════════════════════════════════════════════════════════════════════════════════
// ⚠️ ISTO NUNCA FUNCIONOU NA PÁGINA DO TORNEIO ATÉ 12/09/2026, e o motivo era a ORDEM DOS
// SCRIPTS. Medido no HTML entregue: este arquivo sai na linha 3941 e o `bootstrap.bundle.js`
// na 4736 — os scripts da lista de jogos são emitidos no CORPO da página, e o Bootstrap só
// chega no fim, pelo _Layout. O `if (!window.bootstrap) return` de antes disparava sempre, em
// silêncio: nenhum ouvinte era registrado, o `sessionStorage` ficava vazio, e a memória de aba
// era uma peça morta desde 08/08. Conferido no navegador: ZERO ouvintes no `#jogosTabs`.
//
// Por isso agora TUDO acontece depois do `DOMContentLoaded`, que é disparado só quando todos
// os scripts síncronos do documento já rodaram — inclusive o Bootstrap, esteja ele onde
// estiver. Guarda-chuva contra a mesma armadilha se alguém mover um `<script>` de lugar.
// Ver Padelizou.Tests/js/conferir-abas-que-ficam.js, que roda nesta ordem de propósito.
//
// ⚠️ E A BARRA DE CIMA TAMBÉM É LEMBRADA (`#torneioTabs`). 🗣️ Felipe: *"estava mexendo na aba
// palpiteiros e sozinho foi para o aovivo, isso nao pode acontecer"*. Lembrar só as sub-abas
// devolvia a pessoa pra Jogos a cada recarregamento — de Palpiteiros, de Inscritos, de onde
// estivesse. As duas barras usam a mesma régua, com chaves separadas.
// ══════════════════════════════════════════════════════════════════════════════════════════
(function () {
    "use strict";

    // Cada barra com a sua chave. A de cima (as abas mãe do torneio) e a de baixo (Ao Vivo,
    // Agendadas, Finalizadas, Classificação).
    var BARRAS = [
        { id: "torneioTabs", chave: "pdz-aba-torneio:" },
        { id: "jogosTabs", chave: "pdz-aba-jogos:" },
    ];

    // sessionStorage pode ser PROIBIDO (navegação privada com cookies bloqueados) e aí o
    // próprio acesso estoura. Falhar aqui não pode derrubar a aba — sem memória, vale o padrão
    // do servidor.
    function lembrada(chave) {
        try { return window.sessionStorage.getItem(chave); } catch (e) { return null; }
    }

    function guardar(chave, valor) {
        try { window.sessionStorage.setItem(chave, valor); } catch (e) { /* sem memória */ }
    }

    function ligar(barra) {
        var pills = document.getElementById(barra.id);
        if (!pills) return;

        // Por torneio: quem opera dois no mesmo dia não herda a aba de um no outro.
        var chave = barra.chave + (pills.getAttribute("data-torneio-id") || "");

        // ⚠️ O OUVINTE É REGISTRADO ANTES DE QUALQUER COISA, e sem depender do Bootstrap: é o
        // que grava a escolha da pessoa. Só o RESTAURAR precisa do `bootstrap.Tab`.
        pills.addEventListener("shown.bs.tab", function (evento) {
            var escolhida = evento.target && evento.target.getAttribute("data-bs-target");
            if (escolhida) guardar(chave, escolhida);
        });

        var alvo = lembrada(chave);
        if (!alvo || !window.bootstrap) return;

        // A aba pode não existir nesta tela (Classificação só aparece no Americano) e pode já
        // ser a ativa. `querySelector` com o seletor montado é seguro: o valor guardado foi
        // escrito por nós a partir de um `data-bs-target` da própria página.
        var botao = pills.querySelector('.nav-link[data-bs-target="' + alvo + '"]');
        if (botao && !botao.classList.contains("active")) {
            window.bootstrap.Tab.getOrCreateInstance(botao).show();
        }
    }

    function ligarTudo() {
        // ⚠️ A DE CIMA PRIMEIRO: mostrar a aba mãe torna visíveis as sub-abas de dentro dela, e
        // o Bootstrap não mostra aba em painel escondido de jeito confiável.
        BARRAS.forEach(ligar);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", ligarTudo);
    } else {
        ligarTudo();
    }
})();
