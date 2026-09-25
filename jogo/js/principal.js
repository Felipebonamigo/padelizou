// PUNHOS DE SHAOLIN — o laço principal: telas (título → menu → dificuldade → escolha → fases,
// com o Templo entre elas → fim), opções, conquistas, pausa, passo fixo de simulação (1/60 s),
// congelamento de acerto, câmera lenta da finalização e o progresso salvo pela plataforma
// (navegador ou Electron/Steam).
(function (raiz) {
    'use strict';
    const { Motor, Desenho, Som, Entrada, Progresso, Conquistas, Plataforma, Loja } = raiz.PunhosDeShaolin;
    const PASSO = 1 / 60;
    const DURACAO_INTRO = 2.6;
    const DURACAO_AVISO = 4;
    const ESPERA_DO_TEMPLO = 0.5;            // quem chega socando do fim da fase não compra sem querer
    const DURACAO_DO_RECADO = 2.5;
    const GEOMETRIA_DO_TEMPLO = { y0: 222, passo: 32 };   // 7 itens + Seguir não cabem no passo de 46

    // Texto novo pro jogador (pt-BR), num lugar só — a tradução da Fase 3 começa daqui.
    const TEXTOS = {
        templo: 'Templo',
        seguir: 'Seguir',
        voltar: 'Voltar',
        aprendido: 'aprendido',
        faltaKarma: 'falta karma',
        karma: n => `${n} karma`,
        ganhoNaFase: n => `+${n} karma nesta fase`,
        saldo: n => `saldo ${n} karma`,
        aprendeu: nome => `Aprendeu: ${nome}`,
        proximaFase: (n, nome) => `Fase ${n} · ${nome}`,
        recusa: { sem_karma: 'Falta karma pra esse golpe.', aprendido: 'Esse golpe você já sabe.', desconhecido: 'Esse golpe não existe.' },
        dicaTemplo: '↑ ↓ ESCOLHEM · ENTER APRENDE · ESC SAI',
        dicaTemploToque: 'TOQUE NUM GOLPE PRA LER · DE NOVO PRA APRENDER · TOQUE EMBAIXO SAI',
    };

    const canvas = document.getElementById('tela');
    const ctx = canvas.getContext('2d');
    const plataforma = Plataforma.criar();
    const progresso = Progresso.criar(plataforma);
    const loja = Loja.criar(progresso);
    const som = Som.criar();
    const entrada = Entrada.criar({ joystick: document.getElementById('joystick'), joystickBolinha: document.getElementById('bolinha'), botoes: document.getElementById('botoes') });
    let efeitos = Desenho.criarEfeitos();
    if (plataforma.ehDesktop) document.body.classList.add('desktop');

    const jogo = {
        tela: 'titulo', tempo: 0, mundo: null, fase: 1, faseInicial: 1, pausado: false, intro: 0, acumulador: 0, fimHa: 0,
        sel: null, salvos: null, pontuacaoNaFase: 0, toque: false, indice: 0, confirmarApagar: false,
        estatisticas: null, fim: null, semente: (Date.now() & 0xffff) || 1, tocouAgora: null,
        avisosDeConquista: [], tempoDePartida: 0, faseFechada: false, escalaTotal: 1,
        karmaDaFase: 0, templo: null,
    };
    const IDS = Object.keys(Motor.PERSONAGENS);
    const conquistas = Conquistas.criar({ progresso, plataforma, aoDesbloquear: def => { jogo.avisosDeConquista.push({ def, idade: 0 }); som.tocar('item'); } });

    function opcoes() { return progresso.dados.opcoes; }
    function aplicarOpcoes() { som.volume({ musica: opcoes().musica, efeitos: opcoes().efeitos }); }
    aplicarOpcoes();

    // ── TAMANHO: 16:9 que cabe na janela; o canvas desenha na resolução REAL da tela ────────
    // O jogo é vetorial, então 1440p fica nítido de graça — é só o buffer acompanhar o DPR.
    // Qualidade que se ajusta sozinha: se a máquina não segura ~45 fps por dois segundos, o
    // buffer cai pra 1× (e depois o desenho pra "média"). Não grava nada — na próxima vez tenta de novo.
    const desempenho = { dprMax: 2, lentoHa: 0, rebaixado: false };
    function medirDesempenho(dt) {
        if (jogo.tela !== 'jogo') return;
        desempenho.lentoHa = dt > 0.022 ? desempenho.lentoHa + dt : Math.max(0, desempenho.lentoHa - dt * 0.5);
        if (desempenho.lentoHa < 2) return;
        desempenho.lentoHa = 0;
        if (desempenho.dprMax > 1) { desempenho.dprMax = 1; ajustarTamanho(); console.info('punhos: fps baixo, buffer em 1×'); }
        else if (!desempenho.rebaixado) { desempenho.rebaixado = true; console.info('punhos: fps baixo, desenho em qualidade média'); }
    }
    function qualidadeAtual() { return desempenho.rebaixado ? 'media' : opcoes().qualidade; }

    function ajustarTamanho() {
        const escala = Math.min(raiz.innerWidth / Motor.LARGURA, raiz.innerHeight / Motor.ALTURA);
        const dpr = Math.min(raiz.devicePixelRatio || 1, opcoes().qualidade === 'media' ? 1 : desempenho.dprMax);
        canvas.style.width = `${Math.floor(Motor.LARGURA * escala)}px`;
        canvas.style.height = `${Math.floor(Motor.ALTURA * escala)}px`;
        canvas.width = Math.max(1, Math.round(Motor.LARGURA * escala * dpr));
        canvas.height = Math.max(1, Math.round(Motor.ALTURA * escala * dpr));
        jogo.escalaTotal = canvas.width / Motor.LARGURA;
    }
    raiz.addEventListener('resize', ajustarTamanho);
    ajustarTamanho();

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
    raiz.addEventListener('blur', () => { if (jogo.tela === 'jogo' && !jogo.pausado) jogo.pausado = true; });

    const botaoTelaCheia = document.getElementById('tela-cheia');
    if (botaoTelaCheia) botaoTelaCheia.addEventListener('click', () => plataforma.telaCheia());

    function mudarTela(nome) { jogo.tela = nome; jogo.indice = 0; jogo.confirmarApagar = false; document.body.dataset.tela = nome; }

    // ── NAVEGAÇÃO DE MENU (teclado, controle e toque, com as mesmas linhas do desenho) ──────
    // `doisToques`: o toque só escolhe, e confirma no segundo toque no mesmo item (Templo: gasta karma).
    function navegar(quantos, sistema, entradas, toque, geometria, doisToques) {
        const r = { confirmou: false, voltou: !!sistema.voltar, esquerda: false, direita: false };
        const e0 = entradas[0], e1 = entradas[1];
        if (e0.apertou.cima || e1.apertou.cima) { jogo.indice = (jogo.indice + quantos - 1) % quantos; som.tocar('selecionar'); }
        if (e0.apertou.baixo || e1.apertou.baixo) { jogo.indice = (jogo.indice + 1) % quantos; som.tocar('selecionar'); }
        if (e0.apertou.esquerda || e1.apertou.esquerda) r.esquerda = true;
        if (e0.apertou.direita || e1.apertou.direita) r.direita = true;
        if (sistema.confirmar || e0.apertou.soco || e1.apertou.soco) r.confirmou = true;
        if (toque) {
            // A faixa tocável é a faixa pintada: a conta mora no desenho.js (conferida no Node).
            const t = Desenho.toqueNoMenu(geometria, quantos, jogo.indice, toque, doisToques);
            if (t) {
                if (t.indice !== jogo.indice && !t.confirmou) som.tocar('selecionar');
                jogo.indice = t.indice;
                if (t.confirmou) r.confirmou = true;
            } else if (toque.y > 480) r.voltou = true;
        }
        return r;
    }

    // ── FASES ─────────────────────────────────────────────────────────────────────────────
    function personagensEscolhidos() {
        const lista = [IDS[jogo.sel.p1]];
        if (jogo.sel.p2Entrou) lista.push(IDS[jogo.sel.p2]);
        return lista;
    }

    function iniciarFase(numero, extra) {
        const o = extra || {};
        jogo.fase = numero;
        const personagens = o.personagens || personagensEscolhidos();
        jogo.mundo = Motor.criarMundo({ fase: numero, jogadores: personagens, semente: jogo.semente * 31 + numero, pontuacao: o.pontuacao || 0, dificuldade: progresso.dados.dificuldade, liberados: loja.liberados() });
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
        jogo.acumulador = 0; jogo.pausado = false; jogo.intro = 0; jogo.faseFechada = false; jogo.fimHa = 0;
        som.pararMusica();
        plataforma.presenca(`Lutando em ${jogo.mundo.faseDef.nome}`);
        mudarTela('intro');
    }

    function salvarJogadores() { jogo.salvos = jogo.mundo.jogadores.map(j => ({ vidas: j.vidas, chi: j.chi, vida: j.vida })); }

    function fecharFase(m) {
        if (jogo.faseFechada) return;
        jogo.faseFechada = true;
        conquistas.concluirFase(m);
        // Karma pelos pontos ganhos NESTA fase (a pontuação é da partida inteira).
        jogo.karmaDaFase = loja.receberDaFase(jogo.pontuacaoNaFase, m.pontuacao);
        if (jogo.fase < Motor.FASES.length - 1) progresso.registrarFase(jogo.fase + 1);
        progresso.somar('tempoJogado', Math.floor(jogo.tempoDePartida)); jogo.tempoDePartida = 0;
        progresso.salvar();
    }

    function terminar(vitoria) {
        const m = jogo.mundo;
        const novoRecorde = progresso.registrarRecorde(m.pontuacao);
        progresso.somar('partidas');
        progresso.somar('tempoJogado', Math.floor(jogo.tempoDePartida)); jogo.tempoDePartida = 0;
        progresso.salvar();
        plataforma.estatistica('pontuacao_maxima', progresso.dados.recorde);
        jogo.fim = { vitoria, pontuacao: m.pontuacao, recorde: progresso.dados.recorde, novoRecorde, faseNome: m.faseDef.nome, toque: jogo.toque, ...jogo.estatisticas };
        jogo.fimHa = 0;
        som.pararMusica();
        som.tocar(vitoria ? 'gongo' : 'morte-jogador');
        mudarTela('fim');
    }

    function irParaMenu() {
        jogo.mundo = null; jogo.salvos = null; jogo.pausado = false;
        jogo.semente = (Date.now() & 0xffff) || 1;
        efeitos = Desenho.criarEfeitos();
        if (som.ligado) som.musica('titulo');
        plataforma.presenca('No menu');
        mudarTela('menu');
    }

    // ── TEMPLO: a loja de golpes. Entre as fases (origem 'fase') e pelo menu (origem 'menu'). ──
    function abrirTemplo(o) {
        jogo.templo = { origem: o.origem, proxima: o.proxima, pontuacao: o.pontuacao || 0, karmaGanho: o.origem === 'fase' ? jogo.karmaDaFase : null, recado: '', recadoHa: 0, abertoHa: 0 };
        mudarTela('templo');
        // Vindo da luta, o cursor começa no "Seguir": apertar sem querer segue em vez de gastar karma.
        if (o.origem === 'fase') jogo.indice = itensDoTemplo().length - 1;
        if (som.ligado) som.musica('titulo');
        plataforma.presenca('No Templo');
    }
    function itensDoTemplo() {
        const t = jogo.templo;
        const itens = loja.itens().map(i => ({
            id: i.id, rotulo: i.nome, desabilitado: i.aprendido, detalhe: i.descricao,
            valor: i.aprendido ? TEXTOS.aprendido : i.podeComprar ? TEXTOS.karma(i.preco) : `${TEXTOS.karma(i.preco)} · ${TEXTOS.faltaKarma}`,
        }));
        if (t.origem === 'fase') itens.push({ id: 'sair', rotulo: TEXTOS.seguir, detalhe: TEXTOS.proximaFase(t.proxima, Motor.FASES[t.proxima].nome) });
        else itens.push({ id: 'sair', rotulo: TEXTOS.voltar });
        return itens;
    }
    function sairDoTemplo() {
        const t = jogo.templo;
        jogo.templo = null;
        if (t.origem === 'fase') iniciarFase(t.proxima, { pontuacao: t.pontuacao });
        else irParaMenu();
    }
    function subtituloDoTemplo() {
        const t = jogo.templo;
        if (t.recadoHa > 0) return t.recado;
        const saldo = TEXTOS.saldo(loja.saldo());
        return t.karmaGanho != null ? `${TEXTOS.ganhoNaFase(t.karmaGanho)} · ${saldo}` : saldo;
    }

    function comecarEscolha(faseInicial) {
        jogo.faseInicial = faseInicial;
        jogo.sel = { p1: 0, p2: 1, p2Entrou: false, confirmadoP1: false, confirmadoP2: false, prontoHa: 0, contagem: 0 };
        jogo.estatisticas = { finalizacoes: 0, maiorCombo: 0, inimigos: 0 };
        jogo.salvos = null;
        mudarTela('selecao');
    }

    function itensDoMenu() {
        const fase = progresso.dados.faseAlcancada;
        const itens = [{ id: 'novo', rotulo: 'Novo jogo' }];
        if (fase > 1) itens.push({ id: 'continuar', rotulo: 'Continuar', valor: `Fase ${fase} · ${Motor.FASES[fase].nome}`, destaque: true });
        itens.push({ id: 'opcoes', rotulo: 'Opções' });
        itens.push({ id: 'conquistas', rotulo: 'Conquistas', valor: `${conquistas.ganhas().length} / ${Conquistas.LISTA.length}` });
        itens.push({ id: 'templo', rotulo: TEXTOS.templo, valor: TEXTOS.karma(loja.saldo()) });
        if (plataforma.ehDesktop) itens.push({ id: 'sair', rotulo: 'Sair' });
        return itens;
    }

    const DIFICULDADES = [
        { id: 'facil', rotulo: 'Fácil', detalhe: 'Inimigos com 70% da vida e 65% do dano. Pra conhecer o templo.' },
        { id: 'normal', rotulo: 'Normal', detalhe: 'Como o jogo foi desenhado.' },
        { id: 'dificil', rotulo: 'Difícil', detalhe: 'Inimigos com 130% da vida e 140% do dano. Defenda ou morra.' },
    ];

    function itensDeOpcoes() {
        const o = opcoes();
        return [
            { id: 'musica', rotulo: 'Música', fracao: o.musica },
            { id: 'efeitos', rotulo: 'Efeitos', fracao: o.efeitos },
            { id: 'tremor', rotulo: 'Tremor de tela', valor: o.tremor ? 'ligado' : 'desligado' },
            { id: 'telaCheia', rotulo: 'Tela cheia', valor: o.telaCheia ? 'ligada' : 'desligada' },
            { id: 'qualidade', rotulo: 'Qualidade visual', valor: o.qualidade === 'media' ? 'média' : 'alta', detalhe: 'Média desliga sombra projetada, névoa e grão — pra máquina fraca.' },
            { id: 'apagar', rotulo: jogo.confirmarApagar ? 'Apagar progresso — confirme de novo' : 'Apagar progresso', valor: `fase ${progresso.dados.faseAlcancada} · recorde ${progresso.dados.recorde.toLocaleString('pt-BR')}` },
            { id: 'voltar', rotulo: 'Voltar' },
        ];
    }

    function mexerOpcao(item, direcao) {
        const o = opcoes();
        if (item.id === 'musica' || item.id === 'efeitos') {
            const atual = Math.round(o[item.id] * 10);
            const novo = direcao === 0 ? (atual + 1) % 11 : Math.min(10, Math.max(0, atual + direcao));
            progresso.opcao(item.id, novo / 10); aplicarOpcoes(); som.tocar('selecionar');
        } else if (item.id === 'tremor') { progresso.opcao('tremor', !o.tremor); som.tocar('selecionar'); }
        else if (item.id === 'qualidade') { progresso.opcao('qualidade', o.qualidade === 'media' ? 'alta' : 'media'); desempenho.rebaixado = false; ajustarTamanho(); som.tocar('selecionar'); }
        else if (item.id === 'telaCheia') { progresso.opcao('telaCheia', !o.telaCheia); plataforma.telaCheia(opcoes().telaCheia); som.tocar('selecionar'); }
        else if (item.id === 'apagar' && direcao === 0) {
            if (!jogo.confirmarApagar) { jogo.confirmarApagar = true; som.tocar('negado'); }
            else { progresso.apagar(); jogo.confirmarApagar = false; som.tocar('quebra'); }
        } else if (item.id === 'voltar' && direcao === 0) irParaMenu();
    }

    // ── ATUALIZAÇÃO ───────────────────────────────────────────────────────────────────────
    function atualizar(dt) {
        const sistema = entrada.lerSistema();
        const entradas = entrada.ler();
        const toque = jogo.tocouAgora; jogo.tocouAgora = null;
        if (sistema.mudo) som.alternarMudo();
        if (sistema.telaCheia) plataforma.telaCheia();
        for (let k = jogo.avisosDeConquista.length - 1; k >= 0; k--) { const a = jogo.avisosDeConquista[k]; if (k === 0) a.idade += dt; if (a.idade >= DURACAO_AVISO) jogo.avisosDeConquista.splice(k, 1); }
        const fundoVivo = () => efeitos.atualizar(dt, { camera: { x: jogo.tempo * 30 } }, 'patio');

        switch (jogo.tela) {
            case 'titulo': {
                if (som.ligado) som.musica('titulo');
                fundoVivo();
                if (sistema.confirmar || toque || entradas[0].apertou.soco) { som.ligar(); som.tocar('confirmar'); irParaMenu(); }
                break;
            }
            case 'menu': {
                fundoVivo();
                const itens = itensDoMenu();
                const nav = navegar(itens.length, sistema, entradas, toque);
                if (nav.confirmou) {
                    const item = itens[jogo.indice];
                    som.tocar('confirmar');
                    if (item.id === 'novo') { mudarTela('dificuldade'); jogo.indice = Math.max(0, DIFICULDADES.findIndex(d => d.id === progresso.dados.dificuldade)); }
                    else if (item.id === 'continuar') comecarEscolha(progresso.dados.faseAlcancada);
                    else if (item.id === 'opcoes') mudarTela('opcoes');
                    else if (item.id === 'conquistas') mudarTela('conquistas');
                    else if (item.id === 'templo') abrirTemplo({ origem: 'menu' });
                    else if (item.id === 'sair') plataforma.sair();
                }
                break;
            }
            case 'dificuldade': {
                fundoVivo();
                const nav = navegar(DIFICULDADES.length, sistema, entradas, toque);
                if (nav.voltou) { irParaMenu(); break; }
                if (nav.confirmou) { progresso.dificuldade(DIFICULDADES[jogo.indice].id); som.tocar('confirmar'); comecarEscolha(1); }
                break;
            }
            case 'opcoes': {
                fundoVivo();
                const itens = itensDeOpcoes();
                const nav = navegar(itens.length, sistema, entradas, toque);
                if (nav.voltou) { irParaMenu(); break; }
                const item = itens[jogo.indice];
                if (nav.esquerda) mexerOpcao(item, -1);
                else if (nav.direita) mexerOpcao(item, 1);
                else if (nav.confirmou) mexerOpcao(item, 0);
                if (item.id !== 'apagar' && (nav.esquerda || nav.direita || nav.confirmou)) jogo.confirmarApagar = false;
                break;
            }
            case 'conquistas': {
                fundoVivo();
                const nav = navegar(1, sistema, entradas, toque);
                if (nav.voltou || nav.confirmou) irParaMenu();
                break;
            }
            case 'templo': {
                fundoVivo();
                const t = jogo.templo;
                t.abertoHa += dt;
                t.recadoHa = Math.max(0, t.recadoHa - dt);
                if (t.abertoHa < ESPERA_DO_TEMPLO) break;
                const itens = itensDoTemplo();
                const nav = navegar(itens.length, sistema, entradas, toque, GEOMETRIA_DO_TEMPLO, true);
                if (nav.voltou) { som.tocar('confirmar'); sairDoTemplo(); break; }
                if (!nav.confirmou) break;
                const item = itens[jogo.indice];
                if (item.id === 'sair') { som.tocar('confirmar'); sairDoTemplo(); break; }
                const r = loja.comprar(item.id);
                if (r.ok) { som.tocar('confirmar'); conquistas.processar([r.evento]); t.recado = TEXTOS.aprendeu(item.rotulo); }
                else { som.tocar('negado'); t.recado = TEXTOS.recusa[r.motivo] || r.motivo; }
                t.recadoHa = DURACAO_DO_RECADO;
                break;
            }
            case 'selecao': {
                const s = jogo.sel;
                fundoVivo();
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
                if (sistema.voltar) { irParaMenu(); break; }
                if (s.prontoHa >= 99 || (s.confirmadoP1 && s.p2Entrou && s.confirmadoP2)) {
                    s.prontoHa = 99;
                    s.contagem += dt;
                    if (s.contagem > 0.5) iniciarFase(jogo.faseInicial);
                }
                break;
            }
            case 'intro': {
                jogo.intro += dt;
                if (jogo.intro >= DURACAO_INTRO || sistema.confirmar) { mudarTela('jogo'); som.musica(jogo.mundo.faseDef.cenario); }
                break;
            }
            case 'jogo': {
                const m = jogo.mundo;
                if (jogo.pausado && sistema.voltar) { irParaMenu(); break; }
                if (sistema.pausa) { jogo.pausado = !jogo.pausado; som.tocar('pausa'); }
                if (jogo.pausado) { if (sistema.confirmar) jogo.pausado = false; break; }
                jogo.tempoDePartida += dt;
                if ((sistema.entrarP2 || entradas[1].apertou.soco) && m.jogadores.length === 1 && !m.fimDeJogo) {
                    jogo.sel.p2Entrou = true; jogo.sel.p2 = 1 - jogo.sel.p1;
                    Motor.adicionarJogador(m, IDS[jogo.sel.p2]);
                    efeitos.processar(m.eventos, m, som); m.eventos = [];
                    som.tocar('gongo');
                }
                // Congelamento de acerto: a tela para por um instante, o mundo não anda.
                if (efeitos.congelar > 0) efeitos.congelar -= dt;
                else {
                    const escala = efeitos.lento > 0 ? 0.3 : 1;
                    jogo.acumulador = Math.min(jogo.acumulador + dt * escala, PASSO * 5);
                    let primeiro = true;
                    while (jogo.acumulador >= PASSO) {
                        Motor.passo(m, PASSO, entradas);
                        efeitos.processar(m.eventos, m, som);
                        conquistas.processar(m.eventos, m);
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
                if (m.concluida) {
                    fecharFase(m);
                    if (m.concluidaHa > 2.2) {
                        salvarJogadores();
                        // Última fase: o karma já entrou no fecharFase e segue pro fim. As outras passam pelo Templo.
                        if (jogo.fase >= Motor.FASES.length - 1) terminar(true);
                        else abrirTemplo({ origem: 'fase', proxima: jogo.fase + 1, pontuacao: m.pontuacao });
                    }
                }
                if (m.fimDeJogo) { jogo.fimHa += dt; if (jogo.fimHa > 1.6) terminar(false); }
                break;
            }
            case 'fim': {
                jogo.fimHa += dt;
                fundoVivo();
                if (jogo.fimHa < 0.8) break;
                if (sistema.voltar || (jogo.fim.vitoria && (sistema.confirmar || toque))) { irParaMenu(); break; }
                if (sistema.confirmar || toque) {
                    // Tentar de novo: a mesma fase, três vidas, os pontos de quando ela começou.
                    jogo.salvos = null;
                    jogo.estatisticas = { finalizacoes: 0, maiorCombo: 0, inimigos: 0 };
                    iniciarFase(jogo.fase, { personagens: jogo.mundo.jogadores.map(j => j.personagem), pontuacao: jogo.pontuacaoNaFase });
                }
                break;
            }
            default: break;
        }
    }

    // ── DESENHO ───────────────────────────────────────────────────────────────────────────
    function desenhar() {
        ctx.setTransform(jogo.escalaTotal, 0, 0, jogo.escalaTotal, 0, 0);
        ctx.clearRect(0, 0, Motor.LARGURA, Motor.ALTURA);
        const extras = { recorde: progresso.dados.recorde, toque: jogo.toque, mudo: som.silenciado, tremor: opcoes().tremor, qualidade: qualidadeAtual() };
        const dicaVoltar = jogo.toque ? 'TOQUE NUM ITEM · TOQUE EMBAIXO VOLTA' : '↑ ↓ ESCOLHEM · ENTER CONFIRMA · ESC VOLTA';
        switch (jogo.tela) {
            case 'titulo': Desenho.desenharTitulo(ctx, jogo.tempo, efeitos, extras); break;
            case 'menu': Desenho.desenharMenu(ctx, jogo.tempo, efeitos, { itens: itensDoMenu(), indice: jogo.indice, dica: `${dicaVoltar}${plataforma.temSteam ? ' · STEAM CONECTADA' : ''}`, subtitulo: `Dificuldade ${progresso.dados.dificuldade} · recorde ${progresso.dados.recorde.toLocaleString('pt-BR')}` }); break;
            case 'dificuldade': Desenho.desenharMenu(ctx, jogo.tempo, efeitos, { titulo: 'Dificuldade', itens: DIFICULDADES, indice: jogo.indice, dica: dicaVoltar }); break;
            case 'opcoes': Desenho.desenharMenu(ctx, jogo.tempo, efeitos, { titulo: 'Opções', itens: itensDeOpcoes(), indice: jogo.indice, dica: jogo.toque ? 'TOQUE NUM ITEM PRA MUDAR · TOQUE EMBAIXO VOLTA' : '← → MUDAM · ENTER CONFIRMA · ESC VOLTA' }); break;
            case 'templo': Desenho.desenharMenu(ctx, jogo.tempo, efeitos, Object.assign({ titulo: TEXTOS.templo, subtitulo: subtituloDoTemplo(), itens: itensDoTemplo(), indice: jogo.indice, dica: jogo.toque ? TEXTOS.dicaTemploToque : TEXTOS.dicaTemplo }, GEOMETRIA_DO_TEMPLO)); break;
            case 'conquistas': Desenho.desenharConquistas(ctx, jogo.tempo, efeitos, { lista: Conquistas.LISTA.map(def => ({ def, ganha: !!progresso.dados.conquistas[def.id] })), dica: jogo.toque ? 'TOQUE PARA VOLTAR' : 'ESC OU ENTER VOLTA' }); break;
            case 'selecao': Desenho.desenharSelecao(ctx, jogo.tempo, efeitos, Object.assign({ toque: jogo.toque }, jogo.sel)); break;
            case 'intro': Desenho.desenharIntroFase(ctx, jogo.mundo.faseDef, jogo.intro / DURACAO_INTRO); break;
            case 'jogo':
                Desenho.desenharMundo(ctx, jogo.mundo, efeitos, jogo.tempo, extras);
                if (jogo.pausado) Desenho.desenharPausa(ctx, extras);
                break;
            case 'fim': Desenho.desenharFim(ctx, jogo.tempo, efeitos, jogo.fim); break;
            default: break;
        }
        if (jogo.avisosDeConquista.length) { const a = jogo.avisosDeConquista[0]; Desenho.desenharAvisoDeConquista(ctx, a.def, a.idade / DURACAO_AVISO); }
    }

    let anterior = performance.now();
    function laco(agora) {
        const dtReal = (agora - anterior) / 1000;
        const dt = Math.min(0.05, dtReal);
        anterior = agora;
        jogo.tempo += dt;
        medirDesempenho(dtReal);
        atualizar(dt);
        desenhar();
        raiz.requestAnimationFrame(laco);
    }

    // As fontes do Google podem não vir (sem internet): o jogo não espera por elas mais de 1,5 s.
    const esperaFontes = document.fonts && document.fonts.ready ? Promise.race([document.fonts.ready, new Promise(r => setTimeout(r, 1500))]) : Promise.resolve();
    esperaFontes.then(() => { anterior = performance.now(); raiz.requestAnimationFrame(laco); });

    raiz.PunhosDeShaolin.jogo = jogo;            // pra inspecionar no console
    raiz.PunhosDeShaolin.progresso = progresso;
    raiz.PunhosDeShaolin.loja = loja;
})(window);
