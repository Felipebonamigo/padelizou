// PUNHOS DE SHAOLIN — o motor de combate. Lógica pura: nada de canvas, som ou teclado aqui.
//
// É um beat-em-up de "cinto" (belt-scroll) no estilo Mortal Kombat: Shaolin Monks — o mundo é
// uma faixa de chão com profundidade (y de 0 a 1), altura (z, pra pulo e lançamento) e um eixo
// x que rola com a câmera. Ondas de inimigos travam a câmera; limpar a onda libera o "SIGA →".
//
// Por que separado do desenho: pra rodar no Node sem navegador. O conferidor
// `Padelizou.Tests/js/conferir-punhos-de-shaolin.js` carrega SÓ este arquivo, dá entradas
// quadro a quadro e confere dano, lançamento, defesa, ondas, IA e finalização. Nada aqui usa
// Math.random: todo acaso sai de `mundo.rng`, semeado — mesma semente, mesma luta.
//
// Unidades: x e z em pixels do mundo; y em fração da faixa de chão; tempo em segundos.
(function (raiz, fabrica) {
    const Motor = fabrica();
    if (typeof module !== 'undefined' && module.exports) module.exports = Motor;
    else { raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {}; raiz.PunhosDeShaolin.Motor = Motor; }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const LARGURA = 960, ALTURA = 540;
    const CHAO_TOPO = 335, CHAO_BASE = 525;          // faixa de chão na tela (y=0 em cima, y=1 embaixo)
    const GRAVIDADE = 2000;
    const TOLERANCIA_Y = 0.14;                       // duas coisas "na mesma linha" ficam a menos disso
    const PULO = 640;
    const MEIA_LARGURA = 18;                         // meia largura do corpo (× escala)
    const ALTURA_CORPO = 72;
    const ATACANTES_MAX = 2;                         // quantos inimigos atacam ao mesmo tempo — é o que deixa a luta justa
    // Dificuldade mexe só em DUAS coisas: quanto o inimigo aguenta e quanto ele machuca. A IA é a
    // mesma — jogo difícil por inimigo burro que bate forte é injusto de um jeito, e por inimigo
    // esperto que bate fraco é injusto de outro.
    const DIFICULDADES = { facil: { vida: 0.7, dano: 0.65 }, normal: { vida: 1, dano: 1 }, dificil: { vida: 1.3, dano: 1.4 } };

    // ── MELHORIAS (compradas no Templo com karma) ─────────────────────────────────────────
    // O motor é a camada de baixo: não importa `loja.js`. Ele só conhece estes ids, e o conferidor
    // da loja garante que todo item à venda está aqui. `criarMundo({ liberados })` recebe os
    // comprados; id que não está nesta lista é ignorado (salvamento de outra versão, digitação).
    const MELHORIAS = Object.freeze(['sequencia_cinco', 'contra_golpe', 'especial_aereo', 'agarrao_costas', 'vigor', 'respiracao', 'punhos_de_ferro']);
    const VIGOR = 1.2;                               // × vida máxima
    const RESPIRACAO = 3;                            // chi por segundo parado, andando ou defendendo
    const PUNHOS_DE_FERRO = 1.15;                    // × dano dos golpes do jogador, arredondado
    const JANELA_DE_APARAR = 0.15;                   // defender até isso ANTES de o golpe ligar é aparar
    const ATORDOADO_DO_CONTRA = 0.9;
    const CHI_DO_CONTRA = 10;
    const DANO_DO_SUPLEX = 20;
    const DURACAO_DO_SUPLEX = 0.5;
    const EPSILON = 1e-6;                            // o tempo é soma de 1/60: compara com folga

    // ── ACASO SEMEADO (mulberry32) ────────────────────────────────────────────────────────
    function criarRng(semente) {
        let s = (semente >>> 0) || 1;
        const proximo = () => {
            s = (s + 0x6D2B79F5) >>> 0;
            let t = s;
            t = Math.imul(t ^ (t >>> 15), t | 1);
            t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
        return {
            proximo,
            entre: (a, b) => a + proximo() * (b - a),
            chance: p => proximo() < p,
            escolher: lista => lista[Math.floor(proximo() * lista.length)],
        };
    }

    // ── OS GOLPES ─────────────────────────────────────────────────────────────────────────
    // inicio: segundos até a caixa de acerto ligar · ativo: quanto tempo fica ligada · total: duração
    // alcance: até onde vai na frente · altura: até que z acerta · recuo: empurrão (px/s)
    // lanca: joga pro alto com essa velocidade · derruba: derruba no chão · proximo: o próximo da sequência
    const PERSONAGENS = {
        long: {
            id: 'long', nome: 'Long', titulo: 'O Dragão do Templo',
            vida: 100, velocidade: 250, corrida: 430, escala: 1,
            cores: { pele: '#e0a878', roupa: '#c9341f', faixa: '#f2c14e', cabelo: '#1a1a1a', detalhe: '#7a1a0f' },
            arma: null,
            golpes: {
                soco1: { dano: 5, inicio: 0.06, ativo: 0.08, total: 0.26, alcance: 62, altura: 85, recuo: 80, congela: 0.03, proximo: 'soco2', som: 'soco' },
                soco2: { dano: 6, inicio: 0.06, ativo: 0.08, total: 0.28, alcance: 66, altura: 85, recuo: 100, congela: 0.03, proximo: 'soco3', proximoCinco: 'soco4', som: 'soco' },
                // soco4 e soco5 só entram na corrente com a Sequência de Cinco (`proximoCinco` do soco2).
                // Recuo baixo e um passo à frente (`avanco`) de propósito: sem o passo, o recuo deles se
                // somava ao do soco1/soco2 e, na parte de fora do alcance, o alvo saía antes do lançador —
                // comprar a melhoria TIRAVA o lançamento. 40 já fecha a conta; 80 é folga (conferidor varre dx).
                soco4: { dano: 6, inicio: 0.06, ativo: 0.08, total: 0.28, alcance: 70, altura: 85, recuo: 50, avanco: 80, congela: 0.03, proximo: 'soco5', som: 'soco' },
                soco5: { dano: 7, inicio: 0.07, ativo: 0.08, total: 0.30, alcance: 72, altura: 90, recuo: 50, avanco: 80, congela: 0.04, proximo: 'soco3', som: 'chute' },
                soco3: { dano: 10, inicio: 0.10, ativo: 0.10, total: 0.46, alcance: 70, altura: 95, recuo: 140, lanca: 560, congela: 0.06, som: 'chute', texto: 'LANÇOU!' },
                chute: { dano: 12, inicio: 0.12, ativo: 0.10, total: 0.50, alcance: 84, altura: 95, recuo: 380, derruba: true, congela: 0.06, som: 'chute' },
                chuteAereo: { dano: 10, inicio: 0.05, ativo: 0.28, total: 0.45, alcance: 72, altura: 130, recuo: 320, derruba: true, congela: 0.05, som: 'chute' },
                investida: { dano: 14, inicio: 0.08, ativo: 0.24, total: 0.55, alcance: 82, altura: 100, recuo: 440, derruba: true, avanco: 400, congela: 0.07, som: 'chute', texto: 'VOO DO DRAGÃO' },
                especial: { nome: 'Sopro do Dragão', tipo: 'projetil', projetil: 'fogo', dano: 22, chi: 30, inicio: 0.22, total: 0.62, velocidade: 640, altura: 95, recuo: 320, derruba: true, som: 'fogo' },
                // Especial no Ar (melhoria): mergulho em chamas na diagonal pra baixo. `mergulho` = [vx, vz] no início.
                especialAereo: { nome: 'Mergulho do Dragão', dano: 18, inicio: 0.04, ativo: 0.40, total: 0.60, alcance: 80, altura: 120, recuo: 360, derruba: true, mergulho: [520, -520], congela: 0.06, som: 'fogo' },
                joelhada: { dano: 7, inicio: 0.08, ativo: 0.06, total: 0.32, alcance: 50, altura: 85, recuo: 0, congela: 0.03, som: 'soco' },
            },
        },
        shen: {
            id: 'shen', nome: 'Shen', titulo: 'O Bastão da Montanha',
            vida: 120, velocidade: 220, corrida: 390, escala: 1.04,
            cores: { pele: '#c98f5f', roupa: '#e8a13a', faixa: '#6b2a10', cabelo: '#2b2b2b', detalhe: '#a8611c' },
            arma: 'bastao',
            golpes: {
                soco1: { dano: 7, inicio: 0.07, ativo: 0.09, total: 0.30, alcance: 82, altura: 90, recuo: 90, congela: 0.03, proximo: 'soco2', som: 'bastao' },
                soco2: { dano: 7, inicio: 0.07, ativo: 0.09, total: 0.30, alcance: 86, altura: 90, recuo: 110, congela: 0.03, proximo: 'soco3', proximoCinco: 'soco4', som: 'bastao' },
                soco4: { dano: 7, inicio: 0.07, ativo: 0.09, total: 0.30, alcance: 90, altura: 90, recuo: 60, avanco: 80, congela: 0.03, proximo: 'soco5', som: 'bastao' },
                soco5: { dano: 8, inicio: 0.07, ativo: 0.09, total: 0.32, alcance: 92, altura: 95, recuo: 60, avanco: 80, congela: 0.04, proximo: 'soco3', som: 'bastao' },
                soco3: { dano: 12, inicio: 0.12, ativo: 0.10, total: 0.50, alcance: 90, altura: 100, recuo: 160, lanca: 600, congela: 0.06, som: 'bastao', texto: 'LANÇOU!' },
                chute: { dano: 10, inicio: 0.14, ativo: 0.12, total: 0.55, alcance: 92, altura: 60, recuo: 300, derruba: true, dosDoisLados: true, congela: 0.05, som: 'bastao', texto: 'VARRIDA' },
                chuteAereo: { dano: 12, inicio: 0.06, ativo: 0.26, total: 0.45, alcance: 80, altura: 130, recuo: 320, derruba: true, congela: 0.05, som: 'bastao' },
                investida: { dano: 16, inicio: 0.08, ativo: 0.22, total: 0.55, alcance: 104, altura: 100, recuo: 460, derruba: true, avanco: 360, congela: 0.07, som: 'bastao', texto: 'ESTOCADA' },
                // Especial no Ar (melhoria): cai com o bastão e bate no chão dos dois lados.
                especialAereo: { nome: 'Queda da Montanha', dano: 16, inicio: 0.10, ativo: 0.14, total: 0.55, alcance: 100, altura: 70, recuo: 380, derruba: true, dosDoisLados: true, mergulho: [0, -900], congela: 0.08, som: 'pancada', tremor: 1 },
                especial: { nome: 'Tempestade do Bastão', tipo: 'giro', dano: 9, chi: 35, inicio: 0.10, ativo: 0.50, total: 0.75, alcance: 100, altura: 110, recuo: 260, dosDoisLados: true, repete: 0.16, derrubaNoFim: true, congela: 0.03, som: 'bastao' },
                joelhada: { dano: 8, inicio: 0.08, ativo: 0.06, total: 0.32, alcance: 50, altura: 85, recuo: 0, congela: 0.03, som: 'soco' },
            },
        },
    };

    const INIMIGOS = {
        sombra: {
            id: 'sombra', nome: 'Sombra', vida: 30, velocidade: 175, pontos: 100, escala: 0.98,
            cores: { pele: '#d9b08c', roupa: '#23232b', faixa: '#5b1f8a', cabelo: '#111', detalhe: '#3a3a48' }, arma: null, capuz: true,
            golpes: {
                soco1: { dano: 6, inicio: 0.18, ativo: 0.10, total: 0.50, alcance: 58, altura: 85, recuo: 120, congela: 0.02, som: 'soco' },
                chute: { dano: 9, inicio: 0.26, ativo: 0.10, total: 0.68, alcance: 72, altura: 90, recuo: 280, derruba: true, congela: 0.04, som: 'chute' },
            },
            ia: { alcance: 56, agressividade: 0.55, defende: 0.15, pausa: [0.25, 0.8], pesos: { soco1: 3, chute: 1 } },
        },
        garra: {
            id: 'garra', nome: 'Garra', vida: 55, velocidade: 205, pontos: 200, escala: 1.06,
            cores: { pele: '#9aa36b', roupa: '#4b3a2a', faixa: '#b8322a', cabelo: '#2a2a1a', detalhe: '#6c5a3d' }, arma: 'laminas',
            golpes: {
                soco1: { dano: 9, inicio: 0.16, ativo: 0.10, total: 0.48, alcance: 64, altura: 90, recuo: 140, congela: 0.03, som: 'lamina' },
                investida: { dano: 13, inicio: 0.10, ativo: 0.30, total: 0.70, alcance: 70, altura: 95, recuo: 380, derruba: true, avanco: 430, congela: 0.05, som: 'lamina' },
            },
            ia: { alcance: 60, agressividade: 0.7, defende: 0.05, pausa: [0.2, 0.7], pesos: { soco1: 3 }, investe: 0.35 },
        },
        bruto: {
            id: 'bruto', nome: 'Bruto', vida: 130, velocidade: 125, pontos: 350, escala: 1.38, armadura: true,
            cores: { pele: '#b9865d', roupa: '#5a5a66', faixa: '#2f2f38', cabelo: '#4a2a12', detalhe: '#8a8a99' }, arma: null,
            golpes: {
                soco1: { dano: 16, inicio: 0.36, ativo: 0.12, total: 0.95, alcance: 80, altura: 100, recuo: 400, derruba: true, congela: 0.07, som: 'pancada' },
                pancada: { dano: 20, inicio: 0.50, ativo: 0.14, total: 1.20, alcance: 110, altura: 40, recuo: 420, derruba: true, dosDoisLados: true, congela: 0.09, som: 'pancada', tremor: 1 },
            },
            ia: { alcance: 74, agressividade: 0.8, defende: 0, pausa: [0.4, 1.0], pesos: { soco1: 3, pancada: 1 } },
        },
        arqueiro: {
            id: 'arqueiro', nome: 'Arqueiro', vida: 35, velocidade: 185, pontos: 250, escala: 1,
            cores: { pele: '#d8b192', roupa: '#2e5d3a', faixa: '#a37a2c', cabelo: '#3b2a1a', detalhe: '#1e3d26' }, arma: 'arco',
            golpes: {
                flecha: { tipo: 'projetil', projetil: 'flecha', dano: 8, inicio: 0.45, total: 0.95, velocidade: 560, altura: 85, recuo: 200, som: 'flecha' },
                soco1: { dano: 5, inicio: 0.18, ativo: 0.10, total: 0.50, alcance: 50, altura: 85, recuo: 200, congela: 0.02, som: 'soco' },
            },
            ia: { alcance: 48, agressividade: 0.6, defende: 0, pausa: [0.5, 1.2], pesos: { flecha: 1 }, distancia: [240, 420] },
        },
        // ── CHEFES ──
        mestreSombra: {
            id: 'mestreSombra', nome: 'Mestre Sombra', vida: 320, velocidade: 245, pontos: 2000, escala: 1.12, chefe: true, teleporta: 3,
            cores: { pele: '#d9b08c', roupa: '#14141c', faixa: '#c9a227', cabelo: '#111', detalhe: '#5b1f8a' }, arma: null, capuz: true,
            golpes: {
                soco1: { dano: 7, inicio: 0.12, ativo: 0.09, total: 0.36, alcance: 62, altura: 88, recuo: 110, congela: 0.03, proximo: 'soco2', som: 'soco' },
                soco2: { dano: 7, inicio: 0.10, ativo: 0.09, total: 0.36, alcance: 66, altura: 88, recuo: 120, congela: 0.03, proximo: 'soco3', som: 'soco' },
                soco3: { dano: 12, inicio: 0.14, ativo: 0.10, total: 0.55, alcance: 72, altura: 95, recuo: 160, lanca: 520, congela: 0.05, som: 'chute' },
                chute: { dano: 14, inicio: 0.22, ativo: 0.12, total: 0.65, alcance: 84, altura: 95, recuo: 380, derruba: true, congela: 0.06, som: 'chute' },
                shuriken: { tipo: 'projetil', projetil: 'shuriken', dano: 10, inicio: 0.30, total: 0.70, velocidade: 700, altura: 85, recuo: 200, som: 'flecha' },
            },
            ia: { alcance: 60, agressividade: 0.85, defende: 0.3, pausa: [0.15, 0.5], pesos: { soco1: 3, chute: 2 }, arremessa: 0.35 },
        },
        graoPresa: {
            id: 'graoPresa', nome: 'Grão-Presa', vida: 450, velocidade: 195, pontos: 3000, escala: 1.5, chefe: true, armadura: true,
            cores: { pele: '#7f8a4c', roupa: '#3b2a1d', faixa: '#c02a2a', cabelo: '#1f1f12', detalhe: '#8a2020' }, arma: 'laminas',
            golpes: {
                soco1: { dano: 14, inicio: 0.18, ativo: 0.10, total: 0.55, alcance: 90, altura: 110, recuo: 200, congela: 0.04, proximo: 'soco2', som: 'lamina' },
                soco2: { dano: 16, inicio: 0.16, ativo: 0.12, total: 0.65, alcance: 96, altura: 110, recuo: 380, derruba: true, congela: 0.05, som: 'lamina' },
                investida: { dano: 18, inicio: 0.12, ativo: 0.34, total: 0.85, alcance: 90, altura: 110, recuo: 460, derruba: true, avanco: 480, congela: 0.07, som: 'lamina' },
                pancada: { dano: 20, inicio: 0.55, ativo: 0.14, total: 1.30, alcance: 130, altura: 50, recuo: 440, derruba: true, dosDoisLados: true, congela: 0.09, som: 'pancada', tremor: 1 },
            },
            ia: { alcance: 84, agressividade: 0.85, defende: 0, pausa: [0.3, 0.8], pesos: { soco1: 3, pancada: 1 }, investe: 0.45 },
        },
        gigante: {
            id: 'gigante', nome: 'O Gigante do Poço', vida: 560, velocidade: 135, pontos: 3500, escala: 1.75, chefe: true, armadura: true,
            cores: { pele: '#8d6b52', roupa: '#2c2f3a', faixa: '#3aa66a', cabelo: '#1a1a1a', detalhe: '#1f9a5a' }, arma: null,
            golpes: {
                soco1: { dano: 20, inicio: 0.40, ativo: 0.14, total: 1.05, alcance: 110, altura: 130, recuo: 460, derruba: true, congela: 0.08, som: 'pancada' },
                pancada: { dano: 26, inicio: 0.60, ativo: 0.16, total: 1.45, alcance: 160, altura: 50, recuo: 480, derruba: true, dosDoisLados: true, congela: 0.10, som: 'pancada', tremor: 1.5 },
            },
            ia: { alcance: 100, agressividade: 0.9, defende: 0, pausa: [0.4, 0.9], pesos: { soco1: 2, pancada: 2 } },
        },
        feiticeiro: {
            id: 'feiticeiro', nome: 'O Feiticeiro', vida: 640, velocidade: 210, pontos: 6000, escala: 1.18, chefe: true, teleporta: 2, invoca: true,
            cores: { pele: '#e3c7a5', roupa: '#3a0f3a', faixa: '#d4af37', cabelo: '#f0f0f0', detalhe: '#7a1fa0' }, arma: null, manto: true,
            golpes: {
                soco1: { dano: 9, inicio: 0.14, ativo: 0.10, total: 0.45, alcance: 66, altura: 90, recuo: 160, congela: 0.03, som: 'soco' },
                chama: { dano: 18, inicio: 0.40, ativo: 0.16, total: 1.00, alcance: 120, altura: 60, recuo: 420, derruba: true, dosDoisLados: true, congela: 0.08, som: 'fogo', texto: 'CHAMA DAS ALMAS' },
                caveira: { tipo: 'projetil', projetil: 'caveira', dano: 14, inicio: 0.35, total: 0.80, velocidade: 520, altura: 90, recuo: 320, derruba: true, som: 'fogo' },
            },
            ia: { alcance: 64, agressividade: 0.8, defende: 0.1, pausa: [0.2, 0.6], pesos: { soco1: 2, chama: 2 }, arremessa: 0.5 },
        },
    };

    // ── AS FASES ──────────────────────────────────────────────────────────────────────────
    // ondas[i].x é o x do jogador que dispara a onda. Os inimigos entram pelos dois lados da tela.
    const FASES = [
        { numero: 0, nome: 'Sala de Treino', cenario: 'patio', comprimento: 4000, ondas: [], objetos: [] },
        {
            numero: 1, nome: 'Pátio do Templo', cenario: 'patio', comprimento: 3600,
            ondas: [
                { x: 500, inimigos: [['sombra', 2]] },
                { x: 1150, inimigos: [['sombra', 3]] },
                { x: 1850, inimigos: [['sombra', 2], ['garra', 1]] },
                { x: 2650, inimigos: [['sombra', 2]], chefe: 'mestreSombra' },
            ],
            objetos: [{ tipo: 'vaso', x: 820, y: 0.25, item: 'cha' }, { tipo: 'vaso', x: 1500, y: 0.85, item: 'pergaminho' }, { tipo: 'vaso', x: 2300, y: 0.4, item: 'cha' }],
        },
        {
            numero: 2, nome: 'Floresta Viva', cenario: 'floresta', comprimento: 4000,
            ondas: [
                { x: 450, inimigos: [['garra', 2]] },
                { x: 1100, inimigos: [['sombra', 2], ['arqueiro', 1]] },
                { x: 1800, inimigos: [['garra', 2], ['arqueiro', 1]] },
                { x: 2500, inimigos: [['bruto', 1], ['sombra', 2]] },
                { x: 3200, inimigos: [['garra', 1]], chefe: 'graoPresa' },
            ],
            objetos: [{ tipo: 'vaso', x: 800, y: 0.6, item: 'cha' }, { tipo: 'vaso', x: 2150, y: 0.2, item: 'pergaminho' }, { tipo: 'vaso', x: 2900, y: 0.9, item: 'cha' }],
        },
        {
            numero: 3, nome: 'Poço das Almas', cenario: 'poco', comprimento: 4200,
            ondas: [
                { x: 450, inimigos: [['sombra', 3], ['arqueiro', 1]] },
                { x: 1150, inimigos: [['bruto', 1], ['garra', 1]] },
                { x: 1900, inimigos: [['garra', 2], ['arqueiro', 2]] },
                { x: 2650, inimigos: [['bruto', 2]] },
                { x: 3400, inimigos: [['sombra', 2]], chefe: 'gigante' },
            ],
            objetos: [{ tipo: 'vaso', x: 800, y: 0.3, item: 'cha' }, { tipo: 'vaso', x: 1600, y: 0.75, item: 'cha' }, { tipo: 'vaso', x: 2300, y: 0.5, item: 'pergaminho' }, { tipo: 'vaso', x: 3100, y: 0.2, item: 'cha' }],
        },
        {
            numero: 4, nome: 'Torre do Feiticeiro', cenario: 'torre', comprimento: 3400,
            ondas: [
                { x: 450, inimigos: [['garra', 2], ['sombra', 2]] },
                { x: 1150, inimigos: [['bruto', 1], ['arqueiro', 2]] },
                { x: 1850, inimigos: [['garra', 2], ['bruto', 1]] },
                { x: 2500, inimigos: [], chefe: 'feiticeiro' },
            ],
            objetos: [{ tipo: 'vaso', x: 800, y: 0.5, item: 'cha' }, { tipo: 'vaso', x: 1500, y: 0.2, item: 'pergaminho' }, { tipo: 'vaso', x: 2200, y: 0.8, item: 'cha' }],
        },
    ];

    // ── ENTRADA ───────────────────────────────────────────────────────────────────────────
    const BOTOES = ['esquerda', 'direita', 'cima', 'baixo', 'soco', 'chute', 'especial', 'pular', 'agarrar', 'defender'];
    function entradaVazia() {
        const e = { apertou: {} };
        for (const b of BOTOES) { e[b] = false; e.apertou[b] = false; }
        return e;
    }

    // ── ENTIDADES ─────────────────────────────────────────────────────────────────────────
    function criarEntidade(mundo, time, def, x, y) {
        return {
            id: ++mundo.proximoId, time, def, x, y, z: 0, vx: 0, vy: 0, vz: 0,
            vida: def.vida, vidaMax: def.vida, virado: time === 'jogador' ? 1 : -1,
            estado: 'parado', quadro: 0, golpe: null, golpeNome: null, atingidos: null, acertou: false, proximoTick: 0,
            invulneravel: 0, atordoadoAte: 0, agarradoPor: null, agarrando: null, correndo: false,
            golpesLevados: 0, escala: def.escala || 1,
        };
    }

    function criarJogador(mundo, personagem, x, y) {
        const def = PERSONAGENS[personagem];
        if (!def) throw new Error(`personagem desconhecido: ${personagem}`);
        const j = criarEntidade(mundo, 'jogador', def, x, y);
        j.personagem = personagem;
        j.chi = 0; j.combo = 0; j.comboTempo = 0; j.vidas = 3; j.danoLevado = 0;
        j.ultimoToque = { direcao: 0, tempo: -9 };
        j.indice = mundo.jogadores.length;
        // Melhorias do Templo: TODO jogador do mundo, inclusive o P2 que entra no meio (adicionarJogador passa por aqui).
        j.melhorias = {};
        for (const id of mundo.liberados) j.melhorias[id] = true;
        if (j.melhorias.vigor) j.vidaMax = j.vida = Math.round(def.vida * VIGOR);
        return j;
    }
    function tem(ent, melhoria) { return !!(ent.melhorias && ent.melhorias[melhoria]); }

    function criarInimigo(mundo, tipo, x, y) {
        const def = INIMIGOS[tipo];
        if (!def) throw new Error(`inimigo desconhecido: ${tipo}`);
        const i = criarEntidade(mundo, 'inimigo', def, x, y);
        i.tipo = tipo;
        // A vida sobe com a fase — a Sombra da torre não é a Sombra do pátio.
        const reforco = (1 + 0.15 * Math.max(0, (mundo.faseDef.numero || 1) - 1)) * mundo.dificuldade.vida;
        i.vidaMax = i.vida = Math.round(def.vida * reforco);
        i.ia = { congelada: false, pausa: mundo.rng.entre(0.2, 0.5), lado: mundo.rng.chance(0.5) ? 1 : -1, defendendoAte: 0, invocou: [] };
        return i;
    }

    function colocarInimigo(mundo, tipo, x, y) {
        const i = criarInimigo(mundo, tipo, x, y);
        mundo.inimigos.push(i);
        return i;
    }

    function adicionarJogador(mundo, personagem) {
        const base = mundo.travado ? mundo.travaX : mundo.camera.x;
        const j = criarJogador(mundo, personagem, base + LARGURA * 0.3, 0.55);
        j.invulneravel = 2;
        mundo.jogadores.push(j);
        mundo.eventos.push({ tipo: 'renasceu', x: j.x, y: j.y });
        return j;
    }

    // ── O MUNDO ───────────────────────────────────────────────────────────────────────────
    function criarMundo(opcoes) {
        const o = opcoes || {};
        const faseDef = FASES[o.fase == null ? 1 : o.fase];
        if (!faseDef) throw new Error(`fase desconhecida: ${o.fase}`);
        const mundo = {
            fase: faseDef.numero, faseDef, tempo: 0, proximoId: 0, rng: criarRng(o.semente == null ? 1 : o.semente),
            dificuldade: DIFICULDADES[o.dificuldade] || DIFICULDADES.normal, nomeDaDificuldade: DIFICULDADES[o.dificuldade] ? o.dificuldade : 'normal',
            camera: { x: 0 }, travado: false, travaX: 0, onda: 0, concluida: false, fimDeJogo: false, concluidaHa: 0,
            jogadores: [], inimigos: [], projeteis: [], itens: [], objetos: [], eventos: [], avisos: [],
            pontuacao: o.pontuacao || 0, chefe: null,
            liberados: (Array.isArray(o.liberados) ? o.liberados : []).filter((id, k, lista) => MELHORIAS.includes(id) && lista.indexOf(id) === k),
        };
        for (const p of (o.jogadores || ['long'])) {
            const j = criarJogador(mundo, p, 120 + mundo.jogadores.length * 60, 0.5 + mundo.jogadores.length * 0.15);
            mundo.jogadores.push(j);
        }
        for (const ob of faseDef.objetos) mundo.objetos.push({ id: ++mundo.proximoId, time: 'objeto', tipo: ob.tipo, x: ob.x, y: ob.y, z: 0, item: ob.item, vida: 1, estado: 'parado', escala: 1 });
        return mundo;
    }

    // ── UTILIDADES DE ESTADO ──────────────────────────────────────────────────────────────
    function mudar(ent, estado) {
        ent.estado = estado;
        ent.quadro = 0;
        if (estado !== 'atacando') { ent.golpe = null; ent.golpeNome = null; ent.atingidos = null; }
    }
    function podeAgir(ent) {
        return ent.estado === 'parado' || ent.estado === 'andando';
    }
    function noChao(ent) { return ent.z <= 0 && ent.vz <= 0; }
    function vivo(ent) { return ent.estado !== 'morto' && ent.estado !== 'finalizado'; }
    function alvoValido(ent) {
        // Quem está no chão caído, morto ou sendo finalizado não pode ser acertado de novo.
        return vivo(ent) && ent.estado !== 'caido' && ent.estado !== 'levantando' && ent.invulneravel <= 0;
    }

    function iniciarGolpe(ent, nome) {
        const golpe = ent.def.golpes[nome];
        if (!golpe) throw new Error(`${ent.def.nome} não tem o golpe ${nome}`);
        mudar(ent, 'atacando');
        ent.golpe = golpe; ent.golpeNome = nome; ent.atingidos = []; ent.acertou = false; ent.proximoTick = golpe.inicio;
        ent.correndo = false;
        if (golpe.mergulho) { ent.vx = ent.virado * golpe.mergulho[0]; ent.vz = golpe.mergulho[1]; }
        return golpe;
    }

    function atordoar(ent, segundos) {
        mudar(ent, 'atordoado');
        ent.atordoadoAte = segundos;
        ent.vx = 0;
    }

    // FINALIZAÇÃO: a régua é a VIDA, não o estado. Levantar com pouca vida atordoa ("FINALIZE!"),
    // mas o Contra-golpe também atordoa — e o atordoado dele, com vida cheia, NÃO pode virar
    // finalização grátis. Por isso agarrar um atordoado só finaliza se a vida está abaixo da
    // mesma linha que produz o "FINALIZE!"; acima dela, é o agarrão comum.
    function limiteDeFinalizacao(ent) { return ent.def.chefe ? 0.1 : 0.22; }
    function finalizavel(ent) { return ent.time === 'inimigo' && ent.vida <= ent.vidaMax * limiteDeFinalizacao(ent); }

    function soltarAgarrado(ent) {
        if (ent.agarrando) {
            const preso = ent.agarrando;
            preso.agarradoPor = null;
            if (preso.estado === 'agarrado') mudar(preso, 'parado');
            ent.agarrando = null;
        }
    }

    // ── DANO ──────────────────────────────────────────────────────────────────────────────
    // golpe: { dano, origem, recuo, lanca, derruba, direcao, congela, leve }
    function aplicarDano(mundo, alvo, golpe) {
        if (!vivo(alvo)) return false;
        const origem = golpe.origem || null;
        const direcao = golpe.direcao != null ? golpe.direcao : (origem ? Math.sign(alvo.x - origem.x) || origem.virado : 1);
        const doInimigo = origem ? origem.time === 'inimigo' : golpe.time === 'inimigo';
        let dano = Math.max(0, Math.round(golpe.dano * (doInimigo && alvo.time === 'jogador' ? mundo.dificuldade.dano : 1)));
        if (origem && origem.time === 'jogador' && tem(origem, 'punhos_de_ferro')) dano = Math.round(dano * PUNHOS_DE_FERRO);

        // DEFESA: só segura golpe que vem pela frente. Quem está defendendo de costas apanha inteiro.
        const defendendo = alvo.estado === 'defendendo' && (origem == null || Math.sign(origem.x - alvo.x) === alvo.virado || origem.x === alvo.x);
        if (defendendo && !golpe.ignoraDefesa && golpe.corpoACorpo && tem(alvo, 'contra_golpe') && naJanelaDeAparar(alvo, origem)) {
            aparar(mundo, alvo, origem);
            return true;
        }
        if (defendendo && !golpe.ignoraDefesa) {
            dano = Math.max(1, Math.round(dano * 0.2));
            alvo.vida = Math.max(0, alvo.vida - dano);
            if (alvo.time === 'jogador') alvo.danoLevado += dano;
            alvo.vx = direcao * 120;
            mundo.eventos.push({ tipo: 'acerto', x: alvo.x, y: alvo.y, z: alvo.z + 40 * alvo.escala, forca: 'bloqueio', bloqueado: true });
            mundo.eventos.push({ tipo: 'som', nome: 'bloqueio' });
            if (alvo.vida === 0) morrer(mundo, alvo, origem, direcao);
            return true;
        }

        const vidaAntes = alvo.vida;
        alvo.vida = Math.max(0, alvo.vida - dano);
        const tirado = vidaAntes - alvo.vida;        // o dano de VERDADE: nunca mais que a vida que havia
        alvo.golpesLevados++;
        if (alvo.time === 'jogador') alvo.danoLevado += dano;
        if (alvo.agarrando) soltarAgarrado(alvo);
        if (alvo.agarradoPor) { alvo.agarradoPor.agarrando = null; alvo.agarradoPor = null; }

        const forte = !!(golpe.lanca || golpe.derruba || dano >= 12);
        mundo.eventos.push({ tipo: 'acerto', x: alvo.x, y: alvo.y, z: alvo.z + 45 * alvo.escala, forca: forte ? 'forte' : 'leve', direcao });
        mundo.eventos.push({ tipo: 'som', nome: forte ? 'acerto-forte' : 'acerto' });
        if (golpe.congela) mundo.eventos.push({ tipo: 'congelar', segundos: golpe.congela });
        if (golpe.tremor) mundo.eventos.push({ tipo: 'tremor', forca: golpe.tremor });

        if (origem && origem.time === 'jogador') {
            origem.combo++;
            origem.comboTempo = 1.6;
            const multiplicador = 1 + Math.floor(origem.combo / 5) * 0.5;
            // Pontos pelo dano TIRADO, não pelo bruto: um golpe de 9999 numa Sombra de 30 valia 99.990
            // pontos — e o karma do Templo sai dos pontos.
            mundo.pontuacao += Math.round(tirado * 10 * multiplicador);
            // O chi é dos PUNHOS: projétil e arremesso não enchem, senão o especial se paga sozinho.
            if (!golpe.semChi) origem.chi = Math.min(100, origem.chi + 4);
            if (origem.combo > 0 && origem.combo % 5 === 0) mundo.eventos.push({ tipo: 'texto', texto: `COMBO ×${origem.combo}`, x: origem.x, y: origem.y, cor: 'combo' });
        }

        if (alvo.vida === 0) { morrer(mundo, alvo, origem, direcao); return true; }

        const noAr = alvo.z > 0 || alvo.vz > 0;
        if (golpe.lanca) {
            mudar(alvo, 'lancado');
            alvo.vz = golpe.lanca; alvo.vx = direcao * (golpe.recuo || 100);
            alvo.tempoNoAr = 0;
        } else if (noAr) {
            // MALABARISMO: quem está no ar quica de novo — é o que permite emendar golpe no ar.
            mudar(alvo, 'lancado');
            alvo.vz = Math.max(alvo.vz, 300); alvo.vx = direcao * (golpe.recuo || 100) * 0.7;
        } else if (golpe.derruba) {
            mudar(alvo, 'lancado');
            alvo.vz = 280; alvo.vx = direcao * (golpe.recuo || 200);
        } else if (alvo.def.armadura && alvo.estado === 'atacando') {
            // ARMADURA: o golpe leve machuca mas não interrompe — o Bruto termina o que começou.
            alvo.vx = direcao * 40;
        } else {
            mudar(alvo, 'atingido');
            alvo.vx = direcao * (golpe.recuo || 100);
            alvo.atingidoAte = 0.3;
        }
        return true;
    }

    function morrer(mundo, alvo, origem, direcao) {
        mudar(alvo, 'morto');
        alvo.vz = 380; alvo.vx = (direcao || 1) * 260; alvo.morteHa = 0;
        alvo.invulneravel = 99;
        if (alvo.agarrando) soltarAgarrado(alvo);
        mundo.eventos.push({ tipo: 'morte', x: alvo.x, y: alvo.y, z: alvo.z, time: alvo.time, nome: alvo.def.nome, id: alvo.def.id });
        mundo.eventos.push({ tipo: 'som', nome: alvo.time === 'jogador' ? 'morte-jogador' : 'morte' });
        if (alvo.time === 'inimigo') {
            if (origem && origem.time === 'jogador') mundo.pontuacao += alvo.def.pontos;
            if (alvo.def.chefe) { mundo.eventos.push({ tipo: 'texto', texto: 'CHEFE DERROTADO', x: alvo.x, y: alvo.y, cor: 'chefe' }); mundo.eventos.push({ tipo: 'tremor', forca: 1.5 }); }
            else if (mundo.rng.chance(0.14)) soltarItem(mundo, alvo.x, alvo.y, mundo.jogadores.some(j => j.vida < j.vidaMax * 0.6) ? 'cha' : 'pergaminho');
        } else {
            alvo.vidas = Math.max(0, alvo.vidas - 1);
            soltarAgarrado(alvo);
        }
    }

    // CONTRA-GOLPE (melhoria): começar a defender no máximo JANELA_DE_APARAR antes de o golpe ligar.
    // `alvo.quadro` = há quanto tempo ele defende; `origem.quadro - inicio` = há quanto tempo o golpe
    // ligou. Defender ANTES da janela (segurando) ou DEPOIS de ligar (a investida que chega no meio
    // do ativo) é o bloqueio de sempre. Só golpe corpo a corpo: aparar flecha não atordoa o arqueiro.
    function naJanelaDeAparar(alvo, origem) {
        if (!origem || origem.estado !== 'atacando' || !origem.golpe) return false;
        const desdeQueLigou = Math.max(0, origem.quadro - origem.golpe.inicio);
        return alvo.quadro >= desdeQueLigou - EPSILON && alvo.quadro <= desdeQueLigou + JANELA_DE_APARAR + EPSILON;
    }
    function aparar(mundo, alvo, origem) {
        atordoar(origem, ATORDOADO_DO_CONTRA);
        alvo.chi = Math.min(100, alvo.chi + CHI_DO_CONTRA);
        mundo.eventos.push({ tipo: 'acerto', x: alvo.x, y: alvo.y, z: alvo.z + 40 * alvo.escala, forca: 'bloqueio', bloqueado: true });
        mundo.eventos.push({ tipo: 'som', nome: 'bloqueio' });
        mundo.eventos.push({ tipo: 'congelar', segundos: 0.08 });
        mundo.eventos.push({ tipo: 'texto', texto: 'CONTRA!', x: alvo.x, y: alvo.y, cor: 'especial' });
    }

    function soltarItem(mundo, x, y, tipo) {
        mundo.itens.push({ id: ++mundo.proximoId, tipo, x, y: Math.min(1, Math.max(0, y)), z: 10, vz: 260 });
    }

    // ── CAIXAS DE ACERTO ──────────────────────────────────────────────────────────────────
    function dentroDoGolpe(ent, golpe, alvo) {
        if (Math.abs(alvo.y - ent.y) > TOLERANCIA_Y) return false;
        const meia = MEIA_LARGURA * (alvo.escala || 1);
        const perto = alvo.x - meia, longe = alvo.x + meia;
        const alcance = golpe.alcance + (ent.escala - 1) * 20;
        let xInicio, xFim;
        if (golpe.dosDoisLados) { xInicio = ent.x - alcance; xFim = ent.x + alcance; }
        else if (ent.virado === 1) { xInicio = ent.x - 4; xFim = ent.x + alcance; }
        else { xInicio = ent.x - alcance; xFim = ent.x + 4; }
        if (longe < xInicio || perto > xFim) return false;
        const zBaixo = ent.z - 30, zAlto = ent.z + golpe.altura * ent.escala;
        const alvoBaixo = alvo.z, alvoAlto = alvo.z + ALTURA_CORPO * (alvo.escala || 1);
        return alvoAlto >= zBaixo && alvoBaixo <= zAlto;
    }

    function alvosDe(mundo, ent) {
        if (ent.time === 'jogador') return mundo.inimigos.concat(mundo.objetos);
        return mundo.jogadores;
    }

    function resolverGolpe(mundo, ent, dt) {
        const golpe = ent.golpe;
        const q = ent.quadro;
        if (golpe.tipo === 'projetil') {
            if (q >= golpe.inicio && !ent.acertou) {
                ent.acertou = true;
                lancarProjetil(mundo, ent, golpe);
            }
            return;
        }
        const fimAtivo = golpe.inicio + golpe.ativo;
        if (q < golpe.inicio || q > fimAtivo) return;
        // Golpe que repete (o giro do Shen) reabre a lista de atingidos a cada tique.
        if (golpe.repete && q >= ent.proximoTick) { ent.atingidos = []; ent.proximoTick += golpe.repete; }
        const ultimoTique = golpe.repete ? (ent.proximoTick - golpe.repete + golpe.repete >= fimAtivo - 0.01) : true;
        for (const alvo of alvosDe(mundo, ent)) {
            if (ent.atingidos.includes(alvo.id)) continue;
            if (alvo.time === 'objeto') {
                if (alvo.estado !== 'parado' || Math.abs(alvo.y - ent.y) > TOLERANCIA_Y) continue;
                if (!dentroDoGolpe(ent, golpe, alvo)) continue;
                ent.atingidos.push(alvo.id);
                quebrarObjeto(mundo, alvo);
                continue;
            }
            if (!alvoValido(alvo) || alvo.agarradoPor === ent) continue;
            if (!dentroDoGolpe(ent, golpe, alvo)) continue;
            ent.atingidos.push(alvo.id);
            ent.acertou = true;
            const derruba = golpe.derruba || (golpe.derrubaNoFim && ultimoTique);
            aplicarDano(mundo, alvo, { dano: golpe.dano, origem: ent, recuo: golpe.recuo, lanca: golpe.lanca, derruba, congela: golpe.congela, tremor: golpe.tremor, corpoACorpo: true });
            if (golpe.texto && ent.time === 'jogador' && vivo(alvo)) mundo.eventos.push({ tipo: 'texto', texto: golpe.texto, x: alvo.x, y: alvo.y, cor: 'golpe' });
            // Aparado no meio da volta: o golpe acabou (e `atingidos` foi zerado) — não acerta mais ninguém.
            if (ent.estado !== 'atacando') return;
        }
    }

    function quebrarObjeto(mundo, objeto) {
        objeto.estado = 'quebrado';
        objeto.vida = 0;
        mundo.eventos.push({ tipo: 'quebra', x: objeto.x, y: objeto.y });
        mundo.eventos.push({ tipo: 'som', nome: 'quebra' });
        if (objeto.item) soltarItem(mundo, objeto.x, objeto.y, objeto.item);
    }

    function lancarProjetil(mundo, ent, golpe) {
        mundo.projeteis.push({
            id: ++mundo.proximoId, tipo: golpe.projetil, time: ent.time, dono: ent,
            x: ent.x + ent.virado * 30, y: ent.y, z: ent.z + 40 * ent.escala, vx: ent.virado * golpe.velocidade, vida: 2.5,
            dano: golpe.dano, recuo: golpe.recuo, derruba: !!golpe.derruba, virado: ent.virado,
        });
        mundo.eventos.push({ tipo: 'projetil', nome: golpe.projetil, x: ent.x, y: ent.y });
        mundo.eventos.push({ tipo: 'som', nome: golpe.som || 'fogo' });
    }

    function moverProjeteis(mundo, dt) {
        const esquerda = mundo.camera.x - 300, direita = mundo.camera.x + LARGURA + 300;
        for (let k = mundo.projeteis.length - 1; k >= 0; k--) {
            const p = mundo.projeteis[k];
            p.x += p.vx * dt; p.vida -= dt;
            let sumiu = p.vida <= 0 || p.x < esquerda || p.x > direita;
            if (!sumiu) {
                const alvos = p.time === 'jogador' ? mundo.inimigos : mundo.jogadores;
                for (const alvo of alvos) {
                    if (!alvoValido(alvo) || Math.abs(alvo.y - p.y) > TOLERANCIA_Y) continue;
                    if (Math.abs(alvo.x - p.x) > MEIA_LARGURA * alvo.escala + 14) continue;
                    if (p.z < alvo.z - 10 || p.z > alvo.z + ALTURA_CORPO * alvo.escala + 10) continue;
                    aplicarDano(mundo, alvo, { dano: p.dano, origem: p.dono, direcao: p.virado, recuo: p.recuo, derruba: p.derruba, semChi: true, congela: 0.04 });
                    sumiu = true;
                    break;
                }
            }
            if (sumiu) mundo.projeteis.splice(k, 1);
        }
    }

    // ── JOGADOR ───────────────────────────────────────────────────────────────────────────
    function inimigoNaFrente(mundo, j, distancia) {
        let melhor = null, melhorDx = Infinity;
        for (const i of mundo.inimigos) {
            if (!vivo(i) || i.agarradoPor) continue;
            const dx = (i.x - j.x) * j.virado;
            if (dx < -6 || dx > distancia || Math.abs(i.y - j.y) > TOLERANCIA_Y) continue;
            if (dx < melhorDx) { melhor = i; melhorDx = dx; }
        }
        return melhor;
    }

    function controlarJogador(mundo, j, e, dt) {
        const def = j.def;
        if (j.estado === 'morto') return;
        // Correr: dois toques na mesma direção em menos de 0,25 s.
        for (const [botao, direcao] of [['esquerda', -1], ['direita', 1]]) {
            if (e.apertou[botao]) {
                if (j.ultimoToque.direcao === direcao && mundo.tempo - j.ultimoToque.tempo < 0.25 && podeAgir(j)) j.correndo = true;
                j.ultimoToque = { direcao, tempo: mundo.tempo };
            }
        }
        const dx = (e.direita ? 1 : 0) - (e.esquerda ? 1 : 0);
        const dy = (e.baixo ? 1 : 0) - (e.cima ? 1 : 0);
        // Joystick de toque e analógico do controle não têm "dois toques": empurrar até o fim corre.
        if (e.correr && dx !== 0 && podeAgir(j)) j.correndo = true;
        if (dx === 0) j.correndo = false;

        // Agarrando alguém: soco arremessa, chute é joelhada, agarrar de novo arremessa.
        if (j.estado === 'agarrando') {
            const preso = j.agarrando;
            if (!preso || preso.estado !== 'agarrado') { j.agarrando = null; mudar(j, 'parado'); return; }
            preso.x = j.x + j.virado * 34 * ((preso.escala + j.escala) / 2); preso.y = j.y; preso.virado = -j.virado;
            if (e.apertou.chute) {
                aplicarDano(mundo, preso, { dano: def.golpes.joelhada.dano, origem: j, recuo: 0, direcao: j.virado, congela: 0.03, semDerrubar: true });
                mundo.eventos.push({ tipo: 'som', nome: 'soco' });
                if (preso.estado === 'atingido' || preso.estado === 'lancado') { mudar(preso, 'agarrado'); preso.vx = 0; preso.vz = 0; preso.z = 0; }
                j.quadro = Math.max(0, j.quadro - 0.4);
            } else if (e.apertou.soco || e.apertou.agarrar) {
                arremessar(mundo, j, preso, dy);
            } else if (j.quadro > 2.2) {
                // O preso se solta sozinho se ninguém faz nada — não existe agarrão eterno.
                soltarAgarrado(j); mudar(j, 'parado');
            }
            return;
        }

        const noAr = !noChao(j) || j.estado === 'pulando';
        if (noAr) {
            if (j.estado === 'pulando' && (e.apertou.chute || e.apertou.soco)) iniciarGolpe(j, 'chuteAereo');
            else if (j.estado === 'pulando' && e.apertou.especial && tem(j, 'especial_aereo')) {
                // Especial no Ar: custa o chi do especial de chão. Sem a melhoria, especial no pulo não faz nada.
                const custo = def.golpes.especial.chi;
                if (j.chi >= custo) {
                    j.chi -= custo;
                    iniciarGolpe(j, 'especialAereo');
                    mundo.eventos.push({ tipo: 'texto', texto: def.golpes.especialAereo.nome.toUpperCase(), x: j.x, y: j.y, cor: 'especial' });
                } else mundo.eventos.push({ tipo: 'som', nome: 'negado' });
            }
            return;
        }

        // Emendar a sequência: durante a recuperação de um golpe que ACERTOU, o próximo entra na hora.
        if (j.estado === 'atacando') {
            const g = j.golpe;
            if (e.apertou.soco && g.proximo && j.acertou && j.quadro >= g.inicio + g.ativo) { iniciarGolpe(j, g.proximoCinco && tem(j, 'sequencia_cinco') ? g.proximoCinco : g.proximo); return; }
            if (e.apertou.especial && j.acertou && j.quadro >= g.inicio + g.ativo && g.tipo !== 'projetil' && g.tipo !== 'giro' && j.chi >= def.golpes.especial.chi) { j.chi -= def.golpes.especial.chi; iniciarGolpe(j, 'especial'); mundo.eventos.push({ tipo: 'texto', texto: def.golpes.especial.nome.toUpperCase(), x: j.x, y: j.y, cor: 'especial' }); return; }
            return;
        }

        if (j.estado === 'defendendo') {
            if (!e.defender) mudar(j, 'parado');
            else { j.vx = 0; if (dx !== 0) j.virado = dx; }
            return;
        }
        if (!podeAgir(j)) return;

        if (e.defender) { mudar(j, 'defendendo'); j.correndo = false; return; }
        if (e.apertou.pular) { mudar(j, 'pulando'); j.vz = PULO; j.vx = dx * (j.correndo ? def.corrida : def.velocidade); mundo.eventos.push({ tipo: 'som', nome: 'pulo' }); return; }
        if (e.apertou.agarrar) {
            const alvo = inimigoNaFrente(mundo, j, 54 + (j.escala - 1) * 20);
            if (alvo && alvo.estado === 'atordoado' && noChao(alvo) && finalizavel(alvo)) { finalizar(mundo, j, alvo); return; }
            const pegavel = alvo && noChao(alvo) && !alvo.def.chefe
                && (podeAgir(alvo) || alvo.estado === 'atacando' || alvo.estado === 'atingido' || alvo.estado === 'atordoado' || alvo.estado === 'defendendo');
            // Agarrão pelas Costas (melhoria): quem está virado PRA LONGE do jogador leva suplex na hora,
            // mesmo com armadura. Chefe nunca (`pegavel` já exclui).
            if (pegavel && tem(j, 'agarrao_costas') && alvo.virado === Math.sign(alvo.x - j.x)) { suplex(mundo, j, alvo); return; }
            const agarravel = pegavel && (!alvo.def.armadura || alvo.estado === 'atordoado');
            if (agarravel) {
                mudar(j, 'agarrando'); j.agarrando = alvo; alvo.agarradoPor = j; mudar(alvo, 'agarrado'); alvo.vx = 0; alvo.vz = 0; alvo.z = 0;
                mundo.eventos.push({ tipo: 'som', nome: 'agarrar' });
                return;
            }
            iniciarGolpe(j, 'soco1');           // agarrar no vazio vira soco: o botão nunca é "nada"
            return;
        }
        if (e.apertou.soco) { iniciarGolpe(j, j.correndo ? 'investida' : 'soco1'); mundo.eventos.push({ tipo: 'som', nome: 'whoosh' }); return; }
        if (e.apertou.chute) { iniciarGolpe(j, j.correndo ? 'investida' : 'chute'); mundo.eventos.push({ tipo: 'som', nome: 'whoosh' }); return; }
        if (e.apertou.especial) {
            if (j.chi >= def.golpes.especial.chi) {
                j.chi -= def.golpes.especial.chi;
                iniciarGolpe(j, 'especial');
                mundo.eventos.push({ tipo: 'texto', texto: def.golpes.especial.nome.toUpperCase(), x: j.x, y: j.y, cor: 'especial' });
            } else mundo.eventos.push({ tipo: 'som', nome: 'negado' });
            return;
        }

        // Andar / correr.
        if (dx !== 0 || dy !== 0) {
            const vel = j.correndo ? def.corrida : def.velocidade;
            const norma = dx !== 0 && dy !== 0 ? 0.78 : 1;
            j.vx = dx * vel * norma;
            j.vy = dy * (vel * 0.55 / (CHAO_BASE - CHAO_TOPO)) * norma;
            if (dx !== 0) j.virado = dx;
            if (j.estado !== 'andando') mudar(j, 'andando');
        } else {
            j.vx = 0; j.vy = 0;
            if (j.estado !== 'parado') mudar(j, 'parado');
        }
    }

    function arremessar(mundo, j, preso, dy) {
        mudar(j, 'arremessando');
        j.agarrando = null; preso.agarradoPor = null;
        mudar(preso, 'arremessado');
        preso.vx = j.virado * 720; preso.vz = 300; preso.z = 12;
        preso.vy = dy * 0.9;
        preso.arremessadoPor = j;
        preso.atropelados = [];
        mundo.eventos.push({ tipo: 'som', nome: 'arremesso' });
        mundo.eventos.push({ tipo: 'texto', texto: 'ARREMESSO!', x: j.x, y: j.y, cor: 'golpe' });
    }

    function suplex(mundo, j, alvo) {
        mudar(j, 'suplex');
        j.vx = 0; j.vy = 0;
        // Por cima da cabeça: o alvo cai ATRÁS do jogador. É arremesso, então não enche chi.
        aplicarDano(mundo, alvo, { dano: DANO_DO_SUPLEX, origem: j, direcao: -j.virado, recuo: 240, derruba: true, ignoraDefesa: true, semChi: true, congela: 0.07, tremor: 0.8 });
        mundo.eventos.push({ tipo: 'som', nome: 'arremesso' });
        mundo.eventos.push({ tipo: 'texto', texto: 'SUPLEX!', x: j.x, y: j.y, cor: 'golpe' });
    }

    function finalizar(mundo, j, alvo) {
        mudar(j, 'finalizando');
        j.invulneravel = 1.4;
        j.virado = Math.sign(alvo.x - j.x) || j.virado;
        alvo.x = j.x + j.virado * 40; alvo.y = j.y; alvo.virado = -j.virado;
        mudar(alvo, 'finalizado');
        alvo.invulneravel = 99;
        alvo.finalizadoPor = j;
        mundo.eventos.push({ tipo: 'finalizacao', x: alvo.x, y: alvo.y, jogador: j.indice, nome: alvo.def.nome });
        mundo.eventos.push({ tipo: 'som', nome: 'finalizacao' });
        mundo.eventos.push({ tipo: 'texto', texto: 'FINALIZAÇÃO', x: alvo.x, y: alvo.y, cor: 'finalizacao' });
    }

    // ── INIMIGO (IA) ──────────────────────────────────────────────────────────────────────
    function alvoMaisProximo(mundo, i) {
        let melhor = null, melhorD = Infinity;
        for (const j of mundo.jogadores) {
            if (!vivo(j)) continue;
            const d = Math.abs(j.x - i.x) + Math.abs(j.y - i.y) * 200;
            if (d < melhorD) { melhor = j; melhorD = d; }
        }
        return melhor;
    }

    function escolherGolpe(mundo, i, pesos) {
        const nomes = Object.keys(pesos);
        const total = nomes.reduce((s, n) => s + pesos[n], 0);
        let r = mundo.rng.proximo() * total;
        for (const n of nomes) { r -= pesos[n]; if (r <= 0) return n; }
        return nomes[nomes.length - 1];
    }

    function teleportar(mundo, i, alvo) {
        i.x = alvo.x - alvo.virado * 80 * i.escala;
        i.y = alvo.y;
        i.z = 0; i.vz = 0; i.vx = 0;
        i.golpesLevados = 0;
        mudar(i, 'parado');
        i.ia.pausa = 0.25;
        mundo.eventos.push({ tipo: 'teleporte', x: i.x, y: i.y });
        mundo.eventos.push({ tipo: 'som', nome: 'teleporte' });
    }

    function controlarInimigo(mundo, i, dt) {
        const ia = i.ia, def = i.def;
        if (ia.congelada || !vivo(i)) return;
        if (i.estado === 'defendendo') {
            ia.defendendoAte -= dt;
            if (ia.defendendoAte <= 0) mudar(i, 'parado');
            return;
        }
        const alvo = alvoMaisProximo(mundo, i);
        // Teleporte do chefe: depois de N golpes seguidos, some e reaparece atrás de quem bateu.
        if (def.teleporta && i.golpesLevados >= def.teleporta && alvo && noChao(i) && (podeAgir(i) || i.estado === 'atingido')) { teleportar(mundo, i, alvo); return; }
        // O Feiticeiro invoca ajuda a cada terço de vida perdido.
        if (def.invoca && alvo) {
            for (const marco of [0.66, 0.33]) {
                if (i.vida <= i.vidaMax * marco && !ia.invocou.includes(marco)) {
                    ia.invocou.push(marco);
                    for (const lado of [-1, 1]) {
                        const s = colocarInimigo(mundo, 'sombra', i.x + lado * 120, Math.min(1, Math.max(0, i.y + lado * 0.2)));
                        mundo.eventos.push({ tipo: 'teleporte', x: s.x, y: s.y });
                    }
                    mundo.eventos.push({ tipo: 'texto', texto: 'INVOCAÇÃO', x: i.x, y: i.y, cor: 'especial' });
                    mundo.eventos.push({ tipo: 'som', nome: 'teleporte' });
                    if (podeAgir(i) && alvo) { teleportar(mundo, i, alvo); i.x = alvo.x - alvo.virado * 260; }
                    return;
                }
            }
        }
        if (!podeAgir(i) || !alvo) { if (podeAgir(i)) { i.vx = 0; i.vy = 0; } return; }

        const dx = alvo.x - i.x, dy = alvo.y - i.y;
        const distancia = Math.abs(dx);
        i.virado = dx >= 0 ? 1 : -1;

        // Defender quando o alvo começa um golpe de perto.
        if (def.ia.defende && alvo.estado === 'atacando' && alvo.quadro < 0.05 && distancia < 110 && Math.abs(dy) < TOLERANCIA_Y && mundo.rng.chance(def.ia.defende)) {
            mudar(i, 'defendendo'); ia.defendendoAte = 0.55; i.vx = 0; i.vy = 0; return;
        }

        if (ia.pausa > 0) { ia.pausa -= dt; i.vx = 0; i.vy = 0; if (i.estado !== 'parado') mudar(i, 'parado'); return; }

        const alinhado = Math.abs(dy) < TOLERANCIA_Y * 0.6;
        const atacantes = mundo.inimigos.filter(o => o !== i && o.estado === 'atacando' && o.time === 'inimigo').length;
        const velV = def.velocidade * 0.55 / (CHAO_BASE - CHAO_TOPO);

        // Arqueiro: mantém distância e atira quando alinhado.
        if (def.ia.distancia) {
            const [minimo, maximo] = def.ia.distancia;
            let mx = 0, my = 0;
            if (distancia < minimo) mx = -Math.sign(dx); else if (distancia > maximo) mx = Math.sign(dx);
            if (!alinhado) my = Math.sign(dy);
            if (mx === 0 && my === 0) {
                i.vx = 0; i.vy = 0;
                if (mundo.rng.chance(def.ia.agressividade)) { iniciarGolpe(i, 'flecha'); return; }
                ia.pausa = mundo.rng.entre(def.ia.pausa[0], def.ia.pausa[1]);
                return;
            }
            if (distancia < 60 && alinhado && mundo.rng.chance(0.5)) { iniciarGolpe(i, 'soco1'); return; }
            i.vx = mx * def.velocidade; i.vy = my * velV;
            if (i.estado !== 'andando') mudar(i, 'andando');
            return;
        }

        // Chefe que arremessa de longe.
        if (def.ia.arremessa && distancia > 220 && alinhado && mundo.rng.chance(def.ia.arremessa * dt * 4)) {
            iniciarGolpe(i, def.golpes.shuriken ? 'shuriken' : 'caveira'); return;
        }
        // Investida de média distância.
        if (def.ia.investe && distancia > 140 && distancia < 360 && alinhado && atacantes < ATACANTES_MAX && mundo.rng.chance(def.ia.investe * dt * 3)) {
            iniciarGolpe(i, 'investida'); return;
        }

        const alcance = def.ia.alcance * i.escala;
        if (distancia <= alcance + 8 && alinhado) {
            i.vx = 0; i.vy = 0;
            if (atacantes < ATACANTES_MAX && mundo.rng.chance(def.ia.agressividade)) {
                iniciarGolpe(i, escolherGolpe(mundo, i, def.ia.pesos));
                return;
            }
            ia.pausa = mundo.rng.entre(def.ia.pausa[0], def.ia.pausa[1]);
            // De vez em quando troca de lado — não fica todo mundo empilhado do mesmo lado.
            if (mundo.rng.chance(0.3)) ia.lado = -ia.lado;
            return;
        }

        // Aproximar: cada inimigo mira um ponto ao lado do alvo (o "lado" dele), pra cercar.
        const destinoX = alvo.x + ia.lado * (alcance - 6);
        const ddx = destinoX - i.x;
        const mx = Math.abs(ddx) > 6 ? Math.sign(ddx) : 0;
        const my = alinhado ? 0 : Math.sign(dy);
        // Se o lado escolhido está longe demais (do outro lado do alvo), vem pelo lado mais perto.
        if (Math.abs(ddx) > 200 && distancia < 120) ia.lado = -ia.lado;
        i.vx = mx * def.velocidade * (my !== 0 ? 0.8 : 1);
        i.vy = my * velV;
        if (i.estado !== 'andando') mudar(i, 'andando');
    }

    // ── FÍSICA E TEMPO DE CADA ESTADO ─────────────────────────────────────────────────────
    function atualizarEntidade(mundo, ent, dt) {
        ent.quadro += dt;
        if (ent.invulneravel > 0 && ent.estado !== 'morto' && ent.estado !== 'finalizado') ent.invulneravel -= dt;
        if (ent.time === 'jogador' && ent.comboTempo > 0) { ent.comboTempo -= dt; if (ent.comboTempo <= 0) ent.combo = 0; }

        const est = ent.estado;
        if (ent.time === 'jogador' && tem(ent, 'respiracao') && (est === 'parado' || est === 'andando' || est === 'defendendo')) ent.chi = Math.min(100, ent.chi + RESPIRACAO * dt);
        // Golpe em andamento.
        if (est === 'atacando') {
            const g = ent.golpe;
            if (g.avanco && ent.quadro >= g.inicio && ent.quadro <= g.inicio + g.ativo) ent.vx = ent.virado * g.avanco;
            else if (!(ent.z > 0)) ent.vx *= Math.max(0, 1 - 12 * dt);
            resolverGolpe(mundo, ent, dt);
            if (ent.quadro >= g.total) mudar(ent, ent.z > 0 ? 'pulando' : 'parado');
        } else if (est === 'atingido') {
            ent.atingidoAte -= dt;
            if (ent.atingidoAte <= 0) mudar(ent, 'parado');
        } else if (est === 'caido') {
            if (ent.quadro >= 0.7) mudar(ent, 'levantando');
        } else if (est === 'levantando') {
            if (ent.quadro >= 0.45) {
                if (finalizavel(ent)) { atordoar(ent, 3); mundo.eventos.push({ tipo: 'texto', texto: 'FINALIZE!', x: ent.x, y: ent.y, cor: 'finalizacao' }); }
                else mudar(ent, 'parado');
            }
        } else if (est === 'atordoado') {
            ent.atordoadoAte -= dt;
            if (ent.atordoadoAte <= 0) mudar(ent, 'parado');
        } else if (est === 'arremessando') {
            if (ent.quadro >= 0.35) mudar(ent, 'parado');
        } else if (est === 'suplex') {
            ent.vx = 0;
            if (ent.quadro >= DURACAO_DO_SUPLEX) mudar(ent, 'parado');
        } else if (est === 'finalizando') {
            if (ent.quadro >= 1.4) mudar(ent, 'parado');
        } else if (est === 'finalizado') {
            if (ent.quadro >= 1.0 && ent.vida > 0) {
                ent.vida = 0;
                const j = ent.finalizadoPor;
                mundo.pontuacao += 500 + ent.def.pontos * 2;
                if (j) { j.chi = 100; }
                mundo.eventos.push({ tipo: 'morte', x: ent.x, y: ent.y, z: ent.z, time: 'inimigo', nome: ent.def.nome, id: ent.def.id, finalizado: true });
                mundo.eventos.push({ tipo: 'tremor', forca: 1.2 });
                mundo.eventos.push({ tipo: 'som', nome: 'gongo' });
                ent.estado = 'morto'; ent.morteHa = 0.9; ent.vz = 0; ent.vx = 0;
            }
        } else if (est === 'agarrado') {
            ent.vx = 0; ent.vy = 0;
            if (!ent.agarradoPor) mudar(ent, 'parado');
        } else if (est === 'arremessado') {
            for (const outro of mundo.inimigos) {
                if (outro === ent || !alvoValido(outro) || ent.atropelados.includes(outro.id)) continue;
                if (Math.abs(outro.y - ent.y) > TOLERANCIA_Y || Math.abs(outro.x - ent.x) > MEIA_LARGURA * (outro.escala + ent.escala)) continue;
                ent.atropelados.push(outro.id);
                aplicarDano(mundo, outro, { dano: 10, origem: ent.arremessadoPor, direcao: Math.sign(ent.vx) || 1, recuo: 300, derruba: true, semChi: true, congela: 0.03 });
                mundo.eventos.push({ tipo: 'atropelou', quantidade: ent.atropelados.length, x: outro.x, y: outro.y });
            }
        } else if (est === 'morto') {
            ent.morteHa += dt;
        }

        // Gravidade e chão.
        const estavaNoAr = ent.z > 0;
        if (ent.z > 0 || ent.vz > 0) {
            ent.vz -= GRAVIDADE * dt;
            ent.z += ent.vz * dt;
            if (ent.z <= 0) {
                ent.z = 0; ent.vz = 0;
                if (ent.estado === 'pulando') { mudar(ent, 'parado'); mundo.eventos.push({ tipo: 'poeira', x: ent.x, y: ent.y }); }
                else if (ent.estado === 'lancado') { mudar(ent, 'caido'); ent.vx *= 0.3; mundo.eventos.push({ tipo: 'poeira', x: ent.x, y: ent.y }); mundo.eventos.push({ tipo: 'som', nome: 'queda' }); }
                else if (ent.estado === 'arremessado') {
                    ent.vx *= 0.2;
                    mundo.eventos.push({ tipo: 'poeira', x: ent.x, y: ent.y }); mundo.eventos.push({ tipo: 'tremor', forca: 0.6 });
                    const vivoAntes = vivo(ent);
                    aplicarDano(mundo, ent, { dano: 12, origem: ent.arremessadoPor, direcao: Math.sign(ent.vx) || 1, recuo: 0, semChi: true });
                    if (vivoAntes && vivo(ent)) { mudar(ent, 'caido'); ent.vz = 0; ent.z = 0; }
                } else if (ent.estado === 'morto') { ent.vx *= 0.2; mundo.eventos.push({ tipo: 'poeira', x: ent.x, y: ent.y }); }
                else if (ent.estado === 'atacando' && ent.golpeNome === 'chuteAereo') { /* termina o golpe no chão */ }
            }
        }
        // Atrito de quem foi empurrado.
        if (ent.z <= 0 && (est === 'atingido' || est === 'caido' || est === 'morto' || est === 'defendendo' || est === 'levantando' || est === 'atordoado' || est === 'arremessado')) {
            ent.vx *= Math.max(0, 1 - 9 * dt);
            if (Math.abs(ent.vx) < 2) ent.vx = 0;
            if (est !== 'arremessado') ent.vy = 0;
        }
        ent.x += ent.vx * dt;
        ent.y = Math.min(1, Math.max(0, ent.y + ent.vy * dt));
        if (estavaNoAr && ent.estado === 'pulando' && ent.z <= 0) mudar(ent, 'parado');

        // Limites do mundo e da tela travada.
        const limiteEsq = 20, limiteDir = mundo.faseDef.comprimento - 20;
        if (ent.time === 'jogador') {
            const esq = mundo.travado ? Math.max(limiteEsq, mundo.travaX + 24) : Math.max(limiteEsq, mundo.camera.x + 16);
            const dir = mundo.travado ? Math.min(limiteDir, mundo.travaX + LARGURA - 24) : limiteDir;
            ent.x = Math.min(dir, Math.max(esq, ent.x));
        } else if (ent.time === 'inimigo') {
            ent.x = Math.min(mundo.camera.x + LARGURA + 240, Math.max(mundo.camera.x - 240, ent.x));
        }
    }

    // ── ONDAS, CÂMERA E FIM DE FASE ───────────────────────────────────────────────────────
    function dispararOnda(mundo, onda) {
        mundo.travado = true;
        const media = mundo.jogadores.reduce((s, j) => s + j.x, 0) / mundo.jogadores.length;
        mundo.travaX = Math.min(mundo.faseDef.comprimento - LARGURA, Math.max(0, media - LARGURA / 2));
        let lado = mundo.rng.chance(0.5) ? 1 : -1;
        const lista = [];
        for (const [tipo, n] of onda.inimigos) for (let k = 0; k < n; k++) lista.push(tipo);
        if (mundo.jogadores.length > 1) lista.push(onda.inimigos.length ? onda.inimigos[0][0] : 'sombra');   // dois jogadores, um a mais
        for (const tipo of lista) {
            const x = lado === 1 ? mundo.travaX + LARGURA + 60 + mundo.rng.entre(0, 80) : mundo.travaX - 60 - mundo.rng.entre(0, 80);
            const i = colocarInimigo(mundo, tipo, x, mundo.rng.entre(0.1, 0.95));
            i.ia.lado = -lado;
            lado = -lado;
        }
        mundo.eventos.push({ tipo: 'onda', numero: mundo.onda + 1, total: mundo.faseDef.ondas.length });
        if (onda.chefe) {
            const chefe = colocarInimigo(mundo, onda.chefe, mundo.travaX + LARGURA + 120, 0.5);
            mundo.chefe = chefe;
            mundo.eventos.push({ tipo: 'chefe', nome: chefe.def.nome, id: chefe.def.id, x: chefe.x, y: chefe.y });
            mundo.eventos.push({ tipo: 'som', nome: 'chefe' });
            mundo.avisos.push('chefe');
        }
    }

    function atualizarOndas(mundo, dt) {
        const ondas = mundo.faseDef.ondas;
        if (!mundo.travado && mundo.onda < ondas.length) {
            const onda = ondas[mundo.onda];
            if (mundo.jogadores.some(j => vivo(j) && j.x >= onda.x)) dispararOnda(mundo, onda);
        }
        if (mundo.travado) {
            const restam = mundo.inimigos.some(i => vivo(i));
            if (!restam) {
                mundo.travado = false;
                mundo.onda++;
                mundo.chefe = null;
                mundo.avisos.push('siga');
                mundo.eventos.push({ tipo: 'siga' });
                mundo.eventos.push({ tipo: 'som', nome: 'siga' });
                if (mundo.onda >= ondas.length) mundo.eventos.push({ tipo: 'texto', texto: 'CAMINHO LIVRE', x: mundo.camera.x + LARGURA / 2, y: 0.5, cor: 'chefe', tela: true });
            }
        }
        if (!mundo.concluida && mundo.onda >= ondas.length && !mundo.travado && ondas.length > 0
            && mundo.jogadores.some(j => vivo(j) && j.x >= mundo.faseDef.comprimento - 80)) {
            mundo.concluida = true;
            mundo.avisos.push('fase-concluida');
            mundo.eventos.push({ tipo: 'fase-concluida', fase: mundo.fase });
            mundo.eventos.push({ tipo: 'som', nome: 'gongo' });
        }
    }

    function atualizarCamera(mundo, dt) {
        const vivos = mundo.jogadores.filter(vivo);
        const lista = vivos.length ? vivos : mundo.jogadores;
        const media = lista.reduce((s, j) => s + j.x, 0) / Math.max(1, lista.length);
        const maximo = Math.max(0, mundo.faseDef.comprimento - LARGURA);
        const alvo = mundo.travado ? mundo.travaX : Math.min(maximo, Math.max(0, media - LARGURA * 0.4));
        const k = 1 - Math.exp(-7 * dt);
        mundo.camera.x += (alvo - mundo.camera.x) * k;
        if (Math.abs(alvo - mundo.camera.x) < 0.5) mundo.camera.x = alvo;
    }

    function atualizarItens(mundo, dt) {
        for (let k = mundo.itens.length - 1; k >= 0; k--) {
            const it = mundo.itens[k];
            if (it.z > 0 || it.vz > 0) { it.vz -= GRAVIDADE * dt; it.z += it.vz * dt; if (it.z <= 0) { it.z = 0; it.vz = 0; } }
            for (const j of mundo.jogadores) {
                if (!vivo(j) || j.z > 30 || Math.abs(j.x - it.x) > 30 || Math.abs(j.y - it.y) > TOLERANCIA_Y) continue;
                if (it.tipo === 'cha') j.vida = Math.min(j.vidaMax, j.vida + 35);
                else if (it.tipo === 'pergaminho') j.chi = Math.min(100, j.chi + 60);
                mundo.pontuacao += 50;
                mundo.eventos.push({ tipo: 'item', nome: it.tipo, x: it.x, y: it.y, jogador: j.indice });
                mundo.eventos.push({ tipo: 'som', nome: 'item' });
                mundo.itens.splice(k, 1);
                break;
            }
        }
    }

    function limparMortos(mundo, dt) {
        for (let k = mundo.inimigos.length - 1; k >= 0; k--) {
            const i = mundo.inimigos[k];
            if (i.estado === 'morto' && i.morteHa >= 1.6 && i.z <= 0) mundo.inimigos.splice(k, 1);
        }
        for (const j of mundo.jogadores) {
            if (j.estado !== 'morto' || j.morteHa < 1.6 || j.z > 0) continue;
            if (j.vidas > 0) {
                const base = mundo.travado ? mundo.travaX : mundo.camera.x;
                j.x = Math.min(Math.max(j.x, base + 80), base + LARGURA - 80); j.y = 0.5; j.z = 0; j.vx = 0; j.vz = 0;
                j.vida = j.vidaMax; j.chi = Math.max(j.chi, 30); j.combo = 0;
                j.invulneravel = 2.5;
                mudar(j, 'parado');
                mundo.eventos.push({ tipo: 'renasceu', x: j.x, y: j.y, jogador: j.indice });
                mundo.eventos.push({ tipo: 'som', nome: 'gongo' });
            } else if (!mundo.fimDeJogo && mundo.jogadores.every(o => o.estado === 'morto' && o.vidas === 0)) {
                mundo.fimDeJogo = true;
                mundo.eventos.push({ tipo: 'fim-de-jogo' });
            }
        }
    }

    // ── O PASSO ───────────────────────────────────────────────────────────────────────────
    function passo(mundo, dt, entradas) {
        mundo.eventos = [];
        mundo.tempo += dt;
        if (mundo.concluida) mundo.concluidaHa += dt;
        for (let k = 0; k < mundo.jogadores.length; k++) {
            const j = mundo.jogadores[k];
            const e = (entradas && entradas[k]) || entradaVazia();
            controlarJogador(mundo, j, e, dt);
        }
        for (const i of mundo.inimigos) controlarInimigo(mundo, i, dt);
        for (const j of mundo.jogadores) atualizarEntidade(mundo, j, dt);
        // Cópia: a IA do Feiticeiro invoca no meio da volta, e a lista cresce.
        for (const i of mundo.inimigos.slice()) atualizarEntidade(mundo, i, dt);
        moverProjeteis(mundo, dt);
        atualizarItens(mundo, dt);
        limparMortos(mundo, dt);
        atualizarOndas(mundo, dt);
        atualizarCamera(mundo, dt);
        return mundo;
    }

    return {
        LARGURA, ALTURA, CHAO_TOPO, CHAO_BASE, TOLERANCIA_Y, MEIA_LARGURA, ALTURA_CORPO,
        PERSONAGENS, INIMIGOS, FASES, BOTOES, DIFICULDADES, MELHORIAS,
        criarRng, criarMundo, passo, entradaVazia, colocarInimigo, adicionarJogador, iniciarGolpe, aplicarDano, atordoar, vivo,
    };
});
