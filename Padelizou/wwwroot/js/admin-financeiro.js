// O formulário de registrar pagamento: mensalidade pede QUEM pagou, taxa pede QUAL torneio.
// Mostrar os dois ao mesmo tempo faria o admin preencher um campo que vai ser ignorado.
(function () {
    var tipo = document.getElementById('reg-tipo');
    var campoPagador = document.getElementById('campo-pagador');
    var campoTorneio = document.getElementById('campo-torneio');
    if (!tipo || !campoPagador || !campoTorneio) return;

    function ajustar() {
        var ehTaxa = tipo.value === 'TaxaTorneio';
        campoPagador.classList.toggle('d-none', ehTaxa);
        campoTorneio.classList.toggle('d-none', !ehTaxa);
        // required acompanha a visibilidade — campo escondido e obrigatório trava o envio
        // com um erro que ninguém vê.
        document.getElementById('reg-pagador').required = !ehTaxa;
        document.getElementById('reg-torneio').required = ehTaxa;
    }

    tipo.addEventListener('change', ajustar);
    ajustar();
})();
