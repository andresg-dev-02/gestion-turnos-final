import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore, CodigoPermiso, IconoNavegacion, PERMISOS } from '../../../app/core';
import { PanelInicial } from '../../../app/shared/data/modelos';
import { SolicitudesAdminApi } from '../../../app/shared/data/apis';
import { UiIconComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import {
  AdminCountersGridComponent,
  ContadorAdminItem,
} from '../components/admin-counters-grid.component';

interface Modulo {
  readonly titulo: string;
  readonly descripcion: string;
  readonly ruta: string;
  readonly query?: Record<string, string>;
  readonly icono: IconoNavegacion;
  readonly permiso: CodigoPermiso;
}

const MODULOS: readonly Modulo[] = [
  { titulo: 'Solicitudes de registro', descripcion: 'Aprobar o rechazar nuevos usuarios.', ruta: '/admin/solicitudes', query: { tipo: 'registro' }, icono: 'shield', permiso: PERMISOS.registrosAprobar },
  { titulo: 'Solicitudes de vacaciones', descripcion: 'Revisar saldo y turnos en conflicto.', ruta: '/admin/solicitudes', query: { tipo: 'vacaciones' }, icono: 'sun', permiso: PERMISOS.vacacionesAprobar },
  { titulo: 'Permisos y novedades', descripcion: 'Incapacidades, calamidades y permisos.', ruta: '/admin/solicitudes', query: { tipo: 'novedades' }, icono: 'inbox', permiso: PERMISOS.novedadesAprobar },
  { titulo: 'Cambios de turno', descripcion: 'Intercambios solicitados entre compañeros.', ruta: '/admin/solicitudes', query: { tipo: 'cambios' }, icono: 'swap', permiso: PERMISOS.turnosProgramar },
  { titulo: 'Asignación de turnos', descripcion: 'Programar, reasignar, anular y publicar.', ruta: '/admin/programacion', icono: 'calendar', permiso: PERMISOS.turnosProgramar },
  { titulo: 'Configuración', descripcion: 'Parámetros, festivos y catálogos.', ruta: '/admin/configuracion', icono: 'settings', permiso: PERMISOS.parametrosEditar },
];

/**
 * Panel Administrativo (/admin): indicadores en vivo (GET /panel) y accesos a los
 * módulos de gestión según los permisos del usuario.
 */
@Component({
  selector: 'app-admin-panel-page',
  imports: [AdminCountersGridComponent, RouterLink, UiIconComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-panel-admin" class="space-y-8">
      <header>
        <h1 id="titulo-panel-admin" class="text-2xl font-extrabold tracking-tight text-slate-900">
          Panel de Administración
        </h1>
        <p class="mt-1 text-sm text-slate-600">
          Indicadores del día y solicitudes pendientes de revisión.
        </p>
      </header>

      @if (panel()) {
        <app-admin-counters-grid [contadores]="contadores()" />
      } @else {
        <ui-skeleton [filas]="2" etiquetaAria="Cargando indicadores" />
      }

      <section aria-labelledby="titulo-modulos">
        <h2 id="titulo-modulos" class="mb-3 text-lg font-bold text-slate-900">Módulos de gestión</h2>
        <ul class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (m of modulos; track m.titulo) {
            <li>
              <a
                [routerLink]="m.ruta"
                [queryParams]="m.query"
                class="flex h-full min-h-24 items-start gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-xs transition hover:border-marca-500 hover:shadow-md"
              >
                <span class="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-marca-50 text-marca-700">
                  <ui-icon [nombre]="m.icono" />
                </span>
                <span>
                  <span class="block font-bold text-slate-900">{{ m.titulo }}</span>
                  <span class="mt-0.5 block text-sm text-slate-600">{{ m.descripcion }}</span>
                </span>
              </a>
            </li>
          }
        </ul>
      </section>
    </section>
  `,
})
export class AdminPanelPageComponent implements OnInit {
  private readonly api = inject(SolicitudesAdminApi);
  private readonly store = inject(AuthStore);

  protected readonly modulos = MODULOS.filter((m) => this.store.puede(m.permiso));
  protected readonly panel = signal<PanelInicial | null>(null);

  protected readonly contadores = computed<readonly ContadorAdminItem[]>(() => {
    const p = this.panel();
    if (!p) return [];
    return [
      { etiqueta: 'Solicitudes de registro', valor: p.registrosPendientes, ruta: '/admin/solicitudes', query: { tipo: 'registro' } },
      { etiqueta: 'Vacaciones pendientes', valor: p.vacacionesPendientes, ruta: '/admin/solicitudes', query: { tipo: 'vacaciones' } },
      { etiqueta: 'Novedades pendientes', valor: p.novedadesPendientes, ruta: '/admin/solicitudes', query: { tipo: 'novedades' } },
      { etiqueta: 'Empleados activos', valor: p.empleadosActivos, ruta: '/admin/empleados' },
      { etiqueta: 'Turnos de hoy', valor: p.turnosHoy, ruta: '/admin/programacion' },
      { etiqueta: 'Turnos sin asistencia', valor: p.sinAsistenciaHoy, ruta: '/admin/asistencia' },
    ];
  });

  ngOnInit(): void {
    this.api.panel().subscribe({
      next: (p) => this.panel.set(p),
      error: () =>
        this.panel.set({
          registrosPendientes: 0,
          vacacionesPendientes: 0,
          novedadesPendientes: 0,
          empleadosActivos: 0,
          turnosHoy: 0,
          sinAsistenciaHoy: 0,
        }),
    });
  }
}
