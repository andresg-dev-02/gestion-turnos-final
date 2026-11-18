import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../app/core';
import { AsignacionTurno, ConflictoTurno, Empleado, Turno } from '../../../app/shared/data/modelos';
import { ProgramacionApi } from '../../../app/shared/data/apis';
import {
  OpcionSelect,
  UiBadgeComponent,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
  UiNotchedTextareaComponent,
  UiTabsComponent,
} from '../../../app/shared/ui';
import { formatearFecha, formatearHora, horaCorta } from '../../../app/shared/utils/formato';

type Modo = 'modificar' | 'empleado' | 'anular';

/**
 * Reasignación de una asignación existente (sección 9.4):
 *  - modificar: otro turno y/o fecha para el mismo empleado (PUT, con vista previa de conflictos);
 *  - empleado: la crea para otro empleado y anula la original (dos pasos; si el primero
 *    falla no se toca nada);
 *  - anular: con observación obligatoria.
 */
@Component({
  selector: 'app-reasignar-turno-dialog',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedSelectComponent,
    UiNotchedTextareaComponent,
    UiTabsComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-reasignar"
      ancho="lg"
      titulo="Reasignar turno"
      [subtitulo]="subtitulo()"
      [abierto]="asignacion() !== null"
      (cerrar)="cerrar.emit()"
    >
      @if (asignacion(); as a) {
        <div class="mb-4 flex items-center gap-2 text-sm">
          Estado actual: <ui-badge [estado]="a.codEstadoAsignacion" />
          @if (a.observacion) { <span class="text-slate-600">· {{ a.observacion }}</span> }
        </div>

        <ui-tabs etiqueta="Tipo de reasignación" [pestanas]="pestanas" [activa]="modo()" (cambio)="cambiarModo($event)" />

        <div class="mt-5 space-y-5">
          @switch (modo()) {
            @case ('modificar') {
              <ui-notched-select id="rea-turno" etiqueta="Nuevo turno" [opciones]="opcionesTurno()" [valor]="idTurno()"
                (valorCambio)="idTurno.set($event); conflictos.set(null)" />
              <ui-notched-field id="rea-fecha" etiqueta="Nueva fecha" tipo="date" [valor]="fecha()"
                (valorCambio)="fecha.set($event); conflictos.set(null)" />
            }
            @case ('empleado') {
              <ui-notched-select id="rea-empleado" etiqueta="Asignar a" [opciones]="opcionesEmpleado()" [valor]="idEmpleado()"
                [error]="errores()['empleado']" (valorCambio)="idEmpleado.set($event); conflictos.set(null)" />
              <p class="aviso-info">Se crea el mismo turno para el empleado elegido y se anula el de {{ a.nombreEmpleado }}.</p>
            }
            @case ('anular') {
              <p class="aviso-alerta">El turno quedará ANULADO y el empleado dejará de verlo en su calendario.</p>
            }
          }

          <ui-notched-textarea
            id="rea-obs"
            [etiqueta]="modo() === 'modificar' ? 'Observación (opcional)' : 'Motivo (obligatorio)'"
            [obligatorio]="modo() !== 'modificar'"
            [valor]="observacion()"
            [error]="errores()['observacion']"
            (valorCambio)="observacion.set($event)"
          />

          @if (conflictos(); as cs) {
            @if (cs.length) {
              <ul [class]="bloquea() ? 'aviso-error space-y-1' : 'aviso-alerta space-y-1'">
                @for (c of cs; track $index) {
                  <li><strong>{{ c.bloquea ? 'Bloquea' : 'Advertencia' }}:</strong> {{ c.mensaje }}</li>
                }
              </ul>
            } @else {
              <p class="aviso-info">Sin conflictos.</p>
            }
          }
        </div>
      }
      <ng-container acciones>
        <ui-button variante="secundario" (click)="cerrar.emit()">Cancelar</ui-button>
        @if (modo() !== 'anular') {
          <ui-button variante="secundario" [cargando]="revisando()" (click)="revisar()">Revisar conflictos</ui-button>
        }
        <ui-button
          [variante]="modo() === 'anular' ? 'peligro' : 'primario'"
          [cargando]="guardando()"
          [deshabilitado]="bloquea()"
          (click)="guardar()"
        >
          {{ modo() === 'anular' ? 'Anular turno' : 'Guardar reasignación' }}
        </ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class ReasignarTurnoDialogComponent {
  readonly asignacion = input<AsignacionTurno | null>(null);
  readonly turnos = input.required<readonly Turno[]>();
  readonly empleados = input.required<readonly Empleado[]>();
  readonly cerrar = output<void>();
  readonly guardado = output<void>();

  private readonly api = inject(ProgramacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly pestanas = [
    { id: 'modificar', etiqueta: 'Cambiar turno o fecha' },
    { id: 'empleado', etiqueta: 'Pasar a otro empleado' },
    { id: 'anular', etiqueta: 'Anular' },
  ];
  protected readonly modo = signal<Modo>('modificar');
  protected readonly idTurno = signal('');
  protected readonly fecha = signal('');
  protected readonly idEmpleado = signal('');
  protected readonly observacion = signal('');
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly conflictos = signal<readonly ConflictoTurno[] | null>(null);
  protected readonly revisando = signal(false);
  protected readonly guardando = signal(false);

  protected readonly bloquea = computed(() => (this.conflictos() ?? []).some((c) => c.bloquea));
  protected readonly subtitulo = computed(() => {
    const a = this.asignacion();
    return a
      ? `${a.nombreEmpleado} · ${a.nombreTurno} · ${formatearFecha(a.fecha)} (${formatearHora(a.periodoInicio)}–${formatearHora(a.periodoFin)})`
      : '';
  });
  protected readonly opcionesTurno = computed<readonly OpcionSelect[]>(() =>
    this.turnos().map((t) => ({
      valor: String(t.idTurno),
      etiqueta: `${t.nombre} (${horaCorta(t.horaInicio)}–${horaCorta(t.horaFin)})`,
    }))
  );
  protected readonly opcionesEmpleado = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Seleccione un empleado' },
    ...this.empleados()
      .filter((e) => e.idEmpleado !== this.asignacion()?.idEmpleado)
      .map((e) => ({ valor: String(e.idEmpleado), etiqueta: `${e.nombres} ${e.apellidos} · ${e.nombreArea}` })),
  ]);

  constructor() {
    effect(() => {
      const a = this.asignacion();
      if (!a) return;
      this.modo.set('modificar');
      this.idTurno.set(String(a.idTurno));
      this.fecha.set(a.fecha);
      this.idEmpleado.set('');
      this.observacion.set('');
      this.errores.set({});
      this.conflictos.set(null);
    });
  }

  cambiarModo(m: string): void {
    this.modo.set(m as Modo);
    this.errores.set({});
    this.conflictos.set(null);
  }

  revisar(): void {
    const a = this.asignacion();
    if (!a) return;
    this.revisando.set(true);
    const fin = (cs: readonly ConflictoTurno[]) => {
      this.conflictos.set(cs);
      this.revisando.set(false);
    };
    const error = (err: unknown) => {
      this.revisando.set(false);
      this.toast.error(ApiClient.extraerMensajeError(err).general);
    };
    if (this.modo() === 'modificar') {
      this.api
        .previsualizarModificacion(a.idAsignacion, Number(this.idTurno()), this.fecha())
        .subscribe({ next: (p) => fin(p.conflictos), error });
    } else if (this.idEmpleado()) {
      this.api
        .revisarConflictos(Number(this.idEmpleado()), a.idTurno, a.fecha)
        .subscribe({ next: (r) => fin(r.conflictos), error });
    } else {
      this.revisando.set(false);
      this.errores.set({ empleado: 'Seleccione el empleado.' });
    }
  }

  guardar(): void {
    const a = this.asignacion();
    if (!a || this.guardando()) return;
    const obs = this.observacion().trim();
    const errores: Record<string, string> = {};
    if (this.modo() !== 'modificar' && obs.length < 5) errores['observacion'] = 'Indique el motivo (mínimo 5 caracteres).';
    if (this.modo() === 'empleado' && !this.idEmpleado()) errores['empleado'] = 'Seleccione el empleado.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    this.guardando.set(true);
    const ok = (msg: string) => {
      this.guardando.set(false);
      this.toast.exito(msg);
      this.guardado.emit();
    };
    const error = (err: unknown, titulo?: string) => {
      this.guardando.set(false);
      this.toast.error(ApiClient.extraerMensajeError(err).general, titulo);
    };

    switch (this.modo()) {
      case 'modificar':
        if (Number(this.idTurno()) === a.idTurno && this.fecha() === a.fecha) {
          this.guardando.set(false);
          this.toast.alerta('No cambió el turno ni la fecha.');
          return;
        }
        this.api.modificar(a.idAsignacion, Number(this.idTurno()), this.fecha(), obs || null).subscribe({
          next: () => ok('Turno reasignado.'),
          error: (e: unknown) => error(e),
        });
        break;
      case 'anular':
        this.api.anular(a.idAsignacion, obs).subscribe({
          next: () => ok('Turno anulado.'),
          error: (e: unknown) => error(e),
        });
        break;
      case 'empleado': {
        const destino = this.empleados().find((e) => e.idEmpleado === Number(this.idEmpleado()));
        const nombreDestino = destino ? `${destino.nombres} ${destino.apellidos}` : 'otro empleado';
        // Paso 1: crear para el nuevo empleado (si falla, la original queda intacta).
        this.api
          .programar([{ idEmpleado: Number(this.idEmpleado()), idTurno: a.idTurno, fecha: a.fecha, observacion: obs }])
          .subscribe({
            next: () =>
              // Paso 2: anular la original.
              this.api.anular(a.idAsignacion, `Reasignado a ${nombreDestino}: ${obs}`.slice(0, 300)).subscribe({
                next: () => ok(`Turno reasignado a ${nombreDestino}.`),
                error: (e: unknown) =>
                  error(e, `Se creó el turno para ${nombreDestino}, pero no se pudo anular el original. Anúlelo manualmente`),
              }),
            error: (e: unknown) => error(e, 'No se reasignó el turno'),
          });
        break;
      }
    }
  }
}
