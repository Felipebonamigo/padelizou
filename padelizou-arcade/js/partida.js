/* A partida: junta placar, árbitro, bola, jogadores e IA numa máquina de estados
 * (saque → rally → fimDoPonto → saque… → fim). Não desenha nem lê teclado: recebe a
 * entrada do humano por parâmetro e avisa os ouvintes do que aconteceu. Por isso roda igual
 * no browser e no `node --test`. */
(function () {
  'use strict';
  const raiz = typeof window !== 'undefined' ? window : globalThis;
  const Padel = raiz.Padel || (raiz.Padel = {});
  const temRequire = typeof require === 'function';
  const Quadra = Padel.Quadra || (temRequire ? require('./quadra.js') : null);
  const Util = Padel.Util || (temRequire ? require('./util.js') : null);
  const Regras = Padel.Regras || (temRequire ? require('./regras.js') : null);
  const Fisica = Padel.Fisica || (temRequire ? require('./fisica.js') : null);
  const Arbitro = Padel.Arbitro || (temRequire ? require('./arbitro.js') : null);
  const Jogadores = Padel.Jogadores || (temRequire ? require('./jogadores.js') : null);

  const { Placar } = Regras;
  const { Bola, calcularGolpe } = Fisica;
  const { Jogador, IA, PERFIS } = Jogadores;
  const { criarAleatorio, gaussiana, limitar } = Util;

  const ENTRADA_VAZIA = Object.freeze({ dx: 0, dy: 0, acaoPressionada: false, acaoSegurada: false });

  const MOTIVOS = Object.freeze({
    doisQuiques: 'dois quiques',
    rede: 'na rede',
    naoPassou: 'não passou',
    paredeSemQuicar: 'no vidro sem quicar',
    paredePropria: 'na própria parede',
    fora: 'pra fora da quadra',
    voltouPeloVidro: 'voltou pelo vidro',
    duplaFalta: 'dupla falta',
    foraDaCaixa: 'saque fora da caixa',
    bolaMorta: 'bola morta',
  });

  const ESPERA_DO_SAQUE_HUMANO = 8;   // segundos até o saque sair sozinho
  const ESPERA_DO_SAQUE_DA_IA = 1.1;

  class Partida {
    constructor(opcoes = {}) {
      const {
        dificuldade = 'medio',
        pontoDeOuro = true,
        setsParaVencer = 1,
        humano = true,
        semente = Math.floor(Math.random() * 4294967296),
      } = opcoes;
      if (!PERFIS[dificuldade]) throw new Error('dificuldade desconhecida: ' + dificuldade);

      this.dificuldade = dificuldade;
      this.aleatorio = criarAleatorio(semente);
      this.placar = new Placar({ pontoDeOuro, setsParaVencer, timeQueSaca: 0 });
      this.arbitro = new Arbitro();
      this.bola = new Bola();
      const perfilRival = PERFIS[dificuldade];
      this.jogadores = [
        new Jogador({ time: 0, indice: 0, nome: 'Você', humano, velocidade: humano ? 5.8 : PERFIS.parceiro.velocidade }),
        new Jogador({ time: 0, indice: 1, nome: 'Parceiro', velocidade: PERFIS.parceiro.velocidade }),
        new Jogador({ time: 1, indice: 0, nome: 'Rival 1', velocidade: perfilRival.velocidade }),
        new Jogador({ time: 1, indice: 1, nome: 'Rival 2', velocidade: perfilRival.velocidade }),
      ];
      this.ias = [
        new IA({ time: 0, perfil: PERFIS.parceiro, aleatorio: this.aleatorio }),
        new IA({ time: 1, perfil: perfilRival, aleatorio: this.aleatorio }),
      ];
      this.ouvintes = [];
      this.estado = 'saque';
      this.temporizador = 0;
      this.mensagem = null;
      this.ultimoGolpe = null;
      this.rolandoHa = 0;
      this.tempoDeJogo = 0;
      this.rallyAtual = 0;
      this.estatisticas = { pontos: 0, golpes: 0, maiorRally: 0, motivos: {}, faltas: 0, lets: 0 };
      this.iniciarPonto();
    }

    ouvir(fn) { this.ouvintes.push(fn); return this; }
    _emitir(tipo, dados) { for (const fn of this.ouvintes) fn(tipo, dados || {}, this); }

    jogadoresDoTime(time) { return time === 0 ? [this.jogadores[0], this.jogadores[1]] : [this.jogadores[2], this.jogadores[3]]; }
    get humano() { return this.jogadores[0]; }
    get sacador() {
      const { time, jogador } = this.placar.sacador;
      return this.jogadores[time * 2 + jogador];
    }
    get acabou() { return this.estado === 'fim'; }

    iniciarPonto() {
      const sacador = this.sacador;
      const lado = sacador.lado;
      const direita = this.placar.ladoDoSaque === 'direita';
      const sinalDoSacador = direita ? lado : -lado;     // metade em que o sacador está
      const timeReceptor = 1 - sacador.time;
      const [receptor, parceiroDoReceptor] = this._quemRecebe(timeReceptor, direita);
      const parceiroDoSacador = this.jogadoresDoTime(sacador.time).find((j) => j !== sacador);

      sacador.x = sinalDoSacador * 2.6;
      sacador.y = lado * 8.3;
      parceiroDoSacador.x = -sinalDoSacador * 2.3;
      parceiroDoSacador.y = lado * 3.6;
      receptor.x = -sinalDoSacador * 2.8;
      receptor.y = -lado * 8.0;
      parceiroDoReceptor.x = sinalDoSacador * 2.3;
      parceiroDoReceptor.y = -lado * 4.2;
      for (const j of this.jogadores) j.cooldown = 0;

      this.bola.reiniciar();
      this.bola.posicionar(sacador.x, sacador.y, 0.8);
      this.caixaDoSaque = Quadra.caixaDeSaque(-lado, direita);
      this.rallyAtual = 0;
      this.rolandoHa = 0;
      this.ultimoGolpe = null;
      this.estado = 'saque';
      this.temporizador = sacador.humano ? ESPERA_DO_SAQUE_HUMANO : ESPERA_DO_SAQUE_DA_IA;
      const segundo = this.arbitro.faltas > 0;
      this.mensagem = sacador.humano
        ? { texto: segundo ? 'Segundo saque — Espaço ou SACAR' : 'Seu saque — Espaço ou SACAR', suave: true }
        : null;
      this._emitir('saquePreparado', { sacador, segundo });
    }

    // Quem recebe é o jogador da metade diagonal ao sacador; o parceiro dele fica na rede.
    _quemRecebe(timeReceptor, direita) {
      const [a, b] = this.jogadoresDoTime(timeReceptor);
      const receptor = direita ? a : b;      // indice 0 = metade direita do time = diagonal do saque à direita
      return [receptor, receptor === a ? b : a];
    }

    sacar(entrada = ENTRADA_VAZIA) {
      const sacador = this.sacador;
      const caixa = this.caixaDoSaque;
      const centro = { x: (caixa.xMin + caixa.xMax) / 2, y: (caixa.yMin + caixa.yMax) / 2 };
      let alvo;
      if (sacador.humano) {
        alvo = { x: limitar(centro.x + entrada.dx * 1.8 + gaussiana(this.aleatorio) * 0.35, caixa.xMin + 0.3, caixa.xMax - 0.3), y: centro.y - sacador.lado * 0.5 };
      } else {
        const ruido = this.ias[sacador.time].perfil.erroDeMira * 0.55;
        alvo = { x: centro.x + gaussiana(this.aleatorio) * ruido, y: centro.y + gaussiana(this.aleatorio) * ruido };
      }
      const v = calcularGolpe({ x: this.bola.x, y: this.bola.y, z: this.bola.z }, alvo, { tempoDeVoo: 1.0 });
      this.bola.lancar(v);
      this.arbitro.iniciarSaque(sacador.time, caixa);
      sacador.cooldown = 0.5;
      sacador.golpes += 1;
      this.ultimoGolpe = { jogador: sacador, tipo: 'saque', em: this.tempoDeJogo };
      this.estado = 'rally';
      this.mensagem = null;
      this.rallyAtual = 1;
      this.estatisticas.golpes += 1;
      for (const ia of this.ias) ia.aoSacar(this, sacador.time);
      this._emitir('golpe', { jogador: sacador, tipo: 'saque' });
    }

    avancar(dt, entrada = ENTRADA_VAZIA) {
      if (!(dt > 0)) return;
      this.tempoDeJogo += dt;
      for (const j of this.jogadores) j.avancarTempo(dt);
      switch (this.estado) {
        case 'saque': {
          this.temporizador -= dt;
          const humanoSaca = this.sacador.humano;
          if ((humanoSaca && entrada.acaoPressionada) || this.temporizador <= 0) this.sacar(entrada);
          break;
        }
        case 'rally':
          this._rally(dt, entrada);
          break;
        case 'fimDoPonto':
          this.temporizador -= dt;
          if (this.temporizador <= 0) {
            if (this.placar.acabou) { this.estado = 'fim'; this._emitir('fim', { vencedor: this.placar.vencedor }); }
            else this.iniciarPonto();
          }
          break;
        default:
          break;
      }
    }

    _rally(dt, entrada) {
      const humano = this.jogadores[0];
      if (humano.humano) humano.mover(entrada.dx, entrada.dy, dt);
      for (const ia of this.ias) ia.reagir(dt, this);

      const bola = this.bola;
      if (bola.emJogo && !bola.rolando) {
        for (const j of this.jogadores) {
          if (j.cooldown > 0 || !this.arbitro.podeGolpear(j.time) || !j.alcanca(bola)) continue;
          this._golpear(j, entrada);
          break;
        }
      }

      const eventos = bola.avancar(dt, []);
      let rebateu = false;
      for (const evento of eventos) {
        this._emitir(evento.tipo, evento);
        const decisao = this.arbitro.processar(evento);
        if (decisao) { this._decidir(decisao); return; }
        if (evento.tipo === 'quique' || evento.tipo === 'parede') rebateu = true;
      }
      if (rebateu) for (const ia of this.ias) ia.aoRebater(this);

      if (bola.rolando) {
        this.rolandoHa += dt;
        if (this.rolandoHa > 0.6) {
          // Bola rolando é bola que já quicou e não vai quicar de novo: conta como o quique seguinte.
          const decisao = this.arbitro.processar({ tipo: 'quique', x: bola.x, y: bola.y, lado: Quadra.ladoDe(bola.y) })
            || { tipo: 'ponto', para: this.arbitro.receptor, motivo: 'bolaMorta' };
          this._decidir(decisao);
        }
      } else {
        this.rolandoHa = 0;
      }
      if (this.estado === 'rally' && !bola.emJogo) {
        this._decidir({ tipo: 'ponto', para: this.arbitro.receptor, motivo: 'fora' });
      }
    }

    _golpear(jogador, entrada) {
      const bola = this.bola;
      const golpe = jogador.humano
        ? this._golpeDoHumano(jogador, entrada)
        : this.ias[jogador.time].escolherGolpe(jogador, bola, this);
      const v = calcularGolpe({ x: bola.x, y: bola.y, z: bola.z }, golpe.alvo, {
        tempoDeVoo: golpe.tempoDeVoo,
        ignorarRede: Boolean(golpe.ignorarRede),
      });
      bola.lancar(v);
      jogador.cooldown = 0.4;
      jogador.golpes += 1;
      this.arbitro.registrarGolpe(jogador.time);
      this.ultimoGolpe = { jogador, tipo: golpe.tipo, em: this.tempoDeJogo };
      this.rallyAtual += 1;
      this.estatisticas.golpes += 1;
      for (const ia of this.ias) ia.aoGolpear(this, golpe, jogador);
      this._emitir('golpe', { jogador, tipo: golpe.tipo });
    }

    /* Mira do humano: esquerda/direita escolhem o canto; pra cima (rumo à rede) encurta e
     * acelera; pra baixo joga fundo e seguro; a ação segurada vira lob; bola alta é smash. */
    _golpeDoHumano(jogador, entrada) {
      const bola = this.bola;
      const ladoDoAlvo = -jogador.lado;
      const sigma = 0.3 + 0.7 * Jogadores.dificuldadeDoGolpe(jogador, bola);
      const ruido = () => gaussiana(this.aleatorio) * sigma;
      let x = entrada.dx < -0.3 ? -3.3 : entrada.dx > 0.3 ? 3.3 : jogador.x * 0.4;
      x = limitar(x + ruido(), -4.5, 4.5);
      if (entrada.acaoSegurada) return { alvo: { x, y: ladoDoAlvo * 8.2 }, tempoDeVoo: 1.7, tipo: 'lob' };
      if (bola.z > 1.7) return { alvo: { x, y: ladoDoAlvo * 4.2 }, tempoDeVoo: 0.42, tipo: 'smash' };
      // dy < 0 é rumo à rede pro time da casa (que joga em y > 0)
      const ataque = entrada.dy * jogador.lado < -0.3;
      const defesa = entrada.dy * jogador.lado > 0.3;
      if (ataque) return { alvo: { x, y: ladoDoAlvo * (4.5 + ruido()) }, tempoDeVoo: 0.6, tipo: 'ataque' };
      if (defesa) return { alvo: { x, y: ladoDoAlvo * (7.8 + ruido()) }, tempoDeVoo: 1.05, tipo: 'defesa' };
      return { alvo: { x, y: ladoDoAlvo * (6.6 + ruido()) }, tempoDeVoo: 0.8, tipo: 'normal' };
    }

    _decidir(decisao) {
      if (decisao.tipo === 'ponto') { this._encerrarPonto(decisao.para, decisao.motivo); return; }
      this.bola.emJogo = false;
      this.estado = 'fimDoPonto';
      this.temporizador = 1.3;
      if (decisao.tipo === 'falta') {
        this.estatisticas.faltas += 1;
        this.mensagem = { texto: `Falta — ${MOTIVOS[decisao.motivo] || decisao.motivo}. Segundo saque` };
        this._emitir('falta', decisao);
      } else {
        this.estatisticas.lets += 1;
        this.mensagem = { texto: 'Let — a bola tocou a rede. Repete o saque' };
        this._emitir('let', decisao);
      }
    }

    _encerrarPonto(para, motivo) {
      const evento = this.placar.pontoPara(para);
      this.estatisticas.pontos += 1;
      this.estatisticas.motivos[motivo] = (this.estatisticas.motivos[motivo] || 0) + 1;
      this.estatisticas.maiorRally = Math.max(this.estatisticas.maiorRally, this.rallyAtual);
      this.bola.emJogo = false;
      this.arbitro.novoPonto();
      this.mensagem = { texto: this._textoDoPonto(para, motivo, evento), destaque: evento.tipo !== 'ponto' };
      this.estado = 'fimDoPonto';
      this.temporizador = evento.tipo === 'ponto' ? 1.5 : 2.4;
      this._emitir(evento.tipo, { time: para, motivo, evento });
    }

    _textoDoPonto(para, motivo, evento) {
      const descricao = MOTIVOS[motivo] || motivo;
      const seu = para === 0;
      switch (evento.tipo) {
        case 'game': return `${seu ? 'Game seu' : 'Game deles'} — ${descricao}`;
        case 'set': return `${seu ? 'Set seu!' : 'Set deles.'} ${this.placar.resumo()}`;
        case 'partida': return seu ? `Você venceu! ${this.placar.resumo()}` : `Perdeu. ${this.placar.resumo()}`;
        default: return `${seu ? 'Ponto seu' : 'Ponto deles'} — ${descricao}`;
      }
    }
  }

  Padel.Partida = Partida;
  Padel.MOTIVOS = MOTIVOS;
  if (typeof module !== 'undefined' && module.exports) module.exports = Partida;
})();
