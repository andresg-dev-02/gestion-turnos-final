using FluentValidation;
using GestionTurnos.Api.Application.DTOs.Organizacion;
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

namespace GestionTurnos.Api.Application.CQRS.Organizacion;

// =====================================================================
// 3.4 CONSULTAR PERFIL DEL EMPLEADO EN SESIÓN
// =====================================================================

public sealed record ObtenerMiPerfilQuery : IRequest<Result<PerfilEmpleadoDto>>, IRequireAuthentication;

public sealed class ObtenerMiPerfilQueryHandler : IRequestHandler<ObtenerMiPerfilQuery, Result<PerfilEmpleadoDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiPerfilQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<PerfilEmpleadoDto>> Handle(
        ObtenerMiPerfilQuery request,
        CancellationToken cancellationToken)
    {
        // Regla de oro (Sección 2): el id_empleado nunca llega del cliente
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<PerfilEmpleadoDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var perfil = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdEmpleado == idEmpleado)
            .Select(e => new PerfilEmpleadoDto(
                e.IdEmpleado,
                e.CodTipoDocumento,
                e.NumeroDocumento,
                e.Nombres,
                e.Apellidos,
                e.CorreoContacto,
                e.Telefono,
                e.Direccion,
                e.FechaIngreso,
                e.Cargo.Nombre,
                e.Area.Nombre,
                e.Area.Sucursal.Nombre,
                e.TipoContrato != null ? e.TipoContrato.Nombre : null,
                e.EstadoEmpleado.Nombre))
            .FirstOrDefaultAsync(cancellationToken);

        if (perfil is null)
        {
            return Result.Failure<PerfilEmpleadoDto>(AuthErrors.EmpleadoNoActivo);
        }

        return Result.Success(perfil);
    }
}

// =====================================================================
// 3.5 EDITAR PERFIL DEL EMPLEADO (LISTA BLANCA ESTRICTA)
// =====================================================================

/// <summary>
/// Sección 3.5: Lista blanca estricta. Solo permite actualizar correo_contacto, telefono y direccion.
/// Campos que el empleado nunca modifica: documento, nombres, apellidos, área, cargo, contrato, fecha de ingreso/retiro, estado.
/// </summary>
public sealed record EditarMiPerfilCommand(
    string? CorreoContacto,
    string? Telefono,
    string? Direccion) : IRequest<Result>, IRequireAuthentication;

public sealed class EditarMiPerfilCommandValidator : AbstractValidator<EditarMiPerfilCommand>
{
    public EditarMiPerfilCommandValidator()
    {
        RuleFor(x => x.CorreoContacto)
            .EmailAddress().WithMessage("El formato del correo de contacto no es válido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoContacto));

        RuleFor(x => x.Telefono)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono));

        RuleFor(x => x.Direccion)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Direccion));
    }
}

public sealed class EditarMiPerfilCommandHandler : IRequestHandler<EditarMiPerfilCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public EditarMiPerfilCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(EditarMiPerfilCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.IdEmpleado == idEmpleado, cancellationToken);

        if (empleado is null)
        {
            return Result.Failure(AuthErrors.EmpleadoNoActivo);
        }

        // Lista blanca estricta: UPDATE organizacion.empleado SET correo_contacto = $2, telefono = $3, direccion = $4 WHERE id_empleado = $1;
        empleado.CorreoContacto = string.IsNullOrWhiteSpace(request.CorreoContacto) ? null : request.CorreoContacto.Trim();
        empleado.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        empleado.Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 3.5 CAMBIO DE CONTRASEÑA CON BITÁCORA MANUAL
// =====================================================================

/// <summary>
/// Sección 3.5: Valida la contraseña actual, guarda el nuevo hash en seguridad.usuario.hash_contrasena.
/// El disparador de auditoría excluye el hash, así que se inserta la fila en auditoria.bitacora_negocio
/// en la misma transacción con descripcion = 'Cambio de contraseña'.
/// </summary>
public sealed record CambiarMiContrasenaCommand(
    string ContrasenaActual,
    string NuevaContrasena) : IRequest<Result>, IRequireAuthentication;

public sealed class CambiarMiContrasenaCommandValidator : AbstractValidator<CambiarMiContrasenaCommand>
{
    public CambiarMiContrasenaCommandValidator()
    {
        RuleFor(x => x.ContrasenaActual)
            .NotEmpty().WithMessage("La contraseña actual es obligatoria.");

        RuleFor(x => x.NuevaContrasena)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72)
            .NotEqual(x => x.ContrasenaActual).WithMessage("La nueva contraseña debe ser diferente a la actual.");
    }
}

public sealed class CambiarMiContrasenaCommandHandler : IRequestHandler<CambiarMiContrasenaCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public CambiarMiContrasenaCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Handle(CambiarMiContrasenaCommand request, CancellationToken cancellationToken)
    {
        var idUsuario = _currentUserService.UserId!.Value;

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario && u.CodEstadoUsuario == EstadosUsuario.Activo, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        if (!_passwordHasher.Verify(request.ContrasenaActual, usuario.HashContrasena))
        {
            return Result.Failure(new Error(
                "Seguridad.ContrasenaActualIncorrecta",
                "La contraseña actual es incorrecta.",
                ErrorType.Failure));
        }

        var idEmpleado = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdUsuario == idUsuario)
            .Select(e => (long?)e.IdEmpleado)
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            usuario.HashContrasena = _passwordHasher.Hash(request.NuevaContrasena);

            // Cierra la sesión en todos los dispositivos; el cliente vuelve a ingresar con la contraseña nueva.
            await Application.Services.Sesiones.SesionesService.RevocarTodasAsync(
                _context, usuario.IdUsuario, Domain.Entities.Seguridad.MotivosRevocacionSesion.CambioContrasena, cancellationToken);

            // Inserción manual en auditoria.bitacora_negocio (Sección 3.5 y Sección 7)
            var bitacora = new BitacoraNegocio
            {
                FechaHora = DateTimeOffset.UtcNow,
                IdUsuario = idUsuario,
                CodTipoAccion = TiposAccionAuditoria.Modificar,
                Esquema = "seguridad",
                Tabla = "usuario",
                IdRegistro = idUsuario.ToString(),
                IdEmpleado = idEmpleado,
                Descripcion = "Cambio de contraseña",
                IpOrigen = _currentUserService.IpAddress
            };

            await _context.BitacoraNegocio.AddAsync(bitacora, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

