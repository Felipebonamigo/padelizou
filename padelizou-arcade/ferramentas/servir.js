#!/usr/bin/env node
/* Servidor estático mínimo, sem dependência: `node ferramentas/servir.js [porta]`.
 * Serve pra testar no celular pela rede local. Abrir o index.html direto do disco também funciona. */
'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const raiz = path.resolve(__dirname, '..');
const porta = Number(process.argv[2] || process.env.PORT || 8080);
const TIPOS = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.md': 'text/markdown; charset=utf-8' };

const servidor = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  let caminho = decodeURIComponent(url.pathname);
  if (caminho.endsWith('/')) caminho += 'index.html';
  const arquivo = path.join(raiz, caminho);
  if (!arquivo.startsWith(raiz + path.sep) && arquivo !== raiz) { res.writeHead(403); res.end('403'); return; }
  fs.readFile(arquivo, (erro, conteudo) => {
    if (erro) { res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end('404: ' + caminho); return; }
    res.writeHead(200, { 'Content-Type': TIPOS[path.extname(arquivo)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(conteudo);
  });
});

servidor.listen(porta, '0.0.0.0', () => {
  console.log(`Padelizou Arcade em http://localhost:${porta}/  (Ctrl+C pra parar)`);
});
