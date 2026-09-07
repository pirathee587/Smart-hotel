using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SmartHotel.Identity.Infrastructure.Services;

public class RsaKeyManager
{
    private readonly ILogger<RsaKeyManager> _logger;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _securityKey;
    public string KeyId { get; }

    public RsaKeyManager(IConfiguration configuration, ILogger<RsaKeyManager> logger)
    {
        _logger = logger;
        KeyId = configuration["Jwt:KeyId"] ?? "smarthotel-identity-key-1";

        var keyPath = configuration["Jwt:RsaKeyPath"] ?? Path.Combine(AppContext.BaseDirectory, "keys", "identity_rsa.pem");
        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _rsa = RSA.Create(2048);

        if (File.Exists(keyPath))
        {
            _logger.LogInformation("Loading existing RSA key pair from {KeyPath}", keyPath);
            var pem = File.ReadAllText(keyPath);
            _rsa.ImportFromPem(pem);
        }
        else
        {
            _logger.LogInformation("No RSA key found at {KeyPath}. Generating a new 2048-bit RSA key pair...", keyPath);
            var privatePem = _rsa.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(keyPath, privatePem);

            var pubKeyPath = Path.ChangeExtension(keyPath, ".pub.pem");
            var publicPem = _rsa.ExportSubjectPublicKeyInfoPem();
            File.WriteAllText(pubKeyPath, publicPem);

            _logger.LogInformation("RSA key pair generated and persisted to {KeyPath}", keyPath);
        }

        _securityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
    }

    public RsaSecurityKey GetSecurityKey() => _securityKey;

    public RSA GetRsa() => _rsa;

    public object GetJwks()
    {
        var parameters = _rsa.ExportParameters(false);
        var modulus = Base64UrlEncoder.Encode(parameters.Modulus);
        var exponent = Base64UrlEncoder.Encode(parameters.Exponent);

        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = modulus,
                    e = exponent
                }
            }
        };
    }
}
