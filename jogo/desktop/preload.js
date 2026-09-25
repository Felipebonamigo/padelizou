// PUNHOS DE SHAOLIN — a ponte entre a página e o Electron. É a ÚNICA superfície que o jogo
// enxerga do sistema (`window.punhos`); nada de `require` vaza pra página.
//
// Roda com `sandbox: true`: aqui só existe o `require('electron')` reduzido (contextBridge,
// ipcRenderer) — nada de fs, path ou módulo do projeto. Cada canal abaixo tem um handler no
// `main.js` que confere remetente e argumentos; a ponte converte o tipo, o main decide.
'use strict';
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('punhos', {
    desktop: true,
    steam: ipcRenderer.sendSync('steam:ativo'),
    carregar: () => ipcRenderer.sendSync('progresso:carregar'),
    salvar: texto => ipcRenderer.send('progresso:salvar', String(texto)),
    conquistar: id => ipcRenderer.send('steam:conquistar', String(id)),
    estatistica: (nome, valor) => ipcRenderer.send('steam:estatistica', String(nome), Number(valor) || 0),
    presenca: texto => ipcRenderer.send('steam:presenca', String(texto)),
    sair: () => ipcRenderer.send('app:sair'),
    telaCheia: ligar => ipcRenderer.send('app:telaCheia', ligar),
});
