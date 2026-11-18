import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { CodigoPermiso } from '../models';
import { AuthStore } from './auth.store';

/**
 * Directiva estructural que renderiza su plantilla únicamente cuando el usuario
 * posee el código de permiso especificado en `seguridad.permiso`.
 */
@Directive({
  selector: '[siPuede]',
})
export class SiPuedeDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly authStore = inject(AuthStore);
  private renderizado = false;

  readonly siPuede = input.required<CodigoPermiso | undefined>();

  constructor() {
    effect(() => {
      const codigo = this.siPuede();
      const autorizado = !codigo || this.authStore.puede(codigo);

      if (autorizado && !this.renderizado) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.renderizado = true;
      } else if (!autorizado && this.renderizado) {
        this.viewContainer.clear();
        this.renderizado = false;
      }
    });
  }
}
