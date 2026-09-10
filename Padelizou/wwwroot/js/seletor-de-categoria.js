// A CATEGORIA DA ABA "Chaves e Grupos" É ESCOLHIDA NUM <select> (Felipe, 10/09/2026:
// "visualmente nao ta legal isso aqui tambem, acho que um drop com select seria melhor").
// O porquê da troca está em Views/Torneios/Details.cshtml, ao lado do seletor.
//
// ⚠️ TROCA O PAINEL NA MÃO, sem bootstrap.Tab: são duas classes (`active` acende o display,
// `show` acende a opacidade do `fade` — só a primeira deixaria a categoria invisível), e
// assim o seletor não depende de o bundle do _Layout já ter carregado. Sem `<li>`/`<button>`
// não há nada que o plugin de abas faria por nós aqui.
(function () {
    "use strict";

    var select = document.getElementById("seletorDeCategoria");
    var painel = document.getElementById("painelDasCategorias");
    // Sai calado onde não há escolha: torneio de uma categoria só (o seletor nem é desenhado),
    // Americano (a tela é outra) e chaves ainda não aprovadas.
    if (!select || !painel) return;

    // Por TORNEIO, como o js/jogos-abas.js: quem opera dois no mesmo dia não herda no segundo
    // a categoria escolhida no primeiro. E sessionStorage, não localStorage, pelo mesmo motivo
    // de lá — é a escolha DESTA sessão de trabalho, não uma preferência permanente: quem abrir
    // o torneio amanhã começa onde o servidor achar melhor, que é a primeira categoria.
    var chave = "pdz-categoria-chaves:" + (select.getAttribute("data-torneio-id") || "");

    function mostrar(id) {
        // getElementById, e não um seletor montado com o valor: o texto vem de uma <option>
        // nossa, mas montar seletor com valor guardado é hábito que uma tela adiante custa
        // caro. A conferência do pai fecha a porta: só painel desta lista é aceito.
        var alvo = document.getElementById(id);
        if (!alvo || alvo.parentElement !== painel) return false;

        Array.prototype.forEach.call(painel.children, function (pane) {
            pane.classList.toggle("active", pane === alvo);
            pane.classList.toggle("show", pane === alvo);
        });
        return true;
    }

    // A ESCOLHA SOBREVIVE AO SALVAR. Dentro da categoria se troca dupla de grupo e se desenha
    // o chaveamento à mão; cada gravação é um POST que redesenha a página na PRIMEIRA
    // categoria, e com 12 delas era caçar a sua de novo a cada vez. Mesma queixa de 08/08 que
    // fez nascer o js/jogos-abas.js: "quando eu salvo aqui, ele volta pra tela de ao vivo".
    var lembrada = null;
    try { lembrada = window.sessionStorage.getItem(chave); } catch (e) { lembrada = null; }

    // A guardada pode não existir mais nesta tela (chave recolhida, categoria apagada). Aí vale
    // o valor do próprio <select> — que NÃO é sempre a primeira opção: num F5 o navegador
    // restaura sozinho o que estava escolhido (form restoration) e não restaura junto a classe
    // do painel, que vem do servidor sempre na primeira categoria. Sem esta linha, o seletor
    // diria "6ª Feminina" com a chave da 3ª Masculina desenhada embaixo.
    if (lembrada && mostrar(lembrada)) {
        select.value = lembrada;
    } else {
        mostrar(select.value);
    }

    select.addEventListener("change", function () {
        if (!mostrar(select.value)) return;
        try { window.sessionStorage.setItem(chave, select.value); } catch (e) { /* sem memória */ }
    });
})();
