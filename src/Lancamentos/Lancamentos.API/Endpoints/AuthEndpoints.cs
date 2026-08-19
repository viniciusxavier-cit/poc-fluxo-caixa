using SharedKernel.Auth;

namespace Lancamentos.API.Endpoints;

public static class AuthEndpoints
{
    // Usuários fictícios para demo — em produção substituir por banco + hash de senha
    private static readonly Dictionary<string, (string Senha, string Papel)> _usuarios = new()
    {
        { "comerciante", ("senha123", "comerciante") },
        { "admin",       ("admin123", "admin") }
    };

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", (LoginRequest req, TokenService tokenService) =>
        {
            if (!_usuarios.TryGetValue(req.Usuario, out var dados) || dados.Senha != req.Senha)
                return Results.Unauthorized();

            var token = tokenService.GerarToken(req.Usuario, dados.Papel);
            return Results.Ok(new { token, expiraEm = $"{60} minutos" });
        })
        .WithTags("Autenticação")
        .WithName("GerarToken")
        .WithSummary("Gera token JWT")
        .WithDescription("Retorna um token Bearer para uso nos demais endpoints. Usuários demo: `comerciante/senha123` ou `admin/admin123`.")
        .AllowAnonymous()
        .WithOpenApi();
    }
}

public sealed record LoginRequest(string Usuario, string Senha);
