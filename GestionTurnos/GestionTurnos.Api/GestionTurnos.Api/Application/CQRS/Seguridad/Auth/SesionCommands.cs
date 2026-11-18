using FluentValidation;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services.Sesiones;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Auth;

// Las propiedades se llaman "...Token" a propósito: LoggingBehavior enmascara todo lo que contenga "token".

// =====================================================================
// RENOVAR SESIÓN (PÚBLICO: el access token pudo haber vencido)
// =====================================================================

public sealed record RenovarSesionCommand(string RefreshToken, string? UserAgent) : IRequest<Result<LoginResponseDto>>;

public sealed class RenovarSesionCommandValidator : AbstractValidator<RenovarSesionCommand>
{
    public RenovarSesionCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
    }
}

public sealed class RenovarSesionCommandHandler : IRequestHandler<RenovarSesionCommand, Result<LoginResponseDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly SesionesService _sesiones;
    private readonly IJwtTokenGenerator _jwt;

    public RenovarSesionCommandHandler(GestionTurnosContext context, SesionesService sesiones, IJwtTokenGenerator jwt)
    {
        _context = context;
        _sesiones = sesiones;
        _jwt = jwt;
    }

    public async Task<Result<LoginResponseDto>> Handle(RenovarSesionCommand request, CancellationToken cancellationToken)
    {
        var rotada = await _sesiones.RotarAsync(request.RefreshToken, request.UserAgent, cancellationToken);
        if (rotada is null)
        {
            return Result.Failure<LoginResponseDto>(AuthErrors.SesionInvalida);
        }

        // Estado, rol y permisos se leen de nuevo: un usuario bloqueado o con otro rol no conserva lo anterior.
        var sesion = await SesionesService.ConstruirSesionAsync(_context, _jwt, rotada.IdUsuario, cancellationToken);
        if (sesion is null)
        {
            await _sesiones.RevocarFamiliaAsync(rotada.Familia, MotivosRevocacionSesion.UsuarioNoActivo, cancellationToken);
            return Result.Failure<LoginResponseDto>(AuthErrors.SesionInvalida);
        }

        return Result.Success(sesion with { RefreshToken = rotada.RefreshToken, RefreshExpiraEn = rotada.RefreshExpiraEn });
    }
}

// =====================================================================
// CERRAR SESIÓN EN ESTE DISPOSITIVO
// =====================================================================

public sealed record CerrarSesionCommand(string RefreshToken) : IRequest<Result>;

public sealed class CerrarSesionCommandHandler : IRequestHandler<CerrarSesionCommand, Result>
{
    private readonly SesionesService _sesiones;

    public CerrarSesionCommandHandler(SesionesService sesiones) => _sesiones = sesiones;

    public async Task<Result> Handle(CerrarSesionCommand request, CancellationToken cancellationToken)
    {
        // Siempre exitoso: no revela si el token existía.
        if (!string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Length <= 200)
        {
            await _sesiones.CerrarAsync(request.RefreshToken, cancellationToken);
        }

        return Result.Success();
    }
}
