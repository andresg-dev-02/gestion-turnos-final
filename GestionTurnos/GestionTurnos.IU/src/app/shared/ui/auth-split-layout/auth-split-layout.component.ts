import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AuthFloatingCardComponent } from '../auth-floating-card/auth-floating-card.component';
import { AuthIllustrationPlaceholderComponent } from '../auth-illustration-placeholder/auth-illustration-placeholder.component';

/**
 * Componente recopilatorio para las pantallas de Ingreso (/ingresar) y Registro (/registro).
 * Ensambla la columna de la ilustración oficial limpia y la tarjeta flotante con el formulario.
 */
@Component({
  selector: 'ui-auth-split-layout',
  imports: [AuthIllustrationPlaceholderComponent, AuthFloatingCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex min-h-dvh w-full flex-col justify-center bg-white px-4 py-10 sm:px-8 lg:px-16">
      <div class="mx-auto grid w-full max-w-6xl grid-cols-1 items-center gap-12 lg:grid-cols-12">
        <div class="flex justify-center lg:col-span-6">
          <ui-auth-illustration-placeholder />
        </div>
        <div class="lg:col-span-6">
          <ui-auth-floating-card [titulo]="titulo()" [subtitulo]="subtitulo()">
            <ng-content />
          </ui-auth-floating-card>
        </div>
      </div>
    </div>
  `,
})
export class AuthSplitLayoutComponent {
  readonly titulo = input.required<string>();
  readonly subtitulo = input('');
}
