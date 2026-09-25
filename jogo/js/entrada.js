// PUNHOS DE SHAOLIN — entrada: teclado, controle (Gamepad API) e toque (joystick + botões).
//
// O motor recebe, por jogador, um objeto `entrada` com o que está SEGURADO e o que foi
// APERTADO neste quadro (`apertou`). A borda "apertou" é calculada aqui, comparando com o
// quadro anterior — o motor nunca precisa lembrar de tecla.
//
// Teclado — P1: setas movem · Z soco · X chute · C especial · V agarrar · B defender · Espaço pula
//           P2: WASD movem  · J soco · K chute · L especial · U agarrar · I defender · H pula
//           Enter confirma · J faz o P2 entrar · Esc ou P pausa · M mudo · F tela cheia
// Controle — analógico/d-pad move · X soco · B chute · Y especial · A pula · RB agarrar · LB/LT defende · Start pausa
(function (raiz) {
    'use strict';
    const Motor = raiz.PunhosDeShaolin.Motor;

    const TECLAS = [
        { ArrowLeft: 'esquerda', ArrowRight: 'direita', ArrowUp: 'cima', ArrowDown: 'baixo', KeyZ: 'soco', KeyX: 'chute', KeyC: 'especial', KeyV: 'agarrar', KeyB: 'defender', Space: 'pular' },
        { KeyA: 'esquerda', KeyD: 'direita', KeyW: 'cima', KeyS: 'baixo', KeyJ: 'soco', KeyK: 'chute', KeyL: 'especial', KeyU: 'agarrar', KeyI: 'defender', KeyH: 'pular' },
    ];
    const BOTOES_CONTROLE = { 2: 'soco', 1: 'chute', 3: 'especial', 0: 'pular', 5: 'agarrar', 4: 'defender', 6: 'defender', 14: 'esquerda', 15: 'direita', 12: 'cima', 13: 'baixo' };

    function criar(opcoes) {
        const o = opcoes || {};
        const teclas = new Set();
        // Tecla apertada E solta entre dois quadros ainda conta como apertada no próximo `ler()`:
        // sem isso um toque mais curto que 16 ms (teclado rápido, quadro perdido) some calado.
        const pendentes = new Set();
        const sistema = { confirmar: false, voltar: false, pausa: false, mudo: false, telaCheia: false, entrarP2: false };
        let anterior = [Motor.entradaVazia(), Motor.entradaVazia()];
        const toque = { ativo: false, dx: 0, dy: 0, correr: false, botoes: {} };
        let houveToque = false;

        raiz.addEventListener('keydown', ev => {
            if (ev.repeat) { if (ev.code !== 'Tab') ev.preventDefault(); return; }
            teclas.add(ev.code); pendentes.add(ev.code);
            if (ev.code === 'Enter') { sistema.confirmar = true; sistema.entrarP2 = true; }
            if (ev.code === 'Space') sistema.confirmar = true;
            if (ev.code === 'Escape' || ev.code === 'KeyP') { sistema.pausa = true; sistema.voltar = ev.code === 'Escape'; }
            if (ev.code === 'KeyM') sistema.mudo = true;
            // F11 também pela página: no Electron o `before-input-event` do main.js pega o F11 nativo e
            // impede que chegue aqui; quando não pega (entrada injetada, outro sistema), este caminho
            // passa pela mesma ponte que o F usa — e que está conferida.
            if (ev.code === 'KeyF' || ev.code === 'F11') { sistema.telaCheia = true; if (ev.code === 'F11') ev.preventDefault(); }
            if (ev.code.startsWith('Arrow') || ev.code === 'Space') ev.preventDefault();
        });
        raiz.addEventListener('keyup', ev => teclas.delete(ev.code));
        raiz.addEventListener('blur', () => teclas.clear());

        // ── TOQUE ─────────────────────────────────────────────────────────────────────
        if (o.joystick && o.botoes) {
            const zona = o.joystick, bolinha = o.joystickBolinha;
            let ponteiro = null, origem = null;
            const RAIO = 44;
            zona.addEventListener('pointerdown', ev => {
                houveToque = true; ponteiro = ev.pointerId; origem = { x: ev.clientX, y: ev.clientY };
                zona.setPointerCapture(ponteiro); toque.ativo = true; ev.preventDefault();
            });
            zona.addEventListener('pointermove', ev => {
                if (ev.pointerId !== ponteiro) return;
                const dx = ev.clientX - origem.x, dy = ev.clientY - origem.y;
                const d = Math.hypot(dx, dy);
                const limitado = Math.min(d, RAIO * 1.4);
                const nx = d ? dx / d : 0, ny = d ? dy / d : 0;
                toque.dx = d > 10 ? nx : 0; toque.dy = d > 10 ? ny : 0;
                toque.correr = d > RAIO * 1.15;
                if (bolinha) bolinha.style.transform = `translate(${nx * limitado}px, ${ny * limitado}px)`;
            });
            const soltar = ev => {
                if (ev.pointerId !== ponteiro) return;
                ponteiro = null; toque.dx = 0; toque.dy = 0; toque.correr = false; toque.ativo = false;
                if (bolinha) bolinha.style.transform = '';
            };
            zona.addEventListener('pointerup', soltar);
            zona.addEventListener('pointercancel', soltar);
            for (const botao of o.botoes.querySelectorAll('[data-botao]')) {
                const nome = botao.dataset.botao;
                const apertar = ev => { houveToque = true; toque.botoes[nome] = true; botao.classList.add('apertado'); if (nome === 'pausa') sistema.pausa = true; sistema.confirmar = sistema.confirmar || nome === 'soco' || nome === 'pular'; ev.preventDefault(); };
                const soltarBotao = () => { toque.botoes[nome] = false; botao.classList.remove('apertado'); };
                botao.addEventListener('pointerdown', apertar);
                botao.addEventListener('pointerup', soltarBotao);
                botao.addEventListener('pointercancel', soltarBotao);
                botao.addEventListener('pointerleave', soltarBotao);
                botao.addEventListener('contextmenu', ev => ev.preventDefault());
            }
        }

        function lerControle(indice) {
            const lista = navigator.getGamepads ? navigator.getGamepads() : [];
            const gp = lista && lista[indice];
            if (!gp) return null;
            const e = Motor.entradaVazia();
            const ax = gp.axes[0] || 0, ay = gp.axes[1] || 0;
            if (ax < -0.4) e.esquerda = true; if (ax > 0.4) e.direita = true;
            if (ay < -0.4) e.cima = true; if (ay > 0.4) e.baixo = true;
            if (Math.abs(ax) > 0.85) e.correr = true;
            for (const [idx, nome] of Object.entries(BOTOES_CONTROLE)) {
                const b = gp.buttons[idx];
                if (b && (b.pressed || b.value > 0.5)) e[nome] = true;
            }
            const start = gp.buttons[9];
            e._start = !!(start && start.pressed);
            return e;
        }

        // Devolve [entradaP1, entradaP2] com as bordas calculadas. Chamar UMA vez por quadro.
        function ler() {
            const atual = [];
            for (let p = 0; p < 2; p++) {
                const e = Motor.entradaVazia();
                for (const [codigo, nome] of Object.entries(TECLAS[p])) if (teclas.has(codigo) || pendentes.has(codigo)) e[nome] = true;
                const gp = lerControle(p);
                if (gp) {
                    for (const b of Motor.BOTOES) e[b] = e[b] || gp[b];
                    e.correr = e.correr || gp.correr;
                    if (gp._start && !anterior[p]._start) { sistema.pausa = true; sistema.confirmar = true; if (p === 1) sistema.entrarP2 = true; }
                    e._start = gp._start;
                }
                if (p === 0 && houveToque) {
                    if (toque.dx < -0.4) e.esquerda = true; if (toque.dx > 0.4) e.direita = true;
                    if (toque.dy < -0.4) e.cima = true; if (toque.dy > 0.4) e.baixo = true;
                    e.correr = e.correr || toque.correr;
                    for (const b of Motor.BOTOES) if (toque.botoes[b]) e[b] = true;
                }
                for (const b of Motor.BOTOES) e.apertou[b] = e[b] && !anterior[p][b];
                atual.push(e);
            }
            anterior = atual;
            pendentes.clear();
            return atual;
        }

        // Eventos de sistema (menus, pausa) — cada leitura zera.
        function lerSistema() {
            const copia = Object.assign({}, sistema);
            for (const k of Object.keys(sistema)) sistema[k] = false;
            return copia;
        }

        return { ler, lerSistema, get houveToque() { return houveToque; } };
    }

    raiz.PunhosDeShaolin.Entrada = { criar };
})(window);
