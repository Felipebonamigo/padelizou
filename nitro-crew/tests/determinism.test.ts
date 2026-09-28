import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { serializeRace, hashRace } from '../src/core/serialize';
import { quickRace, run, ALL_ASSISTS, human } from './helpers';

function walk(dir: string, out: string[] = []): string[] {
  for (const f of fs.readdirSync(dir)) { const p = path.join(dir, f); if (fs.statSync(p).isDirectory()) walk(p, out); else if (p.endsWith('.ts')) out.push(p); }
  return out;
}

/** O que varia entre máquinas (ou entre execuções) e não pode entrar no núcleo. */
const forbidden = /Math\.(random|a?sinh?|a?cosh?|a?tanh?|atan2|pow|exp|expm1|log|log2|log10|log1p|hypot|cbrt)\b|\*\*|Date\.now|new Date\b|performance\.now|crypto\.|Map\(|Set\(/;

describe('determinismo', () => {
  it('a varredura pega as formas menos óbvias (potência, logaritmos e datas)', () => {
    for (const code of ['const y = x ** 2;', 'Math.log2(x)', 'Math.log10(x)', 'Math.log1p(x)', 'Math.expm1(x)', 'Math.asinh(x)', 'Math.atanh(x)', 'new Date()', 'crypto.getRandomValues(buf)']) {
      expect(code.match(forbidden), code).not.toBeNull();
    }
    for (const code of ['Math.floor(x)', 'Math.sqrt(x)', 'Math.imul(a, b)', 'a * b', 'Math.abs(x)']) expect(code.match(forbidden), code).toBeNull();
  });

  it('o núcleo não usa funções que variam entre máquinas', () => {
    for (const f of walk('src/core')) {
      const src = fs.readFileSync(f, 'utf8').replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
      const m = src.match(forbidden);
      // `Map`/`Set` são aceitos só no cache de pistas (index.ts), que não faz parte do estado.
      if (m && (m[0] === 'Map(' || m[0] === 'Set(') && (f.endsWith('track/index.ts') || f.endsWith('championship.ts'))) continue;
      expect(m, `${f} usa ${m?.[0]}`).toBeNull();
    }
  });

  it('duas corridas com a mesma semente são idênticas após 4000 ticks, com 4 humanos e IA', () => {
    const humans = [human(0), human(1, 0, 'trovao'), human(2, 0, 'tornado'), human(3, 0, 'camelo')];
    const steer = (tick: number) => ((tick % 200) < 100 ? 0.4 : -0.4);
    const input = (s: { tick: number }, seat: number) => ({ steer: steer(s.tick + seat * 37), throttle: true, brake: s.tick % 500 > 480, nitro: s.tick % 900 === seat * 10, gearUp: false, gearDown: false });
    const a = quickRace({ humans, totalCars: 20, assists: ALL_ASSISTS, seed: 7 });
    const b = quickRace({ humans, totalCars: 20, assists: ALL_ASSISTS, seed: 7 });
    run(a.state, a.track, 4000, input);
    run(b.state, b.track, 4000, input);
    expect(serializeRace(a.state)).toBe(serializeRace(b.state));
    expect(hashRace(a.state)).toBe(hashRace(b.state));
  }, 30_000);

  it('sementes diferentes produzem corridas diferentes', () => {
    const a = quickRace({ seed: 1 }); const b = quickRace({ seed: 2 });
    run(a.state, a.track, 1500); run(b.state, b.track, 1500);
    expect(hashRace(a.state)).not.toBe(hashRace(b.state));
  });

  it('o estado sobrevive a serializar e desserializar e continua igual', async () => {
    const { deserializeRace } = await import('../src/core/serialize');
    const a = quickRace({ seed: 3 });
    run(a.state, a.track, 800);
    const copy = deserializeRace(serializeRace(a.state));
    run(a.state, a.track, 800); run(copy, a.track, 800);
    expect(serializeRace(copy)).toBe(serializeRace(a.state));
  });
});
