// Mesa de Controle que funciona sem internet.
//
// O ginásio é o pior lugar do mundo pra depender de 3G: parede de concreto, todo mundo no
// mesmo sinal. A regra aqui é uma só: O TOQUE DO ORGANIZADOR NUNCA SE PERDE. Cada toque
// atualiza a tela na hora (o aparelho é o dono do placar durante o jogo), vai pra uma fila
// no localStorage e é entregue quando a rede deixar — segundos depois ou meia hora depois.
//
// A fila guarda o PLACAR INTEIRO por partida, não os toques: de vinte toques presos, só o
// último estado importa, e placar absoluto reenviado duas vezes dá no mesmo lugar (o "+1"
// reentregue dobraria o game). O servidor decide por "marcadoEm" — o relógio DESTE aparelho
// no momento do toque — então placar velho preso na fila nunca atropela um mais novo.
const MesaOffline = (function () {
    let torneioId, limiteGames, contagem, chaveFila;
    const limitePorPartida = {};
    const estado = {};          // partidaId -> {games1, games2, sets1, sets2}
    let fila = {};              // partidaId -> {placar: {...estado, marcadoEm}, finalizar: ms|null}
    let enviando = false;
    let timerDebounce = null;

    // ---------- fila persistente ----------

    function carregarFila() {
        try { fila = JSON.parse(localStorage.getItem(chaveFila)) || {}; }
        catch { fila = {}; }
    }

    function salvarFila() {
        const vazia = Object.keys(fila).length === 0;
        if (vazia) localStorage.removeItem(chaveFila);
        else localStorage.setItem(chaveFila, JSON.stringify(fila));
    }

    function pendencias() {
        return Object.values(fila).reduce((n, p) => n + (p.placar ? 1 : 0) + (p.finalizar ? 1 : 0), 0);
    }

    // ---------- tela ----------

    function desenhar(id) {
        const e = estado[id];
        for (const [campo, elemento] of [["games1", "gamesA_"], ["games2", "gamesB_"], ["sets1", "setsA_"], ["sets2", "setsB_"]]) {
            const span = document.getElementById(elemento + id);
            if (span) span.innerText = e[campo];
        }
    }

    function badge(texto, classe) {
        const el = document.getElementById("mesaStatus");
        if (!el) return;
        el.textContent = texto;
        el.className = "pdz-mesa-status " + classe;
    }

    function atualizarBadge(erroDeRede) {
        const n = pendencias();
        if (n === 0) badge("Placar sincronizado", "pdz-mesa-ok");
        else if (erroDeRede) badge(`Sem internet — ${n} mudança(s) guardada(s) no aparelho. Pode continuar marcando.`, "pdz-mesa-offline");
        else badge(`Sincronizando ${n} mudança(s)...`, "pdz-mesa-enviando");
    }

    // ---------- toques ----------

    // Cada partida tem o limite da FASE dela: com "grupos até 4, final até 6" a mesma Mesa
    // mostra jogos de limites diferentes lado a lado. Sem partida informada, vale o do torneio.
    function limiteDaPartida(id) {
        return limitePorPartida[id] || limiteGames;
    }

    // Até onde ESTE lado pode ir AGORA. Mesma conta de Services/FormatoDaPartida.TetoDoLado.
    //
    // Na SOMA o bolo é fixo: numa soma de 7 com o adversário em 3, o máximo daqui é 4 — por
    // isso o teto depende de QUAL lado está sendo tocado, e não é mais um número só.
    //
    // No "até" o limite não é teto seco: jogo até 4 que empata em 3x3 vai até 5, até 6 que
    // empata em 5x5 vai até 7 — o "vencer por dois" do padel. Jogo até 9 não estende: 8x8 se
    // resolve no tie-break, e o 9º game é ele. Quem separa os casos é a paridade.
    function tetoDaPartida(id, campo) {
        const limite = limiteDaPartida(id);
        const e = estado[id];

        if (contagem === "Soma") {
            const doOutro = campo === "games2" ? e.games1 : e.games2;
            return Math.max(0, limite - doOutro);
        }

        if (limite <= 1 || limite % 2 !== 0) return limite;

        const empatouNaPenultima = e.games1 >= limite - 1 && e.games2 >= limite - 1;
        return empatouNaPenultima ? limite + 1 : limite;
    }

    // O jogo já pode ser encerrado? Na soma fecha quando os games ACABAM (o total foi
    // jogado); no "até" quando alguém alcança o teto. É o que acende o botão de finalizar.
    function fechou(id) {
        const e = estado[id];
        const limite = limiteDaPartida(id);
        if (contagem === "Soma") return e.games1 + e.games2 >= limite;
        return e.games1 >= tetoDaPartida(id, "games1") || e.games2 >= tetoDaPartida(id, "games2");
    }

    function tocar(id, campo, delta) {
        const e = estado[id];
        const limite = campo.startsWith("games") ? tetoDaPartida(id, campo) : 99;
        const fechouAntes = fechou(id);
        e[campo] = Math.min(limite, Math.max(0, e[campo] + delta));
        desenhar(id);

        // Aviso do limite só ao CRUZAR a linha — repetir a cada toque vira buzina.
        if (campo.startsWith("games") && fechou(id) && !fechouAntes) {
            const btn = document.getElementById("btnFim_" + id);
            if (btn) { btn.classList.replace("btn-dark", "btn-success"); btn.classList.add("shadow-lg"); }
        }

        fila[id] = fila[id] || {};
        fila[id].placar = { ...e, marcadoEm: Date.now() };
        salvarFila();
        atualizarBadge(false);

        // Meio segundo de espera junta a rajada de toques num envio só.
        clearTimeout(timerDebounce);
        timerDebounce = setTimeout(enviarFila, 500);
    }

    function finalizar(evento, id) {
        evento.preventDefault();   // sem JS o form posta normal; com JS a fila assume
        fila[id] = fila[id] || {};
        fila[id].finalizar = Date.now();
        salvarFila();

        const btn = document.getElementById("btnFim_" + id);
        if (btn) { btn.disabled = true; btn.innerText = "Finalizando..."; }

        enviarFila();
        return false;
    }

    // ---------- entrega ----------

    async function enviarFila() {
        if (enviando) return;
        if (pendencias() === 0) { atualizarBadge(false); return; }
        enviando = true;
        atualizarBadge(false);

        try {
            for (const id of Object.keys(fila)) {
                const item = fila[id];

                // O placar vai antes do finalizar da MESMA partida, sempre: finalizar grava
                // vencedor a partir do que está no banco.
                if (item.placar) {
                    const p = item.placar;
                    const corpo = `partidaId=${id}&games1=${p.games1}&games2=${p.games2}` +
                        `&sets1=${p.sets1}&sets2=${p.sets2}&marcadoEm=${p.marcadoEm}`;
                    const r = await fetch("/Torneios/SincronizarPlacar", {
                        method: "POST",
                        headers: cabecalhoAntifalsificacao({ "Content-Type": "application/x-www-form-urlencoded" }),
                        body: corpo,
                    });
                    if (!r.ok) throw new Error("servidor recusou o placar: " + r.status);

                    // "Já existe placar mais novo" também limpa a fila: pro aparelho, o
                    // servidor estar NA FRENTE é sucesso — e a tela adota o placar dele.
                    const resposta = await r.json();
                    if (!resposta.aplicado) {
                        estado[id] = { games1: resposta.games1, games2: resposta.games2, sets1: resposta.sets1, sets2: resposta.sets2 };
                        desenhar(id);
                    }
                    delete item.placar;
                    salvarFila();
                }

                if (item.finalizar) {
                    const r = await fetch("/Torneios/FinalizarPartida", {
                        method: "POST",
                        headers: cabecalhoAntifalsificacao({ "Content-Type": "application/x-www-form-urlencoded" }),
                        body: `partidaId=${id}`,
                    });
                    if (!r.ok) throw new Error("servidor recusou o finalizar: " + r.status);
                    delete item.finalizar;
                    salvarFila();

                    // Cartão sai da tela como sairia no fluxo antigo (a página recarregava).
                    const cartao = document.getElementById("cartao_" + id);
                    if (cartao) cartao.remove();
                }

                if (!fila[id].placar && !fila[id].finalizar) { delete fila[id]; salvarFila(); }
            }
            atualizarBadge(false);
        } catch {
            // Sem rede (ou servidor fora): a fila fica como está e a gente tenta de novo.
            // NADA é descartado — perder toque de placar é perder o jogo de alguém.
            atualizarBadge(true);
        } finally {
            enviando = false;
        }
    }

    // ---------- partida ----------

    function iniciar(config) {
        torneioId = config.torneioId;
        limiteGames = config.limiteGames || 9;
        // "Soma" = joga-se esse total de games e acabou; qualquer outra coisa (inclusive
        // ausente, que é a Mesa de um torneio antigo) é o "até" de sempre.
        contagem = config.contagem === "Soma" ? "Soma" : "Ate";
        chaveFila = "pdz-mesa-fila-v1-" + torneioId;

        // `partidas` aceita o Id solto (forma antiga) ou {id, games} — a Mesa passa o limite
        // da FASE de cada jogo, que é o que muda entre uma partida de grupo e uma final.
        for (const item of config.partidas) {
            const id = (item && typeof item === "object") ? item.id : item;
            if (item && typeof item === "object" && item.games > 0) limitePorPartida[id] = item.games;

            estado[id] = {
                games1: parseInt(document.getElementById("gamesA_" + id)?.innerText) || 0,
                games2: parseInt(document.getElementById("gamesB_" + id)?.innerText) || 0,
                sets1: parseInt(document.getElementById("setsA_" + id)?.innerText) || 0,
                sets2: parseInt(document.getElementById("setsB_" + id)?.innerText) || 0,
            };
        }

        // A fila local é MAIS NOVA que o HTML do servidor: se a página recarregou (inclusive
        // offline, servida pelo service worker), o que vale é o que o organizador marcou.
        carregarFila();
        for (const id of Object.keys(fila)) {
            if (fila[id].placar && estado[id]) {
                const { marcadoEm, ...placar } = fila[id].placar;
                estado[id] = placar;
                desenhar(id);
            }
        }

        // Três gatilhos de reenvio: a rede voltou, o relógio (a cada 5s se houver pendência),
        // e a própria carga da página.
        window.addEventListener("online", enviarFila);
        setInterval(() => { if (pendencias() > 0) enviarFila(); }, 5000);
        atualizarBadge(false);
        enviarFila();
    }

    return { iniciar, tocar, finalizar };
})();
