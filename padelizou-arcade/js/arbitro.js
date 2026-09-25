/* Árbitro: transforma os eventos da física em decisão de ponto, seguindo as regras do padel.
 *
 * - Depois de um golpe, a bola precisa cruzar a rede e quicar no chão do outro lado ANTES de
 *   tocar qualquer parede de lá. Parede direto = ponto pra quem recebia.
 * - Quicou do lado de quem bateu (não passou, ou parou na rede) = ponto pra quem recebia.
 * - Bater nas próprias paredes antes de cruzar é permitido.
 * - Depois de quicar, a bola pode bater nas paredes à vontade; o segundo quique encerra o
 *   ponto a favor de quem bateu. Se voltar pela rede depois de quicar, idem.
 * - Saiu por cima da parede: ponto pra quem bateu se já tinha quicado; senão, contra.
 * - Saque: precisa quicar na caixa diagonal; erro é falta, duas faltas é ponto; tocar a
 *   rede e cair na caixa é let (repete). Quem recebe não pode voleiar o saque.
 * - O mesmo time não bate duas vezes seguidas. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});
  const Quadra = Padel.Quadra || (typeof require === 'function' ? require('./quadra.js') : null);

  class Arbitro {
    constructor() {
      this.faltas = 0;
      this._zerarRally();
    }

    _zerarRally() {
      this.emSaque = false;
      this.golpeador = null;         // time que bateu por último
      this.quicouNoReceptor = false;
      this.tocouARede = false;
      this.cruzou = false;
      this.caixa = null;
    }

    novoPonto() { this.faltas = 0; this._zerarRally(); }

    iniciarSaque(time, caixa) {
      this._zerarRally();
      this.emSaque = true;
      this.golpeador = time;
      this.caixa = caixa;
    }

    registrarGolpe(time) {
      this._zerarRally();
      this.golpeador = time;
    }

    get receptor() { return this.golpeador === null ? null : 1 - this.golpeador; }

    podeGolpear(time) {
      if (this.golpeador === null || this.golpeador === time) return false;
      if (this.emSaque && !this.quicouNoReceptor) return false;
      return true;
    }

    // Devolve null (segue), { tipo: 'ponto', para, motivo }, { tipo: 'falta', motivo } ou { tipo: 'let' }.
    processar(evento) {
      if (this.golpeador === null) return null;
      const ladoDoGolpeador = Quadra.ladoDoTime(this.golpeador);
      switch (evento.tipo) {
        case 'cruzouRede':
          if (evento.para !== ladoDoGolpeador) { this.cruzou = true; return null; }
          if (this.quicouNoReceptor) return this._ponto(this.golpeador, 'voltouPeloVidro');
          return null;
        case 'rede':
          this.tocouARede = true;
          return null;
        case 'quique':
          if (evento.lado === ladoDoGolpeador) return this._contraOGolpeador(this.tocouARede ? 'rede' : 'naoPassou');
          if (this.quicouNoReceptor) return this._ponto(this.golpeador, 'doisQuiques');
          this.quicouNoReceptor = true;
          if (this.emSaque) {
            if (!Quadra.dentroDaCaixa(evento.x, evento.y, this.caixa)) return this._contraOGolpeador('foraDaCaixa');
            if (this.tocouARede) return { tipo: 'let' };
          }
          return null;
        case 'parede':
          if (evento.lado === ladoDoGolpeador) return this.emSaque ? this._contraOGolpeador('paredePropria') : null;
          if (!this.quicouNoReceptor) return this._contraOGolpeador('paredeSemQuicar');
          return null;
        case 'saiu':
          if (this.quicouNoReceptor) return this._ponto(this.golpeador, 'fora');
          return this._contraOGolpeador('fora');
        default:
          return null;
      }
    }

    _contraOGolpeador(motivo) {
      if (!this.emSaque) return this._ponto(this.receptor, motivo);
      this.faltas += 1;
      if (this.faltas >= 2) return this._ponto(this.receptor, 'duplaFalta');
      return { tipo: 'falta', motivo };
    }

    _ponto(para, motivo) {
      return { tipo: 'ponto', para, motivo };
    }
  }

  Padel.Arbitro = Arbitro;
  if (typeof module !== 'undefined' && module.exports) module.exports = Arbitro;
})();
