namespace SharedKernel.Auth;

public sealed class JwtSettings
{
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "fluxo-caixa";
    public string Audience { get; init; } = "fluxo-caixa-clients";
    public int ExpirationMinutes { get; init; } = 60;
}
