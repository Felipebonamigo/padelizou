import { describe, expect, it } from 'vitest';
import { CAR_BODIES, CARS } from '../src/core/data/cars';

describe('carrocerias', () => {
  it('todo carro tem um estilo de carroceria conhecido e cor de detalhe válida', () => {
    for (const c of CARS) {
      expect(CAR_BODIES, c.id).toContain(c.body);
      if (c.accent !== undefined) expect(c.accent, c.id).toMatch(/^#[0-9a-f]{6}$/i);
    }
  });
});
