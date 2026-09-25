// PUNHOS DE SHAOLIN — toda decisão de segurança do processo principal, sem Electron.
//
// Quatro portas por onde a página (o conteúdo menos confiável do app) chega ao disco e ao sistema:
//   1. o protocolo app:// — qual arquivo servir para uma URL pedida, e com que cabeçalhos;
//   2. o IPC — o porteiro `aceitar` e os validadores de argumento de cada mensagem;
//   3. a navegação — o que acontece quando a página tenta sair de app://jogo/;
//   4. o salvamento — progresso.json + .bak em disco, e qual texto vale quando o principal corrompe.
// Nada aqui requer `electron` (só `path`, `fs` e o `Response` global do Node 18+): é o que deixa
// o `conferir-desktop-do-shaolin.js` chamar cada função de verdade no CI, com ataque, com pedido
// legítimo e com arquivo de verdade. O `main.js` só liga os fios.
//
// Chame o `fs` sempre pelo objeto (`fs.openSync`, não desestruturado): o conferidor espia essas
// chamadas pra provar que todo arquivo passa por fsync antes do rename.
'use strict';
const path = require('path');
const fs = require('fs');

// O jogo inteiro é `index.html` + `js/*.js` + `fontes/*.woff2` e `fontes/fontes.css`. Lista
// branca, não lista negra: um pedido que não tem essa forma exata é recusado ANTES de virar
// caminho — `..`, `%2e%2e`, barra invertida, `C:/`, byte nulo e pasta nova caem todos aqui, sem
// precisar ser enumerados. A licença (fontes/OFL-*.txt) vai no empacotado, mas não é servida.
const ARQUIVO_DO_JOGO = /^(index\.html|js\/[a-z0-9_-]+\.js|fontes\/[a-z0-9_-]+\.(woff2|css))$/i;
const TAMANHO_MAXIMO_DO_PEDIDO = 256;

const TIPOS = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.woff2': 'font/woff2',
};

// Toda resposta do app:// sai com isto. `style-src 'unsafe-inline'` é pelo <style> do index.html
// (estilo não executa código); script só de arquivo do próprio app, nunca inline nem eval. Nada
// sai pra rede: as fontes vêm empacotadas em fontes/ (antes vinham do Google Fonts).
const POLITICA_DE_CONTEUDO = [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    "font-src 'self'",
    "img-src 'self' data:",
    "connect-src 'none'",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'none'",
    "frame-ancestors 'none'",
].join('; ');

const ORIGEM = 'app://jogo';

// `pedido` é o pathname ainda codificado (ex.: "/js/motor.js"). Devolve o caminho absoluto
// dentro de `raiz`, ou null.
function resolverCaminho(raiz, pedido) {
    if (typeof raiz !== 'string' || typeof pedido !== 'string') return null;
    if (pedido.length === 0 || pedido.length > TAMANHO_MAXIMO_DO_PEDIDO || pedido[0] !== '/') return null;
    // Nenhum escape é aceito: os nomes do jogo não têm nada que precise de %. Isso fecha
    // %2e%2e, %2f, %5c, %00 e a codificação dupla (%252e) de uma vez.
    if (/[%\\\0]/.test(pedido)) return null;
    const relativo = pedido === '/' ? 'index.html' : pedido.slice(1);
    if (!ARQUIVO_DO_JOGO.test(relativo)) return null;
    // Cinto e suspensório: mesmo passando na lista branca, o resolvido tem que ficar dentro.
    const base = path.resolve(raiz);
    const destino = path.resolve(base, ...relativo.split('/'));
    return destino.startsWith(base + path.sep) ? destino : null;
}

function resolverUrl(raiz, url) {
    if (typeof url !== 'string') return null;
    let u;
    try { u = new URL(url); } catch (_) { return null; } // URL malformada é só um pedido recusado.
    if (u.protocol !== 'app:' || u.host !== 'jogo') return null;
    return resolverCaminho(raiz, u.pathname);
}

function tipoDoArquivo(arquivo) { return TIPOS[path.extname(String(arquivo)).toLowerCase()] || 'application/octet-stream'; }

// O handler do protocol.handle('app', …): TODA resposta — 200 ou 404 — sai com a CSP e o nosniff,
// porque os cabeçalhos só são montados aqui.
function responder(corpo, status, tipo) {
    const cabecalhos = { 'Content-Security-Policy': POLITICA_DE_CONTEUDO, 'X-Content-Type-Options': 'nosniff' };
    if (tipo) cabecalhos['Content-Type'] = tipo;
    return new Response(corpo, { status, headers: cabecalhos });
}

async function servirDoJogo(raiz, pedido) {
    const url = pedido && pedido.url;
    const arquivo = resolverUrl(raiz, url);
    if (!arquivo) {
        console.warn(`app://: pedido recusado (fora do jogo): ${url}`);
        return responder('não encontrado', 404);
    }
    try {
        return responder(await fs.promises.readFile(arquivo), 200, tipoDoArquivo(arquivo));
    } catch (erro) {
        console.warn(`app://: não deu pra ler ${url}:`, erro.message);
        return responder('não encontrado', 404);
    }
}

// ── IPC: cada validador responde true/false, nunca lança ──────────────────────────────────
const UM_MEGA = 1024 * 1024;

function ehJsonDeObjeto(texto) {
    try { const v = JSON.parse(texto); return v !== null && typeof v === 'object' && !Array.isArray(v); }
    catch (_) { return false; } // JSON inválido É a resposta (false); quem chama avisa no log.
}

const validar = {
    remetente: url => typeof url === 'string' && url.startsWith(ORIGEM + '/'),
    textoDeProgresso: texto => typeof texto === 'string' && Buffer.byteLength(texto, 'utf8') <= UM_MEGA && ehJsonDeObjeto(texto),
    idDaSteam: id => typeof id === 'string' && /^[a-z_]{1,64}$/.test(id),
    numeroFinito: n => typeof n === 'number' && Number.isFinite(n),
    presenca: texto => typeof texto === 'string' && texto.length <= 200,
    ligarOpcional: v => v === undefined || v === null || typeof v === 'boolean',
};

// O porteiro: a mensagem só é atendida se veio de uma página app://jogo/ E os argumentos passaram
// no validador do canal (`argumentosOk` tem que ser exatamente true — esquecer o argumento
// recusa). Recusada, vai pro log e não executa nada. Mensagem SÍNCRONA (sendSync) recusada ainda
// responde `respostaSeRecusar` — sem resposta a página travaria.
function aceitar(ev, canal, argumentosOk, respostaSeRecusar) {
    let motivo = null;
    if (!validar.remetente(ev.senderFrame && ev.senderFrame.url)) motivo = `remetente fora de ${ORIGEM}/`;
    else if (argumentosOk !== true) motivo = 'argumentos inválidos';
    if (!motivo) return true;
    console.warn(`IPC: '${canal}' recusado — ${motivo}`);
    if (respostaSeRecusar !== undefined) ev.returnValue = respostaSeRecusar;
    return false;
}

// ── Navegação: a página não sai de app://jogo/ ────────────────────────────────────────────
// Recebem o evento do Electron (will-navigate/will-redirect têm `ev.url`); nada de 2º argumento,
// porque o Electron ainda manda a url solta ali, na forma legada.
function bloquearForaDoJogo(ev) {
    if (validar.remetente(ev.url)) return;
    console.warn(`navegação bloqueada: ${ev.url}`);
    ev.preventDefault();
}
function bloquearWebview(ev) {
    console.warn('webview bloqueado');
    ev.preventDefault();
}

// ── Salvamento ────────────────────────────────────────────────────────────────────────────
// `principal` e `reserva` são o conteúdo de progresso.json e progresso.json.bak (null se não
// existem). Sem principal não se ressuscita o .bak: arquivo ausente é jogo novo ou apagado de
// propósito — e a gravação nunca deixa o principal ausente (ela troca por rename).
function escolherProgresso(principal, reserva) {
    if (principal == null) return { texto: null, origem: 'nenhum' };
    if (ehJsonDeObjeto(principal)) return { texto: principal, origem: 'principal' };
    if (reserva != null && ehJsonDeObjeto(reserva))
        return { texto: reserva, origem: 'reserva', aviso: 'progresso.json corrompido — carregando o progresso.json.bak' };
    return { texto: null, origem: 'nenhum', aviso: 'progresso.json corrompido e sem .bak válido — começando do zero' };
}

// Só vira .bak um principal que ainda é bom: gravar por cima de um .bak bom com um principal
// podre jogaria fora a única cópia que presta.
function valeGuardarComoReserva(textoAtual) { return typeof textoAtual === 'string' && ehJsonDeObjeto(textoAtual); }

const NOME_DO_PROGRESSO = 'progresso.json';

// null = não existe. Existe mas não abre (permissão, disco, é pasta) vira '' — "corrompido" — pra
// que o carregar caia no .bak em vez de começar do zero por cima do progresso do jogador.
function lerSeExiste(arquivo) {
    try { return fs.readFileSync(arquivo, 'utf8'); }
    catch (erro) {
        if (erro.code === 'ENOENT') return null;
        console.warn(`progresso: não deu pra ler ${path.basename(arquivo)}:`, erro.message);
        return '';
    }
}

// `pasta` é o userData do app. Devolve o texto do progresso, ou null (jogo novo).
function carregarProgresso(pasta) {
    const destino = path.join(pasta, NOME_DO_PROGRESSO);
    const escolha = escolherProgresso(lerSeExiste(destino), lerSeExiste(destino + '.bak'));
    if (escolha.aviso) console.warn('progresso:', escolha.aviso);
    return escolha.texto;
}

// Grava e só volta depois do fsync: os dados estão no disco ANTES do rename que os publica.
// Sem isso, numa queda de energia logo depois de salvar, o rename (que vai pro journal) pode
// chegar ao disco antes dos dados e o principal e o .bak voltam os dois com 0 byte.
// Não dá pra trocar por `writeFileSync(arq, texto, { encoding: 'utf8', flush: true })`: com
// string em utf8 o Node 22/24 (Electron 44) pega um atalho em C++ que IGNORA o flush.
function gravarNoDisco(arquivo, texto) {
    const dados = Buffer.from(texto, 'utf8');
    const fd = fs.openSync(arquivo, 'w');
    try {
        let gravado = 0;
        while (gravado < dados.length) gravado += fs.writeSync(fd, dados, gravado, dados.length - gravado);
        fs.fsyncSync(fd);
    } finally { fs.closeSync(fd); }
}

// Cada passo grava ao lado (com fsync) e renomeia: nem um desligamento do processo nem uma queda
// de energia no meio deixam arquivo pela metade ou o principal ausente — o fsync antes do rename
// é o que cobre a queda de energia. Atalho: a pasta em si não passa por fsync (no Windows não se
// abre pasta); o teto é, numa queda logo depois de salvar, voltar o salvamento ANTERIOR inteiro.
// O anterior vira .bak — mas só se ainda for JSON válido.
// Devolve true se gravou; false (com aviso no log) se não deu.
function salvarProgresso(pasta, texto) {
    const destino = path.join(pasta, NOME_DO_PROGRESSO);
    try {
        fs.mkdirSync(pasta, { recursive: true });
        gravarNoDisco(destino + '.tmp', texto);
        const atual = lerSeExiste(destino);
        if (valeGuardarComoReserva(atual)) {
            gravarNoDisco(destino + '.bak.tmp', atual);
            fs.renameSync(destino + '.bak.tmp', destino + '.bak');
        } else if (atual !== null) {
            console.warn('progresso: o progresso.json anterior estava corrompido — o .bak fica como estava');
        }
        fs.renameSync(destino + '.tmp', destino);
        return true;
    } catch (erro) {
        console.warn('progresso: não deu pra gravar', erro.message);
        return false;
    }
}

module.exports = {
    ORIGEM, POLITICA_DE_CONTEUDO, resolverCaminho, resolverUrl, tipoDoArquivo, servirDoJogo,
    validar, aceitar, bloquearForaDoJogo, bloquearWebview,
    escolherProgresso, valeGuardarComoReserva, carregarProgresso, salvarProgresso,
};
