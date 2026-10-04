// Os marcos da arte no jogo (docs/ARTE.md, "Marcos baixados"): cada `src/assets/landmarks/<id>.glb` (ex.:
// tuiuiu_ninho.glb) substitui a GEOMETRIA do marco procedural com aquele id; o registro (lugar, lado, vezes por
// volta, giro) continua o do LANDMARKS. Carrega antes de o renderizador nascer (main.ts e os harnesses); arquivo
// recusado pelo validador (check.ts) fica de fora com o motivo e o marco segue procedural. Sem arquivo, nada muda.
// As PEÇAS baixadas (`src/assets/landmarks/parts/<peça>.glb`, parts.ts: o bicho ou a estátua que entra num marco
// procedural) carregam pelo `loadLandmarkParts`, chamado logo antes deste.
import { dataUrlToArrayBuffer } from '../../cars/assets';
import { setLandmarkOverride } from '../catalog';
import { parseLandmarkGlb, parseLandmarkPartGlb } from './gltf';
import { LANDMARKS } from './index';
import { isPartName, PART_NAMES, setLandmarkPart } from './parts';

// atalho: embutido como data URL, como os carros (cars/assets.ts) — o Electron abre por file://. Teto: poucos
// marcos baixados (~100–300 KB cada, um pedaço de JS por arquivo, carregado sob demanda). Saída quando passar de
// ~20: servir os arquivos por um protocolo próprio do Electron.
const FILES = import.meta.glob<string>('../../../assets/landmarks/*.glb', { query: '?inline', import: 'default' });
// As peças baixadas (parts.ts): src/assets/landmarks/parts/<peça>.glb (o tuiuiú, a girafa…), do mesmo jeito.
const PART_FILES = import.meta.glob<string>('../../../assets/landmarks/parts/*.glb', { query: '?inline', import: 'default' });

export interface LandmarkAssetReport {
  loaded: string[];
  rejected: { file: string; problems: string[] }[];
  warnings: string[];
}

let last: LandmarkAssetReport = { loaded: [], rejected: [], warnings: [] };

/** O resultado do último carregamento (para o harness e os playtests). */
export function lastLandmarkAssetReport(): LandmarkAssetReport { return last; }

/** Carrega os arquivos e registra os aceitos. `files` = caminho → função que devolve a data URL. */
export async function loadLandmarkAssets(files: Record<string, () => Promise<string>> = FILES): Promise<LandmarkAssetReport> {
  const report: LandmarkAssetReport = { loaded: [], rejected: [], warnings: [] };
  for (const [path, load] of Object.entries(files)) {
    const file = path.slice(path.lastIndexOf('/') + 1);
    const id = file.replace(/\.glb$/i, '');
    if (!Object.prototype.hasOwnProperty.call(LANDMARKS, id)) {
      report.rejected.push({ file, problems: [`"${id}" não é um marco registrado (landmarks/index.ts; o nome do arquivo é o id)`] });
      continue;
    }
    try {
      const { model, problems, warnings } = await parseLandmarkGlb(LANDMARKS[id].place, dataUrlToArrayBuffer(await load()));
      report.warnings.push(...warnings.map((w) => `${file}: ${w}`));
      if (model && problems.length === 0) { setLandmarkOverride(id, model); report.loaded.push(id); } else report.rejected.push({ file, problems });
    } catch (e) {
      report.rejected.push({ file, problems: [`não carregou: ${e instanceof Error ? e.message : String(e)}`] });
    }
  }
  last = report;
  return report;
}

let lastParts: LandmarkAssetReport = { loaded: [], rejected: [], warnings: [] };

/** O resultado do último carregamento das peças (para o harness e os playtests). */
export function lastLandmarkPartReport(): LandmarkAssetReport { return lastParts; }

/**
 * Carrega as peças baixadas (bicho, estátua) e registra as aceitas pelo validador da peça; o marco que usa a peça
 * passa a montá-la no lugar do bicho procedural. Recusada = o marco segue com o procedural, motivo no relatório.
 */
export async function loadLandmarkParts(files: Record<string, () => Promise<string>> = PART_FILES): Promise<LandmarkAssetReport> {
  const report: LandmarkAssetReport = { loaded: [], rejected: [], warnings: [] };
  for (const [path, load] of Object.entries(files)) {
    const file = path.slice(path.lastIndexOf('/') + 1);
    const name = file.replace(/\.glb$/i, '');
    if (!isPartName(name)) {
      report.rejected.push({ file, problems: [`"${name}" não é uma peça (${PART_NAMES.join(', ')}; landmarks/parts.ts — o nome do arquivo é o da peça)`] });
      continue;
    }
    try {
      const { geometry, problems, warnings } = await parseLandmarkPartGlb(name, dataUrlToArrayBuffer(await load()));
      report.warnings.push(...warnings.map((w) => `${file}: ${w}`));
      if (geometry && problems.length === 0) { setLandmarkPart(name, geometry); report.loaded.push(name); } else report.rejected.push({ file, problems });
    } catch (e) {
      report.rejected.push({ file, problems: [`não carregou: ${e instanceof Error ? e.message : String(e)}`] });
    }
  }
  lastParts = report;
  return report;
}
