import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AuthSplitLayoutComponent } from '../../../app/shared/ui';
import { LoginFormComponent } from './components/login-form.component';

/**
 * Página recopilatoria de Inicio de Sesión (/ingresar).
 * Ensambla el layout dividido (ilustración oficial + tarjeta flotante) y el formulario de acceso.
 */
@Component({
  selector: 'app-login-page',
  imports: [AuthSplitLayoutComponent, LoginFormComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main id="contenido-principal" tabindex="-1" class="focus:outline-none">
      <ui-auth-split-layout
        titulo="Ingresa a tu cuenta"
        subtitulo="Consulta tus turnos, asistencia y solicitudes laborales"
      >
        <app-login-form />
      </ui-auth-split-layout>
    </main>
  `,
})
export class LoginPageComponent {}
