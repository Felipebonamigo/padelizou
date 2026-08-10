// Ordenar e filtrar a tabela de professores do painel (/Admin/Professores).
//
// Roda no navegador, sem ida ao servidor: são poucas dezenas de linhas e a lista já vem
// inteira na página. Filtro que recarrega a tela a cada tecla seria mais lento e ainda perderia
// a posição da rolagem.
//
// ⚠️ A ORDENAÇÃO NUNCA OLHA O TEXTO DA TELA. Cada célula ordenável traz `data-ordem` com o
// valor cru — número invariante (`549.80`) e data como `yyyyMMdd`. Ordenando o que está escrito,
// "R$ 1.000,00" viria antes de "R$ 90,00" e 07/09/2027 antes de 15/08/2026, porque texto
// compara caractere a caractere.
(function () {
    var tabela = document.getElementById('tabela-professores');
    if (!tabela) return;

    var corpo = tabela.querySelector('tbody');
    var linhas = Array.prototype.slice.call(corpo.querySelectorAll('tr'));

    var campoNome = document.getElementById('filtro-nome');
    var campoSituacao = document.getElementById('filtro-situacao');
    var campoPagantes = document.getElementById('filtro-pagantes');
    var conta = document.getElementById('conta-professores');
    var nada = document.getElementById('nada-encontrado');

    var colunaAtual = null;
    var descendente = false;

    // ⚠️ Vazio vai SEMPRE pro fim, nos dois sentidos. "—" não é o menor valor, é ausência de
    // valor: ordenando "última aula" crescente, oito traços no topo pareceriam os mais antigos.
    function comparar(a, b, indice, tipo) {
        var va = (a.children[indice].getAttribute('data-ordem') || '');
        var vb = (b.children[indice].getAttribute('data-ordem') || '');

        if (va === '' && vb === '') return 0;
        if (va === '') return 1;
        if (vb === '') return -1;

        var resultado;
        if (tipo === 'numero') {
            resultado = parseFloat(va) - parseFloat(vb);
        } else {
            resultado = va.localeCompare(vb, 'pt-BR');
        }

        return descendente ? -resultado : resultado;
    }

    function ordenarPor(th) {
        var indice = parseInt(th.getAttribute('data-coluna'), 10);
        var tipo = th.getAttribute('data-tipo');

        // Mesmo cabeçalho de novo inverte; cabeçalho novo começa decrescente nos números
        // (quem mais pagou primeiro é o que se quer ver) e crescente no texto (A–Z).
        if (colunaAtual === indice) {
            descendente = !descendente;
        } else {
            colunaAtual = indice;
            descendente = tipo === 'numero';
        }

        var ordenadas = linhas.slice().sort(function (a, b) { return comparar(a, b, indice, tipo); });
        ordenadas.forEach(function (tr) { corpo.appendChild(tr); });

        tabela.querySelectorAll('th .pdz-seta').forEach(function (s) { s.textContent = ''; });
        th.querySelector('.pdz-seta').textContent = descendente ? '▼' : '▲';
    }

    function filtrar() {
        var termo = (campoNome && campoNome.value || '').trim().toLowerCase();
        var situacao = campoSituacao && campoSituacao.value || '';
        var soPagantes = campoPagantes && campoPagantes.checked;

        var visiveis = 0;

        linhas.forEach(function (tr) {
            var casaNome = !termo || (tr.getAttribute('data-procurar') || '').indexOf(termo) !== -1;
            var casaSituacao = !situacao || tr.getAttribute('data-situacao') === situacao;
            var casaPagante = !soPagantes || tr.getAttribute('data-pagante') === '1';

            var mostrar = casaNome && casaSituacao && casaPagante;
            tr.classList.toggle('d-none', !mostrar);
            if (mostrar) visiveis++;
        });

        if (conta) {
            conta.textContent = visiveis === linhas.length
                ? linhas.length + (linhas.length === 1 ? ' professor' : ' professores')
                : 'Mostrando ' + visiveis + ' de ' + linhas.length;
        }
        if (nada) nada.classList.toggle('d-none', visiveis > 0);
    }

    tabela.querySelectorAll('th[data-coluna]').forEach(function (th) {
        th.style.cursor = 'pointer';
        th.addEventListener('click', function () { ordenarPor(th); });
    });

    if (campoNome) campoNome.addEventListener('input', filtrar);
    if (campoSituacao) campoSituacao.addEventListener('change', filtrar);
    if (campoPagantes) campoPagantes.addEventListener('change', filtrar);

    filtrar();
})();
