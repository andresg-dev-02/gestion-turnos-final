using FluentValidation;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Auth;

/// <summary>
/// Comando de inicio de sesión (Sección 3.3).
/// </summary>
public sealed record IniciarSesionCommand(
    string Correo,
    string Contrasena) : IRequest<Result<LoginResponseDto>>;

public sealed class IniciarSesionCommandValidator : AbstractValidator<IniciarSesionCommand>
{
    public IniciarSesionCommandValidator()
    {
        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

public sealed class IniciarSesionCommandHandler : IRequestHandler<IniciarSesionCommand, Result<LoginResponseDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;

    public IniciarSesionCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LoginResponseDto>> Handle(
        IniciarSesionCommand request,
        CancellationToken cancellationToken)
    {
        var correoNormalizado = request.Correo.Trim().ToLowerInvariant();

        // 1. Busca por lower(correo) en seguridad.usuario
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == correoNormalizado, cancellationToken);

        // 2. Si no existe, revisa solicitud_registro: si hay una PENDIENTE responde "tu registro está pendiente de aprobación";
        //    si la última fue RECHAZADA, informa el rechazo.
        if (usuario is null)
        {
            var ultimaSolicitud = await _context.SolicitudesRegistro
                .AsNoTracking()
                .Where(s => s.Correo.ToLower() == correoNormalizado)
                .OrderByDescending(s => s.SolicitadoEn)
                .FirstOrDefaultAsync(cancellationToken);

            if (ultimaSolicitud is not null)
            {
                if (ultimaSolicitud.CodEstadoSolicitud == EstadosSolicitud.Pendiente)
                {
                    return Result.Failure<LoginResponseDto>(AuthErrors.RegistroPendiente);
                }

                if (ultimaSolicitud.CodEstadoSolicitud == EstadosSolicitud.Rechazada)
                {
                    return Result.Failure<LoginResponseDto>(AuthErrors.RegistroRechazado(ultimaSolicitud.MotivoRechazo));
                }
            }

            return Result.Failure<LoginResponseDto>(AuthErrors.CredencialesInvalidas);
        }

        // 3. Compara la contraseña con bcrypt y exige cod_estado_usuario = 'ACTIVO'.
        //    La contraseña se verifica primero: el estado de la cuenta solo se revela a quien la conoce.
        if (!_passwordHasher.Verify(request.Contrasena, usuario.HashContrasena))
        {
            return Result.Failure<LoginResponseDto>(AuthErrors.CredencialesInvalidas);
        }

        if (usuario.CodEstadoUsuario != EstadosUsuario.Activo)
        {
            return Result.Failure<LoginResponseDto>(AuthErrors.UsuarioNoActivo(usuario.CodEstadoUsuario));
        }

        if (!usuario.Rol.Activo)
        {
            return Result.Failure<LoginResponseDto>(AuthErrors.Forbidden);
        }

        // 4. Carga rol, permisos (seguridad.rol_permiso -> seguridad.permiso.codigo) y el id_empleado activo (Sección 2)
        var permisos = await _context.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.IdRol == usuario.IdRol)
            .Select(rp => rp.Permiso.Codigo)
            .ToListAsync(cancellationToken);

        var empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdUsuario == usuario.IdUsuario && e.CodEstadoEmpleado == EstadosEmpleado.Activo)
            .Select(e => new { e.IdEmpleado, NombreCompleto = e.Nombres + " " + e.Apellidos })
            .FirstOrDefaultAsync(cancellationToken);

        // 5. Registra el acceso en una transacción (UPDATE ultimo_acceso + INSERT en auditoria.bitacora_negocio)
        try
        {
            // Aún no hay sesión autenticada: el autor de la auditoría es el propio usuario que ingresa.
            await _unitOfWork.BeginTransactionAsync(usuario.IdUsuario, cancellationToken);

            usuario.UltimoAcceso = DateTimeOffset.UtcNow;

            var bitacora = new BitacoraNegocio
            {
                FechaHora = DateTimeOffset.UtcNow,
                IdUsuario = usuario.IdUsuario,
                CodTipoAccion = TiposAccionAuditoria.InicioSesion,
                Esquema = "seguridad",
                Tabla = "usuario",
                IdRegistro = usuario.IdUsuario.ToString(),
                IdEmpleado = empleadoActivo?.IdEmpleado,
                IpOrigen = _currentUserService.IpAddress
            };

            await _context.BitacoraNegocio.AddAsync(bitacora, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(DatabaseErrorTranslator.Translate(ex));
        }

        // 6. Genera token JWT con los permisos cargados
        var (token, expiraEn) = _jwtTokenGenerator.GenerateToken(
            usuario.IdUsuario,
            usuario.Correo,
            usuario.Rol.Nombre,
            empleadoActivo?.IdEmpleado,
            empleadoActivo?.NombreCompleto,
            permisos);

        return Result.Success(new LoginResponseDto(
            AccessToken: token,
            ExpiraEn: expiraEn,
            IdUsuario: usuario.IdUsuario,
            Correo: usuario.Correo,
            Rol: usuario.Rol.Nombre,
            IdEmpleado: empleadoActivo?.IdEmpleado,
            NombreCompleto: empleadoActivo?.NombreCompleto,
            Permisos: permisos));
    }
}

