import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AuthService,
  AuthStore,
  ConexionService,
  LoadingStateService,
  PwaUpdateService,
  RelojServidorService,
} from '../../core';
import { FotoPerfilStore } from '../../shared/data/foto-perfil.store';
import { AvatarPerfilComponent } from './avatar-perfil.component';
import { FotoPerfilDialogComponent } from './foto-perfil-dialog.component';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink, AvatarPerfilComponent, FotoPerfilDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header
      class="sticky top-0 z-30 flex h-[var(--spacing-barra)] w-full items-center justify-between border-b border-slate-200 bg-white/95 px-4 backdrop-blur-md sm:px-6"
    >
      @if (cargando.cargando()) {
        <div
          role="progressbar"
          aria-label="Cargando datos del servidor"
          class="fixed top-0 left-0 z-50 h-1 w-full animate-pulse bg-[#1D768A]"
        ></div>
      }

      <div class="flex items-center gap-3">
        <a
          [routerLink]="store.esAdmin() ? '/admin' : '/mi'"
          class="flex min-h-11 items-center gap-2.5 rounded-lg px-1 font-bold tracking-tight text-[#113459]"
        >
          <img
            src="/Logo_APP.png"
            alt="Logo Gestión de Turnos"
            width="36"
            height="36"
            class="h-9 w-9 shrink-0 object-contain"
          />
          <span class="text-base font-extrabold text-[#113459]">Gestión de Turnos</span>
        </a>
      </div>

      <div class="flex items-center gap-3">
        <div
          class="hidden items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-700 md:flex"
          aria-label="Hora oficial del sistema (Bogotá)"
        >
          <span
            class="h-2 w-2 rounded-full"
            [class.bg-[#31BBA1]]="conexion.enLinea()"
            [class.bg-amber-600]="!conexion.enLinea()"
            aria-hidden="true"
          ></span>
          <span>{{ reloj.fechaBogota() }}</span>
          <span class="font-mono font-bold text-[#113459]">{{ reloj.horaBogota() }}</span>
        </div>

        @if (pwa.instalable()) {
          <button
            type="button"
            (click)="pwa.instalarPwa()"
            class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-[#1D768A] bg-[#f0fdfa] px-3 py-1.5 text-xs font-bold text-[#1D768A] hover:bg-[#ccfbf1]"
          >
            Instalar<span class="hidden sm:inline">&nbsp;App</span>
          </button>
        }

        <div class="flex items-center gap-2">
          <button
            type="button"
            (click)="dialogoFotoAbierto.set(true)"
            aria-label="Cambiar foto de perfil"
            title="Cambiar foto de perfil"
            class="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg px-1.5 hover:bg-slate-100"
          >
            <span class="hidden text-right text-xs sm:block">
              <strong class="block font-semibold text-[#113459]">
                {{ store.usuario()?.nombreCompleto || 'Usuario' }}
              </strong>
              <span class="text-slate-500">{{ store.usuario()?.rol || 'Empleado' }}</span>
            </span>
            <app-avatar-perfil [url]="fotos.url()" [nombre]="store.usuario()?.nombreCompleto || ''" />
          </button>

          <button
            type="button"
            (click)="auth.cerrarSesion()"
            class="inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-800 hover:bg-slate-100"
          >
            Salir
          </button>
        </div>
      </div>
    </header>

    <app-foto-perfil-dialog [abierto]="dialogoFotoAbierto()" (cerrar)="dialogoFotoAbierto.set(false)" />
  `,
})
export class TopbarComponent {
  protected readonly store = inject(AuthStore);
  protected readonly auth = inject(AuthService);
  protected readonly reloj = inject(RelojServidorService);
  protected readonly conexion = inject(ConexionService);
  protected readonly cargando = inject(LoadingStateService);
  protected readonly pwa = inject(PwaUpdateService);
  protected readonly fotos = inject(FotoPerfilStore);
  protected readonly dialogoFotoAbierto = signal(false);
}
