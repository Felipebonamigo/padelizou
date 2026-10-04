// Tipos do simplificador do meshoptimizer que vem com o three (examples/jsm/libs; é o motor do SimplifyModifier,
// que não tem as opções de que o conversor de marcos precisa). Só o que scripts/lib/landmark-convert.ts usa.
declare module 'three/examples/jsm/libs/meshopt_simplifier.module.js' {
  type Flag = 'LockBorder' | 'Sparse' | 'ErrorAbsolute' | 'Prune' | 'Regularize' | 'Permissive' | 'RegularizeLight';
  export const MeshoptSimplifier: {
    ready: Promise<void>;
    supported: boolean;
    /** Índices simplificados e o erro relativo alcançado. */
    simplify(indices: Uint32Array, positions: Float32Array, stride: number, targetIndexCount: number, targetError: number, flags?: Flag[]): [Uint32Array, number];
    simplifySloppy(indices: Uint32Array, positions: Float32Array, stride: number, lock: Uint8Array | null, targetIndexCount: number, targetError: number): [Uint32Array, number];
  };
}
