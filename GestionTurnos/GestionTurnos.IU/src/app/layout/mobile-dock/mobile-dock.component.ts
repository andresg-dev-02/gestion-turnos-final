import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '../../core';
import { UiIconComponent } from '../../shared/ui';
import { ITEMS_DOCK_ADMIN, ITEMS_DOCK_MOVIL } from '../navigation-config.constant';

/**
 * Barra de navegación inferior flotante tipo cápsula (PWA Mobile Dock).
 * Dinámico según el rol del usuario:
 * - Rol Empleado: Inicio (/mi), Turnos (/mi/calendario), Marcar (+) (/mi/asistencia), Novedades (/mi/novedades), Perfil (/mi/perfil)
 * - Rol Administrador: Panel (/admin), Solicitudes (/admin/solicitudes), Programar (+) (/admin/programacion), Empleados (/admin/empleados), Ajustes (/admin/configuracion)
 */
@Component({
  selector: 'app-mobile-dock',
  imports: [RouterLink, RouterLinkActive, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav
      aria-label="Navegación principal móvil"
      class="safe-bottom-dock pointer-events-none fixed inset-x-0 bottom-0 z-40 flex justify-center px-4 pt-2 md:hidden"
    >
      <ul
        class="pointer-events-auto flex w-full max-w-md items-center justify-around rounded-full border border-slate-200/80 bg-white/95 px-3 py-2 shadow-[0_16px_40px_-6px_rgba(17,52,89,0.18),0_4px_12px_rgba(0,0,0,0.06)] backdrop-blur-md"
      >
        @for (item of itemsDock(); track item.id) {
          <li class="relative flex flex-col items-center justify-center">
            @if (item.destacadoCentral) {
              <!-- Botón central flotante con gradiente turquesa/menta y sombra suave -->
              <a
                [routerLink]="item.ruta"
                [attr.aria-label]="item.etiqueta"
                class="flex h-13 w-13 items-center justify-center rounded-full bg-gradient-to-b from-[#1D768A] to-[#25a891] text-white shadow-[0_8px_20px_rgba(29,118,138,0.38)] transition-all hover:scale-105 active:scale-95 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[#1D768A]/30"
              >
                <ui-icon [nombre]="item.icono" />
                <span class="sr-only">{{ item.etiqueta }}</span>
              </a>
            } @else {
              <!-- Íconos de navegación con estado activo circular suave y punto indicador -->
              <a
                [routerLink]="item.ruta"
                [routerLinkActiveOptions]="{ exact: item.ruta === '/mi' || item.ruta === '/admin' }"
                #rla="routerLinkActive"
                routerLinkActive="active-dock-item"
                [attr.aria-label]="item.etiqueta"
                class="group relative flex min-h-11 min-w-11 items-center justify-center rounded-full transition-all active:scale-95 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[#1D768A]/25"
                [class.bg-[#dbe8f4]]="rla.isActive"
                [class.text-[#113459]]="rla.isActive"
                [class.text-[#96a1b3]]="!rla.isActive"
                [class.hover:text-[#113459]]="!rla.isActive"
                [class.hover:bg-slate-100/80]="!rla.isActive"
              >
                <ui-icon [nombre]="item.icono" />
                <span class="sr-only">{{ item.etiqueta }}</span>

                <!-- Punto indicador inferior del elemento activo -->
                @if (rla.isActive) {
                  <span
                    class="absolute -bottom-1 h-1.25 w-1.25 rounded-full bg-[#1D768A]"
                    aria-hidden="true"
                  ></span>
                }
              </a>
            }
          </li>
        }
      </ul>
    </nav>
  `,
})
export class MobileDockComponent {
  private readonly auth = inject(AuthStore);

  protected readonly itemsDock = computed(() =>
    this.auth.esAdmin() ? ITEMS_DOCK_ADMIN : ITEMS_DOCK_MOVIL
  );
}
