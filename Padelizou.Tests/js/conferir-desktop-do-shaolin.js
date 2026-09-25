// O APP DESKTOP do "Punhos de Shaolin" (jogo/desktop/), conferido no Node — sem Electron.
//
//     node Padelizou.Tests/js/conferir-desktop-do-shaolin.js
//
// A checklist de segurança do Electron, travada antes da Steam. Duas partes:
//   1. `caminho-seguro.js` não requer electron: TODA decisão de segurança mora lá — o que o
//      app:// serve e com que cabeçalho, o porteiro do IPC, o bloqueio de navegação e o
//      salvamento com .bak (este com arquivo de verdade numa pasta temporária). Aqui se chama a
//      função de verdade, com ataque e com pedido legítimo.
//   2. `main.js`, `preload.js` e `package.json` dependem do Electron e não rodam no CI — então
//      são guardados POR TEXTO, e o main só liga fios: cada fio é conferido pela forma exata
//      (qual função, com qual argumento), sem comentário contando. Texto não prova que funciona
//      (isso é a fumaça sob Xvfb); prova que ninguém tirou.
const path = require('path');
const fs = require('fs');
const os = require('os');

// Os avisos do caminho-seguro vão pro console.warn. Aqui eles são recolhidos (em vez de sujar a
// saída) e conferidos: "recusado com aviso no log" é parte do contrato.
const avisos = [];
console.warn = (...partes) => avisos.push(partes.map(String).join(' '));
const avisouDesde = (marca, trecho) => avisos.slice(marca).some(a => a.includes(trecho));
const raizDoJogo = path.join(__dirname, '..', '..', 'jogo');
const pastaDesktop = path.join(raizDoJogo, 'desktop');

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}
function ler(arquivo) {
    try { return fs.readFileSync(arquivo, 'utf8'); }
    catch (erro) { console.log(`não deu pra ler ${arquivo}: ${erro.message}`); return ''; }
}

let Seguro = null;
try { Seguro = require(path.join(pastaDesktop, 'caminho-seguro.js')); }
catch (erro) { console.log(`caminho-seguro.js não carregou: ${erro.message}`); }
confere('caminho-seguro.js existe e carrega sem Electron', !!Seguro, 'não existe');
Seguro = Seguro || {};
const rotulo = x => String(JSON.stringify(x)).slice(0, 50);
const chama = (fn, ...args) => { try { return typeof fn === 'function' ? fn(...args) : 'SEM FUNÇÃO'; } catch (erro) { return 'LANÇOU: ' + erro.message; } };

// ── 1. O CAMINHO: só index.html, js/ e fontes/, nada fora da pasta do jogo ──────────────────
{
    const raiz = path.resolve('/opt/punhos/app');
    const resolver = pedido => chama(Seguro.resolverCaminho, raiz, pedido);
    const dentro = p => typeof p === 'string' && p.startsWith(raiz + path.sep);

    const legitimos = [
        ['/', path.join(raiz, 'index.html')],
        ['/index.html', path.join(raiz, 'index.html')],
        ['/js/motor.js', path.join(raiz, 'js', 'motor.js')],
        ['/js/principal.js', path.join(raiz, 'js', 'principal.js')],
        // As fontes vêm empacotadas (jogo/fontes/): o desktop não sai pra rede atrás delas.
        ['/fontes/fontes.css', path.join(raiz, 'fontes', 'fontes.css')],
        ['/fontes/cinzel-latin.woff2', path.join(raiz, 'fontes', 'cinzel-latin.woff2')],
        ['/fontes/chakra-petch-700-italico-latin-ext.woff2', path.join(raiz, 'fontes', 'chakra-petch-700-italico-latin-ext.woff2')],
    ];
    for (const [pedido, esperado] of legitimos)
        confere(`serve o legítimo ${pedido}`, resolver(pedido) === esperado, `veio ${JSON.stringify(resolver(pedido))}`);

    const ataques = [
        '/../package.json', '/js/../../etc/passwd', '/js/../desktop/main.js', '..', '../x',
        '/%2e%2e/package.json', '/%2E%2E%2Fdesktop%2Fmain.js', '/js/%2e%2e/%2e%2e/etc/passwd', '/%252e%252e/x',
        '/js\\..\\..\\x', '/..\\desktop\\main.js', '\\js\\motor.js',
        '/etc/passwd', '//etc/passwd', 'C:/Windows/win.ini', '/C:/Windows/win.ini', 'js/motor.js',
        '/js/motor.js%00.html', '/js/motor.js\0', '/index.html\u0000',
        '/desktop/main.js', '/desktop/preload.js', '/package.json', '/node_modules/electron/index.js',
        '/js/', '/js/sub/x.js', '/js/./motor.js', '/./index.html', '/js/motor.json', '/js/.js',
        // Com a forma da lista branca (js/<nome>.js): é o teto de tamanho que tem que barrar.
        '/js/' + 'a'.repeat(600) + '.js', '', undefined, null, 123, {},
        // fontes/: só .woff2 e .css, um nível, nome simples. A licença mora ali, mas não é servida.
        '/fontes/../package.json', '/fontes/%2e%2e/package.json', '/fontes/..\\desktop\\main.js', '/fontes/../js/motor.js',
        '/fontes/', '/fontes', '/fontes/.woff2', '/fontes/.css', '/fontes/sub/x.woff2', '/fontes/x.woff2/',
        '/fontes/x.js', '/fontes/x.ttf', '/fontes/x.woff', '/fontes/x.html', '/fontes/OFL-Cinzel.txt',
        '/fontes/x.woff2.js', '/fontes/x.css%00.js', '/js/fontes.css', '/js/x.woff2', '/fontes.css', '/x.woff2',
        '/fontes/' + 'a'.repeat(600) + '.woff2',
    ];
    // Atalho consciente: o filtro de %, barra invertida e byte nulo e a contenção final (startsWith da base) não têm
    // caso que os alcance sozinhos — a lista branca ARQUIVO_DO_JOGO recusa antes tudo que eles
    // recusariam. São cinto e suspensório; se a lista branca afrouxar, os ataques acima voltam a
    // passar por eles e ficam vermelhos aqui.
    for (const pedido of ataques)
        confere(`recusa o caminho ${rotulo(pedido)}`, resolver(pedido) === null, `veio ${JSON.stringify(resolver(pedido))}`);

    const todos = [...legitimos.map(l => l[0]), ...ataques].map(resolver).filter(r => r !== null);
    confere('tudo que é servido fica DENTRO da pasta do jogo', todos.length > 0 && todos.every(dentro), JSON.stringify(todos.filter(r => !dentro(r))));

    const url = u => chama(Seguro.resolverUrl, raiz, u);
    confere('a URL app://jogo/js/motor.js vira o arquivo', url('app://jogo/js/motor.js') === path.join(raiz, 'js', 'motor.js'), `veio ${JSON.stringify(url('app://jogo/js/motor.js'))}`);
    confere('a URL app://jogo/ vira o index.html', url('app://jogo/') === path.join(raiz, 'index.html'), `veio ${JSON.stringify(url('app://jogo/'))}`);
    confere('a URL com ?consulta e #âncora ainda serve o arquivo', url('app://jogo/index.html?x=1#y') === path.join(raiz, 'index.html'), `veio ${JSON.stringify(url('app://jogo/index.html?x=1#y'))}`);
    confere('a URL app://jogo/fontes/fontes.css vira o arquivo', url('app://jogo/fontes/fontes.css') === path.join(raiz, 'fontes', 'fontes.css'), `veio ${JSON.stringify(url('app://jogo/fontes/fontes.css'))}`);
    for (const u of ['app://outro/fontes/fontes.css', 'app://jogo/fontes/../desktop/main.js', 'app://jogo/fontes/OFL-Cinzel.txt', 'app://outro/index.html', 'file:///etc/passwd', 'https://evil.example/index.html', 'https://jogo/index.html', 'file://jogo/index.html', 'app://jogo/desktop/main.js', 'app://jogo/%2e%2e/package.json', 'não é url', undefined])
        confere(`recusa a URL ${rotulo(u)}`, url(u) === null, `veio ${JSON.stringify(url(u))}`);

    confere('.html sai como text/html', /^text\/html/.test(String(chama(Seguro.tipoDoArquivo, '/x/index.html'))), `veio ${chama(Seguro.tipoDoArquivo, '/x/index.html')}`);
    confere('.woff2 sai como font/woff2', chama(Seguro.tipoDoArquivo, '/x/fontes/cinzel-latin.woff2') === 'font/woff2', `veio ${chama(Seguro.tipoDoArquivo, '/x/fontes/cinzel-latin.woff2')}`);
    confere('.css sai como text/css', /^text\/css(;|$)/.test(String(chama(Seguro.tipoDoArquivo, '/x/fontes/fontes.css'))), `veio ${chama(Seguro.tipoDoArquivo, '/x/fontes/fontes.css')}`);
    confere('.js sai como text/javascript', /^text\/javascript/.test(String(chama(Seguro.tipoDoArquivo, '/x/js/motor.js'))), `veio ${chama(Seguro.tipoDoArquivo, '/x/js/motor.js')}`);
}

// ── 2. A CSP: restritiva, e sem script inline na página que ela protege ──────────────────────
{
    const csp = String(Seguro.POLITICA_DE_CONTEUDO || '');
    const diretivas = Object.fromEntries(csp.split(';').map(d => d.trim()).filter(Boolean).map(d => { const [nome, ...valores] = d.split(/\s+/); return [nome, valores.join(' ')]; }));
    const exigidas = {
        'default-src': "'self'", 'script-src': "'self'",
        'style-src': "'self' 'unsafe-inline'", 'font-src': "'self'",
        'img-src': "'self' data:", 'connect-src': "'none'", 'object-src': "'none'", 'base-uri': "'none'",
        'form-action': "'none'", 'frame-ancestors': "'none'",
    };
    for (const [nome, valor] of Object.entries(exigidas))
        confere(`CSP: ${nome} ${valor}`, diretivas[nome] === valor, `veio ${JSON.stringify(diretivas[nome])}`);
    // As fontes vêm de app://jogo/fontes/: a página não tem motivo nenhum pra falar com a rede.
    confere('CSP não libera nenhum endereço de rede (http:, https:, *)', csp.length > 0 && !/https?:|\*/i.test(csp), csp || '(vazia)');
    confere('CSP não libera eval nem script inline', csp.length > 0 && !/unsafe-eval/.test(csp) && !/script-src[^;]*unsafe-inline/.test(csp), csp || '(vazia)');

    const html = ler(path.join(raizDoJogo, 'index.html'));
    const scripts = html.match(/<script\b[^>]*>/gi) || [];
    confere('index.html não tem <script> inline (a CSP o mataria)', scripts.length > 0 && scripts.every(s => /\bsrc=/.test(s)), scripts.filter(s => !/\bsrc=/.test(s)).join(' '));
    confere('index.html não tem handler inline (onclick=…)', html.length > 0 && !/<[^>]+\son[a-z]+\s*=/i.test(html), 'achou on*= numa tag');
    // Nada de rede no index.html: nem o Google Fonts, nem preconnect, nem qualquer http(s)://.
    const externos = html.match(/\b(href|src)\s*=\s*["']?(https?:)?\/\/[^"'\s>]*/gi) || [];
    confere('index.html não busca nada na rede (sem Google Fonts nem href/src http)', html.length > 0 && externos.length === 0 && !/fonts\.(googleapis|gstatic)\.com/.test(html), externos.join(' ') || 'cita fonts.googleapis/gstatic');
    const folhas = [...html.matchAll(/<link\b[^>]*>/gi)].map(m => m[0]).filter(l => /\brel\s*=\s*["']?stylesheet/i.test(l));
    const hrefs = folhas.map(l => (l.match(/\bhref\s*=\s*["']([^"']+)["']/i) || [])[1]);
    confere('index.html liga fontes/fontes.css (relativo, serve no file:// e no app://)', hrefs.includes('fontes/fontes.css'), JSON.stringify(hrefs));
    confere('toda folha de estilo do index.html passa pelo app://', hrefs.length > 0 && hrefs.every(h => typeof h === 'string' && chama(Seguro.resolverCaminho, raizDoJogo, '/' + h) === path.join(raizDoJogo, ...h.split('/'))), JSON.stringify(hrefs));
}

// ── 2b. As fontes empacotadas: jogo/fontes/fontes.css e os .woff2 que ela cita ────────────────
// O jogo desenha com 'Cinzel' 700/900 e 'Chakra Petch' 600/700/700 itálico (canvas e CSS). Cada
// face precisa do subconjunto latin (onde mora o português: ç ã õ é ê í ú) e do latin-ext, com o
// arquivo de verdade ao lado, em woff2, servível pelo app://, e com a licença OFL junto.
{
    const pastaFontes = path.join(raizDoJogo, 'fontes');
    const css = ler(path.join(pastaFontes, 'fontes.css'));
    const semComentarioCss = css.replace(/\/\*[\s\S]*?\*\//g, '');
    const blocos = [...semComentarioCss.matchAll(/@font-face\s*\{([^}]*)\}/g)].map(m => m[1]);
    const prop = (bloco, nome) => { const m = bloco.match(new RegExp(`(?:^|;|\\s)${nome}\\s*:\\s*([^;]+)`)); return m ? m[1].trim() : ''; };
    // unicode-range "U+0000-00FF, U+0131" → [[0, 255], [305, 305]]
    const faixas = texto => texto.split(',').map(t => t.trim().replace(/^U\+/i, '')).filter(Boolean).map(t => {
        const [a, b] = t.split('-'); return [parseInt(a, 16), parseInt(b === undefined ? a : b, 16)];
    });
    const faces = blocos.map(b => ({
        familia: prop(b, 'font-family').replace(/^['"]|['"]$/g, ''),
        estilo: prop(b, 'font-style') || 'normal',
        peso: prop(b, 'font-weight'),
        display: prop(b, 'font-display'),
        src: prop(b, 'src'),
        faixas: faixas(prop(b, 'unicode-range')),
    }));
    confere('fontes.css existe e tem @font-face', faces.length > 0, 'sem fontes.css ou sem @font-face');
    confere('toda @font-face tem font-display: swap (o jogo não espera a fonte)', faces.length > 0 && faces.every(f => f.display === 'swap'), JSON.stringify(faces.map(f => f.display)));

    const portugues = [...'çãõéêíúáâàóôÇÃÕÉÊÍÚÁÂÀÓÔ'].map(c => c.codePointAt(0));
    const cobre = (f, cp) => f.faixas.some(([a, b]) => cp >= a && cp <= b);
    const pedidas = [['Cinzel', 'normal', '700'], ['Cinzel', 'normal', '900'], ['Chakra Petch', 'normal', '600'], ['Chakra Petch', 'normal', '700'], ['Chakra Petch', 'italic', '700']];
    for (const [familia, estilo, peso] of pedidas) {
        const daFace = faces.filter(f => f.familia === familia && f.estilo === estilo && f.peso === peso);
        const nome = `'${familia}' ${peso}${estilo === 'italic' ? ' itálico' : ''}`;
        confere(`fonte ${nome}: cobre o português (ç ã õ é ê í ú …)`, portugues.every(cp => daFace.some(f => cobre(f, cp))), `faces ${daFace.length}; falta ${portugues.filter(cp => !daFace.some(f => cobre(f, cp))).map(cp => String.fromCodePoint(cp)).join('')}`);
        confere(`fonte ${nome}: tem o latin-ext (U+0100)`, daFace.some(f => cobre(f, 0x100)), `faces ${daFace.length}`);
    }

    const urls = faces.map(f => (f.src.match(/url\(\s*['"]?([^'")]+)['"]?\s*\)/) || [])[1]);
    confere("toda src é url() relativa, na mesma pasta, em woff2 (format('woff2'))", faces.length > 0 && urls.every(u => typeof u === 'string' && /^[a-z0-9_-]+\.woff2$/i.test(u)) && faces.every(f => /format\(\s*['"]woff2['"]\s*\)/.test(f.src)), JSON.stringify(faces.map(f => f.src)));
    confere('fontes.css não fala com a rede', css.length > 0 && !/https?:|\/\//.test(semComentarioCss), 'achou endereço de rede');
    const citados = [...new Set(urls.filter(Boolean))];
    const quebrados = citados.filter(u => { const b = fs.existsSync(path.join(pastaFontes, u)) ? fs.readFileSync(path.join(pastaFontes, u)) : null; return !b || b.length < 1000 || b.subarray(0, 4).toString('latin1') !== 'wOF2'; });
    confere(`todo .woff2 citado existe e é woff2 de verdade (${citados.length - quebrados.length} de ${citados.length})`, citados.length > 0 && quebrados.length === 0, quebrados.join(', ') || 'nenhum citado');
    const naoServidos = citados.filter(u => chama(Seguro.resolverCaminho, raizDoJogo, '/fontes/' + u) !== path.join(pastaFontes, u));
    confere('todo .woff2 citado passa pelo app://', citados.length > 0 && naoServidos.length === 0, naoServidos.join(', ') || 'nenhum citado');
    const naPasta = fs.existsSync(pastaFontes) ? fs.readdirSync(pastaFontes).filter(n => n.endsWith('.woff2')) : [];
    const sobrando = naPasta.filter(n => !citados.includes(n));
    confere('nenhum .woff2 sobrando na pasta (peso morto no instalador)', naPasta.length > 0 && sobrando.length === 0, sobrando.join(', ') || 'pasta vazia');

    // OFL 1.1, cláusula 2: pode ir empacotada e vendida junto com software, desde que cada cópia
    // leve o aviso de copyright e a licença.
    for (const [arquivo, autor] of [['OFL-Cinzel.txt', 'The Cinzel Project Authors'], ['OFL-ChakraPetch.txt', 'The Chakra Petch Project Authors']]) {
        const texto = ler(path.join(pastaFontes, arquivo));
        confere(`licença ${arquivo}: OFL 1.1 com o copyright (${autor})`, texto.includes('SIL Open Font License, Version 1.1') && texto.includes(autor) && /bundled,\s*redistributed and\/or sold with any software/.test(texto), 'ausente ou incompleta');
        confere(`fontes.css aponta a licença ${arquivo}`, css.includes(arquivo), 'não cita');
    }
}

// ── 3. O IPC: cada argumento que a página manda passa por um porteiro ────────────────────────
{
    const v = Seguro.validar || {};
    const umMega = 1024 * 1024;
    const progressoOk = t => chama(v.textoDeProgresso, t) === true;
    confere('progresso: JSON de objeto é aceito', progressoOk('{"versao":1,"recorde":10}'), 'recusou o legítimo');
    confere('progresso: exatamente 1 MB é aceito', progressoOk('{"x":"' + 'a'.repeat(umMega - 8) + '"}'), 'recusou 1 MB');
    confere('progresso: 1 MB + 1 byte é recusado', !progressoOk('{"x":"' + 'a'.repeat(umMega - 7) + '"}'), 'aceitou mais de 1 MB');
    confere('progresso: o limite conta BYTES, não caracteres', !progressoOk('{"x":"' + 'é'.repeat(umMega / 2) + '"}'), 'aceitou 1 MB+ em UTF-8');
    for (const ruim of ['{"quebrado":', 'null', '[1,2]', '"texto"', '42', '', undefined, 42, { versao: 1 }])
        confere(`progresso: recusa ${rotulo(ruim)}`, !progressoOk(ruim), 'aceitou');

    const id = x => chama(v.idDaSteam, x) === true;
    confere('conquista: primeiro_sangue é aceito', id('primeiro_sangue'), 'recusou');
    // O porteiro e a lista de conquistas andam juntos: um id que o jogo desbloqueia e o porteiro
    // recusa some calado na Steam (aconteceu com 'arena_10', que tem algarismo — a API da Steam
    // aceita dígito em nome de conquista). Toda conquista do jogo tem que passar.
    const Conquistas = require(path.join(raizDoJogo, 'js', 'conquistas.js'));
    const recusadas = Conquistas.LISTA.map(c => c.id).filter(c => !id(c));
    confere('toda conquista do jogo passa pelo porteiro da Steam', Conquistas.LISTA.length > 0 && recusadas.length === 0, `recusadas: ${recusadas.join(', ')}`);
    confere('a estatística que o jogo manda (pontuacao_maxima) passa pelo porteiro', id('pontuacao_maxima'), 'recusou');
    confere('conquista: 64 letras é aceito', id('a'.repeat(64)), 'recusou');
    confere('conquista: dígito depois da primeira letra é aceito (arena_10, x1)', id('arena_10') && id('x1'), 'recusou');
    for (const ruim of ['', 'a'.repeat(65), 'Primeiro', 'dez-golpes', '../x', '1x', '_x', 'a b', 'a.b', undefined, 7])
        confere(`conquista: recusa ${rotulo(ruim)}`, !id(ruim), 'aceitou');

    const num = x => chama(v.numeroFinito, x) === true;
    confere('estatística: 0 e 12345 são aceitos', num(0) && num(12345), 'recusou');
    for (const ruim of [NaN, Infinity, -Infinity, '5', null, undefined])
        confere(`estatística: recusa ${String(ruim)}`, !num(ruim), 'aceitou');

    const pres = x => chama(v.presenca, x) === true;
    confere('presença: 200 caracteres é aceito', pres('x'.repeat(200)), 'recusou');
    confere('presença: 201 caracteres é recusado', !pres('x'.repeat(201)), 'aceitou');
    confere('presença: não-texto é recusado', !pres(42) && !pres(undefined), 'aceitou');

    const tela = x => chama(v.ligarOpcional, x) === true;
    confere('tela cheia: vazio, null, true e false são aceitos', tela(undefined) && tela(null) && tela(true) && tela(false), 'recusou');
    confere('tela cheia: "sim" e 1 são recusados', !tela('sim') && !tela(1), 'aceitou');

    const rem = u => chama(v.remetente, u) === true;
    confere('remetente app://jogo/ é aceito', rem('app://jogo/index.html'), 'recusou');
    for (const ruim of ['file:///home/x/index.html', 'https://evil.example/', 'app://outro/', 'app://jogox/', 'app://jogo.evil/', 'https://jogo/', 'about:blank', '', null, undefined])
        confere(`remetente ${rotulo(ruim)} é recusado`, !rem(ruim), 'aceitou');

    // O porteiro em si: `aceitar(ev, canal, argumentosOk, respostaSeRecusar)`, com evento falso.
    const evIpc = url => (url === undefined ? { senderFrame: null } : { senderFrame: { url } });
    const aceita = (...a) => chama(Seguro.aceitar, ...a);
    const bom = evIpc('app://jogo/index.html');
    confere('porteiro: remetente app://jogo/ + argumentos ok → atende', aceita(bom, 'c', true) === true && !('returnValue' in bom), `veio ${rotulo(aceita(evIpc('app://jogo/index.html'), 'c', true))}`);
    for (const url of ['https://evil.example/', 'app://jogox/', 'file:///x/index.html', undefined]) {
        const marca = avisos.length, ev = evIpc(url);
        confere(`porteiro: remetente ${rotulo(url)} → recusa e avisa`, aceita(ev, 'canal:x', true) === false && avisouDesde(marca, 'canal:x'), 'atendeu ou não avisou');
    }
    for (const args of [false, undefined, 'sim', 1]) {
        const marca = avisos.length;
        confere(`porteiro: argumentosOk ${rotulo(args)} → recusa e avisa`, aceita(evIpc('app://jogo/index.html'), 'canal:y', args) === false && avisouDesde(marca, 'canal:y'), 'atendeu ou não avisou');
    }
    const sincrono = evIpc('https://evil.example/');
    aceita(sincrono, 'c', true, null);
    confere('porteiro: síncrono recusado ainda responde (senão a página trava)', 'returnValue' in sincrono && sincrono.returnValue === null, JSON.stringify(sincrono));
    const sincronoArgs = evIpc('app://jogo/index.html');
    aceita(sincronoArgs, 'c', false, false);
    confere('porteiro: síncrono com argumento ruim responde a resposta de recusa', sincronoArgs.returnValue === false, JSON.stringify(sincronoArgs));
    const assincrono = evIpc('https://evil.example/');
    aceita(assincrono, 'c', true);
    confere('porteiro: assíncrono recusado não inventa returnValue', !('returnValue' in assincrono), JSON.stringify(assincrono));
}

// ── 3b. A NAVEGAÇÃO: o handler de verdade, com evento falso ─────────────────────────────────
{
    const evNav = url => ({ url, impedido: false, preventDefault() { this.impedido = true; } });
    const navega = (fn, url) => { const ev = evNav(url); const r = chama(fn, ev); return r === 'SEM FUNÇÃO' || String(r).startsWith('LANÇOU') ? r : ev.impedido; };
    for (const url of ['app://jogo/index.html', 'app://jogo/js/motor.js'])
        confere(`navegação: ${url} segue`, navega(Seguro.bloquearForaDoJogo, url) === false, `veio ${rotulo(navega(Seguro.bloquearForaDoJogo, url))}`);
    for (const url of ['https://evil.example/', 'http://jogo/', 'file:///etc/passwd', 'app://outro/', 'app://jogox/', 'app://jogo.evil/x', 'about:blank', 'javascript:alert(1)', '', undefined]) {
        const marca = avisos.length;
        confere(`navegação: ${rotulo(url)} é impedida e avisa`, navega(Seguro.bloquearForaDoJogo, url) === true && avisouDesde(marca, 'navegação bloqueada'), `veio ${rotulo(navega(Seguro.bloquearForaDoJogo, url))}`);
    }
    for (const url of ['app://jogo/index.html', 'https://evil.example/'])
        confere(`webview: impedido sempre (${url})`, navega(Seguro.bloquearWebview, url) === true, `veio ${rotulo(navega(Seguro.bloquearWebview, url))}`);
}

// ── 4. O SALVAMENTO: o .bak salva o jogador quando o principal corrompe ──────────────────────
{
    const escolher = (a, b) => chama(Seguro.escolherProgresso, a, b) || {};
    const bom = '{"recorde":900}', velho = '{"recorde":500}', podre = '{"recorde":9';
    confere('principal bom: usa o principal', escolher(bom, velho).texto === bom && escolher(bom, velho).origem === 'principal', JSON.stringify(escolher(bom, velho)));
    const r1 = escolher(podre, velho);
    confere('principal corrompido: usa o .bak e avisa', r1.texto === velho && r1.origem === 'reserva' && typeof r1.aviso === 'string' && r1.aviso.length > 0, JSON.stringify(r1));
    const r2 = escolher(podre, podre);
    confere('os dois corrompidos: começa do zero e avisa', r2.texto === null && typeof r2.aviso === 'string', JSON.stringify(r2));
    const r3 = escolher(null, null);
    confere('nenhum arquivo: começa do zero sem alarme', r3.texto === null && !r3.aviso, JSON.stringify(r3));
    const r4 = escolher(null, velho);
    confere('sem principal (apagado de propósito): não ressuscita o .bak', r4.texto === null, JSON.stringify(r4));
    confere('só vira .bak o que é JSON válido (não troca o bom pelo podre)', chama(Seguro.valeGuardarComoReserva, bom) === true && chama(Seguro.valeGuardarComoReserva, podre) === false, 'aceitou o podre ou recusou o bom');
}

// ── 4b. O SALVAMENTO com arquivo de verdade (pasta temporária, apagada no fim) ──────────────
const pastasTemporarias = [];
function pastaNova() { const p = fs.mkdtempSync(path.join(os.tmpdir(), 'punhos-desktop-')); pastasTemporarias.push(p); return p; }
function lerArquivo(p) {
    try { return fs.readFileSync(p, 'utf8'); }
    catch (erro) { if (erro.code === 'ENOENT') return null; return 'ERRO ' + erro.code; }
}
{
    const salvar = (pasta, t) => chama(Seguro.salvarProgresso, pasta, t);
    const carregar = pasta => chama(Seguro.carregarProgresso, pasta);
    const v500 = '{"recorde":500}', v900 = '{"recorde":900}', v1200 = '{"recorde":1200}', podre = '{"recorde":9';

    const vazia = pastaNova();
    let marca = avisos.length;
    confere('disco: pasta sem progresso carrega null, sem alarme', carregar(vazia) === null && !avisouDesde(marca, 'progresso'), `veio ${rotulo(carregar(vazia))}`);

    const pasta = pastaNova();
    const ok1 = salvar(pasta, v500), ok2 = salvar(pasta, v900);
    const principal = path.join(pasta, 'progresso.json'), reserva = principal + '.bak';
    confere('disco: salvar devolve true quando grava', ok1 === true && ok2 === true, `veio ${rotulo([ok1, ok2])}`);
    confere('disco: salvar duas vezes deixa o anterior no .bak', lerArquivo(principal) === v900 && lerArquivo(reserva) === v500, `principal ${rotulo(lerArquivo(principal))} · .bak ${rotulo(lerArquivo(reserva))}`);
    confere('disco: carregar devolve o principal', carregar(pasta) === v900, `veio ${rotulo(carregar(pasta))}`);
    confere('disco: não sobram .tmp', !fs.existsSync(principal + '.tmp') && !fs.existsSync(reserva + '.tmp'), fs.readdirSync(pasta).join(', '));

    fs.writeFileSync(principal, podre);
    marca = avisos.length;
    confere('disco: principal corrompido carrega o .bak e avisa', carregar(pasta) === v500 && avisouDesde(marca, 'corrompido'), `veio ${rotulo(carregar(pasta))}`);
    salvar(pasta, v1200);
    confere('disco: salvar por cima de um principal podre NÃO troca o .bak bom', lerArquivo(principal) === v1200 && lerArquivo(reserva) === v500, `principal ${rotulo(lerArquivo(principal))} · .bak ${rotulo(lerArquivo(reserva))}`);

    // Principal que existe mas não abre (aqui: é uma pasta → EISDIR) não é "jogo novo".
    const ilegivel = pastaNova();
    fs.writeFileSync(path.join(ilegivel, 'progresso.json.bak'), v500);
    fs.mkdirSync(path.join(ilegivel, 'progresso.json'));
    marca = avisos.length;
    confere('disco: principal ilegível (não é ENOENT) carrega o .bak e avisa', carregar(ilegivel) === v500 && avisouDesde(marca, 'progresso'), `veio ${rotulo(carregar(ilegivel))}`);
    marca = avisos.length;
    const okIlegivel = salvar(ilegivel, v900);
    confere('disco: salvar que não consegue gravar devolve false, avisa e não estraga o .bak', okIlegivel === false && avisouDesde(marca, 'progresso') && lerArquivo(path.join(ilegivel, 'progresso.json.bak')) === v500, `veio ${rotulo(okIlegivel)} · .bak ${rotulo(lerArquivo(path.join(ilegivel, 'progresso.json.bak')))}`);

    const funda = path.join(pastaNova(), 'ainda', 'nao', 'existe');
    confere('disco: salvar cria a pasta do userData se faltar', salvar(funda, v500) === true && lerArquivo(path.join(funda, 'progresso.json')) === v500, lerArquivo(path.join(funda, 'progresso.json')));

    // Durabilidade: todo arquivo renomeado por cima do principal/.bak passou por fsync antes.
    // Sem isso, numa queda de energia logo depois de salvar o rename chega ao disco antes dos
    // dados e os DOIS arquivos voltam com 0 byte (reproduzido em ext4 noauto_da_alloc; NTFS não
    // tem a heurística do ext4). Espia o `fs` do Node — o mesmo objeto que o caminho-seguro usa.
    // `writeFileSync(..., {encoding:'utf8', flush:true})` NÃO conta: com string o Node 22/24 pega
    // o atalho em C++ que ignora o flush (visto no strace).
    const espiados = ['openSync', 'writeSync', 'fsyncSync', 'fdatasyncSync', 'writeFileSync', 'renameSync'];
    const originais = Object.fromEntries(espiados.map(n => [n, fs[n]]));
    const porFd = new Map(), sujos = new Set(), renomeios = [];
    const chave = p => path.resolve(String(p));
    fs.openSync = function (p, flags, ...resto) { const fd = originais.openSync.call(fs, p, flags, ...resto); porFd.set(fd, chave(p)); if (/[wa+]/.test(String(flags || 'r'))) sujos.add(chave(p)); return fd; };
    fs.writeSync = function (fd, ...resto) { if (porFd.has(fd)) sujos.add(porFd.get(fd)); return originais.writeSync.call(fs, fd, ...resto); };
    fs.fsyncSync = function (fd) { sujos.delete(porFd.get(fd)); return originais.fsyncSync.call(fs, fd); };
    fs.fdatasyncSync = function (fd) { sujos.delete(porFd.get(fd)); return originais.fdatasyncSync.call(fs, fd); };
    fs.writeFileSync = function (p, ...resto) { if (typeof p !== 'number') sujos.add(chave(p)); return originais.writeFileSync.call(fs, p, ...resto); };
    fs.renameSync = function (de, para) { renomeios.push({ de: path.basename(String(de)), sincronizado: !sujos.has(chave(de)) }); return originais.renameSync.call(fs, de, para); };
    try {
        const duravel = pastaNova();
        salvar(duravel, v500); salvar(duravel, v900);
    } finally { Object.assign(fs, originais); }
    const semFsync = renomeios.filter(r => !r.sincronizado).map(r => r.de);
    confere(`disco: todo arquivo passa por fsync antes do rename (${renomeios.length - semFsync.length} de ${renomeios.length})`, renomeios.length >= 3 && semFsync.length === 0, semFsync.length ? 'renomeou sem fsync: ' + semFsync.join(', ') : 'não renomeou nada');
}

// ── 5. main.js e preload.js, por texto ───────────────────────────────────────────────────────
// Os checks positivos rodam sobre o main SEM comentário: linha comentada (`// sandbox: true,`)
// não protege nada. O corte só pega `//` no começo da linha ou depois de espaço, então
// `'app://jogo/'` fica. Os negativos rodam no texto cru: nem comentado o `webSecurity: false` entra.
function semComentario(codigo) { return codigo.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|\s)\/\/.*$/gm, '$1'); }
const normalizar = t => t.replace(/\s+/g, ' ').trim();
{
    const mainCru = ler(path.join(pastaDesktop, 'main.js'));
    const preload = ler(path.join(pastaDesktop, 'preload.js'));
    const main = semComentario(mainCru);
    const ambos = mainCru + '\n' + preload;
    confere('main.js e preload.js existem', mainCru.length > 0 && preload.length > 0, 'faltou arquivo');

    confere('main: registra o esquema app:// como privilegiado', /protocol\.registerSchemesAsPrivileged\(/.test(main), 'não achou registerSchemesAsPrivileged');
    confere('main: o registro vem ANTES do app ficar pronto', main.indexOf('registerSchemesAsPrivileged') > -1 && main.indexOf('registerSchemesAsPrivileged') < main.indexOf('app.whenReady'), 'fora de ordem');
    confere("main: app:// é atendido pelo servirDoJogo (testado na seção 7), sem Response montada à mão",
        /protocol\.handle\(\s*'app'\s*,\s*(\w+)\s*=>\s*servirDoJogo\(\s*RAIZ_DO_JOGO\s*,\s*\1\s*\)\s*\)/.test(main) && !/new\s+Response\b/.test(main),
        "quer protocol.handle('app', p => servirDoJogo(RAIZ_DO_JOGO, p)) e nenhum new Response no main");
    confere('main: carrega app://jogo/, não file://', /loadURL\(\s*'app:\/\/jogo\//.test(main) && !/loadFile\(/.test(main), 'ainda usa loadFile ou não usa app://');

    // O main só liga fios: cada decisão vem do caminho-seguro, que as seções acima chamam de verdade.
    const importados = ((main.match(/const\s*\{([^}]*)\}\s*=\s*require\(\s*'\.\/caminho-seguro'\s*\)/) || [])[1] || '').split(',').map(n => n.trim()).filter(Boolean);
    for (const nome of ['servirDoJogo', 'aceitar', 'validar', 'bloquearForaDoJogo', 'bloquearWebview', 'salvarProgresso', 'carregarProgresso'])
        confere(`main: ${nome} vem do caminho-seguro e não é redefinido`, importados.includes(nome) && !new RegExp(`(function\\s+${nome}\\b|(const|let|var)\\s+${nome}\\s*=)`).test(main), importados.includes(nome) ? 'redefinido no main' : 'não importado');
    confere('main: não toca o disco — toda E/S passa pelo caminho-seguro testado', main.length > 0 && !/require\(\s*'fs'\s*\)/.test(main) && !/\bfs\./.test(main), 'achou fs no main');

    const blocoWP = (main.match(/webPreferences:\s*\{([^{}]*)\}/) || [])[1] || '';
    confere('main: tem um bloco webPreferences', blocoWP.length > 0, 'não achou');
    for (const [nome, re] of [
        ['sandbox: true', /sandbox:\s*true/], ['contextIsolation: true', /contextIsolation:\s*true/],
        ['nodeIntegration: false', /nodeIntegration:\s*false/], ['webSecurity: true', /webSecurity:\s*true/],
        ['spellcheck: false', /spellcheck:\s*false/], ['devTools: !app.isPackaged', /devTools:\s*!app\.isPackaged/],
    ]) confere(`main: webPreferences com ${nome}`, re.test(blocoWP), 'não achou (fora de comentário, dentro do webPreferences)');
    for (const [nome, re] of [
        ['sandbox: false', /sandbox:\s*false/], ['webSecurity: false', /webSecurity:\s*false/],
        ['allowRunningInsecureContent: true', /allowRunningInsecureContent:\s*true/], ['nodeIntegration: true', /nodeIntegration:\s*true/],
        ['contextIsolation: false', /contextIsolation:\s*false/], ['nodeIntegrationInSubFrames: true', /nodeIntegrationInSubFrames:\s*true/],
        ['webviewTag: true', /webviewTag:\s*true/], ['experimentalFeatures: true', /experimentalFeatures:\s*true/],
        ['enableBlinkFeatures', /enableBlinkFeatures/], ['loadFile(', /\bloadFile\(/],
    ]) confere(`nenhuma ocorrência de ${nome}`, ambos.length > 0 && !re.test(ambos), 'achou');

    confere("main: window.open é negado (setWindowOpenHandler → 'deny')", /setWindowOpenHandler\([^)]*\)\s*=>\s*\(?\s*\{\s*action:\s*'deny'/.test(main) || /setWindowOpenHandler\([\s\S]{0,300}action:\s*'deny'/.test(main), 'não achou');
    // O fio exato: o evento chama o handler testado na seção 3b, passando o evento. (Passar a
    // função direto não serve: o Electron manda a url como 2º argumento, na forma legada.)
    const naCriacao = (main.match(/app\.on\(\s*'web-contents-created'[\s\S]*?\n\}\);/) || [''])[0];
    for (const [evento, fn] of [['will-navigate', 'bloquearForaDoJogo'], ['will-redirect', 'bloquearForaDoJogo'], ['will-attach-webview', 'bloquearWebview']])
        confere(`main: ${evento} passa pelo ${fn}, em todo webContents criado`, new RegExp(`\\.on\\(\\s*'${evento}'\\s*,\\s*(\\w+)\\s*=>\\s*${fn}\\(\\s*\\1\\s*\\)\\s*\\)`).test(naCriacao), `quer conteudo.on('${evento}', ev => ${fn}(ev)) dentro do web-contents-created`);
    confere('main: permissões pedidas são negadas (setPermissionRequestHandler)', /setPermissionRequestHandler\(/.test(main), 'não achou');
    confere('main: permissões checadas são negadas (setPermissionCheckHandler)', /setPermissionCheckHandler\(/.test(main), 'não achou');
    // `spellcheck: false` não basta: no Linux o Chromium baixa o en-us .bdic de redirector.gvt1.com
    // a cada abertura sem cache (visto no --log-net-log). `setSpellCheckerEnabled(false)` também não
    // impede, e trocar a URL de download só muda o destino. Lista de idiomas vazia, sim — e nenhuma
    // outra chamada pode devolver um idioma.
    const idiomas = main.match(/setSpellCheckerLanguages\([^)]*\)/g) || [];
    confere('main: corretor sem idioma nenhum (session.defaultSession.setSpellCheckerLanguages([])), pro desktop não baixar dicionário',
        /session\.defaultSession\.setSpellCheckerLanguages\(\s*\[\s*\]\s*\)/.test(main) && idiomas.every(c => /\(\s*\[\s*\]\s*\)/.test(c)),
        idiomas.length ? `chamada com idioma: ${idiomas.join(' | ')}` : 'não achou');
    // O dicionário é pedido quando a sessão sobe: a chamada precisa vir antes da janela nascer.
    const noPronto = (main.match(/app\.whenReady\(\)\.then\(\(\)\s*=>\s*\{([\s\S]*?)\n\}\);/) || [])[1] || '';
    const corpoNegar = (main.match(/function\s+negarPermissoes\s*\(\)\s*\{([\s\S]*?)\n\}/) || [])[1] || '';
    confere('main: o corretor é zerado antes de criarJanela() (no whenReady ou no negarPermissoes chamado antes)',
        noPronto.includes('criarJanela()') && (
            (noPronto.includes('setSpellCheckerLanguages') && noPronto.indexOf('setSpellCheckerLanguages') < noPronto.indexOf('criarJanela()')) ||
            (corpoNegar.includes('setSpellCheckerLanguages') && noPronto.includes('negarPermissoes()') && noPronto.indexOf('negarPermissoes()') < noPronto.indexOf('criarJanela()'))),
        'não achou a chamada antes de criarJanela()');
    // Cada ipcMain.on: a 1ª instrução é o porteiro, com o PRÓPRIO canal, o validador daquele canal
    // e `return` se recusar. O mapa amarra canal → argumentos do porteiro: canal novo sem entrada
    // aqui fica vermelho, e trocar um validador por `true` também.
    const PORTEIRO = {
        'progresso:carregar': 'true, null',
        'progresso:salvar': 'validar.textoDeProgresso(texto)',
        'steam:ativo': 'true, false',
        'steam:conquistar': 'validar.idDaSteam(id)',
        'steam:estatistica': 'validar.idDaSteam(nome) && validar.numeroFinito(valor)',
        'steam:presenca': 'validar.presenca(texto)',
        'app:sair': 'true',
        'app:telaCheia': 'validar.ligarOpcional(ligar)',
    };
    const total = (main.match(/ipcMain\.on\(/g) || []).length;
    const handlers = [...main.matchAll(/ipcMain\.on\(\s*'([^']+)'\s*,\s*(?:\([^)]*\)|\w+)\s*=>\s*\{([\s\S]*?)\n\}\);/g)].map(m => ({ canal: m[1], corpo: normalizar(m[2]) }));
    confere(`main: todo ipcMain.on tem a forma (ev, …) => { … } conferível (${handlers.length} de ${total})`, total >= 8 && handlers.length === total, `${total - handlers.length} fora da forma`);
    const semEntrada = handlers.filter(h => !(h.canal in PORTEIRO)).map(h => h.canal);
    confere('main: todo canal tem o seu validador no mapa do conferidor', semEntrada.length === 0, semEntrada.join(', '));
    for (const [canal, args] of Object.entries(PORTEIRO)) {
        const h = handlers.find(x => x.canal === canal);
        const esperado = `if (!aceitar(ev, '${canal}', ${args})) return;`;
        confere(`main: '${canal}' começa por ${esperado}`, !!h && h.corpo.startsWith(esperado), h ? `começa por ${h.corpo.slice(0, 90)}` : 'handler não existe');
    }
    const corpo = canal => (handlers.find(x => x.canal === canal) || { corpo: '' }).corpo;
    confere("main: progresso:salvar grava pelo salvarProgresso (testado na 4b) na pasta do userData", corpo('progresso:salvar').includes("salvarProgresso(app.getPath('userData'), texto);"), corpo('progresso:salvar'));
    confere("main: progresso:carregar lê pelo carregarProgresso (testado na 4b) na pasta do userData", corpo('progresso:carregar').includes("ev.returnValue = carregarProgresso(app.getPath('userData'));"), corpo('progresso:carregar'));
    confere('main: nenhum canal de IPC fora do porteiro (handle/once/addListener)', main.length > 0 && !/ipcMain\.(handle|handleOnce|once|addListener)\(/.test(main), 'achou');
    const canaisDaPonte = [...preload.matchAll(/ipcRenderer\.(?:send|sendSync)\(\s*'([^']+)'/g)].map(m => m[1]);
    const semHandler = canaisDaPonte.filter(c => !main.includes(`ipcMain.on('${c}'`));
    confere(`todo canal da ponte (${canaisDaPonte.length}) tem handler no main`, canaisDaPonte.length >= 8 && semHandler.length === 0, semHandler.join(', ') || 'ponte vazia');

    confere('preload: só requer electron', preload.length > 0 && [...preload.matchAll(/require\(\s*'([^']+)'\s*\)/g)].every(m => m[1] === 'electron'), 'requer outra coisa');
    confere('preload: expõe pela contextBridge', /contextBridge\.exposeInMainWorld\(/.test(preload), 'não achou');
    confere('preload: não entrega o ipcRenderer cru nem um send(canal) genérico', preload.length > 0 && !/:\s*ipcRenderer\s*[,}\n]/.test(preload) && !/exposeInMainWorld\([^,]+,\s*ipcRenderer\s*\)/.test(preload) && !/ipcRenderer\.(send|sendSync|invoke)\(\s*[a-z_$]/i.test(preload), 'achou');
}

// ── 6. Os fuses do Electron no package.json (electron-builder → build.electronFuses) ─────────
{
    let pacote = {};
    try { pacote = JSON.parse(ler(path.join(raizDoJogo, 'package.json'))); } catch (erro) { console.log(`package.json ilegível: ${erro.message}`); }
    const fuses = (pacote.build && pacote.build.electronFuses) || {};
    const esperados = {
        runAsNode: false, enableNodeOptionsEnvironmentVariable: false, enableNodeCliInspectArguments: false,
        onlyLoadAppFromAsar: true, enableEmbeddedAsarIntegrityValidation: true,
        grantFileProtocolExtraPrivileges: false, enableCookieEncryption: true,
    };
    for (const [nome, valor] of Object.entries(esperados))
        confere(`fuse ${nome} = ${valor}`, fuses[nome] === valor, `veio ${JSON.stringify(fuses[nome])}`);
    const arquivos = (pacote.build && pacote.build.files) || [];
    confere('o empacotado leva o caminho-seguro.js (desktop/**)', arquivos.includes('desktop/**'), JSON.stringify(arquivos));
    confere('o empacotado leva as fontes e a licença delas (fontes/**)', arquivos.includes('fontes/**') && !arquivos.some(a => /^!fontes/.test(a)), JSON.stringify(arquivos));
}

// ── 7. O app:// de verdade: servirDoJogo lendo de uma pasta temporária ─────────────────────
async function conferirServidor() {
    const raiz = pastaNova();
    fs.writeFileSync(path.join(raiz, 'index.html'), '<!doctype html><title>ok</title>');
    fs.mkdirSync(path.join(raiz, 'js'));
    fs.writeFileSync(path.join(raiz, 'js', 'motor.js'), 'var motor = 1;');
    fs.writeFileSync(path.join(raiz, 'package.json'), '{"segredo":1}');
    const servir = async pedido => {
        const r = await chama(Seguro.servirDoJogo, raiz, pedido);
        return r instanceof Response ? { status: r.status, h: r.headers, corpo: await r.text() } : { erro: String(r) };
    };
    const csp = String(Seguro.POLITICA_DE_CONTEUDO || '(sem política)');
    const blindada = r => !!r.h && r.h.get('content-security-policy') === csp && r.h.get('x-content-type-options') === 'nosniff';
    const resumo = r => r.erro || `${r.status} ${r.h.get('content-type')} · csp=${r.h.get('content-security-policy') === csp} · nosniff=${r.h.get('x-content-type-options')}`;

    const pagina = await servir({ url: 'app://jogo/index.html' });
    confere('app://: index.html sai 200, text/html, com o conteúdo', pagina.status === 200 && /^text\/html/.test(pagina.h.get('content-type')) && pagina.corpo.includes('<title>ok'), resumo(pagina));
    confere('app://: a resposta 200 leva a CSP e o nosniff', blindada(pagina), resumo(pagina));
    const script = await servir({ url: 'app://jogo/js/motor.js' });
    confere('app://: js/motor.js sai 200, text/javascript, com CSP e nosniff', script.status === 200 && /^text\/javascript/.test(script.h.get('content-type')) && blindada(script), resumo(script));
    for (const url of ['app://jogo/%2e%2e/package.json', 'app://jogo/package.json', 'https://evil.example/index.html', 'app://jogo/js/nao-existe.js', undefined]) {
        const marca = avisos.length;
        const r = await servir({ url });
        confere(`app://: ${rotulo(url)} sai 404 com CSP e nosniff, e avisa`, r.status === 404 && blindada(r) && !r.corpo.includes('segredo') && avisouDesde(marca, 'app://'), resumo(r));
    }
    // As fontes: o .css como text/css e o .woff2 como font/woff2, byte a byte (é binário).
    fs.mkdirSync(path.join(raiz, 'fontes'));
    fs.writeFileSync(path.join(raiz, 'fontes', 'fontes.css'), "@font-face { font-family: 'X'; src: url(x.woff2); }");
    const binario = Buffer.from(Array.from({ length: 256 }, (_, i) => i));
    fs.writeFileSync(path.join(raiz, 'fontes', 'x.woff2'), binario);
    fs.writeFileSync(path.join(raiz, 'fontes', 'OFL-X.txt'), 'licença');
    const folha = await servir({ url: 'app://jogo/fontes/fontes.css' });
    confere('app://: fontes/fontes.css sai 200, text/css, com CSP e nosniff', folha.status === 200 && /^text\/css/.test(folha.h.get('content-type')) && folha.corpo.includes('@font-face') && blindada(folha), resumo(folha));
    const rFonte = await chama(Seguro.servirDoJogo, raiz, { url: 'app://jogo/fontes/x.woff2' });
    const bytes = rFonte instanceof Response ? Buffer.from(await rFonte.arrayBuffer()) : null;
    confere('app://: fontes/x.woff2 sai 200, font/woff2, byte a byte, com CSP e nosniff',
        !!bytes && rFonte.status === 200 && rFonte.headers.get('content-type') === 'font/woff2' && bytes.equals(binario) && blindada({ h: rFonte.headers }),
        bytes ? `${rFonte.status} ${rFonte.headers.get('content-type')} · ${bytes.length} bytes · iguais=${bytes.equals(binario)}` : String(rFonte));
    for (const url of ['app://jogo/fontes/OFL-X.txt', 'app://jogo/fontes/../package.json', 'app://jogo/fontes/nao-existe.woff2']) {
        const marca = avisos.length;
        const r = await servir({ url });
        confere(`app://: ${rotulo(url)} sai 404 com CSP e nosniff, e avisa`, r.status === 404 && blindada(r) && !r.corpo.includes('segredo') && avisouDesde(marca, 'app://'), resumo(r));
    }
    const semPedido = await servir(null);
    confere('app://: pedido nulo sai 404 (não lança)', semPedido.status === 404 && blindada(semPedido), resumo(semPedido));
}

conferirServidor()
    .catch(erro => confere('a seção 7 rodou até o fim', false, erro.stack))
    .finally(() => {
        for (const p of pastasTemporarias) fs.rmSync(p, { recursive: true, force: true });
        console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
        process.exit(falhas.length === 0 ? 0 : 1);
    });
