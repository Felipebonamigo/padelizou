// JOGANDO OU DE FORA — o filtro da tabela de palpiteiros (12/09/2026).
//
// 🗣️ Felipe, com o print da aba Palpiteiros aberta: *"aqui no palpitometro, coloque um filtro,
// para ver se a pessoa esta jogando o torneio ou nao"*.
//
// ⚠️ NO NAVEGADOR, sem ida ao servidor: as 30+ linhas já vêm todas no HTML da página (o
// servidor marcou cada uma com `data-joga`), e recarregar pra esconder metade de uma tabela
// que já está na tela custaria o dobro — e ainda perderia a aba, que é a parte que irrita.
//
// ⚠️ ESCONDE LINHA, NÃO RENUMERA. A posição é a do torneio inteiro: virar "1º, 2º, 3º" dentro
// do recorte inventaria uma classificação que não existe. Quem filtra "de fora" quer saber
// quem, entre os que não jogam, está na frente dele na tabela DE VERDADE.
//
// ⚠️ `hidden`, e não `style.display`: assim a linha volta exatamente ao display que a tabela
// dava a ela (`table-row`), sem este arquivo precisar saber qual era.
function filtrarPalpiteiros(botao) {
    "use strict";

    // O escopo é o que permite duas tabelas na mesma página (o hub tem abas): sem ele, o
    // clique num filtro esconderia linha da tabela do vizinho.
    var escopo = botao.closest(".pdz-palpiteiros");
    if (!escopo) return;

    var filtro = botao.dataset.filtro;

    var botoes = escopo.querySelectorAll("[data-filtro]");
    Array.prototype.forEach.call(botoes, function (b) {
        b.classList.toggle("active", b === botao);
        // O leitor de tela precisa saber qual recorte está valendo — a classe `active` do
        // Bootstrap é só tinta.
        b.setAttribute("aria-pressed", b === botao ? "true" : "false");
    });

    var linhas = escopo.querySelectorAll("tbody tr[data-joga]");
    Array.prototype.forEach.call(linhas, function (tr) {
        var joga = tr.dataset.joga === "true";
        tr.hidden = filtro === "todos" ? false : (filtro === "jogando") !== joga;
    });
}
