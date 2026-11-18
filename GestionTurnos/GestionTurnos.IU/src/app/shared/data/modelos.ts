/**
 * Modelos que reflejan los DTO de la API (camelCase por System.Text.Json).
 * DateOnly → 'YYYY-MM-DD', TimeOnly → 'HH:mm:ss', DateTimeOffset → ISO 8601.
 */

export interface PaginaApi<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly pageNumber: number;
  readonly pageSize: number;
}

// ---------- Parametrización ----------
export interface CatalogoItem {
  readonly codigo: string;
  readonly nombre: string;
  readonly activo: boolean;
}

export interface TipoNovedad {
  readonly codTipoNovedad: string;
  readonly nombre: string;
  readonly remuneradoDefecto: boolean;
  readonly afectaHorasDefecto: boolean;
  readonly requiereAprobacion: boolean;
  readonly requiereSoporte: boolean;
  readonly activo: boolean;
}

export interface CategoriaHora {
  readonly codCategoriaHora: string;
  readonly nombre: string;
  readonly factorPago: number;
  readonly activo: boolean;
}

export interface DiaFestivo {
  readonly fecha: string;
  readonly nombre: string;
}

export interface Parametro {
  readonly codParametro: string;
  readonly nombre: string;
  readonly descripcion: string | null;
  readonly tipoDato: 'ENTERO' | 'DECIMAL' | 'TEXTO' | 'BOOLEANO' | 'HORA';
  readonly editable: boolean;
  readonly valorVigente: string | null;
  readonly vigenteDesde: string | null;
}

export interface ParametroValor {
  readonly idParametroValor: number;
  readonly codParametro: string;
  readonly valor: string;
  readonly vigenteDesde: string;
  readonly vigenteHasta: string | null;
}

// ---------- Organización ----------
export interface Sucursal {
  readonly idSucursal: number;
  readonly nombre: string;
  readonly direccion: string | null;
  readonly ciudad: string | null;
  readonly activo: boolean;
}

export interface Area {
  readonly idArea: number;
  readonly idSucursal: number;
  readonly nombreSucursal: string;
  readonly nombre: string;
  readonly activo: boolean;
}

export interface Cargo {
  readonly idCargo: number;
  readonly nombre: string;
  readonly activo: boolean;
}

export interface Empleado {
  readonly idEmpleado: number;
  readonly idUsuario: number | null;
  readonly correoUsuario: string | null;
  readonly codTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correoContacto?: string | null;
  readonly telefono?: string | null;
  readonly direccion?: string | null;
  readonly idArea: number;
  readonly nombreArea: string;
  readonly idSucursal?: number;
  readonly nombreSucursal: string;
  readonly idCargo: number;
  readonly nombreCargo: string;
  readonly codTipoContrato?: string | null;
  readonly fechaIngreso?: string;
  readonly fechaRetiro?: string | null;
  readonly codEstadoEmpleado: string;
}

export interface CrearEmpleadoDto {
  readonly codTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correoContacto?: string | null;
  readonly telefono?: string | null;
  readonly direccion?: string | null;
  readonly idArea: number;
  readonly idCargo: number;
  readonly codTipoContrato?: string | null;
  readonly fechaIngreso: string;
}

export interface ActualizarEmpleadoDto {
  readonly codTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correoContacto?: string | null;
  readonly telefono?: string | null;
  readonly direccion?: string | null;
  readonly idArea: number;
  readonly idCargo: number;
  readonly codTipoContrato?: string | null;
  readonly fechaIngreso: string;
}

export interface PerfilEmpleado {
  readonly idEmpleado: number;
  readonly codTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correoContacto: string | null;
  readonly telefono: string | null;
  readonly direccion: string | null;
  readonly fechaIngreso: string;
  readonly cargo: string;
  readonly area: string;
  readonly sucursal: string;
  readonly tipoContrato: string | null;
  readonly estado: string;
}

// ---------- Solicitudes ----------
export interface SolicitudRegistro {
  readonly idSolicitudRegistro: number;
  readonly codTipoDocumento: string;
  readonly nombreTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correo: string;
  readonly telefono: string | null;
  readonly codEstadoSolicitud: string;
  readonly nombreEstadoSolicitud: string;
  readonly solicitadoEn: string;
  readonly revisadoEn: string | null;
  readonly motivoRechazo: string | null;
}

export interface SolicitudVacaciones {
  readonly idSolicitudVacaciones: number;
  readonly idEmpleado: number;
  readonly nombreEmpleado: string;
  readonly fechaInicio: string;
  readonly fechaFin: string;
  readonly diasSolicitados: number;
  readonly codEstadoSolicitud: string;
  readonly nombreEstadoSolicitud: string;
  readonly observacion: string | null;
  readonly solicitadoEn: string;
  readonly resueltoEn: string | null;
  readonly motivoRechazo: string | null;
}

export interface SolicitudVacacionesBandeja {
  readonly solicitud: SolicitudVacaciones;
  readonly diasDisponibles: number;
  readonly diasEnSolicitud: number;
  readonly saldoEfectivo: number;
}

export interface SaldoVacaciones {
  readonly idEmpleado: number;
  readonly diasOtorgados: number;
  readonly diasUtilizados: number;
  readonly diasEnSolicitud: number;
  readonly diasDisponibles: number;
  readonly diasDisponiblesParaNuevaSolicitud: number;
}

export interface ResultadoSolicitudVacaciones {
  readonly idSolicitudVacaciones: number;
  readonly diasSolicitados: number;
  readonly fechasTurnosEnConflicto: readonly string[];
  readonly advertenciaConflictos: string | null;
}

export interface TurnoEnConflicto {
  readonly idAsignacion: number;
  readonly fecha: string;
  readonly nombreTurno: string;
  readonly periodoInicio: string;
  readonly periodoFin: string;
  readonly tieneAsistencia: boolean;
}

export interface ConflictosSolicitudVacaciones {
  readonly idSolicitudVacaciones: number;
  readonly conflictoBloquea: boolean;
  readonly turnos: readonly TurnoEnConflicto[];
}

export interface Novedad {
  readonly idNovedad: number;
  readonly idEmpleado: number;
  readonly nombreEmpleado: string;
  readonly codTipoNovedad: string;
  readonly nombreTipoNovedad: string;
  readonly inicio: string;
  readonly fin: string;
  readonly remunerado: boolean;
  readonly afectaHoras: boolean;
  readonly codEstadoSolicitud: string;
  readonly nombreEstadoSolicitud: string;
  readonly observaciones: string | null;
  readonly urlSoporte: string | null;
  readonly registradoEn: string;
  readonly resueltoEn: string | null;
  readonly motivoRechazo: string | null;
}

export interface SolicitudCambioTurno {
  readonly idSolicitudCambio: number;
  readonly idAsignacionSolicitante: number;
  readonly idEmpleadoSolicitante: number;
  readonly nombreEmpleadoSolicitante: string;
  readonly fechaTurnoSolicitante: string;
  readonly nombreTurnoSolicitante: string;
  readonly idAsignacionCompanero: number;
  readonly idEmpleadoCompanero: number;
  readonly nombreEmpleadoCompanero: string;
  readonly fechaTurnoCompanero: string;
  readonly nombreTurnoCompanero: string;
  readonly motivo: string | null;
  readonly codEstadoSolicitud: string;
  readonly nombreEstadoSolicitud: string;
  readonly solicitadoEn: string;
  readonly resueltoEn: string | null;
  readonly motivoRechazo: string | null;
}

// ---------- Turnos ----------
export interface Turno {
  readonly idTurno: number;
  readonly nombre: string;
  readonly horaInicio: string;
  readonly horaFin: string;
  readonly minutosDescanso: number;
  readonly cruzaMedianoche: boolean;
  readonly duracionMinutos: number;
  readonly color: string | null;
  readonly activo: boolean;
}

export interface AsignacionTurno {
  readonly idAsignacion: number;
  readonly idEmpleado: number;
  readonly nombreEmpleado: string;
  readonly numeroDocumento: string;
  readonly idArea: number;
  readonly nombreArea: string;
  readonly idTurno: number;
  readonly nombreTurno: string;
  readonly colorTurno: string | null;
  readonly fecha: string;
  readonly periodoInicio: string;
  readonly periodoFin: string;
  readonly codEstadoAsignacion: string;
  readonly nombreEstadoAsignacion: string;
  readonly observacion: string | null;
}

export interface ConflictoTurno {
  readonly tipo: string;
  readonly mensaje: string;
  readonly bloquea: boolean;
}

export interface RevisionConflictosTurno {
  readonly idEmpleado: number;
  readonly idTurno: number;
  readonly nombreTurno: string;
  readonly fecha: string;
  readonly puedeAsignarse: boolean;
  readonly conflictos: readonly ConflictoTurno[];
}

export interface PrevisualizacionModificacion {
  readonly idAsignacion: number;
  readonly puedeModificarse: boolean;
  readonly conflictos: readonly ConflictoTurno[];
}

export interface ItemProgramacion {
  readonly idEmpleado: number;
  readonly idTurno: number;
  readonly fecha: string;
  readonly observacion?: string | null;
}

export interface TurnoIntercambiable {
  readonly idAsignacion: number;
  readonly idEmpleado: number;
  readonly nombreEmpleado: string;
  readonly fecha: string;
  readonly idTurno: number;
  readonly nombreTurno: string;
  readonly horaInicio: string;
  readonly horaFin: string;
  readonly colorTurno: string | null;
  readonly codEstadoAsignacion: string;
}

export interface EventoCalendario {
  readonly tipo: string;
  readonly detalle: string;
  readonly inicio: string;
  readonly fin: string;
}

export interface HistorialTurno {
  readonly idAsignacion: number;
  readonly fecha: string;
  readonly nombreTurno: string;
  readonly horaInicio: string;
  readonly horaFin: string;
  readonly codEstadoAsignacion: string;
  readonly observacion: string | null;
}

export interface HorasPorTurno {
  readonly idAsignacion: number;
  readonly fecha: string;
  readonly nombreTurno: string;
  readonly minutosProgramados: number;
  readonly minutosTrabajados: number;
  readonly diferenciaMinutos: number;
  readonly horaEntrada: string | null;
  readonly horaSalida: string | null;
  readonly minutosTarde: number | null;
  readonly sinAsistencia?: boolean;
  readonly novedad?: string | null;
  readonly esJustificado?: boolean;
}

export interface PermisoSistema {
  readonly idPermiso: number;
  readonly codigo: string;
  readonly descripcion: string | null;
}

export interface RolSistema {
  readonly idRol: number;
  readonly nombre: string;
  readonly descripcion: string | null;
  readonly activo: boolean;
  readonly permisos: readonly string[];
}

export interface PanelInicial {
  readonly registrosPendientes: number;
  readonly vacacionesPendientes: number;
  readonly novedadesPendientes: number;
  readonly empleadosActivos: number;
  readonly turnosHoy: number;
  readonly sinAsistenciaHoy: number;
}

export interface PanelAsistenciaTurno {
  readonly idAsignacion: number;
  readonly idEmpleado: number;
  readonly nombreEmpleado: string;
  readonly idArea: number;
  readonly nombreArea: string;
  readonly idTurno: number;
  readonly nombreTurno: string;
  readonly programadoInicio: string;
  readonly programadoFin: string;
  readonly idRegistro: number | null;
  readonly horaEntrada: string | null;
  readonly horaSalida: string | null;
  readonly minutosTarde: number;
  readonly tarde: boolean;
  readonly estado: string;
  readonly novedadQueExplica: string | null;
}

export interface PanelAsistenciaDia {
  readonly fecha: string;
  readonly toleranciaEntradaMinutos: number;
  readonly turnosProgramados: number;
  readonly conAsistencia: number;
  readonly enCurso: number;
  readonly tardanzas: number;
  readonly sinAsistencia: number;
  readonly turnos: readonly PanelAsistenciaTurno[];
}

export const ESTADOS_SOLICITUD = [
  { valor: 'PENDIENTE', etiqueta: 'Pendientes' },
  { valor: 'APROBADA', etiqueta: 'Aprobadas' },
  { valor: 'RECHAZADA', etiqueta: 'Rechazadas' },
  { valor: 'CANCELADA', etiqueta: 'Canceladas' },
  { valor: '', etiqueta: 'Todas' },
] as const;

// ---------- Auditoría ----------
export interface BitacoraNegocio {
  readonly idBitacora: number;
  readonly fechaHora: string;
  readonly idUsuario: number | null;
  readonly correoUsuario: string | null;
  readonly codTipoAccion: string;
  readonly nombreTipoAccion: string;
  readonly esquema: string;
  readonly tabla: string;
  readonly idRegistro: string;
  readonly idEmpleado: number | null;
  readonly nombreEmpleado: string | null;
  readonly valorAnterior: string | null;
  readonly valorNuevo: string | null;
  readonly descripcion: string | null;
  readonly ipOrigen: string | null;
}

