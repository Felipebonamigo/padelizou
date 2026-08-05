async function votarPalpite(el) {
    if (el.dataset.votavel !== 'true') return;

    const container = el.closest('.pdz-palpitrometro');
    const partidaId = container.dataset.partidaId;
    const duplaId = el.dataset.duplaId;

    const response = await fetch('/Partidas/Votar', {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        body: `partidaId=${partidaId}&duplaId=${duplaId}`
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

    // Saiu do zero: o estado "ninguém palpitou" some. Sem isto o palpitrômetro continuava
    // com cara de apagado DEPOIS do voto, e quem acabou de votar concluía que não tinha
    // funcionado — foi o que aconteceu no Interno.
    if (data.totalVotos > 0) container.classList.remove('pdz-palpite-vazio');

    const totalEl = container.querySelector('.pdz-total-votos');
    if (totalEl) totalEl.innerText = data.totalVotos + ' voto(s)';
}

async function verVotos(partidaId, nome1, nome2) {
    const modalEl = document.getElementById('modalVerVotos');
    if (!modalEl) return;

    document.getElementById('modalVerVotosNome1').innerText = nome1;
    document.getElementById('modalVerVotosNome2').innerText = nome2;
    const lista1 = document.getElementById('modalVerVotosLista1');
    const lista2 = document.getElementById('modalVerVotosLista2');
    lista1.innerHTML = '<div class="text-muted small">Carregando...</div>';
    lista2.innerHTML = '';

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    const response = await fetch('/Partidas/VerVotos?partidaId=' + partidaId);
    const data = await response.json();

    function montarLista(votantes) {
        if (!votantes || votantes.length === 0) return '<div class="text-muted small">Ninguém votou nessa dupla ainda.</div>';
        return votantes.map(function (v) {
            var foto = v.fotoPerfil || '/img/default-avatar.png';
            return '<div class="d-flex align-items-center gap-2 mb-2"><img src="' + foto + '" class="rounded-circle" style="width:28px;height:28px;object-fit:cover;"><span>' + v.nome + '</span></div>';
        }).join('');
    }

    lista1.innerHTML = montarLista(data.votantesDupla1);
    lista2.innerHTML = montarLista(data.votantesDupla2);
}
