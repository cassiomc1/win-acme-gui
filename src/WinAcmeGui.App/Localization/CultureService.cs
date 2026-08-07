using System.Globalization;

namespace WinAcmeGui.App.Localization;

public sealed class CultureService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pt-BR"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "win-acme GUI",
                ["Home"] = "Início",
                ["Renewals"] = "Renovações",
                ["NewCertificate"] = "Novo certificado",
                ["Installation"] = "Instalação",
                ["System"] = "Sistema",
                ["Settings"] = "Configurações",
                ["Logs"] = "Logs",
                ["About"] = "Sobre",
                ["Scanning"] = "Procurando instalações do win-acme…",
                ["NoInstallation"] = "Nenhuma instalação encontrada",
                ["SelectExecutable"] = "Selecionar wacs.exe",
                ["DetectedInstallation"] = "Instalação ativa",
                ["ConfigurationPath"] = "Caminho da configuração",
                ["Endpoint"] = "Endpoint ACME",
                ["RenewalCount"] = "renovações carregadas",
                ["Refresh"] = "Atualizar",
                ["DueSoon"] = "Renovar em breve",
                ["Healthy"] = "Saudável",
                ["Unreadable"] = "Não legível"
            },
            ["en-US"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "win-acme GUI",
                ["Home"] = "Home",
                ["Renewals"] = "Renewals",
                ["NewCertificate"] = "New certificate",
                ["Installation"] = "Installation",
                ["System"] = "System",
                ["Settings"] = "Settings",
                ["Logs"] = "Logs",
                ["About"] = "About",
                ["Scanning"] = "Looking for win-acme installations…",
                ["NoInstallation"] = "No installation found",
                ["SelectExecutable"] = "Select wacs.exe",
                ["DetectedInstallation"] = "Active installation",
                ["ConfigurationPath"] = "Configuration path",
                ["Endpoint"] = "ACME endpoint",
                ["RenewalCount"] = "renewals loaded",
                ["Refresh"] = "Refresh",
                ["DueSoon"] = "Due soon",
                ["Healthy"] = "Healthy",
                ["Unreadable"] = "Unreadable"
            }
        };

    public CultureInfo Current { get; private set; } = CultureInfo.CurrentUICulture;

    public string CultureName => Current.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en-US";

    public string this[string key] => Resources[CultureName].TryGetValue(key, out var value) ? value : key;

    public static string ChooseInitial(string windowsCulture) =>
        windowsCulture.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en-US";

    public void SetCulture(string name)
    {
        var selected = name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en-US";
        Current = CultureInfo.GetCultureInfo(selected);
        CultureInfo.CurrentCulture = Current;
        CultureInfo.CurrentUICulture = Current;
    }
}
