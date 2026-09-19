// Mascaras/validacao de formato para campos BR (CPF/CNPJ, telefone) usadas em
// mais de uma tela (onboarding, Dados da empresa, assinatura) -- extraido de
// app/(auth)/onboarding/page.tsx pra nao duplicar a mesma logica em cada
// formulario novo que precisar disso.

// Mesmo raciocinio de CpfCnpj.Create (backend, modulo 11) e PhoneNumber.Create
// (E.164) — validacao de FORMATO aqui e so uma conveniencia de UX (feedback
// imediato); a validacao de verdade (que ninguem pode burlar trocando o
// payload) e sempre a do backend.
export function isValidCpfCnpj(value: string): boolean {
  const digits = value.replace(/\D/g, "");
  if (digits.length !== 11 && digits.length !== 14) {
    return false;
  }
  if (new Set(digits).size === 1) {
    return false;
  }

  function checkDigit(base: string, weights: number[]): number {
    const sum = weights.reduce((acc, weight, index) => acc + Number(base[index]) * weight, 0);
    const remainder = sum % 11;
    return remainder < 2 ? 0 : 11 - remainder;
  }

  if (digits.length === 11) {
    const d1 = checkDigit(digits, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
    const d2 = checkDigit(digits, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
    return Number(digits[9]) === d1 && Number(digits[10]) === d2;
  }

  const d1 = checkDigit(digits, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  const d2 = checkDigit(digits, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  return Number(digits[12]) === d1 && Number(digits[13]) === d2;
}

/** Mascara em tempo real -- reformata a cada tecla a partir so dos digitos
 * (nunca confia no que ja estava formatado no valor anterior, senao apagar
 * um digito no meio bagunca a pontuacao). Fixo (11) 9999-9999 vs celular
 * (11) 99999-9999: sem separar por tipo, so reflow pra 5+4 quando o 11o
 * digito aparece -- comportamento padrao de mascara de telefone BR. */
export function formatPhone(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 11);
  if (digits.length <= 2) return digits ? `(${digits}` : "";
  if (digits.length <= 6) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  if (digits.length <= 10) return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
}

/** Mesma mascara de formatPhone, mas tambem reconhece um telefone ja
 * normalizado em E.164 pelo backend (+5511999998888, ver PhoneNumber.Create)
 * -- o "+55" so pode vir de la, nunca de digitacao manual, entao e o unico
 * sinal confiavel pra saber que os 2 primeiros digitos sao DDI e nao DDD
 * (DDD 55 existe de verdade, contar digito sozinho seria ambiguo). */
export function formatPhoneDisplay(value: string): string {
  const trimmed = value.trim();
  return trimmed.startsWith("+55") ? `+55 ${formatPhone(trimmed.slice(3))}` : formatPhone(trimmed);
}

/** Mesma ideia do telefone, mas o formato muda de vez (nao so reflui) ao
 * passar de 11 digitos -- ate ali e CPF (000.000.000-00), dali em diante CNPJ
 * (00.000.000/0000-00). Consistente com isValidCpfCnpj (mesmo corte). */
export function formatCpfCnpj(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 14);
  if (digits.length <= 11) {
    return digits
      .replace(/(\d{3})(\d)/, "$1.$2")
      .replace(/(\d{3})(\d)/, "$1.$2")
      .replace(/(\d{3})(\d{1,2})$/, "$1-$2");
  }
  return digits
    .replace(/(\d{2})(\d)/, "$1.$2")
    .replace(/(\d{3})(\d)/, "$1.$2")
    .replace(/(\d{3})(\d)/, "$1/$2")
    .replace(/(\d{4})(\d{1,2})$/, "$1-$2");
}
