/* Quadra de padel, em metros. Origem no centro da rede.
 * x: -5..5 (largura 10 m) · y: -10..10 (comprimento 20 m).
 * O time da casa (o humano, time 0) joga em y > 0 — embaixo na tela; os adversários (time 1) em y < 0.
 * "Direita" de um lado é a direita de quem, naquele lado, olha pra rede:
 * no lado +1 a direita é +x; no lado -1 a direita é -x. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});

  const Quadra = Object.freeze({
    LARGURA: 10,
    COMPRIMENTO: 20,
    MEIA_LARGURA: 5,
    MEIO_COMPRIMENTO: 10,
    LINHA_DE_SAQUE: 6.95,   // distância da rede
    ALTURA_DA_REDE: 0.9,    // 0,88 no centro e 0,92 nos postes — média
    ALTURA_DO_VIDRO: 3,
    ALTURA_DA_PAREDE: 4,    // 3 m de vidro + 1 m de grade; acima disso a bola sai da quadra

    ladoDe(y) { return y >= 0 ? 1 : -1; },
    ladoDoTime(time) { return time === 0 ? 1 : -1; },
    dentro(x, y) { return Math.abs(x) <= 5 && Math.abs(y) <= 10; },

    // Caixa de saque no lado `lado` (±1), à direita ou à esquerda de quem está naquele lado.
    // Vai da rede até a linha de saque, e do centro até a parede lateral.
    caixaDeSaque(lado, direita) {
      const sinalX = direita ? lado : -lado;
      return {
        xMin: sinalX > 0 ? 0 : -Quadra.MEIA_LARGURA,
        xMax: sinalX > 0 ? Quadra.MEIA_LARGURA : 0,
        yMin: lado > 0 ? 0 : -Quadra.LINHA_DE_SAQUE,
        yMax: lado > 0 ? Quadra.LINHA_DE_SAQUE : 0,
      };
    },
    dentroDaCaixa(x, y, caixa) {
      return x >= caixa.xMin && x <= caixa.xMax && y >= caixa.yMin && y <= caixa.yMax;
    },
  });

  Padel.Quadra = Quadra;
  if (typeof module !== 'undefined' && module.exports) module.exports = Quadra;
})();
