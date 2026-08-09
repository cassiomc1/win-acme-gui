# Solução de problemas

- **Nenhuma instalação encontrada:** use Selecionar `wacs.exe`, confira permissões e execute `wacs.exe --version` no PowerShell.
- **Renovação ilegível:** preserve o arquivo original, abra o caminho mostrado e consulte o log. Formatos desconhecidos ficam somente leitura.
- **UAC recusado ou worker não confiável:** aceite o prompt do worker elevado e use um pacote de produção Authenticode-assinado com GUI e worker emitidos pelo mesmo signatário. A GUI eleva apenas a operação allowlisted e não a sessão inteira; um worker ausente, adulterado ou não confiável é bloqueado.
- **Comando falhou:** consulte a saída mascarada e o log original do win-acme. O código de saída é preservado.
- **Download bloqueado:** confira conectividade com os hosts oficiais do GitHub, use uma pasta vazia e confirme o digest SHA-256 e a assinatura Authenticode do release. Redirects, hosts não aprovados, digest ausente, assinatura inválida e ZIP inseguro são bloqueados deliberadamente.
