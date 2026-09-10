// COMPARTILHAR O TEXTO DA LISTA — o irmão do compartilhar-card.js, pra texto em vez de imagem.
//
// O link já é um wa.me com o texto dentro, e funciona sozinho em todo lugar (é a forma do
// convite do torneio). O que este script acrescenta é o menu nativo do CELULAR: lá o
// `navigator.share` com texto abre a lista de apps e o WhatsApp está nela — e, mais importante,
// não passa pela URL. Uma lista de sessenta jogos vira um texto de uns 10 KB; dentro de um
// `wa.me/?text=` é uma URL de uns 20 KB, e é justamente a ponta em que o WhatsApp Web tropeça.
//
// ⚠️ SÓ EM APARELHO MÓVEL. No desktop o `navigator.share` existe (Chrome, Edge) e abre a
// bandeja de compartilhamento do sistema, que quase nunca tem WhatsApp — pior que o wa.me,
// que abre o WhatsApp Web direto. Não há como perguntar "isto é um celular?" sem olhar o
// user-agent; por isso a heurística, e por isso o link continua sendo o caminho de sempre
// quando ela diz não.
(function () {
    'use strict';

    function ehCelular() {
        return /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
    }

    function textoDe(elemento) {
        var origem = document.getElementById(elemento.dataset.origem);
        return origem ? origem.value : '';
    }

    document.addEventListener('click', async function (e) {
        var link = e.target.closest('.pdz-compartilhar-texto');
        if (!link) return;
        if (!navigator.share || !ehCelular()) return;   // o href faz o trabalho

        e.preventDefault();
        try {
            await navigator.share({ text: textoDe(link) });
        } catch (erro) {
            // Cancelar o menu dispara AbortError — é a pessoa desistindo, não um defeito.
            if (erro && erro.name === 'AbortError') return;
            window.open(link.href, '_blank', 'noopener');
        }
    });

    document.addEventListener('click', function (e) {
        var botao = e.target.closest('.pdz-copiar-texto');
        if (!botao) return;

        var origem = document.getElementById(botao.dataset.origem);
        if (!origem) return;

        // Sem clipboard (http, navegador antigo) o texto fica selecionado pra copiar à mão.
        origem.select();
        if (navigator.clipboard) {
            navigator.clipboard.writeText(origem.value).then(function () {
                botao.innerHTML = '<i class="bi bi-clipboard-check"></i> Copiado!';
            });
        }
    });
})();
