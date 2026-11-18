import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../../app/core';
import {
  ActualizarEmpleadoDto,
  Area,
  Cargo,
  CatalogoItem,
  CrearEmpleadoDto,
  Empleado,
  OrganizacionApi,
  ParametrizacionApi,
} from '../../../app/shared/data';
import { UiButtonComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import { EmpleadoDesactivarDialogComponent } from './components/empleado-desactivar-dialog.component';
import { EmpleadoFormDialogComponent } from './components/empleado-form-dialog.component';
import { EmpleadosTableComponent } from './components/empleados-table.component';

@Component({
  selector: 'app-empleados-page',
  imports: [
    FormsModule,
    UiButtonComponent,
    UiSkeletonComponent,
    EmpleadosTableComponent,
    EmpleadoFormDialogComponent,
    EmpleadoDesactivarDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-empleados" class="space-y-6">
      <header class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 id="titulo-empleados" class="text-2xl font-extrabold tracking-tight text-slate-900">
            Gestión de Empleados
          </h1>
          <p class="mt-1 text-sm text-slate-600">
            Directorio y administración integral de colaboradores, áreas, cargos y contratos.
          </p>
        </div>
        <ui-button (click)="abrirNuevo()">
          + Nuevo Empleado
        </ui-button>
      </header>

      <!-- Barra de Filtros y Búsqueda -->
      <div class="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
        <div class="relative w-full max-w-sm">
          <input
            type="search"
            aria-label="Buscar empleado por nombre o documento"
            placeholder="Buscar por nombre, documento o correo..."
            [ngModel]="busqueda()"
            (ngModelChange)="busqueda.set($event)"
            class="w-full rounded-lg border border-slate-300 px-3.5 py-2 text-sm text-slate-900 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
          />
        </div>
        <div class="flex items-center gap-3">
          <label for="filtro-estado" class="text-xs font-semibold text-slate-600">Estado:</label>
          <select
            id="filtro-estado"
            [ngModel]="estadoFiltro()"
            (ngModelChange)="cambiarEstadoFiltro($event)"
            class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800 transition-colors focus:border-[#1D768A] focus:outline-none focus:ring-2 focus:ring-[#1D768A]/20"
          >
            <option value="TODOS">Todos</option>
            <option value="ACTIVO">Activos</option>
            <option value="INACTIVO">Inactivos</option>
          </select>
        </div>
      </div>

      <!-- Tabla de Empleados -->
      @if (cargando()) {
        <ui-skeleton />
      } @else {
        <app-empleados-table
          [empleados]="empleadosFiltrados()"
          (editar)="abrirEditar($event)"
          (desactivar)="abrirDesactivar($event)"
          (reactivar)="reactivarEmpleado($event)"
        />
      }

      <!-- Diálogo de Creación / Edición -->
      <app-empleado-form-dialog
        [abierto]="dialogoFormAbierto()"
        [empleado]="empleadoSeleccionado()"
        [areas]="areas()"
        [cargos]="cargos()"
        [tiposDocumento]="tiposDocumento()"
        [tiposContrato]="tiposContrato()"
        [guardando]="guardando()"
        (guardar)="guardarEmpleado($event)"
        (cerrar)="dialogoFormAbierto.set(false)"
      />

      <!-- Diálogo de Desactivación -->
      <app-empleado-desactivar-dialog
        [abierto]="dialogoDesactivarAbierto()"
        [empleado]="empleadoSeleccionado()"
        [procesando]="guardando()"
        (confirmar)="confirmarDesactivar($event)"
        (cerrar)="dialogoDesactivarAbierto.set(false)"
      />
    </section>
  `,
})
export class EmpleadosPageComponent {
  private readonly orgApi = inject(OrganizacionApi);
  private readonly paramApi = inject(ParametrizacionApi);
  private readonly notificaciones = inject(NotificationService);

  protected readonly cargando = signal(true);
  protected readonly guardando = signal(false);
  protected readonly busqueda = signal('');
  protected readonly estadoFiltro = signal('TODOS');
  protected readonly empleados = signal<readonly Empleado[]>([]);

  protected readonly areas = signal<readonly Area[]>([]);
  protected readonly cargos = signal<readonly Cargo[]>([]);
  protected readonly tiposDocumento = signal<readonly CatalogoItem[]>([]);
  protected readonly tiposContrato = signal<readonly CatalogoItem[]>([]);

  protected readonly dialogoFormAbierto = signal(false);
  protected readonly dialogoDesactivarAbierto = signal(false);
  protected readonly empleadoSeleccionado = signal<Empleado | null>(null);

  protected readonly empleadosFiltrados = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    const list = this.empleados();
    if (!q) return list;
    return list.filter((e) =>
      `${e.nombres} ${e.apellidos} ${e.numeroDocumento} ${e.correoContacto ?? ''} ${e.correoUsuario ?? ''}`
        .toLowerCase()
        .includes(q)
    );
  });

  constructor() {
    this.cargarDatos();
  }

  protected cargarDatos(): void {
    this.cargando.set(true);
    const params: Record<string, string | number | boolean> = { pageSize: 200 };
    if (this.estadoFiltro() !== 'TODOS') {
      params['estado'] = this.estadoFiltro();
    }

    this.orgApi.listarEmpleados(params).subscribe({
      next: (res) => {
        this.empleados.set(res.items);
        this.cargando.set(false);
      },
      error: () => {
        this.notificaciones.error('No fue posible cargar el listado de empleados');
        this.cargando.set(false);
      },
    });

    this.orgApi.listarAreas().subscribe({ next: (res) => this.areas.set(res) });
    this.orgApi.listarCargos().subscribe({ next: (res) => this.cargos.set(res) });
    this.paramApi.listarTiposDocumento().subscribe({ next: (res) => this.tiposDocumento.set(res) });
    this.paramApi.listarTiposContrato().subscribe({ next: (res) => this.tiposContrato.set(res) });
  }

  protected cambiarEstadoFiltro(nuevoEstado: string): void {
    this.estadoFiltro.set(nuevoEstado);
    this.cargarDatos();
  }

  protected abrirNuevo(): void {
    this.empleadoSeleccionado.set(null);
    this.dialogoFormAbierto.set(true);
  }

  protected abrirEditar(empleado: Empleado): void {
    this.empleadoSeleccionado.set(empleado);
    this.dialogoFormAbierto.set(true);
  }

  protected abrirDesactivar(empleado: Empleado): void {
    this.empleadoSeleccionado.set(empleado);
    this.dialogoDesactivarAbierto.set(true);
  }

  protected guardarEmpleado(dto: CrearEmpleadoDto | ActualizarEmpleadoDto): void {
    this.guardando.set(true);
    const emp = this.empleadoSeleccionado();

    if (emp) {
      this.orgApi.actualizarEmpleado(emp.idEmpleado, dto).subscribe({
        next: () => {
          this.notificaciones.exito('Empleado actualizado exitosamente');
          this.dialogoFormAbierto.set(false);
          this.guardando.set(false);
          this.cargarDatos();
        },
        error: (err) => {
          this.notificaciones.error(err.error?.detail || 'Error al actualizar empleado');
          this.guardando.set(false);
        },
      });
    } else {
      this.orgApi.crearEmpleado(dto as CrearEmpleadoDto).subscribe({
        next: () => {
          this.notificaciones.exito('Empleado registrado exitosamente');
          this.dialogoFormAbierto.set(false);
          this.guardando.set(false);
          this.cargarDatos();
        },
        error: (err) => {
          this.notificaciones.error(err.error?.detail || 'Error al registrar empleado');
          this.guardando.set(false);
        },
      });
    }
  }

  protected confirmarDesactivar(fechaRetiro: string): void {
    const emp = this.empleadoSeleccionado();
    if (!emp) return;

    this.guardando.set(true);
    this.orgApi.desactivarEmpleado(emp.idEmpleado, fechaRetiro).subscribe({
      next: () => {
        this.notificaciones.exito('Empleado desactivado correctamente');
        this.dialogoDesactivarAbierto.set(false);
        this.guardando.set(false);
        this.cargarDatos();
      },
      error: (err) => {
        this.notificaciones.error(err.error?.detail || 'Error al desactivar empleado');
        this.guardando.set(false);
      },
    });
  }

  protected reactivarEmpleado(emp: Empleado): void {
    this.cargando.set(true);
    this.orgApi.reactivarEmpleado(emp.idEmpleado).subscribe({
      next: () => {
        this.notificaciones.exito('Empleado reactivado correctamente');
        this.cargarDatos();
      },
      error: (err) => {
        this.notificaciones.error(err.error?.detail || 'Error al reactivar empleado');
        this.cargando.set(false);
      },
    });
  }
}

