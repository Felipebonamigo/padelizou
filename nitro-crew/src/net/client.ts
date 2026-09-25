// Cliente WebSocket do relay: abre a conexão, valida cada mensagem recebida (as inválidas são
// descartadas e contadas) e manda as nossas. Funciona no navegador e no Node 22 (WebSocket global).
import { parseServerMessage, type ClientMessage, type ContentRules, type ServerMessage } from './protocol';

export interface NetClientHandlers {
  onOpen(): void;
  onMessage(msg: ServerMessage): void;
  /** A conexão fechou (ou nem abriu). `wasOpen` distingue "caiu" de "não conectou". */
  onClose(info: { code: number; wasOpen: boolean }): void;
}

export type SocketFactory = (url: string) => WebSocket;

export const defaultSocket: SocketFactory = (url) => new WebSocket(url);

export class NetClient {
  readonly url: string;
  readonly stats = { sent: 0, received: 0, invalid: 0 };
  private ws: WebSocket | null;
  private wasOpen = false;
  private closed = false;

  constructor(url: string, private readonly rules: ContentRules, private readonly handlers: NetClientHandlers, factory: SocketFactory = defaultSocket) {
    this.url = url;
    let ws: WebSocket | null = null;
    try {
      ws = factory(url);
    } catch {
      ws = null;
    }
    this.ws = ws;
    if (!ws) {
      // URL que o próprio construtor do WebSocket recusa: avisa como falha de conexão, fora da pilha.
      queueMicrotask(() => this.finish(1006));
      return;
    }
    ws.onopen = () => { this.wasOpen = true; this.handlers.onOpen(); };
    ws.onmessage = (ev: MessageEvent) => {
      this.stats.received++;
      const msg = parseServerMessage(typeof ev.data === 'string' ? ev.data : null, this.rules);
      if (!msg) { this.stats.invalid++; return; }
      this.handlers.onMessage(msg);
    };
    ws.onclose = (ev: CloseEvent) => this.finish(ev.code);
    ws.onerror = () => { /* o close vem em seguida */ };
  }

  get isOpen(): boolean {
    return this.ws !== null && this.ws.readyState === 1;
  }

  send(msg: ClientMessage): boolean {
    if (!this.ws || this.ws.readyState !== 1) return false;
    this.ws.send(JSON.stringify(msg));
    this.stats.sent++;
    return true;
  }

  /** Fecha sem avisar `onClose` (saída pedida por nós). */
  close(): void {
    this.closed = true;
    const ws = this.ws;
    this.ws = null;
    if (!ws) return;
    ws.onopen = null; ws.onmessage = null; ws.onclose = null; ws.onerror = null;
    try { ws.close(1000); } catch { /* já fechado */ }
  }

  private finish(code: number): void {
    if (this.closed) return;
    this.closed = true;
    this.ws = null;
    this.handlers.onClose({ code, wasOpen: this.wasOpen });
  }
}
