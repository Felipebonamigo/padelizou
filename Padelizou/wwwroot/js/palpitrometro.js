async function votarPalpite(el) {
    if (el.dataset.votavel !== 'true') return;

    const container = el.closest('.pdz-palpitrometro');

    // ⚠️ Trocar de dupla vai SEM placar, e o servidor apaga o que estava gravado. É de
    // propósito: o placar velho apontava a outra dupla, e mantê-lo deixaria a linha dizendo
    // duas coisas contrárias ao mesmo tempo. Quem trocou de opinião escolhe a ficha de novo.
    await enviarPalpite(container, el.dataset.duplaId);
}

// A ficha de placar. Ela mostra VENCEDOR x PERDEDOR — sem lado —, e é aqui que o número ganha
// dono: se eu votei na Dupla 1, o vencedor é o lado 1; se votei na Dupla 2, ele é o lado 2.
// O servidor recebe sempre na orientação do JOGO, que é a mesma das colunas da partida.
async function palpitarPlacar(el) {
    const container = el.closest('.pdz-palpitrometro');
    const meuVoto = container.dataset.meuVoto;

    // Sem voto não há lado pro placar — a tela já esconde as fichas, isto é o cinto.
    if (!meuVoto) return;

    const vencedor = Number(el.dataset.vencedor);
    const perdedor = Number(el.dataset.perdedor);
    const souDupla1 = container.dataset.dupla1Id === meuVoto;

    await enviarPalpite(container, meuVoto,
        souDupla1 ? vencedor : perdedor,
        souDupla1 ? perdedor : vencedor);
}

// ABRIR AS FICHAS DE NOVO. Só tela: trocar de ideia sobre o placar não é palpite nenhum até
// a ficha ser tocada, e um POST aqui gravaria uma intenção que a pessoa ainda não teve.
function trocarPlacar(el) {
    const bloco = el.closest('.pdz-palpite-placar');
    if (!bloco) return;

    const resumo = bloco.querySelector('.pdz-palpite-placar-resumo');
    const fichas = bloco.querySelector('.pdz-palpite-fichas');
    if (resumo) resumo.style.display = 'none';
    if (fichas) fichas.style.display = 'block';
}

// O ÚNICO lugar que fala com o servidor. Voto e placar são o mesmo POST porque são o mesmo
// palpite: duas rotas gravariam a mesma linha por caminhos diferentes, e é assim que nasce a
// linha com voto de uma dupla e placar da outra.
async function enviarPalpite(container, duplaId, placar1, placar2) {
    const partidaId = container.dataset.partidaId;

    let corpo = `partidaId=${partidaId}&duplaId=${duplaId}`;
    if (placar1 != null && placar2 != null) corpo += `&placar1=${placar1}&placar2=${placar2}`;

    return enfileirar(container, '/Partidas/Votar?' + corpo);
}

// RETIRAR O PALPITE (10/09/2026 — 🗣️ Felipe: *"tambem permita retirar o palpite colocado"*).
//
// ⚠️ VAI PELA MESMA FILA do voto e da ficha, e não por um fetch solto: retirar e votar mexem na
// MESMA linha do banco, então dois pedidos soltos podem se cruzar e a tela terminaria pintada
// com o que respondeu por último — o palpite reaparecendo depois de retirado. Quem decide se
// dá pra retirar é o servidor (só enquanto o jogo está agendado).
async function retirarPalpite(el) {
    const container = el.closest('.pdz-palpitrometro');
    if (!container) return;

    return enfileirar(container, '/Partidas/RetirarPalpite?partidaId=' + container.dataset.partidaId);
}

// ⚠️ UM POST POR VEZ, E A TRAVA É POR CARTÃO — mesma régua do `placar-ao-vivo.js`. Dois jogos
// na mesma tela não têm nada a ver um com o outro e não esperam um pelo outro.
//
// Sem ela, o toque duplo no nome dispara dois POSTs que leem "esse jogador ainda não votou" ao
// mesmo tempo e tentam INSERIR os dois: era o `DbUpdateException em POST /Partidas/Votar` de
// 10/09 em produção. ⚠️ O servidor também sabe perder essa corrida (`PalpiteService`), e as duas
// travas são necessárias: esta poupa a requisição gêmea DESTA aba, e a de lá é a que segura
// duas abas, dois aparelhos e o POST montado à mão — trava de tela não atravessa a rede.
//
// E ela conserta o que o servidor não alcança: **duas respostas voltam fora de ordem**. Com o
// primeiro POST lento, a resposta dele chegava DEPOIS da do segundo e repintava a tela com o
// palpite velho — a ficha recém-escolhida apagava sozinha na frente da pessoa, e só o F5
// consertava.
// ⚠️ `pedido` é a linha inteira — `rota?corpo` numa string só. Ela mora no `dataset`, que só
// sabe guardar texto, e é comparada inteira pra saber se o toque repetiu: sem a rota junto, um
// "retirar" chegando no meio de um voto do MESMO jogo pareceria o mesmo pedido.
async function enfileirar(container, pedido) {
    // ⚠️ Toque que chega no meio do envio NÃO se perde (mesma régua do placar-ao-vivo.js): se
    // diz outra coisa — trocou de dupla, escolheu ficha, retirou —, ele vai assim que a vez
    // chegar. Se repete o que já está indo, é o toque duplo: não há nada novo pra mandar.
    if (container.dataset.palpiteEmVoo) {
        if (pedido !== container.dataset.palpiteEmVoo) container.dataset.palpitePendente = pedido;
        return;
    }

    try {
        let proximo = pedido;
        while (proximo) {
            container.dataset.palpiteEmVoo = proximo;
            await falarComOServidor(container, proximo);

            proximo = container.dataset.palpitePendente || '';
            delete container.dataset.palpitePendente;
        }
    } finally {
        // A trava se solta aconteça o que acontecer. Travado pra sempre, o palpitrômetro
        // deixaria de aceitar toque até o F5 — pior do que o erro que derrubou o envio.
        delete container.dataset.palpiteEmVoo;
        delete container.dataset.palpitePendente;
    }
}

async function falarComOServidor(container, pedido) {
    const corte = pedido.indexOf('?');
    const rota = pedido.slice(0, corte);
    const corpo = pedido.slice(corte + 1);

    const response = await fetch(rota, {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        body: corpo
    });

    const data = await response.json().catch(() => null);

    if (!response.ok) {
        alert((data && data.erro) || 'Não foi possível registrar seu palpite.');
        return;
    }


    atualizarPalpitrometro(container, data);
}

function atualizarPalpitrometro(container, data) {
    const badge1 = container.querySelector('[data-pct="1"]');
    const badge2 = container.querySelector('[data-pct="2"]');
    const dupla1Lidera = data.percentualDupla1 >= data.percentualDupla2;

    // Duas apresentações do MESMO palpitrômetro. No cartão grande só o lado que lidera
    // mostra o número, com uma seta apontando a barra. Na LINHA das listas os dois números
    // ficam sempre à vista, um de cada lado — ali eles são o palpitrômetro inteiro, e
    // esconder um deixaria a linha dizendo pela metade.
    const emLinha = container.classList.contains('pdz-palpite-linha');

    if (emLinha) {
        badge1.textContent = data.percentualDupla1 + '%';
        badge2.textContent = data.percentualDupla2 + '%';
    } else {
        badge1.innerHTML = data.percentualDupla1 + '%<i class="bi bi-caret-down-fill"></i>';
        badge2.innerHTML = data.percentualDupla2 + '%<i class="bi bi-caret-down-fill"></i>';
        badge1.style.display = dupla1Lidera ? 'block' : 'none';
        badge2.style.display = dupla1Lidera ? 'none' : 'block';
    }

    // A barra só existe no cartão grande. Na LINHA das listas os dois números fazem o
    // papel dela, e sem este null-check o voto quebrava a atualização no meio — o placar
    // ia pro servidor e a tela não mexia, que é o pior dos dois mundos.
    const barra1 = container.querySelector('[data-bar="1"]');
    const barra2 = container.querySelector('[data-bar="2"]');
    if (barra1) barra1.style.width = data.percentualDupla1 + '%';
    if (barra2) barra2.style.width = data.percentualDupla2 + '%';

    // O ✓ na frente do nome só existe no cartão grande; na linha o campo é o próprio
    // número, e reescrever o texto dele apagaria a porcentagem.
    container.querySelectorAll('.palpite-opcao').forEach(op => {
        const souEuAgora = String(data.meuVotoDuplaId) === op.dataset.duplaId;
        op.classList.toggle('fw-bold', souEuAgora);
        op.innerText = (souEuAgora ? '✓ ' : '') + op.innerText.replace(/^✓ /, '');
    });

    // Marca de "foi em quem eu votei", nas duas apresentações.
    container.querySelectorAll('[data-dupla-id]').forEach(op => {
        op.classList.toggle('pdz-palpite-meu', String(data.meuVotoDuplaId) === op.dataset.duplaId);
    });

    // O "retirar" só existe enquanto existe palpite meu — senão ele fica na tela oferecendo
    // desfazer o que já foi desfeito. (`hidden`, e não `display`, porque é a mesma chave que o
    // Razor usa pra nascer escondido.)
    container.querySelectorAll('.pdz-retirar-palpite').forEach(function (botao) {
        botao.hidden = data.meuVotoDuplaId == null;
    });

    // Saiu do zero: o estado "ninguém palpitou" some. Sem isto o palpitrômetro continuava
    // com cara de apagado DEPOIS do voto, e quem acabou de votar concluía que não tinha
    // funcionado — foi o que aconteceu no Interno.
    if (data.totalVotos > 0) container.classList.remove('pdz-palpite-vazio');

    const totalEl = container.querySelector('.pdz-total-votos');
    if (totalEl) totalEl.innerText = data.totalVotos + ' voto(s)';

    atualizarPlacarDoPalpite(container, data);
}

// A parte do PLACAR: as fichas e a frase da galera. Tudo aqui é null-safe porque a versão EM
// LINHA do palpitrômetro não tem nada disto — e foi um null-check faltando neste arquivo que
// já fez o voto ir pro servidor sem a tela mexer, que é o pior dos dois mundos.
function atualizarPlacarDoPalpite(container, data) {
    // Quem sou eu agora, pra próxima ficha saber de que lado orientar o placar.
    container.dataset.meuVoto = data.meuVotoDuplaId != null ? String(data.meuVotoDuplaId) : '';

    const bloco = container.querySelector('.pdz-palpite-placar');
    if (bloco) {
        // O bloco nasce escondido pra quem ainda não votou: a ficha "6x4" não tem lado nenhum
        // antes de existir um voto.
        bloco.style.display = data.meuVotoDuplaId != null ? 'block' : 'none';

        // A ficha marcada. ⚠️ Comparar SEM lado (maior × menor) é o que faz o destaque
        // sobreviver a quem votou na Dupla 2: lá o meu "6" é o lado 2, e comparar lado a lado
        // não acharia ficha nenhuma.
        const temPlacar = data.meuPlacarLado1 != null && data.meuPlacarLado2 != null;
        const meuVencedor = temPlacar ? Math.max(data.meuPlacarLado1, data.meuPlacarLado2) : null;
        const meuPerdedor = temPlacar ? Math.min(data.meuPlacarLado1, data.meuPlacarLado2) : null;

        // ⚠️ DEPOIS DE ESCOLHER, AS FICHAS SE RECOLHEM (11/09/2026 — 🗣️ Felipe: *"podemos
        // 'minimizar' os placares depois de votado, pra nao ficar poluindo a tela"*). Fica o
        // resumo com o placar escolhido; as fichas voltam pelo "trocar", sem passar pelo
        // servidor. Null-safe pelo motivo de sempre: nem toda apresentação tem os dois.
        const resumo = bloco.querySelector('.pdz-palpite-placar-resumo');
        const fichas = bloco.querySelector('.pdz-palpite-fichas');
        if (resumo && fichas) {
            resumo.style.display = temPlacar ? 'flex' : 'none';
            fichas.style.display = temPlacar ? 'none' : 'block';

            const escrito = resumo.querySelector('.pdz-resumo-placar');
            if (escrito && temPlacar) escrito.innerText = meuVencedor + ' x ' + meuPerdedor;
        }

        bloco.querySelectorAll('.pdz-ficha-placar').forEach(function (ficha) {
            const escolhida = temPlacar
                && Number(ficha.dataset.vencedor) === meuVencedor
                && Number(ficha.dataset.perdedor) === meuPerdedor;

            ficha.classList.toggle('btn-success', escolhida);
            ficha.classList.toggle('btn-outline-secondary', !escolhida);
        });
    }

    const consenso = container.querySelector('.pdz-palpite-consenso');
    if (!consenso) return;

    const temConsenso = data.placarMaisPalpitadoLado1 != null && data.placarMaisPalpitadoLado2 != null;
    consenso.style.display = temConsenso ? 'block' : 'none';
    if (!temConsenso) return;

    const placarEl = consenso.querySelector('.pdz-consenso-placar');
    const votosEl = consenso.querySelector('.pdz-consenso-votos');
    if (placarEl) placarEl.innerText = data.placarMaisPalpitadoLado1 + ' x ' + data.placarMaisPalpitadoLado2;
    if (votosEl) votosEl.innerText = '(' + data.placarMaisPalpitadoVotos + ' de ' + data.palpitesComPlacar + ')';
}

async function verVotos(partidaId, nome1, nome2) {
    const modalEl = document.getElementById('modalVerVotos');
    if (!modalEl) return;

    const titulo1 = document.getElementById('modalVerVotosNome1');
    const titulo2 = document.getElementById('modalVerVotosNome2');
    titulo1.innerText = nome1;
    titulo2.innerText = nome2;
    const lista1 = document.getElementById('modalVerVotosLista1');
    const lista2 = document.getElementById('modalVerVotosLista2');
    lista1.innerHTML = '<div class="text-muted small">Carregando...</div>';
    lista2.innerHTML = '';

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    const response = await fetch('/Partidas/VerVotos?partidaId=' + partidaId);
    const data = await response.json();

    // ⚠️ Nome e foto vêm do CADASTRO de quem votou — texto de gente, não do sistema. Como esta
    // lista é montada com innerHTML, tudo que vem do servidor passa por aqui antes: sem isso um
    // nome com "<" quebra o modal, e um nome montado de propósito injeta marcação na página.
    function texto(valor) {
        return String(valor == null ? '' : valor)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function montarLista(votantes) {
        if (!votantes || votantes.length === 0) return '<div class="text-muted small">Ninguém votou nessa dupla ainda.</div>';
        return votantes.map(function (v) {
            var foto = texto(v.fotoPerfil || '/img/default-avatar.png');

            // ⚠️ O placar é OPCIONAL e continua sendo: quem só disse quem vence aparece só com o
            // nome. Um "0 x 0" no lugar do vazio inventaria um palpite que ninguém deu.
            // ⚠️ `flex-shrink-0` e `text-nowrap`: a ficha é o ÚLTIMO item da linha e, sem travar,
            // o nome longo a espremia até o "9 x 0" quebrar em duas linhas.
            var placar = v.placarVencedor != null && v.placarPerdedor != null
                ? '<span class="badge bg-success-subtle text-success-emphasis ms-auto flex-shrink-0 text-nowrap">'
                    + v.placarVencedor + ' x ' + v.placarPerdedor
                    + (v.placarEmSets ? ' <span class="fw-normal">sets</span>' : '')
                    + '</span>'
                // ⚠️ QUEM NÃO PALPITOU PLACAR LEVA UM TRAÇO, e não o vazio de antes: com metade
                // das linhas sem ficha, a coluna da direita ficava esburacada e parecia defeito.
                // O lugar do placar é sempre o mesmo, e o olho corre a lista numa passada.
                : '<span class="ms-auto flex-shrink-0 small text-body-secondary pdz-votante-sem-placar"'
                    + ' title="Palpitou só quem vence">–</span>';

            // ⚠️ `width:28px` num filho de flex é só o tamanho BASE — ele encolhe por padrão, e a
            // foto redonda saía oval ao lado de nome comprido.
            //
            // O nome vem ABREVIADO do servidor (NomeBonito.Curto), então o corte quase nunca
            // dispara — ele é a rede pro nome longo em tela estreita. ⚠️ `text-truncate` sozinho
            // não corta nada aqui: num flex o item se recusa a encolher abaixo do próprio
            // conteúdo, e sem `min-width:0` quem sai empurrado pra fora é a ficha do placar.
            // ⚠️ CADA VOTANTE É UMA CAIXA (11/09/2026). 🗣️ Felipe, num print do modal com 14
            // nomes em coluna: *"deixe um 'quadrado' ou algo assim, fica dificil ver quem fez o
            // que nessa tela"*. Sem moldura, catorze linhas viram um bloco de texto — e é a
            // borda que faz o par nome↔placar ler como uma coisa só.
            return '<div class="d-flex align-items-center gap-2 mb-2 p-2 border rounded-3 pdz-votante"><img src="' + foto
                + '" class="rounded-circle flex-shrink-0" style="width:28px;height:28px;object-fit:cover;">'
                + '<span class="text-truncate" style="min-width:0;">' + texto(v.nome) + '</span>'
                + placar + '</div>';
        }).join('');
    }

    // A contagem ao lado do nome da dupla: é ela que diz de cara pra que lado a galera pendeu —
    // a barra do palpitrômetro fica fora do modal.
    titulo1.innerText = nome1 + ' · ' + (data.votantesDupla1 || []).length;
    titulo2.innerText = nome2 + ' · ' + (data.votantesDupla2 || []).length;

    lista1.innerHTML = montarLista(data.votantesDupla1);
    lista2.innerHTML = montarLista(data.votantesDupla2);
}
