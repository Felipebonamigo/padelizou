// AS REAÇÕES DO JOGO, conferidas contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-reacoes-do-jogo.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, que varre o glob
// `Padelizou.Tests/js/conferir-*.js` e reprova o build se qualquer um cair. Rode à mão antes de
// commitar: descobrir pelo PR vermelho custa um ciclo.
//
// Sem dependência nenhuma de propósito: `require('fs')` e mais nada (ver SUPPLY-CHAIN.md).
//
// O que ele guarda: o painel é UM só, reusado pelos 97 cartões da lista, e a fileira de cada
// cartão é repintada sem F5. As duas coisas erram calado — gravando no jogo errado, ou deixando
// o cartão sem caminho de volta depois de a última reação sair.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/reacoes-do-jogo.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function elemento() {
    return {
        innerHTML: '', textContent: '', value: '', hidden: false,
        dataset: {}, classList: { contains: () => false },
    };
}

function tela() {
    const els = {};
    for (const id of ['modalQuemReagiu', 'modalQuemReagiuLista', 'modalQuemReagiuTitulo',
                      'modalQuemReagiuPilulas', 'modalQuemReagiuCampo', 'modalQuemReagiuErro']) {
        els[id] = elemento();
    }

    // As fileiras dos CARTÕES, por jogo — é nelas que se vê a repintura sem F5.
    const fileiras = {};
    function fileira(partidaId) {
        if (!fileiras[partidaId]) {
            fileiras[partidaId] = Object.assign(elemento(), { dataset: { partidaId: String(partidaId) } });
        }
        return fileiras[partidaId];
    }

    return {
        els,
        fileira,
        fileiras,
        // A pílula do cartão: o que o `onclick` do Razor entrega ao `verQuemReagiu`.
        pilulaDoCartao: (partidaId, emoji) => ({
            dataset: { emoji, eu: 'false' },
            classList: { contains: () => false },
            closest: () => fileira(partidaId),
        }),
        // A pílula do PAINEL, que soma ou tira.
        pilulaDoPainel: (emoji, minha) => ({
            dataset: { emoji },
            classList: { contains: c => minha && c === 'pdz-reacao-minha' },
        }),
        document: {
            getElementById: id => els[id] || null,
            querySelector: sel => {
                const m = /data-partida-id="(\d+)"/.exec(sel);
                return m ? (fileiras[m[1]] || null) : null;
            },
        },
        bootstrap: { Modal: { getOrCreateInstance: () => ({ show() { } }) } },
    };
}

// ── O SERVIDOR FALSO ──────────────────────────────────────────────────────────────────────
// Guarda cada chamada com o instante em que entrou e respondeu — é assim que se vê se dois
// pedidos se cruzaram.
function servidor(respostas = {}, atrasos = {}) {
    const chamadas = [];
    let emVoo = 0, cruzaram = false;

    async function fetchFalso(url, opcoes) {
        const i = chamadas.length;
        chamadas.push({ url, metodo: (opcoes && opcoes.method) || 'GET', corpo: opcoes && opcoes.body,
                        cabecalhos: (opcoes && opcoes.headers) || {} });

        emVoo++;
        if (emVoo > 1) cruzaram = true;
        await new Promise(r => setTimeout(r, atrasos[i] ?? 1));
        emVoo--;

        const rota = url.split('?')[0];
        const resposta = respostas[rota];
        if (resposta) return resposta;

        // Padrão: o GET devolve a lista, o POST devolve o resumo com uma pílula de 🔥.
        if (url.indexOf('/Partidas/QuemReagiu') === 0) {
            return { ok: true, json: async () => ({ linhas: [{ emoji: '🔥', nome: 'Ana', fotoPerfil: null }] }) };
        }
        return { ok: true, json: async () => ({ partidaId: 1, total: 1, reacoes: [{ emoji: '🔥', total: 1, euReagi: true }] }) };
    }

    return { chamadas, fetchFalso, cruzou: () => cruzaram };
}

function carregar(fetchFalso, t) {
    const montar = new Function('fetch', 'alert', 'cabecalhoAntifalsificacao', 'document', 'bootstrap',
        fonte + '\n; return { verQuemReagiu, alternarMinhaReacao, reagirComEmoji, reagirDoCampo };');
    return montar(fetchFalso, () => { throw new Error('alert() não deveria ser usado aqui'); },
        h => Object.assign({ RequestVerificationToken: 'carimbo' }, h || {}),
        t.document, t.bootstrap);
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
const falhas = [];
function confere(nome, condicao, detalhe) {
    if (condicao) { console.log('  ok  ' + nome); return; }
    falhas.push(nome);
    console.log('FALHA  ' + nome + (detalhe ? '  →  ' + detalhe : ''));
}

(async () => {
    // 1. A PÍLULA DO CARTÃO ABRE O PAINEL DAQUELE JOGO — é o pedido do Felipe ("ao clicar no
    //    emoji, veja quem colocou o que"), e o painel é UM só pra lista inteira.
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);

        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        confere('a pílula do cartão busca quem reagiu NAQUELE jogo',
                s.chamadas.length === 1 && s.chamadas[0].url === '/Partidas/QuemReagiu?partidaId=26',
                JSON.stringify(s.chamadas.map(c => c.url)));
        confere('a lista vem pintada com quem reagiu',
                t.els.modalQuemReagiuLista.innerHTML.indexOf('Ana') >= 0,
                t.els.modalQuemReagiuLista.innerHTML);
        confere('o título conta as reações, como no print do WhatsApp',
                t.els.modalQuemReagiuTitulo.textContent === '1 reação',
                t.els.modalQuemReagiuTitulo.textContent);
    }

    // 2. ABRIR OUTRO JOGO TROCA O ALVO. ⚠️ Sem isto, reagir depois de olhar dois cartões
    //    gravaria no jogo de antes — e o painel é reusado por todos os 97.
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);

        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        await js.verQuemReagiu(t.pilulaDoCartao(99, '👏'));
        await js.reagirComEmoji('😂');

        const post = s.chamadas.filter(c => c.metodo === 'POST')[0];
        confere('reagir depois de abrir outro jogo grava no jogo ABERTO',
                post && post.corpo.indexOf('partidaId=99') >= 0,
                post && post.corpo);
    }

    // 3. NO PAINEL, A PÍLULA MARCADA TIRA E A NÃO MARCADA SOMA. É o "toque para remover" do
    //    WhatsApp — no cartão a pílula abre, aqui ela muda a minha.
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        await js.alternarMinhaReacao(t.pilulaDoPainel('🔥', true));
        const tirou = s.chamadas.filter(c => c.metodo === 'POST').pop();
        confere('a pílula que é MINHA vai pro TirarReacao',
                tirou && tirou.url === '/Partidas/TirarReacao', tirou && tirou.url);

        await js.alternarMinhaReacao(t.pilulaDoPainel('👏', false));
        const somou = s.chamadas.filter(c => c.metodo === 'POST').pop();
        confere('a pílula que NÃO é minha vai pro Reagir',
                somou && somou.url === '/Partidas/Reagir', somou && somou.url);
        confere('o emoji viaja codificado no corpo',
                somou && somou.corpo.indexOf('emoji=' + encodeURIComponent('👏')) >= 0,
                somou && somou.corpo);
    }

    // 4. O CARIMBO ANTIFALSIFICAÇÃO VAI EM TODO POST. O filtro global do Program.cs recusa
    //    quem chega sem ele — e a recusa apareceria só como "não foi possível registrar".
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        await js.reagirComEmoji('🔥');

        const post = s.chamadas.filter(c => c.metodo === 'POST').pop();
        confere('o POST leva o carimbo antifalsificação',
                post && post.cabecalhos.RequestVerificationToken === 'carimbo',
                post && JSON.stringify(post.cabecalhos));
    }

    // 5. A FILEIRA DO CARTÃO É REPINTADA, E O BOTÃO DO TECLADO SOBREVIVE.
    //    ⚠️ Sem o botão na repintura, quem tirasse a ÚNICA reação do jogo ficaria com a
    //    fileira vazia e sem caminho de volta até o F5.
    {
        const t = tela();
        const s = servidor({
            '/Partidas/TirarReacao': { ok: true, json: async () => ({ partidaId: 26, total: 0, reacoes: [] }) },
        });
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        await js.alternarMinhaReacao(t.pilulaDoPainel('🔥', true));

        const html = t.fileira(26).innerHTML;
        confere('tirar a última reação deixa o botão do teclado na fileira',
                html.indexOf('pdz-reacao-abrir') >= 0 && html.indexOf('pdz-reacao-pilula') < 0, html);
    }

    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        await js.reagirComEmoji('🔥');

        const html = t.fileira(26).innerHTML;
        confere('a fileira do cartão ganha a pílula com a contagem, sem F5',
                html.indexOf('pdz-reacao-pilula') >= 0 && html.indexOf('>1<') >= 0, html);
        confere('a pílula repintada continua ABRINDO o painel (e não somando)',
                html.indexOf('verQuemReagiu(this)') >= 0, html);
        confere('a minha pílula repintada vem marcada',
                html.indexOf('pdz-reacao-minha') >= 0, html);
    }

    // 6. O NOME É TEXTO DE GENTE E A LISTA É innerHTML. Sem escapar, um nome com "<" quebra o
    //    painel — e um nome montado de propósito injeta marcação na página.
    {
        const t = tela();
        const s = servidor({
            '/Partidas/QuemReagiu': {
                ok: true,
                json: async () => ({ linhas: [{ emoji: '🔥', nome: '<img src=x onerror=alert(1)>', fotoPerfil: null }] }) ,
            },
        });
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        const html = t.els.modalQuemReagiuLista.innerHTML;
        // ⚠️ NÃO dá pra conferir por "não tem `<img`": a foto do avatar é uma `<img>` legítima
        // da própria linha. O que importa é que a marcação do NOME chegou escapada e que
        // nenhuma tag nasceu dela.
        confere('o nome de quem reagiu é escapado antes de entrar no painel',
                html.indexOf('&lt;img src=x onerror=alert(1)&gt;') >= 0
                && html.indexOf('<img src=x') < 0, html);
    }

    // 7. O JOGO APAGADO É 404, E O PAINEL DIZ ISSO. ⚠️ Olhar o `ok` ANTES do `json()`: a
    //    resposta de erro não é JSON, o parse estoura, ninguém pega a promessa e o painel fica
    //    em "Carregando..." pra sempre — foi o que fez o dedo bater três vezes em 11/09.
    {
        const t = tela();
        const s = servidor({
            '/Partidas/QuemReagiu': { ok: false, status: 404, json: async () => { throw new Error('não é JSON'); } },
        });
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        const html = t.els.modalQuemReagiuLista.innerHTML;
        confere('jogo apagado explica o 404 em vez de travar em Carregando',
                html.indexOf('saiu da lista') >= 0 && html.indexOf('Carregando') < 0, html);
    }

    // 8. A RECUSA APARECE NO PAINEL, NÃO NUM ALERT. A recusa mais comum é "isso não é um
    //    emoji", que é sobre o que a pessoa acabou de digitar no campo ao lado — um alerta do
    //    navegador tiraria o texto da tela junto com o motivo. (O `alert` do DOM falso
    //    ESTOURA de propósito: se o JS chamar, esta conferência cai.)
    {
        const t = tela();
        const s = servidor({
            '/Partidas/Reagir': { ok: false, status: 400, json: async () => ({ erro: 'Isso não é um emoji.' }) },
        });
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        t.els.modalQuemReagiuCampo.value = 'legal';
        await js.reagirDoCampo();

        confere('a recusa do servidor aparece no painel',
                t.els.modalQuemReagiuErro.textContent === 'Isso não é um emoji.'
                && t.els.modalQuemReagiuErro.hidden === false,
                t.els.modalQuemReagiuErro.textContent);
        confere('o texto recusado FICA no campo, pra pessoa corrigir',
                t.els.modalQuemReagiuCampo.value === 'legal', t.els.modalQuemReagiuCampo.value);
    }

    // 9. O CAMPO VAZIO NÃO MANDA NADA. Sem isto, o toque no "Reagir" sem digitar gastaria um
    //    POST pra receber "isso não é um emoji" de volta.
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        const antes = s.chamadas.length;

        t.els.modalQuemReagiuCampo.value = '   ';
        await js.reagirDoCampo();

        confere('campo vazio não gasta POST', s.chamadas.length === antes,
                s.chamadas.length + ' vs ' + antes);
    }

    // 10. UM POST POR VEZ. ⚠️ Duas respostas fora de ordem repintariam a fileira com a
    //     contagem velha, e a pílula recém-somada voltaria pro número anterior na frente da
    //     pessoa — é o mesmo defeito que a fila do palpitômetro guarda.
    {
        const t = tela();
        const s = servidor({}, { 1: 30, 2: 1 });   // o primeiro POST é o lento
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        await Promise.all([js.reagirComEmoji('🔥'), js.reagirComEmoji('👏')]);

        confere('dois toques não se cruzam na rede', !s.cruzou());
        const posts = s.chamadas.filter(c => c.metodo === 'POST');
        confere('os dois emoji diferentes chegam, um depois do outro', posts.length === 2,
                posts.length + ' POST(s)');
    }

    // 11. O TOQUE DUPLO NO MESMO EMOJI NÃO MANDA DOIS. A trava de verdade é a chave composta
    //     do banco (é ela que segura duas abas e o POST montado à mão), mas poupar a
    //     requisição gêmea DESTA aba é o que evita a contagem piscando.
    {
        const t = tela();
        const s = servidor({}, { 1: 20 });
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));

        await Promise.all([js.reagirComEmoji('🔥'), js.reagirComEmoji('🔥')]);

        confere('toque duplo no mesmo emoji manda UM POST',
                s.chamadas.filter(c => c.metodo === 'POST').length === 1,
                s.chamadas.filter(c => c.metodo === 'POST').length + ' POST(s)');
    }

    // 12. O CARTÃO PODE TER SAÍDO DA TELA no meio do caminho: a atualização automática troca o
    //     HTML da lista a cada 20 segundos. Repintar o que não existe mais não pode estourar —
    //     o painel continua valendo.
    {
        const t = tela();
        const s = servidor();
        const js = carregar(s.fetchFalso, t);
        await js.verQuemReagiu(t.pilulaDoCartao(26, '🔥'));
        delete t.fileiras['26'];                      // a lista foi trocada embaixo do painel

        let estourou = false;
        try { await js.reagirComEmoji('👏'); } catch (e) { estourou = true; }

        confere('reagir com o cartão fora da tela não estoura', !estourou);
    }

    console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
    process.exit(falhas.length === 0 ? 0 : 1);
})();
