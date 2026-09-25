/* Sons sintetizados com WebAudio — nenhum arquivo de áudio. O contexto só nasce no primeiro
 * gesto do usuário (regra dos browsers). */
(function () {
  'use strict';
  const Padel = window.Padel;

  const Som = {
    ctx: null,
    ligado: true,

    garantir() {
      const Contexto = window.AudioContext || window.webkitAudioContext;
      if (!Contexto) return;
      if (!this.ctx) this.ctx = new Contexto();
      if (this.ctx.state === 'suspended') this.ctx.resume().catch(() => {});
    },

    tocar(tipo, dados) {
      if (!this.ligado || !this.ctx || this.ctx.state !== 'running') return;
      switch (tipo) {
        case 'golpe': this._ruido(0.05, 0.5); this._tom(160, 0.08, 'triangle', 0.25); break;
        case 'quique': this._tom(240, 0.06, 'sine', 0.22); break;
        case 'parede': this._tom(110, 0.1, 'triangle', 0.3); this._ruido(0.04, 0.2); break;
        case 'rede': this._tom(80, 0.14, 'sawtooth', 0.15); break;
        case 'ponto': dados && dados.time === 0 ? this._melodia([523, 659], 0.09) : this._melodia([330, 262], 0.11); break;
        case 'game': dados && dados.time === 0 ? this._melodia([523, 659, 784], 0.1) : this._melodia([392, 330, 262], 0.12); break;
        case 'set':
        case 'partida': dados && dados.time === 0 ? this._melodia([523, 659, 784, 1047], 0.12) : this._melodia([392, 349, 330, 262], 0.14); break;
        case 'falta':
        case 'let': this._tom(440, 0.08, 'square', 0.08); break;
        default: break;
      }
    },

    _tom(frequencia, duracao, forma, ganho) {
      const ctx = this.ctx;
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = forma;
      osc.frequency.setValueAtTime(frequencia, ctx.currentTime);
      g.gain.setValueAtTime(ganho, ctx.currentTime);
      g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duracao);
      osc.connect(g).connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + duracao + 0.02);
    },

    _ruido(duracao, ganho) {
      const ctx = this.ctx;
      const amostras = Math.floor(ctx.sampleRate * duracao);
      const buffer = ctx.createBuffer(1, amostras, ctx.sampleRate);
      const canal = buffer.getChannelData(0);
      for (let i = 0; i < amostras; i++) canal[i] = (Math.random() * 2 - 1) * (1 - i / amostras);
      const fonte = ctx.createBufferSource();
      fonte.buffer = buffer;
      const g = ctx.createGain();
      g.gain.value = ganho;
      fonte.connect(g).connect(ctx.destination);
      fonte.start();
    },

    _melodia(notas, passo) {
      const ctx = this.ctx;
      notas.forEach((f, i) => {
        const osc = ctx.createOscillator();
        const g = ctx.createGain();
        const inicio = ctx.currentTime + i * passo;
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(f, inicio);
        g.gain.setValueAtTime(0.0001, inicio);
        g.gain.exponentialRampToValueAtTime(0.25, inicio + 0.01);
        g.gain.exponentialRampToValueAtTime(0.001, inicio + passo * 1.6);
        osc.connect(g).connect(ctx.destination);
        osc.start(inicio);
        osc.stop(inicio + passo * 1.7);
      });
    },
  };

  Padel.Som = Som;
})();
