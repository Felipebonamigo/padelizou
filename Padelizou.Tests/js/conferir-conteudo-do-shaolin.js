// O CONTEÚDO NOVO do "Punhos de Shaolin" (jogo/), conferido no Node sem navegador.
//
//     node Padelizou.Tests/js/conferir-conteudo-do-shaolin.js
//
// Fase 1 do `jogo/CRONOGRAMA.md`, a parte de conteúdo: o terceiro monge (a Lian, com corrente),
// dois inimigos novos (o Lanceiro, que bate de longe no chão, e o Monge Renegado, que defende e
// contra-ataca), os chefes em duas fases (FÚRIA ao cruzar metade da vida) e a tela de escolha com
// N monges. E um DEFEITO que o simulador achou: com a tela travada, o jogador fica preso entre
// `travaX + 24` e `travaX + 936`, mas o inimigo podia ficar até 240 px além da tela — o Gigante batia
// de fora do alcance do soco e o Arqueiro fugia pra fora da tela e atirava de lá.
//
// Arquivos puros (`motor.js`) entram por `require`; `figura.js` e `desenho.js` (do navegador) rodam
// num contexto `vm` com um `ctx` que só anota o que foi pedido, como no conferidor da loja; o
// `principal.js` (laço do navegador) é conferido pelo texto, no mesmo espírito da GEOMETRIA_DO_TEMPLO.
//
// Sem dependência: `require` de arquivos do repositório e mais nada.
const path = require('path');
const fs = require('fs');
const vm = require('vm');
const raiz = path.join(__dirname, '..', '..', 'jogo', 'js');
const Motor = require(path.join(raiz, 'motor.js'));
const Bot = require(path.join(__dirname, '..', '..', 'jogo', 'ferramentas', 'bot.js'));

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}
// Um bloco que ESTOURA vira falha com a mensagem — não derruba os outros blocos e não some calado.
function bloco(nome, fn) {
    try { fn(); } catch (erro) { confere(`${nome} (o bloco rodou até o fim)`, false, `${erro && erro.message}`); }
}

const DT = 1 / 60;
const CHEFES = ['mestreSombra', 'graoPresa', 'gigante', 'feiticeiro'];

// ── AJUDANTES (os mesmos dos outros conferidores do jogo) ─────────────────────────────────
function entrada(extra) { return Object.assign(Motor.entradaVazia(), extra || {}); }
function rodar(mundo, quadros, entradas) {
    for (let i = 0; i < quadros; i++) {
        const e = typeof entradas === 'function' ? entradas(i) : entradas;
        Motor.passo(mundo, DT, e || [entrada()]);
    }
}
function apertar(mundo, botao, extra) {
    const e = entrada(extra);
    e.apertou[botao] = true;
    e[botao] = true;
    const lista = mundo.jogadores.map(() => entrada());
    lista[0] = e;
    Motor.passo(mundo, DT, lista);
}
function treino(personagens, semente) {
    return Motor.criarMundo({ fase: 0, jogadores: personagens || ['long'], semente: semente || 7 });
}
function boneco(mundo, tipo, dx, extra) {
    const j = mundo.jogadores[0];
    const i = Motor.colocarInimigo(mundo, tipo, j.x + dx, j.y);
    i.ia.congelada = true;
    return Object.assign(i, extra || {});
}
function textos(mundo) { return mundo.eventos.filter(e => e.tipo === 'texto').map(e => e.texto); }
// Rodar anotando os textos que apareceram.
function rodarAnotando(mundo, quadros, entradas) {
    const vistos = [];
    for (let i = 0; i < quadros; i++) {
        const e = typeof entradas === 'function' ? entradas(i) : entradas;
        Motor.passo(mundo, DT, e || mundo.jogadores.map(() => entrada()));
        vistos.push(...textos(mundo));
    }
    return vistos;
}
// Deixa o inimigo em guarda (como a IA faz ao ver o golpe começar), de frente pro jogador.
function emGuarda(i) {
    i.estado = 'defendendo'; i.quadro = 0; i.golpe = null; i.golpeNome = null; i.vx = 0;
    i.ia.defendendoAte = 0.55;
    i.virado = -1;
    return i;
}

// ── 1. A LIAN EXISTE, COM TODOS OS GOLPES QUE O RESTO DO JOGO PEDE ────────────────────────
// Os golpes que o jogador usa são pedidos pelo NOME no motor (`iniciarGolpe(j, 'chuteAereo')`,
// `def.golpes.joelhada`), pelas correntes (`proximo`, `proximoCinco`) e pelas melhorias do Templo
// (a Sequência de Cinco pede soco4/soco5, o Especial no Ar pede especialAereo). Varre o trecho do
// jogador no motor e confere nos três monges — golpe que falta é `throw` no meio da luta.
bloco('a Lian', () => {
    const lian = Motor.PERSONAGENS.lian;
    confere('existe o terceiro monge, a Lian, A Corrente do Rio, com arma corrente', !!lian && lian.nome === 'Lian' && lian.titulo === 'A Corrente do Rio' && lian.arma === 'corrente',
            JSON.stringify(lian && { nome: lian.nome, titulo: lian.titulo, arma: lian.arma }));
    if (!lian) return;
    const { long, shen } = Motor.PERSONAGENS;
    confere('rápida e frágil: menos vida e mais velocidade e corrida que os outros dois',
            lian.vida < long.vida && lian.vida < shen.vida && lian.velocidade > long.velocidade && lian.velocidade > shen.velocidade && lian.corrida > long.corrida && lian.corrida > shen.corrida,
            `vida ${lian.vida} · velocidade ${lian.velocidade} · corrida ${lian.corrida}`);
    confere('socos de alcance longo (≥ 100) e dano baixo (menor que o do Long)', lian.golpes.soco1.alcance >= 100 && lian.golpes.soco1.dano < long.golpes.soco1.dano && lian.golpes.soco2.alcance >= 100,
            `soco1 alcance ${lian.golpes.soco1.alcance} dano ${lian.golpes.soco1.dano}`);

    const fonte = fs.readFileSync(path.join(raiz, 'motor.js'), 'utf8');
    const comeco = fonte.indexOf('function controlarJogador'), fim = fonte.indexOf('// ── INIMIGO (IA)');
    confere('achei o trecho do jogador no motor', comeco > 0 && fim > comeco, 'os marcadores mudaram — ajuste a varredura aqui');
    const trecho = fonte.slice(comeco, fim);
    const pedidos = new Set();
    // O argumento inteiro de cada iniciarGolpe(j, …), com parênteses balanceados; `tem(j, 'melhoria')`
    // dentro dele é condição, não golpe.
    for (let k = trecho.indexOf('iniciarGolpe(j,'); k >= 0; k = trecho.indexOf('iniciarGolpe(j,', k + 1)) {
        let nivel = 0, fimArg = k + 'iniciarGolpe('.length;
        for (; fimArg < trecho.length; fimArg++) { const ch = trecho[fimArg]; if (ch === '(') nivel++; else if (ch === ')') { if (nivel === 0) break; nivel--; } }
        const arg = trecho.slice(k + 'iniciarGolpe(j,'.length, fimArg).replace(/tem\([^)]*\)/g, '');
        for (const n of arg.matchAll(/'(\w+)'/g)) pedidos.add(n[1]);
    }
    for (const m of trecho.matchAll(/golpes\.(\w+)/g)) pedidos.add(m[1]);
    for (const p of [long, shen]) for (const n of Object.keys(p.golpes)) pedidos.add(n);
    confere('a varredura achou os golpes de sempre (soco1, chute, chuteAereo, investida, especial, especialAereo, joelhada)',
            ['soco1', 'chute', 'chuteAereo', 'investida', 'especial', 'especialAereo', 'joelhada', 'soco4', 'soco5'].every(n => pedidos.has(n)), [...pedidos].join(','));
    for (const [id, p] of Object.entries(Motor.PERSONAGENS)) {
        const faltam = [...pedidos].filter(n => !p.golpes[n]);
        for (const g of Object.values(p.golpes)) for (const n of [g.proximo, g.proximoCinco]) if (n && !p.golpes[n]) faltam.push(`${n} (corrente)`);
        confere(`${id}: tem todos os golpes que o motor, as correntes e as melhorias pedem`, faltam.length === 0, `faltam: ${faltam.join(', ')}`);
    }
    confere('o especial da Lian é o Puxão do Rio', lian.golpes.especial.nome === 'Puxão do Rio' && lian.golpes.especial.chi > 0, JSON.stringify(lian.golpes.especial));
    confere('o especial no ar da Lian gira dos dois lados', !!lian.golpes.especialAereo.dosDoisLados, JSON.stringify(lian.golpes.especialAereo));

    // Joga de verdade: a corrente de socos emenda e lança, como nos outros dois.
    const mundo = treino(['lian']);
    const j = mundo.jogadores[0];
    const alvo = boneco(mundo, 'sombra', 80, { vida: 1000, vidaMax: 1000 });
    for (let k = 0; k < 3; k++) { apertar(mundo, 'soco'); rodar(mundo, 11); }
    rodar(mundo, 3);
    confere('Lian: três socos a 80 px acertam e o terceiro lança', alvo.vida < 1000 && (alvo.estado === 'lancado' || alvo.z > 0), `vida=${alvo.vida} estado=${alvo.estado} golpe=${j.golpeNome}`);
});

// ── 2. O PUXÃO DO RIO ─────────────────────────────────────────────────────────────────────
// A corrente vai à frente, na mesma faixa, até ~260 px, e PUXA o primeiro inimigo que acerta pra
// perto dela: dano baixo e atordoado curto. Chefe só leva o dano. E a régua da finalização continua
// sendo a VIDA: o atordoado do puxão num inimigo são é agarrão comum.
function puxao(dxs, extra) {
    const o = extra || {};
    const mundo = treino(['lian']);
    const j = mundo.jogadores[0];
    j.chi = 100;
    const alvos = dxs.map(([tipo, dx, dy]) => { const a = boneco(mundo, tipo, dx); if (dy) a.y = Math.min(1, j.y + dy); if (o.vida) a.vida = o.vida; return a; });
    const antes = alvos.map(a => ({ x: a.x, vida: a.vida }));
    apertar(mundo, 'especial');
    const estados = alvos.map(() => new Set());
    for (let f = 0; f < 45; f++) { rodar(mundo, 1); alvos.forEach((a, k) => estados[k].add(a.estado)); }
    return { mundo, j, alvos, antes, estados };
}
bloco('puxão do rio', () => {
    if (!Motor.PERSONAGENS.lian) { confere('o puxão existe (sem a Lian, nada a conferir)', false, 'não existe a Lian'); return; }
    const custo = Motor.PERSONAGENS.lian.golpes.especial.chi;
    const r = puxao([['sombra', 220]]);
    const a = r.alvos[0];
    confere('o puxão gasta o chi do especial', r.j.chi <= 100 - custo + 4 && r.j.chi >= 100 - custo, `chi=${r.j.chi} custo=${custo}`);
    confere('a 220 px, o puxão acerta e TRAZ o inimigo pra perto (< 90 px)', a.vida < r.antes[0].vida && Math.abs(a.x - r.j.x) < 90,
            `vida ${r.antes[0].vida}→${a.vida} · distância ${Math.abs(r.antes[0].x - r.j.x).toFixed(0)}→${Math.abs(a.x - r.j.x).toFixed(0)}`);
    confere('dano baixo (≤ 8)', r.antes[0].vida - a.vida <= 8, `tirou ${r.antes[0].vida - a.vida}`);
    confere('o puxado fica atordoado', r.estados[0].has('atordoado'), [...r.estados[0]].join(','));
    {
        // CURTO: mede quanto dura, como na guarda quebrada (ver o nome não basta — 5 s passavam).
        const mundo = treino(['lian']);
        mundo.jogadores[0].chi = 100;
        const s = boneco(mundo, 'sombra', 220);
        apertar(mundo, 'especial');
        let dur = 0;
        for (let f = 0; f < 180; f++) { rodar(mundo, 1); if (s.estado === 'atordoado') dur += DT; }
        confere('e o atordoado do puxão é curto (entre 0,3 e 1,0 s)', dur >= 0.3 && dur <= 1.0, `${dur.toFixed(2)} s`);
    }

    const longe = puxao([['sombra', 330]]);
    confere('a 330 px a corrente não chega', longe.alvos[0].vida === longe.antes[0].vida && longe.alvos[0].x === longe.antes[0].x, `vida=${longe.alvos[0].vida} x=${longe.alvos[0].x}`);
    const faixa = puxao([['sombra', 150, 0.35]]);
    confere('em outra faixa de profundidade não pega', faixa.alvos[0].vida === faixa.antes[0].vida, `vida=${faixa.alvos[0].vida}`);
    const dois = puxao([['sombra', 120], ['sombra', 200]]);
    confere('puxa só o PRIMEIRO da fila; o de trás fica onde estava e intacto', dois.alvos[0].vida < dois.antes[0].vida && dois.alvos[1].vida === dois.antes[1].vida && dois.alvos[1].x === dois.antes[1].x,
            `primeiro vida ${dois.alvos[0].vida} · segundo vida ${dois.alvos[1].vida} x ${dois.antes[1].x}→${dois.alvos[1].x}`);

    // Alvo NO AR (lançado pelo combo): a corrente só machuca, não puxa. Antes ela zerava a altura no
    // mesmo quadro — 40 a 68 px de queda num quadro — e quem ia cair (e ficar fora do agarrão)
    // terminava atordoado em pé, ao alcance da mão.
    {
        const mundo = treino(['lian']);
        const j = mundo.jogadores[0]; j.chi = 100;
        const s = boneco(mundo, 'sombra', 150);
        s.estado = 'lancado'; s.quadro = 0; s.z = 60; s.vz = 0;
        const vida = s.vida;
        apertar(mundo, 'especial');
        let maiorQueda = 0, zAntes = s.z;
        const vistos = new Set();
        for (let f = 0; f < 60; f++) { rodar(mundo, 1); maiorQueda = Math.max(maiorQueda, zAntes - s.z); zAntes = s.z; vistos.add(s.estado); }
        confere('alvo no ar: leva o dano da corrente, mas não é puxado nem cai de uma vez (queda ≤ 15 px por quadro)', s.vida < vida && !vistos.has('puxado') && maiorQueda <= 15 && vistos.has('caido'),
                `vida ${vida}→${s.vida} · maior queda num quadro ${maiorQueda.toFixed(0)} px · estados ${[...vistos].join(',')}`);
    }

    // Quem SEGUROU o puxão na defesa só leva o dano de defesa: não vem, não fica atordoado.
    for (const tipo of ['sombra', 'renegado']) {
        const mundo = treino(['lian']);
        const j = mundo.jogadores[0]; j.chi = 100;
        const g = emGuarda(boneco(mundo, tipo, 200));
        const vida = g.vida, estados = new Set();
        apertar(mundo, 'especial');
        for (let f = 0; f < 45; f++) { rodar(mundo, 1); estados.add(g.estado); }
        const defesa = Math.max(1, Math.round(Motor.PERSONAGENS.lian.golpes.especial.dano * 0.2));
        confere(`quem defende (${tipo}) segura o puxão: só o dano de defesa, não vem nem fica atordoado`,
                vida - g.vida === defesa && Math.abs(g.x - j.x) >= 195 && !estados.has('puxado') && !estados.has('atordoado'),
                `vida ${vida}→${g.vida} (defesa ${defesa}) · distância ${Math.abs(g.x - j.x).toFixed(0)} · estados ${[...estados].join(',')}`);
    }

    for (const c of CHEFES) {
        const rc = puxao([[c, 200]]);
        const ch = rc.alvos[0];
        confere(`chefe (${c}) só leva o dano: não vem`, ch.vida < rc.antes[0].vida && Math.abs(ch.x - rc.j.x) >= 195 && !rc.estados[0].has('atordoado'),
                `vida ${rc.antes[0].vida}→${ch.vida} · distância ${Math.abs(ch.x - rc.j.x).toFixed(0)} · estados ${[...rc.estados[0]].join(',')}`);
    }

    // A régua da finalização é a vida: o puxado com vida cheia é agarrado, não finalizado. Agarra no
    // primeiro quadro em que ele está atordoado — com a Lian já livre do golpe e colada nele.
    function agarrarPuxado(extra) {
        const mundo = treino(['lian']); const j = mundo.jogadores[0]; j.chi = 100;
        const s = boneco(mundo, 'sombra', 200, extra);
        apertar(mundo, 'especial');
        let tonto = false;
        for (let f = 0; f < 45 && !tonto; f++) { rodar(mundo, 1); tonto = s.estado === 'atordoado'; }
        j.x = s.x - 40; j.virado = 1; j.estado = 'parado'; j.golpe = null; j.golpeNome = null;
        apertar(mundo, 'agarrar');
        return { tonto, jogador: j.estado, alvo: s.estado, vida: `${s.vida}/${s.vidaMax}` };
    }
    const semFinalizar = agarrarPuxado();
    confere('puxado com vida cheia NÃO é finalizado: é o agarrão comum', semFinalizar.tonto && semFinalizar.jogador === 'agarrando' && semFinalizar.alvo === 'agarrado', JSON.stringify(semFinalizar));
    const comPoucaVida = agarrarPuxado({ vida: 11 });            // o puxão tira 6: sobram 5 de 30, abaixo dos 22%
    confere('e com pouca vida (abaixo da linha) finaliza — a régua continua a vida', comPoucaVida.tonto && comPoucaVida.jogador === 'finalizando' && comPoucaVida.alvo === 'finalizado', JSON.stringify(comPoucaVida));
});

// ── 3. O LANCEIRO ─────────────────────────────────────────────────────────────────────────
bloco('lanceiro', () => {
    const def = Motor.INIMIGOS.lanceiro;
    confere('existe o Lanceiro, com lança e ~45 de vida', !!def && def.arma === 'lanca' && def.vida >= 40 && def.vida <= 50, JSON.stringify(def && { arma: def.arma, vida: def.vida }));
    if (!def) return;
    const est = def.golpes.estocada;
    confere('a estocada tem alcance longo (≥ 110) e derruba', !!est && est.alcance >= 110 && est.derruba === true, JSON.stringify(est));

    // Acerta de LONGE: a 115 px, onde o soco do Long nem chega.
    const mundo = treino(['long']);
    const j = mundo.jogadores[0];
    const l = boneco(mundo, 'lanceiro', 115, { virado: -1 });
    Motor.iniciarGolpe(l, 'estocada');
    const vistos = new Set();
    for (let f = 0; f < 40; f++) { rodar(mundo, 1); vistos.add(j.estado); }
    confere('a estocada acerta a 115 px e derruba', j.vida < j.vidaMax && (vistos.has('lancado') || vistos.has('caido')), `vida=${j.vida} estados=${[...vistos].join(',')}`);
    const alcanceDoSoco = Motor.PERSONAGENS.long.golpes.soco1.alcance + Motor.MEIA_LARGURA * l.escala;
    confere('(controle) de 115 px o soco do Long não chega', 115 > alcanceDoSoco, `soco chega a ${alcanceDoSoco}`);

    // Mantém a distância certa pra lança: vem de longe e estoca de onde a lança chega e o soco não.
    const distancias = [];
    for (const semente of [1, 2, 3]) {
        const m = treino(['long'], semente);
        const jj = m.jogadores[0];
        const li = Motor.colocarInimigo(m, 'lanceiro', jj.x + 450, jj.y);
        let antes = li.estado;
        for (let f = 0; f < 60 * 6; f++) {
            rodar(m, 1);
            if (li.estado === 'atacando' && antes !== 'atacando' && li.golpeNome === 'estocada') distancias.push(Math.round(Math.abs(li.x - jj.x)));
            antes = li.estado;
        }
    }
    const [minimo, maximo] = def.ia.distancia || [0, 0];
    confere('o lanceiro vem de longe e estoca (3 sementes, 6 s cada)', distancias.length >= 3, `estocadas=${distancias.length}`);
    confere('e estoca da distância da lança: longe do soco, dentro da estocada', distancias.length > 0 && distancias.every(d => d >= minimo - 12 && d <= maximo + 12) && distancias.some(d => d > 70),
            `distâncias=${distancias.join(',')} faixa=${minimo}–${maximo}`);

    // Recua quando o jogador cola — com chance: em algumas sementes recua, em outras estoca de perto.
    let recuou = 0, ficou = 0;
    for (let semente = 1; semente <= 24; semente++) {
        const m = treino(['long'], semente);
        const jj = m.jogadores[0];
        const li = Motor.colocarInimigo(m, 'lanceiro', jj.x + 30, jj.y);
        li.ia.pausa = 0; li.virado = -1;
        let resultado = null;
        for (let f = 0; f < 50 && !resultado; f++) {
            rodar(m, 1);
            if (li.estado === 'atacando') resultado = 'ficou';
            else if (Math.abs(li.x - jj.x) > 60) resultado = 'recuou';
        }
        if (resultado === 'recuou') recuou++; else if (resultado === 'ficou') ficou++;
    }
    confere('colado demais, às vezes recua e às vezes estoca de perto (24 sementes)', recuou >= 3 && ficou >= 3, `recuou ${recuou} · ficou ${ficou}`);

    // A estocada é golpe no chão: entra na fila dos ATACANTES_MAX (2) que batem ao mesmo tempo. A flecha
    // é projétil e fica fora dessa fila. Quatro de cada em volta do jogador parado, contando quantos
    // estão em 'atacando' no mesmo quadro.
    function maisAtacandoJuntos(tipo) {
        let maximo = 0;
        for (const semente of [1, 2, 3, 4, 5, 6]) {
            const m = treino(['long'], semente);
            const jj = m.jogadores[0];
            jj.vidaMax = jj.vida = 1e6;
            const bando = [-120, -100, 100, 120].map(dx => Motor.colocarInimigo(m, tipo, jj.x + dx, jj.y));
            for (let q = 0; q < 60 * 15; q++) {
                rodar(m, 1);
                jj.vida = jj.vidaMax;
                if (jj.estado !== 'parado') { jj.estado = 'parado'; jj.z = 0; jj.vz = 0; jj.vx = 0; }
                maximo = Math.max(maximo, bando.filter(i => i.estado === 'atacando').length);
            }
        }
        return maximo;
    }
    const lanceirosJuntos = maisAtacandoJuntos('lanceiro'), arqueirosJuntos = maisAtacandoJuntos('arqueiro');
    confere('quatro lanceiros: nunca mais que 2 estocando ao mesmo tempo (a estocada respeita o limite)', lanceirosJuntos >= 1 && lanceirosJuntos <= 2, `no máximo ${lanceirosJuntos} juntos`);
    confere('quatro arqueiros: a flecha fica fora do limite (mais de 2 atirando juntos)', arqueirosJuntos > 2, `no máximo ${arqueirosJuntos} juntos`);

    // O arqueiro continua o de antes: foge pra longe e atira.
    const ma = treino(['long'], 5);
    const ja = ma.jogadores[0];
    const arq = Motor.colocarInimigo(ma, 'arqueiro', ja.x + 100, ja.y);
    let maisLonge = 0, flechas = 0;
    for (let f = 0; f < 60 * 4; f++) {
        rodar(ma, 1);
        maisLonge = Math.max(maisLonge, Math.abs(arq.x - ja.x));
        flechas += ma.eventos.filter(e => e.tipo === 'projetil' && e.nome === 'flecha').length;
    }
    confere('(o arqueiro não quebrou) ele ainda se afasta pra longe e atira flecha', maisLonge >= 230 && flechas >= 1, `mais longe=${maisLonge.toFixed(0)} flechas=${flechas}`);
});

// ── 4. O MONGE RENEGADO ───────────────────────────────────────────────────────────────────
bloco('renegado', () => {
    const def = Motor.INIMIGOS.renegado;
    confere('existe o Monge Renegado, com ~60 de vida e defesa alta (≥ 0,5)', !!def && def.vida >= 55 && def.vida <= 65 && def.ia.defende >= 0.5,
            JSON.stringify(def && { vida: def.vida, defende: def.ia.defende }));
    if (!def) return;

    // Defende MUITO: de 30 socos começados com ele parado de frente, a maioria vira guarda.
    let defendeu = 0;
    for (let semente = 1; semente <= 30; semente++) {
        const m = treino(['long'], semente);
        const r = Motor.colocarInimigo(m, 'renegado', m.jogadores[0].x + 50, m.jogadores[0].y);
        r.ia.pausa = 2; r.virado = -1;
        apertar(m, 'soco');
        rodar(m, 2);
        if (r.estado === 'defendendo') defendeu++;
    }
    confere('defende a maioria dos socos que vê começar (≥ 40% de 30)', defendeu >= 12, `defendeu ${defendeu}/30`);

    // Bloqueou → CONTRA-ATACA na hora, com um golpe rápido próprio, e acerta.
    {
        const m = treino(['long']);
        const j = m.jogadores[0];
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', j.x + 45, j.y));
        const vidaAntes = r.vida;
        apertar(m, 'soco');
        let contra = null, bloqueou = null;
        for (let f = 0; f < 40; f++) {
            rodar(m, 1);
            if (bloqueou == null && r.vida < vidaAntes) bloqueou = f;
            if (contra == null && r.estado === 'atacando') contra = { f, golpe: r.golpeNome };
        }
        confere('o soco no renegado em guarda é bloqueado (≤ 20% do dano)', r.vida < vidaAntes && vidaAntes - r.vida <= Math.max(1, Math.round(j.def.golpes.soco1.dano * 0.2)), `vida ${vidaAntes}→${r.vida}`);
        confere('depois de bloquear, contra-ataca NA HORA (≤ 2 quadros) com golpe próprio', contra && bloqueou != null && contra.f - bloqueou <= 2 && !!def.golpes[contra.golpe] && contra.golpe === def.ia.contraAtaca,
                JSON.stringify({ contra, bloqueou, esperado: def.ia.contraAtaca }));
        confere('o contra-ataque é rápido (liga em ≤ 0,08 s) e acerta o jogador', def.golpes[def.ia.contraAtaca] && def.golpes[def.ia.contraAtaca].inicio <= 0.08 && j.vida < j.vidaMax, `vida do jogador=${j.vida}`);
    }

    // A marca do contra-ataque é da guarda em que ele bloqueou. Se ele sai dela sem responder (no mesmo
    // quadro, o P2 bate pelas costas), a marca não pode sobrar pra próxima guarda: lá ele contra-atacava
    // no primeiro quadro, sem ter bloqueado nada.
    {
        const m = treino(['long', 'shen']);
        const [p1, p2] = m.jogadores;
        p1.x = 300; p1.y = 0.5; p2.x = 400; p2.y = 0.5;
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', 350, 0.5));
        r.ia.congelada = true;
        Motor.aplicarDano(m, r, { dano: 5, origem: p1, corpoACorpo: true });      // de frente: bloqueado
        Motor.aplicarDano(m, r, { dano: 5, origem: p2, corpoACorpo: true });      // pelas costas: inteiro
        const saiu = r.estado;
        rodar(m, 60, () => [entrada(), entrada()]);
        emGuarda(r); r.ia.congelada = false;
        let contraNoVazio = null;
        for (let f = 0; f < 20 && !contraNoVazio; f++) { rodar(m, 1, () => [entrada(), entrada()]); if (r.estado === 'atacando') contraNoVazio = `${r.golpeNome} no quadro ${f}`; }
        confere('saiu da guarda sem responder: na PRÓXIMA guarda, sem bloquear nada, não contra-ataca', saiu !== 'defendendo' && !contraNoVazio,
                `saiu da guarda como ${saiu} · na guarda nova atacou: ${contraNoVazio}`);
    }
    // Contra-ataque só de onde ele alcança: o Puxão do Rio (corpo a corpo, 260 px) bloqueado a ~240 px
    // não arma um contra que sai no vazio.
    if (Motor.PERSONAGENS.lian) {
        const m = treino(['lian']);
        const j = m.jogadores[0]; j.chi = 100;
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', j.x + 240, j.y));
        apertar(m, 'especial');
        let contraDeLonge = null;
        for (let f = 0; f < 30 && !contraDeLonge; f++) { rodar(m, 1); if (r.estado === 'atacando') contraDeLonge = `${r.golpeNome} a ${Math.abs(r.x - j.x).toFixed(0)} px`; }
        confere('puxão bloqueado de longe: ele não contra-ataca de onde o golpe não chega', !contraDeLonge, contraDeLonge);
    }

    // O CHUTE quebra a guarda: atordoado curto, "GUARDA QUEBRADA", e ele não contra-ataca.
    {
        const m = treino(['long']);
        const j = m.jogadores[0];
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', j.x + 55, j.y));
        const eventos = [];
        apertar(m, 'chute'); eventos.push(...m.eventos);
        const estados = new Set();
        for (let f = 0; f < 30; f++) { rodar(m, 1); eventos.push(...m.eventos); estados.add(r.estado); }
        const ev = eventos.find(e => e.tipo === 'guarda-quebrada');
        confere('o chute quebra a guarda do renegado: fica atordoado', estados.has('atordoado') && !estados.has('atacando'), [...estados].join(','));
        confere('com o texto GUARDA QUEBRADA e o evento guarda-quebrada (id do inimigo)', eventos.some(e => e.tipo === 'texto' && e.texto === 'GUARDA QUEBRADA') && ev && ev.id === 'renegado',
                JSON.stringify(ev));
        let dur = 0;
        const m2 = treino(['long']);
        const r2 = emGuarda(Motor.colocarInimigo(m2, 'renegado', m2.jogadores[0].x + 55, m2.jogadores[0].y));
        apertar(m2, 'chute');
        for (let f = 0; f < 180; f++) { rodar(m2, 1); if (r2.estado === 'atordoado') dur += DT; }
        confere('o atordoado da guarda quebrada é curto (entre 0,3 e 1,2 s)', dur >= 0.3 && dur <= 1.2, `${dur.toFixed(2)} s`);
        // Vida cheia: agarrar o de guarda quebrada é agarrão comum, não finalização.
        const m3 = treino(['long']);
        const j3 = m3.jogadores[0];
        const r3 = emGuarda(Motor.colocarInimigo(m3, 'renegado', j3.x + 55, j3.y));
        apertar(m3, 'chute');
        let tonto = false;
        for (let f = 0; f < 40 && !tonto; f++) { rodar(m3, 1); tonto = r3.estado === 'atordoado' && j3.estado !== 'atacando'; }
        j3.x = r3.x - 40; j3.virado = 1;
        apertar(m3, 'agarrar');
        confere('guarda quebrada com vida cheia NÃO finaliza (agarrão comum)', tonto && j3.estado === 'agarrando' && r3.estado === 'agarrado', `tonto=${tonto} jogador=${j3.estado} renegado=${r3.estado}`);
    }
    // A quebra passa SÓ o dano de defesa (20% do chute) — nem zero, nem o chute inteiro. E só o CHUTE
    // quebra: a investida (correr + chute) e o chute no ar são bloqueados como qualquer golpe.
    function golpeNaGuarda(comoBater) {
        const m = treino(['long']);
        const j = m.jogadores[0];
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', j.x + 55, j.y));
        r.ia.congelada = true;                    // fica em guarda: o que se mede é o golpe, não a resposta
        const vida = r.vida, eventos = [], golpes = new Set();
        comoBater(m, j);
        for (let f = 0; f < 40; f++) { rodar(m, 1); eventos.push(...m.eventos); if (j.golpeNome) golpes.add(j.golpeNome); }
        return { perdeu: vida - r.vida, quebrou: eventos.some(e => e.tipo === 'guarda-quebrada'), golpes: [...golpes].join(',') };
    }
    {
        const chute = golpeNaGuarda(m => apertar(m, 'chute'));
        const esperado = Math.max(1, Math.round(Motor.PERSONAGENS.long.golpes.chute.dano * 0.2));
        confere('a guarda quebrada passa só o dano de defesa (20% do chute)', chute.quebrou && chute.perdeu === esperado, `quebrou=${chute.quebrou} · perdeu ${chute.perdeu}, esperado ${esperado}`);
        const investida = golpeNaGuarda(m => { const e = entrada({ direita: true, correr: true }); e.apertou.chute = true; e.chute = true; Motor.passo(m, DT, [e]); });
        confere('a investida (correr + chute) no renegado em guarda é bloqueada, não quebra', investida.golpes.includes('investida') && investida.perdeu > 0 && !investida.quebrou,
                JSON.stringify(investida));
        const aereo = golpeNaGuarda(m => { apertar(m, 'pular'); rodar(m, 2); apertar(m, 'chute'); });
        confere('o chute no ar no renegado em guarda é bloqueado, não quebra', aereo.golpes.includes('chuteAereo') && aereo.perdeu > 0 && !aereo.quebrou, JSON.stringify(aereo));
    }
    // Qualquer inimigo que está defendendo: a Sombra também.
    {
        const m = treino(['long']);
        const s = emGuarda(Motor.colocarInimigo(m, 'sombra', m.jogadores[0].x + 55, m.jogadores[0].y));
        apertar(m, 'chute');
        const estados = new Set();
        for (let f = 0; f < 30; f++) { rodar(m, 1); estados.add(s.estado); }
        confere('o chute quebra a guarda de QUALQUER inimigo (Sombra em guarda)', estados.has('atordoado'), [...estados].join(','));
    }
    // E o agarrão continua funcionando em quem defende.
    {
        const m = treino(['long']);
        const j = m.jogadores[0];
        const r = emGuarda(Motor.colocarInimigo(m, 'renegado', j.x + 40, j.y));
        apertar(m, 'agarrar');
        confere('agarrar quem está em guarda funciona', j.estado === 'agarrando' && r.estado === 'agarrado', `jogador=${j.estado} renegado=${r.estado}`);
    }
});

// ── 5. CHEFES EM DUAS FASES ───────────────────────────────────────────────────────────────
// Ao cruzar 50% da vida, uma vez só: FÚRIA (~1 s, invulnerável, onda de choque que empurra quem está
// perto, tremor) e, depois, os parâmetros da fase 2. Quem cai de acima de 50% direto a zero morre
// sem fúria.
function furia(tipo) {
    const mundo = treino(['long', 'shen']);
    const [j, p2] = mundo.jogadores;
    const c = boneco(mundo, tipo, 50, { virado: -1 });
    p2.x = c.x + 420; p2.y = c.y;
    const iaAntes = JSON.parse(JSON.stringify(c.def.ia)), defAntes = { velocidade: c.def.velocidade, teleporta: c.def.teleporta };
    c.vida = Math.floor(c.vidaMax * 0.5) + 3;
    const eventos = [];
    apertar(mundo, 'soco'); eventos.push(...mundo.eventos);
    let entrou = null;
    for (let f = 0; f < 12 && entrou == null; f++) { rodar(mundo, 1); eventos.push(...mundo.eventos); if (c.estado === 'furia') entrou = f; }
    const vidaNaFuria = c.vida;
    const invul = Motor.aplicarDano(mundo, c, { dano: 20, origem: j });
    const vidaDepoisDoGolpe = c.vida;
    const xJ = j.x, x2 = p2.x;
    let duracao = 0, estadosJ = new Set();
    for (let f = 0; f < 150; f++) { rodar(mundo, 1); eventos.push(...mundo.eventos); if (c.estado === 'furia') duracao += DT; estadosJ.add(j.estado); }
    return { mundo, j, p2, c, eventos, entrou, vidaNaFuria, invul, vidaDepoisDoGolpe, xJ, x2, duracao, estadosJ, iaAntes, defAntes };
}
bloco('chefes em duas fases', () => {
    for (const tipo of CHEFES) {
        const def = Motor.INIMIGOS[tipo];
        confere(`${tipo}: a definição tem fase2`, !!(def && def.fase2), JSON.stringify(def && def.fase2));
        const r = furia(tipo);
        const textosVistos = r.eventos.filter(e => e.tipo === 'texto').map(e => e.texto);
        confere(`${tipo}: cruzar 50% com um soco entra em FÚRIA`, r.entrou != null && textosVistos.includes('FÚRIA') && r.eventos.some(e => e.tipo === 'tremor'),
                `entrou=${r.entrou} textos=${textosVistos.join('|')} vida=${r.c.vida}/${r.c.vidaMax}`);
        confere(`${tipo}: a fúria dura ~1 s`, r.duracao >= 0.8 && r.duracao <= 1.3, `${r.duracao.toFixed(2)} s`);
        confere(`${tipo}: na fúria é invulnerável`, r.vidaDepoisDoGolpe === r.vidaNaFuria, `vida ${r.vidaNaFuria}→${r.vidaDepoisDoGolpe}`);
        confere(`${tipo}: a onda de choque empurra quem está perto`, Math.abs(r.j.x - r.c.x) > 110 && Math.abs(r.j.x - r.xJ) > 40, `jogador ${r.xJ.toFixed(0)}→${r.j.x.toFixed(0)} chefe ${r.c.x.toFixed(0)} estados=${[...r.estadosJ].join(',')}`);
        confere(`${tipo}: e empurra SEM dano (é espaço, não castigo)`, r.j.vida === r.j.vidaMax, `vida do empurrado ${r.j.vida}/${r.j.vidaMax}`);
        // Perto em x mas em OUTRA faixa de profundidade (além de FURIA.profundidade): a onda não pega.
        {
            const mo = treino(['long', 'shen']);
            const po = mo.jogadores[1];
            const c = boneco(mo, tipo, 50, { virado: -1 });
            po.x = c.x + 60; po.y = c.y + 0.45 <= 1 ? c.y + 0.45 : c.y - 0.45;
            const xAntes = po.x, estados = new Set();
            c.vida = Math.floor(c.vidaMax * 0.5) + 3;
            apertar(mo, 'soco');
            for (let f = 0; f < 90; f++) { rodar(mo, 1); estados.add(po.estado); }
            confere(`${tipo}: a onda não pega quem está perto em x mas em outra faixa`, c.furiaFeita && Math.abs(po.x - xAntes) < 1 && !estados.has('lancado'),
                    `fúria=${!!c.furiaFeita} · P2 ${xAntes.toFixed(0)}→${po.x.toFixed(0)} (dy ${Math.abs(po.y - c.y).toFixed(2)}) estados=${[...estados].join(',')}`);
        }
        confere(`${tipo}: e não mexe em quem está longe`, Math.abs(r.p2.x - r.x2) < 1 && r.p2.vida === r.p2.vidaMax, `p2 ${r.x2.toFixed(0)}→${r.p2.x.toFixed(0)}`);
        confere(`${tipo}: depois da fúria está na fase 2`, r.c.estado !== 'furia' && r.c.fase === 2 && r.c.vida === r.vidaNaFuria, `estado=${r.c.estado} fase=${r.c.fase}`);
        // Uma vez só: bater de novo até 30% não repete a fúria.
        const antes = r.eventos.length;
        const vistos = [];
        for (let k = 0; k < 6 && r.c.vida > r.c.vidaMax * 0.3; k++) { Motor.aplicarDano(r.mundo, r.c, { dano: Math.ceil(r.c.vidaMax * 0.04), origem: r.p2 }); vistos.push(...textos(r.mundo)); rodar(r.mundo, 40); }
        confere(`${tipo}: a fúria vem uma vez só`, !vistos.includes('FÚRIA') && r.c.estado !== 'furia', `textos=${vistos.join('|')} estado=${r.c.estado}`);
        void antes;

        // Cruzar 50% com um golpe DEFENDIDO (o dano de defesa) também é fúria. Sem esse ramo, a fase 2 se
        // perdia de vez: os golpes seguintes já partem de ≤ 50% e nunca mais "cruzam".
        {
            const mg = treino(['long']);
            const jg = mg.jogadores[0];
            const c = emGuarda(boneco(mg, tipo, 50));
            c.vida = Math.floor(c.vidaMax * 0.5) + 1;
            Motor.aplicarDano(mg, c, { dano: 10, origem: jg, corpoACorpo: true });
            const estadoLogo = c.estado, txt = textos(mg);
            rodar(mg, 72);
            confere(`${tipo}: cruzar 50% com golpe defendido também entra em FÚRIA e vai pra fase 2`, estadoLogo === 'furia' && txt.includes('FÚRIA') && c.fase === 2,
                    `logo depois: ${estadoLogo} · textos ${txt.join('|')} · 1,2 s depois: fase ${c.fase} · vida ${c.vida}/${c.vidaMax}`);
        }

        // De cima de 50% direto a zero: morre sem fúria.
        const m = treino(['long']);
        const c2 = boneco(m, tipo, 50, { virado: -1 });
        c2.vida = Math.round(c2.vidaMax * 0.6);
        Motor.aplicarDano(m, c2, { dano: 99999, origem: m.jogadores[0] });
        const tx = textos(m);
        const depois = rodarAnotando(m, 5);
        confere(`${tipo}: caiu de 60% a zero num golpe, morre sem fúria`, c2.estado === 'morto' && !tx.includes('FÚRIA') && !depois.includes('FÚRIA'), `estado=${c2.estado} textos=${tx.concat(depois).join('|')}`);
        // Sem cruzar (60% → 55%) não tem fúria.
        const m3 = treino(['long']);
        const c3 = boneco(m3, tipo, 50, { virado: -1 });
        c3.vida = Math.round(c3.vidaMax * 0.6);
        Motor.aplicarDano(m3, c3, { dano: Math.round(c3.vidaMax * 0.05), origem: m3.jogadores[0] });
        confere(`${tipo}: acima de 50% não tem fúria`, c3.estado !== 'furia' && !textos(m3).includes('FÚRIA'), `estado=${c3.estado}`);

        // Os parâmetros da fase 2, um por chefe.
        const d = r.c.def, ia = d.ia, iaA = r.iaAntes;
        const parte = (pesos, n) => pesos[n] / Object.values(pesos).reduce((s, v) => s + v, 0);
        let ok = false, detalhe = '';
        if (tipo === 'mestreSombra') {
            ok = d.teleporta === 2 && r.defAntes.teleporta === 3 && ia.arremessa > iaA.arremessa && ia.pausa[1] < iaA.pausa[1] && Math.abs(d.velocidade - r.defAntes.velocidade * 1.2) < 1;
            detalhe = `teleporta ${r.defAntes.teleporta}→${d.teleporta} · arremessa ${iaA.arremessa}→${ia.arremessa} · pausa ${iaA.pausa}→${ia.pausa} · velocidade ${r.defAntes.velocidade}→${d.velocidade}`;
        } else if (tipo === 'graoPresa') {
            ok = ia.investe >= iaA.investe * 1.5 && parte(ia.pesos, 'pancada') > parte(iaA.pesos, 'pancada');
            detalhe = `investe ${iaA.investe}→${ia.investe} · pancada ${parte(iaA.pesos, 'pancada')}→${parte(ia.pesos, 'pancada')}`;
        } else if (tipo === 'gigante') {
            ok = parte(ia.pesos, 'pancada') > parte(iaA.pesos, 'pancada') && Math.abs(d.velocidade - r.defAntes.velocidade * 1.3) < 1;
            detalhe = `pancada ${parte(iaA.pesos, 'pancada')}→${parte(ia.pesos, 'pancada')} · velocidade ${r.defAntes.velocidade}→${d.velocidade}`;
        } else if (tipo === 'feiticeiro') {
            ok = d.teleporta === 1 && r.defAntes.teleporta === 2;
            detalhe = `teleporta ${r.defAntes.teleporta}→${d.teleporta}`;
        }
        confere(`${tipo}: a fase 2 muda os parâmetros como pedido`, ok, detalhe);

        // O `ia` da fase 2 é MISTURADO com o da fase 1, não trocado: o que a fase 2 não muda continua lá.
        // Trocado, o chefe perdia `alcance` (NaN) e parava de bater — e nenhum check olhava.
        const f2ia = (def.fase2 && def.fase2.ia) || {};
        const sumiram = Object.keys(iaA).filter(k => !(k in f2ia) && JSON.stringify(ia[k]) !== JSON.stringify(iaA[k]));
        confere(`${tipo}: na fase 2, a IA guarda o que a fase 2 não muda (alcance, agressividade, pausa, pesos…)`, sumiram.length === 0,
                sumiram.map(k => `${k}: ${JSON.stringify(iaA[k])}→${JSON.stringify(ia[k])}`).join(' · '));
        // E luta de verdade na fase 2: IA solta, jogador por perto, 10 s.
        {
            const mf = treino(['long']);
            const jf = mf.jogadores[0];
            const c = boneco(mf, tipo, 80, { virado: -1 });
            c.vida = Math.floor(c.vidaMax * 0.5) + 3;
            Motor.aplicarDano(mf, c, { dano: 5, origem: jf });
            rodar(mf, 90);
            c.ia.congelada = false;
            jf.vidaMax = jf.vida = 1e6;
            let golpes = 0, antesF = c.estado;
            for (let q = 0; q < 600; q++) { rodar(mf, 1); if (c.estado === 'atacando' && antesF !== 'atacando') golpes++; antesF = c.estado; }
            confere(`${tipo}: na fase 2 continua atacando (≥ 1 golpe começado em 10 s)`, c.fase === 2 && golpes >= 1, `fase=${c.fase} golpes=${golpes}`);
        }
    }

    // O Feiticeiro na fase 2 solta TRÊS caveiras, em três faixas de profundidade; na fase 1, uma.
    function caveiras(naFase2) {
        const m = treino(['long']);
        const f = boneco(m, 'feiticeiro', 300, { virado: -1 });
        if (naFase2) { f.vida = Math.floor(f.vidaMax * 0.5) + 2; Motor.aplicarDano(m, f, { dano: 5, origem: m.jogadores[0] }); rodar(m, 90); }
        m.jogadores[0].x -= 400;
        Motor.iniciarGolpe(f, 'caveira');
        let maximo = [];
        for (let k = 0; k < 40; k++) { rodar(m, 1); if (m.projeteis.length > maximo.length) maximo = m.projeteis.map(p => p.y); }
        return { fase: f.fase, ys: maximo };
    }
    const f1 = caveiras(false), f2 = caveiras(true);
    confere('Feiticeiro na fase 1: uma caveira', f1.ys.length === 1, JSON.stringify(f1));
    const ys = f2.ys.slice().sort((a, b) => a - b);
    confere('Feiticeiro na fase 2: três caveiras em três faixas (separadas por mais que a tolerância de linha)',
            f2.fase === 2 && ys.length === 3 && ys[1] - ys[0] > 2 * Motor.TOLERANCIA_Y && ys[2] - ys[1] > 2 * Motor.TOLERANCIA_Y && ys.every(y => y >= 0 && y <= 1), JSON.stringify(f2));
});

// ── 6. AS FASES 2 A 4 TÊM OS INIMIGOS NOVOS ───────────────────────────────────────────────
// Quantos inimigos cada onda tinha antes dos novos: nenhuma onda pode crescer mais de 1.
bloco('fases', () => {
    const ANTES = { 1: [2, 3, 3, 2], 2: [2, 3, 3, 3, 1], 3: [4, 2, 4, 2, 2], 4: [4, 3, 3, 0] };
    for (const f of [2, 3, 4]) {
        const tipos = Motor.FASES[f].ondas.flatMap(o => o.inimigos.map(([t]) => t));
        confere(`fase ${f}: tem lanceiro e renegado nas ondas`, tipos.includes('lanceiro') && tipos.includes('renegado'), tipos.join(','));
    }
    for (const f of [1, 2, 3, 4]) {
        const ondas = Motor.FASES[f].ondas;
        const n = ondas.map(o => o.inimigos.reduce((s, [, k]) => s + k, 0));
        confere(`fase ${f}: mesmas ondas, nenhuma com mais de 1 inimigo a mais que antes`, n.length === ANTES[f].length && n.every((k, i) => k <= ANTES[f][i] + 1), `antes ${ANTES[f].join(',')} · agora ${n.join(',')}`);
        confere(`fase ${f}: o chefe continua na última onda`, !!ondas[ondas.length - 1].chefe, JSON.stringify(ondas[ondas.length - 1]));
    }
});

// ── 7. O DEFEITO: INIMIGO FORA DA TELA TRAVADA ────────────────────────────────────────────
// Reprodução só com o motor (fase 3, fácil, semente 1, onda do chefe): o jogador segura → e fica
// na borda direita da tela travada; o Gigante vem pela direita. Antes, ele batia de até 183 px, 3 dos
// golpes com ele FORA da tela, e o soco do Long chega a ~94 px.
function arena(m) {
    return { esq: Math.max(20, m.travaX + 24), dir: Math.min(m.faseDef.comprimento - 20, m.travaX + Motor.LARGURA - 24) };
}
bloco('inimigo fora da tela', () => {
    const m = Motor.criarMundo({ fase: 3, semente: 1, dificuldade: 'facil' });
    const j = m.jogadores[0];
    m.onda = 4; j.x = 3410; j.y = 0.5;
    const direita = () => [entrada({ direita: true })];
    Motor.passo(m, DT, direita());
    confere('(cenário) a onda do Gigante disparou e travou a tela', m.travado && m.inimigos.some(i => i.tipo === 'gigante'), `travado=${m.travado}`);
    for (const i of m.inimigos) if (i.tipo !== 'gigante') { i.ia.congelada = true; i.x = m.travaX - 200; }
    const g = m.inimigos.find(i => i.tipo === 'gigante');
    g.ia.congelada = true;
    rodar(m, 180, direita);
    g.ia.congelada = false; g.ia.lado = 1; g.x = j.x + 300;
    const { esq, dir } = arena(m);
    let golpesDeFora = [], acertosDeFora = 0, acertos = 0, entrou = false, saiu = [];
    let antes = g.estado;
    for (let q = 0; q < 60 * 20; q++) {
        const vida = j.vida;
        Motor.passo(m, DT, direita());
        if (g.estado === 'atacando' && antes !== 'atacando' && (g.x < esq - 0.5 || g.x > dir + 0.5)) golpesDeFora.push(Math.round(g.x - m.travaX));
        antes = g.estado;
        if (j.vida < vida) { acertos++; if (g.x < m.travaX || g.x > m.travaX + Motor.LARGURA) acertosDeFora++; }
        if (g.x >= m.travaX && g.x <= m.travaX + Motor.LARGURA) entrou = true;
        else if (entrou) saiu.push(Math.round(g.x - m.travaX));
        if (j.estado === 'morto') break;
    }
    confere('o Gigante não começa golpe de onde o jogador não chega (fora de travaX+24 … travaX+936)', golpesDeFora.length === 0, `golpes começados em x-travaX = ${golpesDeFora.slice(0, 8).join(',')}`);
    confere('e não acerta ninguém estando fora da tela', acertosDeFora === 0, `${acertosDeFora} de ${acertos} acertos com ele fora da tela`);
    confere('o Gigante entra na tela e, depois de entrar, não sai mais dela', entrou && saiu.length === 0, `entrou=${entrou} saiu em x-travaX = ${saiu.slice(0, 6).join(',')}`);
    confere('(controle) o Gigante ainda luta: acertou o jogador', acertos > 0, `acertos=${acertos}`);

    // O Arqueiro colado no jogador, perto da borda direita: ele quer 240 px de distância e só tem
    // tela pra fora. Antes, fugia pra fora da tela e atirava de lá; agora fica e luta dentro.
    const ma = Motor.criarMundo({ fase: 2, semente: 1, dificuldade: 'facil' });
    const ja = ma.jogadores[0];
    ja.x = ma.faseDef.ondas[1].x + 10;
    Motor.passo(ma, DT, [entrada()]);
    confere('(cenário) a onda 2 da fase 2 travou a tela', ma.travado, `travado=${ma.travado}`);
    ma.inimigos.length = 0;
    const arq = Motor.colocarInimigo(ma, 'arqueiro', ma.travaX + 700, ja.y);
    arq.ia.congelada = true;
    rodar(ma, 90);                                    // a câmera assenta na trava
    ja.x = ma.travaX + 860; arq.x = ma.travaX + 900; arq.ia.congelada = false;
    const ar = arena(ma);
    let foraMax = 0, flechasDeFora = 0, flechas = 0;
    for (let q = 0; q < 60 * 12; q++) {
        Motor.passo(ma, DT, [entrada()]);
        foraMax = Math.max(foraMax, arq.x - ar.dir, ar.esq - arq.x);
        for (const ev of ma.eventos) if (ev.tipo === 'projetil' && ev.nome === 'flecha') { flechas++; if (ev.x < ar.esq - 0.5 || ev.x > ar.dir + 0.5) flechasDeFora++; }
        if (!Motor.vivo(arq)) break;
    }
    confere('o Arqueiro acuado não sai do chão alcançável (nem 1 px além da borda)', foraMax <= 0.5, `passou ${foraMax.toFixed(0)} px da borda`);
    confere('e nenhuma flecha sai de fora dele', flechasDeFora === 0, `${flechasDeFora} de ${flechas} flechas de fora`);
    // "Fica e luta dentro": sem isso, os dois checks acima passam com 0 de 0 — o Arqueiro empurrando a
    // parede sem parar, sem atirar nada.
    confere('e, acuado, luta de dentro: atira flecha (≥ 1 em 12 s)', flechas >= 1, `${flechas} flechas`);

    // Investida (Garra, Grão-Presa) e arremesso (shuriken do Mestre Sombra, caveira do Feiticeiro) também
    // esperam entrar: de fora, a 295 px do jogador e alinhado — distância das duas —, nenhum começa.
    // O Gigante acima não investe nem arremessa; sem isto, a trava delas só era vista por acaso do bot.
    const deForaLonge = {};
    for (const tipo of ['garra', 'graoPresa', 'mestreSombra', 'feiticeiro']) {
        deForaLonge[tipo] = [];
        for (let semente = 1; semente <= 20; semente++) {
            const mf = Motor.criarMundo({ fase: 2, semente, dificuldade: 'normal' });
            const jf = mf.jogadores[0];
            jf.x = mf.faseDef.ondas[1].x + 10;
            Motor.passo(mf, DT, [entrada()]);
            mf.inimigos.length = 0;
            rodar(mf, 90);                                // a câmera assenta na trava
            jf.x = mf.travaX + 900; jf.y = 0.5; jf.vidaMax = jf.vida = 1e6;
            const i = Motor.colocarInimigo(mf, tipo, mf.travaX + 1195, 0.5);
            i.ia.pausa = 0;
            let antesI = i.estado;
            for (let q = 0; q < 60 * 4 && !i.entrouNaTela; q++) {
                rodar(mf, 1);
                if (i.estado === 'atacando' && antesI !== 'atacando' && !i.entrouNaTela) deForaLonge[tipo].push(`${i.golpeNome}@${Math.round(i.x - mf.travaX)}`);
                antesI = i.estado;
            }
        }
    }
    const lista = Object.entries(deForaLonge).filter(([, v]) => v.length).map(([t, v]) => `${t}: ${v.slice(0, 3).join(',')} (${v.length})`);
    confere('de fora da tela travada, ninguém começa investida nem arremesso (20 sementes)', lista.length === 0, lista.join(' · '));

    // Quem nasce fora continua ENTRANDO ANDANDO: nada de teleporte pra dentro.
    const mn = Motor.criarMundo({ fase: 3, semente: 2, dificuldade: 'normal' });
    const jn = mn.jogadores[0];
    jn.x = mn.faseDef.ondas[0].x + 5;
    Motor.passo(mn, DT, [entrada()]);
    const posicoes = new Map(mn.inimigos.map(i => [i.id, i.x]));
    const nasceramFora = mn.inimigos.filter(i => i.x < mn.travaX || i.x > mn.travaX + Motor.LARGURA).length;
    let salto = 0, quem = '';
    for (let q = 0; q < 60 * 2; q++) {
        Motor.passo(mn, DT, [entrada()]);
        for (const i of mn.inimigos) {
            const d = Math.abs(i.x - posicoes.get(i.id)) - i.def.velocidade * DT;
            if (d > salto) { salto = d; quem = `${i.tipo} ${posicoes.get(i.id).toFixed(0)}→${i.x.toFixed(0)}`; }
            posicoes.set(i.id, i.x);
        }
    }
    confere('quem nasce fora da tela entra andando, sem salto (≤ velocidade × quadro)', nasceramFora > 0 && salto <= 0.01, `nasceram fora=${nasceramFora} · maior salto além da velocidade=${salto.toFixed(2)} (${quem})`);

    // Quem ainda está FORA não pode estacionar na borda. O destino da aproximação é preso ao chão do
    // jogador, e o passo só anda com mais de 6 px de sobra: com o destino a menos de 6 px da borda, quem
    // vinha de fora parava entre `dir` e `dir + 6` — sem entrar (então sem atacar) — e a onda não
    // acabava enquanto o jogador ficasse parado ali.
    const presos = [];
    for (const tipo of ['sombra', 'garra', 'bruto', 'renegado', 'gigante', 'graoPresa']) {
        const mb = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 3 });
        mb.travado = true; mb.travaX = 0; mb.camera.x = 0;
        const { dir: bordaDir } = arena(mb);
        const i = Motor.colocarInimigo(mb, tipo, bordaDir + 4, 0.5);
        const jb = mb.jogadores[0];
        jb.x = bordaDir - (i.def.ia.alcance * i.escala - 6) - 2; jb.y = 0.5; jb.vidaMax = jb.vida = 1e6;
        i.ia.lado = 1; i.ia.pausa = 0;
        rodar(mb, 600);
        if (!i.entrouNaTela) presos.push(`${tipo} parado em x-dir=${(i.x - bordaDir).toFixed(1)} (${i.estado})`);
    }
    confere('quem vem de fora e mira perto da borda ENTRA (não fica estacionado a poucos px fora)', presos.length === 0, presos.join(' · '));

    // A mesma regra dos dois lados: se o inimigo que ainda não entrou não bate, também não apanha. Senão
    // a borda vira PONTO SEGURO — o jogador parado nela, virado pra fora e socando, acertava quem vinha
    // de fora, cada acerto o empurrava de volta, ele nunca entrava e nunca batia (o Gigante: 0 de dano).
    const seguros = [];
    for (const tipo of ['gigante', 'graoPresa', 'bruto']) for (const semente of [1, 2, 3]) {
        const mb = Motor.criarMundo({ fase: 2, jogadores: ['long'], semente, dificuldade: 'normal' });
        mb.travado = true; mb.travaX = 1000; mb.camera.x = 1000;
        const { dir: bordaDir } = arena(mb);
        const jb = mb.jogadores[0];
        jb.x = bordaDir; jb.y = 0.5; jb.virado = 1; jb.vidaMax = jb.vida = 1e6;
        const i = Motor.colocarInimigo(mb, tipo, bordaDir + 200, 0.5);
        i.ia.lado = -1;
        rodar(mb, 60 * 20, q => { const e = entrada(); e.apertou.soco = q % 10 === 0; e.soco = e.apertou.soco; return [e]; });
        if (!(jb.danoLevado > 0)) seguros.push(`${tipo} s${semente}: dano 0, entrou=${!!i.entrouNaTela}, x-dir=${(i.x - bordaDir).toFixed(0)}`);
    }
    confere('parado na borda, virado pra fora e socando, o jogador NÃO fica imune (Gigante, Grão-Presa, Bruto)', seguros.length === 0, seguros.join(' · '));

    // Em lutas de verdade (o bot do simulador, fases 2 a 4): nenhum inimigo começa golpe de fora do
    // chão onde o jogador pode ficar.
    const deFora = [];
    for (const fase of [2, 3, 4]) {
        const mb = Motor.criarMundo({ fase, jogadores: ['long'], semente: 1, dificuldade: 'facil' });
        const bot = Bot.criarBot({ habilidade: 'bom', semente: 1 });
        const estadoAntes = new Map();
        for (let q = 0; q < 60 * 300 && !mb.concluida && !mb.fimDeJogo; q++) {
            Motor.passo(mb, DT, [bot.decidir(mb)]);
            if (!mb.travado) continue;
            const a = arena(mb);
            for (const i of mb.inimigos) {
                if (i.estado === 'atacando' && estadoAntes.get(i.id) !== 'atacando' && (i.x < a.esq - 0.5 || i.x > a.dir + 0.5)) deFora.push(`fase ${fase} ${i.tipo} ${i.golpeNome} em x-travaX=${Math.round(i.x - mb.travaX)}`);
                estadoAntes.set(i.id, i.estado);
            }
        }
    }
    confere('em lutas de verdade (bot bom, fases 2–4) ninguém começa golpe de fora do chão alcançável', deFora.length === 0, deFora.slice(0, 5).join(' · ') + (deFora.length > 5 ? ` … (${deFora.length})` : ''));
});

// ── 8. A TELA DE ESCOLHA COM N MONGES E O DESENHO DO NOVO ─────────────────────────────────
// `figura.js` e `desenho.js` num contexto `vm`, com o Motor de verdade, o cenário desligado e um
// `ctx` que anota os retângulos e os pontos das linhas.
bloco('tela de escolha e figura', () => {
    const nada = () => {};
    const janela = { PunhosDeShaolin: { Motor, Cenario: new Proxy({}, { get: () => nada }) } };
    const contexto = vm.createContext({ window: janela, Math, console });
    for (const f of ['figura.js', 'desenho.js']) vm.runInContext(fs.readFileSync(path.join(raiz, f), 'utf8'), contexto, { filename: f });
    const { Desenho: D, Figura: F } = janela.PunhosDeShaolin;
    function ctxQueAnota() {
        const anotado = { rects: [], pontos: [] };
        const ctx = new Proxy({}, {
            get(t, k) {
                if (k in t) return t[k];
                if (k === '__anotado') return anotado;
                if (k === 'rect') return (x, y, w, h) => anotado.rects.push({ x, y, w, h });
                if (k === 'moveTo' || k === 'lineTo') return (x, y) => anotado.pontos.push([x, y]);
                if (k === 'measureText') return () => ({ width: 10 });
                if (/Gradient|Pattern/.test(k)) return () => ({ addColorStop: nada });
                return nada;
            },
            set(t, k, v) { t[k] = v; return true; },
        });
        return ctx;
    }
    const ids = Object.keys(Motor.PERSONAGENS);
    confere('são três monges na escolha', ids.length === 3, ids.join(','));
    confere('o desenho exporta colunaDaSelecao (a mesma conta pro toque)', typeof D.colunaDaSelecao === 'function', Object.keys(D).join(','));
    const ctx = ctxQueAnota();
    D.desenharSelecao(ctx, 0, { particulas: [] }, { p1: 2, p2: 0, p2Entrou: true, confirmadoP1: false, confirmadoP2: false, toque: false });
    const cartoes = ctx.__anotado.rects.filter(r => r.h > 300).sort((a, b) => a.x - b.x);
    const cabem = cartoes.every(c => c.x >= 0 && c.x + c.w <= Motor.LARGURA) && cartoes.every((c, k) => k === 0 || cartoes[k - 1].x + cartoes[k - 1].w <= c.x);
    confere('a escolha pinta um cartão por monge, lado a lado, dentro da tela', cartoes.length === ids.length && cabem, JSON.stringify(cartoes));
    if (typeof D.colunaDaSelecao === 'function' && cartoes.length === ids.length) {
        const erradas = [];
        cartoes.forEach((c, k) => { for (let x = Math.ceil(c.x); x < c.x + c.w; x += 3) if (D.colunaDaSelecao(x, ids.length) !== k) erradas.push(`x=${x} (cartão ${k}) → ${D.colunaDaSelecao(x, ids.length)}`); });
        confere('tocar em qualquer ponto de um cartão escolhe aquele monge', erradas.length === 0, erradas.slice(0, 5).join(' · '));
        const todas = Array.from({ length: Motor.LARGURA }, (_, x) => D.colunaDaSelecao(x, ids.length));
        confere('toque fora dos cartões cai no mais perto (sempre um monge válido)', todas.every(k => Number.isInteger(k) && k >= 0 && k < ids.length), [...new Set(todas)].join(','));
        confere('com dois monges, a conta continua a de antes (metade esquerda, metade direita)', D.colunaDaSelecao(100, 2) === 0 && D.colunaDaSelecao(479, 2) === 0 && D.colunaDaSelecao(481, 2) === 1 && D.colunaDaSelecao(900, 2) === 1,
                [100, 479, 481, 900].map(x => D.colunaDaSelecao(x, 2)).join(','));
    }

    // O principal.js (laço do navegador): toque por coluna e "P2 pega o outro" = (p1 + 1) % N.
    const principal = fs.readFileSync(path.join(raiz, 'principal.js'), 'utf8');
    confere('principal.js: nada de "p2 = 1 - p1" (só servia pra dois monges)', !/p2\s*=\s*1\s*-/.test(principal), (principal.match(/.*p2\s*=\s*1\s*-.*/g) || []).join(' | '));
    const outro = principal.match(/p2\s*=\s*\(\s*(?:s|jogo\.sel)\.p1\s*\+\s*1\s*\)\s*%\s*IDS\.length/g) || [];
    confere('principal.js: o P2 pega (p1 + 1) % N na escolha E quando entra no meio da luta', outro.length >= 2, `achei ${outro.length}`);
    confere('principal.js: o toque na escolha usa a coluna do desenho', /Desenho\.colunaDaSelecao\(/.test(principal), 'não usa');
    // ← anda pra TRÁS e → pra frente, pros dois jogadores (com dois monges dava no mesmo; com três, não).
    for (const p of ['p1', 'p2']) {
        const esq = new RegExp(`apertou\\.esquerda\\)\\s*\\{\\s*s\\.${p}\\s*=\\s*\\(s\\.${p}\\s*\\+\\s*IDS\\.length\\s*-\\s*1\\)\\s*%\\s*IDS\\.length`);
        const dir = new RegExp(`apertou\\.direita\\)\\s*\\{\\s*s\\.${p}\\s*=\\s*\\(s\\.${p}\\s*\\+\\s*1\\)\\s*%\\s*IDS\\.length`);
        confere(`principal.js: na escolha do ${p.toUpperCase()}, ← volta (${p} + N - 1) % N e → avança (${p} + 1) % N`, esq.test(principal) && dir.test(principal),
                (principal.match(new RegExp(`.*apertou\\.(esquerda|direita)\\)\\s*\\{\\s*s\\.${p}.*`, 'g')) || []).map(l => l.trim()).join(' | '));
    }

    // Estilos e desenho dos novos: nada estoura, e a corrente/lança chegam até o alcance do golpe.
    for (const id of ['lian', 'lanceiro', 'renegado']) confere(`figura: existe o estilo de ${id}`, !!F.ESTILOS[id], Object.keys(F.ESTILOS).join(','));
    function desenharEm(defId, estado, golpeNome, quadro) {
        const def = Motor.PERSONAGENS[defId] || Motor.INIMIGOS[defId];
        const ent = { id: 1, def, estado, quadro: quadro || 0, virado: 1, escala: def.escala || 1, vz: 0, z: 0, vx: 0 };
        if (golpeNome) { ent.golpeNome = golpeNome; ent.golpe = def.golpes[golpeNome]; }
        const c = ctxQueAnota();
        F.desenhar(c, ent, 1.3, { virado: 1 });
        return Math.max(...c.__anotado.pontos.map(p => p[0]));
    }
    if (Motor.PERSONAGENS.lian && Motor.INIMIGOS.lanceiro && Motor.INIMIGOS.renegado) {
        const lian = Motor.PERSONAGENS.lian.golpes;
        const parada = desenharEm('lian', 'parado');
        const soco = desenharEm('lian', 'atacando', 'soco1', lian.soco1.inicio + 0.01);
        const puxao = desenharEm('lian', 'atacando', 'especial', lian.especial.inicio + 0.01);
        confere('a corrente da Lian chega ao alcance do soco quando ele está ligado (e não quando ela está parada)', soco >= lian.soco1.alcance * 0.85 / Motor.PERSONAGENS.lian.escala && parada < 70,
                `parada ${parada.toFixed(0)} · soco ${soco.toFixed(0)} (alcance ${lian.soco1.alcance})`);
        confere('no puxão, a corrente vai até ~260 px', puxao >= 220, `puxão ${puxao.toFixed(0)}`);
        const est = Motor.INIMIGOS.lanceiro.golpes.estocada;
        const estocada = desenharEm('lanceiro', 'atacando', 'estocada', est.inicio + 0.01);
        confere('a lança do lanceiro chega ao alcance da estocada', estocada >= est.alcance * 0.85 / Motor.INIMIGOS.lanceiro.escala, `estocada ${estocada.toFixed(0)} (alcance ${est.alcance})`);
        let estourou = null;
        for (const id of ['lian', 'lanceiro', 'renegado']) {
            const def = Motor.PERSONAGENS[id] || Motor.INIMIGOS[id];
            for (const estado of ['parado', 'andando', 'defendendo', 'atordoado', 'puxado', 'furia']) {
                try { desenharEm(id, estado); } catch (e) { estourou = `${id}/${estado}: ${e.message}`; }
            }
            for (const g of Object.keys(def.golpes)) for (const t of [0, def.golpes[g].inicio + 0.01, def.golpes[g].total - 0.01]) {
                try { desenharEm(id, 'atacando', g, t); } catch (e) { estourou = `${id}/${g}@${t}: ${e.message}`; }
            }
        }
        confere('desenhar os três novos em todo estado e golpe não estoura', estourou === null, estourou);
    }
});

console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
process.exit(falhas.length === 0 ? 0 : 1);
