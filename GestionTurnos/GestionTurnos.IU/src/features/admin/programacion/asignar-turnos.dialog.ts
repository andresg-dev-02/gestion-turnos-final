import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../app/core';
import { Empleado, ItemProgramacion, RevisionConflictosTurno, Turno } from '../../../app/shared/data/modelos';
import { ProgramacionApi } from '../../../app/shared/data/apis';
import {
  OpcionSelect,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
} from '../../../app/shared/ui';
import { diaSemana, formatearFecha, horaCorta, sumarDias } from '../../../app/shared/utils/formato';

export interface PresetAsignacion {
  readonly idEmpleado: number | null;
  readonly fecha: string;
}

const DIAS = [
  { n: 1, etiqueta: 'Lun' },
  { n: 2, etiqueta: 'Mar' },
  { n: 3, etiqueta: 'Mié' },
  { n: 4, etiqueta: 'Jue' },
  { n: 5, etiqueta: 'Vie' },
  { n: 6, etiqueta: 'Sáb' },
  { n: 0, etiqueta: 'Dom' },
] as const;

const MAX_ITEMS = 500;

/**
 * Asignación de turnos (sección 6.1): uno o varios empleados, un turno, un rango de
 * fechas y los días de la semana. La API inserta todo en una transacción: si una
 * asignación tiene un conflicto bloqueante no se guarda ninguna y se informa cuál falló.
 * Para una sola asignación se revisan los conflictos antes de guardar.
 */
@Component({
  selector: 'app-asignar-turnos-dialog',
  imports: [UiButtonComponent, UiDialogComponent, UiNotchedFieldComponent, UiNotchedSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-asignar"
      ancho="lg"
      titulo="Asignar turnos"
      subtitulo="Las asignaciones quedan PROGRAMADAS hasta que publiques la semana."
      [abierto]="abierto()"
      (cerrar)="cerrar.emit()"
    >
      <div class="space-y-5">
        <ui-notched-select
          id="asg-turno"
          etiqueta="Turno"
          [opciones]="opcionesTurno()"
          [valor]="idTurno()"
          [obligatorio]="true"
          [error]="errores()['turno']"
          (valorCambio)="idTurno.set($event); revision.set(null)"
        />

        <div class="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <ui-notched-field id="asg-desde" etiqueta="Desde" tipo="date" [valor]="desde()" [obligatorio]="true"
            (valorCambio)="desde.set($event); revision.set(null)" />
          <ui-notched-field id="asg-hasta" etiqueta="Hasta" tipo="date" [valor]="hasta()" [obligatorio]="true"
            [error]="errores()['fechas']" (valorCambio)="hasta.set($event); revision.set(null)" />
        </div>

        @if (desde() !== hasta()) {
          <fieldset>
            <legend class="mb-2 text-xs font-semibold text-slate-700">Días de la semana</legend>
            <div class="flex flex-wrap gap-2">
              @for (d of dias; track d.n) {
                <label class="inline-flex min-h-10 cursor-pointer items-center gap-2 rounded-lg border border-slate-300 px-3 text-sm"
                  [class.border-marca-600]="diasMarcados().has(d.n)" [class.bg-marca-50]="diasMarcados().has(d.n)">
                  <input type="checkbox" class="h-4 w-4 accent-marca-600" [checked]="diasMarcados().has(d.n)" (change)="alternarDia(d.n)" />
                  {{ d.etiqueta }}
                </label>
              }
            </div>
          </fieldset>
        }

        <fieldset>
          <legend class="mb-2 flex w-full items-center justify-between text-xs font-semibold text-slate-700">
            <span>Empleados ({{ seleccionados().size }} seleccionados)</span>
            <span class="flex gap-3">
              <button type="button" class="cursor-pointer font-bold text-marca-700 underline" (click)="todos(true)">Todos</button>
              <button type="button" class="cursor-pointer font-bold text-marca-700 underline" (click)="todos(false)">Ninguno</button>
            </span>
          </legend>
          <input type="search" placeholder="Filtrar por nombre" aria-label="Filtrar empleados" class="filtro-select mb-2 w-full"
            (input)="filtro.set($any($event.target).value)" />
          <div class="max-h-48 overflow-y-auto rounded-xl border border-slate-200">
            @for (e of empleadosFiltrados(); track e.idEmpleado) {
              <label class="flex min-h-10 cursor-pointer items-center gap-3 border-b border-slate-100 px-3 text-sm last:border-0 hover:bg-slate-50">
                <input type="checkbox" class="h-4 w-4 accent-marca-600" [checked]="seleccionados().has(e.idEmpleado)" (change)="alternarEmpleado(e.idEmpleado)" />
                <span class="font-medium">{{ e.nombres }} {{ e.apellidos }}</span>
                <span class="ml-auto text-xs text-slate-500">{{ e.nombreArea }}</span>
              </label>
            } @empty {
              <p class="p-3 text-sm text-slate-600">No hay empleados activos.</p>
            }
          </div>
          @if (errores()['empleados']) {
            <p role="alert" class="mt-1.5 text-xs font-medium text-peligro">{{ errores()['empleados'] }}</p>
          }
        </fieldset>

        <ui-notched-field id="asg-obs" etiqueta="Observación (opcional)" [valor]="observacion()" (valorCambio)="observacion.set($event)" />

        <p class="aviso-info">
          Se crearán <strong>{{ items().length }}</strong> asignaciones.
          @if (items().length > maxItems) { <strong class="text-peligro">Máximo {{ maxItems }} por operación.</strong> }
        </p>

        @if (revision(); as r) {
          @if (r.conflictos.length) {
            <ul [class]="r.puedeAsignarse ? 'aviso-alerta space-y-1' : 'aviso-error space-y-1'">
              @for (c of r.conflictos; track $index) {
                <li><strong>{{ c.bloquea ? 'Bloquea' : 'Advertencia' }}:</strong> {{ c.mensaje }}</li>
              }
            </ul>
          } @else {
            <p class="aviso-info">Sin conflictos: el turno puede asignarse.</p>
          }
        }
      </div>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="cerrar.emit()">Cancelar</ui-button>
        @if (items().length === 1) {
          <ui-button variante="secundario" [cargando]="revisando()" (click)="revisar()">Revisar conflictos</ui-button>
        }
        <ui-button [cargando]="guardando()" [deshabilitado]="revision()?.puedeAsignarse === false" (click)="guardar()">
          Asignar
        </ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class AsignarTurnosDialogComponent {
  readonly abierto = input(false);
  readonly turnos = input.required<readonly Turno[]>();
  readonly empleados = input.required<readonly Empleado[]>();
  readonly preset = input<PresetAsignacion | null>(null);
  readonly cerrar = output<void>();
  readonly guardado = output<void>();

  private readonly api = inject(ProgramacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly dias = DIAS;
  protected readonly maxItems = MAX_ITEMS;
  protected readonly idTurno = signal('');
  protected readonly desde = signal('');
  protected readonly hasta = signal('');
  protected readonly diasMarcados = signal(new Set<number>([1, 2, 3, 4, 5]));
  protected readonly seleccionados = signal(new Set<number>());
  protected readonly filtro = signal('');
  protected readonly observacion = signal('');
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly revision = signal<RevisionConflictosTurno | null>(null);
  protected readonly revisando = signal(false);
  protected readonly guardando = signal(false);

  protected readonly opcionesTurno = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Seleccione un turno' },
    ...this.turnos().map((t) => ({
      valor: String(t.idTurno),
      etiqueta: `${t.nombre} (${horaCorta(t.horaInicio)}–${horaCorta(t.horaFin)})`,
    })),
  ]);

  protected readonly empleadosFiltrados = computed(() => {
    const f = this.filtro().trim().toLowerCase();
    return f
      ? this.empleados().filter((e) => `${e.nombres} ${e.apellidos}`.toLowerCase().includes(f))
      : this.empleados();
  });

  /** Fechas resultantes del rango y los días de la semana marcados. */
  private readonly fechas = computed(() => {
    const d = this.desde();
    const h = this.hasta();
    if (!d || !h || h < d) return [];
    if (d === h) return [d];
    const lista: string[] = [];
    for (let f = d; f <= h && lista.length <= 366; f = sumarDias(f, 1)) {
      if (this.diasMarcados().has(diaSemana(f))) lista.push(f);
    }
    return lista;
  });

  protected readonly items = computed<readonly ItemProgramacion[]>(() => {
    const idTurno = Number(this.idTurno());
    if (!idTurno) return [];
    const obs = this.observacion().trim() || null;
    return [...this.seleccionados()].flatMap((idEmpleado) =>
      this.fechas().map((fecha) => ({ idEmpleado, idTurno, fecha, observacion: obs }))
    );
  });

  constructor() {
    // Al abrir, aplica el preset (celda de la cuadrícula o botón general).
    effect(() => {
      if (!this.abierto()) return;
      const p = this.preset();
      this.idTurno.set('');
      this.desde.set(p?.fecha ?? '');
      this.hasta.set(p?.fecha ?? '');
      this.seleccionados.set(new Set(p?.idEmpleado ? [p.idEmpleado] : []));
      this.observacion.set('');
      this.filtro.set('');
      this.errores.set({});
      this.revision.set(null);
    });
  }

  alternarDia(n: number): void {
    this.diasMarcados.update((s) => {
      const c = new Set(s);
      if (c.has(n)) c.delete(n);
      else c.add(n);
      return c;
    });
    this.revision.set(null);
  }

  alternarEmpleado(id: number): void {
    this.seleccionados.update((s) => {
      const c = new Set(s);
      if (c.has(id)) c.delete(id);
      else c.add(id);
      return c;
    });
    this.revision.set(null);
  }

  todos(marcar: boolean): void {
    this.seleccionados.set(new Set(marcar ? this.empleadosFiltrados().map((e) => e.idEmpleado) : []));
    this.revision.set(null);
  }

  private validar(): boolean {
    const errores: Record<string, string> = {};
    if (!this.idTurno()) errores['turno'] = 'Seleccione el turno.';
    if (!this.desde() || !this.hasta()) errores['fechas'] = 'Indique el rango de fechas.';
    else if (this.hasta() < this.desde()) errores['fechas'] = 'La fecha final debe ser igual o posterior a la inicial.';
    else if (this.fechas().length === 0) errores['fechas'] = 'Ningún día del rango coincide con los días marcados.';
    if (this.seleccionados().size === 0) errores['empleados'] = 'Seleccione al menos un empleado.';
    this.errores.set(errores);
    return Object.keys(errores).length === 0;
  }

  revisar(): void {
    if (!this.validar() || this.items().length !== 1) return;
    const it = this.items()[0];
    this.revisando.set(true);
    this.api.revisarConflictos(it.idEmpleado, it.idTurno, it.fecha).subscribe({
      next: (r) => {
        this.revision.set(r);
        this.revisando.set(false);
      },
      error: (err: unknown) => {
        this.revisando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  guardar(): void {
    if (!this.validar() || this.guardando()) return;
    const items = this.items();
    if (items.length > MAX_ITEMS) {
      this.toast.alerta(`Divida la programación: máximo ${MAX_ITEMS} asignaciones por operación.`);
      return;
    }
    this.guardando.set(true);
    this.api.programar(items).subscribe({
      next: (ids) => {
        this.guardando.set(false);
        this.toast.exito(`${ids.length} turno(s) asignado(s) del ${formatearFecha(this.desde())} al ${formatearFecha(this.hasta())}.`);
        this.guardado.emit();
      },
      error: (err: unknown) => {
        this.guardando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general, 'No se asignó ningún turno');
      },
    });
  }
}
