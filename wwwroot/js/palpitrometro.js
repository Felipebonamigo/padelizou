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
    container.querySelector('[data-pct="1"]').innerText = data.percentualDupla1 + '%';
    container.querySelector('[data-pct="2"]').innerText = data.percentualDupla2 + '%';
    container.querySelector('[data-bar="1"]').style.width = data.percentualDupla1 + '%';
    container.querySelector('[data-bar="2"]').style.width = data.percentualDupla2 + '%';

    container.querySelectorAll('.palpite-opcao').forEach(op => {
        const span = op.querySelector('span.text-truncate');
        const souEuAgora = String(data.meuVotoDuplaId) === op.dataset.duplaId;
        span.classList.toggle('fw-bold', souEuAgora);
        span.innerText = (souEuAgora ? '✓ ' : '') + span.innerText.replace(/^✓ /, '');
    });

    const totalEl = container.querySelector('.pdz-total-votos');
    if (totalEl) totalEl.innerText = data.totalVotos + ' voto(s)';
}
