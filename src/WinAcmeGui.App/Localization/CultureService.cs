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
                , ["GuiSubtitle"] = "CENTRAL DE ADMINISTRAÇÃO"
                , ["Status"] = "STATUS"
                , ["Download"] = "Baixar win-acme"
                , ["Update"] = "↻  Atualizar"
                , ["ActiveInstallation"] = "INSTALAÇÃO ATIVA"
                , ["RenewalsLoaded"] = "Renovações carregadas"
                , ["Health"] = "SAÚDE"
                , ["ReadyToOperate"] = "Pronto para operar"
                , ["ScheduledTask"] = "TAREFA AGENDADA"
                , ["VerifyInSystem"] = "Verificar no Sistema"
                , ["FilterRenewals"] = "Filtrar por nome, domínio ou status"
                , ["FriendlyName"] = "Nome amigável"
                , ["Domains"] = "Domínios"
                , ["StatusColumn"] = "Status"
                , ["Diagnostics"] = "Diagnóstico"
                , ["Editable"] = "Editável"
                , ["Renew"] = "Renovar"
                , ["Force"] = "Forçar"
                , ["Cancel"] = "Cancelar"
                , ["Revoke"] = "Revogar"
                , ["NewCertificateAction"] = "＋ Novo certificado"
                , ["CertificateTitle"] = "Emitir certificado"
                , ["CertificateInstructions"] = "Preencha as opções; a linha de comando será revisada antes da execução."
                , ["DomainsLabel"] = "Domínios (separados por vírgula)"
                , ["Validation"] = "Validação"
                , ["PrivateKey"] = "Chave privada"
                , ["Storage"] = "Armazenamento"
                , ["StoragePath"] = "Caminho de saída (PEM/PFX)"
                , ["CancelOperation"] = "Cancelar operação"
                , ["UseStaging"] = "Usar servidor de testes (staging)"
                , ["PreviewPlaceholder"] = "A prévia aparecerá aqui."
                , ["PreviewAction"] = "Pré-visualizar"
                , ["Execute"] = "Executar"
                , ["SelectExecutableAction"] = "Selecionar wacs.exe"
                , ["HomeDescription"] = "Resumo da instalação ativa e das renovações detectadas."
                , ["RenewalsDescription"] = "Pesquise, revise e opere renovações sem misturar instalações."
                , ["NewDescription"] = "Abra o assistente para revisar e emitir um certificado manual."
                , ["InstallationDescription"] = "Troque a instalação ativa e confira o caminho efetivo de configuração."
                , ["SystemDescription"] = "Consulte o estado da sessão, do worker elevado e do endpoint ACME."
                , ["SettingsDescription"] = "Use os controles de idioma no cabeçalho; alterações no win-acme continuam backup-first."
                , ["LogsDescription"] = "A saída da última operação aparece no status e os logs originais permanecem no win-acme."
                , ["AboutDescription"] = "Administração portátil para win-acme, com operações tipadas e validação explícita."
                , ["OpenCertificate"] = "Abrir assistente de certificado"
                , ["ChooseInstallation"] = "Selecionar wacs.exe"
                , ["ConfirmCreate"] = "Executar esta operação no win-acme?"
                , ["ConfirmCreateTitle"] = "Confirmar emissão"
                , ["EmailAddress"] = "E-mail da conta (opcional)"
                , ["AcceptTerms"] = "Aceito os termos de serviço da Let's Encrypt para esta operação"
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
                , ["GuiSubtitle"] = "ADMINISTRATION CENTER"
                , ["Status"] = "STATUS"
                , ["Download"] = "Download win-acme"
                , ["Update"] = "↻  Refresh"
                , ["ActiveInstallation"] = "ACTIVE INSTALLATION"
                , ["RenewalsLoaded"] = "Loaded renewals"
                , ["Health"] = "HEALTH"
                , ["ReadyToOperate"] = "Ready to operate"
                , ["ScheduledTask"] = "SCHEDULED TASK"
                , ["VerifyInSystem"] = "Check in System"
                , ["FilterRenewals"] = "Filter by name, domain or status"
                , ["FriendlyName"] = "Friendly name"
                , ["Domains"] = "Domains"
                , ["StatusColumn"] = "Status"
                , ["Diagnostics"] = "Diagnostics"
                , ["Editable"] = "Editable"
                , ["Renew"] = "Renew"
                , ["Force"] = "Force"
                , ["Cancel"] = "Cancel"
                , ["Revoke"] = "Revoke"
                , ["NewCertificateAction"] = "＋ New certificate"
                , ["CertificateTitle"] = "Issue certificate"
                , ["CertificateInstructions"] = "Fill the options; the command line will be reviewed before execution."
                , ["DomainsLabel"] = "Domains (comma separated)"
                , ["Validation"] = "Validation"
                , ["PrivateKey"] = "Private key"
                , ["Storage"] = "Storage"
                , ["StoragePath"] = "Output path (PEM/PFX)"
                , ["CancelOperation"] = "Cancel operation"
                , ["UseStaging"] = "Use test server (staging)"
                , ["PreviewPlaceholder"] = "The preview will appear here."
                , ["PreviewAction"] = "Preview"
                , ["Execute"] = "Execute"
                , ["SelectExecutableAction"] = "Select wacs.exe"
                , ["HomeDescription"] = "Overview of the active installation and detected renewals."
                , ["RenewalsDescription"] = "Search, review and operate renewals without mixing installations."
                , ["NewDescription"] = "Open the wizard to review and issue a manual certificate."
                , ["InstallationDescription"] = "Switch the active installation and inspect the effective configuration path."
                , ["SystemDescription"] = "Review session state, elevated worker and ACME endpoint status."
                , ["SettingsDescription"] = "Use the language controls in the header; win-acme changes remain backup-first."
                , ["LogsDescription"] = "The latest operation output appears in status while original win-acme logs remain untouched."
                , ["AboutDescription"] = "Portable administration for win-acme with typed operations and explicit validation."
                , ["OpenCertificate"] = "Open certificate wizard"
                , ["ChooseInstallation"] = "Select wacs.exe"
                , ["ConfirmCreate"] = "Run this operation in win-acme?"
                , ["ConfirmCreateTitle"] = "Confirm issuance"
                , ["EmailAddress"] = "Account email (optional)"
                , ["AcceptTerms"] = "I accept the Let's Encrypt terms of service for this operation"
            }
        };

    public CultureInfo Current { get; private set; } = CultureInfo.CurrentUICulture;

    public static IReadOnlyCollection<string> Keys => Resources["pt-BR"].Keys.ToArray();

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
