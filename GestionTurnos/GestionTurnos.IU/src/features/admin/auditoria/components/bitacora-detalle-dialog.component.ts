import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe, KeyValuePipe } from '@angular/common';
import { BitacoraNegocio } from '../../../../app/shared/data';
import { UiButtonComponent, UiDialogComponent } from '../../../../app/shared/ui';

interface DiferenciaCampo {
  campo: string;
  anterior: unknown;
  nuevo: unknown;
}

@Component({
  selector: 'app-bitacora-detalle-dialog',
  imports: [DatePipe, KeyValuePipe, UiDialogComponent, UiButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="modal-bitacora-detalle"
      titulo="Detalle de Auditoría"
      [subtitulo]="subtitulo()"
      [abierto]="abierto()"
      ancho="lg"
      (cerrar)="cerrar.emit()"
    >
      @if (registro(); as reg) {
        <div class="space-y-6 text-sm">
          <!-- Metadatos de la Operación -->
          <div class="grid grid-cols-1 gap-4 rounded-xl border border-slate-200 bg-slate-50/70 p-4 sm:grid-cols-2">
            <div>
              <span class="block text-xs font-medium text-slate-500">Fecha y Hora</span>
              <span class="font-semibold text-slate-900">{{ reg.fechaHora | date:'dd/MM/yyyy, hh:mm:ss a' }}</span>
            </div>
            <div>
              <span class="block text-xs font-medium text-slate-500">Tipo de Acción</span>
              <span class="inline-flex items-center gap-1.5 font-bold uppercase tracking-wide text-slate-800">
                <span class="h-2 w-2 rounded-full" [class]="puntoColor(reg.codTipoAccion)"></span>
                {{ reg.nombreTipoAccion || reg.codTipoAccion }}
              </span>
            </div>
            <div>
              <span class="block text-xs font-medium text-slate-500">Ubicación</span>
              <span class="font-mono text-xs font-medium text-slate-800">{{ reg.esquema }}.{{ reg.tabla }} (ID: {{ reg.idRegistro }})</span>
            </div>
            <div>
              <span class="block text-xs font-medium text-slate-500">Usuario Autor</span>
              <span class="font-medium text-slate-900">{{ reg.correoUsuario || 'Sistema / Anónimo' }}</span>
              @if (reg.ipOrigen) {
                <span class="ml-1 text-xs text-slate-500">({{ reg.ipOrigen }})</span>
              }
            </div>
            @if (reg.nombreEmpleado) {
              <div class="sm:col-span-2">
                <span class="block text-xs font-medium text-slate-500">Empleado Afectado</span>
                <span class="font-medium text-slate-900">{{ reg.nombreEmpleado }} (ID: {{ reg.idEmpleado }})</span>
              </div>
            }
          </div>

          <!-- Comparador de Cambios (MODIFICAR) -->
          @if (reg.codTipoAccion === 'MODIFICAR') {
            <div>
              <h3 class="mb-2 text-xs font-bold uppercase tracking-wider text-slate-600">
                Campos Modificados (Diferencias)
              </h3>
              @if (diferencias().length > 0) {
                <div class="overflow-hidden rounded-xl border border-slate-200">
                  <table class="w-full text-left text-xs">
                    <thead class="border-b border-slate-200 bg-slate-100 font-semibold text-slate-700">
                      <tr>
                        <th class="px-3.5 py-2.5">Campo</th>
                        <th class="px-3.5 py-2.5">Valor Anterior</th>
                        <th class="px-3.5 py-2.5">Valor Nuevo</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-200 bg-white">
                      @for (dif of diferencias(); track dif.campo) {
                        <tr>
                          <td class="whitespace-nowrap px-3.5 py-2 font-mono font-medium text-slate-800">
                            {{ dif.campo }}
                          </td>
                          <td class="px-3.5 py-2 font-mono text-rose-800 bg-rose-50/50">
                            {{ formatearValor(dif.anterior) }}
                          </td>
                          <td class="px-3.5 py-2 font-mono text-emerald-800 bg-emerald-50/50">
                            {{ formatearValor(dif.nuevo) }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              } @else {
                <p class="rounded-lg border border-slate-200 bg-slate-50 p-3 text-xs text-slate-500">
                  No se registraron diferencias específicas en los campos.
                </p>
              }
            </div>
          }

          <!-- Valores Creados (CREAR) -->
          @if (reg.codTipoAccion === 'CREAR') {
            <div>
              <h3 class="mb-2 text-xs font-bold uppercase tracking-wider text-slate-600">
                Valores Registrados
              </h3>
              @if (objetoNuevo(); as nuevo) {
                <div class="overflow-hidden rounded-xl border border-slate-200">
                  <table class="w-full text-left text-xs">
                    <thead class="border-b border-slate-200 bg-slate-100 font-semibold text-slate-700">
                      <tr>
                        <th class="px-3.5 py-2.5">Campo</th>
                        <th class="px-3.5 py-2.5">Valor Inicial</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-200 bg-white">
                      @for (item of nuevo | keyvalue; track item.key) {
                        <tr>
                          <td class="whitespace-nowrap px-3.5 py-2 font-mono font-medium text-slate-800">
                            {{ item.key }}
                          </td>
                          <td class="px-3.5 py-2 font-mono text-emerald-800">
                            {{ formatearValor(item.value) }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          }

          <!-- Valores Eliminados (ELIMINAR) -->
          @if (reg.codTipoAccion === 'ELIMINAR') {
            <div>
              <h3 class="mb-2 text-xs font-bold uppercase tracking-wider text-slate-600">
                Valores del Registro Eliminado
              </h3>
              @if (objetoAnterior(); as anterior) {
                <div class="overflow-hidden rounded-xl border border-slate-200">
                  <table class="w-full text-left text-xs">
                    <thead class="border-b border-slate-200 bg-slate-100 font-semibold text-slate-700">
                      <tr>
                        <th class="px-3.5 py-2.5">Campo</th>
                        <th class="px-3.5 py-2.5">Valor Previo</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-200 bg-white">
                      @for (item of anterior | keyvalue; track item.key) {
                        <tr>
                          <td class="whitespace-nowrap px-3.5 py-2 font-mono font-medium text-slate-800">
                            {{ item.key }}
                          </td>
                          <td class="px-3.5 py-2 font-mono text-rose-800">
                            {{ formatearValor(item.value) }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          }
        </div>
      }

      <div acciones>
        <ui-button variante="secundario" (click)="cerrar.emit()">
          Cerrar
        </ui-button>
      </div>
    </ui-dialog>
  `,
})
export class BitacoraDetalleDialogComponent {
  readonly abierto = input(false);
  readonly registro = input<BitacoraNegocio | null>(null);
  readonly cerrar = output<void>();

  protected readonly subtitulo = computed(() => {
    const r = this.registro();
    if (!r) return null;
    return `${r.esquema}.${r.tabla} · Registro #${r.idRegistro}`;
  });

  protected readonly objetoAnterior = computed<Record<string, unknown> | null>(() => {
    const r = this.registro();
    if (!r?.valorAnterior) return null;
    try {
      return JSON.parse(r.valorAnterior);
    } catch {
      return null;
    }
  });

  protected readonly objetoNuevo = computed<Record<string, unknown> | null>(() => {
    const r = this.registro();
    if (!r?.valorNuevo) return null;
    try {
      return JSON.parse(r.valorNuevo);
    } catch {
      return null;
    }
  });

  protected readonly diferencias = computed<readonly DiferenciaCampo[]>(() => {
    const ant = this.objetoAnterior() ?? {};
    const nue = this.objetoNuevo() ?? {};

    const claves = Array.from(new Set([...Object.keys(ant), ...Object.keys(nue)]));
    return claves.map((campo) => ({
      campo,
      anterior: ant[campo],
      nuevo: nue[campo],
    }));
  });

  protected puntoColor(codAccion: string): string {
    switch (codAccion?.toUpperCase()) {
      case 'CREAR':
      case 'APROBAR':
        return 'bg-emerald-500';
      case 'MODIFICAR':
        return 'bg-sky-500';
      case 'ELIMINAR':
      case 'RECHAZAR':
        return 'bg-rose-500';
      case 'INICIO_SESION':
        return 'bg-indigo-500';
      default:
        return 'bg-slate-400';
    }
  }

  protected formatearValor(valor: unknown): string {
    if (valor === null || valor === undefined) return 'null';
    if (typeof valor === 'boolean') return valor ? 'true' : 'false';
    if (typeof valor === 'object') return JSON.stringify(valor);
    return String(valor);
  }
}
