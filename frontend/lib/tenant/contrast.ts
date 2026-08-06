// Espelha Agendio.Modules.Tenancy.Domain.ContrastValidator (backend) para dar
// feedback imediato na UI. O backend e SEMPRE a fonte da verdade — isto e so
// preview; a rejeicao real acontece ao salvar (ver Tenant.UpdateBranding).
const MINIMUM_AA_CONTRAST_RATIO = 4.5;

function linearize(channel: number): number {
  return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
}

function relativeLuminance(hex: string): number {
  const value = hex.replace("#", "");
  const r = parseInt(value.slice(0, 2), 16) / 255;
  const g = parseInt(value.slice(2, 4), 16) / 255;
  const b = parseInt(value.slice(4, 6), 16) / 255;
  return 0.2126 * linearize(r) + 0.7152 * linearize(g) + 0.0722 * linearize(b);
}

export function contrastRatio(hexA: string, hexB: string): number {
  const luminanceA = relativeLuminance(hexA);
  const luminanceB = relativeLuminance(hexB);
  const lighter = Math.max(luminanceA, luminanceB);
  const darker = Math.min(luminanceA, luminanceB);
  return (lighter + 0.05) / (darker + 0.05);
}

export function meetsAaContrast(foregroundHex: string, backgroundHex: string): boolean {
  return contrastRatio(foregroundHex, backgroundHex) >= MINIMUM_AA_CONTRAST_RATIO;
}
