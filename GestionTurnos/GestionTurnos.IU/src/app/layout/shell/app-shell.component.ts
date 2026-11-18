import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MobileDockComponent } from '../mobile-dock/mobile-dock.component';
import { OfflineBannerComponent } from '../offline-banner/offline-banner.component';
import { PwaUpdateBannerComponent } from '../pwa-update-banner/pwa-update-banner.component';
import { SidebarNavComponent } from '../sidebar-nav/sidebar-nav.component';
import { TopbarComponent } from '../topbar/topbar.component';

/**
 * Componente recopilatorio del armazón principal (Layout PWA).
 * Únicamente orquesta la barra superior, avisos de estado/PWA, menú lateral,
 * área de contenido principal y el dock flotante inferior móvil.
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    TopbarComponent,
    OfflineBannerComponent,
    PwaUpdateBannerComponent,
    SidebarNavComponent,
    MobileDockComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-dvh bg-slate-50 text-slate-900">
      <app-offline-banner />
      <app-pwa-update-banner />
      <app-topbar />

      <div class="flex min-h-[calc(100dvh-var(--spacing-barra))] w-full">
        <app-sidebar-nav />

        <main
          id="contenido-principal"
          tabindex="-1"
          class="flex-1 px-4 py-6 pb-[var(--spacing-navinf)] focus:outline-none sm:px-6 lg:px-8 md:pb-8"
        >
          <div class="mx-auto w-full max-w-6xl">
            <router-outlet />
          </div>
        </main>
      </div>

      <app-mobile-dock />
    </div>
  `,
})
export class AppShellComponent {}
