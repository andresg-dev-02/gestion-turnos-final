import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Empleado } from '../../../../app/shared/data';
import { UiBadgeComponent } from '../../../../app/shared/ui';

@Component({
  selector: 'app-empleados-table',
  imports: [UiBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tabla-contenedor overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
      <table class="tabla w-full text-left text-sm" aria-label="Listado general de empleados">
        <thead class="border-b border-slate-200 bg-slate-50 text-xs font-semibold text-slate-700">
          <tr>
            <th scope="col" class="px-4 py-3">Documento</th>
            <th scope="col" class="px-4 py-3">Empleado</th>
            <th scope="col" class="px-4 py-3">Contacto</th>
            <th scope="col" class="px-4 py-3">Área / Sucursal</th>
            <th scope="col" class="px-4 py-3">Cargo</th>
            <th scope="col" class="px-4 py-3">Estado</th>
            <th scope="col" class="px-4 py-3 text-right">Acciones</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100 text-slate-800">
          @for (emp of empleados(); track emp.idEmpleado) {
            <tr class="transition-colors hover:bg-slate-50/70">
              <td class="px-4 py-3 font-mono text-xs font-semibold text-slate-600">
                {{ emp.codTipoDocumento }} {{ emp.numeroDocumento }}
              </td>
              <td class="px-4 py-3">
                <div class="flex items-center gap-2.5">
                  <div
                    class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[#1D768A]/15 font-bold text-[#113459]"
                    aria-hidden="true"
                  >
                    {{ emp.nombres[0] }}{{ emp.apellidos[0] }}
                  </div>
                  <div>
                    <span class="font-bold text-slate-900">{{ emp.nombres }} {{ emp.apellidos }}</span>
                    @if (emp.correoUsuario) {
                      <span class="block text-xs text-slate-500">{{ emp.correoUsuario }}</span>
                    }
                  </div>
                </div>
              </td>
              <td class="px-4 py-3 text-xs">
                <div>{{ emp.correoContacto || emp.correoUsuario || '—' }}</div>
                @if (emp.telefono) {
                  <div class="text-slate-500">{{ emp.telefono }}</div>
                }
              </td>
              <td class="px-4 py-3 text-xs">
                <span class="font-medium text-slate-800">{{ emp.nombreArea }}</span>
                <span class="block text-slate-500">{{ emp.nombreSucursal }}</span>
              </td>
              <td class="px-4 py-3 text-xs font-medium text-slate-700">
                {{ emp.nombreCargo }}
              </td>
              <td class="px-4 py-3">
                <ui-badge [estado]="emp.codEstadoEmpleado" />
              </td>
              <td class="px-4 py-3 text-right whitespace-nowrap">
                <button
                  type="button"
                  class="accion-secundaria text-xs"
                  (click)="editar.emit(emp)"
                  [attr.aria-label]="'Editar empleado ' + emp.nombres + ' ' + emp.apellidos"
                >
                  Editar
                </button>
                @if (emp.codEstadoEmpleado === 'ACTIVO') {
                  <button
                    type="button"
                    class="accion-peligro ml-2 text-xs"
                    (click)="desactivar.emit(emp)"
                    [attr.aria-label]="'Desactivar empleado ' + emp.nombres + ' ' + emp.apellidos"
                  >
                    Desactivar
                  </button>
                } @else {
                  <button
                    type="button"
                    class="accion-exito ml-2 text-xs"
                    (click)="reactivar.emit(emp)"
                    [attr.aria-label]="'Reactivar empleado ' + emp.nombres + ' ' + emp.apellidos"
                  >
                    Reactivar
                  </button>
                }
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7" class="py-12 text-center text-slate-500">
                <div class="flex flex-col items-center justify-center space-y-1">
                  <p class="font-semibold text-slate-700">Aún no hay registros</p>
                  <p class="text-xs text-slate-500">No se encontraron empleados con los criterios de búsqueda.</p>
                </div>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class EmpleadosTableComponent {
  readonly empleados = input.required<readonly Empleado[]>();
  readonly editar = output<Empleado>();
  readonly desactivar = output<Empleado>();
  readonly reactivar = output<Empleado>();
}
