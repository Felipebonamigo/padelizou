// Carrega os módulos do jogo na ordem certa e devolve o namespace `Padel`.
'use strict';
const caminho = require('node:path');
const js = (nome) => require(caminho.join(__dirname, '..', 'js', nome));
js('util.js'); js('quadra.js'); js('regras.js'); js('fisica.js'); js('arbitro.js'); js('jogadores.js'); js('partida.js');
module.exports = globalThis.Padel;
