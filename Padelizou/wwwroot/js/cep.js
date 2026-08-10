// Preenche cidade, UF, rua e bairro a partir do CEP — no cadastro e no Editar Perfil.
//
// Basta marcar o campo com data-cep e dar aos outros os ids padrão (pdzCidade, pdzEstado,
// pdzLogradouro, pdzBairro). O bloco de endereço fica escondido até o CEP dar certo: campo
// vazio que ninguém sabe preencher é o que faz a pessoa digitar "POA" e seguir em frente.
//
// ⚠️ Isto aqui é CONVENIÊNCIA, não é a verdade: quem grava reconsulta o CEP no servidor
// (Services/EnderecoPeloCep). Se este arquivo não rodar — JS desligado, cache velho, erro de
// rede —, o cadastro continua funcionando e o endereço é preenchido do mesmo jeito na
// gravação. Nada aqui pode ser o único caminho pra um dado chegar ao banco.
(function () {
    'use strict';

    function apenasDigitos(valor) {
        return (valor || '').replace(/\D/g, '');
    }

    function iniciar() {
        var campoCep = document.querySelector('[data-cep]');
        if (!campoCep) return;

        var cidade = document.getElementById('pdzCidade');
        var estado = document.getElementById('pdzEstado');
        var logradouro = document.getElementById('pdzLogradouro');
        var bairro = document.getElementById('pdzBairro');
        var recado = document.getElementById('pdzCepRecado');
        var bloco = document.getElementById('pdzEnderecoAchado');

        // O último CEP consultado: sem isso, tirar e repor um dígito dispararia a consulta
        // de novo, e sair/entrar no campo também.
        var ultimo = apenasDigitos(campoCep.value).length === 8 ? apenasDigitos(campoCep.value) : '';

        function dizer(texto, classe) {
            if (!recado) return;
            recado.textContent = texto || '';
            recado.className = 'd-block mt-1 small ' + (classe || 'text-muted');
        }

        function mostrarBloco(mostrar) {
            if (bloco) bloco.classList.toggle('d-none', !mostrar);
        }

        function preencher(dados) {
            if (cidade) cidade.value = dados.cidade || '';
            if (estado) estado.value = dados.uf || '';
            if (logradouro) logradouro.value = dados.logradouro || '';
            if (bairro) bairro.value = dados.bairro || '';
        }

        function consultar() {
            var digitos = apenasDigitos(campoCep.value);

            // Apagou o CEP: some com o bloco, mas NÃO limpa cidade/UF de quem já tinha —
            // apagar a cidade de alguém porque ele mexeu no CEP seria tirar o que ele já
            // tinha respondido, e o filtro do ranking depende dela.
            if (digitos.length === 0) {
                ultimo = '';
                dizer('');
                mostrarBloco(false);
                return;
            }

            if (digitos.length !== 8 || digitos === ultimo) return;
            ultimo = digitos;

            dizer('Procurando o endereço…');

            fetch('/Auth/BuscarCep?cep=' + digitos, { headers: { 'Accept': 'application/json' } })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (dados) {
                    if (!dados) {
                        // Inclui o 429 da trava: quem tentou demais não pode ver "CEP inválido".
                        dizer('Não deu pra consultar agora — pode preencher a cidade à mão.', 'text-muted');
                        mostrarBloco(true);
                        return;
                    }
                    if (dados.achou) {
                        preencher(dados);
                        dizer('Endereço encontrado.', 'text-success');
                        mostrarBloco(true);
                        return;
                    }
                    // "Consultou e não achou" é diferente de "não deu pra consultar": só o
                    // primeiro autoriza dizer que o CEP está errado.
                    dizer(dados.consultou
                        ? 'CEP não encontrado. Confira o número ou preencha a cidade à mão.'
                        : 'Não deu pra consultar agora — pode preencher a cidade à mão.',
                        dados.consultou ? 'text-danger' : 'text-muted');
                    mostrarBloco(true);
                })
                .catch(function () {
                    dizer('Não deu pra consultar agora — pode preencher a cidade à mão.', 'text-muted');
                    mostrarBloco(true);
                });
        }

        campoCep.addEventListener('input', consultar);
        campoCep.addEventListener('blur', consultar);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})();
