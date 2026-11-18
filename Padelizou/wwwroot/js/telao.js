// O TELÃO SE ATUALIZA SOZINHO (18/11/2026) — Views/Torneios/Telao.cshtml.
//
// ⚠️ ES5 DE PROPÓSITO: navegador de Smart TV é velho. Nada de const/let, arrow, fetch, template
// string ou módulo — XMLHttpRequest e var. Se este arquivo nem rodar, o `meta refresh` de 10 min
// da página ainda atualiza a TV.
//
// A cada 20 s busca só o miolo (?parcial=true) e troca o conteúdo, sem piscar a tela. Se a rede
// cair, mantém o último quadro e o rodapé passa a dizer há quanto tempo foi a última atualização —
// a TV nunca mostra uma tela de erro.
(function () {
    var script = document.currentScript || document.querySelector('script[data-parcial]');
    var urlParcial = script && script.getAttribute('data-parcial');
    var conteudo = document.getElementById('telao-conteudo');
    var atualizado = document.getElementById('telao-atualizado');
    var relogio = document.getElementById('telao-relogio');
    var ultimaVez = new Date().getTime();
    var INTERVALO = 20000;

    function doisDigitos(n) { return n < 10 ? '0' + n : '' + n; }

    function marcarIdade() {
        if (!atualizado) return;
        var minutos = Math.floor((new Date().getTime() - ultimaVez) / 60000);
        if (minutos < 1) {
            atualizado.textContent = 'atualizado agora';
            atualizado.className = 'tl-atualizado';
        } else {
            atualizado.textContent = 'sem conexão · atualizado há ' + minutos + ' min';
            atualizado.className = 'tl-atualizado tl-velho';
        }
    }

    function buscar() {
        if (!urlParcial || !conteudo) return;
        var xhr = new XMLHttpRequest();
        xhr.open('GET', urlParcial, true);
        xhr.timeout = 15000;
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;
            if (xhr.status === 200 && xhr.responseText) {
                conteudo.innerHTML = xhr.responseText;
                ultimaVez = new Date().getTime();
            }
            marcarIdade();
        };
        try { xhr.send(); } catch (e) { marcarIdade(); }
    }

    function tiqueDoRelogio() {
        if (!relogio) return;
        var agora = new Date();
        relogio.textContent = doisDigitos(agora.getHours()) + ':' + doisDigitos(agora.getMinutes());
    }

    // A TV não apaga a tela onde o navegador deixa (Wake Lock). Onde não deixa, segue sem.
    function manterAcesa() {
        try {
            if (navigator.wakeLock && navigator.wakeLock.request) {
                navigator.wakeLock.request('screen').then(null, function () { });
            }
        } catch (e) { }
    }

    setInterval(buscar, INTERVALO);
    setInterval(tiqueDoRelogio, 15000);
    manterAcesa();
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') manterAcesa();
    });
})();
