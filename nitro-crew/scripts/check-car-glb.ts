// Confere um carro em .glb contra a convenção do briefing de arte, com o mesmo validador do jogo
// (src/render/cars/gltf.ts + check.ts): rodas e escapes, pegada, faróis e lanternas, orçamento, pinturas.
// Uso: npm run check-car -- caminho/para/pickup.glb [estilo]   (o estilo sai do nome do arquivo)
import { readFileSync } from 'node:fs';
import { basename } from 'node:path';
import { CAR_BODIES } from '../src/core/data/cars';
import type { CarBody } from '../src/core/types';
import { parseCarGlb } from '../src/render/cars/gltf';

const file = process.argv[2];
if (!file) { console.error('uso: npm run check-car -- <arquivo.glb> [estilo]'); process.exit(2); }
const body = (process.argv[3] ?? basename(file).replace(/\.glb$/i, '')) as CarBody;
if (!CAR_BODIES.includes(body)) { console.error(`"${body}" não é um estilo: ${CAR_BODIES.join(', ')}`); process.exit(2); }
const buf = readFileSync(file);
const { model, problems, warnings } = await parseCarGlb(body, buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength));
for (const w of warnings) console.log(`aviso: ${w}`);
if (model) {
  const bb = model.shell.boundingBox;
  const m = (v: number) => v.toFixed(2);
  console.log(`${basename(file)} (${body}): ${model.triangles} triângulos no casco; ${bb ? `${m(bb.max.z - bb.min.z)} × ${m(bb.max.x - bb.min.x)} × ${m(bb.max.y)} m` : ''}; rodas r ${m(model.axles[0].r)}/${m(model.axles[1].r)} m`);
}
if (problems.length) { for (const p of problems) console.log(`✗ ${p}`); process.exit(1); }
console.log('✓ aceito: o jogo usa este modelo se ele estiver em src/assets/cars/');
