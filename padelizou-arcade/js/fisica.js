/* Física da bola em 3D (x, y no chão; z pra cima), em metros e segundos.
 * Gravidade, arrasto leve, quique no chão, rebote nas quatro paredes, rede como obstáculo,
 * saída por cima da parede. `avancar` devolve os eventos do intervalo — é o árbitro quem
 * decide o que cada um significa. `calcularGolpe` acha a velocidade que leva a bola de um
 * ponto a um alvo no chão passando por cima da rede. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});
  const Quadra = Padel.Quadra || (typeof require === 'function' ? require('./quadra.js') : null);

  const G = 9.81;
  const ARRASTO = 0.04;             // fração da velocidade perdida por segundo no ar
  const RESTITUICAO_CHAO = 0.7;     // bola de padel tem menos pressão que a de tênis
  const ATRITO_CHAO = 0.86;
  const RESTITUICAO_PAREDE = 0.78;
  const RESTITUICAO_REDE = 0.2;
  const PASSO_MAXIMO = 1 / 240;
  const VZ_MINIMO_PARA_QUICAR = 0.7; // abaixo disso a bola para de quicar e rola

  class Bola {
    constructor() { this.reiniciar(); }

    reiniciar() {
      this.x = 0; this.y = 0; this.z = 1;
      this.vx = 0; this.vy = 0; this.vz = 0;
      this.emJogo = false;
      this.rolando = false;
      this.parada = false;
    }

    clonar() { return Object.assign(new Bola(), this); }

    posicionar(x, y, z) {
      this.x = x; this.y = y; this.z = z;
      this.vx = 0; this.vy = 0; this.vz = 0;
      this.rolando = false; this.parada = false;
    }

    lancar(v) {
      this.vx = v.vx; this.vy = v.vy; this.vz = v.vz;
      this.emJogo = true; this.rolando = false; this.parada = false;
    }

    get velocidade() { return Math.hypot(this.vx, this.vy, this.vz); }

    avancar(dt, eventos = []) {
      if (!this.emJogo || this.parada) return eventos;
      let restante = dt;
      while (restante > 1e-9 && this.emJogo && !this.parada) {
        const h = Math.min(PASSO_MAXIMO, restante);
        this._passo(h, eventos);
        restante -= h;
      }
      return eventos;
    }

    _passo(h, eventos) {
      const yAntes = this.y, zAntes = this.z;
      if (this.rolando) {
        const freio = Math.max(0, 1 - 2.5 * h);
        this.vx *= freio; this.vy *= freio; this.vz = 0; this.z = 0;
        if (Math.hypot(this.vx, this.vy) < 0.05) { this.vx = 0; this.vy = 0; this.parada = true; return; }
      } else {
        this.vz -= G * h;
        const ar = 1 - ARRASTO * h;
        this.vx *= ar; this.vy *= ar; this.vz *= ar;
      }
      this.x += this.vx * h; this.y += this.vy * h; this.z += this.vz * h;

      // Rede: plano y = 0 até ALTURA_DA_REDE.
      if ((yAntes < 0 && this.y >= 0) || (yAntes > 0 && this.y <= 0)) {
        const fracao = yAntes / (yAntes - this.y);
        const zNaRede = zAntes + (this.z - zAntes) * fracao;
        const veioDe = yAntes > 0 ? 1 : -1;
        if (zNaRede < Quadra.ALTURA_DA_REDE) {
          this.y = veioDe * 0.03;
          this.vy = -this.vy * RESTITUICAO_REDE;
          this.vx *= 0.4;
          this.vz = Math.min(this.vz, 0) * 0.3;
          eventos.push({ tipo: 'rede', x: this.x, y: this.y, lado: veioDe });
        } else {
          eventos.push({ tipo: 'cruzouRede', para: -veioDe, x: this.x, z: zNaRede });
        }
      }

      // Chão.
      if (this.z <= 0 && !this.rolando) {
        this.z = 0;
        this.vz = -this.vz * RESTITUICAO_CHAO;
        this.vx *= ATRITO_CHAO; this.vy *= ATRITO_CHAO;
        eventos.push({ tipo: 'quique', x: this.x, y: this.y, lado: Quadra.ladoDe(this.y) });
        if (this.vz < VZ_MINIMO_PARA_QUICAR) { this.vz = 0; this.rolando = true; }
      }

      // Paredes laterais.
      if (Math.abs(this.x) > Quadra.MEIA_LARGURA) {
        if (this.z > Quadra.ALTURA_DA_PAREDE) { this._sair(eventos); return; }
        this.x = Math.sign(this.x) * (Quadra.MEIA_LARGURA - 0.001);
        this.vx = -this.vx * RESTITUICAO_PAREDE;
        eventos.push({ tipo: 'parede', qual: 'lateral', x: this.x, y: this.y, lado: Quadra.ladoDe(this.y) });
      }
      // Paredes de fundo.
      if (Math.abs(this.y) > Quadra.MEIO_COMPRIMENTO) {
        if (this.z > Quadra.ALTURA_DA_PAREDE) { this._sair(eventos); return; }
        this.y = Math.sign(this.y) * (Quadra.MEIO_COMPRIMENTO - 0.001);
        this.vy = -this.vy * RESTITUICAO_PAREDE;
        eventos.push({ tipo: 'parede', qual: 'fundo', x: this.x, y: this.y, lado: Quadra.ladoDe(this.y) });
      }
    }

    _sair(eventos) {
      this.emJogo = false;
      eventos.push({ tipo: 'saiu', x: this.x, y: this.y, lado: Quadra.ladoDe(this.y) });
    }
  }

  /* Velocidade inicial pra levar a bola de `origem` {x,y,z} ao `alvo` {x,y} (no chão) em
   * `tempoDeVoo` segundos. Se a trajetória não passa a rede com folga, alonga o tempo
   * (arco mais alto) até passar. `ignorarRede` deixa a bola ir baixa de propósito (erro). */
  function calcularGolpe(origem, alvo, opcoes = {}) {
    const { tempoDeVoo = 0.9, folgaNaRede = 0.3, ignorarRede = false } = opcoes;
    let T = tempoDeVoo;
    let resultado = null;
    for (let i = 0; i < 16; i++) {
      // A bola perde ~ARRASTO*T/2 da velocidade horizontal no caminho; compensa.
      const compensacao = 1 / (1 - ARRASTO * T / 2);
      const vx = (alvo.x - origem.x) / T * compensacao;
      const vy = (alvo.y - origem.y) / T * compensacao;
      const vz = (0.5 * G * T * T - origem.z) / T;
      resultado = { vx, vy, vz, tempo: T };
      if (ignorarRede) break;
      const cruza = Math.sign(alvo.y) !== Math.sign(origem.y) && origem.y !== 0;
      if (!cruza || vy === 0) break;
      const tRede = -origem.y / vy;
      const zNaRede = origem.z + vz * tRede - 0.5 * G * tRede * tRede;
      if (zNaRede >= Quadra.ALTURA_DA_REDE + folgaNaRede) break;
      T *= 1.1;
    }
    return resultado;
  }

  const Fisica = Object.freeze({ Bola, calcularGolpe, G, ARRASTO });
  Padel.Fisica = Fisica;
  if (typeof module !== 'undefined' && module.exports) module.exports = Fisica;
})();
