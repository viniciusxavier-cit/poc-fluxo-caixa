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
        .WithDescription("Retorna um token Bearer. Usuários demo: `comerciante/senha123` ou `admin/admin123`.")
        .AllowAnonymous()
        .AddEndpointFilter<LoginRequestValidator>()
        .WithOpenApi();
    }
}

public sealed record LoginRequest(string Usuario, string Senha);

internal sealed class LoginRequestValidator : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var req = ctx.GetArgument<LoginRequest>(0);

        List<string> erros = [];
        if (string.IsNullOrWhiteSpace(req.Usuario))
            erros.Add("O campo 'usuario' é obrigatório.");
        if (string.IsNullOrWhiteSpace(req.Senha))
            erros.Add("O campo 'senha' é obrigatório.");

        if (erros.Count > 0)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["usuario"] = erros.Where(e => e.Contains("usuario")).ToArray(),
                    ["senha"]   = erros.Where(e => e.Contains("senha")).ToArray()
                },
                title: "Dados inválidos");

        return await next(ctx);
    }
}
