"use client";

import * as React from "react";
import * as THREE from "three";
import { gsap } from "gsap";

// Deslocamento maximo do campo de luz ambiente ao seguir o cursor -- em
// unidades de UV (0..1 = largura/altura da tela), bem pequeno de proposito
// ("sutil e elegante, nao um holofote"). 0.016 ~= 22px numa tela de 1400px,
// a mesma amplitude da versao anterior em CSS.
const MAX_CURSOR_OFFSET_UV = 0.016;

const VERTEX_SHADER = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = vec4(position, 1.0);
  }
`;

// Cada glow() e um falloff analogico (nao um radial-gradient com stops
// empilhados) -- 1.0 no centro, some suavemente ate `radius`, com um pow()
// extra pra deixar a cauda ainda mais gradual. Sem contorno perceptivel,
// mesmo objetivo visual da versao CSS anterior (documentado em globals.css).
const FRAGMENT_SHADER = /* glsl */ `
  precision mediump float;
  uniform vec2 uResolution;
  uniform vec2 uBlobA;
  uniform vec2 uBlobB;
  uniform vec2 uCursorOffset;
  uniform vec3 uColorA;
  uniform vec3 uColorB;
  uniform float uIntensityA;
  uniform float uIntensityB;
  varying vec2 vUv;

  float glow(vec2 uv, vec2 center, float radius) {
    float aspect = uResolution.x / uResolution.y;
    vec2 p = vec2((uv.x - center.x) * aspect, uv.y - center.y);
    float d = length(p);
    float falloff = 1.0 - smoothstep(0.0, radius, d);
    return pow(clamp(falloff, 0.0, 1.0), 1.6);
  }

  void main() {
    vec2 offset = uCursorOffset;
    float gA = glow(vUv, uBlobA + offset, 0.95) * uIntensityA;
    float gB = glow(vUv, uBlobB + offset, 0.9) * uIntensityB;
    float alpha = clamp(gA + gB, 0.0, 1.0);
    // Saida NAO premultiplicada de proposito (cor "cheia", alpha separado) --
    // o renderer usa premultipliedAlpha:false (ver app-background.tsx) pra
    // compositar certinho com o fundo por baixo, seja ele escuro ou claro.
    // Combinar as duas cores premultiplicadas e SO ENTAO dividir pelo alpha
    // total evita a cor lavar/estourar quando o destino (--background) e
    // muito claro, que era exatamente o bug visto no dark/light compare.
    vec3 color = alpha > 0.0001 ? (uColorA * gA + uColorB * gB) / alpha : vec3(0.0);
    gl_FragColor = vec4(color, alpha);
  }
`;

// Origem dos dois campos de luz em espaco UV (0,0 = canto inferior esquerdo,
// 1,1 = canto superior direito) -- roxo perto do canto superior esquerdo,
// azul perto do inferior direito, ambos levemente PRA FORA da tela (mesma
// posição conceitual da versao CSS: so a cauda difusa alcança a area visivel).
const BLOB_A_ORIGIN = { x: -0.1, y: 1.05 };
const BLOB_B_ORIGIN = { x: 1.05, y: -0.05 };
const DRIFT_A = { x: 0.055, y: -0.05 };
const DRIFT_B = { x: -0.055, y: 0.05 };

// uIntensityA > uIntensityB no MESMO tom de proposito: o roxo (--primary,
// canais R/G baixos) le muito mais escuro a olho nu que o azul/ciano
// (--info, canais G/B altos) na mesma intensidade -- sem essa compensacao o
// campo A ficava quase invisivel enquanto o B ja aparecia bem (confirmado
// comparando os dois lado a lado no navegador). Escuro suporta mais
// intensidade que o claro sem competir com o conteudo, mesma proporcao da
// versao anterior em CSS.
function intensitiesFor(isDark: boolean): { a: number; b: number } {
  // Claro tem muito menos "headroom" que escuro pra clarear ainda mais sem
  // ficar um borrao branco visivel (fundo ja e quase branco) -- por isso a
  // queda de intensidade do escuro pro claro e bem maior aqui do que a
  // proporcao usada nos outros tons do design system.
  return isDark ? { a: 0.62, b: 0.3 } : { a: 0.16, b: 0.09 };
}

/**
 * Le a cor computada de uma CSS var em RGB 0-255 garantido, nao importa a
 * funcao de cor original (oklch/hex/color-mix/lab). Dois passos:
 * 1) getComputedStyle resolve o var() -- mas o VALOR DEVOLVIDO pode vir como
 *    "lab(...)" (nao "rgb(...)") quando a cor original (oklch de croma alto,
 *    ex.: --primary no tema claro) cai FORA do gamut sRGB -- o navegador
 *    serializa preservando fidelidade em vez de arredondar pra rgb(). Bug
 *    real encontrado assim: THREE.Color.setStyle() so entende rgb/hsl/hex/
 *    nome, NAO entende "lab(...)" -- falha silenciosa e vira branco (fundo
 *    aparecia "lavado" so no tema claro, nunca no escuro, porque so o
 *    --primary claro tem croma alto o bastante pra sair do gamut).
 * 2) Rasterizar essa string (seja rgb() ou lab()) num canvas 2D 1x1 e ler o
 *    pixel de volta -- canvas 2D entende qualquer <color> valido do CSS e
 *    SEMPRE devolve 0-255 sRGB, sem excecao.
 */
// Vector3 (nao THREE.Color) de proposito: o color management automatico do
// Three (ColorManagement.enabled, r152+) trata os 3 numeros de um THREE.Color
// como espaco LINEAR por convencao e reaplica um encode sRGB sempre que o
// valor e lido de volta (ex.: material.color, getHexString()) -- como os
// numeros aqui JA SAO sRGB (vem de canvas 2D, que so devolve sRGB), a
// "correcao" dobra o gamma e lava a cor pra um tom bem mais claro que o
// pretendido (bug real, visto comparando getHexString() com o pixel
// original). Vector3 nao carrega semantica de cor nenhuma -- chega na GPU
// exatamente como calculado aqui.
function resolveCssColor(cssValue: string): THREE.Vector3 {
  const probe = document.createElement("span");
  probe.style.color = cssValue;
  probe.style.display = "none";
  document.body.appendChild(probe);
  const resolved = getComputedStyle(probe).color;
  document.body.removeChild(probe);

  const canvas = document.createElement("canvas");
  canvas.width = 1;
  canvas.height = 1;
  const ctx = canvas.getContext("2d");
  if (!ctx) return new THREE.Vector3(0.384, 0.341, 0.961);
  ctx.fillStyle = resolved;
  ctx.fillRect(0, 0, 1, 1);
  const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
  return new THREE.Vector3(r / 255, g / 255, b / 255);
}

/** Fundo ambiente do painel interno -- so atras do conteudo (SidebarInset), nunca atras da sidebar (ver app/(app)/layout.tsx e globals.css). */
export function AppBackground() {
  const canvasRef = React.useRef<HTMLCanvasElement>(null);

  React.useEffect(() => {
    const canvasEl = canvasRef.current;
    if (!canvasEl) return;
    // Alias non-nulo: TS nao propaga o narrowing de `if (!x) return` pra
    // dentro de function declarations aninhadas (resize/render abaixo)
    // mesmo com `const` -- reatribuir pra uma nova const contorna isso.
    const canvas: HTMLCanvasElement = canvasEl;

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const hasRealCursor = window.matchMedia("(hover: hover)").matches;

    const renderer = new THREE.WebGLRenderer({
      canvas,
      alpha: true,
      antialias: false,
      powerPreference: "low-power",
      // false: o shader ja devolve cor NAO premultiplicada (ver fragment
      // shader) -- com premultipliedAlpha:true (default do Three), o
      // navegador espera cor JA multiplicada pelo alpha e o resultado lava
      // pra branco sobre um --background claro (bug real, reproduzido
      // comparando dark/light no navegador antes deste fix).
      premultipliedAlpha: false,
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));

    const scene = new THREE.Scene();
    const camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);

    const initialIntensities = intensitiesFor(document.documentElement.classList.contains("dark"));
    const uniforms = {
      uResolution: { value: new THREE.Vector2(1, 1) },
      uBlobA: { value: new THREE.Vector2(BLOB_A_ORIGIN.x, BLOB_A_ORIGIN.y) },
      uBlobB: { value: new THREE.Vector2(BLOB_B_ORIGIN.x, BLOB_B_ORIGIN.y) },
      uCursorOffset: { value: new THREE.Vector2(0, 0) },
      // Ambos os campos de luz vem da MESMA cor de marca do tenant --
      // uColorB e um tom mais claro (mix com branco) so pra dar variacao
      // visual entre os dois "blobs", nunca uma cor independente (--info) que
      // ignorava a marca configurada -- bug relatado: mudar a cor de marca em
      // Configuracoes -> Marca so refletia num dos dois campos de luz.
      uColorA: { value: resolveCssColor("var(--primary)") },
      uColorB: { value: resolveCssColor("color-mix(in srgb, var(--primary) 55%, white)") },
      uIntensityA: { value: initialIntensities.a },
      uIntensityB: { value: initialIntensities.b },
    };

    const material = new THREE.ShaderMaterial({
      vertexShader: VERTEX_SHADER,
      fragmentShader: FRAGMENT_SHADER,
      uniforms,
      transparent: true,
      depthTest: false,
      depthWrite: false,
    });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), material);
    scene.add(mesh);

    function resize() {
      const { clientWidth, clientHeight } = canvas.parentElement ?? canvas;
      const width = clientWidth || window.innerWidth;
      const height = clientHeight || window.innerHeight;
      renderer.setSize(width, height, false);
      uniforms.uResolution.value.set(width, height);
    }
    resize();
    window.addEventListener("resize", resize);

    // Re-resolve as cores quando o tema (.dark no <html>) MUDA DE CLASSE ou
    // quando a cor de marca do tenant muda -- TenantThemeProvider (ver
    // lib/tenant/tenant-theme-provider.tsx) aplica a cor personalizada como
    // INLINE STYLE em document.documentElement, nao como classe -- observar
    // so "class" nunca via essa mudanca, entao trocar a cor de marca so
    // atualizava o fundo depois de um F5 (o proximo mount recriava as
    // uniforms do zero, mas o MutationObserver em si ficava cego pra troca
    // ao vivo).
    const themeObserver = new MutationObserver(() => {
      const isDark = document.documentElement.classList.contains("dark");
      uniforms.uColorA.value.copy(resolveCssColor("var(--primary)"));
      uniforms.uColorB.value.copy(resolveCssColor("color-mix(in srgb, var(--primary) 55%, white)"));
      const { a, b } = intensitiesFor(isDark);
      uniforms.uIntensityA.value = a;
      uniforms.uIntensityB.value = b;
    });
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["class", "style"] });

    let cursorTween: gsap.core.Tween | null = null;
    let driftTweenA: gsap.core.Tween | null = null;
    let driftTweenB: gsap.core.Tween | null = null;
    let frameId: number | null = null;

    if (!reducedMotion) {
      // Ciclos longos e primos entre si (34s/27s) pra as duas camadas nunca
      // se repetirem em sincronia -- amplitude pequena de proposito
      // ("movimento quase imperceptivel"). yoyo+repeat:-1 fecha exatamente
      // no ponto de partida, sem salto no loop.
      driftTweenA = gsap.to(uniforms.uBlobA.value, {
        x: BLOB_A_ORIGIN.x + DRIFT_A.x,
        y: BLOB_A_ORIGIN.y + DRIFT_A.y,
        duration: 34,
        ease: "sine.inOut",
        repeat: -1,
        yoyo: true,
      });
      driftTweenB = gsap.to(uniforms.uBlobB.value, {
        x: BLOB_B_ORIGIN.x + DRIFT_B.x,
        y: BLOB_B_ORIGIN.y + DRIFT_B.y,
        duration: 27,
        ease: "sine.inOut",
        repeat: -1,
        yoyo: true,
        delay: -9,
      });
    }

    function handleMouseMove(event: MouseEvent) {
      const nx = (event.clientX / window.innerWidth - 0.5) * 2 * MAX_CURSOR_OFFSET_UV;
      // Y de tela cresce pra baixo, Y de UV cresce pra cima -- inverte.
      const ny = -(event.clientY / window.innerHeight - 0.5) * 2 * MAX_CURSOR_OFFSET_UV;
      cursorTween?.kill();
      cursorTween = gsap.to(uniforms.uCursorOffset.value, { x: nx, y: ny, duration: 2.2, ease: "power3.out" });
    }
    if (hasRealCursor && !reducedMotion) {
      window.addEventListener("mousemove", handleMouseMove);
    }

    function render() {
      renderer.render(scene, camera);
      frameId = requestAnimationFrame(render);
    }
    render();

    return () => {
      if (frameId !== null) cancelAnimationFrame(frameId);
      window.removeEventListener("resize", resize);
      window.removeEventListener("mousemove", handleMouseMove);
      themeObserver.disconnect();
      driftTweenA?.kill();
      driftTweenB?.kill();
      cursorTween?.kill();
      material.dispose();
      mesh.geometry.dispose();
      renderer.dispose();
    };
  }, []);

  return (
    // SEM overflow-hidden aqui: um ancestral com overflow (mesmo "hidden",
    // que nao tem barra de rolagem mas ainda conta como "scroll container"
    // pra spec) vira o contexto de referencia do sticky do filho -- como
    // essa div nunca rola sozinha (quem rola e a pagina), o sticky calculado
    // contra ela fica sempre preso em top:-scrollY, ou seja, NUNCA gruda de
    // verdade (bug real, visto com getBoundingClientRect mostrando o canvas
    // saindo da tela junto com o scroll). overflow-hidden entra so nos
    // filhos que realmente precisam (cada um abaixo).
    <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
      {/* sticky + h-screen (nao fixed): o wrapper e tao alto quanto o
          CONTEUDO da pagina (pode ser bem mais que 100vh); sticky prende o
          canvas no topo do viewport durante toda a rolagem sem nunca escapar
          da largura do proprio wrapper (que ja para exatamente onde a
          sidebar termina) -- mesmo resultado do background-attachment:fixed
          usado na versao anterior em CSS, sem o risco de um position:fixed
          de verdade vazar por cima da sidebar. overflow-hidden aqui e so
          contencao da PROPRIA caixa (nao afeta a propria capacidade de
          "grudar" — a regra do spec e sobre ANCESTRAIS, nunca o proprio
          elemento sticky). */}
      <div className="sticky top-0 h-screen w-full overflow-hidden">
        <canvas ref={canvasRef} className="absolute inset-0 size-full" />
      </div>
      {/* app-bg-lines usa inset:-15% (proposital, pra transform:translate
          nunca revelar borda) -- precisa da PROPRIA caixa com overflow-hidden
          pra nao vazar, ja que a wrapper de fora nao tem mais essa classe. */}
      <div className="absolute inset-0 overflow-hidden">
        <div className="app-bg-lines" />
      </div>
    </div>
  );
}
