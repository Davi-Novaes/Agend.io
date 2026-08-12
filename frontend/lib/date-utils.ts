export function toDateOnly(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

export function daysAgo(days: number): Date {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return date;
}

/**
 * Intervalo anterior de mesmo tamanho, imediatamente antes de `from` — usado
 * pra calcular variacao percentual vs periodo anterior. Ex.: 01/07-01/08
 * (31 dias) -> 31/05-01/07 (31 dias antes de 01/07).
 */
export function previousPeriod(from: string, to: string): { from: string; to: string } {
  const fromDate = new Date(`${from}T00:00:00`);
  const toDate = new Date(`${to}T00:00:00`);
  const lengthMs = toDate.getTime() - fromDate.getTime();

  const previousTo = fromDate;
  const previousFrom = new Date(fromDate.getTime() - lengthMs);

  return { from: toDateOnly(previousFrom), to: toDateOnly(previousTo) };
}
