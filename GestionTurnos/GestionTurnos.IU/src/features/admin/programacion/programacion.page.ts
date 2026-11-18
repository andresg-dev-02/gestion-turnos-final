import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { EMPTY, Observable, expand, forkJoin, map, reduce } from 'rxjs';
import { ApiClient, NotificationService } from '../../../app/core';
import { Area, AsignacionTurno, Empleado, PaginaApi, Turno } from '../../../app/shared/data/modelos';
import { OrganizacionApi, ProgramacionApi } from '../../../app/shared/data/apis';
import { UiButtonComponent, UiDialogComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import {
  formatearFecha,
  formatearHora,
  hoyIso,
  inicioSemana,
  sumarDias,
} from '../../../app/shared/utils/formato';
import { AsignarTurnosDialogComponent, PresetAsignacion } from './asignar-turnos.dialog';
import { ReasignarTurnoDialogComponent } from './reasignar-turno.dialog';

interface FilaEmpleado {
  readonly idEmpleado: number;
  readonly nombre: string;
  readonly area: string;
  readonly dias: readonly (readonly AsignacionTurno[])[];
}

const NOMBRES_DIA = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

/** Recorre todas las páginas (la API limita pageSize a 50). */
function todasLasPaginas<T>(pedir: (pagina: number) => Observable<PaginaApi<T>>): Observable<readonly T[]> {
  return pedir(1).pipe(
    expand((r) => (r.pageNumber * r.pageSize < r.totalCount && r.pageNumber < 40 ? pedir(r.pageNumber + 1) : EMPTY)),
    reduce<PaginaApi<T>, T[]>((acc, r) => acc.concat(r.items), [])
  );
}

/**
 * Programación de turnos (/admin/programacion): cuadrícula semanal empleado × día.
 * Clic en una celda vacía → asignar; clic en un turno → reasignar / anular.
 * "Publicar semana" pasa las asignaciones PROGRAMADAS a CONFIRMADAS (visibles al empleado).
 */
@Component({
  selector: 'app-programacion-page',
  imports: [
    UiButtonComponent,
    UiDialogComponent,
    UiSkeletonComponent,
    AsignarTurnosDialogComponent,
    ReasignarTurnoDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-programacion" class="space-y-5">
      <header class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 id="titulo-programacion" class="text-2xl font-extrabold tracking-tight text-slate-900">
            Programación de turnos
          </h1>
          <p class="mt-1 text-sm text-slate-600">
            Asigna, reasigna y publica los turnos de la semana. Los turnos PROGRAMADOS aún no los ve el empleado.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <ui-button variante="secundario" [cargando]="publicando()" (click)="confirmarPublicar.set(true)">
            Publicar semana
          </ui-button>
          <ui-button (click)="abrirAsignar(null)">Asignar turnos</ui-button>
        </div>
      </header>

      <div class="tarjeta flex flex-wrap items-center gap-3">
        <div class="flex items-center gap-1">
          <button type="button" class="accion-secundaria" aria-label="Semana anterior" (click)="moverSemana(-7)">‹</button>
          <button type="button" class="accion-secundaria" (click)="irHoy()">Hoy</button>
          <button type="button" class="accion-secundaria" aria-label="Semana siguiente" (click)="moverSemana(7)">›</button>
        </div>
        <p class="text-sm font-bold text-slate-900">
          {{ formatearFecha(lunes()) }} – {{ formatearFecha(domingo()) }}
        </p>
        <label for="prog-area" class="ml-auto text-sm font-semibold text-slate-700">Área</label>
        <select id="prog-area" class="filtro-select" (change)="cambiarArea($any($event.target).value)">
          <option value="">Todas</option>
          @for (a of areas(); track a.idArea) {
            <option [value]="a.idArea">{{ a.nombre }} · {{ a.nombreSucursal }}</option>
          }
        </select>
        <label class="inline-flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" class="h-4 w-4 accent-marca-600" [checked]="verAnuladas()" (change)="verAnuladas.set(!verAnuladas())" />
          Ver anulados
        </label>
      </div>

      <div class="flex flex-wrap gap-3 text-xs text-slate-700" aria-label="Convenciones">
        <span class="inline-flex items-center gap-1.5"><span class="h-3 w-3 rounded border-2 border-dashed border-slate-500"></span> Programado (sin publicar)</span>
        <span class="inline-flex items-center gap-1.5"><span class="h-3 w-3 rounded bg-slate-500"></span> Confirmado (publicado)</span>
        <span class="inline-flex items-center gap-1.5"><span class="h-3 w-3 rounded bg-slate-200 line-through"></span> Anulado</span>
      </div>

      @if (cargando()) {
        <ui-skeleton [filas]="5" />
      } @else {
        <div class="tabla-contenedor">
          <table class="tabla table-fixed">
            <caption class="sr-only">Turnos por empleado y día de la semana</caption>
            <thead>
              <tr>
                <th scope="col" class="w-48">Empleado</th>
                @for (d of diasSemana(); track d.fecha; let i = $index) {
                  <th scope="col" class="w-32" [class.bg-marca-50]="d.fecha === hoy">
                    {{ nombresDia[i] }} <span class="font-normal">{{ d.fecha.slice(8) }}</span>
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (f of filas(); track f.idEmpleado) {
                <tr>
                  <th scope="row" class="px-4 py-2 text-left align-top">
                    <span class="block text-sm font-semibold text-slate-900">{{ f.nombre }}</span>
                    <span class="block text-xs font-normal text-slate-500">{{ f.area }}</span>
                  </th>
                  @for (celda of f.dias; track $index; let i = $index) {
                    <td class="!p-1.5" [class.bg-marca-50]="diasSemana()[i].fecha === hoy">
                      <div class="flex min-h-14 flex-col gap-1">
                        @for (a of celda; track a.idAsignacion) {
                          <button
                            type="button"
                            (click)="reasignando.set(a)"
                            [attr.aria-label]="'Reasignar ' + a.nombreTurno + ' de ' + f.nombre + ' el ' + formatearFecha(a.fecha)"
                            class="w-full cursor-pointer rounded-lg px-2 py-1 text-left text-xs font-semibold transition hover:ring-2 hover:ring-marca-500"
                            [style.background-color]="a.codEstadoAsignacion === 'CONFIRMADA' ? (a.colorTurno || '#64748b') : 'transparent'"
                            [style.border]="a.codEstadoAsignacion === 'PROGRAMADA' ? '2px dashed ' + (a.colorTurno || '#64748b') : 'none'"
                            [class.text-white]="a.codEstadoAsignacion === 'CONFIRMADA'"
                            [class.text-slate-900]="a.codEstadoAsignacion !== 'CONFIRMADA'"
                            [class.line-through]="a.codEstadoAsignacion === 'ANULADA'"
                            [class.bg-slate-200]="a.codEstadoAsignacion === 'ANULADA'"
                            [style.text-shadow]="a.codEstadoAsignacion === 'CONFIRMADA' ? '0 1px 2px rgb(0 0 0 / .45)' : null"
                          >
                            {{ a.nombreTurno }}
                            <span class="block font-normal">{{ formatearHora(a.periodoInicio) }}–{{ formatearHora(a.periodoFin) }}</span>
                          </button>
                        }
                        <button
                          type="button"
                          (click)="abrirAsignar({ idEmpleado: f.idEmpleado, fecha: diasSemana()[i].fecha })"
                          [attr.aria-label]="'Asignar turno a ' + f.nombre + ' el ' + formatearFecha(diasSemana()[i].fecha)"
                          class="flex min-h-7 w-full cursor-pointer items-center justify-center rounded-lg border border-dashed border-transparent text-slate-400 hover:border-slate-300 hover:text-marca-700"
                        >
                          +
                        </button>
                      </div>
                    </td>
                  }
                </tr>
              } @empty {
                <tr><td colspan="8" class="py-8 text-center text-slate-600">No hay empleados activos en esta área.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>

    <app-asignar-turnos-dialog
      [abierto]="asignarAbierto()"
      [turnos]="turnos()"
      [empleados]="empleadosFiltrados()"
      [preset]="preset()"
      (cerrar)="asignarAbierto.set(false)"
      (guardado)="asignarAbierto.set(false); cargarSemana()"
    />

    <app-reasignar-turno-dialog
      [asignacion]="reasignando()"
      [turnos]="turnos()"
      [empleados]="empleados()"
      (cerrar)="reasignando.set(null)"
      (guardado)="reasignando.set(null); cargarSemana()"
    />

    <ui-dialog
      id="dlg-publicar"
      titulo="Publicar semana"
      [abierto]="confirmarPublicar()"
      (cerrar)="confirmarPublicar.set(false)"
    >
      <p class="text-sm text-slate-700">
        Se confirmarán <strong>{{ programadas() }}</strong> turnos programados entre el
        {{ formatearFecha(lunes()) }} y el {{ formatearFecha(domingo()) }}{{ idArea() ? ' del área seleccionada' : '' }}.
        Los empleados los verán en su calendario y recibirán una notificación.
      </p>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="confirmarPublicar.set(false)">Cancelar</ui-button>
        <ui-button [cargando]="publicando()" [deshabilitado]="programadas() === 0" (click)="publicar()">Publicar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class ProgramacionPageComponent implements OnInit {
  private readonly api = inject(ProgramacionApi);
  private readonly org = inject(OrganizacionApi);
  private readonly toast = inject(NotificationService);

  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly nombresDia = NOMBRES_DIA;
  protected readonly hoy = hoyIso();

  protected readonly lunes = signal(inicioSemana(this.hoy));
  protected readonly domingo = computed(() => sumarDias(this.lunes(), 6));
  protected readonly diasSemana = computed(() =>
    Array.from({ length: 7 }, (_, i) => ({ fecha: sumarDias(this.lunes(), i) }))
  );
  protected readonly idArea = signal<number | null>(null);
  protected readonly verAnuladas = signal(false);

  protected readonly turnos = signal<readonly Turno[]>([]);
  protected readonly areas = signal<readonly Area[]>([]);
  protected readonly empleados = signal<readonly Empleado[]>([]);
  private readonly asignaciones = signal<readonly AsignacionTurno[]>([]);
  protected readonly cargando = signal(true);
  protected readonly publicando = signal(false);
  protected readonly confirmarPublicar = signal(false);

  protected readonly asignarAbierto = signal(false);
  protected readonly preset = signal<PresetAsignacion | null>(null);
  protected readonly reasignando = signal<AsignacionTurno | null>(null);

  protected readonly empleadosFiltrados = computed(() => {
    const area = this.idArea();
    return area ? this.empleados().filter((e) => e.idArea === area) : this.empleados();
  });

  protected readonly programadas = computed(
    () => this.asignaciones().filter((a) => a.codEstadoAsignacion === 'PROGRAMADA').length
  );

  /** Filas: empleados activos del área + quien tenga turnos en la semana aunque no esté en la lista. */
  protected readonly filas = computed<readonly FilaEmpleado[]>(() => {
    const dias = this.diasSemana().map((d) => d.fecha);
    const visibles = this.asignaciones().filter(
      (a) => this.verAnuladas() || a.codEstadoAsignacion !== 'ANULADA'
    );
    const base = new Map<number, { nombre: string; area: string }>();
    for (const e of this.empleadosFiltrados()) {
      base.set(e.idEmpleado, { nombre: `${e.nombres} ${e.apellidos}`, area: e.nombreArea });
    }
    for (const a of visibles) {
      if (!base.has(a.idEmpleado)) base.set(a.idEmpleado, { nombre: a.nombreEmpleado, area: a.nombreArea });
    }
    return [...base.entries()]
      .map(([idEmpleado, info]) => ({
        idEmpleado,
        nombre: info.nombre,
        area: info.area,
        dias: dias.map((fecha) =>
          visibles
            .filter((a) => a.idEmpleado === idEmpleado && a.fecha === fecha)
            .sort((x, y) => x.periodoInicio.localeCompare(y.periodoInicio))
        ),
      }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  });

  ngOnInit(): void {
    forkJoin({
      turnos: this.api.listarTurnos(),
      areas: this.org.listarAreas(),
      empleados: todasLasPaginas((p) => this.org.listarEmpleados({ pageNumber: p, pageSize: 50 })),
    }).subscribe({
      next: ({ turnos, areas, empleados }) => {
        this.turnos.set(turnos);
        this.areas.set(areas);
        this.empleados.set(empleados);
        this.cargarSemana();
      },
      error: (err: unknown) => {
        this.toast.error(ApiClient.extraerMensajeError(err).general, 'No se cargaron los catálogos');
        this.cargarSemana();
      },
    });
  }

  cargarSemana(): void {
    this.cargando.set(true);
    const area = this.idArea();
    todasLasPaginas((p) =>
      this.api.listarAsignaciones({
        fechaDesde: this.lunes(),
        fechaHasta: this.domingo(),
        ...(area ? { idArea: area } : {}),
        pageNumber: p,
        pageSize: 50,
      })
    )
      .pipe(map((l) => [...l]))
      .subscribe({
        next: (l) => {
          this.asignaciones.set(l);
          this.cargando.set(false);
        },
        error: (err: unknown) => {
          this.cargando.set(false);
          this.toast.error(ApiClient.extraerMensajeError(err).general);
        },
      });
  }

  moverSemana(dias: number): void {
    this.lunes.set(sumarDias(this.lunes(), dias));
    this.cargarSemana();
  }

  irHoy(): void {
    this.lunes.set(inicioSemana(this.hoy));
    this.cargarSemana();
  }

  cambiarArea(valor: string): void {
    this.idArea.set(valor ? Number(valor) : null);
    this.cargarSemana();
  }

  abrirAsignar(p: PresetAsignacion | null): void {
    this.preset.set(p ?? { idEmpleado: null, fecha: '' });
    this.asignarAbierto.set(true);
  }

  publicar(): void {
    this.publicando.set(true);
    this.api.publicar(this.lunes(), this.domingo(), this.idArea()).subscribe({
      next: (n) => {
        this.publicando.set(false);
        this.confirmarPublicar.set(false);
        this.toast.exito(`${typeof n === 'number' ? n : this.programadas()} turno(s) publicados.`);
        this.cargarSemana();
      },
      error: (err: unknown) => {
        this.publicando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
