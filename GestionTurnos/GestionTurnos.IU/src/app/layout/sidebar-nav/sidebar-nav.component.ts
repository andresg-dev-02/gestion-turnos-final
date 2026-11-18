import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore, QrAsistenciaService, SiPuedeDirective } from '../../core';
import { UiIconComponent } from '../../shared/ui';
import { ITEMS_MENU_ADMIN, ITEMS_MENU_EMPLEADO } from '../navigation-config.constant';

@Component({
  selector: 'app-sidebar-nav',
  imports: [RouterLink, RouterLinkActive, SiPuedeDirective, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside
      aria-label="Menú lateral del sistema"
      class="hidden min-h-[calc(100dvh-var(--spacing-barra))] w-18 shrink-0 flex-col justify-between border-r border-slate-200 bg-white py-5 md:flex xl:w-64"
    >
      <div class="flex-1">
        @if (!store.esAdmin()) {
          <nav aria-label="Secciones del empleado" class="px-2.5 xl:px-4">
            <p class="mb-2 hidden px-3 text-xs font-bold tracking-wider text-slate-500 uppercase xl:block">
              Mi Espacio
            </p>
            <ul class="space-y-1">
              @for (item of itemsEmpleado; track item.id) {
                <li>
                  <a
                    [routerLink]="item.ruta"
                    [routerLinkActiveOptions]="{ exact: item.ruta === '/mi' }"
                    routerLinkActive="bg-[#f0fdfa] text-[#1D768A] font-bold border-[#1D768A]"
                    [attr.aria-label]="item.etiqueta"
                    class="flex min-h-11 items-center justify-center gap-3 rounded-xl border border-transparent px-3 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-100 hover:text-[#113459] xl:justify-start"
                  >
                    <ui-icon [nombre]="item.icono" />
                    <span class="hidden xl:inline">{{ item.etiqueta }}</span>
                  </a>
                </li>
              }
            </ul>
          </nav>
        }

        @if (store.esAdmin()) {
          <nav aria-label="Secciones de administración" class="px-2.5 xl:px-4">
            <p class="mb-2 hidden px-3 text-xs font-bold tracking-wider text-slate-500 uppercase xl:block">
              Administración
            </p>
            <ul class="space-y-1">
              @for (item of itemsAdmin; track item.id) {
                <li *siPuede="item.permiso">
                  <a
                    [routerLink]="item.ruta"
                    [routerLinkActiveOptions]="{ exact: item.ruta === '/admin' }"
                    routerLinkActive="bg-[#f0fdfa] text-[#1D768A] font-bold border-[#1D768A]"
                    [attr.aria-label]="item.etiqueta"
                    class="flex min-h-11 items-center justify-center gap-3 rounded-xl border border-transparent px-3 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-100 hover:text-[#113459] xl:justify-start"
                  >
                    <ui-icon [nombre]="item.icono" />
                    <span class="hidden xl:inline">{{ item.etiqueta }}</span>
                  </a>
                </li>
              }
            </ul>
          </nav>
        }
      </div>

      <!-- Acceso a Pantalla Tótem QR: Full inferior del Sidebar Admin en Modo Escritorio (oculto en PWA) -->
      @if (store.esAdmin() && !esPwa()) {
        <div class="mt-auto px-2.5 pt-4 border-t border-slate-200 xl:px-4">
          <a
            routerLink="/totem-qr"
            target="_blank"
            class="group flex min-h-11 items-center justify-center gap-3 rounded-xl border border-[#99f6e4] bg-[#f0fdfa] px-3 py-2.5 text-sm font-semibold text-[#113459] transition hover:bg-[#ccfbf1] hover:text-[#0c2540] hover:border-[#5eead4] xl:justify-start"
            title="Abrir pantalla tótem de acceso (monitor o tablet de entrada)"
            aria-label="Abrir pantalla tótem QR"
          >
            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[#1D768A] text-white shadow-xs group-hover:scale-105 transition-transform">
              <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z"
                />
              </svg>
            </span>
            <div class="hidden flex-col text-left xl:flex">
              <span class="text-xs font-bold leading-tight text-[#113459]">Tótem QR Acceso</span>
              <span class="text-[10px] text-[#1D768A] font-medium leading-tight">Pantalla para entrada</span>
            </div>
          </a>
        </div>
      }
    </aside>
  `,
})
export class SidebarNavComponent {
  protected readonly store = inject(AuthStore);
  private readonly qrService = inject(QrAsistenciaService);
  protected readonly esPwa = signal(this.qrService.esModoPwa());
  protected readonly itemsEmpleado = ITEMS_MENU_EMPLEADO;
  protected readonly itemsAdmin = ITEMS_MENU_ADMIN;
}
