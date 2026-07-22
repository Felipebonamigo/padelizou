// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Adiciona um clube novo via AJAX (sem recarregar a página, pra não perder o resto do formulário)
// e insere um checkbox já marcado na lista. Usado em Cadastro, Preferências e Criar Aviso.
function adicionarClube(nomeInputId, listaContainerId, checkboxName) {
    var input = document.getElementById(nomeInputId);
    var nome = input.value.trim();
    if (!nome) return;

    fetch('/Clubes/Criar', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'nome=' + encodeURIComponent(nome)
    })
        .then(function (res) { return res.json(); })
        .then(function (clube) {
            var container = document.getElementById(listaContainerId);
            var wrapper = document.createElement('div');
            wrapper.className = 'form-check form-check-inline';
            wrapper.innerHTML = '<input class="form-check-input clube-check" type="checkbox" checked name="' + checkboxName + '" value="' + clube.id + '" id="clube_' + clube.id + '">' +
                '<label class="form-check-label small" for="clube_' + clube.id + '">' + clube.nome + '</label>';
            container.appendChild(wrapper);
            input.value = '';
        });
}

// Desmarca todos os checkboxes de uma dimensão de preferência (categoria/clube/diahorario) —
// usado pelos botões "Aceito qualquer X".
function desmarcarTodos(classe) {
    document.querySelectorAll('.' + classe + '-check').forEach(function (el) { el.checked = false; });
}
