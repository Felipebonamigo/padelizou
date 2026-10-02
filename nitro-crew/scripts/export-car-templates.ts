// Gera os modelos-base dos carros para o artista: art/templates/cars/<estilo>.glb, um por estilo de
// carroceria — o carro do jogo na convenção do briefing de arte (frente +Z, metros, nós wheel_* e
// exhaust_*, materiais pelo nome). Abrir no Blender, redesenhar, exportar como .glb e conferir com
// `npm run check-car -- <arquivo>`. Uso: npm run car-templates
import { mkdirSync, writeFileSync } from 'node:fs';
import { CAR_BODIES } from '../src/core/data/cars';
import { carTemplateScene, exportGlb } from '../src/render/cars/template';
import { MODEL_BUILDERS } from '../src/render/cars/models';
import { installNodeFileReader } from './lib/node-filereader';

installNodeFileReader();
const dir = 'art/templates/cars';
mkdirSync(dir, { recursive: true });
for (const body of CAR_BODIES) {
  const glb = await exportGlb(carTemplateScene(body));
  writeFileSync(`${dir}/${body}.glb`, Buffer.from(glb));
  console.log(`${dir}/${body}.glb  ${(glb.byteLength / 1024).toFixed(0)} KB  ${MODEL_BUILDERS[body]().triangles} triângulos no casco`);
}
