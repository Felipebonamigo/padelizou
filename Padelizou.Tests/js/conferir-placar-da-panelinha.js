// O PLACAR DIGITADO NÃO SOBREVIVE A UMA TROCA DE VENCEDOR, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-placar-da-panelinha.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`. Sem dependência nenhuma: este repositório não tem npm.
//
// ── O QUE ELE GUARDA ──────────────────────────────────────────────────────────────────────
//
// 🗣️ Um jogador da panelinha Sub 90, 30/09/2026: *"To preenchendo lá clico em salvar e não dá
// em nada"* · *"Só apaga os dados que coloquei"*, com o aviso "O placar diz que a OUTRA dupla
// venceu" preso na tela.
//
// 🕳️ A tela já ZERA O VENCEDOR quando as duplas se desfazem — e nunca zerou o PLACAR. Quem
// digita 6 x 3, se dá conta de que marcou o lado errado e toca na outra dupla leva o 6 x 3
// junto: o número fica preso à POSIÇÃO ("Games Dupla 1"), enquanto os rótulos se redesenham com
// os nomes novos. O servidor então recusa o lançamento, com razão — o placar realmente
// contradiz o vencedor —, e a recusa chega como formulário em branco.
//
// ⚠️ NÃO É "limpar por garantia": é a mesma regra que já vale pro vencedor. O que descreve um
// jogo — quem ganhou e por quanto — não pode sobreviver à troca de quem está jogando, senão a
// tela afirma um resultado que ninguém digitou.
const fs = require('fs');

const FONTE = fs.readFileSync('Padelizou/wwwroot/js/registrar-jogo-da-panelinha.js', 'utf8');

let falhas = 0;
function ok(condicao, texto) {
    console.log((condicao ? '  ok  ' : ' FALHA') + ' · ' + texto);
    if (!condicao) falhas++;
}

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
//
// Só o que este arquivo usa. Um nó guarda classes, filhos, valor e o `onclick` — é por ele que
// o teste "toca" na tela, do mesmo jeito que o dedo do jogador.
function No(classes) {
    const set = new Set((classes || '').split(' ').filter(Boolean));
    const no = {
        filhos: [],
        value: '',
        name: '',
        textContent: '',
        innerHTML: '',
        disabled: false,
        dataset: {},
        style: {},
        setAttribute: () => {},
        onclick: null,
        classList: {
            add: (c) => set.add(c),
            remove: (c) => set.delete(c),
            contains: (c) => set.has(c),
            toggle: (c, ligado) => (ligado ? set.add(c) : set.delete(c)),
        },
        appendChild: (f) => { no.filhos.push(f); return f; },
        temClasse: (c) => set.has(c),
    };

    function todos(raiz) {
        return raiz.filhos.reduce((lista, f) => lista.concat([f], todos(f)), []);
    }

    no.querySelector = (sel) => no.querySelectorAll(sel)[0] || null;
    no.querySelectorAll = (sel) => {
        const classe = sel.replace(/^\./, '');
        return todos(no).filter((f) => f.temClasse(classe));
    };

    return no;
}

// O molde de um jogo, igual ao <template> de Views/Grupos/RegistrarJogo.cshtml. As classes
// aqui e as de lá precisam bater — quem guarda isso pelo outro lado é PlacarRecusadoNaPanelinhaTests.
function moldeDeJogo() {
    const raiz = No('pdz-jogo');
    [
        'pdz-jogo-cabecalho', 'pdz-jogo-titulo', 'pdz-jogo-remover',
        'pdz-c-d1j1', 'pdz-c-d1j2', 'pdz-c-d2j1', 'pdz-c-d2j2', 'pdz-c-venc',
        'pdz-times1', 'pdz-times2', 'pdz-dica1', 'pdz-dica2',
        'pdz-card-vencedor', 'pdz-card-placar',
        'pdz-btn-vencedor pdz-btn-venc1', 'pdz-btn-vencedor pdz-btn-venc2',
        'pdz-abrir-placar', 'pdz-placar d-none',
        'pdz-rotulo-games1', 'pdz-rotulo-games2', 'pdz-c-g1', 'pdz-c-g2',
    ].forEach((c) => raiz.appendChild(No(c)));

    // Os dois botões de vencedor e o "Empatou" carregam o lado no dataset.
    raiz.querySelector('.pdz-btn-venc1').dataset.lado = '1';
    raiz.querySelector('.pdz-btn-venc2').dataset.lado = '2';
    const empatou = No('pdz-btn-vencedor');
    empatou.dataset.lado = '0';
    raiz.appendChild(empatou);

    return raiz;
}

function montarTela() {
    const porId = {
        pdzJogos: No(),
        pdzMaisUmJogo: No(),
        pdzTetoDeJogos: No(),
        pdzSalvar: No(),
        pdzModeloJogo: { content: { firstElementChild: { cloneNode: () => moldeDeJogo() } } },
    };

    const document = {
        getElementById: (id) => porId[id] || null,
        createElement: () => No(),
    };

    const f = new Function('document', FONTE + '\nreturn pdzMontarRegistroDeJogo;');
    const montar = f(document);

    const jogadores = [1, 2, 3, 4].map((i) => ({ id: i, nome: 'Jogador ' + i, confirmado: true, convidado: false }));
    montar(jogadores, { noFio: -1, maximo: 2, rotulo: 'Convidado' }, 20);

    return porId.pdzJogos.filhos[0];
}

// Monta o quarteto tocando nos nomes, igual ao jogador na beira da quadra.
function escolherQuarteto(jogo) {
    [1, 2].forEach((lado) => {
        const caixa = jogo.querySelector('.pdz-times' + lado);
        // Os botões de nome são criados pelo desenhar(); os dois primeiros que ainda não foram
        // escolhidos viram a dupla.
        caixa.filhos.filter((b) => b.onclick).slice(0, 2).forEach((b) => b.onclick());
    });
}

function tocarNoVencedor(jogo, lado) {
    jogo.querySelectorAll('.pdz-btn-vencedor')
        .filter((b) => b.dataset.lado === String(lado))
        .forEach((b) => b.onclick());
}

function digitarPlacar(jogo, g1, g2) {
    jogo.querySelector('.pdz-abrir-placar').onclick();
    jogo.querySelector('.pdz-c-g1').value = String(g1);
    jogo.querySelector('.pdz-c-g2').value = String(g2);
}

// ── 1. TROCAR O VENCEDOR DEPOIS DE DIGITAR O PLACAR ───────────────────────────────────────
(function () {
    const jogo = montarTela();
    escolherQuarteto(jogo);
    tocarNoVencedor(jogo, 1);
    digitarPlacar(jogo, 6, 3);

    // "Marquei errado" — o toque de um dedo só, e o caminho que o jogador de verdade fez.
    tocarNoVencedor(jogo, 2);

    ok(jogo.querySelector('.pdz-c-g1').value === '' && jogo.querySelector('.pdz-c-g2').value === '',
        'trocar o vencedor limpa o placar digitado (senão o 6 x 3 contradiz o novo vencedor)');
    ok(jogo.querySelector('.pdz-placar').temClasse('d-none'),
        'e o painel do placar se fecha, pra o campo vazio não parecer um campo esquecido');
    ok(!jogo.querySelector('.pdz-abrir-placar').temClasse('d-none'),
        'e o "+ Adicionar placar" volta, senão não há como digitar o placar certo');
})();

// ── 2. REMONTAR AS DUPLAS DEPOIS DE DIGITAR O PLACAR ──────────────────────────────────────
(function () {
    const jogo = montarTela();
    escolherQuarteto(jogo);
    tocarNoVencedor(jogo, 1);
    digitarPlacar(jogo, 6, 3);

    // Tirar alguém da dupla 1 desfaz o quarteto — é o ponto em que a tela JÁ zerava o vencedor.
    const chip = jogo.querySelector('.pdz-times1').filhos.find((f) => f.onclick);
    chip.onclick();

    ok(jogo.querySelector('.pdz-c-venc').value === '',
        'desfazer o quarteto zera o vencedor (comportamento que já existia)');
    ok(jogo.querySelector('.pdz-c-g1').value === '' && jogo.querySelector('.pdz-c-g2').value === '',
        'e zera o placar junto — o 6 x 3 era daquelas quatro pessoas, não daquelas posições');
})();

// ── 3. O PLACAR QUE CONCORDA COM O VENCEDOR NÃO PODE SUMIR SOZINHO ────────────────────────
(function () {
    const jogo = montarTela();
    escolherQuarteto(jogo);
    tocarNoVencedor(jogo, 1);
    digitarPlacar(jogo, 6, 3);

    // Tocar DE NOVO no mesmo vencedor não é arrependimento: apagar aqui seria o conserto
    // virando o próximo defeito, com a pessoa redigitando o placar a cada toque.
    tocarNoVencedor(jogo, 1);

    ok(jogo.querySelector('.pdz-c-g1').value === '6' && jogo.querySelector('.pdz-c-g2').value === '3',
        'confirmar o MESMO vencedor preserva o placar já digitado');
})();

console.log(falhas === 0 ? '\nTudo certo.' : '\n' + falhas + ' falha(s).');
process.exit(falhas === 0 ? 0 : 1);
