# Solução de problemas

Primeiro preserve o arquivo original e copie o código exibido no status. Não publique senhas, tokens, chaves privadas ou a saída sem mascaramento em tickets.

## Descoberta e inventário

- **Nenhuma instalação encontrada:** use **Selecionar `wacs.exe`**, confira permissões e execute `wacs.exe --version` no PowerShell.
- **`discovery.configuration.collision`:** duas instalações apontam para a mesma configuração efetiva. Mantenha apenas uma como contexto operacional; as demais permanecem isoladas/diagnósticas.
- **`renewal.read_only`:** a linha é inválida, desconhecida ou usa configuração compartilhada. Use o caminho mostrado, corrija o ambiente no win-acme e atualize a GUI.
- **`renewal.json.invalid`, `renewal.json.incomplete` ou `renewal.file.unreadable`:** preserve o JSON, valide permissões/sintaxe e consulte o log original. A GUI não reescreve o renewal JSON.
- **`renewal.plugin.unknown`:** o plugin não é entendido pela GUI. Execute o fluxo pelo console do win-acme; não force uma mutação pela GUI.
- **`renewal.directory.unreadable`:** confira se o caminho de configuração existe e se a conta tem leitura.

## Operações

| Código | Causa provável | Ação |
|---|---|---|
| `process.start.notfound` | Executável ou dependência não encontrada | Revalide o caminho de `wacs.exe` e reinstale se necessário. |
| `process.start.denied` | Acesso negado ao iniciar o executável | Verifique permissões do arquivo e bloqueios do antivírus. |
| `process.start.failed` | O executável não pôde ser iniciado por outro motivo | Revalide `wacs.exe` e permissões. |
| `process.exit.nonzero` | O win-acme terminou com erro | Consulte a saída mascarada, o código de saída e o log original. |
| `operation.cancelled` | O usuário cancelou a operação | Confirme que o processo terminou e atualize o inventário. |
| `operation.timeout` | O processo excedeu o limite | Consulte o log e tente novamente em staging. |
| `renewal.read_only` | A renovação não é editável | Corrija o documento/ambiente no win-acme. |
| `certificate.*` | Dados do assistente inválidos | Corrija domínios, validação, chave, armazenamento, caminho absoluto ou termos. |

Cancelar e revogar exigem o nome amigável exato. Revogação é destinada a comprometimento da chave, não a uma renovação comum.

## UAC e confiança

- **`elevation.uac.rejected`:** aceite o prompt do UAC ou execute a operação com uma conta autorizada.
- **`elevation.worker.missing`:** mantenha `worker/WinAcmeGui.ElevatedWorker.exe` ao lado da GUI.
- **`elevation.worker.untrusted`, `elevation.executable.untrusted` ou `elevation.worker.publisher.mismatch`:** use um pacote de produção Authenticode-assinado; GUI e worker precisam compartilhar o mesmo signatário confiável.
- **`elevation.worker.start.failed` ou `elevation.worker.timeout`:** verifique permissões, antivírus, caminho do worker e o log do Windows.
- **`elevation.operation.not_allowed`:** a operação ou argumento não pertence à lista permitida; não tente contornar o bloqueio.
- **`elevation.protocol.*`:** o worker e a GUI não concordaram sobre o protocolo, ou o processo conectado não era o worker elevado iniciado pela GUI. Reextraia um pacote completo e não misture versões da pasta `worker`.

## Download bloqueado

Confira conectividade com os hosts oficiais do GitHub e use uma pasta vazia. Redirects, hosts não aprovados, digest ausente ou divergente, assinatura inválida, executável não confiável e ZIP inseguro são bloqueados deliberadamente. Não desative essas verificações para produção.

## Limite da validação

O workflow Windows valida testes, compilação WPF/worker, empacotamento CI sem assinatura e hashes em `windows-latest`. Ele não substitui uma aceitação real em cada edição do Windows, com UAC, IIS, Scheduled Tasks, certificate store e emissão staging.
