using System.Net;

namespace WinAcmeGui.Infrastructure.Downloads;

internal sealed class OfficialHttpTransport
{
    private const int MaxRedirects = 5;
    private readonly HttpClient _httpClient;
    private readonly PackageVerifier _verifier;

    public OfficialHttpTransport(HttpClient httpClient, PackageVerifier verifier)
    {
        _httpClient = httpClient;
        _verifier = verifier;
    }

    public OfficialHttpTransport(PackageVerifier verifier)
        : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), verifier)
    {
    }

    public HttpClient Client => _httpClient;

    public bool IsApproved(Uri uri) => _verifier.IsApproved(uri);

    public async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!_verifier.IsApproved(uri)) throw new InvalidOperationException("Release source is not approved.");

        var current = uri;
        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var observedUri = response.RequestMessage?.RequestUri ?? current;
            if (!_verifier.IsApproved(observedUri))
            {
                response.Dispose();
                throw new InvalidDataException("Release request reached an unapproved host.");
            }

            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new InvalidDataException("Release redirect has no destination.");
                var next = new Uri(current, location);
                if (!_verifier.IsApproved(next)) throw new InvalidDataException("Release redirect targets an unapproved host.");
                current = next;
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        throw new InvalidDataException("Release source exceeded the redirect limit.");
    }
}
