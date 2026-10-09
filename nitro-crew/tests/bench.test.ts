// Banco de prova (src/bench/): métricas puras, plano de cenas e validação do relatório `nitro-crew-bench/1`.
// O que o navegador faz (run.ts) e o que o Electron faz (storage.cjs) são conferidos em outro lugar
// (tools/bench.mjs e tests/desktop-storage.test.ts); aqui só a parte pura.
// Os testes 1–16 são os do cartão K4; os "Ultra" são o acréscimo do dono de 09/10/2026 (docs/CRONOGRAMA.md, "Revisão visual", R1).
import { describe, expect, it } from 'vitest';
import { COUNTDOWN_TICKS, getTrack } from '../src/core';
import {
  BENCH_SCHEMA, benchWarnings, evaluateTargets, summarizeFrames, validateBenchReport, type BenchReport, type BenchRun,
} from '../src/bench/metrics';
import { BENCH_LEVELS, BENCH_QUALITIES, BENCH_SCENES, parseBenchOptions, renderSizeFor } from '../src/bench/options';
import { FLOOR_TARGET_FPS, ULTRA_EFFECTS, ULTRA_TARGET, renderQualityOf, ultraEffectsActive } from '../src/bench/ultra';

const VP = { calls: 30, triangles: 50000, targetW: 640, targetH: 360, targetPx: 230400, samples: 0, passes: 0, postPx: 0 };
function withViewports(r: BenchRun, vps: BenchRun['viewports']): BenchRun {
  const calls = vps.reduce((s, v) => s + v.calls, 0);
  const triangles = vps.reduce((s, v) => s + v.triangles, 0);
  return { ...r, viewports: vps, calls, triangles, maxCalls: calls, maxTriangles: triangles };
}
function run(scene: string, humans: number): BenchRun {
  const base: BenchRun = { scene, track: 'copacabana', humans, quality: 'low', frames: 4, avgFps: 60, low1Fps: 50, avgMs: 16.7, maxMs: 20,
    calls: 0, triangles: 0, maxCalls: 0, maxTriangles: 0, shadows: false, viewports: [] };
  return withViewports(base, Array.from({ length: humans }, () => ({ ...VP })));
}
function report(): BenchReport {
  return { schema: BENCH_SCHEMA, version: '0.1.0', date: '2026-10-09T12:00:00.000Z',
    env: { userAgent: 'teste', gl: 'teste', webgpu: 'sem-api', width: 1280, height: 720, dpr: 1, electron: false, uncapped: false },
    options: { frames: 4, warm: 2, scenes: ['copa-1p', 'copa-4p'], qualities: ['low'] },
    runs: [run('copa-1p', 1), run('copa-4p', 4)] };
}

// ───────────────────────────── métricas de quadro ─────────────────────────────

describe('summarizeFrames', () => {
  it('média e 1% low de uma série conhecida', () => {
    const s = summarizeFrames([...Array(99).fill(1000 / 60), 50]);
    expect(s.frames).toBe(100);
    expect(s.avgMs).toBeCloseTo(17, 6);
    expect(s.avgFps).toBeCloseTo(58.8235, 3);
    expect(s.low1Fps).toBeCloseTo(20, 6);
    expect(s.maxMs).toBe(50);
  });

  it('o 1% low usa floor(n/100) quadros, no mínimo 1', () => {
    const a = summarizeFrames([...Array(245).fill(10), ...Array(5).fill(40)]);
    expect(a.low1Fps).toBeCloseTo(25, 6); // k = 2 quadros de 40 ms
    expect(a.low1Fps).toBeLessThanOrEqual(a.avgFps);
    const b = summarizeFrames([...Array(9).fill(10), 30]);
    expect(b.low1Fps).toBeCloseTo(33.333, 2); // k = 1
    expect(b.low1Fps).toBeLessThanOrEqual(b.avgFps);
  });

  it('série vazia dá zeros, não NaN', () => {
    expect(summarizeFrames([])).toEqual({ frames: 0, avgMs: 0, avgFps: 0, low1Fps: 0, maxMs: 0 });
  });
});

// ───────────────────────────── plano do bench ─────────────────────────────

describe('parseBenchOptions', () => {
  it('sem bench=1 não é modo bench', () => {
    expect(parseBenchOptions('')).toBeNull();
    expect(parseBenchOptions('?track=copacabana')).toBeNull();
    expect(parseBenchOptions('?bench=0')).toBeNull();
  });

  it('bench=1 sozinho usa o plano padrão (as três qualidades e o Ultra)', () => {
    const o = parseBenchOptions('?bench=1');
    expect(o).not.toBeNull();
    if (!o) return;
    expect(o.frames).toBe(600);
    expect(o.warm).toBe(60);
    expect(o.scenes.map((s) => s.id)).toEqual(BENCH_SCENES.map((s) => s.id));
    expect(o.qualities).toEqual([...BENCH_LEVELS]);
    expect(o.uncapped).toBe(false);
    expect(o.quit).toBe(false);
    expect(o.res).toBeNull();
    expect(BENCH_QUALITIES).toEqual(['low', 'medium', 'high']);
  });

  it('frames, warm, cenas e q escolhem o plano, na ordem dada', () => {
    const o = parseBenchOptions('?bench=1&frames=4&warm=2&cenas=sampa-4p,copa-1p&q=high,low');
    expect(o).not.toBeNull();
    if (!o) return;
    expect(o.frames).toBe(4);
    expect(o.warm).toBe(2);
    expect(o.scenes.map((s) => s.id)).toEqual(['sampa-4p', 'copa-1p']);
    expect(o.qualities).toEqual(['high', 'low']);
  });

  it('cena ou qualidade desconhecida é recusada com o nome', () => {
    expect(() => parseBenchOptions('?bench=1&cenas=marte')).toThrow(/cena desconhecida: marte/);
    expect(() => parseBenchOptions('?bench=1&q=maxima')).toThrow(/qualidade desconhecida: maxima/);
  });

  it('frames e warm fora da faixa são recusados', () => {
    for (const bad of ['?bench=1&frames=0', '?bench=1&frames=abc', '?bench=1&frames=20001', '?bench=1&warm=-1']) {
      expect(() => parseBenchOptions(bad), bad).toThrow(/frames|warm/);
    }
  });

  it('uncapped=1 e sair=1 viram booleanos', () => {
    const o = parseBenchOptions('?bench=1&uncapped=1&sair=1');
    expect(o?.uncapped).toBe(true);
    expect(o?.quit).toBe(true);
  });
});

describe('BENCH_SCENES', () => {
  it('ids únicos, pistas que existem, 1 a 4 jogadores, medindo depois da contagem', () => {
    const ids = BENCH_SCENES.map((s) => s.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const s of BENCH_SCENES) {
      expect(getTrack(s.track).def.id, s.id).toBe(s.track);
      expect(s.humans, s.id).toBeGreaterThanOrEqual(1);
      expect(s.humans, s.id).toBeLessThanOrEqual(4);
      expect(s.ticks, s.id).toBeGreaterThanOrEqual(COUNTDOWN_TICKS);
    }
  });

  it('cobre as metas da onda L', () => {
    const copa = BENCH_SCENES.filter((s) => s.track === 'copacabana').map((s) => s.humans).sort();
    expect(copa).toEqual(expect.arrayContaining([1, 2, 4]));
    expect(BENCH_SCENES.some((s) => s.track === 'sampa_noite')).toBe(true);
  });
});

// ───────────────────────────── validação do relatório ─────────────────────────────

describe('validateBenchReport', () => {
  it('relatório bem formado não tem problema', () => {
    expect(validateBenchReport(report())).toEqual([]);
  });

  it('chamadas que não somam os viewports viram problema', () => {
    const r = report();
    r.runs[1] = { ...r.runs[1], calls: 121 };
    expect(validateBenchReport(r)).toEqual(['copa-4p/low: chamadas 121 ≠ soma dos viewports 120']);
  });

  it('número não finito, 1% low acima da média e viewport a menos, um problema cada', () => {
    const a = report();
    a.runs[0] = { ...a.runs[0], avgFps: NaN };
    expect(validateBenchReport(a)).toEqual(['copa-1p/low: avgFps inválido (NaN)']);
    const b = report();
    b.runs[0] = { ...b.runs[0], low1Fps: 70 };
    expect(validateBenchReport(b)).toEqual(['copa-1p/low: 1% low 70 acima da média 60']);
    const c = report();
    c.runs[1] = withViewports(c.runs[1], c.runs[1].viewports.slice(0, 3));
    expect(validateBenchReport(c)).toEqual(['copa-4p/low: 3 viewports para 4 jogadores']);
  });

  it('corrida faltando e esquema errado', () => {
    const a = report();
    a.runs = [a.runs[0]];
    expect(validateBenchReport(a)).toEqual(['falta copa-4p/low']);
    expect(validateBenchReport({ ...report(), schema: 'x' as typeof BENCH_SCHEMA })).toEqual(['schema: esperado nitro-crew-bench/1, veio x']);
  });

  it('run sem viewports vira problema, sem lançar', () => {
    const r = report();
    delete (r.runs[1] as Partial<BenchRun>).viewports;
    expect(() => validateBenchReport(r)).not.toThrow();
    expect(validateBenchReport(r)).toEqual(['copa-4p/low: viewports ausente']);
  });
});

// ───────────────────────────── Ultra (acréscimo do dono, 09/10/2026) ─────────────────────────────
// A L2 implementa os efeitos; aqui só o registro do nível e a régua: o que o Ultra promete, em que resolução se mede e
// se a meta de quadros foi batida. Enquanto os efeitos não existem, o Ultra mede a Alta em 1440p (a folga que sobra).

/** Relatório com a Baixa e o Ultra nas duas cenas; o Ultra grava a resolução em que desenhou. */
function reportWithUltra(): BenchReport {
  const ultra = (scene: string, humans: number): BenchRun => ({ ...run(scene, humans), quality: 'ultra', renderQuality: 'high', renderW: 2560, renderH: 1440, avgFps: 130, low1Fps: 110 });
  return { ...report(),
    options: { frames: 4, warm: 2, scenes: ['copa-1p', 'copa-4p'], qualities: ['low', 'ultra'] },
    runs: [run('copa-1p', 1), run('copa-4p', 4), ultra('copa-1p', 1), ultra('copa-4p', 4)] };
}
const withFps = (r: BenchRun, o: Partial<BenchRun>): BenchRun => ({ ...r, ...o });

describe('Ultra: registro do nível', () => {
  it('BENCH_LEVELS é as três qualidades e depois o ultra; BENCH_QUALITIES não muda', () => {
    expect(BENCH_QUALITIES).toEqual(['low', 'medium', 'high']);
    expect(BENCH_LEVELS).toEqual(['low', 'medium', 'high', 'ultra']);
  });

  it('a meta é a 4070 Ti a 1440p com 120 fps; o piso continua 60', () => {
    expect(ULTRA_TARGET).toEqual({ fps: 120, width: 2560, height: 1440, gpu: 'NVIDIA GeForce RTX 4070 Ti' });
    expect(FLOOR_TARGET_FPS).toBe(60);
  });

  it('os 7 efeitos previstos, ids únicos, cada um com parâmetros, e nenhum implementado ainda (é da L)', () => {
    expect(ULTRA_EFFECTS.map((e) => e.id)).toEqual(['gtao', 'ssr', 'volumetric-rays', 'dof', 'motion-blur', 'csm', 'taa']);
    for (const e of ULTRA_EFFECTS) expect(Object.keys(e.params).length, e.id).toBeGreaterThan(0);
    const csm = ULTRA_EFFECTS.find((e) => e.id === 'csm');
    expect(csm?.params).toMatchObject({ cascades: 4, mapSize: 4096 });
    expect(ultraEffectsActive()).toEqual([]);
  });

  it('o Ultra desenha na qualidade Alta do renderizador; o resto é o próprio nível', () => {
    expect(renderQualityOf('ultra')).toBe('high');
    for (const q of BENCH_QUALITIES) expect(renderQualityOf(q)).toBe(q);
  });
});

describe('Ultra: plano e resolução', () => {
  it('q=ultra é aceito, e res=LxA força o tamanho de desenho', () => {
    const o = parseBenchOptions('?bench=1&q=ultra&res=1920x1080');
    expect(o?.qualities).toEqual(['ultra']);
    expect(o?.res).toEqual({ w: 1920, h: 1080 });
  });

  it('res fora do formato ou da faixa é recusado com o texto', () => {
    for (const bad of ['1920', 'axb', '100x100', '9000x9000', '1920x', '1920X1080']) {
      expect(() => parseBenchOptions(`?bench=1&res=${bad}`), bad).toThrow(/res inválido/);
    }
  });

  it('renderSizeFor: o Ultra sem res desenha em 2560×1440, res manda, e os outros níveis seguem a janela', () => {
    expect(renderSizeFor('ultra', null)).toEqual({ w: 2560, h: 1440 });
    expect(renderSizeFor('ultra', { w: 1920, h: 1080 })).toEqual({ w: 1920, h: 1080 });
    expect(renderSizeFor('high', null)).toBeNull();
    expect(renderSizeFor('low', { w: 1920, h: 1080 })).toEqual({ w: 1920, h: 1080 });
  });
});

describe('Ultra: a régua das metas', () => {
  it('Baixa e Média miram 60 fps; a Alta não tem meta; 4 jogadores na Média ficam sem meta', () => {
    const base = run('copa-1p', 1);
    const rows = evaluateTargets([
      withFps(base, { quality: 'low', avgFps: 60 }),
      withFps(base, { quality: 'low', avgFps: 59 }),
      withFps(base, { quality: 'medium', avgFps: 75 }),
      withFps(run('copa-4p', 4), { quality: 'medium', avgFps: 20 }),
      withFps(base, { quality: 'high', avgFps: 20 }),
    ]);
    expect(rows.map((r) => r.targetFps)).toEqual([60, 60, 60, null, null]);
    expect(rows.map((r) => r.ok)).toEqual([true, false, true, null, null]);
  });

  it('o Ultra exige 120 fps a 1440p; abaixo dessa resolução, ou sem dizê-la, não conta', () => {
    const ultra = (o: Partial<BenchRun>): BenchRun => ({ ...run('copa-1p', 1), quality: 'ultra', renderQuality: 'high', renderW: 2560, renderH: 1440, ...o });
    const rows = evaluateTargets([
      ultra({ avgFps: 120 }),
      ultra({ avgFps: 119.9 }),
      ultra({ avgFps: 240, renderW: 1920, renderH: 1080 }),
      ultra({ avgFps: 240, renderW: undefined, renderH: undefined }),
      ultra({ avgFps: 125, renderW: 3840, renderH: 2160 }),
    ]);
    expect(rows.map((r) => r.targetFps)).toEqual([120, 120, 120, 120, 120]);
    expect(rows.map((r) => r.ok)).toEqual([true, false, null, null, true]);
    expect(rows[2].note).toMatch(/1440p/);
  });
});

describe('Ultra: avisos do relatório', () => {
  it('todas as corridas coladas no mesmo teto de quadros avisam que o limite não foi desligado', () => {
    const r = report();
    r.runs = [withFps(r.runs[0], { avgFps: 59.8 }), withFps(r.runs[1], { avgFps: 60.1 })];
    expect(benchWarnings(r).join('\n')).toMatch(/teto de quadros em 60 fps/);
    r.runs = [withFps(r.runs[0], { avgFps: 59.8 }), withFps(r.runs[1], { avgFps: 31 })];
    expect(benchWarnings(r).join('\n')).not.toMatch(/teto de quadros/);
    expect(benchWarnings({ ...r, runs: [withFps(r.runs[0], { avgFps: 60 })] }).join('\n')).not.toMatch(/teto de quadros/);
  });

  it('o Ultra sem os efeitos avisa que o número é o da Alta em 1440p, e só quando há corrida Ultra', () => {
    const r = reportWithUltra();
    r.ultra = { target: ULTRA_TARGET, effectsPlanned: ULTRA_EFFECTS.map((e) => e.id), effectsActive: [] };
    expect(benchWarnings(r).join('\n')).toMatch(/ultra: 0 de 7 efeitos/);
    const semUltra = { ...report(), ultra: r.ultra };
    expect(benchWarnings(semUltra).join('\n')).not.toMatch(/ultra:/);
  });

  it('corrida Ultra precisa dizer a resolução em que desenhou', () => {
    expect(validateBenchReport(reportWithUltra())).toEqual([]);
    const r = reportWithUltra();
    r.runs[2] = withFps(r.runs[2], { renderH: undefined });
    expect(validateBenchReport(r)).toEqual(['copa-1p/ultra: renderH inválido (undefined)']);
  });
});
