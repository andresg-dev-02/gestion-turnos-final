import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiClient, AuthService, NotificationService, PwaUpdateService } from '../../../../app/core';
import { UiButtonComponent, UiCheckComponent, UiNotchedFieldComponent } from '../../../../app/shared/ui';

@Component({
  selector: 'app-login-form',
  imports: [RouterLink, UiNotchedFieldComponent, UiButtonComponent, UiCheckComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form (submit)="enviar($event)" novalidate class="space-y-5">
      @if (errorGeneral()) {
        <div
          role="alert"
          class="rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-xs font-medium text-red-900"
        >
          {{ errorGeneral() }}
        </div>
      }

      <ui-notched-field
        id="login-correo"
        etiqueta="Correo electrónico"
        tipo="email"
        placeholder="usuario@empresa.com"
        autocomplete="email"
        [obligatorio]="true"
        [valor]="correo()"
        [error]="erroresCampo()['correo']"
        (valorCambio)="correo.set($event)"
      />

      <ui-notched-field
        id="login-contrasena"
        etiqueta="Contraseña"
        tipo="password"
        placeholder="Ingresa tu contraseña"
        autocomplete="current-password"
        [obligatorio]="true"
        [valor]="contrasena()"
        [error]="erroresCampo()['contrasena']"
        (valorCambio)="contrasena.set($event)"
      />

      <ui-check
        id="login-recordar"
        etiqueta="Mantener la sesión iniciada"
        ayuda="No vuelvas a ingresar en este dispositivo. No lo actives en equipos compartidos."
        [marcado]="recordar()"
        (marcadoCambio)="recordar.set($event)"
      />

      <div class="pt-1">
        <ui-button tipo="submit" [anchoCompleto]="true" [cargando]="enviando()">
          Iniciar sesión
        </ui-button>
      </div>

      <p class="pt-2 text-center text-xs text-slate-600">
        ¿Eres empleado nuevo y aún no tienes cuenta?
        <a
          routerLink="/registro"
          class="ml-1 inline-flex min-h-11 items-center font-bold text-[#1D768A] underline underline-offset-4 hover:text-[#113459]"
        >
          Solicitar registro
        </a>
      </p>
    </form>
  `,
})
export class LoginFormComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(NotificationService);

  readonly correo = signal('');
  readonly contrasena = signal('');
  /** Activado por defecto en el celular (app instalada o navegador móvil); desactivado en computadores. */
  readonly recordar = signal(inject(PwaUpdateService).esMovil || PwaUpdateService.detectarStandalone());
  readonly enviando = signal(false);
  readonly errorGeneral = signal<string | null>(null);
  readonly erroresCampo = signal<Record<string, string>>({});

  enviar(evento: Event): void {
    evento.preventDefault();
    this.errorGeneral.set(null);
    const errores: Record<string, string> = {};

    if (!this.correo().trim()) {
      errores['correo'] = 'El correo electrónico es obligatorio.';
    }
    if (!this.contrasena()) {
      errores['contrasena'] = 'La contraseña es obligatoria.';
    }
    if (Object.keys(errores).length > 0) {
      this.erroresCampo.set(errores);
      return;
    }

    this.erroresCampo.set({});
    this.enviando.set(true);

    this.auth
      .iniciarSesion({ correo: this.correo().trim(), contrasena: this.contrasena(), recordar: this.recordar() })
      .subscribe({
        next: () => {
          this.enviando.set(false);
          this.toast.exito('Bienvenido al sistema de Gestión de Turnos.');
          void this.router.navigate([this.auth.rutaInicialSegunPermisos()]);
        },
        error: (err: unknown) => {
          this.enviando.set(false);
          const extraido = ApiClient.extraerMensajeError(err);
          this.errorGeneral.set(extraido.general);
          this.erroresCampo.set(extraido.campos);
        },
      });
  }
}
