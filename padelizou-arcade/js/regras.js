/* Placar de padel, puro (sem física, sem tela): pontos 0/15/30/40, ponto de ouro ou vantagem,
 * games, tie-break em 6-6, sets, partida em melhor de N. Também diz quem saca e de que lado.
 *
 * Convenções: times são 0 (casa) e 1 (visitante); jogadores dentro do time são 0 e 1.
 * Ordem de saque: os times alternam a cada game; dentro do time, os jogadores alternam a
 * cada vez que o time volta a sacar. No tie-break quem abriu saca 1 ponto, depois 2 a 2. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});

  const NOMES_DOS_PONTOS = ['0', '15', '30', '40'];

  class Placar {
    constructor({ pontoDeOuro = true, setsParaVencer = 1, timeQueSaca = 0 } = {}) {
      this.pontoDeOuro = pontoDeOuro;
      this.setsParaVencer = setsParaVencer;
      this.pontos = [0, 0];
      this.games = [0, 0];
      this.sets = [0, 0];
      this.setsAnteriores = [];      // [{ games: [6, 4], tieBreak: null | [7, 5] }]
      this.emTieBreak = false;
      this.vencedor = null;
      this.sacadorDoGame = timeQueSaca;   // time que abriu o game (no tie-break: quem abriu o tie-break)
      this.proximoJogador = [0, 0];       // por time: quem saca da próxima vez que o time sacar
      this.jogadorSacador = [0, 0];       // por time: quem está sacando no turno atual
      this.turnoDeSaque = null;           // time sacando neste instante
      this._abrirTurnoDeSaque(timeQueSaca);
    }

    _abrirTurnoDeSaque(time) {
      this.turnoDeSaque = time;
      this.jogadorSacador[time] = this.proximoJogador[time];
      this.proximoJogador[time] ^= 1;
    }

    get acabou() { return this.vencedor !== null; }
    get sacador() { return { time: this.turnoDeSaque, jogador: this.jogadorSacador[this.turnoDeSaque] }; }
    get totalDePontosNoGame() { return this.pontos[0] + this.pontos[1]; }
    // Saque do lado direito quando a soma dos pontos do game é par (vale no tie-break também).
    get ladoDoSaque() { return this.totalDePontosNoGame % 2 === 0 ? 'direita' : 'esquerda'; }
    get emPontoDecisivo() {
      if (this.emTieBreak) return false;
      return this.pontoDeOuro && this.pontos[0] === 3 && this.pontos[1] === 3;
    }

    textoDosPontos(time) {
      if (this.emTieBreak) return String(this.pontos[time]);
      const a = this.pontos[time], b = this.pontos[1 - time];
      if (a >= 3 && b >= 3) return a > b ? 'AD' : '40';
      return NOMES_DOS_PONTOS[Math.min(a, 3)];
    }

    pontoPara(time) {
      if (this.acabou) return { tipo: 'encerrada', time: this.vencedor };
      if (time !== 0 && time !== 1) throw new Error('time inválido: ' + time);
      this.pontos[time] += 1;
      const a = this.pontos[time], b = this.pontos[1 - time];
      if (this.emTieBreak) {
        if (a >= 7 && a - b >= 2) return this._fecharGame(time);
        this._rodarSaqueNoTieBreak();
        return { tipo: 'ponto', time };
      }
      const fechou = this.pontoDeOuro ? (a >= 4 && a > b) : (a >= 4 && a - b >= 2);
      if (fechou) return this._fecharGame(time);
      return { tipo: 'ponto', time };
    }

    _rodarSaqueNoTieBreak() {
      const n = this.totalDePontosNoGame;
      const turno = Math.floor((n + 1) / 2) % 2 === 0 ? this.sacadorDoGame : 1 - this.sacadorDoGame;
      if (turno !== this.turnoDeSaque) this._abrirTurnoDeSaque(turno);
    }

    _fecharGame(time) {
      const eraTieBreak = this.emTieBreak;
      const pontosDoTieBreak = eraTieBreak ? [...this.pontos] : null;
      this.games[time] += 1;
      this.pontos = [0, 0];
      this.emTieBreak = false;
      const g = this.games[time], h = this.games[1 - time];
      let evento;
      if (eraTieBreak || (g >= 6 && g - h >= 2)) {
        evento = this._fecharSet(time, pontosDoTieBreak);
      } else {
        if (g === 6 && h === 6) this.emTieBreak = true;
        evento = { tipo: 'game', time };
      }
      // Próximo game: o outro time saca. Depois do tie-break, saca quem NÃO abriu o tie-break.
      this.sacadorDoGame = 1 - this.sacadorDoGame;
      if (!this.acabou) this._abrirTurnoDeSaque(this.sacadorDoGame);
      return evento;
    }

    _fecharSet(time, pontosDoTieBreak) {
      this.sets[time] += 1;
      this.setsAnteriores.push({ games: [...this.games], tieBreak: pontosDoTieBreak });
      this.games = [0, 0];
      if (this.sets[time] >= this.setsParaVencer) {
        this.vencedor = time;
        return { tipo: 'partida', time };
      }
      return { tipo: 'set', time };
    }

    // "6-4 3-6 7-6(5)" do ponto de vista do time 0; com o set em andamento no fim.
    resumo() {
      const partes = this.setsAnteriores.map((s) => {
        const texto = `${s.games[0]}-${s.games[1]}`;
        if (!s.tieBreak) return texto;
        return `${texto}(${Math.min(s.tieBreak[0], s.tieBreak[1])})`;
      });
      if (!this.acabou) partes.push(`${this.games[0]}-${this.games[1]}`);
      return partes.join(' ');
    }
  }

  const Regras = Object.freeze({ Placar });
  Padel.Regras = Regras;
  if (typeof module !== 'undefined' && module.exports) module.exports = Regras;
})();
