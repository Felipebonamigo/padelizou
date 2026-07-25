async function votarPalpite(el) {
    if (el.dataset.votavel !== 'true') return;

    const container = el.closest('.pdz-palpitrometro');
    const partidaId = container.dataset.partidaId;
    const duplaId = el.dataset.duplaId;

    const response = await fetch('/Partidas/Votar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
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

    badge1.innerHTML = data.percentualDupla1 + '%<i class="bi bi-caret-down-fill"></i>';
    badge2.innerHTML = data.percentualDupla2 + '%<i class="bi bi-caret-down-fill"></i>';
    badge1.style.display = dupla1Lidera ? 'block' : 'none';
    badge2.style.display = dupla1Lidera ? 'none' : 'block';

    container.querySelector('[data-bar="1"]').style.width = data.percentualDupla1 + '%';
    container.querySelector('[data-bar="2"]').style.width = data.percentualDupla2 + '%';

    container.querySelectorAll('.palpite-opcao').forEach(op => {
        const souEuAgora = String(data.meuVotoDuplaId) === op.dataset.duplaId;
        op.classList.toggle('fw-bold', souEuAgora);
        op.innerText = (souEuAgora ? '✓ ' : '') + op.innerText.replace(/^✓ /, '');
    });

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
