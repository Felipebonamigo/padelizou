// ACHAR UM INSCRITO NA LISTA DO ORGANIZADOR (13/09/2026).
//
// 🗣️ Felipe, com o print do "Gerenciar Inscritos" do 2ª Etapa ER PADEL TOUR aberto no
// celular: *"aqui no gerenciar escrito esta dificil achar, permita pesquisar por nome,
// coloque filtro por categoria, deixe melhor essa parte"*. A lista tem uma linha por dupla,
// categoria após categoria — achar "o Rafael" ali é rolar a tela inteira.
//
// ⚠️ NO NAVEGADOR, sem ida ao servidor: as linhas JÁ vêm todas no HTML (o servidor marcou
// cada uma com `data-nome`, `data-categoria`, `data-pago`, `data-parceiro` e `data-espera`).
// Filtrar no servidor recarregaria uma página de 1 MB, perderia a ABA e os painéis abertos —
// e o organizador está num ginásio, com a internet do ginásio.
//
// ⚠️ `d-none`, e NÃO o atributo `hidden` do irmão filtro-de-palpiteiros.js: lá as linhas são
// `<tr>`, que o navegador esconde sozinho; aqui são `.list-group-item`, e o Bootstrap lhes dá
// `display: block` — regra de autor ganha do `[hidden]` da folha do navegador, e a linha
// "escondida" continuaria na tela. `.d-none` é `display: none !important`, do mesmo Bootstrap.
//
// ⚠️ ESCONDE O CABEÇALHO DA CATEGORIA QUE FICOU VAZIA. Sem isso, buscar um nome deixa a tela
// com uma pilha de títulos de categoria e ninguém embaixo — mais confuso do que a lista
// comprida que o filtro veio consertar.
//
// ⚠️ O ESCOPO (`.pdz-inscritos-filtraveis`) existe porque a página tem DUAS destas listas: a
// do "Gerenciar Inscritos" e a de "Pagamentos e impedimentos". Sem ele, digitar numa
// esconderia linha da outra.
(function () {
    "use strict";

    // "coração" e "coracao" têm que achar a mesma pessoa — `normalize('NFD')` separa o acento
    // do caractere e o `replace` o tira. Mesma peneira do teclado-de-emoji.js.
    function semAcento(texto) {
        return String(texto || "").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
    }

    function cada(lista, fn) {
        Array.prototype.forEach.call(lista, fn);
    }

    // Os três recortes são um E, não um OU: quem escolheu "Não pagos" na 3ª Masculina e
    // digitou "rafael" quer as três coisas ao mesmo tempo.
    function combina(item, termos, categoria, situacao) {
        if (categoria && item.dataset.categoria !== categoria) return false;
        if (situacao === "naopagos" && item.dataset.pago !== "nao") return false;
        if (situacao === "semparceiro" && item.dataset.parceiro !== "falta") return false;
        if (situacao === "espera" && item.dataset.espera !== "sim") return false;

        // Cada termo separado tem que aparecer em ALGUM lugar do nome da dupla — é o que faz
        // "felipe bage" achar "Felipe Bonamigo / Guilherme Bagesteiro", com um pedaço em cada
        // jogador. Buscar a frase inteira de uma vez só acharia isso se a pessoa digitasse os
        // dois nomes completos, na ordem.
        var nome = semAcento(item.dataset.nome);
        for (var i = 0; i < termos.length; i++) {
            if (nome.indexOf(termos[i]) < 0) return false;
        }

        return true;
    }

    function aplicar(escopo) {
        var campo = escopo.querySelector(".pdz-fi-busca");
        var seletor = escopo.querySelector(".pdz-fi-categoria");
        var ativo = escopo.querySelector(".pdz-fi-situacao.active");

        var termos = semAcento(campo && campo.value).split(/\s+/).filter(Boolean);
        var categoria = seletor ? seletor.value : "";
        var situacao = ativo ? ativo.dataset.situacao : "todos";

        var aparecendo = 0;

        cada(escopo.querySelectorAll(".pdz-fi-bloco"), function (bloco) {
            var noBloco = 0;

            cada(bloco.querySelectorAll(".pdz-fi-item"), function (item) {
                var mostra = combina(item, termos, categoria, situacao);
                item.classList.toggle("d-none", !mostra);
                if (mostra) noBloco++;
            });

            bloco.classList.toggle("d-none", noBloco === 0);
            aparecendo += noBloco;
        });

        var vazio = escopo.querySelector(".pdz-fi-vazio");
        if (vazio) vazio.classList.toggle("d-none", aparecendo > 0);
    }

    function ligar(escopo) {
        var campo = escopo.querySelector(".pdz-fi-busca");
        if (campo) campo.addEventListener("input", function () { aplicar(escopo); });

        var seletor = escopo.querySelector(".pdz-fi-categoria");
        if (seletor) seletor.addEventListener("change", function () { aplicar(escopo); });

        cada(escopo.querySelectorAll(".pdz-fi-situacao"), function (botao) {
            botao.addEventListener("click", function () {
                cada(escopo.querySelectorAll(".pdz-fi-situacao"), function (outro) {
                    outro.classList.toggle("active", outro === botao);
                    // A classe `active` do Bootstrap é tinta: quem usa leitor de tela precisa
                    // do estado dito com todas as letras.
                    outro.setAttribute("aria-pressed", outro === botao ? "true" : "false");
                });

                aplicar(escopo);
            });
        });

        // Uma passada na entrada: o navegador restaura o que estava digitado quando a pessoa
        // volta pra página (e toda ação daqui é POST → redirect → GET, então ela volta muito).
        aplicar(escopo);
    }

    function iniciar() {
        cada(document.querySelectorAll(".pdz-inscritos-filtraveis"), ligar);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciar);
    else iniciar();
})();
