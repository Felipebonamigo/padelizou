// A TRAVA DE CLIQUE DO PALPITRÔMETRO, conferida contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-palpitrometro.js
//
// ⚠️ NINGUÉM RODA ISTO SOZINHO — nem o `dotnet test`, nem o CI (que não tem passo de Node). É
// conferência de mão, e está versionada só pra não morrer junto com o scratchpad da sessão,
// como morreu a de 19/08. Quem mexer no `palpitrometro.js` roda antes de commitar.
//
// Sem dependência nenhuma de propósito: `require('fs')` e mais nada. Um `npm install` aqui
// traria package.json, lockfile e supply chain pra um repositório que hoje não tem nada disso
// (ver SUPPLY-CHAIN.md) — por uma conferência de 150 linhas.
//
// O defeito que ele guarda: 10/09/2026, `DbUpdateException em POST /Partidas/Votar` em
// produção. Dois POSTs do mesmo dedo. A trava do servidor está em `PalpiteService` e tem teste
// em `PalpiteEmDobroTests`; esta aqui é a metade da tela.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/palpitrometro.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function elemento() {
    return {
        textContent: '', innerHTML: '', innerText: '',
        style: {}, classList: { toggle() { }, remove() { }, contains: () => false },
        dataset: {},
    };
}

function palpitrometro(partidaId, dupla1Id, meuVoto) {
    const filhos = {};
    for (const sel of ['[data-pct="1"]', '[data-pct="2"]', '[data-bar="1"]', '[data-bar="2"]',
                       '.pdz-total-votos', '.pdz-palpite-placar', '.pdz-palpite-consenso',
                       '.pdz-consenso-placar', '.pdz-consenso-votos']) {
        filhos[sel] = elemento();
    }
    // As fichas de placar de verdade: é NELAS que dá pra ver com que palpite a tela TERMINOU
    // pintada — a marca "btn-success" é a ficha escolhida.
    const fichas = [[6, 4], [6, 3]].map(([v, p]) => ({
        dataset: { vencedor: String(v), perdedor: String(p) },
        marcada: false,
        classList: { toggle(classe, ligada) { if (classe === 'btn-success') this.dono.marcada = ligada; } },
    }));
    fichas.forEach(f => { f.classList.dono = f; });
    filhos['.pdz-palpite-placar'].querySelectorAll = () => fichas;
    filhos['.pdz-palpite-consenso'].querySelector = sel => filhos[sel] || null;

    const container = {
        fichas,
        dataset: { partidaId: String(partidaId), dupla1Id: String(dupla1Id), meuVoto: meuVoto || '' },
        classList: { contains: () => false, remove() { }, toggle() { } },
        querySelector: sel => filhos[sel] || null,
        querySelectorAll: () => [],
    };

    const toque = duplaId => ({ dataset: { duplaId: String(duplaId), votavel: 'true' }, closest: () => container });
    const ficha = (v, p) => ({ dataset: { vencedor: String(v), perdedor: String(p) }, closest: () => container });
    return { container, toque, ficha };
}

// ── O SERVIDOR FALSO ──────────────────────────────────────────────────────────────────────
// Registra cada POST com o instante em que ENTROU e em que RESPONDEU — é assim que se vê se
// dois pedidos se cruzaram.
function servidor(atrasos = {}) {
    const posts = [];
    let emVoo = 0, cruzaram = false;

    async function fetchFalso(url, opcoes) {
        const i = posts.length;
        const corpo = opcoes.body;
        posts.push({ corpo, respondido: false });

        emVoo++;
        if (emVoo > 1) cruzaram = true;
        await new Promise(r => setTimeout(r, atrasos[i] ?? 1));
        emVoo--;
        posts[i].respondido = true;

        // O resumo que o servidor devolveria pra ESTE palpite — é ele que a tela pinta.
        const p = new URLSearchParams(corpo);
        return {
            ok: true,
            json: async () => ({
                votosDupla1: 1, votosDupla2: 0, totalVotos: 1,
                percentualDupla1: 100, percentualDupla2: 0,
                meuVotoDuplaId: Number(p.get('duplaId')),
                meuPlacarLado1: p.has('placar1') ? Number(p.get('placar1')) : null,
                meuPlacarLado2: p.has('placar2') ? Number(p.get('placar2')) : null,
                placarEmSets: false, placarMaisPalpitadoLado1: null, placarMaisPalpitadoLado2: null,
                placarMaisPalpitadoVotos: 0, palpitesComPlacar: 0,
            }),
        };
    }

    return { posts, fetchFalso, cruzou: () => cruzaram };
}

function carregar(fetchFalso) {
    const montar = new Function('fetch', 'alert', 'cabecalhoAntifalsificacao', 'document', 'bootstrap',
        fonte + '\n; return { votarPalpite, palpitarPlacar };');
    return montar(fetchFalso, () => { }, h => h, undefined, undefined);
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}

(async () => {
    // 1. TOQUE DUPLO no mesmo nome: o segundo toque não diz nada de novo → UM POST só.
    {
        const s = servidor({ 0: 30 });
        const js = carregar(s.fetchFalso);
        const { toque } = palpitrometro(7, 10);
        const el = toque(10);
        const a = js.votarPalpite(el);
        const b = js.votarPalpite(el);   // o dedo bateu duas vezes
        await Promise.all([a, b]);
        confere('toque duplo no mesmo nome manda UM POST', s.posts.length === 1,
                `mandou ${s.posts.length}`);
    }

    // 2. VOTO + FICHA (dois palpites DIFERENTES, o segundo no meio do primeiro): os dois vão,
    //    em fila — e a tela termina com o ÚLTIMO, não com o que respondeu por último.
    {
        const s = servidor({ 0: 40, 1: 1 });   // o primeiro demora; sem fila ele pinta por último
        const js = carregar(s.fetchFalso);
        const { container, toque, ficha } = palpitrometro(7, 10, '10');
        const a = js.votarPalpite(toque(10));
        const b = js.palpitarPlacar(ficha(6, 4));
        await Promise.all([a, b]);

        confere('voto e ficha no meio: os dois chegam ao servidor', s.posts.length === 2,
                `mandou ${s.posts.length}`);
        confere('um de cada vez — os dois POSTs não se cruzam', !s.cruzou(), 'cruzaram');
        // ⚠️ ESTA é a que importa: sem fila, a resposta do primeiro POST (que demorou) volta
        // DEPOIS da do segundo e repinta a tela com o palpite VELHO — a ficha 6x4 apaga
        // sozinha na frente da pessoa, e só o F5 conserta.
        const marcada = container.fichas.find(f => f.marcada);
        confere('a tela termina pintada com o ÚLTIMO palpite (6x4)',
                marcada && marcada.dataset.vencedor === '6' && marcada.dataset.perdedor === '4',
                marcada ? `ficha ${marcada.dataset.vencedor}x${marcada.dataset.perdedor}` : 'nenhuma ficha marcada');
    }

    // 3. A trava é POR CARTÃO: dois jogos na mesma tela não esperam um pelo outro.
    {
        const s = servidor({ 0: 30, 1: 30 });
        const js = carregar(s.fetchFalso);
        const um = palpitrometro(7, 10);
        const outro = palpitrometro(8, 20);
        await Promise.all([js.votarPalpite(um.toque(10)), js.votarPalpite(outro.toque(20))]);
        confere('dois jogos diferentes falam ao mesmo tempo', s.posts.length === 2 && s.cruzou(),
                `${s.posts.length} POSTs, cruzaram=${s.cruzou()}`);
    }

    console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
    process.exit(falhas.length === 0 ? 0 : 1);
})();
