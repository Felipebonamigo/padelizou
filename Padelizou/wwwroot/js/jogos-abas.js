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
(function () {
    "use strict";

    var pills = document.getElementById("jogosTabs");
    if (!pills || !window.bootstrap) return;

    // Por torneio: quem opera dois no mesmo dia não herda a aba de um no outro.
    var chave = "pdz-aba-jogos:" + (pills.getAttribute("data-torneio-id") || "");

    function lembrada() {
        // sessionStorage pode ser proibido (navegação privada com cookies bloqueados). Falhar
        // aqui não pode derrubar a aba — sem memória, vale o padrão do servidor.
        try { return window.sessionStorage.getItem(chave); } catch (e) { return null; }
    }

    var alvo = lembrada();
    if (alvo) {
        // A aba pode não existir nesta tela (Classificação só aparece no Americano) e pode já
        // ser a ativa. `querySelector` com o seletor montado é seguro: o valor guardado foi
        // escrito por nós a partir de um `data-bs-target` da própria página.
        var botao = pills.querySelector('.nav-link[data-bs-target="' + alvo + '"]');
        if (botao && !botao.classList.contains("active")) {
            bootstrap.Tab.getOrCreateInstance(botao).show();
        }
    }

    pills.addEventListener("shown.bs.tab", function (evento) {
        var escolhida = evento.target.getAttribute("data-bs-target");
        if (!escolhida) return;
        try { window.sessionStorage.setItem(chave, escolhida); } catch (e) { /* sem memória */ }
    });
})();
