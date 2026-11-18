import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type EstadoInsignia =
  | 'PENDIENTE'
  | 'APROBADA'
  | 'RECHAZADA'
  | 'CANCELADA'
  | 'PROGRAMADA'
  | 'CONFIRMADA'
  | 'ANULADA'
  | 'ACTIVO'
  | 'INACTIVO'
  | 'SUSPENDIDO'
  | 'BLOQUEADO';

const ESTILOS_ESTADO: Record<EstadoInsignia, string> = {
  PENDIENTE: 'bg-amber-100 text-amber-900 border-amber-300',
  APROBADA: 'bg-emerald-100 text-emerald-900 border-emerald-300',
  CONFIRMADA: 'bg-emerald-100 text-emerald-900 border-emerald-300',
  ACTIVO: 'bg-emerald-100 text-emerald-900 border-emerald-300',
  PROGRAMADA: 'bg-indigo-100 text-indigo-900 border-indigo-300',
  RECHAZADA: 'bg-red-100 text-red-900 border-red-300',
  ANULADA: 'bg-red-100 text-red-900 border-red-300',
  CANCELADA: 'bg-slate-200 text-slate-800 border-slate-300',
  INACTIVO: 'bg-slate-200 text-slate-800 border-slate-300',
  SUSPENDIDO: 'bg-amber-100 text-amber-900 border-amber-300',
  BLOQUEADO: 'bg-red-100 text-red-900 border-red-300',
};

@Component({
  selector: 'ui-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="clases()">
      <span class="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true"></span>
      {{ estado() }}
    </span>
  `,
})
export class UiBadgeComponent {
  readonly estado = input.required<EstadoInsignia | string>();

  readonly clases = computed(
    () =>
      `inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold ${
        ESTILOS_ESTADO[this.estado() as EstadoInsignia] ?? ESTILOS_ESTADO.CANCELADA
      }`
  );
}
