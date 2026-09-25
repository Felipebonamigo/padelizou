/* Entrada do humano: teclado (setas/WASD, Espaço, P/Esc) e toque (joystick virtual na área
 * da quadra + botão de ação). `ler()` devolve o estado do quadro e consome os "apertou". */
(function () {
  'use strict';
  const Padel = window.Padel;

  const TECLAS = {
    ArrowLeft: 'esquerda', KeyA: 'esquerda',
    ArrowRight: 'direita', KeyD: 'direita',
    ArrowUp: 'cima', KeyW: 'cima',
    ArrowDown: 'baixo', KeyS: 'baixo',
    Space: 'acao', Enter: 'acao', KeyJ: 'acao', KeyL: 'acao',
    Escape: 'pausa', KeyP: 'pausa',
  };

  class Entrada {
    constructor({ areaDoToque, botaoAcao, joystick, knob }) {
      this.ativas = new Set();
      this.acaoPressionada = false;
      this.pausaPressionada = false;
      this.toque = { id: null, x0: 0, y0: 0, dx: 0, dy: 0 };
      this.acaoNoToque = false;
      this.joystick = joystick;
      this.knob = knob;
      this.aoInteragir = null;   // callback pro primeiro gesto (destrava o áudio)

      window.addEventListener('keydown', (e) => {
        const nome = TECLAS[e.code];
        if (!nome) return;
        if (e.repeat) { e.preventDefault(); return; }
        if (nome === 'acao') this.acaoPressionada = true;
        if (nome === 'pausa') this.pausaPressionada = true;
        this.ativas.add(nome);
        this._interagiu();
        e.preventDefault();
      });
      window.addEventListener('keyup', (e) => {
        const nome = TECLAS[e.code];
        if (nome) { this.ativas.delete(nome); e.preventDefault(); }
      });
      window.addEventListener('blur', () => this.ativas.clear());

      if (areaDoToque) this._ligarJoystick(areaDoToque);
      if (botaoAcao) this._ligarBotao(botaoAcao);
    }

    _interagiu() { if (this.aoInteragir) this.aoInteragir(); }

    _ligarJoystick(area) {
      const RAIO = 45;
      area.addEventListener('touchstart', (e) => {
        for (const t of e.changedTouches) {
          if (this.toque.id !== null) continue;
          this.toque = { id: t.identifier, x0: t.clientX, y0: t.clientY, dx: 0, dy: 0 };
          this._mostrarJoystick(t.clientX, t.clientY);
          this._interagiu();
        }
        e.preventDefault();
      }, { passive: false });
      area.addEventListener('touchmove', (e) => {
        for (const t of e.changedTouches) {
          if (t.identifier !== this.toque.id) continue;
          let dx = t.clientX - this.toque.x0, dy = t.clientY - this.toque.y0;
          const n = Math.hypot(dx, dy);
          if (n > RAIO) { dx = (dx / n) * RAIO; dy = (dy / n) * RAIO; }
          this.toque.dx = dx / RAIO; this.toque.dy = dy / RAIO;
          if (this.knob) this.knob.style.transform = `translate(${dx}px, ${dy}px)`;
        }
        e.preventDefault();
      }, { passive: false });
      const soltar = (e) => {
        for (const t of e.changedTouches) {
          if (t.identifier !== this.toque.id) continue;
          this.toque = { id: null, x0: 0, y0: 0, dx: 0, dy: 0 };
          this._esconderJoystick();
        }
      };
      area.addEventListener('touchend', soltar);
      area.addEventListener('touchcancel', soltar);
    }

    _ligarBotao(botao) {
      const apertar = (e) => { this.acaoNoToque = true; this.acaoPressionada = true; this._interagiu(); e.preventDefault(); };
      const soltar = (e) => { this.acaoNoToque = false; if (e.cancelable) e.preventDefault(); };
      botao.addEventListener('touchstart', apertar, { passive: false });
      botao.addEventListener('touchend', soltar);
      botao.addEventListener('touchcancel', soltar);
      botao.addEventListener('mousedown', apertar);
      botao.addEventListener('mouseup', soltar);
      botao.addEventListener('mouseleave', soltar);
    }

    _mostrarJoystick(x, y) {
      if (!this.joystick) return;
      this.joystick.style.left = `${x}px`;
      this.joystick.style.top = `${y}px`;
      this.joystick.classList.add('ativo');
      if (this.knob) this.knob.style.transform = 'translate(0px, 0px)';
    }

    _esconderJoystick() { if (this.joystick) this.joystick.classList.remove('ativo'); }

    ler() {
      let dx = 0, dy = 0;
      if (this.ativas.has('esquerda')) dx -= 1;
      if (this.ativas.has('direita')) dx += 1;
      if (this.ativas.has('cima')) dy -= 1;
      if (this.ativas.has('baixo')) dy += 1;
      if (this.toque.id !== null) { dx = this.toque.dx; dy = this.toque.dy; }
      const leitura = {
        dx, dy,
        acaoSegurada: this.ativas.has('acao') || this.acaoNoToque,
        acaoPressionada: this.acaoPressionada,
        pausa: this.pausaPressionada,
      };
      this.acaoPressionada = false;
      this.pausaPressionada = false;
      return leitura;
    }
  }

  Padel.Entrada = Entrada;
})();
