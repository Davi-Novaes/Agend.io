// Decodificacao client-side so para exibicao (ex.: iniciais no avatar) — NUNCA
// verifica assinatura. A validacao real do token sempre acontece no backend;
// isto e so leitura de um payload que ja confiamos por termos acabado de
// receber do proprio backend no login.
export function decodeJwtEmail(accessToken: string): string | null {
  try {
    const payload = accessToken.split(".")[1];
    if (!payload) {
      return null;
    }

    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      atob(base64)
        .split("")
        .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, "0")}`)
        .join("")
    );

    const claims = JSON.parse(json) as Record<string, unknown>;
    const email = claims["email"] ?? claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];

    return typeof email === "string" ? email : null;
  } catch {
    return null;
  }
}
