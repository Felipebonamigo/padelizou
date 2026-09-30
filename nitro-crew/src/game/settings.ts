// Opções do jogador: leitura e gravação (storage.ts) com saneamento. Nada aqui lança —
// sem localStorage (Electron sem sessão, testes em Node, modo privado) tudo cai nos padrões.
import { COLOR_PALETTES } from '../core/data/drivers';
import { ASSIST_LEVELS } from '../core/sim/assist';
import type { AssistLevel, CoopAssists, Difficulty } from '../core/types';
import type { Lang } from '../i18n';
import { sanitizeBindings } from '../ui/remap/bindings';
import { normalizeServerUrl } from '../net/protocol';
import { DEFAULT_SETTINGS, type Quality, type RenderStyle, type Settings } from './contracts';
import { readJson, writeJson } from './storage';

// Onde gravar (localStorage, arquivo do Electron, memória da sessão) é com storage.ts; save e fantasmas importam daqui.
export { readJson, writeJson };

export const SETTINGS_KEY = 'nitro-crew.settings';
export const TOTAL_CARS_MIN = 8;
export const TOTAL_CARS_MAX = 20;
export const QUICK_LAPS_MIN = 2;
export const QUICK_LAPS_MAX = 8;
export const HUD_SCALE_MIN = 0.8;
export const HUD_SCALE_MAX = 1.5;

export const LANGUAGES: readonly Lang[] = ['pt', 'en'];
export const QUALITIES: readonly Quality[] = ['low', 'medium', 'high'];
export const RENDER_STYLES: readonly RenderStyle[] = ['modern', 'retro'];
export const DIFFICULTIES: readonly Difficulty[] = ['amador', 'profissional', 'campeao'];

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

/** Uma assistência por assento (4), cada uma válida; o que faltar ou vier errado vira 'none'. */
function pickSeatAssists(v: unknown): AssistLevel[] {
  const r = Array.isArray(v) ? v : [];
  return DEFAULT_SETTINGS.seatAssists.map((d, i) => pickEnum(r[i], ASSIST_LEVELS, d));
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
    renderStyle: pickEnum(r.renderStyle, RENDER_STYLES, d.renderStyle),
    showMinimap: pickBool(r.showMinimap, d.showMinimap),
    screenShake: pickBool(r.screenShake, d.screenShake),
    difficulty: pickEnum(r.difficulty, DIFFICULTIES, d.difficulty),
    manualGear: pickBool(r.manualGear, d.manualGear),
    assists: pickAssists(r.assists),
    totalCars: pickNumber(r.totalCars, TOTAL_CARS_MIN, TOTAL_CARS_MAX, d.totalCars, true),
    quickLaps: pickNumber(r.quickLaps, QUICK_LAPS_MIN, QUICK_LAPS_MAX, d.quickLaps, true),
    music: pickString(r.music, d.music),
    // O booleano `telemetry` de antes dos termos versionados não vale como consentimento: volta desligada.
    telemetryConsent: pickNumber(r.telemetryConsent, 0, 1_000_000, d.telemetryConsent, true),
    controls: sanitizeBindings(r.controls),
    vibration: pickBool(r.vibration, d.vibration),
    serverUrl: normalizeServerUrl(r.serverUrl) ?? d.serverUrl,
    ghost: pickBool(r.ghost, d.ghost),
    seatAssists: pickSeatAssists(r.seatAssists),
    colorPalette: pickEnum(r.colorPalette, COLOR_PALETTES, d.colorPalette),
    // Em passos de 10%, como a tela de opções anda.
    hudScale: Math.round(pickNumber(r.hudScale, HUD_SCALE_MIN, HUD_SCALE_MAX, d.hudScale) * 10) / 10,
    largeText: pickBool(r.largeText, d.largeText),
    reduceEffects: pickBool(r.reduceEffects, d.reduceEffects),
  };
}

export function loadSettings(): Settings {
  return sanitizeSettings(readJson(SETTINGS_KEY));
}

export function saveSettings(s: Settings): void {
  writeJson(SETTINGS_KEY, sanitizeSettings(s));
}
