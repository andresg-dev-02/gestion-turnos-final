import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PestanaItem, UiTabsComponent } from '../../../app/shared/ui';
import { CatalogoSimpleSectionComponent } from './secciones/catalogo-simple.section';
import { CategoriasHoraSectionComponent } from './secciones/categorias-hora.section';
import { DiasFestivosSectionComponent } from './secciones/dias-festivos.section';
import { ParametrosSectionComponent } from './secciones/parametros.section';
import { RolesPermisosSectionComponent } from './secciones/roles-permisos.section';
import { TiposNovedadSectionComponent } from './secciones/tipos-novedad.section';

const PESTANAS: readonly PestanaItem[] = [
  { id: 'roles', etiqueta: 'Roles y permisos' },
  { id: 'parametros', etiqueta: 'Parámetros de negocio' },
  { id: 'festivos', etiqueta: 'Días festivos' },
  { id: 'novedades', etiqueta: 'Tipos de novedad' },
  { id: 'horas', etiqueta: 'Categorías de hora' },
  { id: 'documentos', etiqueta: 'Tipos de documento' },
  { id: 'contratos', etiqueta: 'Tipos de contrato' },
];

/**
 * Módulo de Configuración (/admin/configuracion): CRUD de las entidades del
 * esquema "parametrizacion". La pestaña activa se refleja en ?seccion= para
 * poder enlazarla directamente.
 *
 * Los catálogos de estados (usuario, empleado, solicitud, asignación), fuentes de
 * marcación y tipos de acción de auditoría no se editan aquí: el código de la API
 * depende de sus valores fijos.
 */
@Component({
  selector: 'app-configuracion-page',
  imports: [
    UiTabsComponent,
    CatalogoSimpleSectionComponent,
    CategoriasHoraSectionComponent,
    DiasFestivosSectionComponent,
    ParametrosSectionComponent,
    RolesPermisosSectionComponent,
    TiposNovedadSectionComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-configuracion" class="space-y-5">
      <header>
        <h1 id="titulo-configuracion" class="text-2xl font-extrabold tracking-tight text-slate-900">
          Configuración
        </h1>
        <p class="mt-1 text-sm text-slate-600">
          Catálogos y reglas de negocio del sistema (esquema de parametrización).
        </p>
      </header>

      <ui-tabs etiqueta="Secciones de configuración" [pestanas]="pestanas" [activa]="activa()" (cambio)="cambiar($event)" />

      <div role="tabpanel" [attr.aria-labelledby]="'tab-' + activa()" class="tarjeta">
        @switch (activa()) {
          @case ('roles') { <app-roles-permisos-section /> }
          @case ('parametros') { <app-parametros-section /> }
          @case ('festivos') { <app-dias-festivos-section /> }
          @case ('novedades') { <app-tipos-novedad-section /> }
          @case ('horas') { <app-categorias-hora-section /> }
          @case ('documentos') { <app-catalogo-simple-section tipo="documento" /> }
          @case ('contratos') { <app-catalogo-simple-section tipo="contrato" /> }
        }
      </div>
    </section>
  `,
})
export class ConfiguracionPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pestanas = PESTANAS;
  protected readonly activa = signal(this.seccionInicial());

  private seccionInicial(): string {
    const s = this.route.snapshot.queryParamMap.get('seccion');
    return PESTANAS.some((p) => p.id === s) ? (s as string) : 'parametros';
  }

  cambiar(id: string): void {
    this.activa.set(id);
    void this.router.navigate([], { queryParams: { seccion: id }, replaceUrl: true });
  }
}
