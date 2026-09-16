// FINALIZAR E "VOLTAR PRA AGENDADO" PARAM DE RECARREGAR A PÁGINA, conferido num DOM falso.
//
//     node Padelizou.Tests/js/conferir-acao-do-cartao-ao-vivo.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// ── O QUE ELE GUARDA ──────────────────────────────────────────────────────────────────────
//
// 🗣️ Felipe, 14/09/2026: *"apenas queria q o video nao travasse, nao mude o layout"*.
//
// 🕳️ Os dois botões do cartão AO VIVO — "Finalizar" e "Voltar pra agendado" — eram POST comum:
// o navegador recarregava a página inteira, e RECARGA REINICIA TODO <iframe> da tela. Quem
// estava assistindo à Quadra 2 perdia o vídeo porque o organizador finalizou o jogo da Quadra 1.
// É o mesmo defeito que o js/placar-ao-vivo.js e o js/saque-ao-vivo.js já tiraram do −/+ e da
// bolinha do saque; estes dois ficaram de fora.
//
// ⚠️ E AQUI NÃO DÁ PRA USAR A PROVA DOS OUTROS DOIS. Lá a resposta é JSON, e exigir
// `content-type: json` é o que separa "salvou" de "a sessão caiu e isto é a tela de login" — o
// `fetch` SEGUE o 302 e entrega 200 com o HTML do login. O FinalizarPartida responde com
// redirect pra PÁGINA, então a prova aqui é outra: a resposta tem que conter a lista de jogos
// (`#jogosTabsContent`). Sem ela, recarrega — que é exatamente o que teria acontecido sem a
// interceptação, e leva a pessoa pro login em vez de mentir que finalizou.
const fs = require('fs');

let falhas = 0;
function ok(condicao, texto) {
    console.log((condicao ? '  ok  ' : ' FALHA') + ' · ' + texto);
    if (!condicao) falhas++;
}

const FONTE = fs.readFileSync('Padelizou/wwwroot/js/acao-do-cartao-ao-vivo.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function elemento(atributos, classes) {
    const el = {
        _atributos: Object.assign({}, atributos || {}),
        _classes: new Set(classes || []),
        disabled: false,
        getAttribute: (n) => (n in el._atributos ? el._atributos[n] : null),
        setAttribute: (n, v) => { el._atributos[n] = String(v); },
        removeAttribute: (n) => { delete el._atributos[n]; },
        hasAttribute: (n) => n in el._atributos,
        classList: { contains: (c) => el._classes.has(c) },
        querySelector: () => null,
        scrollIntoView: () => { el._rolou = true; },
    };
    return el;
}

// O formulário do "Finalizar": a classe que o JS procura, a confirmação do confirmar.js e o
// botão que não pode aceitar dois toques.
function formulario(opcoes) {
    const o = opcoes || {};
    const atributos = { action: '/Torneios/FinalizarPartida' };
    if (o.confirmar !== false) atributos['data-confirmar'] = 'Encerra o jogo com o placar 6 x 4?';
    if (o.confirmado) atributos['data-confirmado'] = '1';

    const form = elemento(atributos, ['pdz-live-acao']);
    form.action = '/Torneios/FinalizarPartida';
    form.botao = elemento({ type: 'submit' });
    form.querySelector = (sel) => (sel === '[type=submit]' || sel === 'button[type=submit]' ? form.botao : null);
    form.closest = (sel) => (sel === '.pdz-live-acao' ? form : null);
    return form;
}

// `html` é o que o servidor devolve; `status` o código. `semLista` simula a tela de login
// entregue com 200 depois do 302 que o fetch seguiu.
function cenario(resposta) {
    const r = resposta || {};
    const aviso = elemento({ id: 'pdzAvisoDaAcao' });
    aviso.innerHTML = '';

    const doc = {
        _ouvintes: {},
        addEventListener(tipo, fn) { (this._ouvintes[tipo] = this._ouvintes[tipo] || []).push(fn); },
        disparar(tipo, evento) { (this._ouvintes[tipo] || []).forEach((fn) => fn(evento)); },
        querySelector: (sel) => (sel === '#pdzAvisoDaAcao' ? aviso : null),
    };

    const registro = { posts: [], recarregou: 0, aplicado: null, impediu: 0 };

    const win = {
        document: doc,
        location: { reload: () => { registro.recarregou++; } },
        FormData: function (formulario) { this.form = formulario; },
        pdzAplicarRespostaDeAcao: (html) => {
            registro.aplicado = html;
            // A função de verdade devolve `false` quando a resposta não é esta tela.
            return String(html).indexOf('jogosTabsContent') !== -1;
        },
    };

    if (!r.semFetch) {
        win.fetch = (url, opcoes) => {
            registro.posts.push({ url: url, opcoes: opcoes });
            if (r.rede === 'caiu') return Promise.reject(new Error('sem rede'));
            return Promise.resolve({
                ok: r.status === undefined || r.status < 400,
                status: r.status || 200,
                text: () => Promise.resolve(r.semLista ? '<html>Entrar na sua conta</html>'
                                                       : '<html><div id="jogosTabsContent"></div></html>'),
            });
        };
    }

    const f = new Function('window', 'document', FONTE);
    f(win, doc);

    return { doc, win, aviso, registro };
}

async function enviar(cenarioMontado, form) {
    let impediu = 0;
    cenarioMontado.doc.disparar('submit', {
        target: form,
        preventDefault: () => { impediu++; },
    });
    await new Promise((r) => setTimeout(r, 20));
    return impediu;
}

(async function () {
    console.log('── A CONFIRMAÇÃO VEM PRIMEIRO ──────────────────────────────────────────────');

    // 1. O confirmar.js abre o modal e só então reenvia com requestSubmit(), que dispara este
    //    ouvinte DE NOVO — já com a marca. Interceptar antes dela mandaria o POST sem perguntar
    //    nada: o Finalizar viraria um toque sem volta.
    const semConfirmar = cenario();
    const f1 = formulario({ confirmado: false });
    const impediu1 = await enviar(semConfirmar, f1);
    ok(impediu1 === 0, 'formulário ainda NÃO confirmado passa direto (quem pergunta é o confirmar.js)');
    ok(semConfirmar.registro.posts.length === 0, 'e nenhum POST sai antes da pessoa confirmar');

    console.log('── O POST SAI SEM RECARREGAR A PÁGINA ──────────────────────────────────────');

    // 2. Confirmado: o POST vai por fetch e a página NÃO recarrega — que é o ponto de tudo
    //    isto. Recarga reinicia todo <iframe> da tela, inclusive o das outras quadras.
    const feliz = cenario();
    const f2 = formulario({ confirmado: true });
    const impediu2 = await enviar(feliz, f2);
    ok(impediu2 === 1, 'confirmado, o envio de sempre é impedido');
    ok(feliz.registro.posts.length === 1, 'e um POST sai no lugar dele');

    // Defensivo de propósito: contra um esqueleto vazio não há POST nenhum, e um `posts[0].url`
    // cru derrubaria o arquivo na primeira conferência — escondendo as outras sete.
    const post = feliz.registro.posts[0] || { opcoes: {} };
    ok(post.url === '/Torneios/FinalizarPartida', 'pro mesmo endereço do formulário');
    ok(post.opcoes.method === 'POST', 'pelo método POST');
    ok(post.opcoes.credentials === 'same-origin', 'com o cookie da sessão');
    ok(post.opcoes.body && post.opcoes.body.form === f2,
        'e com o FORMULÁRIO INTEIRO no corpo — é o que leva o carimbo antifalsificação e os campos escondidos');
    ok(feliz.registro.recarregou === 0, 'a página NÃO recarrega (o <iframe> não é reiniciado)');
    ok(feliz.registro.aplicado !== null, 'a tela é remendada com o HTML da própria resposta');

    // 3. A marca de confirmado é APAGADA. Sem recarga o formulário continua na tela, e a marca
    //    que o confirmar.js deixou faria o SEGUNDO Finalizar não perguntar nada — um toque sem
    //    volta, aberto justamente pela correção.
    ok(f2.getAttribute('data-confirmado') === null,
        'a marca de "já confirmei" é apagada, senão o próximo Finalizar não pergunta nada');

    // 4. Dois toques não viram dois POSTs: o botão sai do ar enquanto a ação está indo.
    const doisToques = cenario();
    const f3 = formulario({ confirmado: true });
    doisToques.doc.disparar('submit', { target: f3, preventDefault: () => {} });
    ok(f3.botao.disabled === true, 'o botão é desabilitado enquanto o POST está no ar');
    await new Promise((r) => setTimeout(r, 20));

    console.log('── NADA DE FALHA CALADA ────────────────────────────────────────────────────');

    // 5. Sessão caiu: o 302 leva pro login e o fetch entrega 200 com o HTML do login. `ok` é
    //    true e não finalizou NADA. A prova aqui é a lista de jogos estar na resposta.
    const sessaoCaiu = cenario({ semLista: true });
    const f4 = formulario({ confirmado: true });
    await enviar(sessaoCaiu, f4);
    ok(sessaoCaiu.registro.recarregou === 1,
        'resposta 200 que NÃO é esta tela (login) recarrega a página, em vez de fingir que finalizou');

    // 6. O servidor recusou (403 do Forbid, 500): o aviso aparece e o botão volta.
    const recusado = cenario({ status: 403 });
    const f5 = formulario({ confirmado: true });
    await enviar(recusado, f5);
    ok(/403/.test(recusado.aviso.innerHTML), 'o servidor recusando aparece na tela, com o código (403)');
    ok(recusado.registro.aplicado === null, 'e nada é remendado com uma resposta que não vingou');
    ok(f5.botao.disabled === false, 'o botão volta, pra pessoa poder tentar de novo');

    // 7. Rede do clube caiu no meio: a tela não mente sobre o que não aconteceu.
    const semRede = cenario({ rede: 'caiu' });
    const f6 = formulario({ confirmado: true });
    await enviar(semRede, f6);
    ok(semRede.aviso.innerHTML.length > 0, 'rede caída também aparece na tela');
    ok(f6.botao.disabled === false, 'e o botão volta');

    console.log('── NAVEGADOR SEM FETCH ─────────────────────────────────────────────────────');

    // 8. Sem fetch, o envio de sempre acontece — com recarga, como era antes. Pior que o ideal,
    //    melhor que um botão que não faz nada.
    const antigo = cenario({ semFetch: true });
    const f7 = formulario({ confirmado: true });
    const impediu7 = await enviar(antigo, f7);
    ok(impediu7 === 0, 'sem window.fetch, o envio de sempre acontece (o botão nunca fica morto)');

    console.log(falhas === 0 ? '\nTUDO VERDE\n' : '\n' + falhas + ' FALHA(S)\n');
    process.exit(falhas === 0 ? 0 : 1);
})();
