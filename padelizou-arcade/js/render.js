/* Desenho da quadra vista de cima, com altura falsa: a sombra fica no chão e a bola sobe na
 * tela (e cresce) conforme o z. Só lê a partida; não muda nada nela. */
(function () {
  'use strict';
  const Padel = window.Padel;
  const { Quadra } = Padel;

  const ESCALA = 30;        // px por metro no chão
  const MARGEM = 26;        // faixa fora da quadra, onde ficam as paredes
  const ALTURA_PX = 22;     // px por metro de altura da bola
  const LARGURA = Quadra.LARGURA * ESCALA + 2 * MARGEM;
  const ALTURA = Quadra.COMPRIMENTO * ESCALA + 2 * MARGEM;

  const CORES = Object.freeze({
    fundo: '#0f1730',
    piso: '#2a63c4',
    pisoFaixa: '#2d6bd1',
    linha: 'rgba(255,255,255,0.92)',
    vidro: 'rgba(150, 205, 255, 0.22)',
    vidroBorda: 'rgba(205, 232, 255, 0.85)',
    grade: 'rgba(255,255,255,0.22)',
    rede: '#0b1224',
    redeMalha: 'rgba(255,255,255,0.35)',
    casa: '#A3D827',
    casaBorda: '#1C2742',
    visitante: '#ff6a3d',
    visitanteBorda: '#4a1508',
    bola: '#f2ff4d',
    bolaBorda: '#8a9400',
    sombra: 'rgba(0,0,0,0.38)',
    destaque: '#ffffff',
    caixa: 'rgba(163, 216, 39, 0.9)',
  });

  class Renderizador {
    constructor(canvas) {
      this.canvas = canvas;
      this.ctx = canvas.getContext('2d');
      this.dpr = 1;
      this.redimensionar();
    }

    static get LARGURA() { return LARGURA; }
    static get ALTURA() { return ALTURA; }

    // Resolução interna pelo devicePixelRatio; tamanho na tela é a maior escala que cabe no pai,
    // mantendo a proporção (CSS com aspect-ratio + max-width esticava a quadra no celular).
    redimensionar() {
      this.dpr = Math.min(window.devicePixelRatio || 1, 3);
      this.canvas.width = Math.round(LARGURA * this.dpr);
      this.canvas.height = Math.round(ALTURA * this.dpr);
      const pai = this.canvas.parentElement;
      if (!pai) return;
      const escala = Math.max(0.1, Math.min(pai.clientWidth / LARGURA, pai.clientHeight / ALTURA));
      this.canvas.style.width = `${Math.floor(LARGURA * escala)}px`;
      this.canvas.style.height = `${Math.floor(ALTURA * escala)}px`;
    }

    px(x) { return MARGEM + (x + Quadra.MEIA_LARGURA) * ESCALA; }
    py(y) { return MARGEM + (y + Quadra.MEIO_COMPRIMENTO) * ESCALA; }

    desenhar(partida) {
      const ctx = this.ctx;
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      this._quadra(ctx);
      if (partida.estado === 'saque' && partida.caixaDoSaque) this._caixaDeSaque(ctx, partida.caixaDoSaque);
      this._rede(ctx);
      this._sombraDaBola(ctx, partida.bola);
      const ordenados = [...partida.jogadores].sort((a, b) => a.y - b.y);
      for (const j of ordenados) this._jogador(ctx, j);
      this._bola(ctx, partida.bola);
    }

    _quadra(ctx) {
      ctx.fillStyle = CORES.fundo;
      ctx.fillRect(0, 0, LARGURA, ALTURA);

      // Paredes: vidro em volta, grade (1 m acima do vidro) só nos fundos.
      const esp = 14;
      ctx.fillStyle = CORES.vidro;
      ctx.fillRect(MARGEM - esp, MARGEM - esp, LARGURA - 2 * (MARGEM - esp), ALTURA - 2 * (MARGEM - esp));
      ctx.strokeStyle = CORES.vidroBorda;
      ctx.lineWidth = 2;
      ctx.strokeRect(MARGEM - esp + 1, MARGEM - esp + 1, LARGURA - 2 * (MARGEM - esp) - 2, ALTURA - 2 * (MARGEM - esp) - 2);
      ctx.strokeStyle = CORES.grade;
      ctx.lineWidth = 1;
      ctx.beginPath();
      for (let x = MARGEM - esp; x < LARGURA - MARGEM + esp; x += 6) {
        ctx.moveTo(x, MARGEM - esp); ctx.lineTo(x + 6, MARGEM);
        ctx.moveTo(x, ALTURA - MARGEM + esp); ctx.lineTo(x + 6, ALTURA - MARGEM);
      }
      ctx.stroke();

      // Piso.
      ctx.fillStyle = CORES.piso;
      ctx.fillRect(this.px(-5), this.py(-10), Quadra.LARGURA * ESCALA, Quadra.COMPRIMENTO * ESCALA);
      ctx.fillStyle = CORES.pisoFaixa;
      ctx.fillRect(this.px(-5), this.py(-Quadra.LINHA_DE_SAQUE), Quadra.LARGURA * ESCALA, 2 * Quadra.LINHA_DE_SAQUE * ESCALA);

      // Linhas: saque (as duas), linha central de saque até o fundo.
      ctx.strokeStyle = CORES.linha;
      ctx.lineWidth = 2;
      ctx.beginPath();
      for (const s of [-1, 1]) {
        const y = this.py(s * Quadra.LINHA_DE_SAQUE);
        ctx.moveTo(this.px(-5), y); ctx.lineTo(this.px(5), y);
        ctx.moveTo(this.px(0), y); ctx.lineTo(this.px(0), this.py(s * Quadra.MEIO_COMPRIMENTO));
      }
      ctx.stroke();
    }

    _caixaDeSaque(ctx, caixa) {
      ctx.save();
      ctx.strokeStyle = CORES.caixa;
      ctx.setLineDash([6, 5]);
      ctx.lineWidth = 2;
      ctx.strokeRect(this.px(caixa.xMin) + 2, this.py(caixa.yMin) + 2, (caixa.xMax - caixa.xMin) * ESCALA - 4, (caixa.yMax - caixa.yMin) * ESCALA - 4);
      ctx.restore();
    }

    _rede(ctx) {
      const y = this.py(0);
      ctx.fillStyle = 'rgba(0,0,0,0.25)';
      ctx.fillRect(this.px(-5), y + 2, Quadra.LARGURA * ESCALA, 5);   // sombra da rede no piso
      ctx.fillStyle = CORES.rede;
      ctx.fillRect(this.px(-5) - 4, y - 4, Quadra.LARGURA * ESCALA + 8, 7);
      ctx.strokeStyle = CORES.redeMalha;
      ctx.lineWidth = 1;
      ctx.beginPath();
      for (let x = this.px(-5); x < this.px(5); x += 5) { ctx.moveTo(x, y - 4); ctx.lineTo(x, y + 3); }
      ctx.stroke();
      ctx.strokeStyle = CORES.linha;
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(this.px(-5) - 4, y - 4); ctx.lineTo(this.px(5) + 4, y - 4); ctx.stroke();
      ctx.fillStyle = CORES.destaque;
      for (const x of [this.px(-5) - 5, this.px(5) + 5]) { ctx.beginPath(); ctx.arc(x, y - 1, 3.5, 0, Math.PI * 2); ctx.fill(); }
    }

    _sombraDaBola(ctx, bola) {
      if (!bola.emJogo && bola.z <= 0) return;
      const alfa = 0.38 * Math.max(0.15, 1 - bola.z / 6);
      ctx.fillStyle = `rgba(0,0,0,${alfa.toFixed(3)})`;
      ctx.beginPath();
      ctx.ellipse(this.px(bola.x), this.py(bola.y), 4.5 + bola.z * 0.5, 3 + bola.z * 0.3, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    _jogador(ctx, j) {
      const x = this.px(j.x), y = this.py(j.y);
      const casa = j.time === 0;
      ctx.fillStyle = 'rgba(0,0,0,0.3)';
      ctx.beginPath(); ctx.ellipse(x, y + 3, 11, 5, 0, 0, Math.PI * 2); ctx.fill();

      if (j.humano) {
        ctx.strokeStyle = CORES.destaque;
        ctx.lineWidth = 2.5;
        ctx.beginPath(); ctx.arc(x, y, 15, 0, Math.PI * 2); ctx.stroke();
      }
      ctx.fillStyle = casa ? CORES.casa : CORES.visitante;
      ctx.strokeStyle = casa ? CORES.casaBorda : CORES.visitanteBorda;
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(x, y, 10, 0, Math.PI * 2); ctx.fill(); ctx.stroke();

      // Raquete: um traço apontando pra rede.
      const paraARede = -j.lado;
      ctx.strokeStyle = casa ? CORES.casaBorda : CORES.visitanteBorda;
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(x + 7, y); ctx.lineTo(x + 13, y + paraARede * 9); ctx.stroke();
      ctx.fillStyle = casa ? CORES.casaBorda : CORES.visitanteBorda;
      ctx.beginPath(); ctx.arc(x + 14, y + paraARede * 11, 3.5, 0, Math.PI * 2); ctx.fill();

      if (j.humano) {
        ctx.fillStyle = CORES.destaque;
        ctx.font = 'bold 10px system-ui, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('VOCÊ', x, y + 28);
      }
    }

    _bola(ctx, bola) {
      const x = this.px(bola.x), yChao = this.py(bola.y);
      const y = yChao - bola.z * ALTURA_PX;
      if (bola.z > 0.3) {
        ctx.strokeStyle = 'rgba(255,255,255,0.25)';
        ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(x, yChao); ctx.lineTo(x, y); ctx.stroke();
      }
      const raio = 4.5 + bola.z * 1.1;
      ctx.fillStyle = CORES.bola;
      ctx.strokeStyle = CORES.bolaBorda;
      ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.arc(x, y, raio, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    }
  }

  Padel.Renderizador = Renderizador;
})();
