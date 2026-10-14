using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FiscalApi.Cli.Tests.Listen;

/// <summary>Un certificado autofirmado para 127.0.0.1, como el de un receptor local en https.</summary>
internal static class SelfSignedCertificate
{
    public static X509Certificate2 ForLoopback()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fiscalapi-cli-tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        // Ida y vuelta por PFX: en Windows, SslStream no usa como servidor una llave efímera.
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);
    }
}
