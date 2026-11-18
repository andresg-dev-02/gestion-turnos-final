export interface LoginRequest {
  readonly correo: string;
  readonly contrasena: string;
  /** "Mantener la sesión iniciada en este dispositivo". */
  readonly recordar?: boolean;
}

export interface LoginResponseDto {
  readonly accessToken: string;
  readonly expiraEn: string;
  readonly idUsuario: number;
  readonly correo: string;
  readonly rol: string;
  readonly idEmpleado: number | null;
  readonly nombreCompleto: string | null;
  readonly permisos: readonly string[];
  readonly refreshToken?: string | null;
  readonly refreshExpiraEn?: string | null;
}

export interface SolicitarRegistroRequest {
  readonly codTipoDocumento: string;
  readonly numeroDocumento: string;
  readonly nombres: string;
  readonly apellidos: string;
  readonly correo: string;
  readonly telefono: string | null;
  readonly contrasena: string;
}

export interface TipoDocumentoDto {
  readonly codTipoDocumento: string;
  readonly nombre: string;
  readonly activo: boolean;
}

export interface UsuarioSesion {
  readonly idUsuario: number;
  readonly correo: string;
  readonly rol: string;
  readonly idEmpleado: number | null;
  readonly nombreCompleto: string;
  readonly permisos: readonly string[];
  readonly expiraEn: string;
}
