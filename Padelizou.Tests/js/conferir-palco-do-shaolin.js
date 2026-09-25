// O PALCO do "Punhos de Shaolin" (jogo/), conferido no Node sem navegador.
//
//     node Padelizou.Tests/js/conferir-palco-do-shaolin.js
//
// Fase 1 do `jogo/CRONOGRAMA.md`, a parte que dá vida longa ao jogo:
//   1. CENÁRIO QUE MATA: cada fase tem `perigos` (fogo, espinhos, a beira do poço). Inimigo comum que
//      cai numa zona arremessado, lançado ou derrubado morre na hora; o chefe é imune (é empurrado pra
//      fora, sem dano, inclusive em fúria); o jogador NUNCA morre de uma vez pelo cenário (dano fixo e
//      empurrado pra fora); a IA não entra andando numa zona; e dá pra atravessar a fase sem pisar nela.
//   2. PONTOS DE CONTROLE: `criarMundo({ ondaInicial })` recomeça da última onda disparada, com os
//      pontos de quando ela disparou, sem dobrar o karma da fase.
//   3. MODO ARENA: uma tela, ondas infinitas do `mundo.rng`, chefe a cada 5, nunca conclui, recorde
//      separado e karma pela metade.
//   4. As conquistas 'pelo_cenario' e 'arena_10', e o `normalizar` do progresso com os campos novos.
// E o que é do navegador: `cenario.js` e `desenho.js` num contexto `vm` com um `ctx` que anota, e o
// `principal.js` RODANDO num `vm` (seção 11): "tentar de novo" pelo ponto de controle, o item Arena do
// menu e o fim da Arena, com motor, progresso, loja e desenho reais — as regex da seção 10 são só sinal extra.
//
// Sem dependência: `require` de arquivos do repositório e mais nada.
const path = require('path');
const fs = require('fs');
const vm = require('vm');
const raiz = path.join(__dirname, '..', '..', 'jogo', 'js');
const Motor = require(path.join(raiz, 'motor.js'));
const Progresso = require(path.join(raiz, 'progresso.js'));
const Conquistas = require(path.join(raiz, 'conquistas.js'));
const Loja = require(path.join(raiz, 'loja.js'));

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}
// Um bloco que ESTOURA vira falha com a mensagem — não derruba os outros blocos e não some calado.
function bloco(nome, fn) {
    try { fn(); } catch (erro) { confere(`${nome} (o bloco rodou até o fim)`, false, `${erro && erro.stack ? erro.stack.split('\n').slice(0, 3).join(' | ') : erro}`); }
}

const DT = 1 / 60;
const TIPOS = ['fogo', 'espinhos', 'poco'];
const CHEFES = ['mestreSombra', 'graoPresa', 'gigante', 'feiticeiro'];
const CAMPANHA = [1, 2, 3, 4];
const PX_Y = Motor.CHAO_BASE - Motor.CHAO_TOPO;          // 1 de profundidade = 190 px na tela

// ── AJUDANTES ─────────────────────────────────────────────────────────────────────────────
function entrada(extra) { return Object.assign(Motor.entradaVazia(), extra || {}); }
function apertar(mundo, botao, extra) {
    const e = entrada(extra);
    e.apertou[botao] = true;
    e[botao] = true;
    const lista = mundo.jogadores.map(() => entrada());
    lista[0] = e;
    Motor.passo(mundo, DT, lista);
    return mundo.eventos.slice();
}
// Roda anotando TODOS os eventos (o motor zera `mundo.eventos` a cada passo).
function rodar(mundo, quadros, entradas, aCada) {
    const vistos = [];
    for (let q = 0; q < quadros; q++) {
        const e = typeof entradas === 'function' ? entradas(q) : entradas;
        Motor.passo(mundo, DT, e || mundo.jogadores.map(() => entrada()));
        vistos.push(...mundo.eventos);
        if (aCada) aCada(q);
    }
    return vistos;
}
function dentro(p, x, y) { return x >= p.x0 && x <= p.x1 && y >= p.y0 && y <= p.y1; }
function perigosDe(fase) { const def = fase === 'arena' ? Motor.ARENA : Motor.FASES[fase]; return (def && def.perigos) || []; }
function naZona(faseDef, x, y) { return (faseDef.perigos || []).find(p => dentro(p, x, y)) || null; }
// A primeira zona do tipo pedido, na campanha: { fase, zona }.
function zonaDo(tipo) {
    for (const f of CAMPANHA) for (const p of perigosDe(f)) if (p.tipo === tipo) return { fase: f, zona: p };
    return null;
}
// Um mundo da fase `fase` sem onda nenhuma pra disparar, com o jogador parado em (x, y) e a câmera nele.
function palco(fase, x, y, personagens) {
    const m = Motor.criarMundo({ fase, jogadores: personagens || ['long'], semente: 3 });
    m.onda = m.faseDef.ondas.length;
    const j = m.jogadores[0];
    j.x = x; j.y = y;
    m.camera.x = Math.max(0, Math.min(m.faseDef.comprimento - Motor.LARGURA, x - Motor.LARGURA * 0.4));
    return m;
}
const centro = p => ({ x: (p.x0 + p.x1) / 2, y: (p.y0 + p.y1) / 2 });

// ── 1. OS PERIGOS NA DEFINIÇÃO ────────────────────────────────────────────────────────────
bloco('perigos na definição', () => {
    const esperado = { 1: ['fogo'], 2: ['espinhos'], 3: ['poco'], 4: ['espinhos', 'fogo'] };
    for (const f of CAMPANHA) {
        const lista = perigosDe(f);
        const tipos = new Set(lista.map(p => p.tipo));
        const formaOk = lista.every(p => TIPOS.includes(p.tipo) && p.x0 < p.x1 && p.y0 >= 0 && p.y1 <= 1 && p.y0 < p.y1 && p.x0 >= 0 && p.x1 <= Motor.FASES[f].comprimento);
        confere(`fase ${f} (${Motor.FASES[f].nome}): uma ou duas zonas, com tipo e retângulo válidos`, lista.length >= 1 && lista.length <= 2 && formaOk, JSON.stringify(lista));
        confere(`fase ${f}: tem ${esperado[f].join(' e ')}`, esperado[f].every(t => tipos.has(t)), [...tipos].join(','));
    }
    confere('a Sala de Treino (fase 0) não tem perigo', perigosDe(0).length === 0, JSON.stringify(perigosDe(0)));
    confere('o motor exporta perigoEm e as regras do cenário (PERIGOS)', typeof Motor.perigoEm === 'function' && !!Motor.PERIGOS, Object.keys(Motor).join(','));
    const P = Motor.PERIGOS || {};
    confere('dano fixo no jogador: espinhos 12, fogo 6 por segundo, poço 20', P.espinhos && P.espinhos.dano === 12 && P.fogo && P.fogo.danoPorSegundo === 6 && P.poco && P.poco.dano === 20, JSON.stringify(P));
    // Cada zona fica dentro da tela travada de alguma onda: é na luta que se arremessa alguém nela.
    for (const f of CAMPANHA) {
        const def = Motor.FASES[f];
        for (const p of perigosDe(f)) {
            const cabe = def.ondas.some(o => {
                const travaX = Math.min(def.comprimento - Motor.LARGURA, Math.max(0, o.x - Motor.LARGURA / 2));
                return p.x0 >= travaX + 24 && p.x1 <= travaX + Motor.LARGURA - 24;
            });
            confere(`fase ${f}: a zona ${p.tipo} em x ${p.x0}–${p.x1} fica dentro da tela travada de uma onda`, cabe, JSON.stringify(p));
        }
    }
});

// ── 2. FORA DO CAMINHO OBRIGATÓRIO ────────────────────────────────────────────────────────
// Em todo x da fase sobra uma faixa livre de profundidade de pelo menos 2 × TOLERANCIA_Y (dá pra
// LUTAR sem pisar), e andar da esquerda pro fim segurando só → não pisa em nada.
bloco('caminho livre', () => {
    for (const f of CAMPANHA.concat(['arena'])) {
        const def = f === 'arena' ? Motor.ARENA : Motor.FASES[f];
        if (!def) { confere('existe Motor.ARENA', false, 'não existe'); continue; }
        let pior = 1, ondePior = null;
        for (let x = 0; x <= def.comprimento; x += 5) {
            const cobertos = perigosDe(f).filter(p => x >= p.x0 && x <= p.x1).map(p => [p.y0, p.y1]).sort((a, b) => a[0] - b[0]);
            let livre = 0, cursor = 0;
            for (const [a, b] of cobertos) { livre = Math.max(livre, a - cursor); cursor = Math.max(cursor, b); }
            livre = Math.max(livre, 1 - cursor);
            if (livre < pior) { pior = livre; ondePior = x; }
        }
        confere(`${def.nome}: em todo x sobra faixa livre de ${(2 * Motor.TOLERANCIA_Y).toFixed(2)} de profundidade`, pior >= 2 * Motor.TOLERANCIA_Y - 1e-9, `em x=${ondePior} sobra ${pior.toFixed(2)}`);
    }
    for (const f of CAMPANHA) {
        const m = Motor.criarMundo({ fase: f, jogadores: ['long'], semente: 1 });
        m.onda = m.faseDef.ondas.length;
        const j = m.jogadores[0];
        let pisou = null;
        const eventos = rodar(m, 60 * 30, () => [entrada({ direita: true })], () => { if (!pisou && naZona(m.faseDef, j.x, j.y)) pisou = `x=${j.x.toFixed(0)} y=${j.y.toFixed(2)}`; });
        const doCenario = eventos.filter(e => e.tipo === 'perigo');
        confere(`fase ${f}: andar da esquerda pro fim só com → não pisa em zona e conclui`, m.concluida && !pisou && doCenario.length === 0 && j.danoLevado === 0,
                `concluida=${m.concluida} pisou=${pisou} eventos=${doCenario.length} dano=${j.danoLevado}`);
    }
});

// ── 3. MORTE PELO CENÁRIO: arremessado, lançado, derrubado ────────────────────────────────
// `vida`: a vida do inimigo antes do golpe (null = cheia). Com vida ≤ 12, o tombo do arremesso
// sozinho já mataria: é o caso que trava a ORDEM "o cenário mata antes do tombo".
function morteDe(jeito, tipo, vida) {
    const z = zonaDo(tipo);
    if (!z) return { erro: `nenhuma fase tem ${tipo}` };
    const c = centro(z.zona);
    let m, i;
    const antes = {};
    if (jeito === 'arremessado') {
        // Pelas teclas: agarra de perto e arremessa pra frente. O corpo sai a 34 px e voa ~242 px.
        m = palco(z.fase, c.x - 276, c.y);
        const j = m.jogadores[0];
        i = Motor.colocarInimigo(m, 'sombra', j.x + 40, j.y);
        i.ia.congelada = true;
        if (vida != null) i.vida = vida;
        apertar(m, 'agarrar');
        if (j.estado !== 'agarrando') return { erro: `não agarrou (estado ${j.estado})` };
        antes.pontos = m.pontuacao;
        const ev = apertar(m, 'soco');
        antes.eventos = ev;
    } else {
        // Pelo motor: o golpe do jogador, com o lançamento (560, recuo 140: ~78 px) ou a derrubada (recuo 380: ~106 px).
        const recuo = jeito === 'lancado' ? 140 : 380;
        const voo = jeito === 'lancado' ? 140 * 0.56 : 380 * 0.28;
        m = palco(z.fase, c.x - voo - 200, c.y);
        const j = m.jogadores[0];
        i = Motor.colocarInimigo(m, 'sombra', c.x - voo, c.y);
        i.ia.congelada = true;
        if (vida != null) i.vida = vida;
        Motor.aplicarDano(m, i, jeito === 'lancado' ? { dano: 1, origem: j, lanca: 560, recuo, direcao: 1 } : { dano: 1, origem: j, derruba: true, recuo, direcao: 1 });
        antes.pontos = m.pontuacao;                      // depois do golpe: o ganho medido é só o do cenário
        antes.eventos = m.eventos.slice();
    }
    const vidaNoAr = i.vida;
    const eventos = antes.eventos.concat(rodar(m, 90));
    return { m, i, z, eventos, ganho: m.pontuacao - antes.pontos, vidaNoAr };
}
bloco('morte pelo cenário', () => {
    const P = Motor.PERIGOS || {};
    // Vida cheia e vida 10 (≤ 12, o dano do tombo): o ganho é EXATO — pontos + bônus, nem um ponto do
    // tombo (se o tombo viesse antes do cenário, somaria 120, ou mataria sozinho e levaria o bônus).
    for (const jeito of ['arremessado', 'lancado', 'derrubado']) {
        for (const tipo of TIPOS) for (const vida of [null, 10]) {
            const r = morteDe(jeito, tipo, vida);
            const qual = vida == null ? '' : ` (com vida ${vida})`;
            if (r.erro) { confere(`${jeito} no ${tipo}${qual}`, false, r.erro); continue; }
            const ev = r.eventos.find(e => e.tipo === 'morte-pelo-cenario');
            const morte = r.eventos.find(e => e.tipo === 'morte' && e.time === 'inimigo');
            confere(`inimigo ${jeito} no ${tipo}${qual} morre na hora, com evento próprio`, r.i.estado === 'morto' && !!ev && ev.perigo === tipo && ev.jeito === jeito && ev.id === 'sombra' && !!morte,
                    `estado=${r.i.estado} vida=${r.i.vida} x=${r.i.x.toFixed(0)} y=${r.i.y.toFixed(2)} zona=${JSON.stringify(r.z.zona)} evento=${JSON.stringify(ev)}`);
            const bonus = P.bonus || NaN;
            confere(`${jeito} no ${tipo}${qual}: exatamente os pontos do inimigo mais o bônus do cenário`, r.ganho === Motor.INIMIGOS.sombra.pontos + bonus, `ganho=${r.ganho} (pontos ${Motor.INIMIGOS.sombra.pontos} + bônus ${bonus})`);
            if (jeito === 'arremessado' && vida == null) {
                const textoDoCenario = r.eventos.some(e => e.tipo === 'texto' && ev && e.texto === (Motor.TEXTOS && Motor.TEXTOS.cenario && Motor.TEXTOS.cenario[tipo]));
                confere(`${tipo}: aparece o texto de cenário`, textoDoCenario, r.eventos.filter(e => e.tipo === 'texto').map(e => e.texto).join(','));
            }
        }
    }
    // Arremessado que cai FORA da zona não morre pelo cenário (a régua é o pouso, não o voo).
    const z = zonaDo('espinhos'), c = centro(z.zona);
    const m = palco(z.fase, c.x - 700, c.y);
    const j = m.jogadores[0];
    const i = Motor.colocarInimigo(m, 'sombra', j.x + 40, j.y); i.ia.congelada = true;
    apertar(m, 'agarrar'); apertar(m, 'soco');
    const ev = rodar(m, 90);
    confere('arremessado que pousa longe da zona não morre pelo cenário', !ev.some(e => e.tipo === 'morte-pelo-cenario'), JSON.stringify(ev.filter(e => e.tipo === 'morte-pelo-cenario')));
    // Andar numa zona não mata (quem já está dentro, empurrado por soco, só morre se for ao chão ali).
    const m2 = palco(z.fase, c.x - 300, c.y);
    const dentroDela = Motor.colocarInimigo(m2, 'sombra', c.x, c.y); dentroDela.ia.congelada = true;
    const ev2 = rodar(m2, 30);
    confere('inimigo parado dentro da zona não morre (só quem vai ao chão nela)', Motor.vivo(dentroDela) && !ev2.some(e => e.tipo === 'morte-pelo-cenario'), dentroDela.estado);
    // Quem já está morto ou sendo finalizado não conta.
    const m3 = palco(z.fase, c.x - 300, c.y);
    const fin = Motor.colocarInimigo(m3, 'sombra', c.x, c.y); fin.ia.congelada = true;
    fin.estado = 'finalizado'; fin.quadro = 0; fin.finalizadoPor = m3.jogadores[0]; fin.invulneravel = 99;
    const pontos3 = m3.pontuacao;
    const ev3 = rodar(m3, 30);
    confere('quem está sendo finalizado dentro da zona não morre pelo cenário', !ev3.some(e => e.tipo === 'morte-pelo-cenario') && m3.pontuacao - pontos3 === 0, `eventos=${ev3.map(e => e.tipo).join(',')}`);
});

// ── 4. O CHEFE É IMUNE ────────────────────────────────────────────────────────────────────
bloco('chefe imune', () => {
    for (const tipo of TIPOS) {
        const z = zonaDo(tipo), c = centro(z.zona);
        const m = palco(z.fase, c.x - 500, c.y);
        const j = m.jogadores[0];
        const chefe = Motor.colocarInimigo(m, 'mestreSombra', c.x - 106, c.y);
        chefe.ia.congelada = true;
        Motor.aplicarDano(m, chefe, { dano: 1, origem: j, derruba: true, recuo: 380, direcao: 1 });
        const vidaDepoisDoGolpe = chefe.vida;
        let esteveDentro = false;
        const ev = rodar(m, 150, null, () => { if (naZona(m.faseDef, chefe.x, chefe.y)) esteveDentro = true; });
        confere(`chefe derrubado no ${tipo}: não morre, não perde vida e sai da zona`, esteveDentro && Motor.vivo(chefe) && chefe.vida === vidaDepoisDoGolpe && !naZona(m.faseDef, chefe.x, chefe.y) && !ev.some(e => e.tipo === 'morte-pelo-cenario'),
                `esteveDentro=${esteveDentro} estado=${chefe.estado} vida ${vidaDepoisDoGolpe}→${chefe.vida} x=${chefe.x.toFixed(0)} y=${chefe.y.toFixed(2)}`);
    }
    // Em fúria (parado, invulnerável): dentro da zona, é empurrado pra fora sem dano e a fúria termina normal.
    const z = zonaDo('fogo'), c = centro(z.zona);
    const m = palco(z.fase, c.x - 600, 0.5);
    const j = m.jogadores[0];
    const chefe = Motor.colocarInimigo(m, 'graoPresa', c.x, c.y);
    chefe.ia.congelada = true;
    Motor.aplicarDano(m, chefe, { dano: Math.ceil(chefe.vidaMax * 0.55), origem: j });
    const emFuria = chefe.estado === 'furia';
    const vidaNaFuria = chefe.vida;
    rodar(m, 40);
    confere('chefe em fúria dentro da zona é empurrado pra fora, ainda em fúria e sem dano', emFuria && chefe.estado === 'furia' && chefe.vida === vidaNaFuria && !naZona(m.faseDef, chefe.x, chefe.y),
            `emFuria=${emFuria} estado=${chefe.estado} vida ${vidaNaFuria}→${chefe.vida} x=${chefe.x.toFixed(0)} y=${chefe.y.toFixed(2)}`);
    rodar(m, 40);
    confere('a fúria termina normal (fase 2)', chefe.fase === 2 && Motor.vivo(chefe), `fase=${chefe.fase} estado=${chefe.estado}`);
});

// ── 5. O JOGADOR NUNCA MORRE DE UMA VEZ PELO CENÁRIO ─────────────────────────────────────
bloco('jogador no cenário', () => {
    const P = Motor.PERIGOS || {};
    function jogadorEm(tipo, vida) {
        const z = zonaDo(tipo), c = centro(z.zona);
        const m = palco(z.fase, c.x, c.y);
        const j = m.jogadores[0];
        if (vida != null) j.vida = vida;
        return { m, j, z, c };
    }
    // Espinhos: 12 uma vez, lançado pra fora.
    {
        const { m, j } = jogadorEm('espinhos');
        const ev = rodar(m, 1);
        const levou = j.vidaMax - j.vida;
        rodar(m, 90);
        confere('espinhos: o jogador leva 12 e é lançado pra fora (sem levar de novo)', levou === 12 && j.vidaMax - j.vida === 12 && !naZona(m.faseDef, j.x, j.y) && Motor.vivo(j) && ev.some(e => e.tipo === 'perigo'),
                `levou ${levou} e depois ${j.vidaMax - j.vida}; x=${j.x.toFixed(0)} y=${j.y.toFixed(2)} estado=${j.estado}`);
    }
    // Poço: 20, e volta na borda.
    {
        const { m, j, z } = jogadorEm('poco');
        rodar(m, 1);
        const p = z.zona;
        const distanciaDaBorda = Math.min(Math.abs(j.x - p.x0), Math.abs(j.x - p.x1), Math.abs(j.y - p.y0) * PX_Y, Math.abs(j.y - p.y1) * PX_Y);
        confere('poço: o jogador leva 20 e volta na borda, fora do poço', j.vidaMax - j.vida === 20 && !naZona(m.faseDef, j.x, j.y) && distanciaDaBorda <= 30 && Motor.vivo(j),
                `levou ${j.vidaMax - j.vida}; x=${j.x.toFixed(0)} y=${j.y.toFixed(3)} (borda a ${distanciaDaBorda.toFixed(0)} px)`);
        const P0 = Motor.PERIGOS || {};
        confere('poço: volta com o instante de invulnerabilidade (PERIGOS.poco.invulneravel, 1 s)', P0.poco && P0.poco.invulneravel === 1 && j.invulneravel > 0.9,
                `invulneravel=${j.invulneravel} (regra ${P0.poco && P0.poco.invulneravel})`);
    }
    // Fogo: 6 por segundo (preso dentro por 1 s), e é empurrado pra fora quando solto.
    {
        const { m, j, c } = jogadorEm('fogo');
        rodar(m, 60, null, () => { j.x = c.x; j.y = c.y; });
        const levou = j.vidaMax - j.vida;
        confere('fogo: 6 de dano por segundo dentro dele', levou >= 5 && levou <= 7, `levou ${levou} em 1 s`);
        rodar(m, 90);
        confere('fogo: solto, o jogador é empurrado pra fora', !naZona(m.faseDef, j.x, j.y) && Motor.vivo(j), `x=${j.x.toFixed(0)} y=${j.y.toFixed(2)}`);
    }
    // Nunca de uma vez: com 1 de vida no poço, 3 nos espinhos, 2 preso no fogo por 3 s.
    {
        const a = jogadorEm('poco', 1); rodar(a.m, 60);
        const b = jogadorEm('espinhos', 3); rodar(b.m, 60);
        const f = jogadorEm('fogo', 2); rodar(f.m, 180, null, () => { f.j.x = f.c.x; f.j.y = f.c.y; });
        const ok = [a, b, f].every(r => Motor.vivo(r.j) && r.j.vida >= 1 && r.j.vidas === 3);
        confere('o cenário nunca mata o jogador de uma vez (vida mínima 1, nenhuma vida perdida)', ok,
                [a, b, f].map(r => `${r.z.zona.tipo}: vida ${r.j.vida} estado ${r.j.estado} vidas ${r.j.vidas}`).join(' · '));
    }
    // Pulando por cima: no ar não conta.
    {
        const { m, j } = jogadorEm('espinhos');
        j.z = 120; j.vz = 0; j.estado = 'pulando';
        rodar(m, 1);
        confere('no ar, por cima da zona, não conta', j.vida === j.vidaMax, `vida ${j.vida}`);
    }
    // Quem está finalizando (ou morto) não conta.
    {
        const { m, j, c } = jogadorEm('espinhos');
        j.estado = 'finalizando'; j.quadro = 0;
        rodar(m, 1);
        confere('jogador finalizando dentro da zona não leva nada e não é mexido', j.vida === j.vidaMax && j.x === c.x, `vida ${j.vida} x ${j.x}`);
        const d = jogadorEm('poco');
        d.j.estado = 'morto'; d.j.morteHa = 0; d.j.vida = 0; d.j.invulneravel = 99;
        const ev = rodar(d.m, 1);
        confere('jogador morto dentro da zona não conta', !ev.some(e => e.tipo === 'perigo'), ev.map(e => e.tipo).join(','));
    }
    confere('PERIGOS tem o bônus de pontos do inimigo morto pelo cenário', P.bonus > 0, JSON.stringify(P));
});

// ── 5b. DEFEITO ACHADO AQUI: o jogador DESLIZAVA durante a finalização ───────────────────
// Agarrar andando começava a finalização com o `vx` da caminhada, e nada o zerava: 1,4 s depois o
// monge estava ~340 px adiante, longe do corpo — e, com o cenário, atravessava espinhos imune (quem
// finaliza não conta pro cenário). Quem finaliza fica onde agarrou.
bloco('finalização parada', () => {
    const m = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1 });
    const j = m.jogadores[0];
    const i = Motor.colocarInimigo(m, 'sombra', j.x + 40, j.y);
    i.ia.congelada = true; i.vida = 2; Motor.atordoar(i, 3);
    Motor.passo(m, DT, [entrada({ direita: true })]);
    const x0 = j.x;
    apertar(m, 'agarrar', { direita: true });
    const finalizando = j.estado === 'finalizando';
    rodar(m, 80);
    confere('quem finaliza andando não desliza (fica onde agarrou)', finalizando && Math.abs(j.x - x0) < 10, `estado=${finalizando} andou ${(j.x - x0).toFixed(1)} px`);
});

// ── 5c. DEFEITO ACHADO NA REVISÃO: o jogador DESLIZAVA agarrando ───────────────────────
// O mesmo defeito da finalização, no agarrão comum: agarrar andando começava o 'agarrando' com o `vx`
// da caminhada, e nada o zerava — o monge ia ~250 px por segundo com o preso na frente, sem apertar
// nada, e com o cenário entrava sozinho nos espinhos, arrastando o preso ('agarrado', que o cenário
// ignora) pra dentro da zona, onde era solto vivo. Quem agarra fica onde agarrou.
bloco('agarrão parado', () => {
    const m = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1 });
    const j = m.jogadores[0];
    const i = Motor.colocarInimigo(m, 'sombra', j.x + 80, j.y);
    i.ia.congelada = true;
    rodar(m, 10, () => [entrada({ direita: true })]);
    apertar(m, 'agarrar');
    const agarrou = j.estado === 'agarrando';
    const x0 = j.x;
    let deslize = 0;
    rodar(m, 60, null, () => { if (j.estado === 'agarrando') deslize = Math.max(deslize, Math.abs(j.x - x0)); });
    confere('quem agarra andando e solta as teclas não desliza (fica onde agarrou)', agarrou && j.estado === 'agarrando' && deslize < 2,
            `agarrou=${agarrou} estado=${j.estado} deslizou ${deslize.toFixed(1)} px em 1 s`);
    // Arremessar logo depois de agarrar andando: parado durante o arremesso também.
    const m2 = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1 });
    const j2 = m2.jogadores[0];
    const i2 = Motor.colocarInimigo(m2, 'sombra', j2.x + 80, j2.y); i2.ia.congelada = true;
    rodar(m2, 10, () => [entrada({ direita: true })]);
    apertar(m2, 'agarrar');
    apertar(m2, 'soco');
    const arremessou = j2.estado === 'arremessando', x2 = j2.x;
    rodar(m2, 20);
    confere('quem arremessa logo depois de agarrar andando não desliza no arremesso', arremessou && Math.abs(j2.x - x2) < 2, `arremessou=${arremessou} deslizou ${(j2.x - x2).toFixed(1)} px`);

    // Com o cenário: agarrou andando a 220 px dos espinhos, na faixa deles, e soltou as teclas.
    const z = zonaDo('espinhos'), p = z.zona;
    const m3 = palco(z.fase, p.x0 - 220, (p.y0 + p.y1) / 2);
    const j3 = m3.jogadores[0];
    const i3 = Motor.colocarInimigo(m3, 'sombra', j3.x + 80, j3.y); i3.ia.congelada = true;
    rodar(m3, 10, () => [entrada({ direita: true })]);
    apertar(m3, 'agarrar');
    const agarrou3 = j3.estado === 'agarrando';
    let jogadorEntrou = null, presoEntrou = null;
    const ev3 = rodar(m3, 150, null, q => {
        if (!jogadorEntrou && naZona(m3.faseDef, j3.x, j3.y)) jogadorEntrou = `q${q} x=${j3.x.toFixed(0)} ${j3.estado}`;
        if (!presoEntrou && naZona(m3.faseDef, i3.x, i3.y)) presoEntrou = `q${q} x=${i3.x.toFixed(0)} ${i3.estado}`;
    });
    const perigos3 = ev3.filter(e => e.tipo === 'perigo');
    confere('agarrou andando perto dos espinhos: nem o jogador nem o preso entram na zona', agarrou3 && !jogadorEntrou && !presoEntrou && perigos3.length === 0 && j3.vida === j3.vidaMax,
            `agarrou=${agarrou3} jogador=${jogadorEntrou} preso=${presoEntrou} perigo=${JSON.stringify(perigos3[0] || null)} vida=${j3.vida}`);
});

// ── 6. A IA NÃO ENTRA ANDANDO ─────────────────────────────────────────────────────────────
bloco('IA e as zonas', () => {
    for (const tipo of TIPOS) {
        const z = zonaDo(tipo), p = z.zona, c = centro(p);
        const m = palco(z.fase, p.x1 + 70, c.y);
        const i = Motor.colocarInimigo(m, 'sombra', p.x0 - 80, c.y);
        let entrou = null, chegou = false;
        rodar(m, 60 * 8, null, q => {
            if (!entrou && naZona(m.faseDef, i.x, i.y)) entrou = `quadro ${q}: x=${i.x.toFixed(0)} y=${i.y.toFixed(2)} ${i.estado}`;
            if (Math.abs(i.x - m.jogadores[0].x) < 90 && Math.abs(i.y - m.jogadores[0].y) < Motor.TOLERANCIA_Y) chegou = true;
        });
        confere(`${tipo}: o inimigo trata a zona como parede e contorna até o jogador`, !entrou && chegou, `entrou=${entrou} chegou=${chegou} (i em x=${i.x.toFixed(0)} y=${i.y.toFixed(2)})`);
    }
    // Em luta de verdade (bot bom, fases 1–4): ninguém ENTRA numa zona andando.
    const Bot = require(path.join(__dirname, '..', '..', 'jogo', 'ferramentas', 'bot.js'));
    const violacoes = [];
    for (const f of CAMPANHA) for (const semente of [1, 2]) {
        const m = Motor.criarMundo({ fase: f, jogadores: ['long'], semente, dificuldade: 'normal' });
        const bot = Bot.criarBot({ habilidade: 'bom', semente });
        const antes = new Map();
        for (let q = 0; q < 60 * 150 && !m.concluida && !m.fimDeJogo; q++) {
            Motor.passo(m, DT, [bot.decidir(m)]);
            for (const i of m.inimigos) {
                const agora = !!naZona(m.faseDef, i.x, i.y);
                if (agora && antes.get(i.id) === false && i.estado === 'andando') violacoes.push(`fase ${f} semente ${semente} q${q}: ${i.tipo} x=${i.x.toFixed(0)} y=${i.y.toFixed(2)}`);
                antes.set(i.id, agora);
            }
        }
    }
    confere('em lutas de verdade (bot bom, fases 1–4) nenhum inimigo entra andando numa zona', violacoes.length === 0, violacoes.slice(0, 4).join(' · ') + (violacoes.length > 4 ? ` … (${violacoes.length})` : ''));
});

// ── 6b. O BOT DO SIMULADOR E O PALCO ──────────────────────────────────────────────────────
// Os números do simulador só valem se o bot joga como gente: não fica parado atrás de um chá que está
// dentro de uma zona, e na Arena espera a próxima onda no meio da tela.
bloco('o bot e o palco', () => {
    const Bot = require(path.join(__dirname, '..', '..', 'jogo', 'ferramentas', 'bot.js'));
    const presos = [];
    for (const tipo of TIPOS) {
        const z = zonaDo(tipo), p = z.zona, cz = centro(p);
        const m = palco(z.fase, p.x0 - 300, 0.5);
        const j = m.jogadores[0];
        j.vida = Math.round(j.vidaMax * 0.2);                       // pouca vida: o bot quer chá
        m.itens.push({ id: ++m.proximoId, tipo: 'cha', x: cz.x, y: cz.y, z: 0, vz: 0 });
        const bot = Bot.criarBot({ habilidade: 'bom', semente: 1 });
        for (let q = 0; q < 60 * 30 && !m.concluida; q++) Motor.passo(m, DT, [bot.decidir(m)]);
        if (!m.concluida) presos.push(`${tipo} (fase ${z.fase}): parado em x=${j.x.toFixed(0)} y=${j.y.toFixed(2)}, zona em ${p.x0}–${p.x1}`);
    }
    confere('com pouca vida e o único chá dentro de uma zona, o bot não fica preso na borda: segue e fecha a fase', presos.length === 0, presos.join(' · '));
    // Arena, entre as ondas: o bot vai pro meio da tela (é de lá que se pega a próxima onda).
    const m = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 1 });
    m.arena.respiro = 99;                                            // sem onda: só o respiro
    m.jogadores[0].x = 900;
    const bot = Bot.criarBot({ habilidade: 'bom', semente: 1 });
    for (let q = 0; q < 60 * 4; q++) Motor.passo(m, DT, [bot.decidir(m)]);
    const x = m.jogadores[0].x;
    confere('na Arena, entre as ondas, o bot espera no meio da tela', Math.abs(x - Motor.LARGURA / 2) <= 40, `x=${x.toFixed(0)} (meio ${Motor.LARGURA / 2})`);
});

// ── 7. PONTOS DE CONTROLE: criarMundo({ ondaInicial }) ────────────────────────────────────
bloco('ondaInicial', () => {
    for (const f of CAMPANHA) {
        const def = Motor.FASES[f];
        for (let k = 1; k < def.ondas.length; k++) {
            const m = Motor.criarMundo({ fase: f, jogadores: ['long', 'shen'], semente: 4, ondaInicial: k, pontuacao: 1234 });
            const gatilho = def.ondas[k].x, anterior = def.ondas[k - 1].x;
            const posicaoOk = m.jogadores.every(j => j.x < gatilho && j.x > anterior && !naZona(def, j.x, j.y));
            const naTela = m.jogadores.every(j => j.x >= m.camera.x && j.x <= m.camera.x + Motor.LARGURA);
            const vasosOk = m.objetos.every(o => (o.x < gatilho) === (o.estado === 'quebrado'));
            confere(`fase ${f}, onda ${k + 1}: ondas anteriores feitas, jogadores antes do gatilho e na tela, vasos anteriores quebrados, pontos`,
                    m.onda === k && !m.travado && posicaoOk && naTela && vasosOk && m.pontuacao === 1234 && m.itens.length === 0 && m.inimigos.length === 0,
                    `onda=${m.onda} x=${m.jogadores.map(j => j.x.toFixed(0)).join(',')} gatilho=${gatilho} camera=${m.camera.x.toFixed(0)} vasos=${m.objetos.map(o => `${o.x}:${o.estado}`).join(',')} pontos=${m.pontuacao}`);
        }
    }
    const def = Motor.FASES[1];
    const m = Motor.criarMundo({ fase: 1, semente: 4, ondaInicial: 2 });
    const eventos = rodar(m, 60 * 4, () => [entrada({ direita: true })]);
    const onda = eventos.find(e => e.tipo === 'onda');
    confere('andando pra direita, dispara a onda do ponto de controle (e não a primeira)', onda && onda.numero === 3 && m.travado, JSON.stringify(onda));
    const zero = Motor.criarMundo({ fase: 1, semente: 4, ondaInicial: 0 }), padrao = Motor.criarMundo({ fase: 1, semente: 4 });
    confere('ondaInicial 0 é o começo da fase de sempre', zero.jogadores[0].x === padrao.jogadores[0].x && zero.onda === 0, `${zero.jogadores[0].x} vs ${padrao.jogadores[0].x}`);
    const recusadas = [-1, def.ondas.length, 1.5, 'x'].filter(k => { try { Motor.criarMundo({ fase: 1, ondaInicial: k }); return true; } catch (e) { return false; } });
    confere('ondaInicial fora das ondas da fase é recusada', recusadas.length === 0, `aceitou ${recusadas.join(',')}`);
    const a = Motor.criarMundo({ fase: 2, semente: 9, ondaInicial: 3 }), b = Motor.criarMundo({ fase: 2, semente: 9, ondaInicial: 3 });
    rodar(a, 400, () => [entrada({ direita: true, soco: true })]); rodar(b, 400, () => [entrada({ direita: true, soco: true })]);
    confere('mesma semente e mesma ondaInicial, mesma luta', JSON.stringify(a.jogadores.map(j => [j.x, j.vida])) === JSON.stringify(b.jogadores.map(j => [j.x, j.vida])) && a.inimigos.length === b.inimigos.length,
            `${a.inimigos.length} vs ${b.inimigos.length}`);

    // O ponto de controle é a última onda DISPARADA, com os pontos de quando ela disparou.
    const c = Motor.criarMundo({ fase: 1, semente: 5, pontuacao: 1000 });
    confere('o mundo guarda onde a fase começou (inicioDaFase) e o ponto de controle inicial', c.inicioDaFase === 1000 && c.pontoDeControle && c.pontoDeControle.onda === 0 && c.pontoDeControle.pontuacao === 1000,
            JSON.stringify({ inicio: c.inicioDaFase, pc: c.pontoDeControle }));
    c.onda = 1; c.pontuacao = 2777;
    for (const j of c.jogadores) j.x = def.ondas[1].x + 5;
    rodar(c, 1);
    confere('disparar a onda 2 grava o ponto de controle com os pontos daquela hora', c.pontoDeControle && c.pontoDeControle.onda === 1 && c.pontoDeControle.pontuacao === 2777, JSON.stringify(c.pontoDeControle));
    c.pontuacao = 3500;                                   // pontos ganhos DEPOIS do ponto de controle se perdem ao morrer
    const opcoes = typeof Motor.opcoesDoRecomeco === 'function' ? Motor.opcoesDoRecomeco(c) : null;
    confere('opcoesDoRecomeco: a fase, a onda e os pontos do ponto de controle, e o começo da fase', !!opcoes && opcoes.fase === 1 && opcoes.ondaInicial === 1 && opcoes.pontuacao === 2777 && opcoes.inicioDaFase === 1000,
            JSON.stringify(opcoes));
    // Karma sem dobrar: começou a fase com 1000, morreu, recomeçou do ponto de controle (2777) e fechou com 5000.
    if (opcoes) {
        const d = Motor.criarMundo(Object.assign({ jogadores: ['long'], semente: 5 }, opcoes));
        confere('recomeçar do ponto de controle: 3 vidas, os pontos dele e o começo da fase guardado', d.jogadores[0].vidas === 3 && d.pontuacao === 2777 && d.inicioDaFase === 1000 && d.onda === 1,
                JSON.stringify({ vidas: d.jogadores[0].vidas, pontos: d.pontuacao, inicio: d.inicioDaFase, onda: d.onda }));
        d.pontuacao = 5000;
        const prog = Progresso.criar({ carregar: () => null, salvar: () => { } });
        const loja = Loja.criar(prog);
        const ganho = loja.receberDaFase(d.inicioDaFase, d.pontuacao);
        confere('o karma da fase é dos pontos ganhos NELA (5000 − 1000), uma vez só', ganho === Loja.karmaDaFase(4000) && prog.dados.karma === ganho, `ganho=${ganho} saldo=${prog.dados.karma}`);
    }
});

// ── 8. O MODO ARENA ───────────────────────────────────────────────────────────────────────
bloco('arena', () => {
    confere('existe a Arena no motor (infinita, uma tela só)', !!Motor.ARENA && Motor.ARENA.infinita === true && Motor.ARENA.comprimento === Motor.LARGURA, JSON.stringify(Motor.ARENA && { infinita: Motor.ARENA.infinita, comprimento: Motor.ARENA.comprimento }));
    confere('a Arena não entra na lista da campanha', !Motor.FASES.some(f => f.infinita) && Motor.FASES.length === 5, `${Motor.FASES.length} fases`);
    confere('existe o gerador de onda (Motor.ondaDaArena)', typeof Motor.ondaDaArena === 'function', 'não existe');
    const R = Motor.REGRAS_DA_ARENA || {};
    const teto = R.tetoDeInimigos;
    confere('as regras da Arena estão num lugar só (REGRAS_DA_ARENA: respiro 3 s, chefe a cada 5, teto de inimigos)', R.respiro === 3 && R.chefeACada === 5 && teto >= 6 && teto <= 12, JSON.stringify(R));
    const erradas = [], chefesErrados = [], tiposVistos = new Set(), brutosDemais = [];
    for (const semente of [1, 2, 3]) {
        const rng = Motor.criarRng(semente);
        for (let n = 1; n <= 40; n++) {
            const o = Motor.ondaDaArena(n, rng);
            const comuns = o.inimigos.reduce((s, [, k]) => s + k, 0);
            if (comuns !== Math.min(teto, 2 + Math.floor(n / 2))) erradas.push(`n=${n}: ${comuns}`);
            const chefeCerto = n % 5 === 0 ? CHEFES.includes(o.chefe) : !o.chefe;
            if (!chefeCerto) chefesErrados.push(`n=${n}: ${o.chefe}`);
            for (const [t] of o.inimigos) { tiposVistos.add(t); if (!Motor.INIMIGOS[t] || Motor.INIMIGOS[t].chefe) erradas.push(`n=${n}: tipo ${t}`); }
            const brutos = o.inimigos.filter(([t]) => t === 'bruto').reduce((s, [, k]) => s + k, 0);
            if (brutos > 1 + Math.floor(n / 8)) brutosDemais.push(`semente ${semente} n=${n}: ${brutos}`);
        }
    }
    for (let semente = 4; semente <= 50; semente++) {
        const rng = Motor.criarRng(semente);
        for (let n = 1; n <= 40; n++) {
            const o = Motor.ondaDaArena(n, rng);
            const brutos = o.inimigos.filter(([t]) => t === 'bruto').reduce((s, [, k]) => s + k, 0);
            if (brutos > 1 + Math.floor(n / 8)) brutosDemais.push(`semente ${semente} n=${n}: ${brutos}`);
        }
    }
    confere('teto de Brutos: a onda n tem no máximo 1 + floor(n/8) (três na onda 9 era parede)', brutosDemais.length === 0, brutosDemais.slice(0, 6).join(' · ') + (brutosDemais.length > 6 ? ` … (${brutosDemais.length})` : ''));
    confere('a onda n tem 2 + floor(n/2) inimigos comuns, até o teto', erradas.length === 0, erradas.slice(0, 6).join(' · '));
    confere('a cada 5 ondas vem um chefe sorteado (e só nelas)', chefesErrados.length === 0, chefesErrados.slice(0, 6).join(' · '));
    confere('a mistura inclui Lanceiro e Renegado (e o Bruto)', ['lanceiro', 'renegado', 'bruto'].every(t => tiposVistos.has(t)), [...tiposVistos].join(','));
    const chefesSorteados = new Set();
    for (let s = 1; s <= 30; s++) chefesSorteados.add(Motor.ondaDaArena(5, Motor.criarRng(s)).chefe);
    confere('o chefe é sorteado entre os quatro', CHEFES.every(c => chefesSorteados.has(c)), [...chefesSorteados].join(','));
    const vidaMedia = (de, ate) => {
        let soma = 0, k = 0;
        for (let s = 1; s <= 20; s++) { const rng = Motor.criarRng(s); for (let n = 1; n <= ate; n++) { const o = Motor.ondaDaArena(n, rng); if (n >= de) for (const [t, q] of o.inimigos) { soma += Motor.INIMIGOS[t].vida * q; k += q; } } }
        return soma / k;
    };
    const cedo = vidaMedia(1, 4), tarde = vidaMedia(16, 20);
    confere('a mistura fica mais dura com n (vida média dos comuns das ondas 16–20 > das 1–4)', tarde > cedo * 1.3, `1–4: ${cedo.toFixed(1)} · 16–20: ${tarde.toFixed(1)}`);
    const lista = s => { const rng = Motor.criarRng(s); return JSON.stringify(Array.from({ length: 20 }, (_, n) => Motor.ondaDaArena(n + 1, rng))); };
    confere('o gerador é determinístico: mesma semente, mesmas ondas; outra semente, outras', lista(7) === lista(7) && lista(7) !== lista(8), 'igual/diferente errado');

    // No mundo: uma tela, respiro de 3 s, a onda n com o número certo, nunca conclui.
    // Mata todo mundo meio segundo depois de a onda entrar (chefe inclusive) e anota cada onda.
    function lutaDaArena(semente, ondas) {
        const m = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente });
        const j = m.jogadores[0];
        const r = { m, camera: [], quantos: [], respiros: [], ondasAvisadas: [], composicoes: [], cha: 0, chaDepoisDoChefe: null, naoDisparou: null };
        let ultimaLimpeza = null, ondaEm = 0;
        for (let q = 0; q < 60 * 120 && r.ondasAvisadas.length < ondas; q++) {
            Motor.passo(m, DT, [entrada()]);
            r.camera.push(m.camera.x);
            j.vida = j.vidaMax; j.x = Math.min(j.x, 900);
            for (const e of m.eventos) {
                if (e.tipo === 'onda') {
                    r.ondasAvisadas.push(e.numero);
                    const vivos = m.inimigos.filter(i => Motor.vivo(i) && !i.def.chefe).length;
                    r.quantos.push([e.numero, vivos, !!m.chefe, !!(m.chefe && m.chefe.def.fase2)]);
                    r.composicoes.push(m.inimigos.filter(i => Motor.vivo(i)).map(i => i.tipo).sort().join('+'));
                    if (ultimaLimpeza != null) r.respiros.push(+(m.tempo - ultimaLimpeza).toFixed(3));
                    ondaEm = m.tempo;
                }
                if (e.tipo === 'arena-onda') {
                    ultimaLimpeza = m.tempo;
                    const chaNoMeio = m.itens.some(it => it.tipo === 'cha' && Math.abs(it.x - Motor.LARGURA / 2) < 60);
                    if (e.cha && chaNoMeio) r.cha++;
                    if (e.sobrevividas === 5) r.chaDepoisDoChefe = e.cha === true && chaNoMeio;
                }
            }
            if (m.travado && m.inimigos.length && m.tempo - ondaEm > 0.5) {
                for (const i of m.inimigos) if (Motor.vivo(i)) Motor.aplicarDano(m, i, { dano: 99999, origem: j });
            }
            if (m.concluida) r.naoDisparou = `concluiu no quadro ${q}`;
        }
        return r;
    }
    const L = lutaDaArena(11, 7);
    const m = L.m, camera = L.camera, quantos = L.quantos, respiros = L.respiros, ondasAvisadas = L.ondasAvisadas, cha = L.cha, naoDisparou = L.naoDisparou;
    confere('a Arena dispara ondas seguidas (1, 2, 3, …)', ondasAvisadas.slice(0, 6).join(',') === '1,2,3,4,5,6', ondasAvisadas.join(','));
    confere('no mundo, a onda n tem 2 + floor(n/2) inimigos comuns (um jogador)', quantos.every(([n, v]) => v === Math.min(teto, 2 + Math.floor(n / 2))), JSON.stringify(quantos));
    confere('a onda 5 traz um chefe, com fúria (fase 2 na definição)', quantos.some(([n, , c, f2]) => n === 5 && c && f2) && quantos.filter(([n, , c]) => c).every(([n]) => n === 5), JSON.stringify(quantos));
    confere('entre uma onda e a próxima, 3 s de respiro', respiros.length >= 3 && respiros.every(r => Math.abs(r - 3) <= 2 * DT), respiros.join(','));
    confere('a câmera nunca anda', camera.every(x => x === 0), `câmera foi até ${Math.max(...camera)}`);
    confere('a Arena nunca conclui', !m.concluida && !naoDisparou, naoDisparou);
    confere('as ondas sobrevividas ficam no mundo (mundo.arena.sobrevividas)', m.arena && m.arena.sobrevividas >= 6, JSON.stringify(m.arena));
    confere('um chá de vez em quando entre as ondas', cha > 0, `chás=${cha}`);
    // Depois do chefe o chá é garantido (não é sorte), em qualquer semente.
    const semChaDepoisDoChefe = [];
    for (let s = 1; s <= 12; s++) { const r = lutaDaArena(s, 6); if (r.chaDepoisDoChefe !== true) semChaDepoisDoChefe.push(`semente ${s}: ${r.chaDepoisDoChefe}`); }
    confere('depois da onda do chefe vem sempre um chá no meio', semChaDepoisDoChefe.length === 0, semChaDepoisDoChefe.join(' · '));
    // As ondas saem do mundo.rng: mesma semente, mesmas ondas; sementes diferentes, ondas diferentes.
    const comp = s => lutaDaArena(s, 6).composicoes.join(' | ');
    const porSemente = [1, 2, 3, 21, 22].map(comp);
    confere('no mundo, as ondas da Arena saem do mundo.rng (mesma semente, mesmas ondas; outras sementes, outras ondas)',
            comp(1) === porSemente[0] && new Set(porSemente).size > 1, porSemente.join('  //  '));
    // A vida dos inimigos sobe a cada 5 ondas (1–5 como o pátio, 6–10 como a floresta…).
    const vidaNaOnda = n => { const w = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 1 }); w.arena.onda = n; return Motor.colocarInimigo(w, 'sombra', 100, 0.5).vidaMax; };
    const v1 = vidaNaOnda(1), v6 = vidaNaOnda(6), v16 = vidaNaOnda(16);
    confere('na Arena a vida dos inimigos sobe a cada 5 ondas', v1 === Motor.INIMIGOS.sombra.vida && v6 > v1 && v16 > v6, `Sombra: onda 1 ${v1} · onda 6 ${v6} · onda 16 ${v16}`);
    // Andar até a borda da direita não conclui nem mexe a câmera.
    const m2 = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 12 });
    rodar(m2, 60 * 3, () => [entrada({ direita: true })]);
    confere('andar até a borda não conclui a Arena', !m2.concluida && m2.camera.x === 0 && m2.jogadores[0].x <= Motor.LARGURA, `x=${m2.jogadores[0].x} concluida=${m2.concluida}`);
    // Todos mortos sem vidas: acabou.
    const m3 = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 13 });
    const j3 = m3.jogadores[0];
    j3.vidas = 1;
    Motor.aplicarDano(m3, j3, { dano: 9999, time: 'inimigo' });
    rodar(m3, 120);
    confere('a Arena acaba quando todos morrem sem vidas', m3.fimDeJogo, `fim=${m3.fimDeJogo} vidas=${j3.vidas}`);
    // Determinismo por semente, com IA de verdade.
    const luta = s => { const w = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: s }); rodar(w, 60 * 12, q => [entrada({ soco: q % 20 === 0, apertou: Object.assign(Motor.entradaVazia().apertou, { soco: q % 20 === 0 }) })]); return JSON.stringify([w.pontuacao, w.jogadores[0].vida, w.inimigos.map(i => [i.tipo, Math.round(i.x)]), w.arena]); };
    confere('Arena: mesma semente, mesma luta', luta(21) === luta(21) && luta(21) !== luta(22), 'não repetiu (ou repetiu com outra semente)');
    // As melhorias do Templo e o P2 valem na Arena.
    const m4 = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 1, liberados: ['vigor'] });
    const p2 = Motor.adicionarJogador(m4, 'lian');
    confere('as melhorias do Templo valem na Arena (e pro P2 que entra)', m4.jogadores[0].vidaMax === 120 && p2.melhorias.vigor === true && p2.x >= 0 && p2.x <= Motor.LARGURA, `vida ${m4.jogadores[0].vidaMax} p2.x=${p2.x}`);
    // Dois jogadores: um inimigo a mais, como na campanha.
    const m5 = Motor.criarMundo({ fase: 'arena', jogadores: ['long', 'shen'], semente: 3 });
    const ev5 = rodar(m5, 60 * 3);
    const n5 = ev5.find(e => e.tipo === 'onda');
    confere('com dois jogadores, um inimigo a mais', n5 && m5.inimigos.filter(i => !i.def.chefe).length === Math.min(teto, 2 + Math.floor(n5.numero / 2)) + 1, `${m5.inimigos.length} inimigos`);
});

// ── 9. PROGRESSO, KARMA E CONQUISTAS ──────────────────────────────────────────────────────
bloco('progresso da arena', () => {
    const velho = Progresso.normalizar({ recorde: 900, faseAlcancada: 3 });
    confere('salvamento velho (sem arena) ganha o recorde da Arena zerado', velho.arena && velho.arena.recorde === 0 && velho.arena.melhorOnda === 0 && velho.recorde === 900,
            JSON.stringify(velho.arena));
    const novo = Progresso.normalizar({ recorde: 900, arena: { recorde: 4200, melhorOnda: 12 } });
    confere('salvamento novo mantém o recorde da Arena', novo.arena && novo.arena.recorde === 4200 && novo.arena.melhorOnda === 12, JSON.stringify(novo.arena));
    const lixo = Progresso.normalizar({ arena: { recorde: -5, melhorOnda: 'doze', extra: 1 } });
    const lixo2 = Progresso.normalizar({ arena: 'x' });
    confere('lixo no recorde da Arena vira zero (e chave estranha some)', lixo.arena && lixo.arena.recorde === 0 && lixo.arena.melhorOnda === 0 && !('extra' in lixo.arena) && lixo2.arena && lixo2.arena.recorde === 0,
            JSON.stringify([lixo.arena, lixo2.arena]));
    const guardado = { v: null };
    const prog = Progresso.criar({ carregar: () => ({ recorde: 100 }), salvar: d => { guardado.v = JSON.parse(JSON.stringify(d)); } });
    const r1 = typeof prog.registrarArena === 'function' ? prog.registrarArena(5000, 7) : null;
    confere('registrarArena guarda pontos e melhor onda, separado do recorde da campanha', !!r1 && prog.dados.arena.recorde === 5000 && prog.dados.arena.melhorOnda === 7 && prog.dados.recorde === 100 && guardado.v && guardado.v.arena.recorde === 5000,
            JSON.stringify({ r1, arena: prog.dados.arena, recorde: prog.dados.recorde }));
    const r2 = prog.registrarArena(3000, 9);
    confere('recorde e melhor onda sobem cada um por si', prog.dados.arena.recorde === 5000 && prog.dados.arena.melhorOnda === 9 && r2 && r2.recorde === false && r2.onda === true, JSON.stringify({ r2, arena: prog.dados.arena }));
    prog.registrarRecorde(99999);
    confere('o recorde da campanha não mexe no da Arena', prog.dados.arena.recorde === 5000 && prog.dados.recorde === 99999, JSON.stringify(prog.dados.arena));
    prog.apagar();
    confere('apagar o progresso zera a Arena também', prog.dados.arena.recorde === 0 && prog.dados.arena.melhorOnda === 0, JSON.stringify(prog.dados.arena));

    confere('karma da Arena = pontos / 200 (metade da campanha)', typeof Loja.karmaDaArena === 'function' && Loja.karmaDaArena(1000) === 5 && Loja.karmaDaFase(1000) === 10 && Loja.karmaDaArena(399) === 1 && Loja.karmaDaArena(-50) === 0 && Loja.karmaDaArena(NaN) === 0,
            typeof Loja.karmaDaArena === 'function' ? `${Loja.karmaDaArena(1000)}` : 'não existe');
    let salvou = 0;
    const p2 = Progresso.criar({ carregar: () => null, salvar: () => { salvou++; } });
    const loja = Loja.criar(p2);
    const salvouAntes = salvou;
    const ganho = typeof loja.receberDaArena === 'function' ? loja.receberDaArena(1999) : null;
    confere('receberDaArena credita o karma no fim e grava', ganho === 9 && p2.dados.karma === 9 && salvou > salvouAntes, `ganho=${ganho} saldo=${p2.dados.karma} salvou=${salvou - salvouAntes}`);
});

bloco('conquistas do palco', () => {
    confere('existem as conquistas pelo_cenario e arena_10', !!(Conquistas.POR_ID.pelo_cenario && Conquistas.POR_ID.arena_10), Object.keys(Conquistas.POR_ID).join(','));
    const novas = () => {
        const prog = Progresso.criar({ carregar: () => null, salvar: () => { } });
        const ganhas = [];
        return { prog, ganhas, c: Conquistas.criar({ progresso: prog, aoDesbloquear: d => ganhas.push(d.id) }) };
    };
    // De ponta a ponta: o evento que o motor emite quando o cenário mata.
    const r = morteDe('lancado', 'espinhos');
    const a = novas();
    if (!r.erro) a.c.processar(r.eventos, r.m);
    confere('matar um inimigo com o cenário dá "pelo_cenario"', a.ganhas.includes('pelo_cenario'), a.ganhas.join(','));
    const b = novas();
    b.c.processar([{ tipo: 'morte', time: 'inimigo', id: 'sombra' }]);
    b.c.processar([{ tipo: 'perigo', perigo: 'fogo', jogador: 0, dano: 1 }]);
    confere('morte comum (nem o jogador ferido pelo cenário) não dá "pelo_cenario"', !b.ganhas.includes('pelo_cenario'), b.ganhas.join(','));
    // De ponta a ponta: o jogador parado no fogo e nos espinhos, sem inimigo nenhum.
    const b2 = novas();
    for (const tipo of ['fogo', 'espinhos', 'poco']) {
        const z = zonaDo(tipo), cz = centro(z.zona);
        const w = palco(z.fase, cz.x, cz.y);
        for (let q = 0; q < 60; q++) { Motor.passo(w, DT, [entrada()]); b2.c.processar(w.eventos, w); }
    }
    confere('o jogador ferido pelo cenário não ganha "pelo_cenario"', !b2.ganhas.includes('pelo_cenario'), b2.ganhas.join(','));

    // 'Intocável' ("termine uma fase sem levar dano") não sai de um recomeço: morrer no chefe e vencê-lo
    // sem apanhar do ponto de controle não é uma fase sem dano.
    const f1 = Motor.FASES[1];
    const primeira = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 5 });
    const jp = primeira.jogadores[0];
    Motor.aplicarDano(primeira, jp, { dano: 80, time: 'inimigo' });
    primeira.onda = f1.ondas.length - 1;
    jp.x = f1.ondas[primeira.onda].x + 5;
    rodar(primeira, 1);
    const levou = jp.danoLevado;
    const opc = Motor.opcoesDoRecomeco(primeira);
    const recomeco = Motor.criarMundo(Object.assign({ jogadores: ['long'], semente: 5 }, opc));
    const d = novas();
    d.c.concluirFase(recomeco);
    confere('recomeçar do ponto de controle depois de apanhar e fechar a fase sem dano NÃO dá "Intocável"', levou > 0 && recomeco.onda === f1.ondas.length - 1 && !d.ganhas.includes('intocavel'),
            `dano antes=${levou} onda=${recomeco.onda} ganhas=${d.ganhas.join(',')} opcoes=${JSON.stringify(opc)}`);
    const e = novas();
    e.c.concluirFase(Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 5 }));
    confere('fase inteira sem dano (sem recomeço) continua dando "Intocável"', e.ganhas.includes('intocavel'), e.ganhas.join(','));
    const c = novas();
    c.c.processar([{ tipo: 'arena-onda', sobrevividas: 9 }]);
    const antes = c.ganhas.includes('arena_10');
    c.c.processar([{ tipo: 'arena-onda', sobrevividas: 10 }]);
    confere('sobreviver a 10 ondas na Arena dá "arena_10" (9 não)', !antes && c.ganhas.includes('arena_10'), c.ganhas.join(','));
    // O id tem algarismo: o `normalizar` do progresso não pode jogar fora a conquista ao carregar.
    const relido = Progresso.normalizar(JSON.parse(JSON.stringify(c.prog.dados)));
    confere('"arena_10" sobrevive a salvar e carregar (normalizar aceita algarismo no id)', typeof relido.conquistas.arena_10 === 'number', JSON.stringify(relido.conquistas));
});

// ── 10. O QUE É DO NAVEGADOR: cenario.js, desenho.js e principal.js ───────────────────────
function ctxQueAnota() {
    const nada = () => {};
    const anotado = { rects: [], textos: [], chamadas: 0 };
    const ctx = new Proxy({}, {
        get(t, k) {
            if (k in t) return t[k];
            if (k === '__anotado') return anotado;
            if (k === 'fillRect' || k === 'rect') return (x, y, w, h) => { anotado.chamadas++; anotado.rects.push({ x, y, w, h }); };
            if (k === 'fillText' || k === 'strokeText') return s => { anotado.textos.push(String(s)); };
            if (k === 'measureText') return () => ({ width: 10 });
            if (/Gradient|Pattern/.test(k)) return () => ({ addColorStop: nada });
            if (k === 'getImageData' || k === 'createImageData') return () => ({ data: new Uint8ClampedArray(4 * 256 * 256) });
            return (...a) => { anotado.chamadas++; };
        },
        set(t, k, v) { t[k] = v; return true; },
    });
    return ctx;
}
bloco('desenho do palco', () => {
    const documento = { createElement: () => ({ width: 1, height: 1, getContext: () => ctxQueAnota() }) };
    const janela = { PunhosDeShaolin: { Motor } };
    const contexto = vm.createContext({ window: janela, document: documento, Math, console, Uint8ClampedArray });
    vm.runInContext(fs.readFileSync(path.join(raiz, 'cenario.js'), 'utf8'), contexto, { filename: 'cenario.js' });
    const C = janela.PunhosDeShaolin.Cenario;
    confere('cenario.js exporta o desenho das zonas (Cenario.perigos)', typeof C.perigos === 'function', Object.keys(C).join(','));
    if (typeof C.perigos === 'function') {
        for (const tipo of TIPOS) {
            const z = zonaDo(tipo);
            const ctx = ctxQueAnota();
            C.perigos(ctx, [z.zona], z.zona.x0 - 200, 1.7, { qualidade: 'alta' });
            const media = ctxQueAnota();
            C.perigos(media, [z.zona], z.zona.x0 - 200, 1.7, { qualidade: 'media' });
            const fora = ctxQueAnota();
            C.perigos(fora, [z.zona], z.zona.x1 + 2000, 1.7, { qualidade: 'alta' });
            confere(`o ${tipo} é pintado (e nada é pintado com a zona fora da tela)`, ctx.__anotado.chamadas > 5 && media.__anotado.chamadas > 5 && fora.__anotado.chamadas === 0,
                    `${ctx.__anotado.chamadas} / ${media.__anotado.chamadas} / fora ${fora.__anotado.chamadas}`);
        }
    }
    // desenho.js: a cena chama o desenho das zonas, e as telas da Arena não estouram.
    const chamadas = [];
    const janela2 = { PunhosDeShaolin: { Motor, Cenario: new Proxy({}, { get: (t, k) => (...a) => { chamadas.push([k, a[1]]); return k === 'ambiente' ? '#000' : undefined; } }) } };
    const contexto2 = vm.createContext({ window: janela2, Math, console });
    for (const f of ['figura.js', 'desenho.js']) vm.runInContext(fs.readFileSync(path.join(raiz, f), 'utf8'), contexto2, { filename: f });
    const D = janela2.PunhosDeShaolin.Desenho;
    const m = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1 });
    D.desenharMundo(ctxQueAnota(), m, D.criarEfeitos(), 1, {});
    const pediu = chamadas.find(([k]) => k === 'perigos');
    confere('a cena pede ao cenário as zonas da fase', !!pediu && pediu[1] === m.faseDef.perigos, chamadas.map(([k]) => k).join(','));
    const ma = Motor.criarMundo({ fase: 'arena', jogadores: ['long'], semente: 1 });
    rodar(ma, 60 * 2);
    const ctxA = ctxQueAnota();
    const ef = D.criarEfeitos();
    ef.processar(ma.eventos, ma, null);
    D.desenharMundo(ctxA, ma, ef, 1, {});
    confere('na Arena, o HUD mostra a onda', ctxA.__anotado.textos.some(t => /ONDA/i.test(t)), ctxA.__anotado.textos.join(' | '));
    const ctxI = ctxQueAnota();
    D.desenharIntroFase(ctxI, Motor.ARENA, 0.6);
    confere('a introdução da Arena não diz "FASE arena"', ctxI.__anotado.textos.length > 0 && !ctxI.__anotado.textos.some(t => /FASE\s+arena/i.test(t)), ctxI.__anotado.textos.join(' | '));
    const ctxF = ctxQueAnota();
    D.desenharFim(ctxF, 1, D.criarEfeitos(), { arena: true, vitoria: false, pontuacao: 4321, ondas: 7, recorde: 5000, melhorOnda: 9, novoRecorde: false, novaMelhorOnda: false, karma: 21, faseNome: 'Arena', finalizacoes: 1, maiorCombo: 5, inimigos: 12, toque: false });
    confere('a tela de fim da Arena mostra as ondas sobrevividas e o karma', ctxF.__anotado.textos.some(t => /7 ondas/i.test(t)) && ctxF.__anotado.textos.some(t => /21 karma/i.test(t)), ctxF.__anotado.textos.join(' | '));
    const ev = D.criarEfeitos();
    ev.processar([{ tipo: 'morte-pelo-cenario', perigo: 'fogo', x: 100, y: 0.5 }, { tipo: 'perigo', perigo: 'poco', x: 100, y: 0.5 }, { tipo: 'onda', numero: 3, total: null }], m, null);
    confere('os efeitos aceitam os eventos novos (e a onda da Arena não vira "3 / null")', ev.particulas.length > 0 && !/null|undefined/.test(ev.ondaTexto), `${ev.particulas.length} partículas, "${ev.ondaTexto}"`);

    const principal = fs.readFileSync(path.join(raiz, 'principal.js'), 'utf8');
    confere('principal.js: o menu tem o item Arena', /id:\s*'arena'/.test(principal), 'não tem');
    confere('principal.js: "tentar de novo" recomeça pelo ponto de controle do motor', /Motor\.opcoesDoRecomeco\(/.test(principal), 'não usa');
    confere('principal.js: o karma da fase sai do começo da fase guardado no mundo', /receberDaFase\(\s*m\.inicioDaFase\s*,\s*m\.pontuacao\s*\)/.test(principal), (principal.match(/.*receberDaFase.*/g) || []).join(' | '));
    confere('principal.js: "Continuar" começa no começo da fase alcançada', /comecarEscolha\(progresso\.dados\.faseAlcancada\)/.test(principal), 'mudou');
    confere('principal.js: no fim da Arena, recorde separado e karma da Arena', /registrarArena\(/.test(principal) && /receberDaArena\(/.test(principal), 'não registra');
});

// ── 11. O principal.js RODANDO: tentar de novo, o menu da Arena e o fim da Arena ─────────
// As regex acima só provam que o texto existe. Aqui o principal.js roda de verdade num `vm`, com o
// motor, o progresso, a loja, as conquistas, a figura, o cenário e o desenho REAIS, e falsos só o que
// é do navegador (canvas, teclado, som, plataforma). O `Motor` é embrulhado pra anotar cada
// `criarMundo` e cada `opcoesDoRecomeco`. As telas andam pelo laço (`requestAnimationFrame`).
async function montarPrincipal() {
    const anot = { criarMundo: [], recomecos: [], erros: [] };
    const MotorEspiao = Object.assign({}, Motor, {
        criarMundo: o => { anot.criarMundo.push(Object.assign({}, o)); return Motor.criarMundo(o); },
        opcoesDoRecomeco: m => { const r = Motor.opcoesDoRecomeco(m); anot.recomecos.push(Object.assign({}, r)); return r; },
    });
    const semNada = alvo => new Proxy(alvo, { get: (t, k) => (k in t ? t[k] : () => { }) });
    const elemento = () => semNada({ style: {}, dataset: {}, classList: semNada({}), getBoundingClientRect: () => ({ left: 0, top: 0, width: Motor.LARGURA, height: Motor.ALTURA }) });
    const canvasFalso = () => { const c = elemento(); c.width = 1; c.height = 1; c.getContext = () => ctxQueAnota(); return c; };
    const tela = canvasFalso();
    const documento = { getElementById: id => (id === 'tela' ? tela : elemento()), createElement: () => canvasFalso(), body: elemento() };
    let proxima = null;                    // o próximo quadro que o laço pediu
    let relogio = 0;
    let agora = { sistema: {}, entradas: [entrada(), entrada()] };
    const guardado = { v: null };
    const plataforma = semNada({ ehDesktop: false, temSteam: false, carregar: () => guardado.v, salvar: d => { guardado.v = JSON.parse(JSON.stringify(d)); } });
    const janela = {
        PunhosDeShaolin: {
            Motor: MotorEspiao, Progresso, Conquistas, Loja,
            Plataforma: { criar: () => plataforma },
            Som: { criar: () => semNada({ ligado: false, silenciado: false }) },
            Entrada: { criar: () => ({ lerSistema: () => agora.sistema, ler: () => agora.entradas }) },
        },
        innerWidth: 1280, innerHeight: 720, devicePixelRatio: 1,
        addEventListener: () => { },
        requestAnimationFrame: f => { proxima = f; },
    };
    const consoleFalso = {
        log: () => { }, info: () => { },
        warn: (...a) => anot.erros.push(`warn: ${a.join(' ')}`), error: (...a) => anot.erros.push(`error: ${a.join(' ')}`),
    };
    const contexto = vm.createContext({ window: janela, document: documento, console: consoleFalso, performance: { now: () => relogio }, setTimeout, Uint8ClampedArray });
    for (const f of ['figura.js', 'cenario.js', 'desenho.js', 'principal.js']) vm.runInContext(fs.readFileSync(path.join(raiz, f), 'utf8'), contexto, { filename: f });
    await new Promise(r => setImmediate(r));                         // o laço começa depois das fontes (uma promessa)
    if (!proxima) throw new Error('o principal.js não pediu o primeiro quadro');
    const P = janela.PunhosDeShaolin;
    // Um quadro de 1/60 s com o que se aperta nele. `sistema`: confirmar, voltar…; `e0`: botões do P1.
    function quadro(sistema, e0) {
        const e = entrada(e0);
        for (const b of Object.keys(e0 || {})) if (b in e.apertou && e0[b]) e.apertou[b] = true;
        agora = { sistema: sistema || {}, entradas: [e, entrada()] };
        relogio += 1000 / 60;
        const f = proxima; proxima = null;
        f(relogio);
        if (!proxima) throw new Error(`o laço parou na tela ${P.jogo.tela}`);
    }
    function ate(condicao, limite, sistema) { for (let q = 0; q < limite && !condicao(); q++) quadro(sistema); return condicao(); }
    return { anot, P, jogo: P.jogo, quadro, ate };
}
// Do título até a luta, pelo menu: 'novo' (dificuldade → escolha) ou 'arena' (uma linha abaixo).
function doTituloALuta(h, item) {
    h.quadro({ confirmar: true });                                   // título → menu
    if (item === 'arena') h.quadro({}, { baixo: true });             // menu novo: Novo jogo, Arena, …
    h.quadro({ confirmar: true });
    if (item === 'novo') h.quadro({ confirmar: true });              // dificuldade → escolha
    h.quadro({ confirmar: true });                                   // P1 confirma o monge
    h.quadro({ confirmar: true });                                   // pronto
    h.ate(() => h.jogo.tela === 'intro', 60);
    h.quadro({ confirmar: true });                                   // pula a introdução
}
function morrerDeVez(h) {
    const m = h.jogo.mundo;
    for (const j of m.jogadores) { j.vidas = 1; j.invulneravel = 0; Motor.aplicarDano(m, j, { dano: j.vida, time: 'inimigo' }); }
    return h.ate(() => h.jogo.tela === 'fim', 60 * 8);
}
async function principalRodando() {
    // A. Campanha: morre na onda 3 → "tentar de novo" recomeça DELA, com os pontos de quando disparou.
    {
        const h = await montarPrincipal();
        doTituloALuta(h, 'novo');
        const m = h.jogo.mundo;
        confere('principal (rodando): "Novo jogo" abre a fase 1 na luta', h.jogo.tela === 'jogo' && m && m.fase === 1 && h.anot.criarMundo.length === 1, `tela=${h.jogo.tela} fase=${m && m.fase}`);
        const def = Motor.FASES[1];
        m.onda = 2; m.pontuacao = 2500;
        for (const j of m.jogadores) j.x = def.ondas[2].x + 5;
        h.ate(() => m.pontoDeControle.onda === 2, 10);              // o passo fixo pode sobrar pro quadro seguinte
        const pc = JSON.stringify(m.pontoDeControle);
        m.pontuacao = 3900;                                          // ganho depois do ponto de controle: perde
        Motor.aplicarDano(m, m.jogadores[0], { dano: 30, time: 'inimigo' });
        const chegouAoFim = morrerDeVez(h);
        h.ate(() => h.jogo.fimHa >= 0.8, 120);
        h.quadro({ confirmar: true });                               // tentar de novo
        const r = h.anot.recomecos[h.anot.recomecos.length - 1];
        const o = h.anot.criarMundo[h.anot.criarMundo.length - 1];
        const perdidos = r ? Object.keys(r).filter(k => o[k] !== r[k]) : ['(sem recomeço)'];
        confere('principal (rodando): "tentar de novo" passa ao criarMundo TUDO o que o opcoesDoRecomeco devolveu', chegouAoFim && !!r && h.anot.criarMundo.length === 2 && perdidos.length === 0,
                `fim=${chegouAoFim} recomeço=${JSON.stringify(r)} criarMundo=${JSON.stringify(o && Object.assign({}, o, { liberados: undefined }))} perdidos=${perdidos.join(',')}`);
        const n = h.jogo.mundo;
        confere('principal (rodando): o recomeço é na onda 3, com os pontos de quando ela disparou e três vidas',
                n !== m && n.fase === 1 && n.onda === 2 && n.pontuacao === 2500 && n.inicioDaFase === 0 && n.jogadores.every(j => j.vidas === 3),
                `pc=${pc} onda=${n.onda} pontos=${n.pontuacao} inicio=${n.inicioDaFase} vidas=${n.jogadores.map(j => j.vidas)}`);
        // Fecha a fase recomeçada sem apanhar: karma só dos pontos da fase, e nada de "Intocável".
        h.quadro({ confirmar: true });
        n.onda = def.ondas.length; n.travado = false; n.inimigos = [];
        for (const j of n.jogadores) j.x = def.comprimento - 60;
        const karmaAntes = h.P.loja.saldo();
        h.ate(() => h.jogo.faseFechada, 60 * 5);
        confere('principal (rodando): a fase recomeçada fecha com o karma dos pontos dela, uma vez',
                h.jogo.faseFechada && h.P.loja.saldo() - karmaAntes === Loja.karmaDaFase(n.pontuacao - 0), `fechou=${h.jogo.faseFechada} karma +${h.P.loja.saldo() - karmaAntes} pontos=${n.pontuacao}`);
        confere('principal (rodando): quem apanhou antes do ponto de controle não ganha "Intocável" ao fechar a fase recomeçada',
                h.jogo.faseFechada && !h.P.progresso.dados.conquistas.intocavel, JSON.stringify(Object.keys(h.P.progresso.dados.conquistas)));
        confere('principal (rodando): a campanha roda sem erro no console', h.anot.erros.length === 0, h.anot.erros.slice(0, 3).join(' | '));
    }
    // B. Arena pelo menu: roda duas ondas, e o fim grava o recorde da Arena e o karma pela metade.
    {
        const h = await montarPrincipal();
        doTituloALuta(h, 'arena');
        const m = h.jogo.mundo;
        confere('principal (rodando): o item Arena do menu abre a Arena', h.jogo.tela === 'jogo' && h.jogo.modo === 'arena' && !!m && m.faseDef === Motor.ARENA && h.anot.criarMundo[0].fase === 'arena',
                `tela=${h.jogo.tela} modo=${h.jogo.modo} fase=${m && m.fase} criarMundo=${JSON.stringify(h.anot.criarMundo.map(o => o.fase))}`);
        if (!m || !m.arena) return;                                  // não abriu a Arena: o resto dela não tem o que medir
        let ondaEm = null;
        const duas = h.ate(() => {
            for (const j of m.jogadores) j.vida = j.vidaMax;
            if (m.travado && ondaEm == null) ondaEm = m.tempo;
            if (!m.travado) ondaEm = null;
            if (ondaEm != null && m.tempo - ondaEm > 0.5) for (const i of m.inimigos) if (Motor.vivo(i)) Motor.aplicarDano(m, i, { dano: 99999, origem: m.jogadores[0] });
            return m.arena.sobrevividas >= 2;
        }, 60 * 40);
        confere('principal (rodando): a Arena roda duas ondas sem erro no console', duas && h.anot.erros.length === 0, `sobrevividas=${m.arena.sobrevividas} erros=${h.anot.erros.slice(0, 3).join(' | ')}`);
        const progresso = h.P.progresso;
        const recordeDaCampanha = progresso.dados.recorde, karmaAntes = h.P.loja.saldo();
        m.pontuacao = 4321;
        const ondas = m.arena.sobrevividas;
        const acabou = morrerDeVez(h);
        const f = h.jogo.fim || {};
        confere('principal (rodando): o fim da Arena grava o recorde DELA (pontos e onda) e não mexe no da campanha',
                acabou && f.arena === true && progresso.dados.arena.recorde === 4321 && progresso.dados.arena.melhorOnda === ondas && progresso.dados.recorde === recordeDaCampanha,
                `fim=${acabou} arena=${JSON.stringify(progresso.dados.arena)} recorde da campanha ${recordeDaCampanha}→${progresso.dados.recorde} tela.fim.arena=${f.arena}`);
        confere('principal (rodando): o karma da Arena é pontos/200, creditado uma vez no fim', h.P.loja.saldo() - karmaAntes === Loja.karmaDaArena(4321) && f.karma === Loja.karmaDaArena(4321),
                `karma +${h.P.loja.saldo() - karmaAntes} (fim diz ${f.karma}; esperado ${Loja.karmaDaArena(4321)})`);
        h.ate(() => h.jogo.fimHa >= 0.8, 120);
        h.quadro({ confirmar: true });
        confere('principal (rodando): "tentar de novo" na Arena é uma Arena nova do zero', h.jogo.mundo !== m && h.jogo.mundo.faseDef === Motor.ARENA && h.jogo.mundo.pontuacao === 0,
                `fase=${h.jogo.mundo && h.jogo.mundo.fase} pontos=${h.jogo.mundo && h.jogo.mundo.pontuacao}`);
    }
}

(async () => {
    try { await principalRodando(); } catch (erro) { confere('principal rodando (o bloco rodou até o fim)', false, erro && erro.stack ? erro.stack.split('\n').slice(0, 4).join(' | ') : String(erro)); }
    console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
    process.exit(falhas.length === 0 ? 0 : 1);
})();
