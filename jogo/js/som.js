// PUNHOS DE SHAOLIN — som. Tudo sintetizado na hora com Web Audio: nenhum arquivo de áudio.
//
// O navegador só deixa tocar depois de um gesto do jogador, então `ligar()` é chamado no
// primeiro toque/tecla da tela de título. Antes disso, `tocar` é silencioso e não quebra nada.
(function (raiz) {
    'use strict';

    function criar() {
        let ctx = null, mestre = null, silenciado = false;
        let musica = { parar: null, cenario: null };
        try { silenciado = localStorage.getItem('punhos-de-shaolin.mudo') === '1'; } catch (_) { /* sem storage, sem memória */ }

        function ligar() {
            if (ctx) { if (ctx.state === 'suspended') ctx.resume().catch(() => { }); return; }
            const AC = raiz.AudioContext || raiz.webkitAudioContext;
            if (!AC) return;
            ctx = new AC();
            mestre = ctx.createGain();
            mestre.gain.value = silenciado ? 0 : 0.55;
            mestre.connect(ctx.destination);
        }

        function agora() { return ctx.currentTime; }

        // Ruído branco curto — base de soco, queda, chama.
        function ruido(duracao, ganho, filtro, freq, envelope) {
            if (!ctx) return;
            const n = Math.floor(ctx.sampleRate * duracao);
            const buffer = ctx.createBuffer(1, n, ctx.sampleRate);
            const dados = buffer.getChannelData(0);
            for (let i = 0; i < n; i++) dados[i] = Math.random() * 2 - 1;
            const fonte = ctx.createBufferSource();
            fonte.buffer = buffer;
            const f = ctx.createBiquadFilter();
            f.type = filtro || 'lowpass';
            f.frequency.value = freq || 800;
            const g = ctx.createGain();
            const t = agora();
            g.gain.setValueAtTime(ganho, t);
            g.gain.exponentialRampToValueAtTime(0.001, t + duracao);
            if (envelope) envelope(f, g, t);
            fonte.connect(f); f.connect(g); g.connect(mestre);
            fonte.start(t); fonte.stop(t + duracao);
        }

        // Um tom com envelope — base de tudo que tem altura definida.
        function tom(tipo, freqInicio, freqFim, duracao, ganho, atraso) {
            if (!ctx) return;
            const o = ctx.createOscillator();
            const g = ctx.createGain();
            const t = agora() + (atraso || 0);
            o.type = tipo;
            o.frequency.setValueAtTime(freqInicio, t);
            if (freqFim && freqFim !== freqInicio) o.frequency.exponentialRampToValueAtTime(freqFim, t + duracao);
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(ganho, t + 0.01);
            g.gain.exponentialRampToValueAtTime(0.0001, t + duracao);
            o.connect(g); g.connect(mestre);
            o.start(t); o.stop(t + duracao + 0.02);
        }

        const EFEITOS = {
            'soco': () => { ruido(0.09, 0.5, 'lowpass', 900); tom('sine', 180, 60, 0.1, 0.5); },
            'chute': () => { ruido(0.14, 0.6, 'lowpass', 600); tom('sine', 140, 40, 0.16, 0.6); },
            'bastao': () => { ruido(0.08, 0.4, 'bandpass', 1800); tom('triangle', 520, 180, 0.09, 0.3); },
            'lamina': () => { ruido(0.16, 0.35, 'highpass', 3000); tom('sawtooth', 1800, 400, 0.12, 0.12); },
            'pancada': () => { ruido(0.3, 0.8, 'lowpass', 300); tom('sine', 90, 30, 0.35, 0.8); },
            'whoosh': () => ruido(0.16, 0.18, 'bandpass', 1200, (f, g, t) => { f.frequency.setValueAtTime(500, t); f.frequency.exponentialRampToValueAtTime(2400, t + 0.14); }),
            'acerto': () => { ruido(0.08, 0.45, 'lowpass', 1400); tom('square', 220, 90, 0.07, 0.18); },
            'acerto-forte': () => { ruido(0.2, 0.7, 'lowpass', 700); tom('sine', 160, 45, 0.24, 0.7); tom('square', 300, 100, 0.08, 0.15); },
            'bloqueio': () => { tom('triangle', 900, 700, 0.08, 0.25); ruido(0.06, 0.2, 'highpass', 2500); },
            'pulo': () => tom('sine', 300, 620, 0.16, 0.15),
            'queda': () => { ruido(0.22, 0.5, 'lowpass', 400); tom('sine', 100, 40, 0.2, 0.4); },
            'agarrar': () => { ruido(0.1, 0.3, 'bandpass', 700); tom('sawtooth', 200, 120, 0.1, 0.1); },
            'arremesso': () => { ruido(0.3, 0.3, 'bandpass', 900, (f, g, t) => { f.frequency.setValueAtTime(400, t); f.frequency.exponentialRampToValueAtTime(2000, t + 0.25); }); tom('sine', 260, 90, 0.3, 0.3); },
            'fogo': () => { ruido(0.45, 0.45, 'bandpass', 900, (f, g, t) => { f.frequency.setValueAtTime(300, t); f.frequency.exponentialRampToValueAtTime(3000, t + 0.4); }); tom('sawtooth', 120, 60, 0.4, 0.15); },
            'flecha': () => { ruido(0.12, 0.3, 'highpass', 2000); tom('triangle', 1200, 300, 0.1, 0.12); },
            'item': () => { tom('sine', 660, 660, 0.08, 0.25); tom('sine', 880, 880, 0.1, 0.25, 0.08); tom('sine', 1320, 1320, 0.18, 0.22, 0.16); },
            'quebra': () => { ruido(0.25, 0.5, 'highpass', 1500); tom('square', 700, 200, 0.12, 0.12); },
            'negado': () => tom('square', 200, 150, 0.12, 0.12),
            'morte': () => { tom('sawtooth', 220, 60, 0.5, 0.25); ruido(0.3, 0.3, 'lowpass', 500); },
            'morte-jogador': () => { tom('sawtooth', 300, 40, 1.1, 0.3); tom('sine', 150, 30, 1.2, 0.3); },
            'teleporte': () => { tom('sine', 1200, 200, 0.3, 0.2); tom('sine', 300, 1400, 0.3, 0.15, 0.05); ruido(0.3, 0.15, 'highpass', 2000); },
            'siga': () => { tom('triangle', 523, 523, 0.12, 0.2); tom('triangle', 659, 659, 0.12, 0.2, 0.12); tom('triangle', 784, 784, 0.25, 0.22, 0.24); },
            'chefe': () => { tom('sawtooth', 55, 55, 1.4, 0.35); tom('sawtooth', 58, 58, 1.4, 0.3); ruido(0.6, 0.5, 'lowpass', 200); },
            'gongo': () => { tom('sine', 196, 190, 2.2, 0.5); tom('sine', 392, 385, 1.8, 0.25); tom('triangle', 588, 580, 1.2, 0.12); ruido(0.15, 0.4, 'lowpass', 1200); },
            'finalizacao': () => { ruido(0.5, 0.6, 'lowpass', 300); tom('sawtooth', 80, 30, 1.2, 0.4); tom('sine', 40, 40, 1.5, 0.5); },
            'selecionar': () => { tom('square', 440, 440, 0.06, 0.15); tom('square', 660, 660, 0.08, 0.15, 0.06); },
            'confirmar': () => { tom('triangle', 392, 392, 0.1, 0.2); tom('triangle', 587, 587, 0.1, 0.2, 0.1); tom('triangle', 784, 784, 0.3, 0.25, 0.2); },
            'pausa': () => tom('triangle', 500, 250, 0.2, 0.15),
        };

        function tocar(nome) {
            if (!ctx || silenciado) return;
            const efeito = EFEITOS[nome];
            if (efeito) efeito();
        }

        // ── MÚSICA: um sequenciador pequeno, pentatônico, com tambor. Cada cenário tem um humor.
        const ESCALAS = {
            patio: { base: 110, notas: [0, 3, 5, 7, 10, 12, 15], bpm: 108, tipo: 'triangle', tambor: [1, 0, 0, 1, 0, 1, 0, 0] },
            floresta: { base: 98, notas: [0, 2, 5, 7, 9, 12, 14], bpm: 96, tipo: 'sine', tambor: [1, 0, 0, 0, 1, 0, 1, 0] },
            poco: { base: 82, notas: [0, 1, 5, 6, 8, 12, 13], bpm: 88, tipo: 'sawtooth', tambor: [1, 0, 1, 0, 0, 0, 1, 1] },
            torre: { base: 92, notas: [0, 3, 6, 7, 10, 12, 15], bpm: 126, tipo: 'square', tambor: [1, 0, 1, 1, 0, 1, 0, 1] },
            titulo: { base: 73, notas: [0, 3, 5, 7, 10, 12], bpm: 72, tipo: 'sine', tambor: [1, 0, 0, 0, 0, 0, 0, 0] },
        };

        function tocarMusica(cenario) {
            if (!ctx) { musica.cenario = cenario; return; }
            if (musica.cenario === cenario && musica.parar) return;
            pararMusica();
            musica.cenario = cenario;
            if (!cenario) return;
            const escala = ESCALAS[cenario] || ESCALAS.patio;
            const ganhoMusica = ctx.createGain();
            ganhoMusica.gain.value = 0.28;
            ganhoMusica.connect(mestre);
            const passo = 60 / escala.bpm / 2;              // colcheia
            let indice = 0, proximo = ctx.currentTime + 0.05, ativo = true;
            let semente = 12345;
            const rng = () => { semente = (semente * 1103515245 + 12345) & 0x7fffffff; return semente / 0x7fffffff; };
            // Um bordão grave contínuo — o "gongo" de fundo.
            const bordao = ctx.createOscillator(), gb = ctx.createGain();
            bordao.type = 'sine'; bordao.frequency.value = escala.base / 2; gb.gain.value = 0.12;
            bordao.connect(gb); gb.connect(ganhoMusica); bordao.start();

            function agendar() {
                if (!ativo) return;
                while (proximo < ctx.currentTime + 0.4) {
                    const t = proximo;
                    const batida = indice % 8;
                    if (escala.tambor[batida]) {
                        // Taiko: seno grave com queda rápida + um sopro de ruído.
                        const o = ctx.createOscillator(), g = ctx.createGain();
                        o.type = 'sine'; o.frequency.setValueAtTime(140, t); o.frequency.exponentialRampToValueAtTime(45, t + 0.18);
                        g.gain.setValueAtTime(0.7, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.3);
                        o.connect(g); g.connect(ganhoMusica); o.start(t); o.stop(t + 0.32);
                    }
                    if (indice % 2 === 0 || rng() < 0.35) {
                        const grau = escala.notas[Math.floor(rng() * escala.notas.length)];
                        const oitava = rng() < 0.25 ? 2 : 1;
                        const f = escala.base * Math.pow(2, grau / 12) * oitava;
                        const o = ctx.createOscillator(), g = ctx.createGain();
                        o.type = escala.tipo; o.frequency.value = f;
                        g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.22, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + passo * 1.6);
                        o.connect(g); g.connect(ganhoMusica); o.start(t); o.stop(t + passo * 1.7);
                    }
                    proximo += passo; indice++;
                }
            }
            const timer = setInterval(agendar, 120);
            agendar();
            musica.parar = () => { ativo = false; clearInterval(timer); try { bordao.stop(); } catch (_) { /* já parou */ } ganhoMusica.disconnect(); };
        }

        function pararMusica() {
            if (musica.parar) musica.parar();
            musica.parar = null;
        }

        function alternarMudo() {
            silenciado = !silenciado;
            if (mestre) mestre.gain.value = silenciado ? 0 : 0.55;
            try { localStorage.setItem('punhos-de-shaolin.mudo', silenciado ? '1' : '0'); } catch (_) { /* sem storage */ }
            return silenciado;
        }

        return { ligar, tocar, musica: tocarMusica, pararMusica, alternarMudo, get silenciado() { return silenciado; }, get ligado() { return !!ctx; } };
    }

    raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {};
    raiz.PunhosDeShaolin.Som = { criar };
})(window);
