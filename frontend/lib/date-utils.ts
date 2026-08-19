export function toDateOnly(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

export function startOfYear(date: Date): Date {
  return new Date(date.getFullYear(), 0, 1);
}

export function startOfPreviousMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth() - 1, 1);
}

export function endOfPreviousMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 0);
}

/** Segunda-feira da semana de `date` — convencao de semana comercial usada nos filtros de periodo. */
export function startOfWeek(date: Date): Date {
  const dayOfWeek = date.getDay();
  const diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  const monday = new Date(date);
  monday.setDate(date.getDate() + diffToMonday);
  return monday;
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
