// PUNHOS DE SHAOLIN — plataforma: ONDE o jogo salva e COM QUEM ele fala (Steam ou ninguém).
//
// No navegador: localStorage. No Electron: a ponte `window.punhos` que o `desktop/preload.js`
// expõe — arquivo em disco e, quando a Steam está aberta, conquistas/estatísticas/presença.
// O resto do jogo só conhece esta interface; a Steam entrar ou sair não muda uma linha lá.
//
// Tudo aqui engole erro com aviso: storage bloqueado, disco cheio, Steam fechada — o jogo segue.
(function (raiz) {
    'use strict';
    const CHAVE = 'punhos-de-shaolin.progresso';
    const CHAVE_RECORDE_ANTIGA = 'punhos-de-shaolin.recorde';
    const CHAVE_MUDO_ANTIGA = 'punhos-de-shaolin.mudo';

    function aviso(o, erro) { console.warn(`plataforma: ${o}`, erro && erro.message); }

    function criar() {
        const ponte = raiz.punhos && raiz.punhos.desktop ? raiz.punhos : null;

        function carregar() {
            if (ponte) {
                const texto = ponte.carregar();
                return texto ? JSON.parse(texto) : null;
            }
            let texto = null;
            try { texto = localStorage.getItem(CHAVE); } catch (erro) { aviso('sem localStorage pra ler', erro); return null; }
            if (texto) return JSON.parse(texto);
            // Migração do protótipo: recorde e mudo moravam em chaves soltas.
            try {
                const recorde = Number(localStorage.getItem(CHAVE_RECORDE_ANTIGA)) || 0;
                const mudo = localStorage.getItem(CHAVE_MUDO_ANTIGA) === '1';
                if (!recorde && !mudo) return null;
                return { recorde, opcoes: mudo ? { musica: 0, efeitos: 0 } : {} };
            } catch (_) { return null; }
        }

        function salvar(dados) {
            const texto = JSON.stringify(dados);
            if (ponte) { ponte.salvar(texto); return; }
            try { localStorage.setItem(CHAVE, texto); } catch (erro) { aviso('sem localStorage pra gravar', erro); }
        }

        return {
            ehDesktop: !!ponte,
            temSteam: !!(ponte && ponte.steam),
            carregar, salvar,
            conquistar(id) { if (ponte) ponte.conquistar(id); },
            estatistica(nome, valor) { if (ponte) ponte.estatistica(nome, valor); },
            presenca(texto) { if (ponte) ponte.presenca(texto); },
            sair() { if (ponte) ponte.sair(); },
            telaCheia(ligar) {
                if (ponte) { ponte.telaCheia(ligar); return; }
                try {
                    const ligado = !!document.fullscreenElement;
                    const querLigar = ligar == null ? !ligado : !!ligar;
                    if (querLigar && !ligado && document.documentElement.requestFullscreen) document.documentElement.requestFullscreen().catch(() => { });
                    else if (!querLigar && ligado) document.exitFullscreen().catch(() => { });
                } catch (erro) { aviso('tela cheia recusada', erro); }
            },
        };
    }

    raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {};
    raiz.PunhosDeShaolin.Plataforma = { criar };
})(window);
