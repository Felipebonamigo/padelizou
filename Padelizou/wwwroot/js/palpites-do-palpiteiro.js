// O QUE UMA PESSOA PALPITOU NESTE TORNEIO — o modal que abre ao clicar no nome dela na tabela
// de palpiteiros (12/09/2026).
//
// 🗣️ Felipe, com o print da aba Palpiteiros no celular: *"ai clicar no nome, permita ver os
// resultados q a pessoa colocou mas de um modo que nao quebre a tela"*.
//
// ⚠️ "SEM QUEBRAR A TELA" É O REQUISITO, e ele é o motivo de cada escolha daqui: a lista abre
// num MODAL (`modal-dialog-scrollable`, rola por dentro, o X fica sempre visível) em vez de
// crescer dentro da tabela — uma linha que se expande empurra as de baixo e, com 41 palpites,
// some com a tabela inteira no celular. Dentro de cada jogo, tudo que pode ser longo (o nome
// das duplas, a categoria) TRUNCA com `min-width: 0`, e o que não pode quebrar (a ficha do
// placar, o ponto) leva `flex-shrink-0` — é a mesma receita do modal de quem votou, onde o
// nome comprido empurrava a ficha "9 x 0" pra fora da linha.
//
// ⚠️ Conferido por Padelizou.Tests/js/conferir-palpites-do-palpiteiro.js — DOM falso no Node,
// sem dependência. O `dotnet test` NÃO enxerga arquivo .js; quem roda é o CI, que varre
// `Padelizou.Tests/js/conferir-*.js` e reprova o build. Rode à mão antes de commitar.

// ⚠️ Nome, categoria e fase vêm do CADASTRO — texto de gente, não do sistema. A lista é montada
// com innerHTML, então tudo que vem do servidor passa por aqui antes: sem isso um nome com "<"
// quebra o modal, e um nome montado de propósito injeta marcação na página.
function pdzTextoSeguro(valor) {
    return String(valor == null ? '' : valor)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// A ficha "6 x 4". Vazia quando falta um dos lados: meio placar não é placar, e um "0 x 0" no
// lugar do vazio inventaria um palpite que ninguém deu.
function pdzFichaDePlacar(lado1, lado2, emSets, classes) {
    if (lado1 == null || lado2 == null) return '';
    return '<span class="badge ' + classes + ' flex-shrink-0 text-nowrap">'
        + Number(lado1) + ' x ' + Number(lado2)
        + (emSets ? ' <span class="fw-normal">sets</span>' : '')
        + '</span>';
}

// UM jogo da lista. Cada um é uma CAIXA, pelo mesmo motivo do modal de quem votou: sem
// moldura, quinze jogos em coluna viram um bloco de texto, e é a borda que faz o par
// palpite↔resultado ler como uma coisa só.
function pdzJogoPalpitado(linha) {
    var esperando = !linha.apurado;

    var icone = esperando ? 'bi-hourglass-split text-body-secondary'
        : linha.acertou ? 'bi-check-circle-fill text-success'
            : 'bi-x-circle-fill text-danger';

    // O selo da direita: o ponto que aquele palpite valeu. ⚠️ Quem estava EM QUADRA aparece
    // marcado em vez de sumir — o palpite dela existe, ela lembra de ter dado, e o que a linha
    // precisa dizer é por que ele não soma (é a mesma exclusão do ranking).
    var selo = '';
    if (linha.estavaEmQuadra) {
        selo = '<span class="badge bg-secondary-subtle text-body-secondary flex-shrink-0 text-nowrap"'
            + ' title="Quem está em quadra não pontua no próprio jogo">não conta</span>';
    } else if (linha.apurado) {
        selo = '<span class="badge flex-shrink-0 text-nowrap '
            + (linha.pontos > 0 ? 'bg-success-subtle text-success-emphasis' : 'bg-body-secondary text-body-secondary')
            + '">' + (linha.pontos > 0 ? '+' : '') + Number(linha.pontos)
            + (Math.abs(Number(linha.pontos)) === 1 ? ' ponto' : ' pontos') + '</span>';
    }

    var palpite = pdzFichaDePlacar(linha.palpitouEscolhida, linha.palpitouAdversaria, linha.emSets,
        'bg-primary-subtle text-primary-emphasis ms-auto');

    // Quem palpitou só o vencedor leva um traço, e não o vazio: com metade das linhas sem
    // ficha, a coluna da direita fica esburacada e parece defeito.
    if (!palpite) {
        palpite = '<span class="ms-auto flex-shrink-0 small text-body-secondary"'
            + ' title="Palpitou só quem vence">–</span>';
    }

    var resultado = esperando
        ? '<span class="ms-auto flex-shrink-0 text-nowrap">esperando resultado</span>'
        : linha.placarEscolhida != null && linha.placarAdversaria != null
            ? '<span class="ms-auto flex-shrink-0 text-nowrap">deu ' + Number(linha.placarEscolhida)
                + ' x ' + Number(linha.placarAdversaria) + '</span>'
            : '';

    var onde = [linha.categoria, linha.fase].filter(function (p) { return p; }).join(' · ');

    return '<div class="p-2 mb-2 border rounded-3 pdz-palpite-jogo">'
        + '<div class="d-flex align-items-center gap-2 small text-body-secondary mb-1">'
        + '<span class="text-truncate" style="min-width:0;">' + pdzTextoSeguro(onde) + '</span>'
        + (selo ? '<span class="ms-auto d-flex">' + selo + '</span>' : '')
        + '</div>'
        + '<div class="d-flex align-items-center gap-2">'
        + '<i class="bi ' + icone + ' flex-shrink-0"></i>'
        + '<span class="fw-semibold text-truncate" style="min-width:0;">' + pdzTextoSeguro(linha.escolhida) + '</span>'
        + palpite
        + '</div>'
        + '<div class="d-flex align-items-center gap-2 small text-body-secondary">'
        + '<span class="text-truncate" style="min-width:0;">contra ' + pdzTextoSeguro(linha.adversaria) + '</span>'
        + resultado
        + '</div>'
        + '</div>';
}

// A LISTA INTEIRA, com o que já valeu ponto em cima e o que espera resultado embaixo — a ordem
// vem pronta do servidor; aqui só entra o cabeçalho que separa os dois blocos.
function pdzListaDePalpites(linhas) {
    if (!linhas || linhas.length === 0) {
        return '<div class="text-muted small">Nenhum palpite neste torneio.</div>';
    }

    var html = '';
    var abriuEsperando = false;

    linhas.forEach(function (linha) {
        if (!linha.apurado && !abriuEsperando) {
            abriuEsperando = true;
            html += '<div class="small text-uppercase fw-bold text-body-secondary mt-3 mb-2"'
                + ' style="letter-spacing:.08em;">Esperando resultado</div>';
        }
        html += pdzJogoPalpitado(linha);
    });

    return html;
}

// O RESUMO DE CIMA — os mesmos números da linha da tabela, que é de onde o modal foi aberto.
// Se eles discordarem, é a tela que explica a conta desmentindo a conta (ver
// PalpitesDoPalpiteiroTests.Os_totais_da_lista_sao_EXATAMENTE_os_da_linha_da_tabela).
function pdzResumoDePalpites(dados) {
    var partes = [];
    partes.push('<strong>' + Number(dados.pontos) + (Number(dados.pontos) === 1 ? ' ponto' : ' pontos') + '</strong>');
    if (dados.palpites > 0) partes.push(Number(dados.acertos) + ' de ' + Number(dados.palpites) + ' acertos');
    if (dados.cravadas > 0) {
        partes.push(Number(dados.cravadas) + (Number(dados.cravadas) === 1 ? ' cravada' : ' cravadas'));
    }
    if (dados.emAberto > 0) partes.push(Number(dados.emAberto) + ' esperando resultado');
    return partes.join(' · ');
}

async function verPalpitesDoPalpiteiro(torneioId, jogadorId, nome) {
    var modalEl = document.getElementById('modalPalpitesDoPalpiteiro');
    if (!modalEl) return;

    var titulo = document.getElementById('pdzPalpitesNome');
    var resumo = document.getElementById('pdzPalpitesResumo');
    var lista = document.getElementById('pdzPalpitesLista');
    var perfil = document.getElementById('pdzPalpitesPerfil');

    // O nome vem do clique e entra por `innerText`: ele já está na tela, e o modal precisa ter
    // título ANTES da resposta chegar — carregar sem título é meio segundo sem saber de quem
    // é a lista que está abrindo.
    titulo.innerText = nome || 'Palpites';
    resumo.innerText = 'Carregando...';
    lista.innerHTML = '';
    if (perfil) perfil.setAttribute('href', '/Jogadores/Perfil/' + Number(jogadorId));

    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    var resposta = await fetch('/Torneios/PalpitesDoPalpiteiro/' + Number(torneioId)
        + '?jogadorId=' + Number(jogadorId));

    // ⚠️ OLHAR O `ok` ANTES DO `json()`, como no modal de quem votou: a resposta de erro NÃO é
    // JSON, e sem esta guarda o parse estoura, ninguém pega a promessa, e o modal fica em
    // "Carregando..." pra sempre — foi o que aconteceu em produção em 11/09/2026.
    if (!resposta.ok) {
        resumo.innerText = '';
        lista.innerHTML = '<div class="small text-danger">' + (resposta.status === 404
            ? 'Esta pessoa não tem palpite neste torneio — atualize a página.'
            : 'Não foi possível carregar os palpites. Tente de novo.') + '</div>';
        return;
    }

    var dados = await resposta.json();

    titulo.innerText = dados.jogador || nome || 'Palpites';
    resumo.innerHTML = pdzResumoDePalpites(dados);
    lista.innerHTML = pdzListaDePalpites(dados.linhas);
}
