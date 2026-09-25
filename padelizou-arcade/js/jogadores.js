/* Jogadores e a inteligência artificial. Um `Jogador` é posição, alcance e velocidade.
 * A `IA` controla os jogadores de um time que não são o humano: quando a bola muda de
 * trajetória ela simula a física pra frente (mesma `Bola`, mesmo passo) e decide quem vai
 * buscar e onde; o outro cobre. Na hora de bater escolhe o alvo — o buraco entre os
 * adversários, lob quando eles estão na rede, smash em bola alta — com erro por dificuldade. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});
  const Quadra = Padel.Quadra || (typeof require === 'function' ? require('./quadra.js') : null);
  const Util = Padel.Util || (typeof require === 'function' ? require('./util.js') : null);
  const { limitar, gaussiana } = Util;

  const PERFIS = Object.freeze({
    facil:    { velocidade: 4.2, reacao: 0.45, erroDeMira: 1.5, erroDePrevisao: 1.1, chanceDeErro: 0.14, chanceDeLob: 0.2 },
    medio:    { velocidade: 5.2, reacao: 0.25, erroDeMira: 0.9, erroDePrevisao: 0.7, chanceDeErro: 0.07, chanceDeLob: 0.3 },
    dificil:  { velocidade: 6.2, reacao: 0.10, erroDeMira: 0.5, erroDePrevisao: 0.35, chanceDeErro: 0.025, chanceDeLob: 0.35 },
    // O parceiro do humano: confiável sem ser um muro.
    parceiro: { velocidade: 5.4, reacao: 0.18, erroDeMira: 0.8, erroDePrevisao: 0.55, chanceDeErro: 0.04, chanceDeLob: 0.3 },
  });

  // 0 = golpe confortável; cresce quando o jogador está esticado, a bola está rente ao chão ou rápida.
  function dificuldadeDoGolpe(jogador, bola) {
    const esticado = jogador.distanciaAte(bola.x, bola.y) / jogador.alcance;
    return limitar(esticado * 0.8 + (bola.z < 0.3 ? 0.4 : 0) + (bola.velocidade > 18 ? 0.4 : 0), 0, 1.6);
  }

  class Jogador {
    constructor({ time, indice, nome, humano = false, velocidade = 5.5 }) {
      this.time = time;
      this.indice = indice;      // 0 = metade direita do time, 1 = esquerda
      this.nome = nome;
      this.humano = humano;
      this.velocidade = velocidade;
      this.lado = Quadra.ladoDoTime(time);
      this.x = this.xDaMetade;
      this.y = this.lado * 6;
      this.alcance = humano ? 1.35 : 1.1;   // braço + raquete; o humano ganha folga de propósito
      this.alturaMaxima = 2.7;
      this.cooldown = 0;
      this.golpes = 0;
    }

    get xDaMetade() { return this.lado * (this.indice === 0 ? 2.5 : -2.5); }

    _limitar() {
      this.x = limitar(this.x, -4.7, 4.7);
      this.y = this.lado > 0 ? limitar(this.y, 0.5, 9.7) : limitar(this.y, -9.7, -0.5);
    }

    mover(dx, dy, dt) {
      const n = Math.hypot(dx, dy);
      if (n < 1e-6) return;
      const forca = Math.min(1, n);
      this.x += (dx / n) * forca * this.velocidade * dt;
      this.y += (dy / n) * forca * this.velocidade * dt;
      this._limitar();
    }

    irPara(x, y, dt) {
      const dx = x - this.x, dy = y - this.y;
      const d = Math.hypot(dx, dy);
      if (d < 0.03) return true;
      const passo = Math.min(d, this.velocidade * dt);
      this.x += (dx / d) * passo;
      this.y += (dy / d) * passo;
      this._limitar();
      return d - passo < 0.03;
    }

    distanciaAte(x, y) { return Math.hypot(x - this.x, y - this.y); }

    alcanca(bola) {
      return this.distanciaAte(bola.x, bola.y) <= this.alcance && bola.z >= 0 && bola.z <= this.alturaMaxima;
    }

    avancarTempo(dt) { this.cooldown = Math.max(0, this.cooldown - dt); }
  }

  class IA {
    constructor({ time, perfil, aleatorio }) {
      this.time = time;
      this.perfil = typeof perfil === 'string' ? PERFIS[perfil] : perfil;
      if (!this.perfil) throw new Error('perfil de IA desconhecido: ' + perfil);
      this.aleatorio = aleatorio;
      this.plano = null;
      this.relogio = 0;
      this.posicao = 'fundo';   // 'rede' ou 'fundo': onde o time se posiciona quando não está buscando a bola
    }

    // Ganchos da partida.
    aoSacar(partida, timeSacador) {
      this.posicao = timeSacador === this.time ? 'rede' : 'fundo';   // quem saca sobe; quem recebe fica atrás
      this.planejar(partida, true);
    }

    aoGolpear(partida, golpe, jogador) {
      if (jogador.time === this.time) {
        const naFrente = Math.abs(jogador.y) < 5.5;
        this.posicao = golpe.tipo === 'lob' || golpe.tipo === 'smash' || naFrente ? 'rede' : 'fundo';
      }
      this.planejar(partida, true);
    }

    // Bola quicou ou bateu na parede: releitura da trajetória, sem novo tempo de reação.
    aoRebater(partida) {
      if (this.plano) this.planejar(partida, false);
    }

    /* Simula a bola pra frente e decide quem vai buscar e onde. `novaTrajetoria` marca uma
     * leitura nova (saque/golpe), que custa tempo de reação; a releitura num quique não. */
    planejar(partida, novaTrajetoria) {
      const { bola, arbitro } = partida;
      const planoAnterior = this.plano;
      this.plano = null;
      if (!bola.emJogo || arbitro.golpeador === this.time) return;
      const candidatos = this._prever(bola, arbitro);
      if (candidatos.length === 0) return;
      const meus = partida.jogadoresDoTime(this.time);

      let escolha = null;
      for (const c of candidatos) {
        let melhor = null, melhorFolga = -Infinity;
        for (const j of meus) {
          const distancia = j.distanciaAte(c.x, c.y);
          if (c.voleio && !(Math.abs(j.y) < 5 && distancia < 2.5)) continue;
          const folga = c.t - (distancia / j.velocidade + (j.humano ? 0 : this.perfil.reacao));
          if (folga >= 0 && folga > melhorFolga) { melhorFolga = folga; melhor = j; }
        }
        if (melhor) { escolha = { jogador: melhor, alvo: c }; break; }
      }
      if (!escolha) {
        // Ninguém chega a tempo: o mais perto do último ponto possível tenta mesmo assim.
        const c = candidatos[candidatos.length - 1];
        let melhor = meus[0], menor = Infinity;
        for (const j of meus) { const d = j.distanciaAte(c.x, c.y); if (d < menor) { menor = d; melhor = j; } }
        escolha = { jogador: melhor, alvo: c };
      }
      // Erro de leitura: maior quanto mais longe (no tempo) a bola está; a releitura corrige.
      const alvo = escolha.alvo;
      const sigma = this.perfil.erroDePrevisao * Math.min(1, alvo.t / 0.8);
      const meuLado = Quadra.ladoDoTime(this.time);
      const alvoLido = {
        x: limitar(alvo.x + gaussiana(this.aleatorio) * sigma, -4.7, 4.7),
        y: meuLado * limitar(Math.abs(alvo.y) + gaussiana(this.aleatorio) * sigma * 0.7, 0.5, 9.7),
        t: alvo.t,
      };
      const criadoEm = novaTrajetoria || !planoAnterior ? this.relogio : planoAnterior.criadoEm;
      this.plano = { responsavel: escolha.jogador, alvo: alvoLido, criadoEm };
    }

    // Pontos (x, y, z, t) em que a bola estará batível no nosso lado, em ordem de tempo.
    _prever(bola, arbitro) {
      const clone = bola.clonar();
      const meuLado = Quadra.ladoDoTime(this.time);
      const passo = 1 / 60;
      const candidatos = [];
      let quiques = 0, t = 0, acabou = false;
      for (let i = 0; i < 240 && clone.emJogo && !clone.parada && !acabou; i++) {
        const eventos = clone.avancar(passo, []);
        t += passo;
        for (const e of eventos) {
          if (e.tipo === 'quique') {
            if (e.lado === meuLado) quiques += 1; else acabou = true;
            if (quiques >= 2) acabou = true;
          }
        }
        if (acabou || quiques >= 2) break;
        if (Quadra.ladoDe(clone.y) !== meuLado) continue;
        if (quiques === 0 && arbitro.emSaque) continue;
        if (clone.rolando) break;
        if (clone.z < 0.35 || clone.z > 2.3) continue;
        if (quiques > 0 && clone.vz > 0 && clone.z < 0.9) continue; // subindo logo depois do quique: espera
        candidatos.push({ x: clone.x, y: clone.y, z: clone.z, t, voleio: quiques === 0 });
        if (candidatos.length > 150) break;
      }
      return candidatos;
    }

    reagir(dt, partida) {
      this.relogio += dt;
      for (const j of partida.jogadoresDoTime(this.time)) {
        if (j.humano) continue;
        const plano = this.plano;
        if (plano && plano.responsavel === j) {
          if (this.relogio - plano.criadoEm < this.perfil.reacao) continue;
          j.irPara(plano.alvo.x, plano.alvo.y, dt);
        } else {
          const casa = this._posicaoDeFormacao(j);
          j.irPara(casa.x, casa.y, dt);
        }
      }
    }

    _posicaoDeFormacao(j) {
      const profundidade = this.posicao === 'rede' ? 3.2 : 6.5;
      return { x: j.xDaMetade, y: j.lado * profundidade };
    }

    escolherGolpe(jogador, bola, partida) {
      const r = this.aleatorio;
      const ladoDoAlvo = -jogador.lado;
      const adversarios = partida.jogadoresDoTime(1 - this.time);
      const dificuldade = dificuldadeDoGolpe(jogador, bola);

      if (r() < this.perfil.chanceDeErro * (1 + 2.5 * dificuldade)) {
        return r() < 0.5
          ? { alvo: { x: (r() - 0.5) * 6, y: ladoDoAlvo * 0.3 }, tempoDeVoo: 0.45, ignorarRede: true, tipo: 'erro' }
          : { alvo: { x: (r() - 0.5) * 6, y: ladoDoAlvo * 11.5 }, tempoDeVoo: 0.55, ignorarRede: true, tipo: 'erro' };
      }
      if (bola.z > 1.6 && Math.abs(jogador.y) < 6) {
        return { alvo: { x: (r() - 0.5) * 7, y: ladoDoAlvo * (3 + r() * 3) }, tempoDeVoo: 0.42, tipo: 'smash' };
      }
      const adversariosNaRede = adversarios.filter((a) => Math.abs(a.y) < 4.5).length;
      if (adversariosNaRede >= 1 && r() < this.perfil.chanceDeLob) {
        return { alvo: { x: (r() - 0.5) * 5, y: ladoDoAlvo * 8.2 }, tempoDeVoo: 1.7, tipo: 'lob' };
      }
      const opcoes = [-3.3, 0, 3.3].map((x) => ({ x, y: ladoDoAlvo * 6.8 }));
      let melhor = opcoes[0], melhorDistancia = -1;
      for (const o of opcoes) {
        const d = Math.min(...adversarios.map((a) => a.distanciaAte(o.x, o.y)));
        if (d > melhorDistancia) { melhorDistancia = d; melhor = o; }
      }
      const ruido = this.perfil.erroDeMira * (1 + 1.2 * dificuldade);
      const x = limitar(melhor.x + gaussiana(r) * ruido, -4.5, 4.5);
      const y = ladoDoAlvo * limitar(Math.abs(melhor.y + gaussiana(r) * ruido * 0.6), 2, 9.4);
      return { alvo: { x, y }, tempoDeVoo: 0.85, tipo: 'normal' };
    }
  }

  const Jogadores = Object.freeze({ Jogador, IA, PERFIS, dificuldadeDoGolpe });
  Padel.Jogadores = Jogadores;
  if (typeof module !== 'undefined' && module.exports) module.exports = Jogadores;
})();
