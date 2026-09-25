// A LOJA DE GOLPES do "Punhos de Shaolin" (jogo/): karma, catálogo, compra e o efeito de cada
// melhoria no motor, conferidos no Node sem navegador.
//
//     node Padelizou.Tests/js/conferir-loja-do-shaolin.js
//
// Fase 1 do `jogo/CRONOGRAMA.md`, primeiro item: a progressão que é o coração do Shaolin Monks.
// Terminar uma fase dá KARMA (pelos pontos ganhos nela); no Templo, entre as fases, o karma
// compra golpes e melhorias. Três arquivos puros entram aqui:
//   - `loja.js`      — catálogo, preço, compra (desconta, salva, não repete);
//   - `progresso.js` — onde o karma e os golpes aprendidos ficam guardados (`normalizar`);
//   - `motor.js`     — o que cada melhoria MUDA na luta, com e sem ela.
// Cada melhoria é conferida nos dois sentidos: com ela o efeito aparece, sem ela o jogo continua
// igual. Só um dos dois lados provaria pouco — uma melhoria que vaza pra quem não comprou é
// defeito do mesmo tamanho de uma que não funciona.
//
// Sem dependência: `require` de arquivos do repositório e mais nada.
const path = require('path');
const raiz = path.join(__dirname, '..', '..', 'jogo', 'js');
const Motor = require(path.join(raiz, 'motor.js'));
const Progresso = require(path.join(raiz, 'progresso.js'));
const Loja = require(path.join(raiz, 'loja.js'));

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
const TODAS = ['sequencia_cinco', 'contra_golpe', 'especial_aereo', 'agarrao_costas', 'vigor', 'respiracao', 'punhos_de_ferro'];

// ── AJUDANTES (os mesmos do conferidor de combate) ────────────────────────────────────────
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
    Motor.passo(mundo, DT, [e]);
}
function treino(liberados, personagem) {
    return Motor.criarMundo({ fase: 0, jogadores: [personagem || 'long'], semente: 7, liberados: liberados || [] });
}
function boneco(mundo, tipo, dx, extra) {
    const j = mundo.jogadores[0];
    const i = Motor.colocarInimigo(mundo, tipo, j.x + dx, j.y);
    i.ia.congelada = true;
    return Object.assign(i, extra || {});
}
function plataformaFalsa(guardado) {
    const p = { guardado: guardado === undefined ? null : guardado };
    p.carregar = () => p.guardado;
    p.salvar = dados => { p.guardado = JSON.parse(JSON.stringify(dados)); };
    return p;
}

// ── 1. O CATÁLOGO ─────────────────────────────────────────────────────────────────────────
bloco('catálogo', () => {
    const cat = Loja.CATALOGO;
    confere('o catálogo é uma lista com as sete melhorias', Array.isArray(cat) && TODAS.every(id => cat.some(d => d.id === id)),
            JSON.stringify(cat && cat.map(d => d.id)));
    confere('ids únicos e só de [a-z_]', new Set(cat.map(d => d.id)).size === cat.length && cat.every(d => /^[a-z_]+$/.test(d.id)),
            cat.map(d => d.id).join(','));
    confere('todo item tem nome, descrição e preço inteiro > 0', cat.every(d => d.nome && d.descricao && Number.isInteger(d.preco) && d.preco > 0),
            JSON.stringify(cat.filter(d => !(d.nome && d.descricao && Number.isInteger(d.preco) && d.preco > 0))));
    confere('o motor exporta a lista de melhorias que entende', Array.isArray(Motor.MELHORIAS), `${typeof Motor.MELHORIAS}`);
    const desconhecidos = cat.map(d => d.id).filter(id => !(Motor.MELHORIAS || []).includes(id));
    confere('todo id do catálogo é conhecido pelo motor', desconhecidos.length === 0, `o motor não conhece: ${desconhecidos.join(',')}`);
});

// ── 2. KARMA DA FASE ──────────────────────────────────────────────────────────────────────
bloco('karmaDaFase', () => {
    const k = Loja.karmaDaFase;
    confere('karma = pontos da fase / 100, arredondado pra baixo', k(0) === 0 && k(99) === 0 && k(100) === 1 && k(1234) === 12 && k(1099.9) === 10,
            `0→${k(0)} 99→${k(99)} 100→${k(100)} 1234→${k(1234)} 1099,9→${k(1099.9)}`);
    confere('pontos negativos, NaN ou ausentes dão zero', k(-500) === 0 && k(NaN) === 0 && k(undefined) === 0 && k('900') === 0,
            `-500→${k(-500)} NaN→${k(NaN)} undefined→${k(undefined)} '900'→${k('900')}`);
});

// ── 3. PROGRESSO: karma e golpes guardados ────────────────────────────────────────────────
bloco('normalizar', () => {
    const p = Progresso.padrao();
    confere('o padrão nasce sem karma e sem golpe', p.karma === 0 && Array.isArray(p.golpes) && p.golpes.length === 0, JSON.stringify({ karma: p.karma, golpes: p.golpes }));
    const velho = Progresso.normalizar({ recorde: 300, faseAlcancada: 2 });
    confere('salvamento velho, sem os campos, ganha karma 0 e golpes []', velho.karma === 0 && Array.isArray(velho.golpes) && velho.golpes.length === 0 && velho.recorde === 300,
            JSON.stringify({ karma: velho.karma, golpes: velho.golpes }));
    const bom = Progresso.normalizar({ karma: 420, golpes: ['vigor', 'punhos_de_ferro'] });
    confere('karma e golpes válidos sobrevivem', bom.karma === 420 && bom.golpes.join(',') === 'vigor,punhos_de_ferro', JSON.stringify({ karma: bom.karma, golpes: bom.golpes }));
    const karmas = [-3, 2.5, '100', null, NaN, Infinity].map(v => Progresso.normalizar({ karma: v }).karma);
    confere('karma corrompido vira 0 (negativo, fracionário, texto, nulo, NaN, infinito)', karmas.every(v => v === 0), karmas.join(','));
    const lixo = Progresso.normalizar({ golpes: ['vigor', 'Vigor!', 3, null, 'vigor', '__proto__x', 'punhos_de_ferro', { id: 'vigor' }] });
    confere('golpes corrompidos: só ids [a-z_], sem repetição', lixo.golpes.join(',') === 'vigor,__proto__x,punhos_de_ferro',
            JSON.stringify(lixo.golpes));
    confere('golpes que não são lista viram []', Progresso.normalizar({ golpes: 'vigor' }).golpes.length === 0 && Progresso.normalizar({ golpes: { vigor: true } }).golpes.length === 0,
            JSON.stringify(Progresso.normalizar({ golpes: 'vigor' }).golpes));
    const plat = plataformaFalsa({ karma: 80, golpes: ['vigor'] });
    const prog = Progresso.criar(plat);
    prog.apagar();
    confere('apagar o progresso zera karma e golpes', prog.dados.karma === 0 && prog.dados.golpes.length === 0 && plat.guardado.karma === 0,
            JSON.stringify({ karma: prog.dados.karma, golpes: prog.dados.golpes }));
});

// ── 4. A COMPRA ───────────────────────────────────────────────────────────────────────────
bloco('compra', () => {
    const preco = id => Loja.CATALOGO.find(d => d.id === id).preco;
    const plat = plataformaFalsa({ karma: 500 });
    const prog = Progresso.criar(plat);
    const loja = Loja.criar(prog);

    const r1 = loja.comprar('vigor');
    confere('comprar desconta o preço', r1.ok === true && prog.dados.karma === 500 - preco('vigor'), `${JSON.stringify(r1)} karma=${prog.dados.karma}`);
    confere('comprar grava karma e golpe na plataforma', plat.guardado && plat.guardado.karma === 500 - preco('vigor') && plat.guardado.golpes.includes('vigor'),
            JSON.stringify(plat.guardado && { karma: plat.guardado.karma, golpes: plat.guardado.golpes }));
    confere('o comprado aparece nos liberados', loja.liberados().includes('vigor'), JSON.stringify(loja.liberados()));

    const saldo = prog.dados.karma;
    const r2 = loja.comprar('vigor');
    confere('comprar de novo é recusado e não desconta', r2.ok === false && r2.motivo === 'aprendido' && prog.dados.karma === saldo && prog.dados.golpes.filter(g => g === 'vigor').length === 1,
            `${JSON.stringify(r2)} karma=${prog.dados.karma}`);
    const r3 = loja.comprar('voadora_secreta');
    const r4 = loja.comprar('__proto__');
    const r5 = loja.comprar('constructor');
    confere('id inventado é recusado e não desconta', [r3, r4, r5].every(r => r.ok === false && r.motivo === 'desconhecido') && prog.dados.karma === saldo
            && !prog.dados.golpes.includes('voadora_secreta'), `${JSON.stringify([r3, r4, r5])} karma=${prog.dados.karma}`);

    const r6 = loja.comprar('punhos_de_ferro');         // 350 - 300 = 50
    const r7 = loja.comprar('contra_golpe');            // custa 200, sobram 50
    confere('sem karma é recusado e não desconta', r6.ok === true && r7.ok === false && r7.motivo === 'sem_karma' && prog.dados.karma === saldo - preco('punhos_de_ferro')
            && !prog.dados.golpes.includes('contra_golpe'), `${JSON.stringify(r7)} karma=${prog.dados.karma}`);

    const itens = loja.itens();
    const vigor = itens.find(i => i.id === 'vigor'), contra = itens.find(i => i.id === 'contra_golpe');
    confere('itens() diz o que foi aprendido e o que dá pra comprar', itens.length === Loja.CATALOGO.length && vigor.aprendido === true && contra.aprendido === false && contra.podeComprar === false,
            JSON.stringify({ vigor, contra }));

    confere('a compra devolve um evento pra conquista (aprendidos / total)', r1.evento && r1.evento.tipo === 'golpe-aprendido' && r1.evento.aprendidos === 1
            && r6.evento.aprendidos === 2 && r6.evento.total === Loja.CATALOGO.length, JSON.stringify([r1.evento, r6.evento]));

    const antes = prog.dados.karma;
    const ganho = loja.receber(1234);
    confere('receber os pontos da fase soma karmaDaFase e grava', ganho === 12 && prog.dados.karma === antes + 12 && plat.guardado.karma === antes + 12,
            `ganho=${ganho} karma=${prog.dados.karma}`);
    confere('receber lixo não mexe no karma', loja.receber(-900) === 0 && loja.receber(NaN) === 0 && prog.dados.karma === antes + 12, `karma=${prog.dados.karma}`);

    const velha = Loja.criar(Progresso.criar(plataformaFalsa({ karma: 10, golpes: ['vigor', 'golpe_que_saiu_do_jogo'] })));
    confere('liberados ignora golpe guardado que o catálogo não tem mais', velha.liberados().join(',') === 'vigor', JSON.stringify(velha.liberados()));
});

// ── 5. O MOTOR RECEBE OS LIBERADOS ────────────────────────────────────────────────────────
bloco('liberados no mundo', () => {
    const m = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1, liberados: ['fantasma', 'vigor', 'vigor'] });
    confere('o mundo guarda os liberados e ignora id desconhecido', Array.isArray(m.liberados) && m.liberados.join(',') === 'vigor', JSON.stringify(m.liberados));
    const sem = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1 });
    confere('sem liberados, o mundo nasce sem melhoria', Array.isArray(sem.liberados) && sem.liberados.length === 0, JSON.stringify(sem.liberados));
});

// ── 6. SEQUÊNCIA DE CINCO ─────────────────────────────────────────────────────────────────
function corrente(liberados, personagem) {
    const mundo = treino(liberados, personagem);
    const j = mundo.jogadores[0];
    const alvo = boneco(mundo, 'sombra', 45, { vida: 1000, vidaMax: 1000 });
    const golpes = [];
    let lancou = false;
    for (let f = 0; f < 120 && !golpes.includes('soco3'); f++) {
        if (f % 10 === 0) apertar(mundo, 'soco'); else rodar(mundo, 1);
        if (j.golpeNome && j.golpeNome !== golpes[golpes.length - 1]) golpes.push(j.golpeNome);
    }
    rodar(mundo, 8);
    lancou = alvo.estado === 'lancado';
    return { golpes, dano: 1000 - alvo.vida, lancou, def: j.def };
}
bloco('sequencia_cinco', () => {
    for (const p of ['long', 'shen']) {
        const sem = corrente([], p), com = corrente(['sequencia_cinco'], p);
        confere(`${p}: sem a melhoria a corrente continua em 3`, sem.golpes.join(',') === 'soco1,soco2,soco3' && sem.lancou, `${sem.golpes.join(',')} lançou=${sem.lancou}`);
        const g = com.def.golpes;
        const esperado = ['soco1', 'soco2', 'soco4', 'soco5', 'soco3'].reduce((s, n) => s + ((g[n] && g[n].dano) || 0), 0);
        confere(`${p}: com a melhoria são 5 golpes antes do lançador`, com.golpes.join(',') === 'soco1,soco2,soco4,soco5,soco3', com.golpes.join(','));
        confere(`${p}: os 5 acertam e o último lança`, !!(g.soco4 && g.soco5) && com.lancou && com.dano === esperado, `dano=${com.dano} esperado=${esperado} lançou=${com.lancou}`);
    }
});

// ── 7. CONTRA-GOLPE ───────────────────────────────────────────────────────────────────────
// soco1 da Sombra liga em 0,18 s. `defendeNoQuadro` = em que quadro, depois de o golpe começar,
// o jogador aperta defender (negativo = já estava defendendo antes).
function defesa(liberados, defendeNoQuadro, extra) {
    const mundo = treino(liberados);
    const j = mundo.jogadores[0];
    const inimigo = boneco(mundo, 'sombra', 45, Object.assign({ virado: -1 }, extra || {}));
    const textos = [];
    if (defendeNoQuadro < 0) rodar(mundo, -defendeNoQuadro, [entrada({ defender: true })]);
    Motor.iniciarGolpe(inimigo, 'soco1');
    let atordoadoAte = null;
    for (let f = 0; f < 30; f++) {
        Motor.passo(mundo, DT, [entrada({ defender: f >= defendeNoQuadro })]);
        for (const ev of mundo.eventos) if (ev.tipo === 'texto') textos.push(ev.texto);
        if (atordoadoAte == null && inimigo.estado === 'atordoado') atordoadoAte = inimigo.atordoadoAte;
    }
    return { mundo, j, inimigo, textos, atordoadoAte, levou: j.vidaMax - j.vida };
}
bloco('contra_golpe', () => {
    const cheio = Motor.INIMIGOS.sombra.golpes.soco1.dano;
    const bloqueio = Math.max(1, Math.round(cheio * 0.2));

    const apara = defesa(['contra_golpe'], 5);          // ~0,1 s antes de ligar
    confere('defender em cima da hora (≤ 0,15 s antes) APARA: zero dano', apara.levou === 0, `levou ${apara.levou}`);
    confere('aparar atordoa o atacante por ~0,9 s', apara.atordoadoAte != null && apara.atordoadoAte > 0.8 && apara.atordoadoAte <= 1.0, `atordoadoAte=${apara.atordoadoAte} estado=${apara.inimigo.estado}`);
    confere('aparar mostra CONTRA! e dá +10 de chi', apara.textos.includes('CONTRA!') && apara.j.chi === 10, `textos=${apara.textos.join('|')} chi=${apara.j.chi}`);

    const segurado = defesa(['contra_golpe'], -30);     // defendendo meio segundo antes
    confere('defesa segurada desde antes da janela continua o bloqueio de 20%', segurado.levou === bloqueio && segurado.inimigo.estado !== 'atordoado',
            `levou ${segurado.levou} (esperava ${bloqueio}) estado=${segurado.inimigo.estado}`);

    const semMelhoria = defesa([], 5);
    confere('sem a melhoria, a mesma defesa em cima da hora só bloqueia', semMelhoria.levou === bloqueio && semMelhoria.inimigo.estado !== 'atordoado' && semMelhoria.j.chi === 0,
            `levou ${semMelhoria.levou} estado=${semMelhoria.inimigo.estado} chi=${semMelhoria.j.chi}`);

    // Tarde: a investida da Garra liga em 0,10 s e só chega depois — defender DEPOIS de ligar é bloqueio.
    {
        const mundo = treino(['contra_golpe']);
        const j = mundo.jogadores[0];
        const garra = boneco(mundo, 'garra', 160, { virado: -1 });
        Motor.iniciarGolpe(garra, 'investida');
        rodar(mundo, 40, f => [entrada({ defender: f >= 9 })]);
        const dano = Math.max(1, Math.round(Motor.INIMIGOS.garra.golpes.investida.dano * 0.2));
        confere('defender depois de o golpe ligar é bloqueio, não aparada', j.vidaMax - j.vida === dano && garra.estado !== 'atordoado',
                `levou ${j.vidaMax - j.vida} (esperava ${dano}) garra=${garra.estado}`);
    }

    // O atordoado da aparada NÃO é o atordoado da finalização: inimigo com vida cheia só é agarrado.
    // Solta a defesa por um quadro (sair da defesa consome o quadro) e agarra o atordoado.
    function agarrarAparado(extra) {
        const r = defesa(['contra_golpe'], 5, extra);
        rodar(r.mundo, 1);
        r.estavaAtordoado = r.inimigo.estado === 'atordoado';
        apertar(r.mundo, 'agarrar');
        return r;
    }
    {
        const r = agarrarAparado();
        confere('agarrar quem foi aparado com vida cheia NÃO finaliza', r.estavaAtordoado && r.j.estado !== 'finalizando' && r.inimigo.estado !== 'finalizado',
                `atordoado antes=${r.estavaAtordoado} jogador=${r.j.estado} inimigo=${r.inimigo.estado}`);
        confere('é o agarrão comum', r.estavaAtordoado && r.j.estado === 'agarrando' && r.inimigo.estado === 'agarrado', `jogador=${r.j.estado} inimigo=${r.inimigo.estado}`);
    }
    {
        const r = agarrarAparado({ vida: 5 });
        confere('aparado com pouca vida pode ser finalizado (a régua é a vida, não o atordoado)', r.estavaAtordoado && r.j.estado === 'finalizando' && r.inimigo.estado === 'finalizado',
                `atordoado antes=${r.estavaAtordoado} jogador=${r.j.estado} inimigo=${r.inimigo.estado}`);
    }
});

// ── 8. ESPECIAL NO AR ─────────────────────────────────────────────────────────────────────
function especialNoAr(liberados, personagem, chi, lados) {
    const mundo = treino(liberados, personagem);
    const j = mundo.jogadores[0];
    j.chi = chi;
    const alvos = lados.map(dx => boneco(mundo, 'sombra', dx));
    apertar(mundo, 'pular');
    rodar(mundo, 8);
    const noAr = j.z > 0;
    apertar(mundo, 'especial');
    const golpe = j.golpeNome, chiDepois = j.chi;
    rodar(mundo, 50);
    return { noAr, golpe, chiDepois, j, alvos, projeteis: mundo.projeteis.length };
}
bloco('especial_aereo', () => {
    for (const [p, lados] of [['long', [70]], ['shen', [60, -60]]]) {
        const custo = Motor.PERSONAGENS[p].golpes.especial.chi;
        const com = especialNoAr(['especial_aereo'], p, 100, lados);
        confere(`${p}: com a melhoria, especial no pulo vira o especialAereo`, com.noAr && com.golpe === 'especialAereo', `noAr=${com.noAr} golpe=${com.golpe}`);
        confere(`${p}: o especialAereo gasta o chi do especial`, com.chiDepois === 100 - custo, `chi=${com.chiDepois} custo=${custo}`);
        confere(`${p}: e acerta ${lados.length > 1 ? 'dos dois lados' : 'na diagonal pra baixo'}`, com.alvos.every(a => a.vida < a.vidaMax), com.alvos.map(a => a.vida).join(','));
        const sem = especialNoAr([], p, 100, lados);
        confere(`${p}: sem a melhoria, especial no pulo não faz nada`, sem.golpe !== 'especialAereo' && sem.chiDepois === 100 && sem.projeteis === 0,
                `golpe=${sem.golpe} chi=${sem.chiDepois}`);
        const pobre = especialNoAr(['especial_aereo'], p, 5, lados);
        confere(`${p}: sem chi, nem com a melhoria`, pobre.golpe !== 'especialAereo' && pobre.chiDepois === 5, `golpe=${pobre.golpe} chi=${pobre.chiDepois}`);
    }
    const semGolpe = Object.keys(Motor.PERSONAGENS).filter(id => !Motor.PERSONAGENS[id].golpes.especialAereo);
    confere('todo monge tem o golpe especialAereo', semGolpe.length === 0, semGolpe.join(','));
});

// ── 9. AGARRÃO PELAS COSTAS ───────────────────────────────────────────────────────────────
function agarrao(liberados, tipo, viradoDoAlvo) {
    const mundo = treino(liberados);
    const j = mundo.jogadores[0];
    const alvo = boneco(mundo, tipo, 40, { virado: viradoDoAlvo });
    const vida = alvo.vida;
    apertar(mundo, 'agarrar');
    const r = { jogador: j.estado, alvo: alvo.estado, tirou: vida - alvo.vida };
    rodar(mundo, 60);
    r.depois = alvo.estado;
    return r;
}
bloco('agarrao_costas', () => {
    // O jogador está à esquerda olhando pra direita; alvo com virado +1 está DE COSTAS pra ele.
    const costas = agarrao(['agarrao_costas'], 'sombra', 1);
    confere('de costas, com a melhoria: suplex na hora', costas.jogador === 'suplex' && costas.alvo === 'lancado', JSON.stringify(costas));
    confere('o suplex tira ~20 e derruba', costas.tirou >= 18 && costas.tirou <= 22 && (costas.depois === 'caido' || costas.depois === 'levantando'), JSON.stringify(costas));
    const frente = agarrao(['agarrao_costas'], 'sombra', -1);
    confere('de frente, com a melhoria: agarrão comum', frente.jogador === 'agarrando' && frente.alvo === 'agarrado', JSON.stringify(frente));
    const semMelhoria = agarrao([], 'sombra', 1);
    confere('de costas, sem a melhoria: agarrão comum', semMelhoria.jogador === 'agarrando' && semMelhoria.alvo === 'agarrado', JSON.stringify(semMelhoria));
    const bruto = agarrao(['agarrao_costas'], 'bruto', 1);
    confere('de costas, mesmo com armadura (Bruto): suplex', bruto.jogador === 'suplex' && bruto.alvo === 'lancado', JSON.stringify(bruto));
    const chefe = agarrao(['agarrao_costas'], 'mestreSombra', 1);
    confere('chefe de costas NUNCA leva suplex', chefe.jogador !== 'suplex' && chefe.alvo !== 'lancado', JSON.stringify(chefe));
});

// ── 10. VIGOR ─────────────────────────────────────────────────────────────────────────────
bloco('vigor', () => {
    const vida = p => Motor.PERSONAGENS[p].vida;
    const com = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1, liberados: ['vigor'] });
    const j = com.jogadores[0];
    confere('com vigor: +20% de vida máxima, e começa cheia', j.vidaMax === Math.round(vida('long') * 1.2) && j.vida === j.vidaMax, `vidaMax=${j.vidaMax} vida=${j.vida}`);
    const sem = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1 });
    confere('sem vigor: a vida de sempre', sem.jogadores[0].vidaMax === vida('long'), `vidaMax=${sem.jogadores[0].vidaMax}`);
    const p2 = Motor.adicionarJogador(com, 'shen');
    confere('o P2 que entra no meio também ganha o vigor', p2.vidaMax === Math.round(vida('shen') * 1.2) && p2.vida === p2.vidaMax, `vidaMax=${p2.vidaMax} vida=${p2.vida}`);
    const p2sem = Motor.adicionarJogador(sem, 'shen');
    confere('e o P2 de um mundo sem vigor não ganha', p2sem.vidaMax === vida('shen'), `vidaMax=${p2sem.vidaMax}`);
    const dupla = Motor.criarMundo({ fase: 1, jogadores: ['long', 'shen'], semente: 1, liberados: ['vigor'] });
    confere('os dois jogadores do começo ganham', dupla.jogadores.every(x => x.vidaMax === Math.round(x.def.vida * 1.2)), dupla.jogadores.map(x => x.vidaMax).join(','));
});

// ── 11. RESPIRAÇÃO DO TEMPLO ──────────────────────────────────────────────────────────────
function respirar(liberados, entradaDoQuadro) {
    const mundo = treino(liberados);
    rodar(mundo, 60, f => [entradaDoQuadro(f)]);
    return mundo.jogadores[0].chi;
}
bloco('respiracao', () => {
    const parado = respirar(['respiracao'], () => entrada());
    confere('com a melhoria, parado: +3 de chi por segundo', Math.abs(parado - 3) < 0.1, `chi=${parado}`);
    const andando = respirar(['respiracao'], () => entrada({ direita: true }));
    confere('andando também respira', Math.abs(andando - 3) < 0.1, `chi=${andando}`);
    const defendendo = respirar(['respiracao'], () => entrada({ defender: true }));
    confere('defendendo também respira', Math.abs(defendendo - 3) < 0.1, `chi=${defendendo}`);
    const atacando = respirar(['respiracao'], () => { const e = entrada({ soco: true }); e.apertou.soco = true; return e; });
    confere('atacando (no vazio) não respira', atacando < 0.2, `chi=${atacando}`);
    const sem = respirar([], () => entrada());
    confere('sem a melhoria, parado não ganha chi', sem === 0, `chi=${sem}`);
});

// ── 12. PUNHOS DE FERRO ───────────────────────────────────────────────────────────────────
function socoNoBoneco(liberados, botao) {
    const mundo = treino(liberados);
    const alvo = boneco(mundo, 'sombra', 45, { vida: 1000, vidaMax: 1000 });
    apertar(mundo, botao);
    rodar(mundo, 40);
    return 1000 - alvo.vida;
}
bloco('punhos_de_ferro', () => {
    const g = Motor.PERSONAGENS.long.golpes;
    const soco = socoNoBoneco(['punhos_de_ferro'], 'soco'), chute = socoNoBoneco(['punhos_de_ferro'], 'chute');
    confere('com a melhoria: +15% de dano, arredondado', soco === Math.round(g.soco1.dano * 1.15) && chute === Math.round(g.chute.dano * 1.15),
            `soco=${soco} (esperava ${Math.round(g.soco1.dano * 1.15)}) chute=${chute} (esperava ${Math.round(g.chute.dano * 1.15)})`);
    const semSoco = socoNoBoneco([], 'soco');
    confere('sem a melhoria: o dano de sempre', semSoco === g.soco1.dano, `soco=${semSoco}`);
    // O inimigo não herda os punhos do jogador.
    const mundo = treino(['punhos_de_ferro']);
    const j = mundo.jogadores[0];
    const inimigo = boneco(mundo, 'sombra', 45, { virado: -1 });
    Motor.iniciarGolpe(inimigo, 'soco1');
    rodar(mundo, 30);
    confere('o dano do inimigo no jogador não muda', j.vidaMax - j.vida === Motor.INIMIGOS.sombra.golpes.soco1.dano, `levou ${j.vidaMax - j.vida}`);
});

// ── 13. O DEFEITO DOS PONTOS: pontos pelo dano TIRADO, não pelo dano bruto ────────────────
// Um golpe de 9999 numa Sombra de 30 de vida dava 99.990 pontos — e o karma vem dos pontos.
bloco('pontos pelo dano efetivo', () => {
    const mundo = treino([]);
    const j = mundo.jogadores[0];
    const alvo = boneco(mundo, 'sombra', 45);
    const vida = alvo.vida;
    Motor.aplicarDano(mundo, alvo, { dano: 9999, origem: j });
    confere('golpe exagerado vale só a vida que o alvo tinha (+ os pontos da morte)', mundo.pontuacao === vida * 10 + alvo.def.pontos,
            `pontos=${mundo.pontuacao} esperava ${vida * 10 + alvo.def.pontos}`);
    const m2 = treino([]);
    const a2 = boneco(m2, 'sombra', 45, { vida: 3 });
    Motor.aplicarDano(m2, a2, { dano: 10, origem: m2.jogadores[0] });
    confere('golpe de 10 em quem tinha 3 vale 30 (+ morte)', m2.pontuacao === 30 + a2.def.pontos, `pontos=${m2.pontuacao}`);
    const m3 = treino([]);
    const a3 = boneco(m3, 'sombra', 45);
    Motor.aplicarDano(m3, a3, { dano: 5, origem: m3.jogadores[0] });
    confere('golpe comum continua valendo dano × 10', m3.pontuacao === 50, `pontos=${m3.pontuacao}`);
});

console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
process.exit(falhas.length === 0 ? 0 : 1);
