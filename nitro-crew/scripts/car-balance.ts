// Balanceamento dos carros (docs/CARROS.md): o mesmo piloto com cada carro em cada pista.
//   solo  — sozinho, corrida inteira com combustível e box: tempo relativo ao do Falcão GT (1,000 = igual a ele)
//   grade — 20 carros, profissional, piloto-proxy (PROXY_SKILL): posição média de chegada
// Uso: npx tsx scripts/car-balance.ts [solo|grade|ambos=solo] [sementes=1] [carros=todos, vírgulas] [pistas=todas, vírgulas]
// A grade leva ~3 s por corrida numa máquina livre (14 carros × 16 pistas × 2 sementes ≈ 20 min): rode por partes
// (carros) em processos paralelos. docs/CARROS.md traz a tabela medida e o porquê de cada número.
import { CARS } from '../src/core/data/cars';
import { TICK_RATE } from '../src/core/constants';
import { TRACKS } from '../src/core/track';
import { gridPosition, soloRaceTicks, summarizeSolo } from './car-balance-lib';

const mode = process.argv[2] ?? 'solo';
const seedCount = Number(process.argv[3] ?? 1);
const carIds = process.argv[4] && process.argv[4] !== 'todos' ? process.argv[4].split(',') : CARS.map((c) => c.id);
const trackIds = process.argv[5] ? process.argv[5].split(',') : TRACKS.map((t) => t.id);
const seeds = Array.from({ length: seedCount }, (_, i) => i + 1);
const name = (id: string) => CARS.find((c) => c.id === id)?.name ?? id;
const price = (id: string) => CARS.find((c) => c.id === id)?.price ?? 0;

if (mode === 'solo' || mode === 'ambos') {
  const times: Record<string, Record<string, number>> = {};
  const t0 = Date.now();
  for (const c of carIds) {
    times[c] = {};
    for (const t of trackIds) times[c][t] = seeds.reduce((a, s) => a + soloRaceTicks(t, c, s), 0) / seeds.length;
  }
  const rows = summarizeSolo(times).sort((a, b) => a.relTime - b.relTime);
  console.log(`solo: ${trackIds.length} pistas × ${seeds.length} semente(s), ${((Date.now() - t0) / 1000).toFixed(0)} s`);
  console.log('carro            preço   tempo rel.  melhor  pior   1º  último');
  for (const r of rows) {
    console.log(`${name(r.carId).padEnd(15)} ${String(price(r.carId)).padStart(6)}   ${r.relTime.toFixed(4)}     ${r.best.toFixed(3)}   ${r.worst.toFixed(3)}  ${String(r.wins).padStart(2)}  ${String(r.lasts).padStart(2)}`);
  }
  // Tempo médio de corrida em segundos, por carro (para ler a diferença em segundos, não só em %).
  const secs = (c: string) => Object.values(times[c]).reduce((a, b) => a + b, 0) / Object.values(times[c]).length / TICK_RATE;
  console.log(`corrida média: ${rows.map((r) => `${r.carId} ${secs(r.carId).toFixed(1)} s`).join(' · ')}`);
}

if (mode === 'grade' || mode === 'ambos') {
  console.log(`grade: ${trackIds.length} pistas × ${seeds.length} semente(s), piloto-proxy no profissional`);
  for (const c of carIds) {
    const t0 = Date.now();
    const ps: number[] = [];
    for (const s of seeds) for (const t of trackIds) ps.push(gridPosition(t, c, s));
    const avg = ps.reduce((a, b) => a + b, 0) / ps.length;
    const top5 = ps.filter((p) => p <= 5).length;
    console.log(`${name(c).padEnd(15)} ${String(price(c)).padStart(6)}  posição média ${avg.toFixed(2)}  top5 ${top5}/${ps.length}  vitórias ${ps.filter((p) => p === 1).length}  [${ps.join(',')}]  (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
  }
}
