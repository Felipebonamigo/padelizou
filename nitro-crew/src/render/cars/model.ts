// O que cada estilo de carroceria entrega ao renderizador: a malha única (carroceria, cabine, vidro,
// lentes e peças, uma chamada de desenho por estilo), as rodas (desenho, eixos), as saídas do nitro e
// a ordem das pinturas. Um arquivo por estilo em styles/ — a porta para trocar um modelo por glTF.
import * as THREE from 'three';
import type { CarBody } from '../../core/types';
import type { Axle, Region } from './body';
import type { Brush, MeshBuilder, P3 } from './kit';
import { PAINT, STRIPE_A, STRIPE_B, TRIM, UNDER } from './paints';
import type { WheelDesign } from './wheels';

export interface CarModel {
  body: CarBody;
  shell: THREE.BufferGeometry;
  wheel: WheelDesign;
  axles: readonly [Axle, Axle];
  /** Saídas da chama do nitro (1 ou 2), na ponta do escapamento. */
  exhausts: readonly P3[];
  /** Altura do teto (a etiqueta de nome fica acima). */
  height: number;
  /** Ordem das pinturas (bits LIVERY_A/LIVERY_B) para o 1º, 2º... carro do estilo. */
  liveries: readonly number[];
  triangles: number;
}

export interface ModelParts {
  wheel: WheelDesign;
  axles: readonly [Axle, Axle];
  exhausts: readonly P3[];
  liveries: readonly number[];
}

/** Fecha o modelo: mede a altura, conta triângulos, monta a geometria. */
export function finish(body: CarBody, b: MeshBuilder, parts: ModelParts): CarModel {
  const shell = b.build();
  const bb = shell.boundingBox ?? new THREE.Box3();
  return { body, shell, ...parts, height: bb.max.y, triangles: b.triangles };
}

const STANDARD: Record<Region, Brush | null> = {
  under: UNDER, well: UNDER, sill: TRIM, lower: PAINT, band: STRIPE_B, upper: PAINT, shoulder: PAINT, top: PAINT, stripe: STRIPE_A, center: PAINT,
};

/** Pincel por região da carroceria: o padrão (soleira preta, faixa B no vinco, faixas A no capô) com trocas. */
export function regions(over: Partial<Record<Region, Brush | null>> = {}): (r: Region) => Brush | null {
  const m = { ...STANDARD, ...over };
  return (r) => m[r];
}
