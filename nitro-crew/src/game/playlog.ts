// Diário de jogo: o que o jogador fez, medido SEM sair do computador. Guarda o funil (do título à 1ª largada), as
// corridas (modo, pista, posição, duração, abandono, termômetro de emoção por assento) e as sessões datadas, num anel
// com teto, e as respostas do questionário de 3 perguntas. Nada é enviado: a chave `nitro-crew.playlog` é uma chave
// de save comum (writeJson), então no Electron o espelho a leva para <userData>/saves/nitro-crew.playlog.json — esse
// é o "relatório exportado em arquivo" — e "Copiar relatório" (src/errors/options.ts) junta o texto dele ao dos erros.
// Privacidade (docs/legal/PRIVACIDADE.md): nunca guarda nome de jogador, só números, ids de pista/copa/carro e datas.
// Puro: relógio, leitura e gravação entram por `PlaylogDeps`; os imports de valor são só do núcleo, do termômetro
// e do id da pista do tutorial; o resto é `import type` (nada de DOM, i18n nem desktop aqui).
import { carIndexOfSeat } from '../core/modes';
import type { AssistLevel, ChampionshipState, RaceState, Track } from '../core/types';
import type { RaceTelemetry } from './achievements';
import type { MenuEvent, RaceMode } from './contracts';
import { feelOf, meanFeel, type FeelTracker, type RaceFeel } from './race-feel';
import { TUTORIAL_TRACK_ID } from './tutorial-track';

export const PLAYLOG_KEY = 'nitro-crew.playlog';
export const PLAYLOG_FORMAT = 1;
export const MAX_RACES = 150;
export const MAX_SESSIONS = 100;
export const MAX_SURVEYS = 50;
/** < 1 MB por arquivo (desktop/storage.cjs:13); o diário é só ASCII, então caracteres = bytes. Fantasmas: 160 000. */
export const PLAYLOG_MAX_CHARS = 250_000;
export const SURVEY_AFTER_RACES = 3;
/**
 * Eventos que, depois de SURVEY_AFTER_RACES corridas completas, abrem o questionário antes de agir (ligação na sessão:
 * `askSurveyBefore` na 1ª linha de `handleMenuEvent`). O resultado também emite `startQuick` ("mais uma?") e
 * `startCup` (recomeçar/próxima copa): a ligação os acrescenta, conferindo com `grep -o "type: '[a-zA-Z]*'" results.ts`.
 */
export const SURVEY_GATE_EVENTS: readonly MenuEvent['type'][] = ['nextRace', 'retryRace', 'toMain', 'careerRace', 'startQuick', 'startCup'];

export type PlaylogMode = RaceMode | 'tutorial';
export type SurveyReason = 'races' | 'quit';
export type SurveyDifficulty = 'easy' | 'right' | 'hard';
export type SurveyHurdle = 'none' | 'menus' | 'controls' | 'seeing' | 'performance' | 'other';
export interface SurveyAnswers { again: number | null; difficulty: SurveyDifficulty | null; hurdle: SurveyHurdle | null }

export interface PlaylogSeat {
  seat: number; car: number; position: number; finished: boolean; assist: AssistLevel;
  crashes: number; contacts: number; pits: number; fuelEmpty: boolean; feel: RaceFeel | null;
}
export interface PlaylogRace {
  at: string; session: number; mode: PlaylogMode; online: boolean; track: string; cup: string | null;
  laps: number; field: number; seconds: number; eliminated: boolean; cupCompleted: boolean; abandoned: boolean; seats: PlaylogSeat[];
}
export type PlaylogRaceInput = Omit<PlaylogRace, 'at' | 'session'>;
export interface PlaylogSession {
  id: number; start: string; last: string; version: string;
  /** Corridas registradas nesta sessão (abandonadas inclusive) e as que chegaram ao race_over (não abandonadas). */
  races: number; completed: number;
  funnel: { ms: number; inputs: number; mode: PlaylogMode } | null;
}
export interface SurveyEntry extends SurveyAnswers { at: string; session: number; reason: SurveyReason; skipped: boolean }
export interface PlaylogData { format: 1; nextSession: number; sessions: PlaylogSession[]; races: PlaylogRace[]; surveys: SurveyEntry[] }

export interface PlaylogDeps {
  now(): Date;
  read(key: string): unknown;
  write(key: string, value: unknown): boolean;
  version: string;
  /** false desliga o questionário (main.ts: ?nosurvey=1, para playtests que encadeiam corridas); ausente = ligado. */
  survey?: boolean;
}

export interface Playlog {
  /** Abre sessão datada e o funil; GRAVA. */
  sessionStart(): void;
  /** Entrada de menu (tecla, clique ou direcional); NÃO grava. */
  input(): void;
  /** Fecha o funil (1ª vez na sessão); GRAVA. */
  raceStarted(info: { mode: RaceMode; trackId: string; humans: number }): void;
  /** GRAVA. */
  raceEnded(race: PlaylogRaceInput): void;
  /** Diz se o questionário deve abrir ANTES de `next` agir (e guarda o pedido); NÃO grava. */
  askSurveyBefore(next: MenuEvent, raceRunning: boolean): boolean;
  pendingSurvey(): { reason: SurveyReason; next: MenuEvent } | null;
  /** null = pulou; sem pedido não faz nada; GRAVA. */
  answerSurvey(a: SurveyAnswers | null): void;
  data(): Readonly<PlaylogData>;
  /** formatPlaylogReport(data, { version: deps.version, generated: agora }). */
  reportText(): string;
}

/** O que `playlogRace` precisa da sessão (a corrida ativa). */
export interface PlaylogRaceSource {
  state: RaceState; track: Track; mode: RaceMode; telemetry: RaceTelemetry; localSeats: readonly number[]; driver: unknown;
  /** Rastreador por CARRO (índice em state.cars), não por assento: no revezamento a dupla divide o carro. */
  feel: Readonly<Record<number, FeelTracker>>;
}

export interface PlaylogSummary {
  sessions: number; days: number; races: number; finished: number; abandoned: number; eliminated: number;
  retriedAfterElimination: number; racesPerSession: number; firstRaceFinished: boolean | null; firstFunnel: PlaylogSession['funnel'];
  surveysAnswered: number; surveysSkipped: number; againMean: number | null; feelMean: RaceFeel | null;
  fuelEmptyRaces: number; pitRaces: number;
}

const r2 = (n: number): number => Math.round(n * 100) / 100;

// ───────────────────────────── Validação (o que volta do armazenamento) ─────────────────────────────

const MODES: readonly string[] = ['cup', 'quick', 'timetrial', 'career', 'tournament', 'escort', 'relay', 'tutorial'];
const ASSISTS: readonly string[] = ['none', 'brake', 'steer', 'full'];
const DIFFICULTIES: readonly string[] = ['easy', 'right', 'hard'];
const HURDLES: readonly string[] = ['none', 'menus', 'controls', 'seeing', 'performance', 'other'];
const REASONS: readonly string[] = ['races', 'quit'];
const FEEL_KEYS: ReadonlyArray<keyof RaceFeel> = ['seconds', 'overtakes', 'overtakesPerMin', 'battleSeconds', 'aloneSeconds', 'flatOutPct', 'nitroPct', 'scrapes', 'rearHits'];

type Rec = Record<string, unknown>;
const isRec = (v: unknown): v is Rec => typeof v === 'object' && v !== null && !Array.isArray(v);
const isNum = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v);
const isStr = (v: unknown): v is string => typeof v === 'string';

function emptyData(): PlaylogData {
  return { format: PLAYLOG_FORMAT, nextSession: 1, sessions: [], races: [], surveys: [] };
}

function cleanFeel(v: unknown): RaceFeel | null | undefined {
  if (v === null) return null;
  if (!isRec(v)) return undefined;
  const out = {} as Record<keyof RaceFeel, number>;
  for (const k of FEEL_KEYS) {
    const n = v[k];
    if (!isNum(n)) return undefined;
    out[k] = n;
  }
  return out;
}

function cleanSeat(v: unknown): PlaylogSeat | null {
  if (!isRec(v)) return null;
  const feel = cleanFeel(v.feel);
  if (!isNum(v.seat) || !isNum(v.car) || !isNum(v.position) || typeof v.finished !== 'boolean' || !isStr(v.assist) || !ASSISTS.includes(v.assist)
    || !isNum(v.crashes) || !isNum(v.contacts) || !isNum(v.pits) || typeof v.fuelEmpty !== 'boolean' || feel === undefined) return null;
  return {
    seat: v.seat, car: v.car, position: v.position, finished: v.finished, assist: v.assist as AssistLevel,
    crashes: v.crashes, contacts: v.contacts, pits: v.pits, fuelEmpty: v.fuelEmpty, feel,
  };
}

function cleanRace(v: unknown): PlaylogRace | null {
  if (!isRec(v) || !Array.isArray(v.seats)) return null;
  const seats: PlaylogSeat[] = [];
  for (const s of v.seats) {
    const seat = cleanSeat(s);
    if (!seat) return null; // um assento inválido derruba a corrida inteira
    seats.push(seat);
  }
  if (!isStr(v.at) || !isNum(v.session) || !isStr(v.mode) || !MODES.includes(v.mode) || typeof v.online !== 'boolean' || !isStr(v.track)
    || !(v.cup === null || isStr(v.cup)) || !isNum(v.laps) || !isNum(v.field) || !isNum(v.seconds) || typeof v.eliminated !== 'boolean'
    || typeof v.cupCompleted !== 'boolean' || typeof v.abandoned !== 'boolean') return null;
  return {
    at: v.at, session: v.session, mode: v.mode as PlaylogMode, online: v.online, track: v.track, cup: v.cup, laps: v.laps, field: v.field,
    seconds: v.seconds, eliminated: v.eliminated, cupCompleted: v.cupCompleted, abandoned: v.abandoned, seats,
  };
}

function cleanSession(v: unknown): PlaylogSession | null {
  if (!isRec(v)) return null;
  let funnel: PlaylogSession['funnel'] = null;
  if (v.funnel !== null && v.funnel !== undefined) {
    const f = v.funnel;
    if (!isRec(f) || !isNum(f.ms) || !isNum(f.inputs) || !isStr(f.mode) || !MODES.includes(f.mode)) return null;
    funnel = { ms: f.ms, inputs: f.inputs, mode: f.mode as PlaylogMode };
  }
  if (!isNum(v.id) || !isStr(v.start) || !isStr(v.last) || !isStr(v.version) || !isNum(v.races) || !isNum(v.completed)) return null;
  return { id: v.id, start: v.start, last: v.last, version: v.version, races: v.races, completed: v.completed, funnel };
}

function cleanSurvey(v: unknown): SurveyEntry | null {
  if (!isRec(v)) return null;
  const again = v.again === null || (isNum(v.again) && v.again >= 1 && v.again <= 5) ? v.again : undefined;
  const difficulty = v.difficulty === null || (isStr(v.difficulty) && DIFFICULTIES.includes(v.difficulty)) ? v.difficulty : undefined;
  const hurdle = v.hurdle === null || (isStr(v.hurdle) && HURDLES.includes(v.hurdle)) ? v.hurdle : undefined;
  if (!isStr(v.at) || !isNum(v.session) || !isStr(v.reason) || !REASONS.includes(v.reason) || typeof v.skipped !== 'boolean'
    || again === undefined || difficulty === undefined || hurdle === undefined) return null;
  return {
    at: v.at, session: v.session, reason: v.reason as SurveyReason, skipped: v.skipped,
    again: again as number | null, difficulty: difficulty as SurveyDifficulty | null, hurdle: hurdle as SurveyHurdle | null,
  };
}

/** O que vem do armazenamento (arquivo editado, versão futura, lixo): diário vazio ou o que for válido, já com os tetos. */
export function sanitizePlaylog(raw: unknown): PlaylogData {
  if (!isRec(raw) || raw.format !== PLAYLOG_FORMAT) return emptyData();
  const list = <T>(v: unknown, clean: (x: unknown) => T | null): T[] => (Array.isArray(v) ? v.map(clean).filter((x): x is T => x !== null) : []);
  const sessions = list(raw.sessions, cleanSession).slice(-MAX_SESSIONS);
  const races = list(raw.races, cleanRace).slice(-MAX_RACES);
  const surveys = list(raw.surveys, cleanSurvey).slice(-MAX_SURVEYS);
  const next = isNum(raw.nextSession) && raw.nextSession >= 1 ? Math.floor(raw.nextSession) : 1;
  const maxId = sessions.reduce((m, s) => Math.max(m, s.id), 0);
  return { format: PLAYLOG_FORMAT, nextSession: Math.max(next, maxId + 1), sessions, races, surveys };
}

// ───────────────────────────── O diário ─────────────────────────────

export function createPlaylog(deps: PlaylogDeps): Playlog {
  const data = sanitizePlaylog(deps.read(PLAYLOG_KEY));
  let current: PlaylogSession | null = null;
  /** Funil aberto: quando a sessão começou e quantas entradas de menu houve desde então. */
  let funnel: { start: number; inputs: number } | null = null;
  let askedThisSession = false;
  let pending: { reason: SurveyReason; next: MenuEvent } | null = null;

  const nowIso = (): string => deps.now().toISOString();

  function persist(): void {
    data.races = data.races.slice(-MAX_RACES);
    data.sessions = data.sessions.slice(-MAX_SESSIONS);
    data.surveys = data.surveys.slice(-MAX_SURVEYS);
    // Teto em caracteres: a corrida mais antiga sai primeiro (o contador da sessão já a somou).
    while (data.races.length > 0 && JSON.stringify(data).length > PLAYLOG_MAX_CHARS) data.races.shift();
    deps.write(PLAYLOG_KEY, data); // o aviso de save que falha é do storage.ts
  }

  /** O "dia" é a data UTC de `at` (perto da meia-noite de Brasília ele vira às 21h): aceito, é só para não perguntar demais. */
  const today = (): string => nowIso().slice(0, 10);

  return {
    sessionStart() {
      const at = nowIso();
      current = { id: data.nextSession++, start: at, last: at, version: deps.version, races: 0, completed: 0, funnel: null };
      data.sessions.push(current);
      funnel = { start: deps.now().getTime(), inputs: 0 };
      askedThisSession = false;
      pending = null;
      persist();
    },
    input() {
      if (funnel) funnel.inputs++;
    },
    raceStarted(info) {
      if (!current) return;
      if (funnel) {
        current.funnel = {
          ms: deps.now().getTime() - funnel.start, inputs: funnel.inputs,
          mode: info.trackId === TUTORIAL_TRACK_ID ? 'tutorial' : info.mode,
        };
        funnel = null;
      }
      current.last = nowIso();
      persist();
    },
    raceEnded(race) {
      if (!current) return;
      data.races.push({ at: nowIso(), session: current.id, ...race });
      current.races++;
      if (!race.abandoned) current.completed++;
      current.last = nowIso();
      persist();
    },
    askSurveyBefore(next, raceRunning) {
      if (deps.survey === false || !current || pending || askedThisSession) return false;
      if (data.surveys.some((s) => s.at.slice(0, 10) === today())) return false;
      let reason: SurveyReason | null = null;
      if (next.type === 'quitApp' && current.races >= 1) reason = 'quit';
      else if (!raceRunning && SURVEY_GATE_EVENTS.includes(next.type) && current.completed >= SURVEY_AFTER_RACES) reason = 'races';
      if (!reason) return false;
      pending = { reason, next };
      askedThisSession = true;
      return true;
    },
    pendingSurvey: () => pending,
    answerSurvey(a) {
      if (!pending || !current) return;
      data.surveys.push({
        at: nowIso(), session: current.id, reason: pending.reason, skipped: a === null,
        again: a?.again ?? null, difficulty: a?.difficulty ?? null, hurdle: a?.hurdle ?? null,
      });
      pending = null;
      current.last = nowIso();
      persist();
    },
    data: () => data,
    reportText: () => formatPlaylogReport(data, { version: deps.version, generated: nowIso() }),
  };
}

// ───────────────────────────── Da corrida ao registro ─────────────────────────────

/** A corrida que terminou (ou foi largada no meio) vista como linha do diário. Nunca copia o nome do jogador. */
export function playlogRace(r: PlaylogRaceSource, o: { abandoned: boolean; champ: ChampionshipState | null }): PlaylogRaceInput {
  const { state, track } = r;
  const mode: PlaylogMode = track.def.id === TUTORIAL_TRACK_ID ? 'tutorial' : r.mode;
  const seats: PlaylogSeat[] = r.localSeats.map((seat) => {
    const car = carIndexOfSeat(state, seat);
    // Pelo CARRO e não pelo assento: no revezamento a linha do resultado é a do assento que dirigia no fim.
    const row = state.results?.find((x) => x.carId === car);
    const tel = r.telemetry.seats.get(seat);
    const tracker = r.feel[car];
    return {
      seat, car,
      position: o.abandoned || !row ? 0 : row.position,
      finished: !o.abandoned && !!row?.finished,
      assist: state.config.humans.find((h) => h.seat === seat)?.assist ?? 'none',
      crashes: tel?.crashes ?? 0, contacts: tel?.collisions ?? 0, pits: tel?.pitStops ?? 0,
      fuelEmpty: tracker?.fuelEmpty ?? false,
      feel: tracker ? feelOf(tracker) : null,
    };
  });
  return {
    mode,
    // O tutorial também usa driver (session.ts), mas não é online.
    online: r.driver !== null && mode !== 'tutorial',
    track: track.def.id, cup: o.champ?.cupId ?? null, laps: state.config.laps, field: state.cars.length,
    seconds: seats.reduce((m, s) => Math.max(m, s.feel?.seconds ?? 0), 0),
    eliminated: !o.abandoned && !!o.champ?.eliminated,
    cupCompleted: !o.abandoned && !!o.champ?.completed,
    abandoned: o.abandoned, seats,
  };
}

// ───────────────────────────── Resumo e relatório ─────────────────────────────

export function summarizePlaylog(d: PlaylogData): PlaylogSummary {
  const finishedRace = (r: PlaylogRace): boolean => !r.abandoned && r.seats.some((s) => s.finished);
  const ranked = r2Avg(d.surveys.map((s) => s.again).filter((n): n is number => n !== null));
  const firstSession = d.sessions.reduce<PlaylogSession | null>((m, s) => (m === null || s.id < m.id ? s : m), null);
  const firstRace = firstSession ? d.races.find((r) => r.session === firstSession.id) : undefined;
  const feels: RaceFeel[] = [];
  for (const r of d.races) {
    if (r.abandoned) continue;
    const seen = new Set<number>(); // um termômetro por carro: a dupla do revezamento conta uma vez
    for (const s of r.seats) if (s.feel && !seen.has(s.car)) { seen.add(s.car); feels.push(s.feel); }
  }
  const retried = d.races.filter((r, i) => r.eliminated && d.races.slice(i + 1).some((n) => n.session === r.session && (n.mode === 'cup' || n.mode === 'career'))).length;
  return {
    sessions: d.sessions.length,
    days: new Set(d.sessions.map((s) => s.start.slice(0, 10))).size,
    races: d.races.length,
    finished: d.races.filter(finishedRace).length,
    abandoned: d.races.filter((r) => r.abandoned).length,
    eliminated: d.races.filter((r) => r.eliminated).length,
    retriedAfterElimination: retried,
    racesPerSession: d.sessions.length ? r2(d.sessions.reduce((a, s) => a + s.completed, 0) / d.sessions.length) : 0,
    firstRaceFinished: firstRace ? finishedRace(firstRace) : null,
    firstFunnel: d.sessions.find((s) => s.funnel !== null)?.funnel ?? null,
    surveysAnswered: d.surveys.filter((s) => !s.skipped).length,
    surveysSkipped: d.surveys.filter((s) => s.skipped).length,
    againMean: ranked,
    feelMean: meanFeel(feels),
    fuelEmptyRaces: d.races.filter((r) => r.seats.some((s) => s.fuelEmpty)).length,
    pitRaces: d.races.filter((r) => r.seats.some((s) => s.pits > 0)).length,
  };
}

function r2Avg(list: readonly number[]): number | null {
  return list.length ? r2(list.reduce((a, b) => a + b, 0) / list.length) : null;
}

/** Texto em inglês e de formato estável (como formatReport, errors.ts): resumo legível e, depois, o JSON inteiro. */
export function formatPlaylogReport(d: PlaylogData, meta: { version: string; generated: string }): string {
  const s = summarizePlaylog(d);
  const funnel = s.firstFunnel ? `${(s.firstFunnel.ms / 1000).toFixed(1)} s, ${s.firstFunnel.inputs} inputs (${s.firstFunnel.mode})` : 'n/a';
  const survey = s.surveysAnswered + s.surveysSkipped > 0
    ? `${s.surveysAnswered} answered, ${s.surveysSkipped} skipped, "again tomorrow" mean ${s.againMean ?? 'n/a'}`
    : 'n/a';
  const f = s.feelMean;
  const pct = (part: number, total: number): number => (total > 0 ? Math.round((100 * part) / total) : 0);
  const feel = f
    ? `overtakes/min ${f.overtakesPerMin.toFixed(2)} · battle ${pct(f.battleSeconds, f.seconds)}% · alone ${pct(f.aloneSeconds, f.seconds)}% · flat out ${Math.round(f.flatOutPct)}% · nitro ${f.nitroPct.toFixed(1)}% · scrapes ${f.scrapes.toFixed(1)} · rear hits ${f.rearHits.toFixed(1)}`
    : 'n/a';
  const lines = [
    'Nitro Crew — play log',
    `version: ${meta.version}`,
    `generated: ${meta.generated}`,
    `sessions: ${s.sessions} (days: ${s.days})`,
    `races: ${s.races} (finished ${s.finished}, abandoned ${s.abandoned}, eliminated ${s.eliminated}, retried after elimination ${s.retriedAfterElimination})`,
    `races per session: ${s.racesPerSession}`,
    `first race finished: ${s.firstRaceFinished === null ? 'n/a' : s.firstRaceFinished ? 'yes' : 'no'}`,
    `title → first start: ${funnel}`,
    `survey: ${survey}`,
    `feel (completed races, one per car): ${feel}`,
    `fuel empty: ${s.fuelEmptyRaces} races · pit stops: ${s.pitRaces} races`,
    '--- json ---',
    JSON.stringify(d),
  ];
  return `${lines.join('\n')}\n`;
}

// ───────────────────────────── Instância do jogo ─────────────────────────────

let active: Playlog | null = null;

export function setActivePlaylog(p: Playlog | null): void { active = p; }
export function getActivePlaylog(): Playlog | null { return active; }
