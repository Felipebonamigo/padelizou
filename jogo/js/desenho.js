// PUNHOS DE SHAOLIN — desenho. Tudo em Canvas 2D e à mão: nenhum sprite, nenhuma imagem.
//
// Os lutadores são bonecos articulados (quadril, tronco, cabeça, dois braços, duas pernas)
// desenhados com linhas grossas de ponta redonda. Cada estado do motor vira uma POSE — ângulos
// das juntas em função do tempo no estado — e as poses se misturam nas transições. A cara de
// cada personagem vem das cores do `def` e de meia dúzia de enfeites (capuz, bastão, lâminas).
//
// Também moram aqui: os cenários com paralaxe (cacheados em canvas fora da tela), os efeitos
// (faíscas, brasas, poeira, textos que sobem, tremor, congelamento), o HUD e as telas de menu.
(function (raiz) {
    'use strict';
    const Motor = raiz.PunhosDeShaolin.Motor;
    const { LARGURA, ALTURA, CHAO_TOPO, CHAO_BASE } = Motor;
    const FONTE_TITULO = '"Cinzel", "Times New Roman", Georgia, serif';
    const FONTE_HUD = '"Chakra Petch", "Trebuchet MS", "Segoe UI", sans-serif';
    const TAU = Math.PI * 2;

    function telaY(y) { return CHAO_TOPO + y * (CHAO_BASE - CHAO_TOPO); }
    function suave(k) { k = Math.min(1, Math.max(0, k)); return k * k * (3 - 2 * k); }
    function lerp(a, b, k) { return a + (b - a) * k; }
    function limitar(v, a, b) { return Math.min(b, Math.max(a, v)); }

    // ── TEXTO ─────────────────────────────────────────────────────────────────────────────
    function radial(ctx, x, y, r0, r1, paradas) { const g = ctx.createRadialGradient(x, y, r0, x, y, r1); for (const [k, c] of paradas) g.addColorStop(k, c); return g; }

    function texto(ctx, str, x, y, o) {
        o = o || {};
        ctx.save();
        ctx.font = `${o.italico ? 'italic ' : ''}${o.peso || 700} ${o.tamanho || 16}px ${o.fonte || FONTE_HUD}`;
        ctx.textAlign = o.alinhar || 'left';
        ctx.textBaseline = o.base || 'alphabetic';
        if (o.brilho) { ctx.shadowColor = o.brilho; ctx.shadowBlur = o.brilhoTamanho || 18; }
        if (o.contorno) { ctx.lineWidth = o.contornoLargura || Math.max(2, (o.tamanho || 16) / 8); ctx.strokeStyle = o.contorno; ctx.lineJoin = 'round'; ctx.strokeText(str, x, y); }
        ctx.fillStyle = o.cor || '#fff';
        ctx.fillText(str, x, y);
        ctx.restore();
    }

    // ── LUTADORES (a figura mora em figura.js; o cenário em cenario.js) ─────────────────
    const Figura = raiz.PunhosDeShaolin.Figura;
    const Cenario = raiz.PunhosDeShaolin.Cenario;
    function canvasFora(l, a) { const c = document.createElement('canvas'); c.width = l; c.height = a; return c; }

    // Tingido num canvas à parte — um `source-atop` direto na tela pintaria o cenário atrás.
    let telaDeTingir = null;
    function desenharFiguraTingida(ctx, ent, tempo, virado, cor) {
        if (!telaDeTingir) telaDeTingir = canvasFora(520, 520);
        const c2 = telaDeTingir.getContext('2d');
        c2.setTransform(1, 0, 0, 1, 0, 0); c2.clearRect(0, 0, 520, 520);
        c2.translate(260, 470); c2.scale(virado, 1);
        Figura.desenhar(c2, ent, tempo, { virado });
        c2.setTransform(1, 0, 0, 1, 0, 0);
        c2.globalCompositeOperation = 'source-atop'; c2.fillStyle = cor; c2.fillRect(0, 0, 520, 520);
        c2.globalCompositeOperation = 'source-over';
        ctx.drawImage(telaDeTingir, -260, -470);
    }

    function desenharLutador(ctx, ent, cameraX, tempo, qualidade) {
        const sx = ent.x - cameraX, sy = telaY(ent.y);
        const virado = ent.virado;
        ctx.save();
        ctx.translate(sx, sy);
        // Sombra de contato + sombra projetada (deitada no chão, pro lado oposto da luz).
        ctx.fillStyle = 'rgba(0,0,0,0.35)';
        const enc = Math.max(0.35, 1 - ent.z / 300);
        ctx.beginPath(); ctx.ellipse(0, 0, 24 * ent.escala * enc, 6 * ent.escala * enc, 0, 0, TAU); ctx.fill();
        if (ent.estado !== 'morto' || ent.morteHa < 0.7) Figura.sombraProjetada(ctx, ent, tempo, virado, qualidade);
        ctx.translate(0, -ent.z);
        let alpha = 1;
        if (ent.estado === 'morto') alpha = ent.morteHa > 0.7 ? Math.max(0, 1 - (ent.morteHa - 0.7) / 0.9) : 1;
        if (ent.invulneravel > 0 && ent.estado !== 'morto' && ent.estado !== 'finalizando' && ent.estado !== 'finalizado' && Math.floor(tempo * 14) % 2 === 0) alpha *= 0.45;
        ctx.globalAlpha = alpha;
        Figura.rastro(ctx, ent, tempo, virado);
        if (ent.estado === 'atingido' && ent.quadro < 0.08) desenharFiguraTingida(ctx, ent, tempo, virado, 'rgba(255,60,40,0.55)');
        else if (ent.estado === 'finalizado' && ent.quadro > 0.6) desenharFiguraTingida(ctx, ent, tempo, virado, `rgba(255,40,20,${Math.min(0.8, (ent.quadro - 0.6) * 2)})`);
        else { ctx.scale(virado, 1); Figura.desenhar(ctx, ent, tempo, { virado }); }
        ctx.restore();
        // Barra de vida do inimigo (só de quem apanhou).
        if (ent.time === 'inimigo' && !ent.def.chefe && ent.vida < ent.vidaMax && ent.estado !== 'morto') {
            const l = 44 * ent.escala, y = sy - ent.z - 168 * ent.escala;
            ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.fillRect(sx - l / 2 - 1, y - 1, l + 2, 6);
            ctx.fillStyle = ent.estado === 'atordoado' ? '#ffd23a' : '#e33a2c'; ctx.fillRect(sx - l / 2, y, l * (ent.vida / ent.vidaMax), 4);
        }
    }

    // ── PROJÉTEIS, ITENS, OBJETOS ─────────────────────────────────────────────────────────
    function desenharProjetil(ctx, p, cameraX, tempo) {
        const sx = p.x - cameraX, sy = telaY(p.y) - p.z;
        ctx.save();
        ctx.translate(sx, sy);
        if (p.tipo === 'fogo') {
            ctx.shadowColor = '#ff7a1a'; ctx.shadowBlur = 24;
            const g = ctx.createRadialGradient(0, 0, 2, 0, 0, 20);
            g.addColorStop(0, '#fff6c8'); g.addColorStop(0.4, '#ff9a2a'); g.addColorStop(1, 'rgba(255,60,20,0)');
            ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, 0, 24, 16, 0, 0, TAU); ctx.fill();
            ctx.fillStyle = 'rgba(255,120,30,0.5)'; ctx.beginPath(); ctx.ellipse(-p.virado * 22, 0, 22, 9, 0, 0, TAU); ctx.fill();
        } else if (p.tipo === 'flecha') {
            ctx.rotate(p.virado === 1 ? 0 : Math.PI);
            segmento(ctx, -18, 0, 16, 0, 2.5, '#c9b27a'); ctx.fillStyle = '#e6e6e6'; ctx.beginPath(); ctx.moveTo(16, -4); ctx.lineTo(24, 0); ctx.lineTo(16, 4); ctx.fill();
            ctx.fillStyle = '#c0392b'; ctx.fillRect(-20, -3, 6, 6);
        } else if (p.tipo === 'shuriken') {
            ctx.rotate(tempo * 30);
            ctx.fillStyle = '#d0d4da';
            for (let k = 0; k < 4; k++) { ctx.rotate(Math.PI / 2); ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(14, -4); ctx.lineTo(16, 0); ctx.lineTo(14, 4); ctx.fill(); }
        } else if (p.tipo === 'caveira') {
            ctx.shadowColor = '#b06bff'; ctx.shadowBlur = 24;
            ctx.fillStyle = 'rgba(160,90,255,0.35)'; ctx.beginPath(); ctx.ellipse(-p.virado * 14, 0, 26, 12, 0, 0, TAU); ctx.fill();
            ctx.fillStyle = '#e9e3ff'; ctx.beginPath(); ctx.arc(0, -2, 11, 0, TAU); ctx.fill(); ctx.fillRect(-7, 6, 14, 6);
            ctx.fillStyle = '#4a1a7a'; ctx.fillRect(-7, -5, 5, 5); ctx.fillRect(2, -5, 5, 5); ctx.fillRect(-2, 2, 3, 3);
        }
        ctx.restore();
    }

    function desenharItem(ctx, it, cameraX, tempo) {
        const sx = it.x - cameraX, sy = telaY(it.y) - it.z - 6 - Math.sin(tempo * 4) * 3;
        ctx.save();
        ctx.fillStyle = 'rgba(0,0,0,0.3)'; ctx.beginPath(); ctx.ellipse(sx, telaY(it.y), 12, 4, 0, 0, TAU); ctx.fill();
        ctx.translate(sx, sy);
        if (it.tipo === 'cha') {
            ctx.shadowColor = '#7cff9a'; ctx.shadowBlur = 14;
            ctx.fillStyle = '#3f7d4a'; ctx.beginPath(); ctx.moveTo(-11, -6); ctx.lineTo(11, -6); ctx.lineTo(8, 8); ctx.lineTo(-8, 8); ctx.fill();
            ctx.fillStyle = '#9ef0a8'; ctx.fillRect(-10, -8, 20, 3);
            ctx.strokeStyle = '#3f7d4a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(13, 0, 5, -Math.PI / 2, Math.PI / 2); ctx.stroke();
        } else {
            ctx.shadowColor = '#7ad0ff'; ctx.shadowBlur = 14;
            ctx.fillStyle = '#f2e6c8'; ctx.fillRect(-12, -8, 24, 16);
            ctx.fillStyle = '#7a3a1a'; ctx.fillRect(-14, -10, 4, 20); ctx.fillRect(10, -10, 4, 20);
            ctx.fillStyle = '#2b6fb3'; ctx.fillRect(-6, -4, 12, 2); ctx.fillRect(-6, 0, 10, 2); ctx.fillRect(-6, 4, 8, 2);
        }
        ctx.restore();
    }

    function desenharObjeto(ctx, ob, cameraX) {
        if (ob.estado !== 'parado') return;
        const sx = ob.x - cameraX, sy = telaY(ob.y);
        ctx.save();
        ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.beginPath(); ctx.ellipse(sx, sy, 20, 6, 0, 0, TAU); ctx.fill();
        ctx.translate(sx, sy);
        const g = ctx.createLinearGradient(-18, 0, 18, 0);
        g.addColorStop(0, '#5a2a1a'); g.addColorStop(0.5, '#a3522f'); g.addColorStop(1, '#4a1f12');
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.moveTo(-12, 0); ctx.bezierCurveTo(-24, -14, -20, -34, -8, -40); ctx.lineTo(8, -40); ctx.bezierCurveTo(20, -34, 24, -14, 12, 0); ctx.fill();
        ctx.fillStyle = '#d4af37'; ctx.fillRect(-10, -42, 20, 4);
        ctx.fillStyle = 'rgba(0,0,0,0.25)'; ctx.fillRect(-14, -22, 28, 3);
        ctx.restore();
    }

    // ── EFEITOS ───────────────────────────────────────────────────────────────────────────
    function criarEfeitos() {
        const ef = { particulas: [], textos: [], tremor: 0, flash: 0, flashCor: '#fff', congelar: 0, lento: 0, finalizacao: 0, siga: 0, chefeAviso: 0, chefeNome: '', ondaAviso: 0, ondaTexto: '', rng: Motor.criarRng(99) };

        function particula(p) { ef.particulas.push(Object.assign({ vx: 0, vy: 0, vz: 0, vida: 0.5, idade: 0, tam: 3, cor: '#fff', tipo: 'ponto', gravidade: 0 }, p)); }

        ef.processar = function (eventos, mundo, som) {
            for (const ev of eventos) {
                switch (ev.tipo) {
                    case 'som': if (som) som.tocar(ev.nome); break;
                    case 'acerto': {
                        const n = ev.forca === 'forte' ? 14 : ev.forca === 'bloqueio' ? 5 : 8;
                        for (let k = 0; k < n; k++) {
                            const a = ef.rng.entre(0, TAU), v = ef.rng.entre(80, ev.forca === 'forte' ? 420 : 240);
                            particula({ x: ev.x, y: ev.y, z: ev.z, vx: Math.cos(a) * v + (ev.direcao || 0) * 120, vz: Math.sin(a) * v * 0.6 + 60, vida: ef.rng.entre(0.18, 0.4), tam: ef.rng.entre(1.5, 3.5), cor: ev.bloqueado ? '#9ad7ff' : (k % 3 ? '#ffb347' : '#fff3c4'), tipo: 'faisca', gravidade: 600 });
                        }
                        if (ev.forca === 'forte') { ef.tremor = Math.max(ef.tremor, 5); ef.flash = Math.max(ef.flash, 0.12); ef.flashCor = '#fff'; }
                        break;
                    }
                    case 'congelar': ef.congelar = Math.max(ef.congelar, ev.segundos); break;
                    case 'tremor': ef.tremor = Math.max(ef.tremor, 6 * ev.forca); break;
                    case 'texto': ef.textos.push({ texto: ev.texto, x: ev.x, y: ev.y, cor: ev.cor, idade: 0, tela: !!ev.tela }); break;
                    case 'poeira': for (let k = 0; k < 7; k++) particula({ x: ev.x + ef.rng.entre(-14, 14), y: ev.y, z: 2, vx: ef.rng.entre(-90, 90), vz: ef.rng.entre(20, 80), vida: ef.rng.entre(0.3, 0.6), tam: ef.rng.entre(4, 9), cor: 'rgba(200,190,170,0.5)', tipo: 'poeira' }); break;
                    case 'quebra': for (let k = 0; k < 12; k++) particula({ x: ev.x, y: ev.y, z: 20, vx: ef.rng.entre(-220, 220), vz: ef.rng.entre(80, 320), vida: ef.rng.entre(0.4, 0.8), tam: ef.rng.entre(3, 6), cor: k % 2 ? '#a3522f' : '#d4af37', tipo: 'caco', gravidade: 900 }); break;
                    case 'morte': {
                        const n = ev.finalizado ? 60 : 16;
                        for (let k = 0; k < n; k++) {
                            const a = ef.rng.entre(0, TAU), v = ef.rng.entre(60, ev.finalizado ? 380 : 180);
                            particula({ x: ev.x, y: ev.y, z: ev.z + 40, vx: Math.cos(a) * v, vz: Math.abs(Math.sin(a)) * v + 80, vida: ef.rng.entre(0.5, 1.4), tam: ef.rng.entre(2, 5), cor: ev.finalizado ? (k % 2 ? '#ff4a2a' : '#ffd0a0') : '#ff6a3a', tipo: ev.finalizado ? 'alma' : 'brasa', gravidade: ev.finalizado ? -60 : 300 });
                        }
                        if (ev.finalizado) { ef.flash = 0.5; ef.flashCor = '#ff2a1a'; ef.tremor = 14; }
                        break;
                    }
                    case 'finalizacao': ef.finalizacao = 1.5; ef.lento = 1.2; ef.flash = 0.25; ef.flashCor = '#000'; break;
                    case 'teleporte': for (let k = 0; k < 18; k++) particula({ x: ev.x + ef.rng.entre(-10, 10), y: ev.y, z: ef.rng.entre(0, 80), vx: ef.rng.entre(-40, 40), vz: ef.rng.entre(40, 160), vida: ef.rng.entre(0.3, 0.7), tam: ef.rng.entre(2, 5), cor: '#b06bff', tipo: 'alma' }); break;
                    case 'projetil': for (let k = 0; k < 8; k++) particula({ x: ev.x, y: ev.y, z: 40, vx: ef.rng.entre(-60, 60), vz: ef.rng.entre(-30, 80), vida: 0.3, tam: 3, cor: '#ffb347', tipo: 'brasa' }); break;
                    case 'item': for (let k = 0; k < 10; k++) particula({ x: ev.x, y: ev.y, z: 10, vx: ef.rng.entre(-60, 60), vz: ef.rng.entre(60, 200), vida: 0.6, tam: 3, cor: ev.nome === 'cha' ? '#9ef0a8' : '#7ad0ff', tipo: 'alma', gravidade: -100 }); break;
                    case 'siga': ef.siga = 4; break;
                    case 'chefe': ef.chefeAviso = 3; ef.chefeNome = ev.nome; ef.tremor = 10; break;
                    case 'onda': ef.ondaAviso = 1.6; ef.ondaTexto = `ONDA ${ev.numero} / ${ev.total}`; break;
                    case 'renasceu': for (let k = 0; k < 20; k++) particula({ x: ev.x, y: ev.y, z: ef.rng.entre(0, 90), vx: ef.rng.entre(-30, 30), vz: ef.rng.entre(30, 120), vida: 0.8, tam: 3, cor: '#ffe680', tipo: 'alma' }); break;
                    case 'fase-concluida': ef.flash = 0.4; ef.flashCor = '#fff'; break;
                    default: break;
                }
            }
        };

        ef.atualizar = function (dt, mundo, cenario) {
            for (let k = ef.particulas.length - 1; k >= 0; k--) {
                const p = ef.particulas[k];
                p.idade += dt;
                if (p.idade >= p.vida) { ef.particulas.splice(k, 1); continue; }
                p.vz -= p.gravidade * dt; p.x += p.vx * dt; p.z += p.vz * dt; p.y += p.vy * dt;
                if (p.z < 0 && p.tipo !== 'alma') { p.z = 0; p.vz *= -0.3; p.vx *= 0.7; }
            }
            for (let k = ef.textos.length - 1; k >= 0; k--) { ef.textos[k].idade += dt; if (ef.textos[k].idade > 1.3) ef.textos.splice(k, 1); }
            ef.tremor = Math.max(0, ef.tremor - 30 * dt);
            ef.flash = Math.max(0, ef.flash - dt * 1.5);
            ef.finalizacao = Math.max(0, ef.finalizacao - dt);
            ef.lento = Math.max(0, ef.lento - dt);
            ef.siga = Math.max(0, ef.siga - dt);
            ef.chefeAviso = Math.max(0, ef.chefeAviso - dt);
            ef.ondaAviso = Math.max(0, ef.ondaAviso - dt);
            // Ambiente: brasas, vagalumes ou esporos, conforme o cenário.
            const amb = Cenario.ambiente(cenario);
            if (mundo && ef.rng.chance(dt * 6)) {
                const cor = amb === 'vagalume' ? '#c8ff8a' : amb === 'esporo' ? '#5affb0' : '#ff9a3a';
                particula({ x: mundo.camera.x + ef.rng.entre(-40, LARGURA + 40), y: ef.rng.entre(-1.5, 1.2), z: ef.rng.entre(0, 60), vx: ef.rng.entre(-15, 25), vz: ef.rng.entre(15, 45), vida: ef.rng.entre(2, 5), tam: ef.rng.entre(1.5, 3), cor, tipo: 'ambiente' });
            }
        };
        return ef;
    }

    function desenharParticulas(ctx, ef, cameraX) {
        for (const p of ef.particulas) {
            const k = 1 - p.idade / p.vida;
            const sx = p.x - cameraX, sy = telaY(p.y) - p.z;
            ctx.globalAlpha = p.tipo === 'ambiente' ? Math.min(1, k * 2) * 0.7 : k;
            ctx.fillStyle = p.cor;
            if (p.tipo === 'faisca') { ctx.strokeStyle = p.cor; ctx.lineWidth = p.tam; ctx.beginPath(); ctx.moveTo(sx, sy); ctx.lineTo(sx - p.vx * 0.02, sy + p.vz * 0.02); ctx.stroke(); }
            else if (p.tipo === 'poeira') { ctx.beginPath(); ctx.arc(sx, sy, p.tam * (1 + p.idade * 2), 0, TAU); ctx.fill(); }
            else if (p.tipo === 'caco') { ctx.fillRect(sx - p.tam / 2, sy - p.tam / 2, p.tam, p.tam); }
            else { ctx.shadowColor = p.cor; ctx.shadowBlur = p.tipo === 'alma' || p.tipo === 'ambiente' ? 8 : 0; ctx.beginPath(); ctx.arc(sx, sy, p.tam * (p.tipo === 'alma' ? k : 1) + 0.5, 0, TAU); ctx.fill(); ctx.shadowBlur = 0; }
        }
        ctx.globalAlpha = 1;
    }

    const CORES_TEXTO = { golpe: ['#fff7d6', '#7a2a0a'], combo: ['#ffb347', '#5a1a00'], especial: ['#7ae0ff', '#0a2a5a'], finalizacao: ['#ff3a2a', '#2a0000'], chefe: ['#ffd23a', '#3a2a00'] };
    function desenharTextos(ctx, ef, cameraX) {
        for (const t of ef.textos) {
            const [cor, contorno] = CORES_TEXTO[t.cor] || CORES_TEXTO.golpe;
            const escala = t.idade < 0.12 ? 0.4 + (t.idade / 0.12) * 0.8 : t.idade < 0.25 ? 1.2 - (t.idade - 0.12) / 0.13 * 0.2 : 1;
            const alpha = t.idade > 0.9 ? 1 - (t.idade - 0.9) / 0.4 : 1;
            const sx = t.tela ? LARGURA / 2 : t.x - cameraX, sy = t.tela ? 200 : telaY(t.y) - 120 - t.idade * 40;
            ctx.save(); ctx.globalAlpha = alpha; ctx.translate(sx, sy); ctx.scale(escala, escala);
            const grande = t.cor === 'finalizacao' || t.cor === 'chefe';
            texto(ctx, t.texto, 0, 0, { tamanho: grande ? 40 : 22, fonte: grande ? FONTE_TITULO : FONTE_HUD, peso: 900, italico: !grande, cor, contorno, contornoLargura: grande ? 6 : 4, alinhar: 'center', brilho: grande ? cor : null });
            ctx.restore();
        }
    }

    // ── HUD ───────────────────────────────────────────────────────────────────────────────
    const hudSuave = {};
    function barraVida(ctx, x, y, larg, alt, fracao, corCheia, corFundo, invertido, chave) {
        // A barra "atrasada" (dano recente em branco) some devagar — é o que faz o dano se ver.
        const anterior = hudSuave[chave] == null ? fracao : hudSuave[chave];
        hudSuave[chave] = anterior > fracao ? Math.max(fracao, anterior - 0.012) : fracao;
        ctx.save();
        ctx.beginPath();
        const inc = 10;
        if (!invertido) { ctx.moveTo(x, y); ctx.lineTo(x + larg, y); ctx.lineTo(x + larg - inc, y + alt); ctx.lineTo(x, y + alt); }
        else { ctx.moveTo(x + larg, y); ctx.lineTo(x, y); ctx.lineTo(x + inc, y + alt); ctx.lineTo(x + larg, y + alt); }
        ctx.closePath();
        ctx.fillStyle = 'rgba(0,0,0,0.65)'; ctx.fill();
        ctx.strokeStyle = 'rgba(255,255,255,0.35)'; ctx.lineWidth = 1.5; ctx.stroke();
        ctx.clip();
        const largAtras = larg * hudSuave[chave], largAgora = larg * fracao;
        ctx.fillStyle = '#fff';
        ctx.fillRect(invertido ? x + larg - largAtras : x, y, largAtras, alt);
        const g = ctx.createLinearGradient(0, y, 0, y + alt);
        g.addColorStop(0, corCheia); g.addColorStop(1, corFundo);
        ctx.fillStyle = g;
        ctx.fillRect(invertido ? x + larg - largAgora : x, y, largAgora, alt);
        ctx.restore();
    }

    function desenharHud(ctx, mundo, ef, tempo, extras) {
        const j1 = mundo.jogadores[0], j2 = mundo.jogadores[1];
        const painel = (j, x, invertido, rotulo, chave) => {
            const alinhar = invertido ? 'right' : 'left';
            const xTexto = invertido ? x + 300 : x;
            texto(ctx, `${rotulo} · ${j.def.nome.toUpperCase()}`, xTexto, 30, { tamanho: 16, cor: '#ffe9b0', contorno: '#000', alinhar, peso: 700 });
            barraVida(ctx, x, 38, 300, 18, j.vida / j.vidaMax, '#ff5a3a', '#a01a10', invertido, chave + 'vida');
            barraVida(ctx, invertido ? x + 60 : x, 62, 240, 8, j.chi / 100, '#7ae0ff', '#1e5fa8', invertido, chave + 'chi');
            texto(ctx, 'CHI', invertido ? x + 52 : x + 246, 70, { tamanho: 10, cor: '#9ad7ff', alinhar: invertido ? 'right' : 'left', peso: 700 });
            // Vidas: punhos.
            for (let k = 0; k < j.vidas; k++) {
                const px = invertido ? x + 300 - 14 - k * 20 : x + 8 + k * 20;
                ctx.fillStyle = '#ffd23a'; ctx.beginPath(); ctx.arc(px, 86, 6, 0, TAU); ctx.fill();
                ctx.fillStyle = '#7a4a00'; ctx.fillRect(px - 4, 84, 8, 1.5);
            }
            if (j.combo >= 2) {
                const escala = 1 + Math.max(0, 0.3 - (1.6 - j.comboTempo)) * 2;
                ctx.save(); ctx.translate(invertido ? x + 300 : x, 122); ctx.scale(escala, escala);
                texto(ctx, `${j.combo} GOLPES`, 0, 0, { tamanho: 26, cor: '#ffb347', contorno: '#3a1200', italico: true, peso: 900, alinhar });
                ctx.restore();
            }
            if (j.estado === 'morto' && j.vidas > 0) texto(ctx, 'RENASCENDO…', xTexto, 122, { tamanho: 16, cor: '#ffe680', contorno: '#000', alinhar });
        };
        painel(j1, 24, false, 'P1', 'p1');
        if (j2) painel(j2, LARGURA - 324, true, 'P2', 'p2');
        else if (!extras.toque) texto(ctx, Math.floor(tempo * 2) % 2 ? 'P2 · APERTE J PARA ENTRAR' : '', LARGURA - 24, 30, { tamanho: 13, cor: '#bfc7d5', contorno: '#000', alinhar: 'right' });

        // Pontuação e onda.
        texto(ctx, 'PONTOS', LARGURA / 2, 26, { tamanho: 12, cor: '#bfc7d5', alinhar: 'center', peso: 700 });
        texto(ctx, mundo.pontuacao.toLocaleString('pt-BR'), LARGURA / 2, 52, { tamanho: 26, cor: '#fff', contorno: '#000', alinhar: 'center', peso: 900 });
        if (extras.recorde) texto(ctx, `RECORDE ${extras.recorde.toLocaleString('pt-BR')}`, LARGURA / 2, 70, { tamanho: 11, cor: '#8f97a8', alinhar: 'center' });

        // Chefe.
        if (mundo.chefe && mundo.chefe.estado !== 'morto') {
            const c = mundo.chefe;
            texto(ctx, c.def.nome.toUpperCase(), LARGURA / 2, ALTURA - 40, { tamanho: 16, fonte: FONTE_TITULO, cor: '#ffd23a', contorno: '#000', alinhar: 'center', peso: 700 });
            barraVida(ctx, LARGURA / 2 - 220, ALTURA - 30, 440, 14, c.vida / c.vidaMax, '#c86bff', '#5a1a8a', false, 'chefe' + c.id);
        }
        // Avisos.
        if (ef.ondaAviso > 0) texto(ctx, ef.ondaTexto, LARGURA / 2, 110, { tamanho: 18, cor: '#ffe9b0', contorno: '#000', alinhar: 'center', peso: 700 });
        if (ef.chefeAviso > 0) {
            const k = Math.min(1, (3 - ef.chefeAviso) * 3);
            ctx.save(); ctx.globalAlpha = k;
            texto(ctx, 'CHEFE', LARGURA / 2, 190, { tamanho: 22, cor: '#ff3a2a', contorno: '#000', alinhar: 'center', peso: 900 });
            texto(ctx, ef.chefeNome.toUpperCase(), LARGURA / 2, 240, { tamanho: 44, fonte: FONTE_TITULO, cor: '#ffd23a', contorno: '#2a0000', contornoLargura: 6, alinhar: 'center', peso: 900, brilho: '#ff3a2a' });
            ctx.restore();
        }
        if (!mundo.travado && !mundo.concluida && mundo.faseDef.ondas.length && (mundo.onda < mundo.faseDef.ondas.length || mundo.camera.x < mundo.faseDef.comprimento - LARGURA - 40) && Math.floor(tempo * 3) % 2 === 0) {
            texto(ctx, 'SIGA ▶', LARGURA - 30, ALTURA / 2 - 40, { tamanho: 30, cor: '#ffd23a', contorno: '#000', alinhar: 'right', peso: 900, italico: true, brilho: '#ff9a3a' });
        }
    }

    // ── A CENA INTEIRA ────────────────────────────────────────────────────────────────────
    function desenharMundo(ctx, mundo, ef, tempo, extras) {
        ctx.save();
        if (ef.tremor > 0 && !(extras && extras.tremor === false)) ctx.translate((Math.random() - 0.5) * ef.tremor, (Math.random() - 0.5) * ef.tremor);
        const cam = mundo.camera.x, nome = mundo.faseDef.cenario, qualidade = (extras && extras.qualidade) || 'alta';
        Cenario.desenhar(ctx, nome, cam, tempo, { qualidade });
        for (const ob of mundo.objetos) desenharObjeto(ctx, ob, cam);
        for (const it of mundo.itens) desenharItem(ctx, it, cam, tempo);
        const lutadores = mundo.jogadores.concat(mundo.inimigos).sort((a, b) => a.y - b.y);
        for (const ent of lutadores) desenharLutador(ctx, ent, cam, tempo, qualidade);
        for (const p of mundo.projeteis) desenharProjetil(ctx, p, cam, tempo);
        desenharParticulas(ctx, ef, cam);
        Cenario.frente(ctx, nome, cam, tempo, { qualidade });
        desenharTextos(ctx, ef, cam);
        ctx.restore();
        Cenario.pos(ctx, nome, tempo, { qualidade });
        if (ef.flash > 0) { ctx.globalAlpha = Math.min(0.85, ef.flash); ctx.fillStyle = ef.flashCor; ctx.fillRect(0, 0, LARGURA, ALTURA); ctx.globalAlpha = 1; }
        if (ef.finalizacao > 0) {
            // Barras de cinema + vinheta vermelha.
            const k = Math.min(1, (1.5 - ef.finalizacao) * 4);
            ctx.fillStyle = '#000'; ctx.fillRect(0, 0, LARGURA, 50 * k); ctx.fillRect(0, ALTURA - 50 * k, LARGURA, 50 * k);
            const v = ctx.createRadialGradient(LARGURA / 2, ALTURA / 2, 200, LARGURA / 2, ALTURA / 2, 620);
            v.addColorStop(0, 'rgba(120,0,0,0)'); v.addColorStop(1, `rgba(120,0,0,${0.7 * k})`);
            ctx.fillStyle = v; ctx.fillRect(0, 0, LARGURA, ALTURA);
        }
        desenharHud(ctx, mundo, ef, tempo, extras || {});
    }

    // ── TELAS ─────────────────────────────────────────────────────────────────────────────
    function fundoDeMenu(ctx, tempo, ef) {
        Cenario.desenhar(ctx, 'patio', tempo * 30, tempo, { qualidade: 'media' });
        ctx.fillStyle = 'rgba(5,3,12,0.62)'; ctx.fillRect(0, 0, LARGURA, ALTURA);
        if (ef) desenharParticulas(ctx, ef, tempo * 30);
    }

    function desenharTitulo(ctx, tempo, ef, extras) {
        fundoDeMenu(ctx, tempo, ef);
        const pulso = 1 + Math.sin(tempo * 2) * 0.012;
        ctx.save(); ctx.translate(LARGURA / 2, 190); ctx.scale(pulso, pulso);
        const g = ctx.createLinearGradient(0, -60, 0, 20);
        g.addColorStop(0, '#fff2c0'); g.addColorStop(0.5, '#e6b93a'); g.addColorStop(1, '#8a4a10');
        ctx.font = `900 74px ${FONTE_TITULO}`; ctx.textAlign = 'center';
        ctx.shadowColor = '#ff3a1a'; ctx.shadowBlur = 40;
        ctx.lineWidth = 8; ctx.strokeStyle = '#2a0800'; ctx.lineJoin = 'round'; ctx.strokeText('PUNHOS', 0, -30); ctx.strokeText('DE SHAOLIN', 0, 40);
        ctx.fillStyle = g; ctx.fillText('PUNHOS', 0, -30); ctx.fillText('DE SHAOLIN', 0, 40);
        ctx.restore();
        texto(ctx, 'UM BEAT-EM-UP DE MONGES · SEM UM ÚNICO SPRITE', LARGURA / 2, 275, { tamanho: 14, cor: '#bfc7d5', alinhar: 'center', peso: 600 });
        if (Math.floor(tempo * 1.6) % 2 === 0) texto(ctx, extras.toque ? 'TOQUE PARA COMEÇAR' : 'APERTE ENTER', LARGURA / 2, 340, { tamanho: 22, cor: '#ffe9b0', contorno: '#000', alinhar: 'center', peso: 900 });
        if (extras.recorde) texto(ctx, `RECORDE · ${extras.recorde.toLocaleString('pt-BR')}`, LARGURA / 2, 375, { tamanho: 14, cor: '#ffd23a', alinhar: 'center', peso: 700 });
        const linhas = extras.toque
            ? ['JOYSTICK move · empurre até o fim pra correr', 'SOCO · CHUTE · ESPECIAL · PULAR · AGARRAR · DEFENDER', 'Agarre um inimigo tonto pra FINALIZAR']
            : ['P1: SETAS movem · Z soco · X chute · C especial · V agarrar · B defender · ESPAÇO pula', 'P2: WASD movem · J soco · K chute · L especial · U agarrar · I defender · H pula · J entra', 'Dois toques na direção CORREM · três socos LANÇAM · agarre um inimigo tonto pra FINALIZAR', 'Controle: analógico move · X soco · B chute · Y especial · A pula · RB agarra · LB defende'];
        linhas.forEach((l, i) => texto(ctx, l, LARGURA / 2, 430 + i * 22, { tamanho: 12, cor: '#8f97a8', alinhar: 'center', peso: 600 }));
        texto(ctx, 'F tela cheia · M mudo · P pausa', LARGURA / 2, 522, { tamanho: 11, cor: '#5c6473', alinhar: 'center' });
    }

    const bonecoDeMostra = {};
    function bonecoDe(id) {
        if (!bonecoDeMostra[id]) {
            const def = Motor.PERSONAGENS[id] || Motor.INIMIGOS[id];
            bonecoDeMostra[id] = { id: 1, def, estado: 'parado', quadro: 0, virado: 1, escala: def.escala || 1, vz: 0, z: 0 };
        }
        return bonecoDeMostra[id];
    }

    function desenharSelecao(ctx, tempo, ef, sel) {
        fundoDeMenu(ctx, tempo, ef);
        texto(ctx, 'ESCOLHA SEU MONGE', LARGURA / 2, 60, { tamanho: 34, fonte: FONTE_TITULO, cor: '#ffe9b0', contorno: '#2a0800', contornoLargura: 5, alinhar: 'center', peso: 900, brilho: '#ff3a1a' });
        const ids = Object.keys(Motor.PERSONAGENS);
        ids.forEach((id, k) => {
            const def = Motor.PERSONAGENS[id];
            const x = LARGURA / 2 + (k - 0.5) * 300, y = 100;
            const escolhidoP1 = sel.p1 === k, escolhidoP2 = sel.p2Entrou && sel.p2 === k;
            ctx.save();
            ctx.fillStyle = escolhidoP1 || escolhidoP2 ? 'rgba(60,30,10,0.75)' : 'rgba(10,8,20,0.6)';
            ctx.strokeStyle = escolhidoP1 ? '#ff5a3a' : escolhidoP2 ? '#7ae0ff' : 'rgba(255,255,255,0.2)'; ctx.lineWidth = escolhidoP1 || escolhidoP2 ? 4 : 1.5;
            ctx.beginPath(); ctx.rect(x - 130, y, 260, 384); ctx.fill(); ctx.stroke();
            ctx.restore();
            const b = bonecoDe(id);
            b.estado = (escolhidoP1 && sel.confirmadoP1) || (escolhidoP2 && sel.confirmadoP2) ? 'atacando' : 'parado';
            if (b.estado === 'atacando') { b.golpeNome = 'soco3'; b.golpe = def.golpes.soco3; b.quadro = (tempo * 0.8) % def.golpes.soco3.total; }
            ctx.save(); ctx.translate(x, y + 262); ctx.scale(1.55, 1.55);
            ctx.fillStyle = radial(ctx, 0, -60, 10, 120, [[0, 'rgba(255,200,120,0.18)'], [1, 'rgba(255,200,120,0)']]); ctx.fillRect(-140, -200, 280, 220);
            ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.beginPath(); ctx.ellipse(0, 0, 26, 7, 0, 0, TAU); ctx.fill();
            Figura.sombraProjetada(ctx, b, tempo, 1, 'alta');
            Figura.desenhar(ctx, b, tempo, { virado: 1 }); ctx.restore();
            texto(ctx, def.nome.toUpperCase(), x, y + 300, { tamanho: 30, fonte: FONTE_TITULO, cor: '#ffe9b0', contorno: '#000', alinhar: 'center', peso: 900 });
            texto(ctx, def.titulo, x, y + 322, { tamanho: 13, cor: '#bfc7d5', alinhar: 'center', italico: true });
            const linhas = [['VIDA', def.vida / 130], ['VELOCIDADE', def.velocidade / 260], ['ALCANCE', def.golpes.soco1.alcance / 90]];
            linhas.forEach(([rotulo, frac], i) => {
                const by = y + 332 + i * 11;
                texto(ctx, rotulo, x - 110, by + 7, { tamanho: 9, cor: '#8f97a8', peso: 700 });
                ctx.fillStyle = 'rgba(255,255,255,0.15)'; ctx.fillRect(x - 30, by, 140, 6);
                ctx.fillStyle = '#ffb347'; ctx.fillRect(x - 30, by, 140 * Math.min(1, frac), 6);
            });
            texto(ctx, `ESPECIAL · ${def.golpes.especial.nome.toUpperCase()}`, x, y + 373, { tamanho: 10, cor: '#7ae0ff', alinhar: 'center', peso: 700 });
            if (escolhidoP1) texto(ctx, sel.confirmadoP1 ? 'P1 ✓' : 'P1', x - 118, y + 26, { tamanho: 18, cor: '#ff5a3a', contorno: '#000', peso: 900 });
            if (escolhidoP2) texto(ctx, sel.confirmadoP2 ? 'P2 ✓' : 'P2', x + 118, y + 26, { tamanho: 18, cor: '#7ae0ff', contorno: '#000', alinhar: 'right', peso: 900 });
        });
        const dica = sel.confirmadoP1 ? (sel.p2Entrou && !sel.confirmadoP2 ? 'P2: ← → ESCOLHE · J CONFIRMA' : (sel.toque ? 'TOQUE DE NOVO PARA LUTAR' : 'ENTER PARA LUTAR · P2 ENTRA COM J')) : (sel.toque ? 'TOQUE NO MONGE · SOCO CONFIRMA' : '← → ESCOLHEM · Z OU ENTER CONFIRMA');
        if (Math.floor(tempo * 2) % 2 === 0) texto(ctx, dica, LARGURA / 2, 515, { tamanho: 15, cor: '#ffe9b0', contorno: '#000', alinhar: 'center', peso: 700 });
    }

    function desenharIntroFase(ctx, faseDef, k) {
        ctx.fillStyle = '#05030a'; ctx.fillRect(0, 0, LARGURA, ALTURA);
        const alpha = k < 0.2 ? k / 0.2 : k > 0.85 ? (1 - k) / 0.15 : 1;
        ctx.save(); ctx.globalAlpha = alpha;
        texto(ctx, `FASE ${faseDef.numero}`, LARGURA / 2, 220, { tamanho: 22, cor: '#bfc7d5', alinhar: 'center', peso: 700 });
        texto(ctx, faseDef.nome.toUpperCase(), LARGURA / 2, 290, { tamanho: 54, fonte: FONTE_TITULO, cor: '#ffe9b0', contorno: '#2a0800', contornoLargura: 6, alinhar: 'center', peso: 900, brilho: '#ff3a1a' });
        if (k > 0.55) texto(ctx, 'LUTE!', LARGURA / 2, 370, { tamanho: 40, cor: '#ff5a3a', contorno: '#000', alinhar: 'center', peso: 900, italico: true });
        ctx.restore();
    }

    function desenharPausa(ctx, extras) {
        ctx.fillStyle = 'rgba(0,0,0,0.65)'; ctx.fillRect(0, 0, LARGURA, ALTURA);
        texto(ctx, 'PAUSA', LARGURA / 2, 240, { tamanho: 60, fonte: FONTE_TITULO, cor: '#ffe9b0', contorno: '#2a0800', contornoLargura: 6, alinhar: 'center', peso: 900 });
        texto(ctx, extras.toque ? 'TOQUE EM ▶ PARA VOLTAR' : 'P OU ENTER VOLTA · ESC SAI PRO TÍTULO · M MUDO', LARGURA / 2, 290, { tamanho: 15, cor: '#bfc7d5', alinhar: 'center', peso: 700 });
        if (extras.mudo) texto(ctx, 'SOM DESLIGADO', LARGURA / 2, 320, { tamanho: 13, cor: '#8f97a8', alinhar: 'center' });
    }

    function desenharFim(ctx, tempo, ef, dados) {
        fundoDeMenu(ctx, tempo, ef);
        const vitoria = dados.vitoria;
        texto(ctx, vitoria ? 'O TEMPLO ESTÁ LIVRE' : 'FIM DE JOGO', LARGURA / 2, 170, { tamanho: vitoria ? 54 : 64, fonte: FONTE_TITULO, cor: vitoria ? '#ffd23a' : '#ff3a2a', contorno: '#2a0000', contornoLargura: 7, alinhar: 'center', peso: 900, brilho: vitoria ? '#ffb347' : '#ff3a1a' });
        texto(ctx, vitoria ? 'O Feiticeiro caiu. As almas do poço descansam. Os monges voltam ao chá.' : `Você caiu em ${dados.faseNome}. O templo ainda espera.`, LARGURA / 2, 215, { tamanho: 15, cor: '#bfc7d5', alinhar: 'center', italico: true });
        texto(ctx, 'PONTOS', LARGURA / 2, 275, { tamanho: 14, cor: '#8f97a8', alinhar: 'center', peso: 700 });
        texto(ctx, dados.pontuacao.toLocaleString('pt-BR'), LARGURA / 2, 320, { tamanho: 44, cor: '#fff', contorno: '#000', alinhar: 'center', peso: 900 });
        if (dados.novoRecorde) texto(ctx, '★ NOVO RECORDE ★', LARGURA / 2, 355, { tamanho: 18, cor: '#ffd23a', contorno: '#000', alinhar: 'center', peso: 900 });
        else if (dados.recorde) texto(ctx, `RECORDE ${dados.recorde.toLocaleString('pt-BR')}`, LARGURA / 2, 355, { tamanho: 13, cor: '#8f97a8', alinhar: 'center' });
        const plural = (n, um, varios) => `${n} ${n === 1 ? um : varios}`;
        texto(ctx, `${plural(dados.finalizacoes, 'finalização', 'finalizações')} · ${plural(dados.maiorCombo, 'golpe', 'golpes')} no maior combo · ${plural(dados.inimigos, 'inimigo derrotado', 'inimigos derrotados')}`, LARGURA / 2, 395, { tamanho: 13, cor: '#bfc7d5', alinhar: 'center' });
        if (Math.floor(tempo * 1.6) % 2 === 0) texto(ctx, dados.toque ? 'TOQUE PARA VOLTAR AO TÍTULO' : (vitoria ? 'ENTER VOLTA AO TÍTULO' : 'ENTER TENTA DE NOVO · ESC VOLTA AO TÍTULO'), LARGURA / 2, 460, { tamanho: 18, cor: '#ffe9b0', contorno: '#000', alinhar: 'center', peso: 900 });
    }

    // ── MENUS ─────────────────────────────────────────────────────────────────────────────
    // Geometria fixa e exportada: o `principal.js` usa as MESMAS linhas pra mapear o toque.
    const MENU_Y0 = 232, MENU_PASSO = 46;

    function logoPequeno(ctx, tempo) {
        ctx.save(); ctx.translate(LARGURA / 2, 96);
        const g = ctx.createLinearGradient(0, -30, 0, 10);
        g.addColorStop(0, '#fff2c0'); g.addColorStop(0.5, '#e6b93a'); g.addColorStop(1, '#8a4a10');
        ctx.font = `900 40px ${FONTE_TITULO}`; ctx.textAlign = 'center';
        ctx.shadowColor = '#ff3a1a'; ctx.shadowBlur = 24;
        ctx.lineWidth = 6; ctx.strokeStyle = '#2a0800'; ctx.lineJoin = 'round'; ctx.strokeText('PUNHOS DE SHAOLIN', 0, 0);
        ctx.fillStyle = g; ctx.fillText('PUNHOS DE SHAOLIN', 0, 0);
        ctx.restore();
    }

    // Menu comprido (o Templo) aperta as linhas: a geometria vem do chamador, e o `principal.js`
    // passa a MESMA pro toque. Sem `y0`/`passo`, é a de sempre.
    function geometriaDoMenu(menu) { return { y0: (menu && menu.y0) || MENU_Y0, passo: (menu && menu.passo) || MENU_PASSO }; }

    // itens: [{ rotulo, valor?, desabilitado?, detalhe?, fracao? }] · indice: o selecionado · y0/passo: geometria opcional
    function desenharMenu(ctx, tempo, ef, menu) {
        fundoDeMenu(ctx, tempo, ef);
        logoPequeno(ctx, tempo);
        const { y0, passo } = geometriaDoMenu(menu);
        const alto = Math.min(40, passo - 4);
        if (menu.titulo) texto(ctx, menu.titulo.toUpperCase(), LARGURA / 2, 160, { tamanho: 22, cor: '#bfc7d5', alinhar: 'center', peso: 700 });
        if (menu.subtitulo) texto(ctx, menu.subtitulo, LARGURA / 2, 186, { tamanho: 13, cor: '#8f97a8', alinhar: 'center', italico: true });
        menu.itens.forEach((item, i) => {
            const y = y0 + i * passo;
            const sel = i === menu.indice;
            if (sel) {
                ctx.fillStyle = 'rgba(255,90,58,0.16)'; ctx.fillRect(LARGURA / 2 - 300, y - alto * 0.75, 600, alto);
                ctx.fillStyle = '#ff5a3a'; ctx.fillRect(LARGURA / 2 - 300, y - alto * 0.75, 4, alto);
                texto(ctx, '▶', LARGURA / 2 - 280, y, { tamanho: 16, cor: '#ffe9b0' });
            }
            const cor = item.desabilitado ? '#5c6473' : sel ? '#ffe9b0' : '#d8dde8';
            texto(ctx, item.rotulo.toUpperCase(), item.valor != null || item.fracao != null ? LARGURA / 2 - 250 : LARGURA / 2, y, { tamanho: 20, cor, alinhar: item.valor != null || item.fracao != null ? 'left' : 'center', peso: 700, contorno: sel ? '#2a0800' : null });
            if (item.fracao != null) {
                ctx.fillStyle = 'rgba(255,255,255,0.14)'; ctx.fillRect(LARGURA / 2 + 20, y - 14, 220, 12);
                ctx.fillStyle = sel ? '#ffb347' : '#c98f3a'; ctx.fillRect(LARGURA / 2 + 20, y - 14, 220 * item.fracao, 12);
                texto(ctx, `${Math.round(item.fracao * 100)}%`, LARGURA / 2 + 290, y, { tamanho: 16, cor, alinhar: 'right', peso: 700 });
            } else if (item.valor != null) {
                texto(ctx, String(item.valor).toUpperCase(), LARGURA / 2 + 290, y, { tamanho: 18, cor: item.destaque ? '#ffb347' : cor, alinhar: 'right', peso: 700 });
            }
            if (sel && item.detalhe) texto(ctx, item.detalhe, LARGURA / 2, y0 + menu.itens.length * passo + 6, { tamanho: 13, cor: '#bfc7d5', alinhar: 'center', italico: true });
        });
        if (menu.dica) texto(ctx, menu.dica, LARGURA / 2, 515, { tamanho: 13, cor: '#8f97a8', alinhar: 'center', peso: 600 });
    }

    function desenharConquistas(ctx, tempo, ef, tela) {
        fundoDeMenu(ctx, tempo, ef);
        logoPequeno(ctx, tempo);
        const ganhas = tela.lista.filter(c => c.ganha).length;
        texto(ctx, `CONQUISTAS · ${ganhas} / ${tela.lista.length}`, LARGURA / 2, 150, { tamanho: 22, cor: '#bfc7d5', alinhar: 'center', peso: 700 });
        // A altura da linha encolhe com a lista (17 conquistas não cabiam em 42 px até a dica, em 520).
        const colunas = 2, largura = 430, altura = Math.min(42, Math.floor(328 / Math.ceil(tela.lista.length / colunas)));
        tela.lista.forEach((c, i) => {
            const col = i % colunas, lin = Math.floor(i / colunas);
            const x = LARGURA / 2 + (col - 1) * largura + 10, y = 172 + lin * altura;
            ctx.fillStyle = c.ganha ? 'rgba(255,179,71,0.14)' : 'rgba(255,255,255,0.05)';
            ctx.fillRect(x, y, largura - 20, altura - 6);
            ctx.fillStyle = c.ganha ? '#ffd23a' : '#3a3f4a'; ctx.beginPath(); ctx.arc(x + 20, y + 18, 11, 0, TAU); ctx.fill();
            if (c.ganha) { ctx.strokeStyle = '#2a0800'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x + 14, y + 18); ctx.lineTo(x + 19, y + 23); ctx.lineTo(x + 27, y + 12); ctx.stroke(); }
            else { ctx.fillStyle = '#1a1d24'; ctx.fillRect(x + 15, y + 16, 10, 8); ctx.strokeStyle = '#1a1d24'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(x + 20, y + 15, 4, Math.PI, 0); ctx.stroke(); }
            texto(ctx, c.def.nome.toUpperCase(), x + 40, y + 15, { tamanho: 13, cor: c.ganha ? '#ffe9b0' : '#8f97a8', peso: 700 });
            texto(ctx, c.def.descricao, x + 40, y + 30, { tamanho: 11, cor: c.ganha ? '#d8dde8' : '#5c6473' });
        });
        texto(ctx, tela.dica || 'ESC OU ENTER VOLTA', LARGURA / 2, 520, { tamanho: 13, cor: '#8f97a8', alinhar: 'center', peso: 600 });
    }

    // O aviso desliza pela direita, fica, e vai embora. k é o progresso de 0 a 1 em ~4 s.
    function desenharAvisoDeConquista(ctx, def, k) {
        const entrada = k < 0.12 ? suave(k / 0.12) : k > 0.85 ? 1 - suave((k - 0.85) / 0.15) : 1;
        const larg = 340, alt = 64;
        const x = LARGURA - 20 - larg * entrada, y = ALTURA - 20 - alt;
        ctx.save();
        ctx.fillStyle = 'rgba(8,6,16,0.92)'; ctx.fillRect(x, y, larg, alt);
        ctx.strokeStyle = '#ffd23a'; ctx.lineWidth = 2; ctx.strokeRect(x + 1, y + 1, larg - 2, alt - 2);
        ctx.fillStyle = '#ffd23a'; ctx.beginPath(); ctx.arc(x + 32, y + 32, 18, 0, TAU); ctx.fill();
        ctx.strokeStyle = '#2a0800'; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(x + 23, y + 33); ctx.lineTo(x + 30, y + 40); ctx.lineTo(x + 42, y + 24); ctx.stroke();
        texto(ctx, 'CONQUISTA DESBLOQUEADA', x + 62, y + 22, { tamanho: 11, cor: '#ffd23a', peso: 700 });
        texto(ctx, def.nome.toUpperCase(), x + 62, y + 40, { tamanho: 16, cor: '#ffe9b0', peso: 900 });
        texto(ctx, def.descricao, x + 62, y + 55, { tamanho: 11, cor: '#bfc7d5' });
        ctx.restore();
    }

    raiz.PunhosDeShaolin.Desenho = { desenharMenu, geometriaDoMenu, desenharConquistas, desenharAvisoDeConquista, MENU_Y0, MENU_PASSO, desenharMundo, desenharTitulo, desenharSelecao, desenharIntroFase, desenharPausa, desenharFim, criarEfeitos, telaY, FONTE_TITULO, FONTE_HUD };
})(window);
