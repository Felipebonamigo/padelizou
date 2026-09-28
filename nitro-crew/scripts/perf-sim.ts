// Custo de CPU da simulação: tempo de cada stepRace em corridas inteiras de 20 carros, com 1 a 4 humanos,
// com e sem assistências, personalidades e modos de festa. O que importa é o pior tick (p99 e máximo) e o
// pior quadro (4 ticks seguidos: o laço fixo de 60 Hz roda até 4 ticks num quadro atrasado), não a média.
// Só o stepRace entra no cronômetro; as entradas de roteiro (scripts/sim-scenarios.ts) ficam de fora.
//
// Duas leituras de cada tick, porque numa máquina ocupada o relógio de parede mente:
//   bruto      — o tempo medido na primeira repetição (com coleta de lixo e o SO tirando a CPU do processo);
//   intrínseco — a mesma corrida repetida (é determinística: os mesmos ticks), e o MENOR tempo de cada tick
//                entre as repetições. Filtra a preempção e o GC e sobra o custo do próprio tick; o p99 e o
//                máximo dela são os ticks realmente caros (curva com 20 carros juntos, largada).
// As pausas de GC (PerformanceObserver 'gc') saem separadas, com contagem e a maior.
//
// Uso: npx tsx scripts/perf-sim.ts [--quick] [--reps N] [--json] [--fingerprints]
//   --quick         uma pista por linha em vez de três
//   --reps N        repetições por corrida para a leitura intrínseca (padrão 3)
//   --json          saída em JSON (para comparar antes/depois)
//   --fingerprints  só imprime a impressão digital das corridas de referência (tests/sim-golden.test.ts)
// Perfil de CPU: node --cpu-prof --cpu-prof-dir=scratch/prof --import tsx scripts/perf-sim.ts --quick
import { PerformanceObserver } from 'node:perf_hooks';
import { stepRace } from '../src/core/sim/race';
import {
  ALL_ASSISTS, fingerprintScenario, GOLDEN_SCENARIOS, SCENARIO_MAX_TICKS, scenarioInputs, startScenario, type SimScenario,
} from './sim-scenarios';
import type { AssistLevel, CoreMode } from '../src/core/types';

const argv = process.argv.slice(2);
const args = new Set(argv);
const repsArg = argv.indexOf('--reps');
const REPS = Math.max(1, repsArg >= 0 ? Number(argv[repsArg + 1]) || 3 : 3);

if (args.has('--fingerprints')) {
  for (const s of GOLDEN_SCENARIOS) {
    const r = fingerprintScenario(s);
    console.log(`'${s.name}': { fingerprint: '${r.fingerprint}', ticks: ${r.ticks} },${r.finished ? '' : '  // NÃO TERMINOU'}`);
  }
  process.exit(0);
}

/** Variações medidas: do núcleo mínimo ao pior caso (tudo ligado). */
interface Variant {
  name: string;
  make: (humans: number, trackId: string, seed: number) => SimScenario | null;
}

const levels = (n: number, level: AssistLevel): AssistLevel[] => Array.from({ length: n }, () => level);
const mixed: AssistLevel[] = ['full', 'steer', 'brake', 'none'];

const VARIANTS: Variant[] = [
  { name: 'básico', make: (humans, trackId, seed) => ({ name: 'básico', trackId, humans, seed, noPersonalities: true }) },
  { name: 'personalidades', make: (humans, trackId, seed) => ({ name: 'personalidades', trackId, humans, seed }) },
  {
    name: 'assistências', make: (humans, trackId, seed) => ({
      name: 'assistências', trackId, humans, seed, assists: ALL_ASSISTS, assistLevels: mixed.slice(0, humans),
    }),
  },
  {
    name: 'assist. completa', make: (humans, trackId, seed) => ({
      name: 'assist. completa', trackId, humans, seed, assists: ALL_ASSISTS, assistLevels: levels(humans, 'full'),
    }),
  },
  {
    name: 'modo de festa', make: (humans, trackId, seed) => {
      // Escolta com 1 e 3 humanos; revezamento com 2 e 4 (o lobby exige par).
      const mode: CoreMode = humans % 2 === 0 ? 'relay' : 'escort';
      return { name: `modo ${mode}`, trackId, humans, seed, mode, assists: ALL_ASSISTS, assistLevels: mixed.slice(0, humans) };
    },
  },
];

const TRACKS = args.has('--quick') ? ['copacabana'] : ['copacabana', 'sampa_noite', 'daintree'];

interface Stats {
  ticks: number; mean: number; p50: number; p99: number; p999: number; max: number; frame4: number;
}

interface Row {
  variant: string; humans: number;
  raw: Stats; intrinsic: Stats;
  gc: { count: number; totalMs: number; maxMs: number };
}

function percentile(sorted: Float64Array, p: number): number {
  return sorted[Math.min(sorted.length - 1, Math.floor(p * sorted.length))];
}

/** Pausas de GC observadas desde o último `takeGc()`. */
const gcPauses: number[] = [];
new PerformanceObserver((list) => { for (const e of list.getEntries()) gcPauses.push(e.duration); }).observe({ entryTypes: ['gc'] });
/** As entradas de GC chegam de forma assíncrona: dá uma volta no laço de eventos antes de recolher. */
async function takeGc(): Promise<number[]> {
  await new Promise((resolve) => setImmediate(resolve));
  return gcPauses.splice(0);
}

/** Uma corrida inteira; devolve o tempo (ms) de cada tick. */
function timeRace(s: SimScenario): Float64Array {
  const { state, track } = startScenario(s);
  const times: number[] = [];
  while (state.phase !== 'finished' && times.length < SCENARIO_MAX_TICKS) {
    const inputs = scenarioInputs(state, track, s);
    const t0 = performance.now();
    stepRace(state, track, inputs);
    times.push(performance.now() - t0);
  }
  return Float64Array.from(times);
}

/** A mesma corrida `REPS` vezes: a primeira é a leitura bruta; o mínimo por tick, a intrínseca. */
function timeRaceReps(s: SimScenario): { raw: Float64Array; intrinsic: Float64Array } {
  const raw = timeRace(s);
  const intrinsic = raw.slice();
  for (let r = 1; r < REPS; r++) {
    const t = timeRace(s);
    if (t.length !== raw.length) throw new Error(`${s.name}: repetição com ${t.length} ticks, esperado ${raw.length} (não determinística?)`);
    for (let i = 0; i < t.length; i++) if (t[i] < intrinsic[i]) intrinsic[i] = t[i];
  }
  return { raw, intrinsic };
}

function summarize(runs: Float64Array[]): Stats {
  let frame4 = 0; let total = 0; let n = 0;
  for (const t of runs) {
    for (let i = 0; i < t.length; i++) {
      total += t[i]; n++;
      if (i >= 3) frame4 = Math.max(frame4, t[i] + t[i - 1] + t[i - 2] + t[i - 3]);
    }
  }
  const all = new Float64Array(n);
  let k = 0;
  for (const t of runs) { all.set(t, k); k += t.length; }
  all.sort();
  return { ticks: n, mean: total / n, p50: percentile(all, 0.5), p99: percentile(all, 0.99), p999: percentile(all, 0.999), max: all[n - 1], frame4 };
}

// Aquecimento: o JIT compila o stepRace antes de medir (a primeira corrida sai sempre mais lenta).
for (const v of VARIANTS) { const s = v.make(2, 'copacabana', 1); if (s) timeRace({ ...s, laps: 1 }); }
await takeGc();

const rows: Row[] = [];
for (const v of VARIANTS) {
  for (let humans = 1; humans <= 4; humans++) {
    const raw: Float64Array[] = []; const intrinsic: Float64Array[] = [];
    let gc: number[] = [];
    for (const [i, trackId] of TRACKS.entries()) {
      const s = v.make(humans, trackId, 1000 + i * 17 + humans);
      if (!s) continue;
      await takeGc();
      const r = timeRaceReps(s);
      gc = gc.concat(await takeGc());
      raw.push(r.raw); intrinsic.push(r.intrinsic);
    }
    rows.push({
      variant: v.name, humans, raw: summarize(raw), intrinsic: summarize(intrinsic),
      gc: { count: gc.length, totalMs: gc.reduce((a, b) => a + b, 0), maxMs: gc.reduce((a, b) => Math.max(a, b), 0) },
    });
  }
}

if (args.has('--json')) {
  console.log(JSON.stringify({ node: process.version, tracks: TRACKS, reps: REPS, rows }, null, 2));
} else {
  const us = (ms: number, w = 6) => (ms * 1000).toFixed(0).padStart(w);
  console.log(`stepRace, 20 carros, corridas inteiras de 2 voltas em ${TRACKS.join(', ')}; ${REPS} repetições — µs por tick (node ${process.version})`);
  console.log('                        |------------ intrínseco (mín. das repetições) -----------|  |---- bruto (1ª) ----|  |--- GC (todas) ---|');
  console.log('variação            hum   ticks  média    p50    p99  p99.9    máx  4 ticks     p99  p99.9    máx   pausas   maior');
  for (const r of rows) {
    const i = r.intrinsic; const b = r.raw;
    console.log(`${r.variant.padEnd(19)} ${String(r.humans).padStart(3)} ${String(i.ticks).padStart(7)} ${us(i.mean, 5)} ${us(i.p50)} ${us(i.p99)} ${us(i.p999)} ${us(i.max)} ${us(i.frame4, 8)} ${us(b.p99, 7)} ${us(b.p999)} ${us(b.max)} ${String(r.gc.count).padStart(8)} ${us(r.gc.maxMs, 7)}`);
  }
  const worst = rows.reduce((a, b) => (b.intrinsic.p99 > a.intrinsic.p99 ? b : a));
  const worstFrame = rows.reduce((a, b) => (b.intrinsic.frame4 > a.intrinsic.frame4 ? b : a));
  console.log(`\nPior p99 intrínseco: ${worst.variant}, ${worst.humans} humano(s) — ${(worst.intrinsic.p99 * 1000).toFixed(0)} µs por tick.`);
  console.log(`Pior quadro intrínseco (4 ticks seguidos): ${worstFrame.variant}, ${worstFrame.humans} humano(s) — ${(worstFrame.intrinsic.frame4 * 1000).toFixed(0)} µs de 16 667 µs a 60 Hz.`);
}
