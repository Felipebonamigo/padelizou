// FileReader mínimo para o Node: o GLTFExporter do three usa readAsArrayBuffer/readAsDataURL com
// onloadend, e o Node não tem FileReader. Só para scripts e testes (o navegador tem o de verdade).
export function installNodeFileReader(): void {
  const g = globalThis as { FileReader?: unknown };
  if (g.FileReader) return;
  class NodeFileReader {
    result: ArrayBuffer | string | null = null;
    onload: (() => void) | null = null;
    onloadend: (() => void) | null = null;
    private done(result: ArrayBuffer | string): void {
      this.result = result;
      this.onload?.();
      this.onloadend?.();
    }
    readAsArrayBuffer(blob: Blob): void {
      void blob.arrayBuffer().then((b) => this.done(b));
    }
    readAsDataURL(blob: Blob): void {
      void blob.arrayBuffer().then((b) => this.done(`data:${blob.type || 'application/octet-stream'};base64,${Buffer.from(b).toString('base64')}`));
    }
  }
  g.FileReader = NodeFileReader;
}
