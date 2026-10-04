// Os carros da arte no jogo (passo 2.4): cada `src/assets/cars/<estilo>.glb` (ex.: gt.glb, pickup.glb)
// substitui o modelo procedural daquele estilo. Carrega antes de o renderizador nascer (main.ts e o
// harness); arquivo recusado pelo validador fica de fora com o motivo e o estilo segue procedural.
// Sem arquivo nenhum, nada muda.
import { CAR_BODIES } from '../../core/data/cars';
import type { CarBody } from '../../core/types';
import { parseCarGlb } from './gltf';
import { setModelOverride } from './models';

// atalho: o .glb vai embutido no build como data URL (um pedaço de JS por arquivo, carregado sob demanda),
// porque o Electron abre o jogo por file:// e o fetch de file:// não funciona. Teto: os 13 carros (~200 KB
// cada). Saída quando o cenário também vier em .glb: servir os arquivos por um protocolo próprio do Electron.
const FILES = import.meta.glob<string>('../../assets/cars/*.glb', { query: '?inline', import: 'default' });

export interface CarAssetReport {
  loaded: CarBody[];
  rejected: { file: string; problems: string[] }[];
  warnings: string[];
}

let last: CarAssetReport = { loaded: [], rejected: [], warnings: [] };

/** O resultado do último carregamento (para o harness e os playtests). */
export function lastCarAssetReport(): CarAssetReport { return last; }

export function dataUrlToArrayBuffer(url: string): ArrayBuffer {
  const comma = url.indexOf(',');
  const bin = atob(url.slice(comma + 1));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out.buffer;
}

/** Carrega os arquivos e registra os aceitos. `files` = caminho → função que devolve a data URL. */
export async function loadCarAssets(files: Record<string, () => Promise<string>> = FILES): Promise<CarAssetReport> {
  const report: CarAssetReport = { loaded: [], rejected: [], warnings: [] };
  for (const [path, load] of Object.entries(files)) {
    const file = path.slice(path.lastIndexOf('/') + 1);
    const body = file.replace(/\.glb$/i, '') as CarBody;
    if (!CAR_BODIES.includes(body)) {
      report.rejected.push({ file, problems: [`"${body}" não é um estilo de carroceria (${CAR_BODIES.join(', ')})`] });
      continue;
    }
    try {
      const { model, problems, warnings } = await parseCarGlb(body, dataUrlToArrayBuffer(await load()));
      report.warnings.push(...warnings.map((w) => `${file}: ${w}`));
      if (model && problems.length === 0) { setModelOverride(body, model); report.loaded.push(body); } else report.rejected.push({ file, problems });
    } catch (e) {
      report.rejected.push({ file, problems: [`não carregou: ${e instanceof Error ? e.message : String(e)}`] });
    }
  }
  last = report;
  return report;
}
