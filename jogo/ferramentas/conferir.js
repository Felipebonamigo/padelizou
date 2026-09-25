// Roda todos os conferidores do jogo — os `conferir-*shaolin*.js` de Padelizou.Tests/js — e sai
// com 1 se algum cair. É o `npm run conferir` de jogo/package.json.
//
// Varre a pasta em vez de listar à mão: a lista escrita no package.json já tinha esquecido dois dos
// cinco (mesma lição do glob do CI no CLAUDE.md). Em Node puro, e não em `for` de shell, porque o
// npm no Windows roda os scripts no cmd.exe, que não conhece esse laço.
'use strict';
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const pasta = path.join(__dirname, '..', '..', 'Padelizou.Tests', 'js');
const conferidores = fs.readdirSync(pasta).filter(n => /^conferir-.*shaolin.*\.js$/.test(n)).sort();
if (conferidores.length === 0) { console.error(`nenhum conferidor do jogo em ${pasta}`); process.exit(1); }

const caidos = [];
for (const nome of conferidores) {
    const r = spawnSync(process.execPath, [path.join(pasta, nome)], { encoding: 'utf8' });
    const ok = r.status === 0;
    console.log(`${ok ? 'verde' : 'VERMELHO'} · ${nome}`);
    if (!ok) { caidos.push(nome); process.stdout.write(r.stdout || ''); process.stderr.write(r.stderr || ''); }
}
console.log(caidos.length ? `\n${caidos.length} de ${conferidores.length} caíram: ${caidos.join(', ')}` : `\n${conferidores.length} conferidores do jogo, todos verdes`);
process.exit(caidos.length ? 1 : 0);
