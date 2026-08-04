// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// O servidor exige um carimbo antifalsificação em toda chamada que grava (filtro global no
// Program.cs). Formulário do Razor leva o campo escondido sozinho; fetch() não leva nada, então
// o valor vai pelo cabeçalho — que é onde o ASP.NET Core procura quando o corpo não é formulário.
//
// O carimbo é renderizado uma vez no _Layout, logo no começo do body, então qualquer tela tem.
// Definido aqui porque site.js é o primeiro script do layout: quem vier depois já encontra.
function cabecalhoAntifalsificacao(outros) {
    var campo = document.querySelector('input[name="__RequestVerificationToken"]');
    var cabecalhos = Object.assign({}, outros || {});
    cabecalhos['RequestVerificationToken'] = campo ? campo.value : '';
    return cabecalhos;
}

// Adiciona um clube novo via AJAX (sem recarregar a página, pra não perder o resto do formulário)
// e insere um checkbox já marcado na lista. Usado em Cadastro, Preferências e Criar Aviso.
function adicionarClube(nomeInputId, listaContainerId, checkboxName) {
    var input = document.getElementById(nomeInputId);
    var nome = input.value.trim();
    if (!nome) return;

    fetch('/Clubes/Criar', {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
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

// Máscara de telefone brasileiro: (XX) XXXXX-XXXX (celular) ou (XX) XXXX-XXXX (fixo).
function pdzFormatarTelefone(valor) {
    valor = valor.replace(/\D/g, '').slice(0, 11);
    if (valor.length === 0) return '';
    if (valor.length <= 2) return '(' + valor;
    if (valor.length <= 6) return '(' + valor.slice(0, 2) + ') ' + valor.slice(2);
    if (valor.length <= 10) return '(' + valor.slice(0, 2) + ') ' + valor.slice(2, 6) + '-' + valor.slice(6);
    return '(' + valor.slice(0, 2) + ') ' + valor.slice(2, 7) + '-' + valor.slice(7);
}

// Alterna um campo de senha entre oculto e visível — usado pelo botão "olhinho" ao lado do
// campo. O botão precisa estar dentro do mesmo .input-group que o input de senha.
function pdzAlternarSenha(botao) {
    var input = botao.closest('.input-group').querySelector('input');
    var vaiMostrar = input.type === 'password';
    input.type = vaiMostrar ? 'text' : 'password';
    botao.querySelector('i').className = vaiMostrar ? 'bi bi-eye-slash' : 'bi bi-eye';
}

// Aplica a máscara em todo input[type=tel] da página, à medida que o usuário digita.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('input[type="tel"]').forEach(function (input) {
        input.value = pdzFormatarTelefone(input.value);
        input.addEventListener('input', function () {
            input.value = pdzFormatarTelefone(input.value);
        });
    });
});

// ── Seta de voltar da barra de cima ──────────────────────────────────────────────────────
//
// O link já nasce com um destino de reserva no href (ver Services/NavegacaoDeVolta), então
// sem JavaScript ele continua levando a algum lugar. O que este trecho faz é preferir o
// caminho de verdade quando ele existe: voltar pra tela ANTERIOR, com a rolagem e a posição
// da lista onde a pessoa deixou — coisa que um href fixo nunca devolve.
//
// O `document.referrer` é o teste de "vim de dentro do site". Ele fica vazio quando alguém
// abriu o link do WhatsApp, tocou numa notificação ou entrou pelo app instalado: nesses casos
// history.back() sairia do Padelizou, e é justamente aí que o destino de reserva vale.
document.addEventListener('DOMContentLoaded', function () {
    var voltar = document.getElementById('pdzVoltar');
    if (!voltar) return;

    voltar.addEventListener('click', function (ev) {
        var veioDeDentro = document.referrer && document.referrer.indexOf(window.location.origin) === 0;
        if (veioDeDentro && window.history.length > 1) {
            ev.preventDefault();
            window.history.back();
        }
        // Sem histórico de dentro: deixa o link seguir pro destino de reserva.
    });
});
