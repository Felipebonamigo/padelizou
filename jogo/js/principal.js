// PUNHOS DE SHAOLIN — o laço principal: telas (título → escolha → fases → fim), pausa,
// passo fixo de simulação (1/60 s), congelamento de acerto, câmera lenta da finalização e o
// recorde no localStorage. Junta os quatro módulos: Motor (regra), Desenho, Som e Entrada.
(function (raiz) {
    'use strict';
    const { Motor, Desenho, Som, Entrada } = raiz.PunhosDeShaolin;
    const PASSO = 1 / 60;
    const DURACAO_INTRO = 2.6;
    const CHAVE_RECORDE = 'punhos-de-shaolin.recorde';

    const canvas = document.getElementById('tela');
    const ctx = canvas.getContext('2d');
    const som = Som.criar();
    const entrada = Entrada.criar({ joystick: document.getElementById('joystick'), joystickBolinha: document.getElementById('bolinha'), botoes: document.getElementById('botoes') });
    let efeitos = Desenho.criarEfeitos();

    function lerRecorde() { try { return Number(localStorage.getItem(CHAVE_RECORDE)) || 0; } catch (_) { return 0; } }
    function gravarRecorde(v) { try { localStorage.setItem(CHAVE_RECORDE, String(v)); } catch (_) { /* sem storage, sem recorde */ } }

    const jogo = {
        tela: 'titulo', tempo: 0, mundo: null, fase: 1, pausado: false, intro: 0, acumulador: 0, fimHa: 0,
        sel: { p1: 0, p2: 1, p2Entrou: false, confirmadoP1: false, confirmadoP2: false, prontoHa: 0 },
        salvos: null, pontuacaoNaFase: 0, recorde: lerRecorde(), toque: false,
        estatisticas: { finalizacoes: 0, maiorCombo: 0, inimigos: 0 }, fim: null, semente: (Date.now() & 0xffff) || 1,
        tocouAgora: null,
    };
    const IDS = Object.keys(Motor.PERSONAGENS);

    // ── TAMANHO: 16:9 que cabe na janela, com barras pretas no que sobra ─────────────────
    function ajustarTamanho() {
        const escala = Math.min(raiz.innerWidth / Motor.LARGURA, raiz.innerHeight / Motor.ALTURA);
        canvas.style.width = `${Math.floor(Motor.LARGURA * escala)}px`;
        canvas.style.height = `${Math.floor(Motor.ALTURA * escala)}px`;
    }
    raiz.addEventListener('resize', ajustarTamanho);
    ajustarTamanho();

    // Toque na tela (menus). Converte pro sistema lógico de 960×540.
    canvas.addEventListener('pointerdown', ev => {
        const r = canvas.getBoundingClientRect();
        jogo.tocouAgora = { x: (ev.clientX - r.left) / r.width * Motor.LARGURA, y: (ev.clientY - r.top) / r.height * Motor.ALTURA };
        if (ev.pointerType === 'touch') marcarToque();
        som.ligar();
    });
    function marcarToque() { jogo.toque = true; document.body.classList.add('com-toque'); }
    if (raiz.matchMedia && raiz.matchMedia('(pointer: coarse)').matches) marcarToque();
    raiz.addEventListener('touchstart', marcarToque, { passive: true, once: true });
    raiz.addEventListener('keydown', () => som.ligar(), { once: true });

    function telaCheia() {
        const el = document.documentElement;
        try {
            if (document.fullscreenElement) document.exitFullscreen().catch(() => { });
            else if (el.requestFullscreen) el.requestFullscreen().catch(() => { });
        } catch (_) { /* nem todo navegador deixa */ }
    }
    const botaoTelaCheia = document.getElementById('tela-cheia');
    if (botaoTelaCheia) botaoTelaCheia.addEventListener('click', telaCheia);

    // ── FASES ─────────────────────────────────────────────────────────────────────────────
    function personagensEscolhidos() {
        const lista = [IDS[jogo.sel.p1]];
        if (jogo.sel.p2Entrou) lista.push(IDS[jogo.sel.p2]);
        return lista;
    }

    function iniciarFase(numero, opcoes) {
        const o = opcoes || {};
        jogo.fase = numero;
        const personagens = o.personagens || personagensEscolhidos();
        jogo.mundo = Motor.criarMundo({ fase: numero, jogadores: personagens, semente: jogo.semente * 31 + numero, pontuacao: o.pontuacao || 0 });
        jogo.pontuacaoNaFase = jogo.mundo.pontuacao;
        if (jogo.salvos) {
            // Quem passou de fase leva o que tinha: vidas, chi e pelo menos metade da vida.
            jogo.mundo.jogadores.forEach((j, k) => {
                const s = jogo.salvos[k];
                if (!s) return;
                j.vidas = s.vidas; j.chi = s.chi; j.vida = Math.max(Math.round(j.vidaMax * 0.5), Math.min(j.vidaMax, s.vida));
            });
        }
        efeitos = Desenho.criarEfeitos();
        jogo.acumulador = 0; jogo.pausado = false; jogo.intro = 0; jogo.tela = 'intro';
        som.pararMusica();
        document.body.dataset.tela = 'intro';
    }

    function salvarJogadores() {
        jogo.salvos = jogo.mundo.jogadores.map(j => ({ vidas: j.vidas, chi: j.chi, vida: j.vida }));
    }

    function terminar(vitoria) {
        const m = jogo.mundo;
        const novoRecorde = m.pontuacao > jogo.recorde;
        if (novoRecorde) { jogo.recorde = m.pontuacao; gravarRecorde(m.pontuacao); }
        jogo.fim = { vitoria, pontuacao: m.pontuacao, recorde: jogo.recorde, novoRecorde, faseNome: m.faseDef.nome, toque: jogo.toque, ...jogo.estatisticas };
        jogo.tela = 'fim'; jogo.fimHa = 0;
        som.pararMusica();
        som.tocar(vitoria ? 'gongo' : 'morte-jogador');
        document.body.dataset.tela = 'fim';
    }

    function irParaTitulo() {
        jogo.tela = 'titulo'; jogo.mundo = null; jogo.salvos = null; jogo.pausado = false;
        jogo.sel = { p1: 0, p2: 1, p2Entrou: false, confirmadoP1: false, confirmadoP2: false, prontoHa: 0 };
        jogo.estatisticas = { finalizacoes: 0, maiorCombo: 0, inimigos: 0 };
        jogo.semente = (Date.now() & 0xffff) || 1;
        efeitos = Desenho.criarEfeitos();
        som.musica('titulo');
        document.body.dataset.tela = 'titulo';
    }

    function comecarJogo() {
        jogo.salvos = null;
        jogo.estatisticas = { finalizacoes: 0, maiorCombo: 0, inimigos: 0 };
        iniciarFase(1);
    }

    // ── ATUALIZAÇÃO ───────────────────────────────────────────────────────────────────────
    function atualizar(dt) {
        const sistema = entrada.lerSistema();
        const entradas = entrada.ler();
        const toque = jogo.tocouAgora; jogo.tocouAgora = null;
        if (sistema.mudo) som.alternarMudo();
        if (sistema.telaCheia) telaCheia();

        switch (jogo.tela) {
            case 'titulo': {
                if (som.ligado) som.musica('titulo');
                efeitos.atualizar(dt, { camera: { x: jogo.tempo * 30 } }, 'patio');
                if (sistema.confirmar || toque || entradas[0].apertou.soco) {
                    som.ligar(); som.tocar('confirmar');
                    jogo.tela = 'selecao'; document.body.dataset.tela = 'selecao';
                }
                break;
            }
            case 'selecao': {
                const s = jogo.sel;
                efeitos.atualizar(dt, { camera: { x: jogo.tempo * 30 } }, 'patio');
                if (toque) {
                    const coluna = toque.x < Motor.LARGURA / 2 ? 0 : 1;
                    if (!s.confirmadoP1) { if (s.p1 === coluna) { s.confirmadoP1 = true; som.tocar('confirmar'); } else { s.p1 = coluna; som.tocar('selecionar'); } }
                    else s.prontoHa = 99;
                }
                if (!s.confirmadoP1) {
                    if (entradas[0].apertou.esquerda || entradas[0].apertou.direita) { s.p1 = (s.p1 + 1) % IDS.length; som.tocar('selecionar'); }
                    if (entradas[0].apertou.soco || sistema.confirmar) { s.confirmadoP1 = true; som.tocar('confirmar'); }
                } else if (sistema.confirmar && !(s.p2Entrou && !s.confirmadoP2)) s.prontoHa = 99;
                // P2 entra com a PRÓPRIA tecla (J, ou Start no segundo controle) — Enter é do P1.
                if ((entradas[1].apertou.soco || entradas[1]._start) && !s.p2Entrou && !(s.prontoHa >= 99)) { s.p2Entrou = true; s.p2 = 1 - s.p1; som.tocar('selecionar'); }
                else if (s.p2Entrou && !s.confirmadoP2) {
                    if (entradas[1].apertou.esquerda || entradas[1].apertou.direita) { s.p2 = (s.p2 + 1) % IDS.length; som.tocar('selecionar'); }
                    if (entradas[1].apertou.soco) { s.confirmadoP2 = true; som.tocar('confirmar'); }
                }
                if (sistema.voltar) { irParaTitulo(); break; }
                if (s.prontoHa >= 99 || (s.confirmadoP1 && (!s.p2Entrou || s.confirmadoP2) && s.p2Entrou)) {
                    s.prontoHa = 99;
                    s.contagem = (s.contagem || 0) + dt;
                    if (s.contagem > 0.5) comecarJogo();
                }
                break;
            }
            case 'intro': {
                jogo.intro += dt;
                if (jogo.intro >= DURACAO_INTRO || sistema.confirmar) {
                    jogo.tela = 'jogo'; document.body.dataset.tela = 'jogo';
                    som.musica(jogo.mundo.faseDef.cenario);
                }
                break;
            }
            case 'jogo': {
                const m = jogo.mundo;
                if (jogo.pausado && sistema.voltar) { irParaTitulo(); break; }
                if (sistema.pausa) { jogo.pausado = !jogo.pausado; som.tocar('pausa'); }
                if (jogo.pausado) { if (sistema.confirmar) { jogo.pausado = false; } break; }
                if ((sistema.entrarP2 || entradas[1].apertou.soco) && m.jogadores.length === 1 && !m.fimDeJogo) {
                    jogo.sel.p2Entrou = true; jogo.sel.p2 = 1 - jogo.sel.p1;
                    Motor.adicionarJogador(m, IDS[jogo.sel.p2]);
                    efeitos.processar(m.eventos, m, som); m.eventos = [];
                    som.tocar('gongo');
                }
                // Congelamento de acerto: a tela para por um instante, o mundo não anda.
                if (efeitos.congelar > 0) { efeitos.congelar -= dt; }
                else {
                    const escala = efeitos.lento > 0 ? 0.3 : 1;
                    jogo.acumulador = Math.min(jogo.acumulador + dt * escala, PASSO * 5);
                    let primeiro = true;
                    while (jogo.acumulador >= PASSO) {
                        Motor.passo(m, PASSO, entradas);
                        efeitos.processar(m.eventos, m, som);
                        for (const ev of m.eventos) {
                            if (ev.tipo === 'finalizacao') jogo.estatisticas.finalizacoes++;
                            if (ev.tipo === 'morte' && ev.time === 'inimigo') jogo.estatisticas.inimigos++;
                        }
                        for (const j of m.jogadores) jogo.estatisticas.maiorCombo = Math.max(jogo.estatisticas.maiorCombo, j.combo);
                        jogo.acumulador -= PASSO;
                        if (primeiro) { for (const e of entradas) for (const b of Motor.BOTOES) e.apertou[b] = false; primeiro = false; }
                        if (efeitos.congelar > 0) break;
                    }
                }
                efeitos.atualizar(dt, m, m.faseDef.cenario);
                if (m.concluida && m.concluidaHa > 2.2) {
                    salvarJogadores();
                    if (jogo.fase >= Motor.FASES.length - 1) terminar(true);
                    else iniciarFase(jogo.fase + 1, { pontuacao: m.pontuacao });
                }
                if (m.fimDeJogo) { jogo.fimHa += dt; if (jogo.fimHa > 1.6) terminar(false); }
                break;
            }
            case 'fim': {
                jogo.fimHa += dt;
                efeitos.atualizar(dt, { camera: { x: jogo.tempo * 30 } }, 'patio');
                if (jogo.fimHa < 0.8) break;
                if (sistema.voltar || (jogo.fim.vitoria && (sistema.confirmar || toque))) { irParaTitulo(); break; }
                if (sistema.confirmar || toque) {
                    // Tentar de novo: a mesma fase, três vidas, os pontos de quando ela começou.
                    jogo.salvos = null;
                    iniciarFase(jogo.fase, { personagens: jogo.mundo.jogadores.map(j => j.personagem), pontuacao: jogo.pontuacaoNaFase });
                }
                break;
            }
            default: break;
        }
    }

    // ── DESENHO ───────────────────────────────────────────────────────────────────────────
    function desenhar() {
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, Motor.LARGURA, Motor.ALTURA);
        const extras = { recorde: jogo.recorde, toque: jogo.toque, mudo: som.silenciado };
        switch (jogo.tela) {
            case 'titulo': Desenho.desenharTitulo(ctx, jogo.tempo, efeitos, extras); break;
            case 'selecao': Desenho.desenharSelecao(ctx, jogo.tempo, efeitos, Object.assign({ toque: jogo.toque }, jogo.sel)); break;
            case 'intro': Desenho.desenharIntroFase(ctx, jogo.mundo.faseDef, jogo.intro / DURACAO_INTRO); break;
            case 'jogo':
                Desenho.desenharMundo(ctx, jogo.mundo, efeitos, jogo.tempo, extras);
                if (jogo.pausado) Desenho.desenharPausa(ctx, extras);
                break;
            case 'fim': Desenho.desenharFim(ctx, jogo.tempo, efeitos, jogo.fim); break;
            default: break;
        }
    }

    let anterior = performance.now();
    function laco(agora) {
        const dt = Math.min(0.05, (agora - anterior) / 1000);
        anterior = agora;
        jogo.tempo += dt;
        atualizar(dt);
        desenhar();
        raiz.requestAnimationFrame(laco);
    }

    // As fontes do Google podem não vir (sem internet): o jogo não espera por elas mais de 1,5 s.
    const esperaFontes = document.fonts && document.fonts.ready ? Promise.race([document.fonts.ready, new Promise(r => setTimeout(r, 1500))]) : Promise.resolve();
    esperaFontes.then(() => { anterior = performance.now(); raiz.requestAnimationFrame(laco); });

    raiz.PunhosDeShaolin.jogo = jogo;   // pra inspecionar no console
})(window);
