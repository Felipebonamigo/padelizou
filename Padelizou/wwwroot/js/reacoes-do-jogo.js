// REAGIR COM EMOJI EM CADA JOGO — 12/09/2026.
//
// 🗣️ Felipe: *"aqui, a cada jogo, permita a pessoa 'reagir' tipo o que tem aqui no discord, com
// emojis"* e *"e ao clicar no emoji, veja quem colocou o que, igual no whats app"*.
//
// O DESENHO, em uma frase: no cartão a pílula ABRE o painel; dentro do painel a pílula SOMA ou
// TIRA a minha. É o WhatsApp, e é o que ele pediu — um toque que somasse no cartão e outro que
// abrisse no painel seriam dois significados pro mesmo alvo de 32px.
//
// Conferido por `Padelizou.Tests/js/conferir-reacoes-do-jogo.js` (o `dotnet test` não enxerga
// arquivo .js; quem reprova o build é o CI, que varre o glob `conferir-*.js`).

// De que jogo é o painel que está aberto. O modal é UM só, reusado por todos os cartões da
// lista — sem isto, reagir depois de abrir dois jogos diferentes gravaria no jogo errado.
let pdzReacaoPartidaId = null;

// ⚠️ Nome e foto vêm do CADASTRO de quem reagiu — texto de gente, e esta lista é montada com
// innerHTML. Sem escapar, um nome com "<" quebra o painel e um nome montado de propósito injeta
// marcação na página. É a mesma guarda que o modal de votos já tem (palpitometro.js).
function pdzTexto(valor) {
    return String(valor == null ? '' : valor)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// Achar a fileira daquele jogo no cartão, pra repintar depois de somar ou tirar. Pode não estar
// mais na tela (a atualização automática troca o HTML da lista a cada 20s), e aí não há o que
// repintar — o painel continua valendo.
function pdzFileiraDoJogo(partidaId) {
    return document.querySelector('.pdz-reacoes[data-partida-id="' + partidaId + '"]');
}

// ── ABRIR O PAINEL ───────────────────────────────────────────────────────────────────────────
// Chamado pela pílula E pelo botão do teclado: os dois abrem a mesma coisa, e é de propósito.
async function verQuemReagiu(el) {
    const fileira = el.closest('.pdz-reacoes');
    if (!fileira) return;

    const modalEl = document.getElementById('modalQuemReagiu');
    if (!modalEl) return;

    pdzReacaoPartidaId = fileira.dataset.partidaId;

    const lista = document.getElementById('modalQuemReagiuLista');
    lista.innerHTML = '<div class="text-muted small">Carregando...</div>';
    pdzEsconderErro();
    const campo = document.getElementById('modalQuemReagiuCampo');
    if (campo) campo.value = '';

    bootstrap.Modal.getOrCreateInstance(modalEl).show();

    await pdzCarregarPainel();
}

async function pdzCarregarPainel() {
    const lista = document.getElementById('modalQuemReagiuLista');

    const response = await fetch('/Partidas/QuemReagiu?partidaId=' + pdzReacaoPartidaId);

    // ⚠️ OLHAR O `ok` ANTES DO `json()`: o jogo apagado (chave regerada com esta lista aberta)
    // responde 404, e a resposta de erro NÃO é JSON — sem esta guarda o parse estoura, ninguém
    // pega a promessa, e o painel fica em "Carregando..." pra sempre. Em produção o dedo bateu
    // três vezes no mesmo minuto por causa disso, no modal de votos, em 11/09.
    if (!response.ok) {
        lista.innerHTML = '<div class="small text-danger">' + (response.status === 404
            ? 'Este jogo saiu da lista — atualize a página.'
            : 'Não foi possível carregar quem reagiu. Tente de novo.') + '</div>';
        return;
    }

    pdzPintarLista(await response.json());
}

function pdzPintarLista(dados) {
    const lista = document.getElementById('modalQuemReagiuLista');
    const titulo = document.getElementById('modalQuemReagiuTitulo');
    const linhas = (dados && dados.linhas) || [];

    // A contagem no topo, como no print: "1 reação" / "N reações".
    if (titulo) titulo.textContent = linhas.length === 1 ? '1 reação' : linhas.length + ' reações';

    if (linhas.length === 0) {
        lista.innerHTML = '<div class="text-muted small">Ninguém reagiu a este jogo ainda.</div>';
        return;
    }

    lista.innerHTML = linhas.map(function (l) {
        const foto = pdzTexto(l.fotoPerfil || '/img/default-avatar.png');
        return '<div class="pdz-reacao-linha">'
            + '<img src="' + foto + '" alt="" class="pdz-reacao-foto">'
            + '<span class="pdz-reacao-nome">' + pdzTexto(l.nome) + '</span>'
            + '<span class="pdz-reacao-emoji-linha">' + pdzTexto(l.emoji) + '</span>'
            + '</div>';
    }).join('');
}

// As pílulas do painel, redesenhadas do resumo que o servidor devolve. A marcada é a minha, e
// tocar nela TIRA — é o "toque para remover" do WhatsApp, e por isso o título dela diz isso.
function pdzPintarPilulas(resumo) {
    const alvo = document.getElementById('modalQuemReagiuPilulas');
    if (!alvo) return;

    const reacoes = (resumo && resumo.reacoes) || [];
    alvo.innerHTML = reacoes.map(function (p) {
        return '<button type="button" class="pdz-reacao-pilula' + (p.euReagi ? ' pdz-reacao-minha' : '')
            + '" data-emoji="' + pdzTexto(p.emoji) + '"'
            + ' title="' + (p.euReagi ? 'Toque para remover a sua' : 'Reagir com este emoji') + '"'
            + ' onclick="alternarMinhaReacao(this)">'
            + '<span class="pdz-reacao-emoji">' + pdzTexto(p.emoji) + '</span>'
            + '<span class="pdz-reacao-conta">' + p.total + '</span>'
            + '</button>';
    }).join('');
}

// ── SOMAR E TIRAR ────────────────────────────────────────────────────────────────────────────
function alternarMinhaReacao(el) {
    const eraMinha = el.classList.contains('pdz-reacao-minha');
    return pdzFalarComOServidor(eraMinha ? '/Partidas/TirarReacao' : '/Partidas/Reagir', el.dataset.emoji);
}

function reagirComEmoji(emoji) {
    return pdzFalarComOServidor('/Partidas/Reagir', emoji);
}

function reagirDoCampo() {
    const campo = document.getElementById('modalQuemReagiuCampo');
    if (!campo || !campo.value.trim()) return;
    return pdzFalarComOServidor('/Partidas/Reagir', campo.value);
}

// ⚠️ UM POST POR VEZ, E A TRAVA É DO PAINEL — mesma régua da fila do palpitômetro. Duas
// respostas fora de ordem repintariam a fileira com a contagem velha, e a pílula recém-somada
// voltaria pro número anterior na frente da pessoa.
//
// ⚠️ O toque que chega no meio do envio NÃO se perde se disser outra coisa; se repetir o que já
// está indo, é o toque duplo e não há nada novo pra mandar (o servidor também sabe perder essa
// corrida — a chave composta (PartidaId, JogadorId, Emoji) é quem garante uma linha só).
let pdzReacaoEmVoo = null;
let pdzReacaoPendente = null;

async function pdzFalarComOServidor(rota, emoji) {
    const pedido = rota + '|' + emoji;

    if (pdzReacaoEmVoo) {
        if (pedido !== pdzReacaoEmVoo) pdzReacaoPendente = pedido;
        return;
    }

    try {
        let proximo = pedido;
        while (proximo) {
            pdzReacaoEmVoo = proximo;
            const corte = proximo.indexOf('|');
            await pdzEnviar(proximo.slice(0, corte), proximo.slice(corte + 1));

            proximo = pdzReacaoPendente;
            pdzReacaoPendente = null;
        }
    } finally {
        // A trava se solta aconteça o que acontecer: travada, o painel deixaria de aceitar
        // toque até o F5 — pior do que o erro que derrubou o envio.
        pdzReacaoEmVoo = null;
        pdzReacaoPendente = null;
    }
}

async function pdzEnviar(rota, emoji) {
    const corpo = 'partidaId=' + encodeURIComponent(pdzReacaoPartidaId) + '&emoji=' + encodeURIComponent(emoji);

    const response = await fetch(rota, {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        body: corpo
    });

    const dados = await response.json().catch(() => null);

    // ⚠️ O ERRO APARECE NO PAINEL, não num `alert`: a recusa mais comum aqui é "isso não é um
    // emoji", que é sobre o que a pessoa acabou de digitar no campo ao lado. Um alerta do
    // navegador tiraria o texto da tela junto com o motivo.
    if (!response.ok) {
        pdzMostrarErro((dados && dados.erro) || 'Não foi possível registrar sua reação.');
        return;
    }

    pdzEsconderErro();
    const campo = document.getElementById('modalQuemReagiuCampo');
    if (campo) campo.value = '';

    pdzPintarPilulas(dados);
    pdzPintarFileiraDoCartao(dados);
    await pdzCarregarPainel();
}

// A fileira do CARTÃO, redesenhada sem F5: a contagem muda no painel e ali atrás ao mesmo tempo.
function pdzPintarFileiraDoCartao(resumo) {
    const fileira = pdzFileiraDoJogo(pdzReacaoPartidaId);
    if (!fileira) return;

    const reacoes = (resumo && resumo.reacoes) || [];
    const pilulas = reacoes.map(function (p) {
        return '<button type="button" class="pdz-reacao-pilula' + (p.euReagi ? ' pdz-reacao-minha' : '')
            + '" data-emoji="' + pdzTexto(p.emoji) + '" data-eu="' + (p.euReagi ? 'true' : 'false') + '"'
            + ' title="Ver quem reagiu" onclick="verQuemReagiu(this)">'
            + '<span class="pdz-reacao-emoji">' + pdzTexto(p.emoji) + '</span>'
            + '<span class="pdz-reacao-conta">' + p.total + '</span>'
            + '</button>';
    }).join('');

    // ⚠️ O BOTÃO DO TECLADO É REMONTADO JUNTO e continua sendo o último: ele é o que sobra num
    // jogo sem reação nenhuma, e perdê-lo aqui deixaria o cartão sem caminho de volta depois de
    // a pessoa tirar a única reação que existia.
    fileira.innerHTML = pilulas
        + '<button type="button" class="pdz-reacao-abrir" title="Reagir com um emoji"'
        + ' aria-label="Reagir com um emoji" onclick="verQuemReagiu(this)">'
        + '<i class="bi bi-emoji-smile"></i></button>';
}

function pdzMostrarErro(mensagem) {
    const alvo = document.getElementById('modalQuemReagiuErro');
    if (!alvo) return;
    alvo.textContent = mensagem;
    alvo.hidden = false;
}

function pdzEsconderErro() {
    const alvo = document.getElementById('modalQuemReagiuErro');
    if (!alvo) return;
    alvo.textContent = '';
    alvo.hidden = true;
}
