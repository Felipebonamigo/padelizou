// Opções do jogador: leitura e gravação no localStorage com saneamento. Nada aqui lança —
// sem localStorage (Electron sem sessão, testes em Node, modo privado) tudo cai nos padrões.
import type { CoopAssists, Difficulty } from '../core/types';
import type { Lang } from '../i18n';
import { sanitizeBindings } from '../ui/remap/bindings';
import { DEFAULT_SETTINGS, type Quality, type Settings } from './contracts';

export const SETTINGS_KEY = 'nitro-crew.settings';
export const TOTAL_CARS_MIN = 8;
export const TOTAL_CARS_MAX = 20;
export const QUICK_LAPS_MIN = 2;
export const QUICK_LAPS_MAX = 8;

export const LANGUAGES: readonly Lang[] = ['pt', 'en'];
export const QUALITIES: readonly Quality[] = ['low', 'medium', 'high'];
export const DIFFICULTIES: readonly Difficulty[] = ['amador', 'profissional', 'campeao'];

// ───────────────────────────── Armazenamento ─────────────────────────────

function storage(): Storage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage;
  } catch {
    return null;
  }
}

/** JSON gravado sob `key`, ou `undefined` se não houver armazenamento, chave ou JSON válido. */
export function readJson(key: string): unknown {
  try {
    const raw = storage()?.getItem(key);
    return raw == null ? undefined : (JSON.parse(raw) as unknown);
  } catch {
    return undefined;
  }
}

/** Grava `value` como JSON; devolve falso quando não há onde gravar. */
export function writeJson(key: string, value: unknown): boolean {
  try {
    const s = storage();
    if (!s) return false;
    s.setItem(key, JSON.stringify(value));
    return true;
  } catch {
    return false;
  }
}

// ───────────────────────────── Saneamento ─────────────────────────────

export function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

export function pickEnum<T extends string>(v: unknown, allowed: readonly T[], fallback: T): T {
  return typeof v === 'string' && (allowed as readonly string[]).includes(v) ? (v as T) : fallback;
}

export function pickBool(v: unknown, fallback: boolean): boolean {
  return typeof v === 'boolean' ? v : fallback;
}

/** Número dentro de [min, max]; fora da faixa é trazido para a borda; lixo vira `fallback`. */
export function pickNumber(v: unknown, min: number, max: number, fallback: number, integer = false): number {
  if (typeof v !== 'number' || !Number.isFinite(v)) return fallback;
  const n = integer ? Math.round(v) : v;
  return Math.min(max, Math.max(min, n));
}

export function pickString(v: unknown, fallback: string, maxLength = 64): string {
  return typeof v === 'string' && v.length > 0 ? v.slice(0, maxLength) : fallback;
}

function pickAssists(v: unknown): CoopAssists {
  const d = DEFAULT_SETTINGS.assists;
  const r = isRecord(v) ? v : {};
  return {
    sharedNitro: pickBool(r.sharedNitro, d.sharedNitro),
    tow: pickBool(r.tow, d.tow),
    teamDraft: pickBool(r.teamDraft, d.teamDraft),
    catchup: pickBool(r.catchup, d.catchup),
  };
}

/** Funde `raw` com os padrões, validando faixas e enumerações. Nunca lança. */
export function sanitizeSettings(raw: unknown): Settings {
  const r = isRecord(raw) ? raw : {};
  const d = DEFAULT_SETTINGS;
  return {
    language: pickEnum(r.language, LANGUAGES, d.language),
    masterVolume: pickNumber(r.masterVolume, 0, 1, d.masterVolume),
    musicVolume: pickNumber(r.musicVolume, 0, 1, d.musicVolume),
    sfxVolume: pickNumber(r.sfxVolume, 0, 1, d.sfxVolume),
    fullscreen: pickBool(r.fullscreen, d.fullscreen),
    quality: pickEnum(r.quality, QUALITIES, d.quality),
    showMinimap: pickBool(r.showMinimap, d.showMinimap),
    screenShake: pickBool(r.screenShake, d.screenShake),
    difficulty: pickEnum(r.difficulty, DIFFICULTIES, d.difficulty),
    manualGear: pickBool(r.manualGear, d.manualGear),
    assists: pickAssists(r.assists),
    totalCars: pickNumber(r.totalCars, TOTAL_CARS_MIN, TOTAL_CARS_MAX, d.totalCars, true),
    quickLaps: pickNumber(r.quickLaps, QUICK_LAPS_MIN, QUICK_LAPS_MAX, d.quickLaps, true),
    music: pickString(r.music, d.music),
    controls: sanitizeBindings(r.controls),
    vibration: pickBool(r.vibration, d.vibration),
  };
}

export function loadSettings(): Settings {
  return sanitizeSettings(readJson(SETTINGS_KEY));
}

export function saveSettings(s: Settings): void {
  writeJson(SETTINGS_KEY, sanitizeSettings(s));
}
