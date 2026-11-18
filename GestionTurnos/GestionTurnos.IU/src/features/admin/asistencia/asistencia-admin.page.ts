import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Area, AsistenciaAdminApi, OrganizacionApi, PanelAsistenciaDia, PanelAsistenciaTurno} from '../../../app/shared/data';
import { AuthStore, NotificationService, PERMISOS } from '../../../app/core';
import { UiButtonComponent, UiIconComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import { formatearFecha, formatearHora, hoyIso, sumarDias } from '../../../app/shared/utils/formato';
import {AsistenciaManualDialogComponent,GuardarAsistenciaManualEvento } from './components/asistencia-manual-dialog.component';

@Component({
  selector: 'app-asistencia-admin-page',
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiIconComponent,
    UiSkeletonComponent,
    AsistenciaManualDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-asistencia-admin" class="space-y-6">
      <header class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1
            id="titulo-asistencia-admin"
            class="text-2xl font-extrabold tracking-tight text-slate-900"
          >
            Panel de asistencia del día
          </h1>
          <p class="mt-1 text-sm text-slate-600">
            Supervisión de marcajes en tiempo real, tardanzas y registro manual con observación obligatoria.
          </p>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <div class="flex items-center rounded-xl border border-slate-200 bg-white p-1 shadow-xs">
            <button
              type="button"
              class="rounded-lg px-2.5 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-100"
              (click)="navegarFecha(-1)"
              title="Día anterior"
            >
              &larr; Ayer
            </button>
            <button
              type="button"
              class="rounded-lg px-3 py-1.5 text-xs font-bold text-marca-700 transition hover:bg-marca-50"
              (click)="irAHoy()"
              title="Ir a hoy"
            >
              Hoy
            </button>
            <button
              type="button"
              class="rounded-lg px-2.5 py-1.5 text-xs font-semibold text-slate-600 transition hover:bg-slate-100"
              (click)="navegarFecha(1)"
              title="Día siguiente"
            >
              Mañana &rarr;
            </button>
          </div>

          <input
            type="date"
            class="rounded-xl border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-800 shadow-xs focus:border-marca-600 focus:outline-none"
            [ngModel]="fecha()"
            (ngModelChange)="cambiarFecha($event)"
          />

          <a
            routerLink="/totem-qr"
            target="_blank"
            class="inline-flex items-center gap-1.5 rounded-xl border border-teal-600 bg-teal-50 px-3 py-1.5 text-xs font-semibold text-teal-800 shadow-xs hover:bg-teal-100 transition-colors"
            title="Abrir pantalla tótem de entrada para monitor o tablet"
          >
            <svg class="h-4 w-4 text-teal-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z" />
            </svg>
            <span>Pantalla Tótem QR</span>
          </a>

          <ui-button variante="secundario" (click)="cargar()">
            <ui-icon nombre="clock" />
            Actualizar
          </ui-button>
        </div>
      </header>

      <!-- KPI Summary Cards -->
      @if (cargando()) {
        <ui-skeleton [filas]="2" etiquetaAria="Cargando indicadores de asistencia" />
      } @else if (panel()) {
        <dl class="grid grid-cols-2 gap-3 sm:grid-cols-5">
          <div class="tarjeta">
            <dt class="text-xs font-semibold text-slate-600">Turnos programados</dt>
            <dd class="mt-1 text-2xl font-extrabold text-slate-900">
              {{ panel()?.turnosProgramados }}
            </dd>
          </div>
          <div class="tarjeta border-l-4 border-l-amber-500 bg-amber-50/20">
            <dt class="text-xs font-semibold text-amber-800">En curso</dt>
            <dd class="mt-1 text-2xl font-extrabold text-amber-700">
              {{ panel()?.enCurso }}
            </dd>
          </div>
          <div class="tarjeta border-l-4 border-l-emerald-500 bg-emerald-50/20">
            <dt class="text-xs font-semibold text-emerald-800">Completados</dt>
            <dd class="mt-1 text-2xl font-extrabold text-emerald-700">
              {{ panel()?.conAsistencia }}
            </dd>
          </div>
          <div
            class="tarjeta border-l-4 transition-colors"
            [class.border-l-orange-500]="(panel()?.tardanzas ?? 0) > 0"
            [class.bg-orange-50/20]="(panel()?.tardanzas ?? 0) > 0"
            [class.border-l-slate-300]="(panel()?.tardanzas ?? 0) === 0"
          >
            <dt class="text-xs font-semibold text-slate-600">Llegadas tarde</dt>
            <dd
              class="mt-1 text-2xl font-extrabold"
              [class.text-orange-600]="(panel()?.tardanzas ?? 0) > 0"
              [class.text-slate-800]="(panel()?.tardanzas ?? 0) === 0"
            >
              {{ panel()?.tardanzas }}
            </dd>
          </div>
          <div
            class="tarjeta border-l-4 transition-colors"
            [class.border-l-rose-500]="(panel()?.sinAsistencia ?? 0) > 0"
            [class.bg-rose-50/20]="(panel()?.sinAsistencia ?? 0) > 0"
            [class.border-l-slate-300]="(panel()?.sinAsistencia ?? 0) === 0"
          >
            <dt class="text-xs font-semibold text-slate-600">Sin asistencia</dt>
            <dd
              class="mt-1 text-2xl font-extrabold"
              [class.text-rose-600]="(panel()?.sinAsistencia ?? 0) > 0"
              [class.text-slate-800]="(panel()?.sinAsistencia ?? 0) === 0"
            >
              {{ panel()?.sinAsistencia }}
            </dd>
          </div>
        </dl>
      }

      <!-- Main Table Card with Filters -->
      <article class="tarjeta" aria-labelledby="titulo-tabla-asistencia">
        <div class="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 id="titulo-tabla-asistencia" class="text-base font-bold text-slate-900">
              Marcajes del {{ formatearFecha(fecha()) }}
            </h2>
            <p class="text-xs text-slate-500">
              Listado detallado de empleados programados para esta fecha.
            </p>
          </div>

          <div class="flex flex-wrap items-center gap-2">
            <!-- Search by Name -->
            <input
              type="text"
              placeholder="Buscar empleado..."
              class="filtro-select text-xs min-w-44"
              [ngModel]="busqueda()"
              (ngModelChange)="busqueda.set($event)"
            />

            <!-- Filter by Area -->
            <select
              aria-label="Filtro de área"
              class="filtro-select text-xs"
              [ngModel]="areaSeleccionada()"
              (ngModelChange)="cambiarArea($event)"
            >
              <option [value]="0">Todas las áreas</option>
              @for (a of areas(); track a.idArea) {
                <option [value]="a.idArea">{{ a.nombre }}</option>
              }
            </select>

            <!-- Filter by Status -->
            <select
              aria-label="Filtro de estado"
              class="filtro-select text-xs"
              [ngModel]="filtroEstado()"
              (ngModelChange)="filtroEstado.set($event)"
            >
              <option value="TODOS">Todos ({{ panel()?.turnos?.length ?? 0 }})</option>
              <option value="EN_CURSO">En curso ({{ panel()?.enCurso ?? 0 }})</option>
              <option value="COMPLETADO">Completados ({{ panel()?.conAsistencia ?? 0 }})</option>
              <option value="TARDES">Llegadas tarde ({{ panel()?.tardanzas ?? 0 }})</option>
              <option value="SIN_ASISTENCIA">Sin asistencia ({{ panel()?.sinAsistencia ?? 0 }})</option>
              <option value="CON_NOVEDAD">Con incapacidad / novedad</option>
            </select>
          </div>
        </div>

        @if (cargando()) {
          <ui-skeleton [filas]="5" />
        } @else if (error()) {
          <p class="aviso-alerta">{{ error() }}</p>
        } @else {
          <div class="tabla-contenedor">
            <table class="tabla">
              <thead>
                <tr>
                  <th scope="col">Empleado</th>
                  <th scope="col">Área</th>
                  <th scope="col">Turno Programado</th>
                  <th scope="col">Hora Entrada</th>
                  <th scope="col">Hora Salida</th>
                  <th scope="col">Estado</th>
                  @if (puedeRegistrarManual()) {
                    <th scope="col" class="text-right">Acción</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (t of turnosFiltrados(); track t.idAsignacion) {
                  <tr
                    [class.bg-rose-50/30]="t.estado === 'SIN_ASISTENCIA' && !t.novedadQueExplica"
                    [class.bg-indigo-50/20]="Boolean(t.novedadQueExplica)"
                    class="transition-colors hover:bg-slate-50"
                  >
                    <td class="font-semibold text-slate-900">
                      {{ t.nombreEmpleado }}
                    </td>
                    <td>
                      <span class="inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">
                        {{ t.nombreArea }}
                      </span>
                    </td>
                    <td>
                      <div class="text-xs">
                        <span class="font-bold text-slate-800">{{ t.nombreTurno }}</span>
                        <span class="block text-slate-500 font-mono">
                          {{ formatearHora(t.programadoInicio) }} &ndash; {{ formatearHora(t.programadoFin) }}
                        </span>
                      </div>
                    </td>
                    <td>
                      @if (t.horaEntrada) {
                        <span class="font-mono text-xs font-medium text-slate-900">
                          {{ formatearHora(t.horaEntrada) }}
                        </span>
                        @if (t.tarde) {
                          <span class="block text-xs font-bold text-orange-600">
                            {{ t.minutosTarde }} min tarde
                          </span>
                        }
                      } @else if (t.novedadQueExplica) {
                        <span class="inline-flex items-center rounded bg-indigo-100/80 px-2 py-0.5 text-xs font-semibold text-indigo-700">
                          {{ t.novedadQueExplica }}
                        </span>
                      } @else {
                        <span class="inline-flex items-center rounded bg-rose-100/80 px-2 py-0.5 text-xs font-semibold text-rose-700">
                          Sin entrada
                        </span>
                      }
                    </td>
                    <td>
                      @if (t.horaSalida) {
                        <span class="font-mono text-xs font-medium text-slate-900">
                          {{ formatearHora(t.horaSalida) }}
                        </span>
                      } @else if (t.horaEntrada) {
                        <span class="inline-flex items-center rounded bg-amber-100/80 px-2 py-0.5 text-xs font-semibold text-amber-800">
                          En curso
                        </span>
                      } @else if (t.novedadQueExplica) {
                        <span class="inline-flex items-center rounded bg-indigo-100/80 px-2 py-0.5 text-xs font-semibold text-indigo-700">
                          {{ t.novedadQueExplica }}
                        </span>
                      } @else {
                        <span class="inline-flex items-center rounded bg-rose-100/80 px-2 py-0.5 text-xs font-semibold text-rose-700">
                          Sin salida
                        </span>
                      }
                    </td>
                    <td>
                      @if (t.novedadQueExplica) {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 bg-indigo-100 px-2.5 py-0.5 text-xs font-bold text-indigo-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-indigo-600" aria-hidden="true"></span>
                          {{ t.novedadQueExplica }}
                        </span>
                      } @else if (t.estado === 'COMPLETADO') {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden="true"></span>
                          Completado
                        </span>
                      } @else if (t.estado === 'EN_CURSO') {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-amber-500" aria-hidden="true"></span>
                          En curso
                        </span>
                      } @else if (t.estado === 'SIN_ASISTENCIA') {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-rose-200 bg-rose-100 px-2.5 py-0.5 text-xs font-bold text-rose-800">
                          <span class="h-1.5 w-1.5 rounded-full bg-rose-500" aria-hidden="true"></span>
                          Sin asistencia
                        </span>
                      } @else if (t.estado === 'SIN_ENTRADA') {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-rose-200 bg-rose-50 px-2.5 py-0.5 text-xs font-semibold text-rose-700">
                          <span class="h-1.5 w-1.5 rounded-full bg-rose-400" aria-hidden="true"></span>
                          Sin entrada
                        </span>
                      } @else {
                        <span class="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                          <span class="h-1.5 w-1.5 rounded-full bg-slate-400" aria-hidden="true"></span>
                          Por iniciar
                        </span>
                      }
                    </td>
                    @if (puedeRegistrarManual()) {
                      <td class="text-right">
                        <button
                          type="button"
                          class="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-700 shadow-2xs transition hover:border-marca-500 hover:text-marca-700"
                          (click)="abrirModalManual(t)"
                          title="Registrar o corregir asistencia manualmente"
                        >
                          Marcar / Corregir
                        </button>
                      </td>
                    }
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="py-8 text-center text-slate-500">
                      No se encontraron registros de turnos para los filtros seleccionados.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </article>

      <!-- Manual Attendance Dialog -->
      <app-asistencia-manual-dialog
        [abierto]="modalManualAbierto()"
        [turno]="turnoSeleccionado()"
        [guardando]="guardandoManual()"
        (guardar)="guardarMarcajeManual($event)"
        (cerrar)="modalManualAbierto.set(false)"
      />
    </section>
  `,
})
export class AsistenciaAdminPageComponent implements OnInit {
  private readonly api = inject(AsistenciaAdminApi);
  private readonly orgApi = inject(OrganizacionApi);
  private readonly auth = inject(AuthStore);
  private readonly notificaciones = inject(NotificationService);

  protected readonly formatearFecha = formatearFecha;
  protected readonly formatearHora = formatearHora;
  protected readonly Boolean = Boolean;

  protected readonly fecha = signal<string>(hoyIso());
  protected readonly areaSeleccionada = signal<number>(0);
  protected readonly busqueda = signal<string>('');
  protected readonly filtroEstado = signal<string>('TODOS');

  protected readonly panel = signal<PanelAsistenciaDia | null>(null);
  protected readonly areas = signal<readonly Area[]>([]);
  protected readonly cargando = signal<boolean>(true);
  protected readonly error = signal<string | null>(null);

  protected readonly modalManualAbierto = signal<boolean>(false);
  protected readonly turnoSeleccionado = signal<PanelAsistenciaTurno | null>(null);
  protected readonly guardandoManual = signal<boolean>(false);

  protected readonly puedeRegistrarManual = computed(() =>
    this.auth.puede(PERMISOS.asistenciaRegistrarManual)
  );

  protected readonly turnosFiltrados = computed(() => {
    const p = this.panel();
    if (!p) return [];

    let lista = p.turnos;

    const b = this.busqueda().trim().toLowerCase();
    if (b) {
      lista = lista.filter(
        (t) =>
          t.nombreEmpleado.toLowerCase().includes(b) ||
          t.nombreTurno.toLowerCase().includes(b) ||
          t.nombreArea.toLowerCase().includes(b)
      );
    }

    const estado = this.filtroEstado();
    if (estado === 'EN_CURSO') {
      lista = lista.filter((t) => t.estado === 'EN_CURSO');
    } else if (estado === 'COMPLETADO') {
      lista = lista.filter((t) => t.estado === 'COMPLETADO');
    } else if (estado === 'TARDES') {
      lista = lista.filter((t) => t.tarde);
    } else if (estado === 'SIN_ASISTENCIA') {
      lista = lista.filter((t) => t.estado === 'SIN_ASISTENCIA' && !t.novedadQueExplica);
    } else if (estado === 'CON_NOVEDAD') {
      lista = lista.filter((t) => Boolean(t.novedadQueExplica));
    }

    return lista;
  });

  ngOnInit(): void {
    this.cargarAreas();
    this.cargar();
  }

  cargarAreas(): void {
    this.orgApi.listarAreas().subscribe({
      next: (as) => this.areas.set(as),
      error: () => {},
    });
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set(null);
    const idArea = this.areaSeleccionada() > 0 ? this.areaSeleccionada() : undefined;

    this.api.obtenerPanelDia(this.fecha(), idArea).subscribe({
      next: (p) => {
        this.panel.set(p);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set('No se pudo cargar el panel de asistencia del día.');
      },
    });
  }

  cambiarFecha(f: string): void {
    if (f) {
      this.fecha.set(f);
      this.cargar();
    }
  }

  navegarFecha(dias: number): void {
    this.fecha.set(sumarDias(this.fecha(), dias));
    this.cargar();
  }

  irAHoy(): void {
    this.fecha.set(hoyIso());
    this.cargar();
  }

  cambiarArea(areaId: string | number): void {
    this.areaSeleccionada.set(Number(areaId) || 0);
    this.cargar();
  }

  abrirModalManual(turno: PanelAsistenciaTurno): void {
    this.turnoSeleccionado.set(turno);
    this.modalManualAbierto.set(true);
  }

  guardarMarcajeManual(evento: GuardarAsistenciaManualEvento): void {
    this.guardandoManual.set(true);
    const peticion =
      evento.esCorreccion && evento.idRegistro
        ? this.api.corregirManual(evento.idRegistro, evento)
        : this.api.registrarManual(evento);

    peticion.subscribe({
      next: () => {
        this.guardandoManual.set(false);
        this.modalManualAbierto.set(false);
        this.notificaciones.mostrar(
          'exito',
          'Éxito',
          evento.esCorreccion
            ? 'Corrección de asistencia aplicada y reliquidada correctamente.'
            : 'Registro de asistencia guardado y liquidado correctamente.'
        );
        this.cargar();
      },
      error: (err: any) => {
        this.guardandoManual.set(false);
        this.notificaciones.mostrar(
          'error',
          'Error',
          err.error?.detail || 'No se pudo procesar el marcaje de asistencia.'
        );
      },
    });
  }
}
