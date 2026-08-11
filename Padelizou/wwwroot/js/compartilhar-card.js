// COMPARTILHAR O CARD — o botão que leva a arte do Padelizou pro story da pessoa.
//
// Por que existe um botão além do "Baixar": no celular, baixar é o caminho longo (salva, abre a
// galeria, escolhe o app, procura o arquivo). O compartilhamento nativo abre a lista de apps
// com a imagem já anexada, que é um toque. E é no celular que este card é usado — ninguém posta
// story do computador.
//
// ⚠️ O `navigator.share` com ARQUIVO é recente e não existe em todo lugar (desktop, navegadores
// antigos, e o próprio Android quando o site não está em https). Por isso ele é testado de
// verdade com `canShare({files})` antes de ser oferecido, e o botão CAI PRO DOWNLOAD quando não
// dá — botão que não faz nada é pior do que botão que faz o caminho longo.
(function () {
    'use strict';

    function baixar(url, arquivo) {
        var a = document.createElement('a');
        a.href = url;
        a.download = arquivo;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }

    async function compartilhar(botao) {
        var url = botao.dataset.imagem;
        var arquivo = botao.dataset.arquivo || 'padelizou.png';
        var titulo = botao.dataset.titulo || 'Padelizou';

        // Sem suporte a arquivo, nem tenta: `navigator.share` só com texto mandaria o link sem
        // a imagem, e a pessoa acharia que o botão está quebrado.
        if (!navigator.canShare || !navigator.share) {
            baixar(url, arquivo);
            return;
        }

        var textoOriginal = botao.innerHTML;
        botao.disabled = true;
        botao.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Preparando...';

        try {
            var resposta = await fetch(url);
            if (!resposta.ok) throw new Error('imagem indisponível');

            var blob = await resposta.blob();
            var imagem = new File([blob], arquivo, { type: 'image/png' });

            if (!navigator.canShare({ files: [imagem] })) {
                baixar(url, arquivo);
                return;
            }

            await navigator.share({ files: [imagem], title: titulo });
        } catch (erro) {
            // Cancelar o menu de compartilhamento dispara AbortError — é a pessoa desistindo,
            // não um defeito, e cair no download aqui baixaria um arquivo que ela não quis.
            if (erro && erro.name === 'AbortError') return;
            baixar(url, arquivo);
        } finally {
            botao.disabled = false;
            botao.innerHTML = textoOriginal;
        }
    }

    document.addEventListener('click', function (e) {
        var botao = e.target.closest('.pdz-compartilhar');
        if (botao) compartilhar(botao);
    });
})();
