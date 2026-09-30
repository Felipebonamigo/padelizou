import { describe, expect, it } from 'vitest';
import { SEGMENT_LENGTH } from '../src/core/constants';
import {
  CAMERA_DEPTH, CAMERA_HEIGHT, PLAYER_Z, placeOnRoad, projectPoint, projectRoad, roadYAt, type RoadCamera,
} from '../src/render-pseudo3d/projection';
import { syntheticTrack } from './helpers';

const view = { width: 320, height: 240 };
const camAt = (track: ReturnType<typeof syntheticTrack>, z: number, x = 0): RoadCamera => ({ z, x, y: roadYAt(track, z) + CAMERA_HEIGHT });

describe('projeção pseudo-3D (modo Retrô)', () => {
  it('fórmula clássica: ponto no eixo da câmera cai no centro; mais longe, menor e mais perto do horizonte', () => {
    const cam = { x: 0, y: CAMERA_HEIGHT, z: 0 };
    const near = projectPoint(0, 0, 1000, cam, view, { x: 0, y: 0, w: 0, scale: 0, cz: 0 });
    const far = projectPoint(0, 0, 10000, cam, view, { x: 0, y: 0, w: 0, scale: 0, cz: 0 });
    expect(near.x).toBe(160); expect(far.x).toBe(160);
    expect(near.scale).toBeCloseTo(CAMERA_DEPTH / 1000, 12);
    expect(near.w).toBeGreaterThan(far.w);
    expect(near.y).toBeGreaterThan(far.y);           // chão perto fica mais embaixo na tela
    expect(far.y).toBeGreaterThan(view.height / 2);  // e nunca passa do horizonte
  });

  it('reta plana: a estrada fica centrada e afina até o horizonte', () => {
    const track = syntheticTrack([{ op: 'straight', length: 400 }]);
    const p = projectRoad(track, camAt(track, 1000), view, 100);
    const visible = p.filter((s) => s.visible);
    // Perto do horizonte vários segmentos caem na mesma linha de pixel (y arredondado, como no Javascript
    // Racer) e não viram polígono; todos continuam à frente para os sprites.
    expect(visible.length).toBeGreaterThan(25);
    expect(p.slice(1).every((s) => s.inFront)).toBe(true); // o 1º começa na própria câmera
    for (const s of visible) { expect(s.p1.x).toBe(160); expect(s.p2.x).toBe(160); }
    for (let i = 1; i < visible.length; i++) {
      expect(visible[i].p1.w).toBeLessThanOrEqual(visible[i - 1].p1.w);
      expect(visible[i].p1.y).toBeLessThanOrEqual(visible[i - 1].p1.y);
    }
  });

  it('curva: a estrada acumula o desvio para o lado da curva (positiva = direita)', () => {
    const right = syntheticTrack([{ op: 'straight', length: 20 }, { op: 'curve', length: 200, curve: 4 }, { op: 'straight', length: 200 }]);
    const left = syntheticTrack([{ op: 'straight', length: 20 }, { op: 'curve', length: 200, curve: -4 }, { op: 'straight', length: 200 }]);
    const pr = projectRoad(right, camAt(right, 0), view, 120).filter((s) => s.visible);
    const pl = projectRoad(left, camAt(left, 0), view, 120).filter((s) => s.visible);
    const lastR = pr[pr.length - 1]; const lastL = pl[pl.length - 1];
    expect(lastR.p2.x).toBeGreaterThan(160 + 20);
    expect(lastL.p2.x).toBeLessThan(160 - 20);
    // Espelho: a curva para a esquerda é a da direita refletida.
    expect(lastR.p2.x - 160).toBeCloseTo(160 - lastL.p2.x, -1);
  });

  it('morro: a subida à frente levanta a estrada na tela; depois da crista a descida some (atrás do morro)', () => {
    const flat = syntheticTrack([{ op: 'straight', length: 600 }]);
    const hill = syntheticTrack([{ op: 'straight', length: 10 }, { op: 'hill', length: 120, height: 40 }, { op: 'straight', length: 500 }]);
    const pf = projectRoad(flat, camAt(flat, 0), view, 60);
    const ph = projectRoad(hill, camAt(hill, 0), view, 60);
    // Segmento ~40 à frente (na subida): mais alto na tela que o mesmo segmento na reta.
    expect(ph[40].p1.y).toBeLessThan(pf[40].p1.y);
    // Do pé do morro, a estrada do outro lado da crista não aparece.
    const behindCrest = projectRoad(hill, camAt(hill, 0), view, 130).slice(75, 130);
    expect(behindCrest.some((s) => !s.visible)).toBe(true);
  });

  it('câmera à direita: a estrada aparece deslocada para a esquerda', () => {
    const track = syntheticTrack([{ op: 'straight', length: 400 }]);
    const p = projectRoad(track, { ...camAt(track, 1000), x: 0.5 * 2000 }, view, 50);
    expect(p[20].p1.x).toBeLessThan(160);
  });

  it('carro do jogador (PLAYER_Z à frente da câmera) em x = +1 fica na borda direita da estrada', () => {
    const track = syntheticTrack([{ op: 'straight', length: 400 }]);
    const camZ = 5000;
    const p = projectRoad(track, camAt(track, camZ), view, 100);
    const center = placeOnRoad(p, track, camZ + PLAYER_Z, 0, view);
    const edge = placeOnRoad(p, track, camZ + PLAYER_Z, 1, view);
    expect(center).not.toBeNull();
    expect(center!.x).toBe(160);
    const seg = p[center!.n];
    const halfWidth = seg.p1.w + (seg.p2.w - seg.p1.w) * (((camZ + PLAYER_Z) % SEGMENT_LENGTH) / SEGMENT_LENGTH);
    expect(edge!.x - center!.x).toBeCloseTo(halfWidth, 0);
    // Fora da distância desenhada não se desenha.
    expect(placeOnRoad(p, track, camZ + 150 * SEGMENT_LENGTH, 0, view)).toBeNull();
  });

  it('fim da volta: os segmentos depois da linha continuam à frente (z + comprimento), sem salto', () => {
    const track = syntheticTrack([{ op: 'straight', length: 300 }]);
    const p = projectRoad(track, camAt(track, track.length - 10 * SEGMENT_LENGTH), view, 60);
    expect(p[15].segment.index).toBeLessThan(p[5].segment.index); // passou da linha
    for (let i = 1; i < 60; i++) expect(p[i].p1.cz).toBeGreaterThan(p[i - 1].p1.cz);
    expect(p.slice(1).every((s) => s.inFront)).toBe(true);
  });
});
