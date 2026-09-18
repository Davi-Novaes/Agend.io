// Parser bem pequeno e local (sem dependencia nova) -- so precisa cobrir os
// poucos navegadores/SOs que realmente aparecem no log de seguranca, e cair
// de volta pro token bruto (ex.: "curl/8.21.0") pra chamadas de API/bots.
// Ordem dos testes importa: Edge/Opera/Chrome tem "Safari" no proprio UA, e
// Chrome tem "Safari" tambem -- por isso os mais especificos vem primeiro.

function detectBrowser(ua: string): string | null {
  const edge = ua.match(/Edg(?:A|iOS)?\/(\d+)/);
  if (edge) return `Edge ${edge[1]}`;

  const opera = ua.match(/(?:OPR|Opera)\/(\d+)/);
  if (opera) return `Opera ${opera[1]}`;

  const firefox = ua.match(/Firefox\/(\d+)/);
  if (firefox) return `Firefox ${firefox[1]}`;

  const chrome = ua.match(/(?:Chrome|CriOS)\/(\d+)/);
  if (chrome) return `Chrome ${chrome[1]}`;

  const safari = ua.match(/Version\/(\d+)[^ ]* .*Safari/);
  if (safari) return `Safari ${safari[1]}`;

  return null;
}

function detectOs(ua: string): string | null {
  if (/Windows NT 10\.0/.test(ua)) return "Windows 10/11";
  if (/Windows NT/.test(ua)) return "Windows";
  if (/iPhone|iPad|iPod/.test(ua)) return "iOS";
  if (/Mac OS X/.test(ua)) return "macOS";
  if (/Android/.test(ua)) return "Android";
  if (/Linux/.test(ua)) return "Linux";
  return null;
}

export type ParsedUserAgent = { label: string; full: string };

/** "Chrome 128 · Windows 10/11" quando reconhece o padrao; senao o primeiro token do UA cru (ex.: "curl/8.21.0"). */
export function parseUserAgent(userAgent: string | null): ParsedUserAgent {
  if (!userAgent) return { label: "—", full: "" };

  const browser = detectBrowser(userAgent);
  const os = detectOs(userAgent);
  if (!browser && !os) {
    return { label: userAgent.split(" ")[0] || userAgent, full: userAgent };
  }

  return { label: [browser, os].filter(Boolean).join(" · "), full: userAgent };
}
