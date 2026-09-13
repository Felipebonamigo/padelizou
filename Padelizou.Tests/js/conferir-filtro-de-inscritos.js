// O FILTRO DA LISTA DE INSCRITOS, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-filtro-de-inscritos.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo dos irmãos: este repositório não tem npm
// (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 90 linhas.
//
// O QUE ELE GUARDA (13/09/2026 — 🗣️ Felipe, com o print do "Gerenciar Inscritos" do ER:
// *"aqui no gerenciar escrito esta dificil achar, permita pesquisar por nome, coloque filtro
// por categoria"*):
//   · a busca acha pelo nome E pelo apelido, SEM ACENTO — quem digita "cezar" acha "Cézar";
//   · dois termos somam (E, não OU): "felipe bage" acha a dupla inteira, um termo em cada nome;
//   · categoria e situação recortam junto com a busca, nunca uma anulando a outra;
//   · o CABEÇALHO DA CATEGORIA VAZIA SOME — sem isso a tela filtrada vira uma pilha de títulos
//     sem ninguém embaixo, que é pior do que a lista comprida que veio consertar;
//   · com zero resultado aparece o aviso, e ele some de novo no primeiro que aparecer;
//   · o escopo é respeitado: as DUAS listas da página (Gerenciar Inscritos e Pagamentos) não
//     se filtram uma à outra;
//   · e o botão de situação ativo é UM só, com `aria-pressed` acompanhando — a classe `active`
//     do Bootstrap é tinta, e quem usa leitor de tela precisa do estado.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/filtro-de-inscritos.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function classList(inicial) {
    const set = new Set(inicial || []);
    return {
        add: (c) => set.add(c),
        remove: (c) => set.delete(c),
        contains: (c) => set.has(c),
        toggle: (c, ligado) => {
            if (ligado === undefined) ligado = !set.has(c);
            if (ligado) set.add(c); else set.delete(c);
            return ligado;
        },
    };
}

function comOuvintes(alvo) {
    const mapa = {};
    alvo.addEventListener = (nome, fn) => { (mapa[nome] = mapa[nome] || []).push(fn); };
    alvo.disparar = (nome) => (mapa[nome] || []).forEach((fn) => fn({ target: alvo }));
    return alvo;
}

// Uma linha da lista. `pago`/`parceiro`/`espera` são os mesmos textos que o Razor escreve.
function inscrito(nome, categoria, extras) {
    const e = extras || {};
    return {
        _nome: nome,
        dataset: {
            nome,
            categoria: String(categoria),
            pago: e.pago === false ? 'nao' : 'sim',
            parceiro: e.semParceiro ? 'falta' : 'ok',
            espera: e.espera ? 'sim' : 'nao',
        },
        classList: classList(['pdz-fi-item']),
        get visivel() { return !this.classList.contains('d-none'); },
    };
}

function bloco(itens) {
    return {
        _itens: itens,
        classList: classList(['pdz-fi-bloco']),
        querySelectorAll: (sel) => (sel === '.pdz-fi-item' ? itens : []),
        get visivel() { return !this.classList.contains('d-none'); },
    };
}

function botaoDeSituacao(situacao, ativo) {
    return comOuvintes({
        dataset: { situacao },
        classList: classList(ativo ? ['pdz-fi-situacao', 'active'] : ['pdz-fi-situacao']),
        atributos: {},
        setAttribute(nome, valor) { this.atributos[nome] = valor; },
    });
}

function escopo(blocos) {
    const campo = comOuvintes({ value: '' });
    const seletor = comOuvintes({ value: '' });
    const situacoes = ['todos', 'naopagos', 'semparceiro', 'espera']
        .map((s, i) => botaoDeSituacao(s, i === 0));
    const vazio = { classList: classList(['pdz-fi-vazio', 'd-none']),
                    get visivel() { return !this.classList.contains('d-none'); } };

    const alvo = {
        querySelector: (sel) => {
            if (sel === '.pdz-fi-busca') return campo;
            if (sel === '.pdz-fi-categoria') return seletor;
            if (sel === '.pdz-fi-situacao.active') return situacoes.find((b) => b.classList.contains('active')) || null;
            if (sel === '.pdz-fi-vazio') return vazio;
            return null;
        },
        querySelectorAll: (sel) => {
            if (sel === '.pdz-fi-bloco') return blocos;
            if (sel === '.pdz-fi-situacao') return situacoes;
            return [];
        },
    };

    return {
        alvo, campo, seletor, situacoes, vazio, blocos,
        itens: blocos.reduce((todos, b) => todos.concat(b._itens), []),
        buscar(texto) { campo.value = texto; campo.disparar('input'); },
        categoria(id) { seletor.value = String(id); seletor.disparar('change'); },
        situacao(qual) { situacoes.find((b) => b.dataset.situacao === qual).disparar('click'); },
        aparecendo() { return this.itens.filter((i) => i.visivel).map((i) => i._nome); },
    };
}

// ⚠️ O `document` falso devolve TODOS os escopos da página — é ele que dá à trava do vizinho
// como discriminar. Sem esse caminho, "a outra lista fica intacta" passaria com qualquer
// implementação: não existiria jeito de o arquivo errar.
function pagina(escopos) {
    const documentoFalso = {
        readyState: 'complete',
        addEventListener: () => {},
        querySelectorAll: (sel) => (sel === '.pdz-inscritos-filtraveis' ? escopos.map((e) => e.alvo) : []),
    };

    new Function('document', fonte)(documentoFalso);
    return escopos;
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log('  FALHA· ' + nome);
}

function mesmaLista(a, b) {
    return a.length === b.length && a.every((x, i) => x === b[i]);
}

// A 3ª e a 4ª Masculina do print, com os nomes que estavam na tela.
function listaDoEr() {
    const terceira = bloco([
        inscrito('Bruno Piccoli Joao Bugs', 3),
        inscrito('Rafael Xavier Cézar Bonomi', 3, { pago: false }),
        inscrito('Eder Marcos Augusto Ohlweiler', 3, { pago: false }),
        inscrito('Paulo Prass (Batata) Arthur Prass (Batatinha)', 3),
    ]);
    const quarta = bloco([
        inscrito('Felipe Bonamigo Guilherme Bagesteiro', 4, { pago: false }),
        inscrito('Marcos Coelho Marcio Rafael', 4),
        inscrito('Lucas Almeida (Foka)', 4, { semParceiro: true }),
        inscrito('Eliezer Júnior Fernando Kraemer', 4, { espera: true }),
    ]);
    return escopo([terceira, quarta]);
}

console.log('\nFILTRO DE INSCRITOS');

// ── busca por nome ────────────────────────────────────────────────────────────────────────
{
    const lista = listaDoEr();
    pagina([lista]);

    conferir('sem nada digitado, todo mundo aparece', lista.aparecendo().length === 8);

    lista.buscar('coelho');
    conferir('acha pelo sobrenome do segundo jogador', mesmaLista(lista.aparecendo(), ['Marcos Coelho Marcio Rafael']));

    lista.buscar('BATATA');
    conferir('acha pelo APELIDO, e sem se importar com maiúscula',
        mesmaLista(lista.aparecendo(), ['Paulo Prass (Batata) Arthur Prass (Batatinha)']));

    lista.buscar('cezar');
    conferir('acha "Cézar" digitando "cezar" — busca SEM ACENTO',
        mesmaLista(lista.aparecendo(), ['Rafael Xavier Cézar Bonomi']));

    lista.buscar('junior');
    conferir('acha "Júnior" digitando "junior"',
        mesmaLista(lista.aparecendo(), ['Eliezer Júnior Fernando Kraemer']));

    lista.buscar('felipe bage');
    conferir('dois termos SOMAM (E, não OU): um em cada nome da dupla',
        mesmaLista(lista.aparecendo(), ['Felipe Bonamigo Guilherme Bagesteiro']));

    lista.buscar('felipe coelho');
    conferir('dois termos que não estão na MESMA dupla não acham nada', lista.aparecendo().length === 0);
    conferir('com zero resultado, o aviso de vazio aparece', lista.vazio.visivel);

    lista.buscar('  ');
    conferir('só espaço em branco devolve a lista inteira', lista.aparecendo().length === 8);
    conferir('e o aviso de vazio some de novo', !lista.vazio.visivel);
}

// ── cabeçalho da categoria vazia ──────────────────────────────────────────────────────────
{
    const lista = listaDoEr();
    pagina([lista]);

    lista.buscar('coelho');
    conferir('o bloco da categoria que ficou sem ninguém SOME', !lista.blocos[0].visivel);
    conferir('e o da categoria que tem alguém fica', lista.blocos[1].visivel);

    lista.buscar('');
    conferir('limpando a busca, o bloco escondido VOLTA', lista.blocos[0].visivel && lista.blocos[1].visivel);
}

// ── filtro por categoria ──────────────────────────────────────────────────────────────────
{
    const lista = listaDoEr();
    pagina([lista]);

    lista.categoria(3);
    conferir('a categoria escolhida mostra só as duplas dela', lista.aparecendo().length === 4);
    conferir('e o bloco da outra categoria some', !lista.blocos[1].visivel);

    lista.buscar('prass');
    conferir('categoria + busca recortam JUNTO',
        mesmaLista(lista.aparecendo(), ['Paulo Prass (Batata) Arthur Prass (Batatinha)']));

    lista.buscar('coelho');
    conferir('nome que existe em OUTRA categoria não escapa do filtro de categoria',
        lista.aparecendo().length === 0);

    lista.categoria('');
    conferir('"todas as categorias" devolve o recorte só da busca',
        mesmaLista(lista.aparecendo(), ['Marcos Coelho Marcio Rafael']));
}

// ── atalhos de situação ───────────────────────────────────────────────────────────────────
{
    const lista = listaDoEr();
    pagina([lista]);

    lista.situacao('naopagos');
    conferir('"Não pagos" mostra só quem não pagou', mesmaLista(lista.aparecendo(), [
        'Rafael Xavier Cézar Bonomi',
        'Eder Marcos Augusto Ohlweiler',
        'Felipe Bonamigo Guilherme Bagesteiro',
    ]));

    lista.situacao('semparceiro');
    conferir('"Sem parceiro" mostra só a inscrição solo',
        mesmaLista(lista.aparecendo(), ['Lucas Almeida (Foka)']));

    lista.situacao('espera');
    conferir('"Lista de espera" mostra só quem está na espera',
        mesmaLista(lista.aparecendo(), ['Eliezer Júnior Fernando Kraemer']));

    lista.situacao('naopagos');
    lista.buscar('felipe');
    conferir('situação + busca recortam JUNTO',
        mesmaLista(lista.aparecendo(), ['Felipe Bonamigo Guilherme Bagesteiro']));

    lista.situacao('todos');
    conferir('"Todos" devolve a lista inteira do recorte da busca',
        mesmaLista(lista.aparecendo(), ['Felipe Bonamigo Guilherme Bagesteiro']));

    lista.buscar('');
    conferir('e sem busca, "Todos" devolve TUDO', lista.aparecendo().length === 8);
}

// ── o botão ativo é um só, e o leitor de tela sabe ────────────────────────────────────────
{
    const lista = listaDoEr();
    pagina([lista]);

    lista.situacao('espera');
    const ativos = lista.situacoes.filter((b) => b.classList.contains('active'));
    conferir('só UM botão de situação fica ativo', ativos.length === 1 && ativos[0].dataset.situacao === 'espera');
    conferir('o aria-pressed acompanha o botão ativo', ativos[0].atributos['aria-pressed'] === 'true');
    conferir('e os outros voltam a aria-pressed="false"',
        lista.situacoes.filter((b) => b.atributos['aria-pressed'] === 'false').length === 3);
}

// ── duas listas na mesma página ───────────────────────────────────────────────────────────
{
    const gerenciar = listaDoEr();
    const pagamentos = listaDoEr();
    pagina([gerenciar, pagamentos]);

    gerenciar.buscar('coelho');
    conferir('filtrar o Gerenciar Inscritos não mexe na lista de Pagamentos',
        pagamentos.aparecendo().length === 8);

    pagamentos.situacao('naopagos');
    conferir('e filtrar Pagamentos não desfaz o recorte do Gerenciar Inscritos',
        mesmaLista(gerenciar.aparecendo(), ['Marcos Coelho Marcio Rafael']));
    conferir('cada lista com o seu recorte', pagamentos.aparecendo().length === 3);
}

console.log(falhas === 0 ? '\nTudo certo.\n' : '\n' + falhas + ' conferência(s) falharam.\n');
process.exit(falhas === 0 ? 0 : 1);
