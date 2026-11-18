import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PanelAsistenciaTurno } from '../../../../app/shared/data';
import {
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedTextareaComponent,
} from '../../../../app/shared/ui';
import { formatearHora } from '../../../../app/shared/utils/formato';

export interface GuardarAsistenciaManualEvento {
  readonly idRegistro?: number | null;
  readonly idEmpleado: number;
  readonly idAsignacion: number;
  readonly horaEntrada: string;
  readonly horaSalida: string | null;
  readonly observacion: string;
  readonly esCorreccion: boolean;
}

@Component({
  selector: 'app-asistencia-manual-dialog',
  imports: [
    FormsModule,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedTextareaComponent,
    UiButtonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-asistencia-manual"
      [abierto]="abierto()"
      [titulo]="esCorreccion() ? 'Corregir Marcaje de Asistencia' : 'Registrar Asistencia Manual'"
      [subtitulo]="subtitulo()"
      (cerrar)="cerrar.emit()"
    >
      <form (submit)="enviar($event)" class="space-y-4">
        <div class="rounded-lg bg-slate-50 p-3 text-xs text-slate-600 space-y-1">
          <div class="flex items-center justify-between">
            <p class="font-semibold text-slate-800">
              {{ turno()?.nombreEmpleado }} · {{ turno()?.nombreArea }}
            </p>
            @if (esCorreccion()) {
              <span class="rounded bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-800">
                Corrección
              </span>
            } @else {
              <span class="rounded bg-blue-100 px-2 py-0.5 text-xs font-bold text-blue-800">
                Nuevo marcaje
              </span>
            }
          </div>
          <p>
            Turno: <span class="font-medium text-slate-900">{{ turno()?.nombreTurno }}</span>
          </p>
          @if (esCorreccion()) {
            <p class="text-slate-500 pt-1 border-t border-slate-200">
              Marcaje actual registrado:
              <span class="font-mono font-medium text-slate-800">
                Entrada: {{ turno()?.horaEntrada ? formatearHora(turno()!.horaEntrada!) : 'Sin entrada' }}
                &bull;
                Salida: {{ turno()?.horaSalida ? formatearHora(turno()!.horaSalida!) : 'En curso' }}
              </span>
            </p>
          }
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-field
            id="asist-manual-entrada"
            tipo="datetime-local"
            etiqueta="Hora de entrada"
            [valor]="formEntrada()"
            [obligatorio]="true"
            (valorCambio)="formEntrada.set($event)"
          />
          <ui-notched-field
            id="asist-manual-salida"
            tipo="datetime-local"
            etiqueta="Hora de salida"
            ayuda="Opcional si la jornada continúa en curso"
            [valor]="formSalida()"
            (valorCambio)="formSalida.set($event)"
          />
        </div>

        <div>
          <ui-notched-textarea
            id="asist-manual-obs"
            etiqueta="Observación obligatoria"
            [valor]="formObservacion()"
            [obligatorio]="true"
            (valorCambio)="formObservacion.set($event)"
          />
          <p class="mt-1 text-xs text-slate-500">
            {{ esCorreccion() ? 'Explica el motivo de la corrección del marcaje (auditoría).' : 'Indica el motivo del registro manual de asistencia (auditoría).' }}
          </p>
        </div>

        <div class="mt-6 flex justify-end gap-3 pt-3">
          <button
            type="button"
            class="accion-secundaria"
            (click)="cerrar.emit()"
          >
            Cancelar
          </button>
          <ui-button
            tipo="submit"
            [deshabilitado]="guardando() || !formularioValido()"
          >
            {{ guardando() ? 'Guardando...' : (esCorreccion() ? 'Guardar corrección' : 'Registrar marcaje') }}
          </ui-button>
        </div>
      </form>
    </ui-dialog>
  `,
})
export class AsistenciaManualDialogComponent {
  readonly abierto = input.required<boolean>();
  readonly turno = input<PanelAsistenciaTurno | null>(null);
  readonly guardando = input<boolean>(false);

  readonly guardar = output<GuardarAsistenciaManualEvento>();
  readonly cerrar = output<void>();

  protected readonly formatearHora = formatearHora;

  protected readonly formEntrada = signal('');
  protected readonly formSalida = signal('');
  protected readonly formObservacion = signal('');

  protected readonly esCorreccion = computed(() =>
    Boolean(this.turno()?.idRegistro || this.turno()?.horaEntrada)
  );

  constructor() {
    effect(() => {
      const t = this.turno();
      if (t) {
        this.formEntrada.set(this.aFormatoInput(t.horaEntrada || t.programadoInicio));
        this.formSalida.set(this.aFormatoInput(t.horaSalida));
        this.formObservacion.set('');
      } else {
        this.formEntrada.set('');
        this.formSalida.set('');
        this.formObservacion.set('');
      }
    });
  }

  protected subtitulo(): string {
    const t = this.turno();
    if (!t) return 'Supervisión de marcajes y registro manual.';
    return this.esCorreccion()
      ? `Ajuste del marcaje registrado para ${t.nombreEmpleado} (${t.nombreTurno})`
      : `Registro manual de asistencia para ${t.nombreEmpleado} (${t.nombreTurno})`;
  }

  protected formularioValido(): boolean {
    return Boolean(
      this.formEntrada().trim() &&
      this.formObservacion().trim().length >= 5
    );
  }

  protected enviar(event: Event): void {
    event.preventDefault();
    const t = this.turno();
    if (!t || !this.formularioValido()) return;

    this.guardar.emit({
      idRegistro: t.idRegistro,
      idEmpleado: t.idEmpleado,
      idAsignacion: t.idAsignacion,
      horaEntrada: new Date(this.formEntrada()).toISOString(),
      horaSalida: this.formSalida() ? new Date(this.formSalida()).toISOString() : null,
      observacion: this.formObservacion().trim(),
      esCorreccion: this.esCorreccion(),
    });
  }

  private aFormatoInput(fechaIso: string | null): string {
    if (!fechaIso) return '';
    const d = new Date(fechaIso);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
