// Idiomas: cada área registra suas próprias strings num namespace (arquivo próprio, sem
// conflito entre módulos). `t('ns.chave', { nome: 'x' })` substitui `{nome}`.
export type Lang = 'pt' | 'en';

type Table = Record<string, string>;
const tables: Record<Lang, Record<string, Table>> = { pt: {}, en: {} };
let current: Lang = 'pt';
const listeners = new Set<(lang: Lang) => void>();

export function registerStrings(ns: string, strings: Record<Lang, Table>): void {
  tables.pt[ns] = { ...(tables.pt[ns] ?? {}), ...strings.pt };
  tables.en[ns] = { ...(tables.en[ns] ?? {}), ...strings.en };
}

export function setLanguage(lang: Lang): void {
  if (lang === current) return;
  current = lang;
  for (const l of listeners) l(lang);
}

export function getLanguage(): Lang { return current; }

export function onLanguageChange(fn: (lang: Lang) => void): () => void {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export function t(key: string, params?: Record<string, string | number>): string {
  const dot = key.indexOf('.');
  const ns = dot < 0 ? '' : key.slice(0, dot);
  const k = dot < 0 ? key : key.slice(dot + 1);
  let text = tables[current][ns]?.[k] ?? tables.pt[ns]?.[k] ?? key;
  if (params) for (const [p, v] of Object.entries(params)) text = text.split(`{${p}}`).join(String(v));
  return text;
}

/** Chaves que existem num idioma e não no outro (teste de completude). */
export function missingKeys(): string[] {
  const out: string[] = [];
  for (const ns of new Set([...Object.keys(tables.pt), ...Object.keys(tables.en)])) {
    const pt = tables.pt[ns] ?? {}; const en = tables.en[ns] ?? {};
    for (const k of Object.keys(pt)) if (!(k in en)) out.push(`en:${ns}.${k}`);
    for (const k of Object.keys(en)) if (!(k in pt)) out.push(`pt:${ns}.${k}`);
  }
  return out;
}

export function namespaces(): string[] { return Object.keys(tables.pt); }
