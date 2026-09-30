// Os carros como dado (docs/CARROS.md): catorze, pelo menos um por estilo de carroceria, atributos na faixa,
// preços com sentido, textos em PT e EN que cabem na ficha, silhueta própria por estilo e nenhum carro
// melhor em tudo. A trava de desempenho (tempos com o mesmo piloto) fica no fim: nenhum carro novo é inútil
// nem domina.
import { beforeAll, describe, expect, it } from 'vitest';
import { soloRaceTicks } from '../scripts/car-balance-lib';
import { REFERENCE_SPEED } from '../src/core/constants';
import { CAR_BODIES, CARS, carDef } from '../src/core/data/cars';
import { TRACKS } from '../src/core/track';
import type { CarDef } from '../src/core/types';
import { setLanguage, t } from '../src/i18n';
import '../src/i18n/core';
import '../src/career/strings';
import { carSilhouetteMarkup } from '../src/ui/screens/icons';

/** Consumo de referência de data/cars.ts (um tanque ≈ 2,4 voltas de 400.000 unidades). */
const BASE_FUEL = 1 / (2.4 * 400_000);
const ORIGINALS = ['falcao', 'trovao', 'tornado', 'camelo', 'sucuri', 'carcara', 'pororoca', 'boitata'];
const NEW_CARS = CARS.filter((c) => !ORIGINALS.includes(c.id));

describe('carros: dados', () => {
  it('são 14, com id e nome únicos, e todo estilo de carroceria tem pelo menos um carro', () => {
    expect(CARS.length).toBe(14);
    expect(new Set(CARS.map((c) => c.id)).size).toBe(CARS.length);
    expect(new Set(CARS.map((c) => c.name)).size).toBe(CARS.length);
    for (const b of CAR_BODIES) expect(CARS.filter((c) => c.body === b).length, `estilo ${b} sem carro`).toBeGreaterThan(0);
    for (const c of CARS) expect(c.id, 'id minúsculo, sem acento (vai no save e no online)').toMatch(/^[a-z]+$/);
    expect(NEW_CARS.map((c) => c.body).sort()).toEqual(['classic', 'micro', 'pickup', 'prototype', 'roadster', 'wedge']);
  });

  it('atributos na faixa do jogo; cor e cor de destaque em hex', () => {
    for (const c of CARS) {
      expect(c.topSpeed / REFERENCE_SPEED, c.id).toBeGreaterThanOrEqual(0.88);
      expect(c.topSpeed / REFERENCE_SPEED, c.id).toBeLessThanOrEqual(1.12);
      expect(c.accel, c.id).toBeGreaterThanOrEqual(550); expect(c.accel, c.id).toBeLessThanOrEqual(950);
      expect(c.brake, c.id).toBeGreaterThanOrEqual(2000); expect(c.brake, c.id).toBeLessThanOrEqual(3400);
      expect(c.handling, c.id).toBeGreaterThanOrEqual(0.55); expect(c.handling, c.id).toBeLessThanOrEqual(1);
      expect(c.fuelPerUnit / BASE_FUEL, c.id).toBeGreaterThanOrEqual(0.45); expect(c.fuelPerUnit / BASE_FUEL, c.id).toBeLessThanOrEqual(1.4 + 1e-9);
      expect(c.color, c.id).toMatch(/^#[0-9a-f]{6}$/i);
      if (c.accent !== undefined) expect(c.accent, c.id).toMatch(/^#[0-9a-f]{6}$/i);
    }
    // Os carros novos trazem a segunda cor escolhida (faixa, teto, aerofólio).
    for (const c of NEW_CARS) expect(c.accent, c.id).toBeDefined();
  });

  it('preços: 7 livres e 7 à venda (de $ 10 mil a $ 50 mil, em milhares); livres primeiro, depois do mais barato ao mais caro', () => {
    const free = CARS.filter((c) => c.price === 0);
    const paid = CARS.filter((c) => c.price > 0);
    expect(free.map((c) => c.id)).toEqual(['falcao', 'trovao', 'tornado', 'camelo', 'saci', 'tatu', 'boto']);
    expect(paid.length).toBe(7);
    for (const c of paid) {
      expect(c.price % 1000, c.id).toBe(0);
      expect(c.price, c.id).toBeGreaterThanOrEqual(10_000);
      expect(c.price, c.id).toBeLessThanOrEqual(50_000);
    }
    // A lista é a ordem do lobby e da garagem (←→): livres, depois a vitrine em preço crescente.
    expect(CARS.slice(0, free.length)).toEqual(free);
    expect(paid.map((c) => c.price)).toEqual(paid.map((c) => c.price).slice().sort((a, b) => a - b));
    expect(new Set(paid.map((c) => c.price)).size).toBe(paid.length);
  });

  it('nenhum carro é melhor (ou igual) em tudo a outro de preço igual ou maior', () => {
    const keys: Array<(c: CarDef) => number> = [(c) => c.topSpeed, (c) => c.accel, (c) => c.brake, (c) => c.handling, (c) => -c.fuelPerUnit, (c) => -c.price];
    const bad: string[] = [];
    for (const a of CARS) for (const b of CARS) {
      if (a === b) continue;
      if (keys.every((k) => k(a) >= k(b))) bad.push(`${a.id} ≥ ${b.id} em tudo`);
    }
    expect(bad).toEqual([]);
  });
});

describe('carros: textos', () => {
  it('cada carro tem apresentação em PT (a mesma do CarDef) e em EN, curta o bastante para a ficha do lobby', () => {
    for (const c of CARS) {
      const key = `core.car.${c.id}.blurb`;
      setLanguage('pt');
      const pt = t(key);
      setLanguage('en');
      const en = t(key);
      setLanguage('pt');
      expect(pt, c.id).toBe(c.blurb);
      expect(en, c.id).not.toBe(key);
      expect(en, c.id).not.toBe(pt);
      // A ficha de 4 jogadores em 720p com texto grande: o mais longo dos originais tem 72 (PT) e 85 (EN).
      expect(pt.length, `${c.id}: "${pt}"`).toBeLessThanOrEqual(80);
      expect(en.length, `${c.id}: "${en}"`).toBeLessThanOrEqual(88);
    }
  });

  it('nenhum carro leva nome de marca ou de modelo de verdade, nem a tradução dele', () => {
    // Marcas e modelos (e traduções que entregam o modelo: Furacão = Huracán, Jacaré = Cayman, Arraia = Stingray).
    const real = [
      'ferrari', 'lamborghini', 'porsche', 'bugatti', 'mclaren', 'maserati', 'bmw', 'audi', 'mercedes', 'ford', 'chevrolet',
      'dodge', 'nissan', 'toyota', 'honda', 'subaru', 'mazda', 'volkswagen', 'fiat', 'aston', 'jaguar', 'lotus', 'pagani',
      'koenigsegg', 'huracan', 'furacao', 'aventador', 'diablo', 'diabo', 'murcielago', 'countach', 'gallardo', 'urus',
      'veyron', 'chiron', 'testarossa', 'enzo', 'mustang', 'camaro', 'corvette', 'viper', 'skyline', 'supra', 'miura',
      'cayman', 'jacare', 'stingray', 'arraia', 'cobra', 'puma', 'corcel', 'toro', 'hilux', 'ranger', 'amarok', 'saveiro',
      'montana', 'impala', 'esprit', 'delorean', 'gurgel', 'troller', 'marrua', 'bandeirante', 'isetta', 'beetle', 'fusca',
      'mini', 'smart', 'miata', 'spider', 'spyder', 'lemans', 'jeep',
    ];
    const words = (name: string) => name.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().split(/[^a-z0-9]+/);
    for (const c of NEW_CARS) expect(words(c.name).filter((w) => real.includes(w)), `${c.name} remete a carro de verdade`).toEqual([]);
  });
});

describe('carros: balanceamento (o mesmo piloto, sozinho, a corrida inteira em cada pista)', () => {
  // scripts/car-balance.ts imprime a tabela; aqui ficam as regras. ~450 corridas curtas (um carro só).
  const tracks = TRACKS.map((t) => t.id);
  let times: Record<string, Record<string, number>> = {};
  beforeAll(() => {
    times = Object.fromEntries(CARS.map((c) => [c.id, Object.fromEntries(tracks.map((t) => [t, soloRaceTicks(t, c.id)]))]));
  }, 600_000);
  const faster = (a: string, b: string) => tracks.filter((t) => times[a][t] < times[b][t]);
  const price = (id: string) => carDef(id).price;
  /** Originais que já perdiam em todas as pistas para outro original de preço igual ou menor (o Camelo X para o Falcão GT). */
  const alreadyBeaten = () => CARS.filter((c) => ORIGINALS.includes(c.id) && CARS.some((o) => o !== c && ORIGINALS.includes(o.id) && o.price <= c.price && faster(o.id, c.id).length === tracks.length)).map((c) => c.id);

  it('nenhum carro novo é inútil: ganha, em alguma pista, de cada carro de preço igual ou menor', () => {
    const bad: string[] = [];
    for (const n of NEW_CARS) for (const o of CARS) {
      if (o === n || o.price > n.price) continue;
      if (faster(n.id, o.id).length === 0) bad.push(`${n.id} perde do ${o.id} em todas`);
    }
    expect(bad).toEqual([]);
  });

  it('nenhum carro novo domina: perde, em alguma pista, para cada carro de preço igual ou maior (fora os que já perdiam para um original)', () => {
    const skip = alreadyBeaten();
    expect(skip, 'a exceção documentada em docs/CARROS.md').toEqual(['camelo']);
    const bad: string[] = [];
    for (const n of NEW_CARS) for (const o of CARS) {
      if (o === n || o.price < n.price || skip.includes(o.id)) continue;
      if (faster(o.id, n.id).length === 0) bad.push(`${n.id} ganha do ${o.id} em todas`);
    }
    expect(bad).toEqual([]);
  });

  it('os livres novos andam na faixa dos livres originais (a IA e o lobby ganham carros do mesmo nível)', () => {
    const rel = (id: string) => tracks.reduce((a, t) => a + times[id][t] / times.falcao[t], 0) / tracks.length;
    const originals = CARS.filter((c) => c.price === 0 && ORIGINALS.includes(c.id)).map((c) => rel(c.id));
    for (const c of NEW_CARS.filter((x) => x.price === 0)) {
      expect(rel(c.id), c.id).toBeGreaterThanOrEqual(Math.min(...originals));
      expect(rel(c.id), c.id).toBeLessThanOrEqual(Math.max(...originals));
    }
  });

  it('o Beija-Flor (o mais caro) divide as pistas com o Boitatá GT: ganha nas de curva, perde nas de reta', () => {
    expect(price('beijaflor')).toBe(Math.max(...CARS.map((c) => c.price)));
    const wins = faster('beijaflor', 'boitata');
    expect(wins.length).toBeGreaterThan(tracks.length / 4);
    expect(wins.length).toBeLessThan(tracks.length);
    // As retas longas da Rota 66 são do Boitatá; as curvas de Mônaco, do Beija-Flor.
    expect(wins).not.toContain('rota_66');
    expect(wins).toContain('monaco_noite');
  });

  it('a Iara Turbo não tira o sentido do Pororoca V10 ($ 2 mil mais barato): ele segue mais rápido que ela em alguma reta', () => {
    // Com 330 km/h e aceleração 720 ela ganhava dele em todas as 32 (docs/CARROS.md).
    expect(faster('pororoca', 'iara').length).toBeGreaterThan(0);
    expect(faster('iara', 'pororoca').length).toBeGreaterThan(tracks.length / 2);
  });
});

describe('carros: silhueta 2D por estilo', () => {
  const strip = (m: string) => m.replace(/carShine\d+/g, 'carShine');

  it('cada um dos 13 estilos tem o seu desenho, diferente de todos os outros', () => {
    const marks = CAR_BODIES.map((body) => strip(carSilhouetteMarkup({ body, color: '#123456', accent: '#abcdef' })));
    expect(new Set(marks).size).toBe(CAR_BODIES.length);
    for (const [i, m] of marks.entries()) {
      expect(m, CAR_BODIES[i]).toContain(`data-body="${CAR_BODIES[i]}"`);
      expect(m, CAR_BODIES[i]).toContain('#123456');
      expect(m, CAR_BODIES[i]).toContain('#abcdef');
    }
  });

  it('usa a cor e a cor de destaque do carro; sem destaque, um tom da própria cor', () => {
    for (const c of CARS) {
      const m = carSilhouetteMarkup(c);
      expect(m, c.id).toContain(c.color);
      if (c.accent) expect(m, c.id).toContain(c.accent);
    }
    expect(carSilhouetteMarkup({ body: 'gt', color: '#ff0000' })).toContain('#9e0000');
  });

  it('cada desenho tem um gradiente com id próprio (vários carros na mesma tela não se misturam)', () => {
    const ids = [0, 1, 2].map(() => /id="(carShine\d+)"/.exec(carSilhouetteMarkup({ body: 'gt', color: '#ffffff' }))?.[1]);
    expect(new Set(ids).size).toBe(3);
  });
});
