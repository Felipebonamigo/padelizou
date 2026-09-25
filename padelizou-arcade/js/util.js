/* Utilidades sem dependência: aleatório com semente (pra simulação reproduzível nos testes),
 * ruído gaussiano e limitação de valor. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});

  // mulberry32: gerador pequeno, determinístico, bom o bastante pra jogo.
  function criarAleatorio(semente) {
    let s = (semente >>> 0) || 0x9e3779b9;
    return function () {
      s = (s + 0x6d2b79f5) >>> 0;
      let t = s;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  // Box-Muller: média 0, desvio 1.
  function gaussiana(aleatorio) {
    let u = 0, v = 0;
    while (u === 0) u = aleatorio();
    while (v === 0) v = aleatorio();
    return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
  }

  function limitar(valor, minimo, maximo) {
    return Math.min(maximo, Math.max(minimo, valor));
  }

  const Util = Object.freeze({ criarAleatorio, gaussiana, limitar });
  Padel.Util = Util;
  if (typeof module !== 'undefined' && module.exports) module.exports = Util;
})();
