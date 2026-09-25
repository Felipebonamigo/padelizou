// PUNHOS DE SHAOLIN — os cenários, pintados em camadas.
//
// Segunda geração. Cada cenário tem: céu com profundidade atmosférica (o que está longe fica
// azulado e sem contraste), uma camada de meio em ladrilho com arquitetura, LUZES de verdade
// (lanternas, tochas, poças) que pintam a parede e o chão e tremulam, chão com textura de ruído
// e perspectiva, névoa em movimento na frente e atrás dos lutadores, e um pós-processamento de
// vinheta, grão e gradação de cor. O que é estático vai pra canvas fora da tela uma vez só.
(function (raiz) {
    'use strict';
    const Motor = raiz.PunhosDeShaolin.Motor;
    const { LARGURA, ALTURA, CHAO_TOPO } = Motor;
    const TAU = Math.PI * 2;
    const ALTURA_CHAO = ALTURA - CHAO_TOPO;

    function canvasFora(l, a) { const c = document.createElement('canvas'); c.width = l; c.height = a; return c; }
    function rng(semente) { let s = semente >>> 0 || 1; return () => { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296; }; }
    function grad(ctx, x0, y0, x1, y1, paradas) { const g = ctx.createLinearGradient(x0, y0, x1, y1); for (const [k, c] of paradas) g.addColorStop(k, c); return g; }
    function radial(ctx, x, y, r0, r1, paradas) { const g = ctx.createRadialGradient(x, y, r0, x, y, r1); for (const [k, c] of paradas) g.addColorStop(k, c); return g; }

    // Ruído: um ladrilho de 256×256 de cinza aleatório. Serve de textura (multiply) e de grão (overlay).
    let ruidoTela = null;
    function ruido() {
        if (ruidoTela) return ruidoTela;
        ruidoTela = canvasFora(256, 256);
        const c = ruidoTela.getContext('2d');
        const img = c.createImageData(256, 256);
        const r = rng(4242);
        for (let i = 0; i < img.data.length; i += 4) { const v = 150 + r() * 105; img.data[i] = img.data[i + 1] = img.data[i + 2] = v; img.data[i + 3] = 255; }
        c.putImageData(img, 0, 0);
        return ruidoTela;
    }

    // ── AS DEFINIÇÕES ─────────────────────────────────────────────────────────────────────
    const CENARIOS = {
        patio: {
            ambiente: 'brasa', gradacao: 'rgba(255,150,60,0.07)', nevoa: 'rgba(90,70,120,0.14)',
            fundo(ctx) {
                ctx.fillStyle = grad(ctx, 0, 0, 0, CHAO_TOPO, [[0, '#050a22'], [0.55, '#1a1638'], [0.85, '#3a1f36'], [1, '#4a2a3a']]); ctx.fillRect(0, 0, LARGURA, CHAO_TOPO);
                const r = rng(7); ctx.fillStyle = '#fff';
                for (let i = 0; i < 140; i++) { ctx.globalAlpha = 0.25 + r() * 0.75; const t = r() * 1.8 + 0.4; ctx.fillRect(r() * LARGURA, r() * 200, t, t); }
                ctx.globalAlpha = 1;
                ctx.fillStyle = radial(ctx, 760, 84, 30, 140, [[0, 'rgba(255,236,190,0.35)'], [1, 'rgba(255,236,190,0)']]); ctx.fillRect(560, -60, 400, 300);
                ctx.fillStyle = radial(ctx, 752, 78, 4, 36, [[0, '#fff7dc'], [0.7, '#f1dc9e'], [1, '#c9b070']]); ctx.beginPath(); ctx.arc(760, 84, 36, 0, TAU); ctx.fill();
                ctx.fillStyle = 'rgba(150,130,90,0.25)'; for (const [x, y, rr] of [[748, 70, 6], [770, 95, 9], [752, 98, 4]]) { ctx.beginPath(); ctx.arc(x, y, rr, 0, TAU); ctx.fill(); }
                // Montanhas: duas cadeias, a de trás mais azul e clara (ar entre a gente e ela).
                for (const [cor, base, amp, semente, y0] of [['#1c1f45', 250, 40, 3, 0.011], ['#0e1030', 285, 28, 9, 0.017]]) {
                    const rr = rng(semente);
                    ctx.fillStyle = cor; ctx.beginPath(); ctx.moveTo(0, CHAO_TOPO);
                    for (let x = 0; x <= LARGURA; x += 24) ctx.lineTo(x, base - Math.abs(Math.sin(x * y0 + semente)) * amp - rr() * 10);
                    ctx.lineTo(LARGURA, CHAO_TOPO); ctx.fill();
                }
                // Um pagode na crista.
                ctx.fillStyle = '#0a0b22';
                for (let n = 0; n < 3; n++) { const y = 232 - n * 18, w = 46 - n * 10; ctx.beginPath(); ctx.moveTo(200 - w, y); ctx.quadraticCurveTo(200, y - 6, 200 + w, y); ctx.lineTo(200 + w - 8, y - 12); ctx.lineTo(200 - w + 8, y - 12); ctx.fill(); ctx.fillRect(200 - w + 12, y - 18, w * 2 - 24, 8); }
                ctx.fillStyle = grad(ctx, 0, 200, 0, CHAO_TOPO, [[0, 'rgba(60,40,90,0)'], [1, 'rgba(60,40,90,0.5)']]); ctx.fillRect(0, 200, LARGURA, CHAO_TOPO - 200);
            },
            meio: { largura: 640, desenhar(ctx) {
                const L = 640;
                // Muro de pedra com fiadas e argamassa.
                ctx.fillStyle = grad(ctx, 0, 150, 0, CHAO_TOPO + 10, [[0, '#4a2a26'], [0.5, '#3a1d1a'], [1, '#22100e']]); ctx.fillRect(0, 150, L, CHAO_TOPO - 140);
                ctx.fillStyle = grad(ctx, 0, 150, 0, 230, [[0, 'rgba(255,170,90,0.10)'], [1, 'rgba(255,170,90,0)']]); ctx.fillRect(0, 150, L, 80);
                ctx.strokeStyle = 'rgba(0,0,0,0.35)'; ctx.lineWidth = 1.5;
                for (let y = 176; y < CHAO_TOPO; y += 22) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(L, y); ctx.stroke(); const off = ((y / 22) % 2) * 26; for (let x = off; x < L; x += 52) { ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x, y + 22); ctx.stroke(); } }
                ctx.fillStyle = 'rgba(255,255,255,0.05)'; for (let y = 176; y < CHAO_TOPO; y += 22) ctx.fillRect(0, y + 1, L, 1);
                // Portão de madeira no meio do ladrilho.
                ctx.fillStyle = grad(ctx, 250, 0, 390, 0, [[0, '#3a2412'], [0.5, '#5a3a1c'], [1, '#2e1c0c']]); ctx.beginPath(); ctx.moveTo(250, CHAO_TOPO + 8); ctx.lineTo(250, 210); ctx.arc(320, 210, 70, Math.PI, 0); ctx.lineTo(390, CHAO_TOPO + 8); ctx.fill();
                ctx.strokeStyle = 'rgba(0,0,0,0.4)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(320, 140); ctx.lineTo(320, CHAO_TOPO); ctx.stroke();
                ctx.fillStyle = '#d4af37'; for (let y = 200; y < CHAO_TOPO; y += 30) for (const x of [280, 360]) { ctx.beginPath(); ctx.arc(x, y, 3, 0, TAU); ctx.fill(); }
                // Pilares de laca vermelha com anéis de ouro e o brilho da laca.
                for (const px of [40, 560]) {
                    ctx.fillStyle = grad(ctx, px, 0, px + 44, 0, [[0, '#4a0f0a'], [0.3, '#a8261a'], [0.55, '#c93a25'], [1, '#5a140e']]); ctx.fillRect(px, 100, 44, CHAO_TOPO - 90);
                    ctx.fillStyle = 'rgba(255,220,180,0.18)'; ctx.fillRect(px + 14, 100, 5, CHAO_TOPO - 90);
                    for (const y of [114, 160, CHAO_TOPO - 22]) { ctx.fillStyle = grad(ctx, 0, y, 0, y + 10, [[0, '#ffe9a0'], [0.5, '#d4af37'], [1, '#7a5a10']]); ctx.fillRect(px - 5, y, 54, 10); }
                    ctx.fillStyle = grad(ctx, 0, CHAO_TOPO - 12, 0, CHAO_TOPO + 10, [[0, '#6a6a78'], [1, '#2a2a34']]); ctx.fillRect(px - 8, CHAO_TOPO - 12, 60, 22);
                }
                // Beiral do telhado: fiadas de telha retas, cumeeira dourada e a sombra que ele joga no muro.
                ctx.fillStyle = grad(ctx, 0, 112, 0, 152, [[0, '#33334a'], [1, '#141420']]); ctx.fillRect(0, 112, L, 40);
                ctx.strokeStyle = 'rgba(0,0,0,0.45)'; ctx.lineWidth = 1;
                for (let y = 120; y < 150; y += 9) { ctx.beginPath(); for (let x = 0; x <= L; x += 14) { ctx.moveTo(x + 7, y); ctx.arc(x, y, 7, 0, Math.PI, true); } ctx.stroke(); }
                ctx.fillStyle = 'rgba(255,255,255,0.05)'; for (let y = 120; y < 150; y += 9) ctx.fillRect(0, y - 6, L, 1);
                ctx.fillStyle = grad(ctx, 0, 106, 0, 114, [[0, '#ffe9a0'], [0.5, '#d4af37'], [1, '#6a4a10']]); ctx.fillRect(0, 106, L, 8);
                ctx.fillStyle = '#0b0b14'; ctx.fillRect(0, 100, L, 6);
                ctx.fillStyle = grad(ctx, 0, 152, 0, 176, [[0, 'rgba(0,0,0,0.6)'], [1, 'rgba(0,0,0,0)']]); ctx.fillRect(0, 152, L, 24);
                // Grade de madeira entre os pilares.
                ctx.fillStyle = '#3a2412'; ctx.fillRect(84, 262, 166, 6); ctx.fillRect(390, 262, 170, 6);
                for (let x = 96; x < 250; x += 22) ctx.fillRect(x, 262, 4, 40); for (let x = 402; x < 560; x += 22) ctx.fillRect(x, 262, 4, 40);
                ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.fillRect(84, 300, 166, 4); ctx.fillRect(390, 300, 170, 4);
                // Lanternas de papel penduradas (a luz delas é pintada por quadro).
                for (const lx of [170, 470]) {
                    ctx.strokeStyle = '#2a1a08'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(lx, 160); ctx.lineTo(lx, 190); ctx.stroke();
                    ctx.fillStyle = grad(ctx, lx - 16, 0, lx + 16, 0, [[0, '#b4401a'], [0.45, '#ff9a3a'], [0.6, '#ffb85a'], [1, '#a03a18']]); ctx.beginPath(); ctx.ellipse(lx, 214, 17, 24, 0, 0, TAU); ctx.fill();
                    ctx.strokeStyle = 'rgba(80,20,0,0.5)'; ctx.lineWidth = 1; for (let k = -2; k <= 2; k++) { ctx.beginPath(); ctx.ellipse(lx, 214, 17 - Math.abs(k) * 6.5, 24, 0, 0, TAU); ctx.stroke(); }
                    ctx.fillStyle = '#3a1a08'; ctx.fillRect(lx - 8, 188, 16, 4); ctx.fillRect(lx - 8, 236, 16, 4);
                    ctx.fillStyle = '#c9341f'; ctx.fillRect(lx - 2, 240, 4, 18);
                }
            } },
            luzes: [{ x: 170, y: 214, cor: '255,150,60', raio: 150, chao: true }, { x: 470, y: 214, cor: '255,150,60', raio: 150, chao: true }],
            chao: { base: [[0, '#6a6474'], [0.4, '#4a4655'], [1, '#221f2c']], ladrilho: 120, linha: 'rgba(0,0,0,0.32)', musgo: '#5a7a4a' },
            raios: true,
        },
        floresta: {
            ambiente: 'vagalume', gradacao: 'rgba(80,200,140,0.06)', nevoa: 'rgba(140,90,190,0.18)',
            fundo(ctx) {
                ctx.fillStyle = grad(ctx, 0, 0, 0, CHAO_TOPO, [[0, '#03100b'], [0.5, '#0a2a1c'], [0.8, '#2a2a4a'], [1, '#4a2a5a']]); ctx.fillRect(0, 0, LARGURA, CHAO_TOPO);
                const r = rng(21);
                for (const [cor, n, amp] of [['#0a2318', 14, 1], ['#061a10', 10, 1.25]]) {
                    for (let i = 0; i < n; i++) { const x = r() * LARGURA, larg = (26 + r() * 30) * amp, alt = (220 + r() * 100) * amp; ctx.fillStyle = cor; ctx.beginPath(); ctx.moveTo(x - larg / 2, CHAO_TOPO); ctx.lineTo(x - larg / 3, CHAO_TOPO - alt); ctx.lineTo(x + larg / 3, CHAO_TOPO - alt); ctx.lineTo(x + larg / 2, CHAO_TOPO); ctx.fill(); for (let k = 0; k < 4; k++) { ctx.beginPath(); ctx.ellipse(x + (r() - 0.5) * larg * 2, CHAO_TOPO - alt + k * 30, larg * (1.3 + r() * 0.6), 24, 0, 0, TAU); ctx.fill(); } }
                }
                ctx.fillStyle = grad(ctx, 0, 180, 0, CHAO_TOPO, [[0, 'rgba(120,70,170,0)'], [1, 'rgba(120,70,170,0.55)']]); ctx.fillRect(0, 180, LARGURA, CHAO_TOPO - 180);
            },
            meio: { largura: 720, desenhar(ctx) {
                for (const [x, larg, semente] of [[60, 74, 1], [330, 96, 2], [560, 60, 3]]) {
                    const rr = rng(semente);
                    ctx.fillStyle = grad(ctx, x, 0, x + larg, 0, [[0, '#0e140a'], [0.3, '#2c3d1c'], [0.55, '#4a5e2e'], [1, '#0b1006']]);
                    ctx.beginPath(); ctx.moveTo(x - 14, CHAO_TOPO + 12); ctx.lineTo(x + larg + 14, CHAO_TOPO + 12); ctx.lineTo(x + larg * 0.85, 0); ctx.lineTo(x + larg * 0.15, 0); ctx.fill();
                    // Casca: sulcos verticais e nós.
                    ctx.strokeStyle = 'rgba(0,0,0,0.35)'; ctx.lineWidth = 2;
                    for (let k = 0; k < 6; k++) { const sx = x + 8 + rr() * (larg - 16); ctx.beginPath(); ctx.moveTo(sx, rr() * 60); ctx.quadraticCurveTo(sx + (rr() - 0.5) * 20, 180, sx + (rr() - 0.5) * 12, CHAO_TOPO); ctx.stroke(); }
                    ctx.fillStyle = 'rgba(0,0,0,0.4)'; for (let k = 0; k < 2; k++) { ctx.beginPath(); ctx.ellipse(x + 10 + rr() * (larg - 20), 80 + rr() * 200, 6, 10, 0, 0, TAU); ctx.fill(); }
                    // Raízes no pé e musgo do lado da sombra.
                    ctx.fillStyle = '#1a2410'; for (let k = -2; k <= 2; k++) { ctx.beginPath(); ctx.moveTo(x + larg / 2 + k * larg * 0.3, CHAO_TOPO - 20); ctx.quadraticCurveTo(x + larg / 2 + k * larg * 0.6, CHAO_TOPO + 5, x + larg / 2 + k * larg * 0.9, CHAO_TOPO + 14); ctx.lineTo(x + larg / 2 + k * larg * 0.7, CHAO_TOPO + 14); ctx.fill(); }
                    ctx.fillStyle = 'rgba(80,140,50,0.35)'; ctx.beginPath(); ctx.ellipse(x + larg * 0.25, CHAO_TOPO - 60, 10, 50, 0, 0, TAU); ctx.fill();
                    // Luz de borda dos cogumelos no pé do tronco.
                    ctx.fillStyle = grad(ctx, 0, CHAO_TOPO - 120, 0, CHAO_TOPO + 10, [[0, 'rgba(180,110,255,0)'], [1, 'rgba(180,110,255,0.35)']]); ctx.fillRect(x - 14, CHAO_TOPO - 120, larg + 28, 130);
                    // Cipós com folhas.
                    ctx.strokeStyle = '#4d7a2a'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x + larg * 0.4, 30); ctx.bezierCurveTo(x + larg, 120, x - 10, 200, x + larg * 0.6, 300); ctx.stroke();
                    ctx.fillStyle = '#5c8f30'; for (let k = 0; k < 7; k++) { const t = k / 7; ctx.beginPath(); ctx.ellipse(x + larg * 0.4 + Math.sin(t * 9) * 26, 30 + t * 270, 7, 3.5, t * 3, 0, TAU); ctx.fill(); }
                    // Cogumelos que brilham (luz por quadro).
                    ctx.fillStyle = '#b06bff'; for (let k = 0; k < 3; k++) { ctx.beginPath(); ctx.ellipse(x + 4 + k * 8, CHAO_TOPO - 4 - k * 5, 5, 3, 0, Math.PI, 0); ctx.fill(); ctx.fillStyle = '#e8d6ff'; ctx.fillRect(x + 2 + k * 8, CHAO_TOPO - 4 - k * 5, 3, 6); ctx.fillStyle = '#b06bff'; }
                }
            } },
            copa(ctx, L) {
                // Copa: cachos de folhas escuros que pendem do alto, na frente dos troncos.
                const rr = rng(5);
                for (let i = 0; i < 26; i++) { const x = rr() * L, y = -10 + rr() * 90, t = 30 + rr() * 40; ctx.fillStyle = i % 2 ? '#0d2a16' : '#123a1e'; ctx.beginPath(); ctx.ellipse(x, y, t, t * 0.55, rr() * 0.6, 0, TAU); ctx.fill(); }
                ctx.fillStyle = grad(ctx, 0, 0, 0, 120, [[0, 'rgba(3,16,11,0.9)'], [1, 'rgba(3,16,11,0)']]); ctx.fillRect(0, 0, L, 120);
            },
            luzes: [{ x: 70, y: CHAO_TOPO - 8, cor: '180,110,255', raio: 90, chao: true }, { x: 340, y: CHAO_TOPO - 8, cor: '180,110,255', raio: 110, chao: true }, { x: 570, y: CHAO_TOPO - 8, cor: '180,110,255', raio: 80, chao: true }],
            chao: { base: [[0, '#3a5a2c'], [0.4, '#2a4020'], [1, '#0e160a']], ladrilho: 0, linha: 'rgba(0,0,0,0.2)', musgo: '#6a9a40', raizes: true },
        },
        poco: {
            ambiente: 'esporo', gradacao: 'rgba(40,220,140,0.08)', nevoa: 'rgba(40,200,120,0.14)',
            fundo(ctx) {
                ctx.fillStyle = grad(ctx, 0, 0, 0, CHAO_TOPO, [[0, '#010203'], [0.7, '#07120f'], [1, '#0e2a22']]); ctx.fillRect(0, 0, LARGURA, CHAO_TOPO);
                const r = rng(33);
                for (const [cor, n, alt] of [['#0b1214', 30, 150], ['#050a0b', 22, 100]]) { ctx.fillStyle = cor; for (let i = 0; i < n; i++) { const x = r() * LARGURA, l = 10 + r() * 28, a = 30 + r() * alt; ctx.beginPath(); ctx.moveTo(x - l, 0); ctx.lineTo(x + l, 0); ctx.lineTo(x, a); ctx.fill(); } }
                for (const [x, l] of [[200, 130], [620, 170], [880, 100]]) {
                    ctx.fillStyle = radial(ctx, x, CHAO_TOPO - 6, 10, l, [[0, 'rgba(60,255,160,0.5)'], [1, 'rgba(40,220,140,0)']]); ctx.fillRect(x - l, CHAO_TOPO - 80, l * 2, 90);
                    ctx.fillStyle = 'rgba(90,255,180,0.7)'; ctx.beginPath(); ctx.ellipse(x, CHAO_TOPO - 6, l, 9, 0, 0, TAU); ctx.fill();
                }
                ctx.fillStyle = '#d9d2c0'; for (const [x, y] of [[420, 318], [760, 322], [90, 326]]) { ctx.beginPath(); ctx.arc(x, y, 7, 0, TAU); ctx.fill(); ctx.fillStyle = '#1a1a1a'; ctx.fillRect(x - 4, y - 3, 2.5, 2.5); ctx.fillRect(x + 1.5, y - 3, 2.5, 2.5); ctx.fillStyle = '#d9d2c0'; ctx.fillRect(x - 12, y + 5, 24, 2.5); }
            },
            meio: { largura: 640, desenhar(ctx) {
                for (const [x, larg] of [[40, 100], [380, 130]]) {
                    ctx.fillStyle = grad(ctx, x, 0, x + larg, 0, [[0, '#0b1012'], [0.35, '#2c383e'], [0.55, '#3d4c54'], [1, '#080c0e']]); ctx.beginPath(); ctx.moveTo(x - 8, CHAO_TOPO + 12); ctx.lineTo(x + larg + 8, CHAO_TOPO + 12); ctx.lineTo(x + larg - 10, 0); ctx.lineTo(x + 10, 0); ctx.fill();
                    ctx.strokeStyle = 'rgba(60,255,160,0.55)'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(x + 30, 40); ctx.lineTo(x + 50, 120); ctx.lineTo(x + 38, 200); ctx.lineTo(x + 70, 300); ctx.stroke();
                    ctx.strokeStyle = 'rgba(0,0,0,0.45)'; ctx.lineWidth = 2; for (let k = 0; k < 5; k++) { ctx.beginPath(); ctx.moveTo(x + 12 + k * (larg / 5), 0); ctx.quadraticCurveTo(x + 20 + k * (larg / 5), 150, x + 8 + k * (larg / 5), CHAO_TOPO); ctx.stroke(); }
                    // Corrente pendurada e caveiras empilhadas no pé.
                    ctx.strokeStyle = '#5a6068'; ctx.lineWidth = 3; ctx.setLineDash([6, 4]); ctx.beginPath(); ctx.moveTo(x + larg - 20, 0); ctx.lineTo(x + larg - 26, 140); ctx.stroke(); ctx.setLineDash([]);
                    ctx.fillStyle = '#cfc7b4'; for (const [dx, dy] of [[larg + 10, -6], [larg + 26, -4], [larg + 18, -18]]) { ctx.beginPath(); ctx.arc(x + dx, CHAO_TOPO + dy, 8, 0, TAU); ctx.fill(); ctx.fillStyle = '#111'; ctx.fillRect(x + dx - 5, CHAO_TOPO + dy - 3, 3, 3); ctx.fillRect(x + dx + 2, CHAO_TOPO + dy - 3, 3, 3); ctx.fillStyle = '#cfc7b4'; }
                }
            } },
            luzes: [{ x: 200, y: CHAO_TOPO - 6, cor: '60,255,160', raio: 170, chao: true, fundo: true }, { x: 620, y: CHAO_TOPO - 6, cor: '60,255,160', raio: 200, chao: true, fundo: true }],
            chao: { base: [[0, '#3a4248'], [0.4, '#262c31'], [1, '#0b0e10']], ladrilho: 0, linha: 'rgba(40,220,140,0.1)', pocas: true },
        },
        torre: {
            ambiente: 'brasa', gradacao: 'rgba(160,80,255,0.08)', nevoa: 'rgba(120,60,160,0.12)',
            fundo(ctx) {
                ctx.fillStyle = grad(ctx, 0, 0, 0, CHAO_TOPO, [[0, '#0e0616'], [0.6, '#2a1238'], [1, '#3a1a48']]); ctx.fillRect(0, 0, LARGURA, CHAO_TOPO);
                ctx.strokeStyle = 'rgba(0,0,0,0.3)'; ctx.lineWidth = 1; for (let y = 0; y < CHAO_TOPO; y += 26) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(LARGURA, y); ctx.stroke(); const off = ((y / 26) % 2) * 30; for (let x = off; x < LARGURA; x += 60) { ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x, y + 26); ctx.stroke(); } }
                for (let x = 120; x < LARGURA; x += 320) {
                    ctx.fillStyle = grad(ctx, 0, 40, 0, 262, [[0, '#6a3aa0'], [0.5, '#3a1a5a'], [1, '#12081c']]); ctx.beginPath(); ctx.moveTo(x, 262); ctx.lineTo(x, 100); ctx.arc(x + 40, 100, 40, Math.PI, 0); ctx.lineTo(x + 80, 262); ctx.fill();
                    ctx.fillStyle = 'rgba(220,200,255,0.12)'; ctx.beginPath(); ctx.moveTo(x + 6, 262); ctx.lineTo(x + 6, 100); ctx.arc(x + 40, 100, 34, Math.PI, 0); ctx.lineTo(x + 74, 262); ctx.fill();
                    ctx.fillStyle = '#0d0514'; ctx.fillRect(x + 38, 60, 4, 202); ctx.fillRect(x, 170, 80, 4);
                    ctx.fillStyle = grad(ctx, 0, 262, 0, 280, [[0, '#7a6a9a'], [1, '#2a1a3a']]); ctx.fillRect(x - 8, 262, 96, 14);
                }
            },
            meio: { largura: 640, desenhar(ctx) {
                for (const x of [40, 360]) {
                    ctx.fillStyle = grad(ctx, x, 0, x + 56, 0, [[0, '#1e0e28'], [0.35, '#5a2f6e'], [0.55, '#7a4a90'], [1, '#170a20']]); ctx.fillRect(x, 0, 56, CHAO_TOPO + 12);
                    ctx.fillStyle = 'rgba(255,255,255,0.06)'; for (let k = 0; k < 4; k++) ctx.fillRect(x + 8 + k * 12, 0, 3, CHAO_TOPO);
                    ctx.fillStyle = grad(ctx, 0, 0, 0, 22, [[0, '#ffe9a0'], [0.5, '#d4af37'], [1, '#6a4a10']]); ctx.beginPath(); ctx.moveTo(x - 10, 0); ctx.lineTo(x + 66, 0); ctx.lineTo(x + 60, 22); ctx.lineTo(x - 4, 22); ctx.fill();
                    ctx.fillStyle = grad(ctx, 0, CHAO_TOPO - 18, 0, CHAO_TOPO + 12, [[0, '#d4af37'], [1, '#4a3410']]); ctx.fillRect(x - 8, CHAO_TOPO - 18, 72, 30);
                    // Tocha: suporte de ferro; a chama é animada por quadro.
                    ctx.fillStyle = '#2a2a30'; ctx.fillRect(x + 25, 168, 6, 44); ctx.beginPath(); ctx.moveTo(x + 18, 166); ctx.lineTo(x + 38, 166); ctx.lineTo(x + 34, 178); ctx.lineTo(x + 22, 178); ctx.fill();
                }
                // Estandarte com o sigilo.
                ctx.fillStyle = grad(ctx, 180, 0, 300, 0, [[0, '#3a0a12'], [0.5, '#6a1220'], [1, '#2a060c']]); ctx.beginPath(); ctx.moveTo(180, 20); ctx.lineTo(300, 20); ctx.lineTo(300, 240); ctx.lineTo(240, 272); ctx.lineTo(180, 240); ctx.fill();
                ctx.fillStyle = '#d4af37'; ctx.fillRect(172, 14, 136, 8);
                ctx.strokeStyle = '#d4af37'; ctx.lineWidth = 3; ctx.beginPath(); ctx.arc(240, 130, 36, 0, TAU); ctx.stroke(); ctx.beginPath(); ctx.moveTo(240, 94); ctx.lineTo(240, 166); ctx.moveTo(204, 130); ctx.lineTo(276, 130); ctx.moveTo(215, 105); ctx.lineTo(265, 155); ctx.stroke();
                ctx.fillStyle = 'rgba(0,0,0,0.25)'; ctx.fillRect(180, 20, 30, 220);
            } },
            luzes: [{ x: 68, y: 160, cor: '255,160,60', raio: 130, chao: true, chama: true }, { x: 388, y: 160, cor: '255,160,60', raio: 130, chao: true, chama: true }],
            chao: { base: [[0, '#4a3858'], [0.4, '#2c2036'], [1, '#120c18']], ladrilho: 100, linha: 'rgba(212,175,55,0.14)', tapete: '#6a1020', reflexo: true },
        },
    };

    // ── CACHE DAS CAMADAS ESTÁTICAS ───────────────────────────────────────────────────────
    const cache = {};
    function camadas(nome) {
        if (cache[nome]) return cache[nome];
        const def = CENARIOS[nome] || CENARIOS.patio;
        const fundo = canvasFora(LARGURA, CHAO_TOPO); def.fundo(fundo.getContext('2d'));
        const meio = canvasFora(def.meio.largura, CHAO_TOPO + 12); def.meio.desenhar(meio.getContext('2d'));
        if (def.copa) def.copa(meio.getContext('2d'), def.meio.largura);
        // Chão: um ladrilho que repete, com textura de ruído multiplicada.
        const larguraChao = 480;
        const chao = canvasFora(larguraChao, ALTURA_CHAO);
        const c = chao.getContext('2d');
        c.fillStyle = grad(c, 0, 0, 0, ALTURA_CHAO, def.chao.base); c.fillRect(0, 0, larguraChao, ALTURA_CHAO);
        c.save(); c.globalCompositeOperation = 'multiply'; c.globalAlpha = 0.5; c.fillStyle = c.createPattern(ruido(), 'repeat'); c.fillRect(0, 0, larguraChao, ALTURA_CHAO); c.restore();
        if (def.chao.tapete) { c.fillStyle = grad(c, 0, 70, 0, 130, [[0, '#8a1a2a'], [0.5, def.chao.tapete], [1, '#3a0810']]); c.fillRect(0, 70, larguraChao, 60); c.fillStyle = '#d4af37'; c.fillRect(0, 70, larguraChao, 2); c.fillRect(0, 128, larguraChao, 2); c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(0, 72, larguraChao, 4); }
        c.strokeStyle = def.chao.linha; c.lineWidth = 1.2;
        if (def.chao.ladrilho) {
            for (let k = 1; k < 5; k++) { const y = ALTURA_CHAO * (k / 5); c.beginPath(); c.moveTo(0, y); c.lineTo(larguraChao, y); c.stroke(); }
            for (let x = 0; x <= larguraChao; x += def.chao.ladrilho) { c.beginPath(); c.moveTo(x, 0); c.lineTo(x - 30, ALTURA_CHAO); c.stroke(); }
            c.fillStyle = 'rgba(255,255,255,0.05)'; for (let k = 1; k < 5; k++) c.fillRect(0, ALTURA_CHAO * (k / 5) + 1, larguraChao, 1);
        }
        const r = rng(nome.length * 77);
        if (def.chao.musgo) { c.globalCompositeOperation = 'multiply'; for (let i = 0; i < 7; i++) { const x = r() * larguraChao, y = r() * ALTURA_CHAO; c.fillStyle = radial(c, x, y, 2, 26 + r() * 30, [[0, def.chao.musgo], [1, 'rgba(255,255,255,0)']]); c.globalAlpha = 0.5 + r() * 0.3; c.beginPath(); c.ellipse(x, y, 30 + r() * 30, 7 + r() * 8, r() * 3, 0, TAU); c.fill(); } c.globalAlpha = 1; c.globalCompositeOperation = 'source-over'; }
        if (def.chao.raizes) { c.strokeStyle = 'rgba(20,30,10,0.6)'; c.lineWidth = 4; for (let i = 0; i < 5; i++) { const x = r() * larguraChao, y = r() * ALTURA_CHAO; c.beginPath(); c.moveTo(x, y); c.quadraticCurveTo(x + 40, y + 10 - r() * 20, x + 90, y + 6); c.stroke(); } }
        if (def.chao.pocas) { for (let i = 0; i < 4; i++) { const x = r() * larguraChao, y = 40 + r() * (ALTURA_CHAO - 60); c.fillStyle = 'rgba(20,60,50,0.55)'; c.beginPath(); c.ellipse(x, y, 30 + r() * 30, 6 + r() * 5, 0, 0, TAU); c.fill(); c.fillStyle = 'rgba(120,255,200,0.12)'; c.beginPath(); c.ellipse(x - 8, y - 2, 12, 2, 0, 0, TAU); c.fill(); } }
        // Rachaduras finas.
        c.strokeStyle = 'rgba(0,0,0,0.3)'; c.lineWidth = 1; for (let i = 0; i < 6; i++) { const x = r() * larguraChao, y = r() * ALTURA_CHAO; c.beginPath(); c.moveTo(x, y); c.lineTo(x + 10 - r() * 20, y + 8 + r() * 12); c.lineTo(x + 4 - r() * 8, y + 20 + r() * 14); c.stroke(); }
        return (cache[nome] = { def, fundo, meio, chao });
    }

    // ── DESENHO POR QUADRO ────────────────────────────────────────────────────────────────
    function tremular(tempo, semente) { return 0.82 + Math.sin(tempo * 11 + semente) * 0.07 + Math.sin(tempo * 23.7 + semente * 2) * 0.06 + Math.sin(tempo * 3.1 + semente) * 0.05; }

    function desenhar(ctx, nome, cameraX, tempo, o) {
        o = o || {};
        const alta = o.qualidade !== 'media';
        const { def, fundo, meio, chao } = camadas(nome);
        // Fundo (paralaxe 0.12) e nuvens/raios no pátio.
        const dFundo = -((cameraX * 0.12) % LARGURA);
        ctx.drawImage(fundo, Math.round(dFundo), 0); ctx.drawImage(fundo, Math.round(dFundo + LARGURA), 0);
        if (nome === 'torre' && Math.sin(tempo * 1.7) > 0.985) { ctx.fillStyle = 'rgba(220,200,255,0.4)'; ctx.fillRect(0, 0, LARGURA, CHAO_TOPO); }
        if (alta && def.raios) {
            ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.translate(760 + dFundo * 0.3, 84);
            for (let k = 0; k < 5; k++) { const a = 0.55 + k * 0.28 + Math.sin(tempo * 0.15 + k) * 0.05; ctx.rotate(0); ctx.fillStyle = `rgba(255,230,170,${0.035 + Math.sin(tempo * 0.7 + k * 1.3) * 0.015})`; ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(Math.cos(a) * 700, Math.sin(a) * 700); ctx.lineTo(Math.cos(a + 0.09) * 700, Math.sin(a + 0.09) * 700); ctx.closePath(); ctx.fill(); }
            ctx.restore();
        }
        if (nome === 'patio' || nome === 'torre') {
            // Nuvens finas passando na frente da lua.
            ctx.save(); ctx.globalAlpha = 0.16; ctx.fillStyle = '#c8c0e0';
            for (let k = 0; k < 4; k++) { const x = ((tempo * 6 + k * 300 - cameraX * 0.05) % (LARGURA + 300)) - 150; ctx.beginPath(); ctx.ellipse(x, 60 + k * 30, 140, 12 + k * 3, 0, 0, TAU); ctx.fill(); }
            ctx.restore();
        }
        // Meio (paralaxe 0.5), em ladrilhos.
        const l = meio.width;
        const dMeio = -((cameraX * 0.5) % l);
        for (let x = dMeio - l; x < LARGURA + l; x += l) ctx.drawImage(meio, Math.round(x), 0);
        // Chão (paralaxe 1), em ladrilhos.
        const lc = chao.width;
        const dChao = -((cameraX) % lc);
        for (let x = dChao - lc; x < LARGURA + lc; x += lc) ctx.drawImage(chao, Math.round(x), CHAO_TOPO);
        // Sombra no pé do muro e a névoa de trás dos lutadores.
        ctx.fillStyle = grad(ctx, 0, CHAO_TOPO, 0, CHAO_TOPO + 50, [[0, 'rgba(0,0,0,0.5)'], [1, 'rgba(0,0,0,0)']]); ctx.fillRect(0, CHAO_TOPO, LARGURA, 50);
        if (alta) nevoa(ctx, def, cameraX, tempo, 0);
        // LUZES: pintam parede e chão, tremulam.
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        for (let x = dMeio - l; x < LARGURA + l; x += l) {
            for (const luz of def.luzes) {
                const lx = x + luz.x, k = tremular(tempo, luz.x + x);
                if (lx < -luz.raio || lx > LARGURA + luz.raio) continue;
                ctx.fillStyle = radial(ctx, lx, luz.y, 2, luz.raio, [[0, `rgba(${luz.cor},${0.7 * k})`], [0.35, `rgba(${luz.cor},${0.26 * k})`], [0.7, `rgba(${luz.cor},${0.07 * k})`], [1, `rgba(${luz.cor},0)`]]);
                ctx.fillRect(lx - luz.raio, luz.y - luz.raio, luz.raio * 2, luz.raio * 2);
                if (luz.chao) {
                    const cy = CHAO_TOPO + 40 + (luz.y < CHAO_TOPO - 40 ? 30 : 0);
                    ctx.save(); ctx.translate(lx, cy); ctx.scale(1, 0.32);
                    ctx.fillStyle = radial(ctx, 0, 0, 4, luz.raio * 1.4, [[0, `rgba(${luz.cor},${0.5 * k})`], [0.5, `rgba(${luz.cor},${0.14 * k})`], [1, `rgba(${luz.cor},0)`]]);
                    ctx.fillRect(-luz.raio * 1.4, -luz.raio * 1.4, luz.raio * 2.8, luz.raio * 2.8); ctx.restore();
                }
                if (luz.chama) chama(ctx, lx, luz.y, tempo, luz.x + x);
            }
        }
        ctx.restore();
    }

    function chama(ctx, x, y, tempo, semente) {
        for (let k = 0; k < 3; k++) {
            const h = 22 - k * 6, w = 9 - k * 2.5;
            const dx = Math.sin(tempo * 13 + semente + k) * 2.5;
            ctx.fillStyle = k === 0 ? 'rgba(255,120,30,0.9)' : k === 1 ? 'rgba(255,190,70,0.95)' : 'rgba(255,245,200,1)';
            ctx.beginPath(); ctx.moveTo(x - w, y + 6); ctx.quadraticCurveTo(x - w + dx, y - h * 0.5, x + dx * 1.5, y - h); ctx.quadraticCurveTo(x + w + dx, y - h * 0.5, x + w, y + 6); ctx.fill();
        }
    }

    function nevoa(ctx, def, cameraX, tempo, camada) {
        ctx.save();
        const y0 = camada === 0 ? CHAO_TOPO - 30 : ALTURA - 90, alt = camada === 0 ? 70 : 90;
        for (let k = 0; k < 3; k++) {
            const x = ((tempo * (10 + k * 6) - cameraX * (camada === 0 ? 0.6 : 1.1) * 0.5 + k * 420) % (LARGURA + 600)) - 300;
            ctx.fillStyle = radial(ctx, x, y0 + alt / 2, 10, 300, [[0, def.nevoa], [1, 'rgba(0,0,0,0)']]);
            ctx.save(); ctx.translate(x, y0 + alt / 2); ctx.scale(1, alt / 600); ctx.translate(-x, -(y0 + alt / 2));
            ctx.fillRect(x - 300, y0 + alt / 2 - 300, 600, 600); ctx.restore();
        }
        ctx.restore();
    }

    // Na frente dos lutadores: névoa rasteira.
    function frente(ctx, nome, cameraX, tempo, o) {
        if (o && o.qualidade === 'media') return;
        nevoa(ctx, (CENARIOS[nome] || CENARIOS.patio), cameraX, tempo, 1);
    }

    // Pós-processamento: vinheta e gradação vêm de UM canvas cacheado por cenário (era um
    // `soft-light` e um gradiente radial de tela cheia por quadro — caro demais em 2× DPR); o grão
    // continua por quadro, só na qualidade alta.
    const cachePos = {};
    function vinhetaDe(nome) {
        if (cachePos[nome]) return cachePos[nome];
        const def = CENARIOS[nome] || CENARIOS.patio;
        const c = canvasFora(LARGURA, ALTURA), x = c.getContext('2d');
        x.fillStyle = def.gradacao; x.fillRect(0, 0, LARGURA, ALTURA);
        x.fillStyle = radial(x, LARGURA / 2, ALTURA / 2, ALTURA * 0.45, ALTURA * 1.05, [[0, 'rgba(0,0,0,0)'], [1, 'rgba(0,0,0,0.55)']]); x.fillRect(0, 0, LARGURA, ALTURA);
        return (cachePos[nome] = c);
    }
    let grao = 0;
    function pos(ctx, nome, tempo, o) {
        ctx.save();
        ctx.drawImage(vinhetaDe(nome), 0, 0);
        if (!(o && o.qualidade === 'media')) {
            grao = (grao + 37) % 256;
            ctx.globalCompositeOperation = 'overlay'; ctx.globalAlpha = 0.07;
            ctx.translate(-grao, -((grao * 3) % 256)); ctx.fillStyle = ctx.createPattern(ruido(), 'repeat'); ctx.fillRect(0, 0, LARGURA + 256, ALTURA + 256);
        }
        ctx.restore();
    }

    function ambiente(nome) { return (CENARIOS[nome] || CENARIOS.patio).ambiente; }

    raiz.PunhosDeShaolin.Cenario = { desenhar, frente, pos, ambiente, CENARIOS };
})(window);
