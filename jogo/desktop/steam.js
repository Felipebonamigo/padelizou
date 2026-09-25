// PUNHOS DE SHAOLIN — a Steam, por trás de uma porta que pode estar fechada.
//
// `steamworks.js` é dependência OPCIONAL: sem ela instalada, ou com a Steam fechada, tudo aqui
// vira no-op com um aviso no log e o jogo roda igual. O AppID vem de `steam_appid.txt` ao lado
// deste arquivo (ou da variável PUNHOS_APPID); sem nenhum dos dois, usa o 480 — o app de teste
// da Valve ("Spacewar"), que qualquer conta consegue rodar antes do AppID de verdade existir.
'use strict';
const fs = require('fs');
const path = require('path');

let cliente = null;
let biblioteca = null;

function appId() {
    if (process.env.PUNHOS_APPID) return Number(process.env.PUNHOS_APPID) || 480;
    try { return Number(fs.readFileSync(path.join(__dirname, 'steam_appid.txt'), 'utf8').trim()) || 480; } catch (_) { return 480; }
}

function carregarBiblioteca() {
    if (biblioteca !== null) return biblioteca;
    try { biblioteca = require('steamworks.js'); }
    catch (erro) { biblioteca = false; console.log('Steam: steamworks.js não está instalado — seguindo sem a Steam.', erro.message); }
    return biblioteca;
}

function prepararOverlay() {
    const sw = carregarBiblioteca();
    try { if (sw && sw.electronEnableSteamOverlay) sw.electronEnableSteamOverlay(); } catch (erro) { console.log('Steam: overlay indisponível:', erro.message); }
}

function iniciar() {
    const sw = carregarBiblioteca();
    if (!sw) return false;
    try {
        cliente = sw.init(appId());
        console.log(`Steam: conectada como ${cliente.localplayer.getName()} (app ${appId()})`);
        return true;
    } catch (erro) {
        cliente = null;
        console.log('Steam: fechada ou sem o app — seguindo sem ela.', erro.message);
        return false;
    }
}

function ativo() { return !!cliente; }

function conquistar(id) {
    if (!cliente) return;
    try { if (!cliente.achievement.isActivated(id)) cliente.achievement.activate(id); }
    catch (erro) { console.log('Steam: conquista recusada', id, erro.message); }
}

function estatistica(nome, valor) {
    if (!cliente) return;
    try { cliente.stats.setInt(nome, Math.round(valor)); cliente.stats.store(); }
    catch (erro) { console.log('Steam: estatística recusada', nome, erro.message); }
}

function presenca(texto) {
    if (!cliente) return;
    try { cliente.localplayer.setRichPresence('status', String(texto).slice(0, 200)); }
    catch (erro) { console.log('Steam: presença recusada', erro.message); }
}

function encerrar() {
    if (!cliente) return;
    try { if (cliente.stats && cliente.stats.store) cliente.stats.store(); } catch (_) { /* já foi */ }
    cliente = null;
}

module.exports = { prepararOverlay, iniciar, ativo, conquistar, estatistica, presenca, encerrar, appId };
