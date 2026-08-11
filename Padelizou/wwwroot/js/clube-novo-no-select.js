// "Jogaram em outro lugar? Cadastre aqui" — cria o clube e já o deixa escolhido no <select>.
//
// Vive num arquivo só porque as DUAS telas de jogo da panelinha precisam dele (registrar e
// corrigir). Copiado na segunda view, o dia em que o endpoint mudasse quebraria só uma das duas
// — e a que quebra é sempre a que ninguém abre pra testar.
function adicionarClubeSelect() {
    var input = document.getElementById('novoClubeNome');
    var nome = input.value.trim();
    if (!nome) return;

    fetch('/Clubes/Criar', {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        body: 'nome=' + encodeURIComponent(nome)
    })
        .then(function (res) { return res.json(); })
        .then(function (clube) {
            var select = document.getElementById('selectClube');
            var option = document.createElement('option');
            option.value = clube.id;
            option.text = clube.nome;
            option.selected = true;
            select.appendChild(option);
            input.value = '';
        });
}
