// Normais suaves do cenário (o pedido "menos quadrado"): cada modelo, ao ser montado pelo catálogo (uma vez por id,
// nunca por instância nem por quadro), recebe a normal suave com o vinco da família dele. Só as partes iluminadas
// com cor por vértice (`flat` e as fachadas): `glow`, `beacon` e `cone` são luz (o material não usa normal) e o
// `panel` é placa plana. Malha indexada é arte com normal própria (glTF): fica como veio.
import { CREASE, ROOF_TURN_DEG, smoothNormals } from '../normals';
import type { MatKey, Model } from './geom';

const LIT: ReadonlySet<MatKey> = new Set<MatKey>(['flat', 'office', 'apartment', 'classic', 'house']);

export interface SceneryShading {
  /** Vinco (graus) da família (`CREASE`). */
  crease: number;
  /** Regra do telhado (`ROOF_TURN_DEG`): só construções e marcos, que têm telhado de quatro águas e pináculo. */
  roofTurnDeg?: number;
}

/** Como o modelo é sombreado, pelo id do catálogo ("<família>:<parâmetros>"; marcos com o prefixo "lm:"). */
export function sceneryShading(id: string): SceneryShading {
  const family = id.slice(0, id.indexOf(':') < 0 ? id.length : id.indexOf(':'));
  switch (family) {
    case 'tree': case 'pine': case 'palm': case 'cactus': case 'bush': case 'tuft': case 'flowers':
      return { crease: CREASE.plant };
    case 'rock': case 'searock': case 'stack': case 'mesa': case 'termite': case 'pebbles':
      return { crease: CREASE.rock };
    case 'far':
      return { crease: id.startsWith('far:rock') || id.startsWith('far:boulder') ? CREASE.rock : CREASE.plant };
    case 'lm':
      return { crease: CREASE.landmark, roofTurnDeg: ROOF_TURN_DEG };
    default:
      return { crease: CREASE.built, roofTurnDeg: ROOF_TURN_DEG };
  }
}

/** Aplica a normal suave nas partes iluminadas do modelo (no lugar) e devolve o próprio modelo. */
export function smoothModel(id: string, model: Model): Model {
  const shading = sceneryShading(id);
  for (const p of model.parts) if (LIT.has(p.mat) && !p.geometry.index) smoothNormals(p.geometry, shading.crease, shading);
  return model;
}
