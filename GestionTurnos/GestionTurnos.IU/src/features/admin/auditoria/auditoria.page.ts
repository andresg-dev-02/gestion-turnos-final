import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditoriaApi, BitacoraNegocio } from '../../../app/shared/data';
import { NotificationService } from '../../../app/core';
import {
  UiButtonComponent,
  UiEmptyStateComponent,
  UiPaginadorComponent,
  UiSkeletonComponent,
} from '../../../app/shared/ui';
import { BitacoraDetalleDialogComponent } from './components/bitacora-detalle-dialog.component';

@Component({
  selector: 'app-auditoria-page',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    UiButtonComponent,
    UiEmptyStateComponent,
    UiPaginadorComponent,
    UiSkeletonComponent,
    BitacoraDetalleDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-auditoria" class="space-y-6">
      <header class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 id="titulo-auditoria" class="text-2xl font-extrabold tracking-tight text-slate-900">
            Bitácora de Auditoría
          </h1>
          <p class="mt-1 text-sm text-slate-600">
            Registro cronológico e inmutable de operaciones, modificaciones y eventos de seguridad.
          </p>
        </div>
        <div class="flex items-center gap-2">
          <ui-button variante="secundario" (click)="cargarBitacora(paginaActual())" [deshabilitado]="cargando()">
            Actualizar
          </ui-button>
        </div>
      </header>

      <!-- Panel de Filtros -->
      <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm space-y-4">
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <!-- Filtro por Acción -->
          <div>
            <label for="filtro-accion" class="block text-xs font-semibold text-slate-600 mb-1">
              Tipo de Acción
            </label>
            <select
              id="filtro-accion"
              [ngModel]="filtroAccion()"
              (ngModelChange)="cambiarFiltroAccion($event)"
              class="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
            >
              <option value="">Todas las acciones</option>
              <option value="CREAR">Creación (CREAR)</option>
              <option value="MODIFICAR">Modificación (MODIFICAR)</option>
              <option value="ELIMINAR">Eliminación (ELIMINAR)</option>
              <option value="INICIO_SESION">Inicio de Sesión (INICIO_SESION)</option>
              <option value="APROBAR">Aprobación (APROBAR)</option>
              <option value="RECHAZAR">Rechazo (RECHAZAR)</option>
            </select>
          </div>

          <!-- Filtro por Esquema / Tabla -->
          <div>
            <label for="filtro-tabla" class="block text-xs font-semibold text-slate-600 mb-1">
              Tabla / Entidad
            </label>
            <select
              id="filtro-tabla"
              [ngModel]="filtroTabla()"
              (ngModelChange)="cambiarFiltroTabla($event)"
              class="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
            >
              <option value="">Todas las tablas</option>
              <option value="organizacion.empleado">organizacion.empleado</option>
              <option value="organizacion.cargo">organizacion.cargo</option>
              <option value="organizacion.area">organizacion.area</option>
              <option value="organizacion.sucursal">organizacion.sucursal</option>
              <option value="seguridad.usuario">seguridad.usuario</option>
              <option value="seguridad.solicitud_registro">seguridad.solicitud_registro</option>
              <option value="seguridad.rol">seguridad.rol</option>
              <option value="turnos.turno">turnos.turno</option>
              <option value="turnos.asignacion">turnos.asignacion</option>
              <option value="turnos.dia_descanso">turnos.dia_descanso</option>
              <option value="asistencia.registro">asistencia.registro</option>
              <option value="novedades.solicitud_vacaciones">novedades.solicitud_vacaciones</option>
              <option value="novedades.novedad">novedades.novedad</option>
            </select>
          </div>

          <!-- Fecha Desde -->
          <div>
            <label for="filtro-desde" class="block text-xs font-semibold text-slate-600 mb-1">
              Fecha Desde
            </label>
            <input
              type="date"
              id="filtro-desde"
              [ngModel]="filtroDesde()"
              (ngModelChange)="cambiarFiltroDesde($event)"
              class="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
            />
          </div>

          <!-- Fecha Hasta -->
          <div>
            <label for="filtro-hasta" class="block text-xs font-semibold text-slate-600 mb-1">
              Fecha Hasta
            </label>
            <input
              type="date"
              id="filtro-hasta"
              [ngModel]="filtroHasta()"
              (ngModelChange)="cambiarFiltroHasta($event)"
              class="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
            />
          </div>
        </div>
      </div>

      <!-- Tabla de Registros de Auditoría -->
      @if (cargando()) {
        <ui-skeleton />
      } @else if (registros().length === 0) {
        <ui-empty-state
          titulo="Sin eventos de auditoría"
          descripcion="Aún no se han registrado eventos con los filtros seleccionados."
        />
      } @else {
        <div class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm text-slate-700">
              <thead class="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase tracking-wider text-slate-600">
                <tr>
                  <th scope="col" class="px-4 py-3">Fecha y Hora</th>
                  <th scope="col" class="px-4 py-3">Acción</th>
                  <th scope="col" class="px-4 py-3">Entidad / Tabla</th>
                  <th scope="col" class="px-4 py-3">ID Registro</th>
                  <th scope="col" class="px-4 py-3">Usuario Autor</th>
                  <th scope="col" class="px-4 py-3">Empleado Afectado</th>
                  <th scope="col" class="px-4 py-3 text-right">Detalle</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 font-normal">
                @for (reg of registros(); track reg.idBitacora) {
                  <tr class="transition-colors hover:bg-slate-50/80">
                    <td class="whitespace-nowrap px-4 py-3 text-xs font-medium text-slate-900">
                      {{ reg.fechaHora | date:'dd/MM/yyyy, hh:mm:ss a' }}
                    </td>
                    <td class="whitespace-nowrap px-4 py-3">
                      <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-bold" [ngClass]="badgeClases(reg.codTipoAccion)">
                        <span class="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true"></span>
                        {{ reg.nombreTipoAccion || reg.codTipoAccion }}
                      </span>
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 font-mono text-xs text-slate-700">
                      {{ reg.esquema }}.{{ reg.tabla }}
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 font-mono text-xs text-slate-600">
                      {{ reg.idRegistro }}
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-xs text-slate-900">
                      <div class="font-medium">{{ reg.correoUsuario || 'Sistema' }}</div>
                      @if (reg.ipOrigen) {
                        <div class="text-[11px] text-slate-500">{{ reg.ipOrigen }}</div>
                      }
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-xs text-slate-700">
                      {{ reg.nombreEmpleado || '—' }}
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-right">
                      <button
                        type="button"
                        (click)="verDetalle(reg)"
                        class="rounded-lg px-2.5 py-1 text-xs font-semibold text-[#1D768A] transition-colors hover:bg-[#1D768A]/10 cursor-pointer"
                      >
                        Ver cambios
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Paginador -->
          <div class="border-t border-slate-200 px-4 py-3">
            <ui-paginador
              [pagina]="paginaActual()"
              [tamano]="tamanoPagina()"
              [total]="totalRegistros()"
              (cambio)="cargarBitacora($event)"
            />
          </div>
        </div>
      }

      <!-- Modal de Detalle de Cambios -->
      <app-bitacora-detalle-dialog
        [abierto]="dialogoDetalleAbierto()"
        [registro]="registroSeleccionado()"
        (cerrar)="dialogoDetalleAbierto.set(false)"
      />
    </section>
  `,
})
export class AuditoriaPageComponent implements OnInit {
  private readonly auditoriaApi = inject(AuditoriaApi);
  private readonly notificaciones = inject(NotificationService);

  protected readonly registros = signal<readonly BitacoraNegocio[]>([]);
  protected readonly cargando = signal(false);
  protected readonly paginaActual = signal(1);
  protected readonly tamanoPagina = signal(25);
  protected readonly totalRegistros = signal(0);

  protected readonly filtroAccion = signal('');
  protected readonly filtroTabla = signal('');
  protected readonly filtroDesde = signal('');
  protected readonly filtroHasta = signal('');

  protected readonly dialogoDetalleAbierto = signal(false);
  protected readonly registroSeleccionado = signal<BitacoraNegocio | null>(null);

  ngOnInit(): void {
    this.cargarBitacora(1);
  }

  protected cargarBitacora(pagina = 1): void {
    this.cargando.set(true);
    this.paginaActual.set(pagina);

    const tablaRaw = this.filtroTabla();
    let esquema: string | undefined;
    let tabla: string | undefined;

    if (tablaRaw.includes('.')) {
      const partes = tablaRaw.split('.');
      esquema = partes[0];
      tabla = partes[1];
    } else if (tablaRaw) {
      tabla = tablaRaw;
    }

    const fechaDesde = this.filtroDesde() ? new Date(this.filtroDesde() + 'T00:00:00Z').toISOString() : undefined;
    const fechaHasta = this.filtroHasta() ? new Date(this.filtroHasta() + 'T23:59:59Z').toISOString() : undefined;

    this.auditoriaApi
      .consultarBitacora({
        codTipoAccion: this.filtroAccion() || undefined,
        esquema,
        tabla,
        fechaDesde,
        fechaHasta,
        pageNumber: pagina,
        pageSize: this.tamanoPagina(),
      })
      .subscribe({
        next: (resp) => {
          this.registros.set(resp.items ?? []);
          this.totalRegistros.set(resp.totalCount ?? 0);
          this.cargando.set(false);
        },
        error: (err) => {
          this.notificaciones.error(
            'Error al cargar auditoría',
            err.error?.detail || 'No se pudieron consultar los registros de la bitácora.'
          );
          this.cargando.set(false);
        },
      });
  }

  protected cambiarFiltroAccion(valor: string): void {
    this.filtroAccion.set(valor);
    this.cargarBitacora(1);
  }

  protected cambiarFiltroTabla(valor: string): void {
    this.filtroTabla.set(valor);
    this.cargarBitacora(1);
  }

  protected cambiarFiltroDesde(valor: string): void {
    this.filtroDesde.set(valor);
    this.cargarBitacora(1);
  }

  protected cambiarFiltroHasta(valor: string): void {
    this.filtroHasta.set(valor);
    this.cargarBitacora(1);
  }

  protected verDetalle(registro: BitacoraNegocio): void {
    this.registroSeleccionado.set(registro);
    this.dialogoDetalleAbierto.set(true);
  }

  protected badgeClases(codAccion: string): string {
    switch (codAccion?.toUpperCase()) {
      case 'CREAR':
      case 'APROBAR':
        return 'bg-emerald-100 text-emerald-800';
      case 'MODIFICAR':
        return 'bg-sky-100 text-sky-800';
      case 'ELIMINAR':
      case 'RECHAZAR':
        return 'bg-rose-100 text-rose-800';
      case 'INICIO_SESION':
        return 'bg-indigo-100 text-indigo-800';
      default:
        return 'bg-slate-100 text-slate-800';
    }
  }
}
