// Contrato dos marcos turísticos (onda G, docs/PISTAS-TURISMO.md): o modelo e onde ele aparece.
// Marcos são só visuais: nunca dentro do alcance do carro (|x| > 26 m), nunca escondendo a pista.
import type { Model } from '../geom';

export interface LandmarkDef {
  build: () => Model;
  /**
   * near: 30–80 m da pista (igreja, placa, casario, portal), tamanho real;
   * far: 120–400 m (ponte, castelo, farol no morro), tamanho real;
   * skyline: no horizonte, 400 m+ (montanha, estátua no alto do morro, vulcão), escala grande.
   */
  place: 'near' | 'far' | 'skyline';
  /** Lado: 'sea' só no lado do mar do litoral (direita), 'land' do lado de terra, 'any' qualquer. */
  side: 'land' | 'sea' | 'any';
  /** Quantas vezes aparece por volta (o primeiro sempre perto da largada). */
  perLap: number;
}

export type LandmarkRegistry = Readonly<Record<string, LandmarkDef>>;
