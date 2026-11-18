/**
 * Utilidades de formato y fechas en la zona horaria de la empresa (America/Bogota).
 * Las fechas "DateOnly" de la API llegan como 'YYYY-MM-DD' y nunca se convierten
 * con new Date(texto) para evitar el corrimiento de un día por UTC.
 */
const ZONA = 'America/Bogota';

const fmtFechaLarga = new Intl.DateTimeFormat('es-CO', {
  weekday: 'short',
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
});

const fmtFechaHora = new Intl.DateTimeFormat('es-CO', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: ZONA,
});

const fmtHora = new Intl.DateTimeFormat('es-CO', {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: ZONA,
});

/** 'YYYY-MM-DD' → 'lun., 05 ene. 2026' */
export function formatearFecha(fecha: string | null | undefined): string {
  if (!fecha) return '—';
  const [a, m, d] = fecha.slice(0, 10).split('-').map(Number);
  return fmtFechaLarga.format(new Date(Date.UTC(a, m - 1, d)));
}

/** ISO con zona → '05 ene. 2026, 14:30' en hora de Bogotá */
export function formatearFechaHora(valor: string | null | undefined): string {
  if (!valor) return '—';
  return fmtFechaHora.format(new Date(valor));
}

/** ISO con zona → '14:30' en hora de Bogotá */
export function formatearHora(valor: string | null | undefined): string {
  if (!valor) return '—';
  return fmtHora.format(new Date(valor));
}

/** TimeOnly 'HH:mm:ss' → 'HH:mm' */
export function horaCorta(valor: string | null | undefined): string {
  return valor ? valor.slice(0, 5) : '—';
}

/** Fecha de hoy en Bogotá como 'YYYY-MM-DD' */
export function hoyIso(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: ZONA }).format(new Date());
}

/** ISO con zona → 'YYYY-MM-DD' del día correspondiente en Bogotá */
export function fechaBogota(valor: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: ZONA }).format(new Date(valor));
}

/** Suma días a una fecha 'YYYY-MM-DD' */
export function sumarDias(fecha: string, dias: number): string {
  const [a, m, d] = fecha.split('-').map(Number);
  const dt = new Date(Date.UTC(a, m - 1, d + dias));
  return dt.toISOString().slice(0, 10);
}

/** Día de la semana (0 = domingo) de una fecha 'YYYY-MM-DD' */
export function diaSemana(fecha: string): number {
  const [a, m, d] = fecha.split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d)).getUTCDay();
}

/** Lunes de la semana que contiene la fecha */
export function inicioSemana(fecha: string): string {
  const dia = diaSemana(fecha);
  return sumarDias(fecha, dia === 0 ? -6 : 1 - dia);
}

/**
 * 'YYYY-MM-DDTHH:mm' (input datetime-local, hora de Bogotá) → ISO con offset -05:00.
 * Colombia no tiene horario de verano, el desfase es fijo.
 */
export function localBogotaAIso(valor: string): string {
  return `${valor.length === 16 ? valor + ':00' : valor}-05:00`;
}

/** Inicio del día en Bogotá como ISO con offset, para rangos DateTimeOffset */
export function inicioDiaIso(fecha: string): string {
  return `${fecha}T00:00:00-05:00`;
}

export function minutosAHoras(minutos: number | null | undefined): string {
  if (minutos === null || minutos === undefined) return '—';
  const signo = minutos < 0 ? '-' : '';
  const abs = Math.abs(minutos);
  return `${signo}${Math.floor(abs / 60)} h ${String(abs % 60).padStart(2, '0')} min`;
}
