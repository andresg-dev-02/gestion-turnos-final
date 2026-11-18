import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  PermisoSistema,
  RolSistema,
  RolesAdminApi,
} from '../../../../app/shared/data';
import { NotificationService } from '../../../../app/core';
import {
  UiButtonComponent,
  UiIconComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import {
  GuardarRolEvento,
  RolFormDialogComponent,
} from './rol-form-dialog.component';

interface PermisoConEstado {
  readonly codigo: string;
  readonly descripcion: string;
  readonly modulo: string;
  readonly asignado: boolean;
}

interface GrupoModulo {
  readonly id: string;
  readonly nombre: string;
  readonly icono: string;
  readonly permisos: readonly PermisoConEstado[];
}

const MODULOS_CONFIG: Record<
  string,
  { nombre: string; icono: string; permisos: readonly string[] }
> = {
  SEGURIDAD: {
    nombre: 'Seguridad, Usuarios y Accesos',
    icono: 'shield',
    permisos: ['usuarios.gestionar', 'registros.aprobar'],
  },
  ORGANIZACION: {
    nombre: 'Organización y Empleados',
    icono: 'users',
    permisos: ['empleados.gestionar', 'organizacion.gestionar'],
  },
  TURNOS: {
    nombre: 'Programación y Catálogo de Turnos',
    icono: 'calendar',
    permisos: ['turnos.configurar', 'turnos.programar'],
  },
  ASISTENCIA: {
    nombre: 'Control de Asistencia y Marcaciones',
    icono: 'clock',
    permisos: ['asistencia.consultar', 'asistencia.registrar_manual'],
  },
  NOVEDADES: {
    nombre: 'Vacaciones y Novedades de Personal',
    icono: 'sun',
    permisos: ['vacaciones.aprobar', 'novedades.aprobar'],
  },
  SISTEMA: {
    nombre: 'Configuración, Reportes y Auditoría',
    icono: 'settings',
    permisos: ['parametros.editar', 'reportes.consultar', 'auditoria.consultar'],
  },
};

@Component({
  selector: 'app-roles-permisos-section',
  imports: [
    FormsModule,
    UiButtonComponent,
    UiIconComponent,
    UiSkeletonComponent,
    RolFormDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 gap-6 lg:grid-cols-12 items-start">
      <!-- Columna Izquierda: Lista de Roles -->
      <div class="lg:col-span-4 rounded-2xl border border-slate-200 bg-white p-4 space-y-4 shadow-xs">
        <div class="flex items-center justify-between pb-3 border-b border-slate-100">
          <div>
            <h3 class="font-bold text-slate-800 text-sm flex items-center gap-2">
              <ui-icon nombre="shield" />
              Roles Institucionales
            </h3>
            <p class="text-xs text-slate-500 mt-0.5">
              Audita y asigna permisos a cada rol
            </p>
          </div>
          <span class="text-xs bg-slate-100 font-bold px-2 py-0.5 rounded-full text-slate-600">
            {{ roles().length }}
          </span>
        </div>

        <div class="flex items-center gap-2">
          <input
            type="text"
            placeholder="Buscar rol..."
            class="filtro-select text-xs w-full"
            [ngModel]="busquedaRoles()"
            (ngModelChange)="busquedaRoles.set($event)"
          />
          <button
            type="button"
            class="rounded-lg bg-marca-50 px-2.5 py-1.5 text-xs font-bold text-marca-700 hover:bg-marca-100 transition shrink-0"
            (click)="abrirModalCrearRol()"
            title="Crear un nuevo rol"
          >
            + Rol
          </button>
        </div>

        @if (cargando()) {
          <ui-skeleton [filas]="3" />
        } @else {
          <div class="space-y-2">
            @for (rol of rolesFiltrados(); track rol.idRol) {
              <div
                (click)="seleccionarRol(rol)"
                class="p-3 rounded-xl border transition-all cursor-pointer flex items-center justify-between gap-2"
                [class.border-marca-500]="rolSeleccionado()?.idRol === rol.idRol"
                [class.bg-marca-50/50]="rolSeleccionado()?.idRol === rol.idRol"
                [class.shadow-xs]="rolSeleccionado()?.idRol === rol.idRol"
                [class.border-slate-200]="rolSeleccionado()?.idRol !== rol.idRol"
                [class.hover:border-slate-300]="rolSeleccionado()?.idRol !== rol.idRol"
                [class.hover:bg-slate-50/50]="rolSeleccionado()?.idRol !== rol.idRol"
              >
                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-xs text-slate-900 truncate">{{ rol.nombre }}</span>
                    @if (!rol.activo) {
                      <span class="text-[10px] bg-rose-100 text-rose-700 font-bold px-1.5 py-0.2 rounded">Inactivo</span>
                    }
                  </div>
                  <p class="text-[11px] text-slate-500 truncate mt-0.5">
                    {{ rol.descripcion || 'Sin descripción' }}
                  </p>
                </div>
                <div class="flex items-center gap-1.5 shrink-0">
                  <span
                    class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 text-slate-700"
                    title="Permisos concedidos"
                  >
                    {{ rol.permisos.length }}
                  </span>
                  <button
                    type="button"
                    class="rounded-md p-1 text-slate-400 hover:bg-slate-200 hover:text-slate-700"
                    (click)="abrirModalEditarRol(rol, $event)"
                    title="Editar datos del rol"
                  >
                    <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                    </svg>
                  </button>
                </div>
              </div>
            } @empty {
              <p class="py-4 text-center text-xs text-slate-400">
                No se encontraron roles coincidentes.
              </p>
            }
          </div>
        }
      </div>

      <!-- Columna Derecha: Matriz de Permisos del Rol Seleccionado -->
      <div class="lg:col-span-8 rounded-2xl border border-slate-200 bg-white p-5 space-y-5 shadow-xs">
        @if (!rolSeleccionado()) {
          <div class="p-16 text-center text-slate-400">
            <ui-icon nombre="shield" class="text-4xl text-slate-300 mb-3" />
            <p class="text-sm font-semibold text-slate-700">Selecciona un rol de la lista</p>
            <p class="text-xs text-slate-500 mt-1">
              Podrás visualizar, auditar y alternar los permisos concedidos en la base de datos.
            </p>
          </div>
        } @else {
          <!-- Cabecera del Rol -->
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-100">
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-lg font-bold text-slate-900">{{ rolSeleccionado()?.nombre }}</h2>
                <span
                  class="px-2 py-0.5 rounded-md text-[11px] font-mono font-bold"
                  [class.bg-emerald-100]="rolSeleccionado()?.activo"
                  [class.text-emerald-800]="rolSeleccionado()?.activo"
                  [class.bg-slate-200]="!rolSeleccionado()?.activo"
                  [class.text-slate-700]="!rolSeleccionado()?.activo"
                >
                  {{ rolSeleccionado()?.activo ? 'ACTIVO' : 'INACTIVO' }}
                </span>
              </div>
              <p class="text-xs text-slate-500 mt-1">
                {{ rolSeleccionado()?.descripcion || 'Rol institucional para control de accesos al sistema.' }}
              </p>
            </div>

            <div class="flex items-center gap-3">
              <div class="text-right hidden sm:block">
                <span class="text-xs font-bold text-slate-800 block">
                  {{ permisosActivosCount() }} de {{ permisosDisponibles().length }}
                </span>
                <span class="text-[10px] text-slate-500">Permisos activos</span>
              </div>

              <ui-button
                (click)="guardarPermisos()"
                [deshabilitado]="guardandoPermisos()"
              >
                {{ guardandoPermisos() ? 'Guardando...' : 'Guardar Permisos BD' }}
              </ui-button>
            </div>
          </div>

          <!-- Filtros de Permisos y Acciones Rápidas -->
          <div class="flex flex-col sm:flex-row items-center justify-between gap-3 bg-slate-50 p-3 rounded-xl border border-slate-200">
            <input
              type="text"
              placeholder="Filtrar permisos por nombre o código..."
              class="filtro-select text-xs w-full sm:w-80"
              [ngModel]="busquedaPermisos()"
              (ngModelChange)="busquedaPermisos.set($event)"
            />

            <div class="flex items-center gap-2 w-full sm:w-auto justify-end">
              <button
                type="button"
                class="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-50 transition"
                (click)="marcarTodos(true)"
              >
                Marcar Todos
              </button>
              <button
                type="button"
                class="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-rose-700 hover:bg-rose-50 transition"
                (click)="marcarTodos(false)"
              >
                Desmarcar Todos
              </button>
            </div>
          </div>

          <!-- Módulos de Permisos Agrupados -->
          <div class="space-y-4 max-h-[620px] overflow-y-auto pr-1">
            @for (grupo of gruposFiltrados(); track grupo.id) {
              <div class="border border-slate-200 rounded-xl overflow-hidden bg-white shadow-2xs">
                <div class="bg-slate-50/80 px-4 py-2.5 border-b border-slate-200 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <ui-icon [nombre]="asIcono(grupo.icono)" class="text-xs text-marca-600" />
                    <span class="font-bold text-xs text-slate-800">{{ grupo.nombre }}</span>
                  </div>
                  <span class="text-[10px] font-bold text-slate-600 bg-slate-200 px-2 py-0.5 rounded-full">
                    {{ contarActivosGrupo(grupo) }} / {{ grupo.permisos.length }}
                  </span>
                </div>

                <div class="divide-y divide-slate-100">
                  @for (p of grupo.permisos; track p.codigo) {
                    <div class="p-3 flex items-center justify-between hover:bg-slate-50/70 transition">
                      <div class="pr-3">
                        <p class="font-semibold text-xs text-slate-900 leading-tight">
                          {{ p.descripcion }}
                        </p>
                        <span class="text-[10px] font-mono text-slate-500 block mt-0.5">
                          {{ p.codigo }}
                        </span>
                      </div>

                      <label class="relative inline-flex items-center cursor-pointer shrink-0">
                        <input
                          type="checkbox"
                          [checked]="p.asignado"
                          (change)="togglePermiso(p.codigo)"
                          class="sr-only peer"
                        />
                        <div class="h-5 w-9 rounded-full bg-slate-300 peer-checked:bg-marca-600 transition peer-focus:outline-none after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:h-4 after:w-4 after:rounded-full after:bg-white after:transition-all peer-checked:after:translate-x-4"></div>
                      </label>
                    </div>
                  }
                </div>
              </div>
            } @empty {
              <p class="py-8 text-center text-xs text-slate-500">
                No hay permisos que coincidan con la búsqueda.
              </p>
            }
          </div>
        }
      </div>
    </div>

    <!-- Diálogo para Crear / Editar Rol -->
    <app-rol-form-dialog
      [abierto]="modalRolAbierto()"
      [rol]="rolParaEditar()"
      [guardando]="guardandoRolInfo()"
      (guardar)="guardarRolInfo($event)"
      (cerrar)="modalRolAbierto.set(false)"
    />
  `,
})
export class RolesPermisosSectionComponent implements OnInit {
  private readonly api = inject(RolesAdminApi);
  private readonly notificaciones = inject(NotificationService);

  protected readonly roles = signal<readonly RolSistema[]>([]);
  protected readonly permisosDisponibles = signal<readonly PermisoSistema[]>([]);
  protected readonly cargando = signal(true);

  protected readonly busquedaRoles = signal('');
  protected readonly busquedaPermisos = signal('');

  protected readonly rolSeleccionado = signal<RolSistema | null>(null);
  protected readonly codigosAsignados = signal<Set<string>>(new Set<string>());
  protected readonly guardandoPermisos = signal(false);

  protected readonly modalRolAbierto = signal(false);
  protected readonly rolParaEditar = signal<RolSistema | null>(null);
  protected readonly guardandoRolInfo = signal(false);

  protected readonly rolesFiltrados = computed(() => {
    const q = this.busquedaRoles().trim().toLowerCase();
    const lista = this.roles();
    if (!q) return lista;
    return lista.filter(
      (r) =>
        r.nombre.toLowerCase().includes(q) ||
        (r.descripcion && r.descripcion.toLowerCase().includes(q))
    );
  });

  protected readonly permisosActivosCount = computed(() => this.codigosAsignados().size);

  protected readonly gruposFiltrados = computed<readonly GrupoModulo[]>(() => {
    const q = this.busquedaPermisos().trim().toLowerCase();
    const permisosBD = this.permisosDisponibles();
    const asignados = this.codigosAsignados();

    // Map de permisos por código
    const mapaPermisos = new Map<string, PermisoSistema>();
    for (const p of permisosBD) {
      mapaPermisos.set(p.codigo, p);
    }

    const grupos: GrupoModulo[] = [];

    for (const [key, conf] of Object.entries(MODULOS_CONFIG)) {
      const items: PermisoConEstado[] = [];

      for (const cod of conf.permisos) {
        const pBD = mapaPermisos.get(cod);
        const desc = pBD?.descripcion || cod;

        if (!q || desc.toLowerCase().includes(q) || cod.toLowerCase().includes(q)) {
          items.push({
            codigo: cod,
            descripcion: desc,
            modulo: conf.nombre,
            asignado: asignados.has(cod),
          });
        }
      }

      if (items.length > 0) {
        grupos.push({
          id: key,
          nombre: conf.nombre,
          icono: conf.icono,
          permisos: items,
        });
      }
    }

    return grupos;
  });

  ngOnInit(): void {
    this.cargarDatos();
  }

  cargarDatos(): void {
    this.cargando.set(true);
    this.api.listarRoles().subscribe({
      next: (rs) => {
        this.roles.set(rs);
        this.api.listarPermisos().subscribe({
          next: (ps) => {
            this.permisosDisponibles.set(ps);
            this.cargando.set(false);
            if (rs.length > 0 && !this.rolSeleccionado()) {
              this.seleccionarRol(rs[0]);
            }
          },
          error: () => this.cargando.set(false),
        });
      },
      error: () => this.cargando.set(false),
    });
  }

  seleccionarRol(rol: RolSistema): void {
    this.rolSeleccionado.set(rol);
    this.codigosAsignados.set(new Set(rol.permisos));
  }

  togglePermiso(codigo: string): void {
    const s = new Set(this.codigosAsignados());
    if (s.has(codigo)) {
      s.delete(codigo);
    } else {
      s.add(codigo);
    }
    this.codigosAsignados.set(s);
  }

  marcarTodos(activar: boolean): void {
    if (activar) {
      const todos = new Set(this.permisosDisponibles().map((p) => p.codigo));
      this.codigosAsignados.set(todos);
    } else {
      this.codigosAsignados.set(new Set<string>());
    }
  }

  contarActivosGrupo(grupo: GrupoModulo): number {
    return grupo.permisos.filter((p) => p.asignado).length;
  }

  asIcono(icono: string): any {
    return icono;
  }

  guardarPermisos(): void {
    const rol = this.rolSeleccionado();
    if (!rol) return;

    this.guardandoPermisos.set(true);
    const codigos = Array.from(this.codigosAsignados());

    this.api
      .guardarRol({
        idRol: rol.idRol,
        nombre: rol.nombre,
        descripcion: rol.descripcion,
        activo: rol.activo,
        codigosPermisos: codigos,
      })
      .subscribe({
        next: () => {
          this.guardandoPermisos.set(false);
          this.notificaciones.mostrar(
            'exito',
            'Permisos actualizados',
            `Permisos asignados al rol "${rol.nombre}" guardados en la BD.`
          );
          // Actualizar en el estado local del rol
          const listaActualizada = this.roles().map((r) =>
            r.idRol === rol.idRol ? { ...r, permisos: codigos } : r
          );
          this.roles.set(listaActualizada);
          this.rolSeleccionado.set({ ...rol, permisos: codigos });
        },
        error: (err: any) => {
          this.guardandoPermisos.set(false);
          this.notificaciones.mostrar(
            'error',
            'Error al guardar permisos',
            err.error?.detail || 'No se pudieron actualizar los permisos del rol.'
          );
        },
      });
  }

  abrirModalCrearRol(): void {
    this.rolParaEditar.set(null);
    this.modalRolAbierto.set(true);
  }

  abrirModalEditarRol(rol: RolSistema, event: Event): void {
    event.stopPropagation();
    this.rolParaEditar.set(rol);
    this.modalRolAbierto.set(true);
  }

  guardarRolInfo(evento: GuardarRolEvento): void {
    this.guardandoRolInfo.set(true);
    const permisosActuales = evento.idRol
      ? this.roles().find((r) => r.idRol === evento.idRol)?.permisos ?? []
      : [];

    this.api
      .guardarRol({
        idRol: evento.idRol,
        nombre: evento.nombre,
        descripcion: evento.descripcion,
        activo: evento.activo,
        codigosPermisos: permisosActuales,
      })
      .subscribe({
        next: (idCreado) => {
          this.guardandoRolInfo.set(false);
          this.modalRolAbierto.set(false);
          this.notificaciones.mostrar(
            'exito',
            'Rol guardado',
            evento.idRol ? 'Rol actualizado correctamente.' : 'Rol institucional creado correctamente.'
          );
          this.api.listarRoles().subscribe({
            next: (rs) => {
              this.roles.set(rs);
              const buscado = rs.find((r) => r.idRol === (evento.idRol || idCreado)) || rs[0];
              if (buscado) this.seleccionarRol(buscado);
            },
          });
        },
        error: (err: any) => {
          this.guardandoRolInfo.set(false);
          this.notificaciones.mostrar(
            'error',
            'Error',
            err.error?.detail || 'No se pudo guardar el rol.'
          );
        },
      });
  }
}
