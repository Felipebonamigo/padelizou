// Material único dos carros (carroceria, vidro, cromo, lentes e rodas de todos os estilos: um programa
// só na GPU). Um MeshPhysicalMaterial com o shader estendido:
// - cor = cor do vértice × (pintura da instância ou acento, conforme `aPaint`); a face de uma camada
//   de pintura (faixa, aro) vira o acento quando a instância usa aquela camada (bits em `instExtra.w`);
// - rugosidade e metal por vértice (`aMat.xy`): verniz, cromo, vidro, borracha no mesmo desenho;
// - farol e lanterna emissivos por vértice (`aMat.zw`), o freio por instância (bit 1 de `instExtra.w`);
// - verniz (clearcoat) só nas faces pintadas, e só na qualidade alta;
// - reflexo do céu (env map da cena) reforçado nas peças metálicas e no vidro;
// - farol escamoteável (`aPaint.w`) sobe à noite.
import * as THREE from 'three';

export interface CarUniforms {
  uSelfLight: { value: number };
  uHeadGlow: { value: number };
  uTailGlow: { value: number };
  uBrakeGlow: { value: number };
  uPopup: { value: number };
  uEnvBoost: { value: number };
}

const VERT_PARS = /* glsl */ `
attribute vec4 aPaint;
attribute vec4 aMat;
attribute vec4 instExtra;
uniform float uPopup;
varying vec4 vMat;
varying float vPaintW;
varying float vBrake;
`;

const VERT_COLOR = /* glsl */ `
vColor = vec4( 1.0 );
{
  int flags = int( instExtra.w + 0.5 );
  float livA = ( flags & 2 ) != 0 ? 1.0 : 0.0;
  float livB = ( flags & 4 ) != 0 ? 1.0 : 0.0;
  int layer = int( aPaint.z * 3.0 + 0.5 );
  float liv = layer == 1 ? livA : ( layer == 2 ? livB : ( layer == 3 ? max( livA, livB ) : 0.0 ) );
  vec3 painted = mix( instanceColor.rgb, instExtra.rgb, aPaint.y );
  vec3 base = color.rgb * mix( vec3( 1.0 ), painted, aPaint.x );
  vColor.rgb = mix( base, instExtra.rgb, liv );
  vBrake = ( flags & 1 ) != 0 ? 1.0 : 0.0;
  vMat = aMat;
  vPaintW = max( aPaint.x, liv );
}
`;

const FRAG_PARS = /* glsl */ `
uniform float uSelfLight;
uniform float uHeadGlow;
uniform float uTailGlow;
uniform float uBrakeGlow;
uniform float uEnvBoost;
varying vec4 vMat;
varying float vPaintW;
varying float vBrake;
`;

export function createCarMaterial(): { material: THREE.MeshPhysicalMaterial; uniforms: CarUniforms } {
  const uniforms: CarUniforms = {
    uSelfLight: { value: 0.2 }, uHeadGlow: { value: 0.4 }, uTailGlow: { value: 0.9 }, uBrakeGlow: { value: 3.2 },
    uPopup: { value: 0 }, uEnvBoost: { value: 1.6 },
  };
  const material = new THREE.MeshPhysicalMaterial({
    vertexColors: true, roughness: 0.4, metalness: 0.2, // normal suave da malha (kit.ts), não sombreado plano
    clearcoat: 0, clearcoatRoughness: 0.06,
  });
  material.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>\n${VERT_PARS}`)
      .replace('#include <color_vertex>', VERT_COLOR)
      .replace('#include <begin_vertex>', '#include <begin_vertex>\ntransformed.y += aPaint.w * uPopup * 0.11;');
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>\n${FRAG_PARS}`)
      .replace('#include <roughnessmap_fragment>', 'float roughnessFactor = vMat.x;')
      .replace('#include <metalnessmap_fragment>', 'float metalnessFactor = vMat.y;')
      .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
        // Uma fração da própria cor como emissivo: o lado na sombra (o que a câmera de perseguição vê)
        // continua lendo a cor do carro em vez do azul do céu. Estilização, não física.
        // Menos nas cores claras: o branco já lê branco na sombra e, com mais, estoura no bloom.
        float selfLum = dot( diffuseColor.rgb, vec3( 0.2126, 0.7152, 0.0722 ) );
        totalEmissiveRadiance += diffuseColor.rgb * uSelfLight * ( 1.0 - vMat.y * 0.6 ) * ( 1.0 - 0.65 * selfLum );
        totalEmissiveRadiance += diffuseColor.rgb * ( vMat.z * uHeadGlow + vMat.w * mix( uTailGlow, uBrakeGlow, vBrake ) );`)
      .replace('#include <lights_physical_fragment>', `#include <lights_physical_fragment>
        #ifdef USE_CLEARCOAT
          material.clearcoat *= vPaintW * 0.7;
        #endif`)
      .replace('#include <lights_fragment_maps>', `#include <lights_fragment_maps>
        #if defined( USE_ENVMAP ) && defined( RE_IndirectSpecular )
          // Peças metálicas (cromo, aro) e vidro refletem mais o céu; a pintura, pouco (a cor manda).
          radiance *= uEnvBoost * ( 0.35 + vMat.y );
          #ifdef USE_CLEARCOAT
            clearcoatRadiance *= uEnvBoost * 0.55;
          #endif
        #endif`);
  };
  // Um programa para todos os carros: a chave não muda por instância.
  material.customProgramCacheKey = () => 'nitro-car-v1';
  return { material, uniforms };
}

/** Verniz só na qualidade alta (o programa troca uma vez, na troca de qualidade). */
export function setCarQuality(material: THREE.MeshPhysicalMaterial, high: boolean): void {
  material.clearcoat = high ? 1 : 0;
}
