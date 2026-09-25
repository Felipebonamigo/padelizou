// PUNHOS DE SHAOLIN — a figura: o lutador desenhado com volume, luz e pano.
//
// Segunda geração do boneco. A primeira era palito com ponta redonda; esta tem anatomia de
// sete cabeças e meia, membros afunilados com sombreamento perpendicular (lado da luz claro,
// lado oposto escuro, sombra fria e luz quente), tronco com peitoral e abdome, rosto com olho,
// sobrancelha, nariz e boca, cabelo, capuz, faixa e sash que balançam, sombra projetada no chão
// e rastro nos golpes rápidos. Continua 100% procedural — nenhuma imagem.
//
// As POSES (ângulos das juntas por estado) moram aqui também. Convenção: ângulo medido a partir
// de "apontando pra baixo": 0 pra baixo, π/2 pra frente, π pra cima. `pT`/`pD` = perna de trás /
// da frente, `bT`/`bD` = braço de trás / da frente, [junta, dobra].
(function (raiz) {
    'use strict';
    const TAU = Math.PI * 2;
    const K = 70 / 36;                       // as poses antigas mediam o quadril em -36; agora ele fica em -70
    const LUZ = { x: -0.55, y: -0.83 };      // de onde vem a luz, na tela (cima-esquerda) — as lanternas ficam no alto

    function suave(k) { k = Math.min(1, Math.max(0, k)); return k * k * (3 - 2 * k); }
    function lerp(a, b, k) { return a + (b - a) * k; }

    // ── CORES ─────────────────────────────────────────────────────────────────────────────
    const cacheTons = new Map();
    function rgb(hex) {
        const h = hex.replace('#', '');
        const n = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
        return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
    }
    const prender = v => Math.max(0, Math.min(255, Math.round(v)));
    // k < 0 escurece puxando pro azul (sombra fria); k > 0 clareia puxando pro amarelo (luz quente).
    function tom(hex, k) {
        const chave = hex + '|' + k;
        if (cacheTons.has(chave)) return cacheTons.get(chave);
        const [r, g, b] = rgb(hex);
        let cor;
        if (k < 0) { const f = 1 + k; cor = `rgb(${prender(r * f)},${prender(g * f * 0.97)},${prender(b * f * 1.08 + 6 * -k)})`; }
        else { cor = `rgb(${prender(r + (255 - r) * k * 1.1)},${prender(g + (255 - g) * k * 0.95)},${prender(b + (255 - b) * k * 0.6)})`; }
        cacheTons.set(chave, cor);
        return cor;
    }

    // ── POSES ─────────────────────────────────────────────────────────────────────────────
    function pose(p) {
        return Object.assign({ quadril: [0, -36], tronco: 0.05, cabeca: 0, pT: [-0.2, 0.2], pD: [0.25, 0.25], bT: [0.4, 1.9], bD: [0.7, 1.7], giro: 0, escalaX: 1 }, p);
    }
    const GUARDA = pose({});
    function mistura(a, b, k) {
        k = Math.min(1, Math.max(0, k));
        const r = {};
        for (const chave of Object.keys(a)) {
            const va = a[chave], vb = b[chave] == null ? va : b[chave];
            r[chave] = Array.isArray(va) ? va.map((v, i) => lerp(v, vb[i], k)) : lerp(va, vb, k);
        }
        return r;
    }
    function curvaDoGolpe(t, g) {
        if (t < g.inicio) return suave(t / Math.max(0.001, g.inicio));
        const fimAtivo = g.inicio + (g.ativo || 0.1);
        if (t <= fimAtivo) return 1;
        return 1 - suave((t - fimAtivo) / Math.max(0.001, g.total - fimAtivo));
    }
    const POSES_DE_GOLPE = {
        soco1: pose({ tronco: 0.22, bD: [1.6, 0.0], bT: [0.5, 2.0], pT: [-0.35, 0.2], pD: [0.4, 0.3] }),
        soco2: pose({ tronco: 0.3, bT: [1.62, 0.0], bD: [0.4, 2.1], pT: [-0.3, 0.2], pD: [0.45, 0.3] }),
        // Sequência de Cinco (melhoria): o 4º e o 5º golpe, variações do soco1/soco2 com mais corpo.
        soco4: pose({ tronco: 0.38, bD: [1.7, 0.1], bT: [0.4, 2.0], pT: [-0.45, 0.25], pD: [0.5, 0.35] }),
        soco5: pose({ tronco: 0.1, quadril: [0, -38], bT: [0.5, 1.9], bD: [2.2, 0.9], pT: [-0.45, 0.25], pD: [0.45, 0.45] }),
        // Especial no Ar do Long: mergulho de cabeça, punhos na frente (o do Shen usa a pancada).
        especialAereo: pose({ tronco: 1.05, quadril: [0, -34], bD: [1.9, 0.1], bT: [1.7, 0.2], pT: [-0.3, 0.6], pD: [0.2, 0.9] }),
        soco3: pose({ tronco: -0.15, quadril: [0, -40], bD: [2.6, 0.5], bT: [0.6, 1.8], pT: [-0.5, 0.3], pD: [0.3, 0.6] }),
        chute: pose({ tronco: -0.25, bD: [0.2, 1.4], bT: [1.0, 1.2], pT: [-0.15, 0.1], pD: [1.55, 0.0] }),
        chuteAereo: pose({ tronco: 0.45, bD: [-0.6, 0.6], bT: [1.2, 0.8], pT: [0.2, 1.5], pD: [1.6, 0.0] }),
        investida: pose({ tronco: 0.85, quadril: [0, -30], bD: [-0.8, 0.5], bT: [1.4, 0.6], pT: [0.3, 1.4], pD: [1.7, 0.0] }),
        especial: pose({ tronco: 0.3, bD: [1.5, 0.15], bT: [1.5, 0.25], pT: [-0.55, 0.25], pD: [0.55, 0.25] }),
        joelhada: pose({ tronco: 0.2, bD: [1.2, 1.0], bT: [1.1, 1.1], pT: [-0.2, 0.1], pD: [1.4, 1.7] }),
        pancada: pose({ tronco: 0.55, bD: [1.3, 0.2], bT: [1.3, 0.2], pT: [-0.4, 0.3], pD: [0.5, 0.5] }),
        pancadaAlta: pose({ tronco: -0.2, bD: [3.0, 0.2], bT: [3.0, 0.2], pT: [-0.3, 0.2], pD: [0.3, 0.2] }),
        flecha: pose({ tronco: 0.05, bT: [1.57, 0.0], bD: [0.9, 2.5], pT: [-0.3, 0.1], pD: [0.3, 0.1] }),
        arremesso: pose({ tronco: 0.3, bD: [1.6, 0.0], bT: [0.3, 1.5], pT: [-0.4, 0.2], pD: [0.5, 0.3] }),
        arremessoPrep: pose({ tronco: -0.2, bD: [-1.2, 1.2], bT: [0.5, 1.6], pT: [-0.3, 0.2], pD: [0.3, 0.3] }),
        chama: pose({ tronco: -0.1, quadril: [0, -42], bD: [2.3, 0.4], bT: [2.3, 0.4], pT: [-0.3, 0.2], pD: [0.3, 0.2] }),
        // Lanceiro e Renegado (onda 2) — rascunho: o Felipe cuida do visual depois.
        estocada: pose({ tronco: 0.45, bD: [1.6, 0.05], bT: [1.3, 0.5], pT: [-0.5, 0.2], pD: [0.6, 0.3] }),
        contra: pose({ tronco: 0.35, bD: [1.65, 0.0], bT: [0.4, 2.0], pT: [-0.4, 0.2], pD: [0.5, 0.3] }),
    };
    function poseDoGolpe(ent) {
        const g = ent.golpe, nome = ent.golpeNome, t = ent.quadro;
        const k = curvaDoGolpe(t, g);
        if (nome === 'pancada' || (nome === 'especialAereo' && ent.def.arma === 'bastao')) {
            if (t < g.inicio) return mistura(GUARDA, POSES_DE_GOLPE.pancadaAlta, suave(t / g.inicio));
            if (t < g.inicio + g.ativo) return mistura(POSES_DE_GOLPE.pancadaAlta, POSES_DE_GOLPE.pancada, suave((t - g.inicio) / 0.06));
            return mistura(POSES_DE_GOLPE.pancada, GUARDA, suave((t - g.inicio - g.ativo) / (g.total - g.inicio - g.ativo)));
        }
        if (g.tipo === 'projetil') {
            const base = nome === 'flecha' ? POSES_DE_GOLPE.flecha : (nome === 'caveira' || nome === 'shuriken') ? POSES_DE_GOLPE.arremesso : POSES_DE_GOLPE.especial;
            if (nome === 'caveira' || nome === 'shuriken') {
                if (t < g.inicio) return mistura(GUARDA, POSES_DE_GOLPE.arremessoPrep, suave(t / g.inicio));
                return mistura(POSES_DE_GOLPE.arremessoPrep, base, suave((t - g.inicio) / 0.12));
            }
            const kk = t < g.inicio ? suave(t / g.inicio) : 1 - suave((t - g.inicio) / (g.total - g.inicio)) * 0.6;
            return mistura(GUARDA, base, kk);
        }
        if (g.tipo === 'giro' || g.gira) {
            const p = mistura(GUARDA, pose({ tronco: 0.1, bD: [1.57, 0], bT: [-1.57, 0], pT: [-0.4, 0.2], pD: [0.4, 0.2] }), Math.min(1, t / 0.08));
            p.escalaX = Math.cos(t * 26) * 0.9 + 0.1 * Math.sign(Math.cos(t * 26) || 1);
            return p;
        }
        if (nome === 'chama') return mistura(GUARDA, POSES_DE_GOLPE.chama, k);
        return mistura(GUARDA, POSES_DE_GOLPE[nome] || POSES_DE_GOLPE.soco1, k);
    }
    function poseDe(ent, tempo) {
        const t = ent.quadro, est = ent.estado;
        switch (est) {
            case 'parado': {
                const b = Math.sin(tempo * 2.6 + ent.id) * 0.035;      // respiração
                return pose({ quadril: [0, -36 + b * 14], bT: [0.4 + b, 1.9 - b], bD: [0.7 - b, 1.7 + b * 0.5], tronco: 0.06 + b * 0.6, cabeca: -b * 0.4 });
            }
            case 'andando': {
                const ph = tempo * 9, s = Math.sin(ph), c = Math.sin(ph + Math.PI);
                return pose({ quadril: [0, -36 + Math.abs(Math.cos(ph)) * 1.6], tronco: 0.12, pD: [0.55 * s, Math.max(0, s) * 0.9 + 0.1], pT: [0.55 * c, Math.max(0, c) * 0.9 + 0.1], bD: [0.5 - 0.35 * s, 1.6], bT: [0.5 - 0.35 * c, 1.8] });
            }
            case 'pulando':
                return mistura(pose({ tronco: 0.15, pT: [-0.5, 1.3], pD: [0.7, 1.7], bT: [1.4, 0.5], bD: [2.6, 0.2] }),
                               pose({ tronco: 0.25, pT: [-0.2, 0.5], pD: [0.4, 0.9], bT: [0.9, 0.9], bD: [1.7, 0.6] }), ent.vz > 0 ? 0 : 1);
            case 'atacando': return poseDoGolpe(ent);
            case 'atingido': return mistura(GUARDA, pose({ tronco: -0.4, cabeca: -0.5, quadril: [-6, -34], bT: [0.9, 0.4], bD: [-0.6, 0.4], pT: [-0.5, 0.3], pD: [0.2, 0.5] }), 1 - suave(t / 0.3));
            case 'lancado': return pose({ giro: -1.1 - Math.min(0.9, t * 1.2), quadril: [0, -34], tronco: -0.1, pT: [0.6, 0.5], pD: [-0.3, 0.6], bT: [2.2, 0.3], bD: [1.2, 0.6] });
            case 'caido': return pose({ giro: -Math.PI / 2, quadril: [8, -9], tronco: 0.1, pT: [0.15, 0.5], pD: [0.35, 0.7], bT: [1.2, 0.5], bD: [0.6, 0.5] });
            case 'levantando':
                return mistura(pose({ giro: -Math.PI / 2, quadril: [8, -9], pT: [0.15, 0.5], pD: [0.35, 0.7], bT: [1.2, 0.5], bD: [0.6, 0.5] }),
                               pose({ quadril: [0, -30], tronco: 0.4, pT: [-0.3, 0.9], pD: [0.5, 1.0], bT: [0.8, 1.0], bD: [1.0, 1.0] }), suave(t / 0.45));
            case 'atordoado': {
                const s = Math.sin(tempo * 5);
                return pose({ tronco: 0.25 + s * 0.12, cabeca: 0.3 + s * 0.2, quadril: [s * 4, -33], bT: [0.1 + s * 0.15, 0.3], bD: [-0.1 - s * 0.15, 0.3], pT: [-0.35, 0.4], pD: [0.35, 0.4] });
            }
            case 'defendendo': return mistura(GUARDA, pose({ tronco: 0.18, bD: [1.25, 2.2], bT: [1.05, 2.4], pT: [-0.35, 0.3], pD: [0.4, 0.35] }), Math.min(1, t / 0.08));
            case 'agarrando': return mistura(GUARDA, pose({ tronco: 0.3, bD: [1.4, 0.4], bT: [1.4, 0.5], pT: [-0.4, 0.25], pD: [0.45, 0.3] }), Math.min(1, t / 0.1));
            case 'agarrado':
            case 'puxado':                // arrastado pela corrente: o mesmo corpo largado do agarrado
                return pose({ tronco: -0.35, cabeca: -0.3, bD: [2.0, 0.4], bT: [1.7, 0.5], pT: [-0.2, 0.7], pD: [0.3, 0.8] });
            case 'furia': {               // chefe urrando, braços pro alto, tremendo
                const tr = Math.sin(t * 50) * 0.05;
                return mistura(GUARDA, pose({ tronco: -0.25 + tr, cabeca: -0.4, quadril: [tr * 30, -38], bD: [2.8, 0.4], bT: [2.8, 0.4], pT: [-0.45, 0.25], pD: [0.45, 0.25] }), suave(t / 0.2));
            }
            case 'suplex':                // do agarrão pra ponte de costas, o preso por cima da cabeça
                return mistura(pose({ tronco: 0.3, bD: [1.4, 0.4], bT: [1.4, 0.5], pT: [-0.4, 0.25], pD: [0.45, 0.3] }),
                               pose({ tronco: -0.9, cabeca: -0.3, quadril: [-4, -32], bD: [3.0, 0.3], bT: [2.9, 0.4], pT: [-0.5, 0.3], pD: [0.3, 0.5] }), suave(t / 0.25));
            case 'arremessando': return mistura(pose({ tronco: -0.1, bD: [1.4, 0.4], bT: [1.4, 0.5] }), pose({ tronco: 0.45, bD: [2.3, 0.1], bT: [-0.4, 0.8], pT: [-0.5, 0.2], pD: [0.6, 0.4] }), suave(t / 0.2));
            case 'arremessado': return pose({ giro: t * 16, quadril: [0, -34], pT: [0.7, 0.5], pD: [-0.5, 0.6], bT: [2.4, 0.3], bD: [1.0, 0.6] });
            case 'finalizando': {
                if (t < 0.55) return mistura(GUARDA, pose({ tronco: -0.25, quadril: [0, -40], bD: [3.1, 0.1], bT: [0.6, 1.9], pT: [-0.5, 0.3], pD: [0.4, 0.4] }), suave(t / 0.5));
                if (t < 0.75) return mistura(pose({ tronco: -0.25, quadril: [0, -40], bD: [3.1, 0.1], bT: [0.6, 1.9] }), pose({ tronco: 0.5, quadril: [4, -30], bD: [1.2, 0.0], bT: [0.2, 1.5], pT: [-0.7, 0.3], pD: [0.7, 0.8] }), suave((t - 0.55) / 0.1));
                return mistura(pose({ tronco: 0.5, quadril: [4, -30], bD: [1.2, 0.0], bT: [0.2, 1.5], pT: [-0.7, 0.3], pD: [0.7, 0.8] }), pose({ tronco: -0.1, bD: [3.0, 0.2], bT: [3.0, 0.2], pT: [-0.3, 0.2], pD: [0.3, 0.2] }), suave((t - 0.75) / 0.4));
            }
            case 'finalizado': {
                const tremor = t > 0.6 ? Math.sin(t * 60) * 0.08 : 0;
                return mistura(GUARDA, pose({ quadril: [tremor * 20, -20], tronco: 0.5 + tremor, cabeca: 0.6, pT: [-0.6, 2.3], pD: [0.4, 2.1], bT: [0.2, 0.3], bD: [-0.1, 0.3] }), suave(t / 0.3));
            }
            case 'morto':
                if (ent.z > 0) return pose({ giro: -1.4, quadril: [0, -34], pT: [0.6, 0.5], pD: [-0.4, 0.6], bT: [2.4, 0.3], bD: [1.5, 0.6] });
                return pose({ giro: -Math.PI / 2, quadril: [8, -9], tronco: 0.1, pT: [0.1, 0.4], pD: [0.3, 0.6], bT: [1.4, 0.3], bD: [0.5, 0.4] });
            default: return GUARDA;
        }
    }

    // ── ESTILOS: o que diferencia cada personagem além das cores ──────────────────────────
    const ESTILOS = {
        long: { torsoNu: true, cabelo: 'curto', faixaCabeca: true, corpo: 1.0 },
        shen: { roupa: 'tunica', cabelo: 'raspado', corpo: 1.06 },
        sombra: { roupa: 'justa', capuz: true, mascara: true, corpo: 0.95 },
        garra: { roupa: 'colete', cabelo: 'moicano', laminas: true, presas: true, corpo: 1.08 },
        bruto: { roupa: 'colete', cabelo: 'raspado', corpo: 1.22, careca: true, barba: 'curta' },
        arqueiro: { roupa: 'tunica', cabelo: 'coque', arco: true, corpo: 0.98 },
        mestreSombra: { roupa: 'justa', capuz: true, mascara: true, olhos: '#e9c25a', corpo: 1.1 },
        graoPresa: { roupa: 'colete', cabelo: 'moicano', laminas: true, presas: true, olhos: '#ff5a3a', corpo: 1.18 },
        gigante: { roupa: 'colete', careca: true, olhos: '#3aff9a', corpo: 1.3, barba: 'longa' },
        feiticeiro: { roupa: 'manto', cabelo: 'longo', barba: 'longa', olhos: '#c86bff', corpo: 1.1 },
        lian: { roupa: 'tunica', cabelo: 'longo', faixaCabeca: true, corpo: 0.92 },
        lanceiro: { roupa: 'colete', cabelo: 'coque', corpo: 1.0 },
        renegado: { roupa: 'tunica', cabelo: 'raspado', olhos: '#ff5a3a', corpo: 1.04 },
    };

    // ── PRIMITIVAS COM VOLUME ─────────────────────────────────────────────────────────────
    // Um membro afunilado de `a` (largura wa) até `b` (largura wb), sombreado de través.
    function membro(ctx, a, b, wa, wb, cor, luz, silhueta, brilho) {
        const dx = b[0] - a[0], dy = b[1] - a[1], len = Math.hypot(dx, dy) || 0.001;
        const nx = -dy / len, ny = dx / len, ang = Math.atan2(ny, nx);
        ctx.beginPath();
        ctx.moveTo(a[0] + nx * wa, a[1] + ny * wa);
        ctx.lineTo(b[0] + nx * wb, b[1] + ny * wb);
        ctx.arc(b[0], b[1], wb, ang, ang - Math.PI, true);
        ctx.lineTo(a[0] - nx * wa, a[1] - ny * wa);
        ctx.arc(a[0], a[1], wa, ang - Math.PI, ang - TAU, true);
        ctx.closePath();
        if (silhueta) { ctx.fillStyle = silhueta; ctx.fill(); return; }
        const w = Math.max(wa, wb);
        const mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
        const lado = (nx * luz.x + ny * luz.y) >= 0 ? 1 : -1;           // qual lado olha pra luz
        const g = ctx.createLinearGradient(mx + nx * w * lado, my + ny * w * lado, mx - nx * w * lado, my - ny * w * lado);
        g.addColorStop(0, tom(cor, 0.28 + (brilho || 0)));
        g.addColorStop(0.42, cor);
        g.addColorStop(1, tom(cor, -0.48));
        ctx.fillStyle = g; ctx.fill();
        ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(10,5,15,0.35)'; ctx.stroke();
    }

    function elipse(ctx, x, y, rx, ry, rot, cor) { ctx.beginPath(); ctx.ellipse(x, y, rx, ry, rot || 0, 0, TAU); ctx.fillStyle = cor; ctx.fill(); }

    // ── A FIGURA ──────────────────────────────────────────────────────────────────────────
    // Desenha com os pés em (0,0), virada pra direita. `o`: { silhueta: cor, tempo, virado, semDetalhe }.
    function desenhar(ctx, ent, tempo, o) {
        o = o || {};
        const p = o.pose || poseDe(ent, tempo);
        const cores = ent.def.cores, estilo = ESTILOS[ent.def.id] || {};
        const corpo = estilo.corpo || 1;
        const luz = { x: LUZ.x * (o.virado || 1), y: LUZ.y };
        const sil = o.silhueta || null;
        const vx = ent.vx || 0;
        const balanco = Math.sin(tempo * 6 + (ent.id || 0)) * 3 - vx * 0.02 * (o.virado || 1);

        ctx.save();
        ctx.scale(ent.escala * (p.escalaX || 1), ent.escala);
        ctx.translate(p.quadril[0] * K, p.quadril[1] * K);
        ctx.rotate(p.giro || 0);

        // Esqueleto.
        const quadril = [0, 0];
        const troncoL = 48 * (corpo > 1.2 ? 1.06 : 1);
        const ombro = [Math.sin(p.tronco) * troncoL, -Math.cos(p.tronco) * troncoL];
        const angCab = p.tronco + p.cabeca;
        const cabeca = [ombro[0] + Math.sin(angCab) * 21, ombro[1] - Math.cos(angCab) * 21];
        const anguloDoPe = a => a[0] - a[1];
        const perna = (a, cor, atras) => {
            const coxaL = 36, canelaL = 34;
            const joelho = [quadril[0] + Math.sin(a[0]) * coxaL, quadril[1] + Math.cos(a[0]) * coxaL];
            const pe = [joelho[0] + Math.sin(a[0] - a[1]) * canelaL, joelho[1] + Math.cos(a[0] - a[1]) * canelaL];
            const sombra = atras ? -0.16 : 0;
            membro(ctx, quadril, joelho, 14 * corpo, 10.5 * corpo, tom(cor, sombra), luz, sil);
            membro(ctx, joelho, pe, 10.5 * corpo, 6.5 * corpo, tom(cor, sombra), luz, sil);
            // Dobra do joelho e o calçado.
            if (!sil) { ctx.fillStyle = 'rgba(0,0,0,0.1)'; ctx.beginPath(); ctx.ellipse(joelho[0], joelho[1] + 2, 5 * corpo, 2.5, anguloDoPe(a), 0, TAU); ctx.fill(); }
            const angPe = a[0] - a[1];
            ctx.save(); ctx.translate(pe[0], pe[1]); ctx.rotate(angPe);
            ctx.beginPath(); ctx.moveTo(-6 * corpo, -2); ctx.lineTo(13 * corpo, 2); ctx.quadraticCurveTo(15 * corpo, 6, 10 * corpo, 7); ctx.lineTo(-6 * corpo, 6); ctx.closePath();
            ctx.fillStyle = sil || tom(cores.detalhe, atras ? -0.3 : -0.1); ctx.fill();
            if (!sil) { ctx.fillStyle = 'rgba(255,255,255,0.12)'; ctx.fillRect(-2, -1, 8 * corpo, 2); }
            ctx.restore();
            return pe;
        };
        const braco = (a, corManga, atras) => {
            const bracoL = 27, anteL = 25;
            const cot = [ombro[0] + Math.sin(a[0]) * bracoL, ombro[1] + Math.cos(a[0]) * bracoL];
            const mao = [cot[0] + Math.sin(a[0] + a[1]) * anteL, cot[1] + Math.cos(a[0] + a[1]) * anteL];
            const sombra = atras ? -0.18 : 0;
            membro(ctx, ombro, cot, 10 * corpo, 8 * corpo, tom(corManga, sombra), luz, sil);
            membro(ctx, cot, mao, 8 * corpo, 5.5 * corpo, tom(cores.pele, sombra), luz, sil);
            // Punho fechado: bloco com os nós dos dedos.
            const angMao = a[0] + a[1];
            ctx.save(); ctx.translate(mao[0], mao[1]); ctx.rotate(angMao);
            ctx.beginPath(); ctx.roundRect(-5.5 * corpo, -1, 11 * corpo, 10 * corpo, 3.5 * corpo);
            ctx.fillStyle = sil || tom(cores.pele, sombra - 0.05); ctx.fill();
            if (!sil) { ctx.strokeStyle = 'rgba(10,5,15,0.35)'; ctx.lineWidth = 1; ctx.stroke(); ctx.fillStyle = 'rgba(0,0,0,0.22)'; for (let k = -1; k <= 1; k++) ctx.fillRect(k * 3.3 * corpo - 1, 1, 2, 3); }
            ctx.restore();
            return { cot, mao, angulo: angMao };
        };
        const corManga = estilo.torsoNu ? cores.pele : (estilo.roupa === 'colete' ? cores.pele : cores.roupa);

        // Manto do feiticeiro atrás de tudo.
        if (estilo.roupa === 'manto') {
            ctx.beginPath(); ctx.moveTo(ombro[0] - 16, ombro[1]); ctx.lineTo(ombro[0] + 8, ombro[1] - 2);
            ctx.quadraticCurveTo(quadril[0] + 14 + balanco, quadril[1] + 30, quadril[0] + 4 + balanco * 2, quadril[1] + 66);
            ctx.lineTo(quadril[0] - 36 - balanco * 2, quadril[1] + 60); ctx.quadraticCurveTo(quadril[0] - 30, quadril[1], ombro[0] - 16, ombro[1]);
            if (sil) ctx.fillStyle = sil; else { const g = ctx.createLinearGradient(-30, ombro[1], 10, quadril[1] + 60); g.addColorStop(0, tom(cores.detalhe, 0.1)); g.addColorStop(1, tom(cores.detalhe, -0.55)); ctx.fillStyle = g; }
            ctx.fill();
        }
        // Rabo da faixa da cabeça e do sash, atrás do corpo.
        if (!sil && estilo.faixaCabeca) fita(ctx, [cabeca[0] - 9, cabeca[1] - 6], -1, balanco, cores.faixa, 26, tempo);
        if (!sil && (estilo.torsoNu || estilo.roupa === 'tunica')) fita(ctx, [quadril[0] - 9, quadril[1] + 4], -1, balanco * 0.7, cores.faixa, 30, tempo + 1);

        // Braço e perna de trás (mais escuros: estão na sombra do corpo).
        const bt = braco(p.bT, corManga, true);
        if (estilo.laminas) lamina(ctx, bt, cores, sil);
        if (estilo.arco) arco(ctx, bt, sil);
        perna(p.pT, cores.roupa, true);

        // Tronco.
        tronco(ctx, quadril, ombro, p, cores, estilo, corpo, luz, sil);
        // Pescoço.
        membro(ctx, [ombro[0], ombro[1] + 2], [cabeca[0] - Math.sin(angCab) * 8, cabeca[1] + Math.cos(angCab) * 8], 6 * corpo, 5.5 * corpo, tom(cores.pele, -0.12), luz, sil);
        // Perna da frente.
        perna(p.pD, cores.roupa, false);
        // Cabeça.
        cabecaDesenho(ctx, cabeca, angCab, ent, cores, estilo, corpo, luz, sil, tempo);
        // Braço da frente e a arma.
        const bd = braco(p.bD, corManga, false);
        if (ent.def.arma === 'bastao') bastao(ctx, bd, bt, ent, sil);
        if (ent.def.arma === 'corrente') corrente(ctx, bd, ent, p, sil, tempo);
        if (ent.def.arma === 'lanca') lanca(ctx, bd, ent, p, sil);
        if (estilo.laminas) lamina(ctx, bd, cores, sil);

        // Estrelinhas do atordoado.
        if (!sil && ent.estado === 'atordoado') {
            ctx.fillStyle = '#ffe680';
            for (let k = 0; k < 3; k++) { const a = tempo * 5 + k * TAU / 3; ctx.beginPath(); ctx.arc(cabeca[0] + Math.cos(a) * 20, cabeca[1] - 20 + Math.sin(a) * 6, 2.6, 0, TAU); ctx.fill(); }
        }
        ctx.restore();
    }

    function fita(ctx, origem, direcao, balanco, cor, comp, tempo) {
        // Uma fita de pano que pende e balança: curva de dois segmentos com o fim atrasado.
        const ondula = Math.sin(tempo * 9) * 3;
        ctx.beginPath();
        ctx.moveTo(origem[0], origem[1]);
        ctx.quadraticCurveTo(origem[0] + direcao * comp * 0.5 + balanco, origem[1] + comp * 0.35 + ondula, origem[0] + direcao * comp * 0.7 + balanco * 1.6, origem[1] + comp * 0.9);
        ctx.lineTo(origem[0] + direcao * comp * 0.7 + balanco * 1.6 + 5, origem[1] + comp * 0.9 + 2);
        ctx.quadraticCurveTo(origem[0] + direcao * comp * 0.5 + balanco + 5, origem[1] + comp * 0.35 + ondula + 3, origem[0] + 4, origem[1] + 3);
        ctx.closePath();
        const g = ctx.createLinearGradient(origem[0], origem[1], origem[0] + direcao * comp * 0.7, origem[1] + comp);
        g.addColorStop(0, tom(cor, 0.05)); g.addColorStop(1, tom(cor, -0.4));
        ctx.fillStyle = g; ctx.fill();
    }

    function tronco(ctx, quadril, ombro, p, cores, estilo, corpo, luz, sil) {
        const ang = p.tronco;
        ctx.save();
        ctx.translate(quadril[0], quadril[1]); ctx.rotate(ang);
        const L = -Math.hypot(ombro[0], ombro[1]);          // ombro fica em (0, L)
        const so = 17 * corpo, sq = 12 * corpo;              // meia largura no ombro e no quadril
        ctx.beginPath();
        ctx.moveTo(-sq, 4); ctx.quadraticCurveTo(-sq - 4, L * 0.55, -so, L + 4);
        ctx.quadraticCurveTo(0, L - 6, so, L + 4);
        ctx.quadraticCurveTo(sq + 5, L * 0.55, sq, 4); ctx.closePath();
        const corBase = estilo.torsoNu ? cores.pele : cores.roupa;
        if (sil) { ctx.fillStyle = sil; ctx.fill(); ctx.restore(); return; }
        const lado = luz.x >= 0 ? 1 : -1;
        const g = ctx.createLinearGradient(-so * lado, L, so * lado, 0);
        g.addColorStop(0, tom(corBase, 0.22)); g.addColorStop(0.5, corBase); g.addColorStop(1, tom(corBase, -0.45));
        ctx.fillStyle = g; ctx.fill();
        ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(10,5,15,0.35)'; ctx.stroke();
        if (estilo.torsoNu) {
            // Peitoral e abdome — leve, sombra e luz, sem contorno.
            ctx.fillStyle = 'rgba(0,0,0,0.13)';
            ctx.beginPath(); ctx.ellipse(-7 * corpo, L + 16, 8 * corpo, 6, -0.2, 0, TAU); ctx.fill();
            ctx.beginPath(); ctx.ellipse(8 * corpo, L + 15, 8 * corpo, 6, 0.2, 0, TAU); ctx.fill();
            ctx.fillStyle = 'rgba(255,240,200,0.14)';
            ctx.beginPath(); ctx.ellipse(-7 * corpo, L + 13, 7 * corpo, 4, -0.2, 0, TAU); ctx.fill();
            ctx.beginPath(); ctx.ellipse(8 * corpo, L + 12, 7 * corpo, 4, 0.2, 0, TAU); ctx.fill();
            ctx.strokeStyle = 'rgba(0,0,0,0.16)'; ctx.lineWidth = 1.5;
            ctx.beginPath(); ctx.moveTo(1, L + 24); ctx.lineTo(0, -2); ctx.stroke();
            for (const y of [L + 30, L + 38]) { ctx.beginPath(); ctx.moveTo(-6 * corpo, y); ctx.quadraticCurveTo(0, y + 2, 6 * corpo, y); ctx.stroke(); }
            // A faixa de monge atravessada e o cinto.
            faixaDiagonal(ctx, so, sq, L, cores.faixa);
        } else if (estilo.roupa === 'tunica') {
            // Gola cruzada e dobras.
            ctx.strokeStyle = 'rgba(0,0,0,0.28)'; ctx.lineWidth = 2;
            ctx.beginPath(); ctx.moveTo(-so + 6, L + 2); ctx.lineTo(3, L + 22); ctx.lineTo(so - 6, L + 2); ctx.stroke();
            ctx.strokeStyle = 'rgba(0,0,0,0.14)'; ctx.lineWidth = 1.5;
            for (const x of [-6, 5]) { ctx.beginPath(); ctx.moveTo(x, L + 26); ctx.quadraticCurveTo(x + 2, L * 0.4, x - 1, 0); ctx.stroke(); }
            ctx.fillStyle = tom(cores.pele, -0.1); ctx.beginPath(); ctx.moveTo(-so + 8, L + 3); ctx.lineTo(2, L + 19); ctx.lineTo(so - 8, L + 3); ctx.closePath(); ctx.fill();
        } else if (estilo.roupa === 'colete') {
            ctx.fillStyle = tom(cores.pele, -0.05);
            ctx.beginPath(); ctx.moveTo(-8 * corpo, L + 6); ctx.lineTo(8 * corpo, L + 6); ctx.lineTo(5 * corpo, 2); ctx.lineTo(-5 * corpo, 2); ctx.closePath(); ctx.fill();
            ctx.fillStyle = 'rgba(0,0,0,0.15)'; ctx.beginPath(); ctx.ellipse(-4 * corpo, L + 18, 6 * corpo, 5, 0, 0, TAU); ctx.fill(); ctx.beginPath(); ctx.ellipse(4 * corpo, L + 18, 6 * corpo, 5, 0, 0, TAU); ctx.fill();
            ctx.strokeStyle = 'rgba(0,0,0,0.3)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(-8 * corpo, L + 6); ctx.lineTo(-5 * corpo, 2); ctx.moveTo(8 * corpo, L + 6); ctx.lineTo(5 * corpo, 2); ctx.stroke();
        } else if (estilo.roupa === 'justa') {
            ctx.strokeStyle = 'rgba(255,255,255,0.08)'; ctx.lineWidth = 1.5;
            for (const y of [L + 14, L + 26, L + 38]) { ctx.beginPath(); ctx.moveTo(-sq, y); ctx.quadraticCurveTo(0, y + 3, sq, y); ctx.stroke(); }
            ctx.fillStyle = tom(cores.detalhe, 0.05); ctx.beginPath(); ctx.moveTo(-so + 4, L + 2); ctx.lineTo(0, L + 14); ctx.lineTo(so - 4, L + 2); ctx.closePath(); ctx.fill();
        }
        // Cinto/faixa da cintura.
        const gc = ctx.createLinearGradient(0, -4, 0, 6);
        gc.addColorStop(0, tom(cores.faixa, 0.15)); gc.addColorStop(1, tom(cores.faixa, -0.35));
        ctx.fillStyle = gc; ctx.beginPath(); ctx.roundRect(-sq - 1, -5, sq * 2 + 2, 10, 3); ctx.fill();
        ctx.fillStyle = tom(cores.faixa, -0.5); ctx.fillRect(sq - 6, -4, 5, 8);
        ctx.restore();
    }

    function faixaDiagonal(ctx, so, sq, L, cor) {
        const g = ctx.createLinearGradient(-so, L, sq, 0);
        g.addColorStop(0, tom(cor, 0.12)); g.addColorStop(1, tom(cor, -0.35));
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.moveTo(-so + 3, L + 4); ctx.lineTo(-so + 12, L); ctx.lineTo(sq + 2, -2); ctx.lineTo(sq - 6, 4); ctx.closePath(); ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.25)'; ctx.lineWidth = 1; ctx.stroke();
    }

    function cabecaDesenho(ctx, c, ang, ent, cores, estilo, corpo, luz, sil, tempo) {
        ctx.save();
        ctx.translate(c[0], c[1]); ctx.rotate(ang);
        const r = 12 * (corpo > 1.2 ? 1.12 : 1);
        const atacando = ent.estado === 'atacando' || ent.estado === 'finalizando' || ent.estado === 'agarrando' || ent.estado === 'suplex';
        const sofrendo = ent.estado === 'atingido' || ent.estado === 'lancado' || ent.estado === 'agarrado' || ent.estado === 'finalizado' || ent.estado === 'morto';
        // Cabelo longo/rabo atrás da cabeça.
        if (!sil && estilo.cabelo === 'longo') { ctx.fillStyle = tom(cores.cabelo, -0.1); ctx.beginPath(); ctx.moveTo(-r + 2, -r + 4); ctx.quadraticCurveTo(-r - 10, 10, -r - 4, 34); ctx.lineTo(-2, 30); ctx.quadraticCurveTo(-4, 8, 2, -r); ctx.closePath(); ctx.fill(); }
        // Crânio + maxilar.
        ctx.beginPath();
        ctx.moveTo(-r, -3); ctx.quadraticCurveTo(-r - 1, 9, -5, r + 2); ctx.lineTo(4, r + 3); ctx.quadraticCurveTo(r + 1, 8, r - 1, -3);
        ctx.arc(0, -2, r, 0, Math.PI, true); ctx.closePath();
        if (sil) { ctx.fillStyle = sil; ctx.fill(); }
        else {
            const g = ctx.createRadialGradient(-r * 0.4 * (luz.x < 0 ? 1 : -1), -r * 0.6, 2, 0, 0, r * 1.6);
            g.addColorStop(0, tom(cores.pele, 0.25)); g.addColorStop(0.55, cores.pele); g.addColorStop(1, tom(cores.pele, -0.5));
            ctx.fillStyle = g; ctx.fill();
            ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(10,5,15,0.35)'; ctx.stroke();
            // Orelha (lado de trás), sombra do maxilar.
            elipse(ctx, -r + 2, 1, 3, 4.5, 0, tom(cores.pele, -0.15));
            ctx.fillStyle = 'rgba(0,0,0,0.14)'; ctx.beginPath(); ctx.moveTo(-6, r - 2); ctx.quadraticCurveTo(0, r + 4, 6, r - 1); ctx.quadraticCurveTo(0, r, -6, r - 2); ctx.fill();
        }
        if (!sil) {
            if (estilo.capuz) {
                // Capuz: cobre o crânio e as laterais; o rosto fica na sombra e a máscara tampa a boca.
                const g = ctx.createLinearGradient(-r, -r, r, r);
                g.addColorStop(0, tom(cores.roupa, 0.12)); g.addColorStop(1, tom(cores.roupa, -0.4));
                ctx.fillStyle = g;
                ctx.beginPath(); ctx.moveTo(-r - 3, 8); ctx.quadraticCurveTo(-r - 4, -r - 4, 0, -r - 5); ctx.quadraticCurveTo(r + 4, -r - 4, r + 2, 0);
                ctx.lineTo(r - 1, -4); ctx.quadraticCurveTo(r - 6, -8, 3, -7); ctx.quadraticCurveTo(-r + 2, -6, -r + 2, 4); ctx.lineTo(-r + 1, r); ctx.closePath(); ctx.fill();
                if (estilo.mascara) { ctx.fillStyle = tom(cores.roupa, -0.2); ctx.beginPath(); ctx.moveTo(-r + 2, 2); ctx.lineTo(r + 1, 1); ctx.lineTo(r - 3, r + 1); ctx.lineTo(-5, r + 2); ctx.closePath(); ctx.fill(); }
                ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.fillRect(-2, -6, r + 2, 7);     // sombra do capuz sobre os olhos
                const corOlho = estilo.olhos || '#e8e8f0';
                ctx.save(); ctx.shadowColor = corOlho; ctx.shadowBlur = estilo.olhos ? 10 : 3; ctx.fillStyle = corOlho;
                ctx.beginPath(); ctx.ellipse(6, -2.5, 3.2, 1.2, 0.1, 0, TAU); ctx.fill(); ctx.restore();
            } else {
                // Cabelo.
                if (estilo.cabelo === 'curto') {
                    ctx.fillStyle = cores.cabelo;
                    ctx.beginPath(); ctx.moveTo(-r - 1, 0); ctx.quadraticCurveTo(-r - 2, -r - 4, -2, -r - 3); ctx.lineTo(4, -r - 6); ctx.lineTo(7, -r - 1); ctx.lineTo(r - 1, -r + 1);
                    ctx.quadraticCurveTo(r - 4, -6, r - 6, -5); ctx.quadraticCurveTo(0, -8, -r + 3, -3); ctx.closePath(); ctx.fill();
                    ctx.fillStyle = 'rgba(255,255,255,0.12)'; ctx.beginPath(); ctx.ellipse(-2, -r + 1, 6, 2, -0.3, 0, TAU); ctx.fill();
                } else if (estilo.cabelo === 'raspado' || estilo.careca) {
                    ctx.fillStyle = 'rgba(0,0,0,0.12)'; ctx.beginPath(); ctx.arc(0, -2, r, Math.PI * 1.05, Math.PI * 1.95); ctx.lineTo(r - 2, -4); ctx.quadraticCurveTo(0, -7, -r + 2, -4); ctx.closePath(); ctx.fill();
                    ctx.fillStyle = 'rgba(255,255,255,0.14)'; ctx.beginPath(); ctx.ellipse(-3, -r + 3, 5, 2, -0.4, 0, TAU); ctx.fill();
                } else if (estilo.cabelo === 'moicano') {
                    ctx.fillStyle = cores.cabelo;
                    ctx.beginPath(); ctx.moveTo(-r + 2, -4); for (let k = 0; k < 5; k++) { ctx.lineTo(-r + 4 + k * 5, -r - 8 - (k % 2) * 3); ctx.lineTo(-r + 7 + k * 5, -r + 1); } ctx.lineTo(r - 3, -5); ctx.closePath(); ctx.fill();
                } else if (estilo.cabelo === 'coque') {
                    ctx.fillStyle = cores.cabelo; ctx.beginPath(); ctx.arc(0, -2, r + 0.5, Math.PI * 1.05, Math.PI * 1.95); ctx.lineTo(r - 2, -5); ctx.quadraticCurveTo(0, -8, -r + 2, -5); ctx.closePath(); ctx.fill();
                    ctx.beginPath(); ctx.arc(-3, -r - 4, 5, 0, TAU); ctx.fill();
                } else if (estilo.cabelo === 'longo') {
                    ctx.fillStyle = cores.cabelo; ctx.beginPath(); ctx.arc(0, -2, r + 1, Math.PI * 1.02, Math.PI * 1.98); ctx.lineTo(r - 1, -6); ctx.quadraticCurveTo(0, -9, -r + 1, -4); ctx.closePath(); ctx.fill();
                }
                if (estilo.faixaCabeca) {
                    const gf = ctx.createLinearGradient(0, -8, 0, -3); gf.addColorStop(0, tom(cores.faixa, 0.15)); gf.addColorStop(1, tom(cores.faixa, -0.3));
                    ctx.fillStyle = gf; ctx.beginPath(); ctx.moveTo(-r - 1, -7); ctx.quadraticCurveTo(0, -9, r + 1, -7); ctx.lineTo(r + 1, -2.5); ctx.quadraticCurveTo(0, -4.5, -r - 1, -2.5); ctx.closePath(); ctx.fill();
                }
                // Sobrancelha, olho, nariz, boca.
                const raiva = atacando ? 1.2 : sofrendo ? -0.6 : 0.3;
                ctx.strokeStyle = tom(cores.cabelo, 0.1); ctx.lineWidth = 1.8; ctx.lineCap = 'round';
                ctx.beginPath(); ctx.moveTo(2, -6 + raiva * 0.5); ctx.lineTo(9, -6.5 - raiva * 1.2); ctx.stroke();
                elipse(ctx, 6, -2.5, 3.2, sofrendo ? 1.2 : 2, 0, '#f3ece4');
                elipse(ctx, 6.6, -2.5, 1.5, sofrendo ? 1.0 : 1.5, 0, estilo.olhos || '#2b1d12');
                if (estilo.olhos) { ctx.save(); ctx.shadowColor = estilo.olhos; ctx.shadowBlur = 8; elipse(ctx, 6.6, -2.5, 1.2, 1.2, 0, estilo.olhos); ctx.restore(); }
                ctx.fillStyle = 'rgba(255,255,255,0.8)'; ctx.fillRect(6.2, -3.4, 1, 1);
                ctx.strokeStyle = 'rgba(0,0,0,0.3)'; ctx.lineWidth = 1.2;
                ctx.beginPath(); ctx.moveTo(r - 2, -1); ctx.lineTo(r, 3); ctx.lineTo(r - 3, 4); ctx.stroke();
                if (atacando || sofrendo) { ctx.fillStyle = '#3a1418'; ctx.beginPath(); ctx.ellipse(6, 8, 3.5, atacando ? 1.8 : 2.4, 0, 0, TAU); ctx.fill(); ctx.fillStyle = '#eee6d8'; ctx.fillRect(3.5, 6.6, 5, 1.2); }
                else { ctx.strokeStyle = 'rgba(60,20,20,0.6)'; ctx.lineWidth = 1.3; ctx.beginPath(); ctx.moveTo(3, 8); ctx.lineTo(9, 8); ctx.stroke(); }
                if (estilo.presas) { ctx.fillStyle = '#f2ead7'; for (const x of [3, 8]) { ctx.beginPath(); ctx.moveTo(x - 1.5, 7); ctx.lineTo(x, 13); ctx.lineTo(x + 1.5, 7); ctx.fill(); } }
                if (estilo.barba) {
                    ctx.fillStyle = cores.cabelo;
                    const comp = estilo.barba === 'longa' ? 26 : 6;
                    ctx.beginPath(); ctx.moveTo(-6, r - 2); ctx.quadraticCurveTo(-4, r + comp, 1, r + comp + 2); ctx.quadraticCurveTo(7, r + comp * 0.6, 8, r - 1); ctx.closePath(); ctx.fill();
                }
            }
        }
        ctx.restore();
    }

    function bastao(ctx, frente, tras, ent, sil) {
        const dx = frente.mao[0] - tras.mao[0], dy = frente.mao[1] - tras.mao[1];
        let ang = Math.hypot(dx, dy) > 8 ? Math.atan2(dy, dx) : frente.angulo - Math.PI / 2;
        if (ent.estado === 'atacando' && ent.golpe && ent.golpe.tipo === 'giro') ang = 0;
        const cx = (frente.mao[0] + tras.mao[0]) / 2, cy = (frente.mao[1] + tras.mao[1]) / 2;
        const comp = 78;
        ctx.save(); ctx.translate(cx, cy); ctx.rotate(ang);
        if (sil) { ctx.fillStyle = sil; ctx.fillRect(-comp, -3, comp * 2, 6); ctx.restore(); return; }
        const g = ctx.createLinearGradient(0, -3.5, 0, 3.5);
        g.addColorStop(0, '#9a6a3a'); g.addColorStop(0.5, '#6b4525'); g.addColorStop(1, '#3a2312');
        ctx.fillStyle = g; ctx.beginPath(); ctx.roundRect(-comp, -3.5, comp * 2, 7, 3); ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.3)'; ctx.lineWidth = 1; ctx.stroke();
        const gm = ctx.createLinearGradient(0, -4, 0, 4); gm.addColorStop(0, '#fff1b0'); gm.addColorStop(0.5, '#d4af37'); gm.addColorStop(1, '#7a5a10');
        ctx.fillStyle = gm; ctx.fillRect(-comp, -4, 11, 8); ctx.fillRect(comp - 11, -4, 11, 8);
        ctx.fillStyle = 'rgba(255,255,255,0.18)'; ctx.fillRect(-comp + 12, -3, comp * 2 - 24, 1.5);
        ctx.restore();
    }
    // Até onde o golpe em curso chega, no espaço da figura (ela está escalada por `escala` e deslocada
    // pelo quadril), e quanto dele está esticado agora (0 a 1, a mesma curva da pose).
    function extensaoDoGolpe(ent, p) {
        if (ent.estado !== 'atacando' || !ent.golpe || !ent.golpe.alcance) return null;
        return { ate: ent.golpe.alcance / (ent.escala || 1) - p.quadril[0] * K, k: curvaDoGolpe(ent.quadro, ent.golpe) };
    }
    // A corrente da Lian: elos (traços curtos) da mão até o alcance do golpe; parada, pende da mão.
    // No especial no ar (`gira`), dois braços de corrente giram em volta dela. Rascunho, sem polir.
    function corrente(ctx, b, ent, p, sil, tempo) {
        const ext = extensaoDoGolpe(ent, p);
        const [mx, my] = b.mao;
        let pontas;
        if (ext && ent.golpe.gira) {
            const a = ent.quadro * 22, r = ext.ate * Math.max(0.3, ext.k);
            pontas = [[Math.cos(a) * r, my + Math.sin(a) * r * 0.35], [-Math.cos(a) * r, my - Math.sin(a) * r * 0.35]].map(q => [[0, my], q]);
        } else if (ext) {
            pontas = [[[mx, my], [mx + (ext.ate - mx) * ext.k, my]]];
        } else {
            const bal = Math.sin(tempo * 3 + (ent.id || 0)) * 6;
            pontas = [[[mx, my], [mx + 6 + bal, my + 34]]];
        }
        ctx.save();
        ctx.lineCap = 'round';
        for (const [[x0, y0], [x1, y1]] of pontas) {
            const len = Math.hypot(x1 - x0, y1 - y0), n = Math.max(2, Math.ceil(len / 9));
            ctx.strokeStyle = sil || '#aeb6c2'; ctx.lineWidth = 3;
            for (let k = 0; k < n; k++) {
                const a = k / n, c = Math.min(1, (k + 0.7) / n);
                ctx.beginPath(); ctx.moveTo(x0 + (x1 - x0) * a, y0 + (y1 - y0) * a); ctx.lineTo(x0 + (x1 - x0) * c, y0 + (y1 - y0) * c); ctx.stroke();
            }
            // O peso da ponta.
            ctx.fillStyle = sil || '#7d8694'; ctx.beginPath(); ctx.moveTo(x1 - 4, y1 - 4); ctx.lineTo(x1 + 5, y1); ctx.lineTo(x1 - 4, y1 + 4); ctx.closePath(); ctx.fill();
        }
        ctx.restore();
    }
    // A lança: cabo reto na altura da mão da frente; na estocada, a ponta vai até o alcance.
    function lanca(ctx, frente, ent, p, sil) {
        const ext = extensaoDoGolpe(ent, p);
        const [fx, fy] = frente.mao;
        const repouso = fx + 46;
        const pontaX = ext ? repouso + (ext.ate - repouso) * ext.k : repouso;
        const ponta = [pontaX, fy], cauda = [pontaX - 150, fy + 6];
        ctx.save(); ctx.lineCap = 'round';
        ctx.strokeStyle = sil || '#6b4525'; ctx.lineWidth = 5;
        ctx.beginPath(); ctx.moveTo(cauda[0], cauda[1]); ctx.lineTo(ponta[0] - 14, ponta[1]); ctx.stroke();
        ctx.fillStyle = sil || '#c9ced8';
        ctx.beginPath(); ctx.moveTo(ponta[0] - 16, ponta[1] - 5); ctx.lineTo(ponta[0], ponta[1]); ctx.lineTo(ponta[0] - 16, ponta[1] + 5); ctx.closePath(); ctx.fill();
        ctx.restore();
    }

    function lamina(ctx, b, cores, sil) {
        ctx.save(); ctx.translate(b.mao[0], b.mao[1]); ctx.rotate(b.angulo - Math.PI / 2);
        ctx.beginPath(); ctx.moveTo(0, -4); ctx.lineTo(34, -1.5); ctx.lineTo(38, 0); ctx.lineTo(0, 4); ctx.closePath();
        if (sil) { ctx.fillStyle = sil; ctx.fill(); ctx.restore(); return; }
        const g = ctx.createLinearGradient(0, -4, 0, 4); g.addColorStop(0, '#f4f6fa'); g.addColorStop(0.5, '#b8bec8'); g.addColorStop(1, '#5a6070');
        ctx.fillStyle = g; ctx.fill(); ctx.strokeStyle = 'rgba(0,0,0,0.4)'; ctx.lineWidth = 1; ctx.stroke();
        ctx.fillStyle = tom(cores.faixa, -0.2); ctx.fillRect(-4, -4, 6, 8);
        ctx.restore();
    }
    function arco(ctx, b, sil) {
        ctx.save(); ctx.translate(b.mao[0], b.mao[1]);
        ctx.strokeStyle = sil || '#7a5230'; ctx.lineWidth = 4; ctx.lineCap = 'round';
        ctx.beginPath(); ctx.arc(0, 0, 34, Math.PI * 0.55, Math.PI * 1.45, true); ctx.stroke();
        if (!sil) { ctx.strokeStyle = '#e8e2d0'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(Math.cos(Math.PI * 0.55) * 34, Math.sin(Math.PI * 0.55) * 34); ctx.lineTo(Math.cos(Math.PI * 1.45) * 34, Math.sin(Math.PI * 1.45) * 34); ctx.stroke(); }
        ctx.restore();
    }

    // ── SOMBRA PROJETADA E RASTRO ─────────────────────────────────────────────────────────
    // Chamada com o contexto já no pé da figura (antes de virar). Achata a silhueta no chão,
    // inclinada pro lado oposto da luz, e desfoca quando a qualidade permite.
    function sombraProjetada(ctx, ent, tempo, virado, qualidade) {
        ctx.save();
        // Sem `filter: blur`: custa um passe de desfoque por lutador por quadro e derruba o fps.
        // A suavidade vem de duas silhuetas sobrepostas, uma um pouco maior e mais fraca.
        const alpha = 0.26 * Math.max(0.3, 1 - ent.z / 320);
        ctx.transform(1, 0, -0.42, -0.24, 0, -ent.z * 0.12);
        if (qualidade !== 'media') { ctx.save(); ctx.globalAlpha = alpha * 0.45; ctx.scale(1.06, 1.04); ctx.scale(virado, 1); desenhar(ctx, ent, tempo, { silhueta: 'rgba(5,3,12,1)', virado }); ctx.restore(); }
        ctx.globalAlpha = alpha;
        ctx.scale(virado, 1);
        desenhar(ctx, ent, tempo, { silhueta: 'rgba(5,3,12,1)', virado });
        ctx.restore();
    }

    function rastro(ctx, ent, tempo, virado) {
        if (ent.estado !== 'atacando' || !ent.golpe || !((ent.golpe.avanco || 0) >= 300 || (ent.golpe.mergulho && ent.golpe.mergulho[0]))) return;   // rastro só em quem avança RÁPIDO na horizontal (investida, mergulho) — o passinho do soco4/soco5 não
        for (let k = 1; k <= 2; k++) {
            ctx.save(); ctx.globalAlpha = 0.14 / k; ctx.translate(-virado * 14 * k, 0); ctx.scale(virado, 1);
            desenhar(ctx, ent, tempo, { silhueta: '#ffb347', virado });
            ctx.restore();
        }
    }

    raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {};
    raiz.PunhosDeShaolin.Figura = { desenhar, poseDe, sombraProjetada, rastro, ESTILOS, tom, LUZ };
})(window);
