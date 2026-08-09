using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;

namespace WinAcmeGui.Infrastructure.Downloads;

public interface IPackageSignatureVerifier
{
    Task VerifyAsync(string destination, CancellationToken cancellationToken);
}

public interface IExecutableTrustVerifier
{
    Task<bool> IsTrustedAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed class WindowsAuthenticodeSignatureVerifier(bool requireTrustedPublisher = true) : IPackageSignatureVerifier, IExecutableTrustVerifier
{
    private static readonly string[] TrustedPublisherMarkers =
    [
        "win.acme.simple@gmail.com",
        "win-acme",
        "wacs",
        "zerossl",
        "zero ssl",
        "wouter tinus"
    ];

    public Task VerifyAsync(string destination, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

        var executables = Directory.EnumerateFiles(destination, "wacs.exe", SearchOption.AllDirectories).ToArray();
        if (executables.Length == 0) throw new InvalidDataException("Downloaded package does not contain wacs.exe.");
        foreach (var executable in executables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyExecutable(executable);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsTrustedAsync(string executablePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(true);
        try
        {
            VerifyExecutable(executablePath);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<bool> HasSameSignerAsync(string firstExecutable, string secondExecutable, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return true;
        if (!await IsTrustedAsync(firstExecutable, cancellationToken) || !await IsTrustedAsync(secondExecutable, cancellationToken)) return false;
        try
        {
            using var first = ReadSignerCertificate(firstExecutable);
            using var second = ReadSignerCertificate(secondExecutable);
            return first.Thumbprint.Equals(second.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return false;
        }
    }

    private void VerifyExecutable(string executable)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("Executable was not found.", executable);
        try
        {
            using var certificate = ReadSignerCertificate(executable);
            var pinnedOfficialCertificate = IsPinnedOfficialCertificate(certificate);
            if (!certificate.Verify() && !pinnedOfficialCertificate)
                throw new InvalidDataException($"Authenticode chain verification failed for {executable}.");
            if (requireTrustedPublisher && !IsTrustedPublisher(certificate))
                throw new InvalidDataException($"The executable publisher is not approved: {executable}.");
            VerifyAuthenticodeSignature(executable, pinnedOfficialCertificate);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidDataException($"Authenticode verification failed for {executable}.", ex);
        }
    }

    private static X509Certificate2 ReadSignerCertificate(string executable)
    {
        using var signed = X509Certificate.CreateFromSignedFile(executable);
        return new X509Certificate2(signed);
    }

    private static bool IsTrustedPublisher(X509Certificate2 certificate)
    {
        var names = new[]
        {
            certificate.GetNameInfo(X509NameType.SimpleName, false),
            certificate.GetNameInfo(X509NameType.EmailName, false),
            certificate.GetNameInfo(X509NameType.DnsName, false),
            certificate.GetNameInfo(X509NameType.UpnName, false),
            certificate.GetNameInfo(X509NameType.UrlName, false)
        };
        return names.Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(name => TrustedPublisherMarkers.Any(marker => name.Trim().Equals(marker, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsPinnedOfficialCertificate(X509Certificate2 certificate) =>
        certificate.Subject.Equals("CN=WACS", StringComparison.OrdinalIgnoreCase)
        && certificate.Issuer.Equals("CN=WACS", StringComparison.OrdinalIgnoreCase)
        && certificate.Thumbprint.Equals("9A733B700FCA BF26D73485B1384346E542558F1FAE704414433378E885A1BD33".Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

    private static void VerifyAuthenticodeSignature(string executable, bool allowPinnedTrust)
    {
        var fileInfo = new WinTrustFileInfo(executable);
        var fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
        var data = new WinTrustData
        {
            CbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
            UiChoice = WtdUiNone,
            RevocationChecks = WtdRevokeWholeChain,
            UnionChoice = WtdChoiceFile,
            File = fileInfoPointer,
            StateAction = WtdStateActionVerify
        };
        var action = GenericVerifyV2;
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (status != 0 && (!allowPinnedTrust || !IsTrustChainFailure(status)))
                throw new InvalidDataException($"Authenticode verification failed for {executable} (0x{status:X8}).");
        }
        finally
        {
            if (data.StateData != IntPtr.Zero)
            {
                data.StateAction = WtdStateActionClose;
                _ = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            }
            Marshal.FreeCoTaskMem(fileInfo.FilePath);
            Marshal.FreeCoTaskMem(fileInfoPointer);
        }
    }

    private static bool IsTrustChainFailure(uint status) =>
        status is TrustESubjectNotTrusted or CertEExpired or CertEUntrustedRoot or CertEChaining;

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeWholeChain = 1;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint TrustESubjectNotTrusted = 0x800B0004;
    private const uint CertEExpired = 0x800B0101;
    private const uint CertEUntrustedRoot = 0x800B0109;
    private const uint CertEChaining = 0x800B010A;
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint CbStruct;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;

        public WinTrustFileInfo(string path)
        {
            CbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>();
            FilePath = Marshal.StringToCoTaskMemUni(path);
            FileHandle = IntPtr.Zero;
            KnownSubject = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint CbStruct;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
