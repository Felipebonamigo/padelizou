// AS FICHAS DA PRÉVIA DO MATA-MATA ("OITAVAS · QUARTAS · SEMIFINAL · FINAL"), conferidas
// contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-fichas-da-chave.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo do conferir-palpitrometro.js: este repositório não tem
// npm (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 100 linhas.
//
// O QUE ELE GUARDA (o porquê inteiro está em FichasDaChaveLevamARodadaCertaTests):
//   · a ficha rola o TRILHO DA PRÓPRIA CATEGORIA — com sete categorias na tela, todas com a
//     mesma prévia, mirar no documento inteiro acha o painel escondido e não rola nada;
//   · e ela CANCELA a navegação por âncora, que é quem parava uma rodada atrás (o encaixe
//     `scroll-snap` desfazia o pulo) e ainda levava a barra de fichas pra fora da tela.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/chave-fichas.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
// Geometria em números redondos, na medida do Chromium a 412px: a rodada ocupa 272px e o
// trilho começa em 0. A rodada `i` está em `272 * i` — é o ponto de encaixe dela.
const LARGURA_DA_RODADA = 272;

function elemento(extra) {
    const alvo = Object.assign({
        _ouvintes: {},
        addEventListener(nome, fn) { (this._ouvintes[nome] = this._ouvintes[nome] || []).push(fn); },
        disparar(nome, evento) { (this._ouvintes[nome] || []).forEach((fn) => fn(evento)); },
    }, extra);
    return alvo;
}

function quadro(categoria, rodadas) {
    const trilho = elemento({
        scrollLeft: 0,
        _pedidos: [],
        getBoundingClientRect: () => ({ left: 0 }),
        scrollTo(opcoes) { this._pedidos.push(opcoes); this.scrollLeft = opcoes.left; },
    });

    const porId = {};
    const fichas = [];
    for (let r = 0; r < rodadas; r++) {
        const id = 'pdz-chd-' + categoria + '-rodada-' + r;
        // A rodada mora onde o trilho a desenhou; `left` é relativo à TELA, então já desconta
        // o quanto o trilho está rolado — é assim que o navegador responde de verdade.
        porId['#' + id] = elemento({
            getBoundingClientRect: () => ({ left: LARGURA_DA_RODADA * r - trilho.scrollLeft }),
        });
        fichas.push(elemento({
            _rodada: r,
            getAttribute: (nome) => (nome === 'href' ? '#' + id : null),
        }));
    }

    return elemento({
        _trilho: trilho,
        _fichas: fichas,
        querySelector: (sel) => (sel === '.pdz-chd-trilho' ? trilho : porId[sel] || null),
        querySelectorAll: (sel) => (sel === '.pdz-chd-ficha' ? fichas : []),
    });
}

function rodar(quadros) {
    const doc = {
        readyState: 'complete',
        addEventListener() { },
        querySelectorAll: (sel) => (sel === '.pdz-chd' ? quadros : []),
    };
    // O arquivo é uma IIFE que fala com `window` e `document` globais.
    new Function('window', 'document', 'self', fonte)({}, doc, {});
    return quadros;
}

function clicar(ficha) {
    let cancelou = false;
    ficha.disparar('click', { preventDefault() { cancelou = true; }, target: ficha });
    return cancelou;
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

// 1. DUAS CATEGORIAS NA MESMA TELA (o caso do ER, que tem sete): a ficha da SEGUNDA tem que
//    rolar o trilho da segunda, e não encostar no da primeira.
{
    const primeira = quadro(12, 4);
    const segunda = quadro(13, 4);
    rodar([primeira, segunda]);

    conferir('a ficha cancela a navegação por âncora', clicar(segunda._fichas[1]));
    conferir('rolou o trilho DA SEGUNDA categoria', segunda._trilho._pedidos.length === 1);
    conferir('e não encostou no trilho da primeira', primeira._trilho._pedidos.length === 0);
    conferir('parou no começo das QUARTAS (272px)', segunda._trilho.scrollLeft === 272);

    // A FINAL é o pulo longo, e o de sempre errar: com a âncora ele parava na semifinal.
    clicar(segunda._fichas[3]);
    conferir('a FINAL para no começo da FINAL (816px)', segunda._trilho.scrollLeft === 816);

    // E voltar pras OITAVAS tem que voltar ao zero — a conta é relativa à tela, então uma
    // rolagem que só soubesse somar ficaria presa na ponta direita.
    clicar(segunda._fichas[0]);
    conferir('voltando, as OITAVAS param no zero', segunda._trilho.scrollLeft === 0);

    conferir('a primeira categoria seguiu intocada o tempo todo', primeira._trilho.scrollLeft === 0);
}

// 2. CADA CATEGORIA CUIDA DA SUA: clicar na ficha da primeira rola a primeira.
{
    const primeira = quadro(12, 3);
    const segunda = quadro(13, 3);
    rodar([primeira, segunda]);

    clicar(primeira._fichas[2]);
    conferir('a ficha da primeira rola a primeira', primeira._trilho.scrollLeft === 544);
    conferir('e a segunda não se mexe', segunda._trilho.scrollLeft === 0);
}

// 3. TELA SEM PRÉVIA NENHUMA (torneio com o mata-mata já sorteado): o script tem que ligar
//    calado, não estourar — ele é carregado na página inteira.
{
    let estourou = false;
    try { rodar([]); } catch (e) { estourou = true; }
    conferir('sem prévia na tela, o script fica quieto', !estourou);
}

console.log('');
console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
process.exit(falhas === 0 ? 0 : 1);
