// Registro dos marcos turísticos (onda G): junta os três arquivos de modelos.
import { LANDMARKS_BRASIL_CENTRO_SUL } from './brasil-centro-sul';
import { LANDMARKS_BRASIL_NORTE_NORDESTE } from './brasil-norte-nordeste';
import { LANDMARKS_MUNDO } from './mundo';
import type { LandmarkRegistry } from './types';

export const LANDMARKS: LandmarkRegistry = { ...LANDMARKS_BRASIL_CENTRO_SUL, ...LANDMARKS_BRASIL_NORTE_NORDESTE, ...LANDMARKS_MUNDO };
