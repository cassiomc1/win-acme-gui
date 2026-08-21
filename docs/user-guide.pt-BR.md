# Guia do usuário — win-acme GUI

## Antes de começar

- Use Windows 10/11 ou Windows Server x64 como alvo operacional. O projeto é distribuído como ZIP portátil `win-x64`.
- Mantenha `worker/WinAcmeGui.ElevatedWorker.exe` ao lado de `WinAcmeGui.exe`.
- Para produção, use um pacote assinado por Authenticode. Pacotes `-AllowUnsigned` servem apenas para CI/desenvolvimento.
- Use staging para validar a integração antes de emitir certificados de produção.
- Use o cabeçalho ou a página Configurações para alternar entre português/inglês e o tema claro/escuro da GUI; essas preferências não modificam o win-acme.

## Descobrir uma instalação

1. Abra `WinAcmeGui.exe`.
2. Na abertura, o programa procura `wacs.exe` em tarefas relacionadas, processos em execução, `PATH`, locais conhecidos e na pasta do aplicativo.
3. Cada candidato é validado com `wacs.exe --version`. A GUI resolve o `settings.json`, o caminho efetivo de configuração e o endpoint ACME sem modificar os arquivos.
4. Selecione uma instalação manualmente quando necessário. Instalações, endpoints e diretórios de configuração diferentes nunca são misturados.
5. Renovações inválidas, desconhecidas ou com configuração compartilhada permanecem visíveis como diagnóstico e não podem ser alteradas.

## Operar renovações

Use a busca por nome amigável, ID, domínio ou status para localizar uma renovação.

- **Renovar:** executa a renovação selecionada.
- **Forçar:** executa uma renovação forçada após confirmação adicional.
- **Cancelar:** exige digitar exatamente o nome amigável.
- **Revogar:** exige o nome amigável e deve ser usado somente quando houver comprometimento da chave.

As ações ficam desabilitadas para linhas somente leitura, sem instalação ativa ou durante outra operação. Depois de uma operação bem-sucedida, o inventário é recarregado.

## Criar um certificado

1. Abra **Novo certificado**.
2. Informe um ou mais domínios, e-mail opcional, validação, tipo de chave e armazenamento.
3. O assistente atual suporta a fonte manual, HTTP-01 ou TLS-ALPN-01, chaves RSA/EC e os armazenamentos `certificatestore`, `pemfiles` e `pfxfile`.
4. Para PEM/PFX, informe um caminho absoluto de saída.
5. Marque a aceitação dos termos Let's Encrypt, revise a prévia e escolha staging quando estiver testando.
6. Confirme a execução. A operação usa o `wacs.exe` oficial e não edita o renewal JSON diretamente.

Plugins DNS, fontes IIS, bindings, edição/clone de renovações e instalação automática em IIS não fazem parte do shell atual. Configure esses fluxos diretamente no win-acme.

## Baixar o win-acme

O botão de download aceita somente o caminho oficial x64 com digest SHA-256. O `wacs.exe` não passa por validação Authenticode; o ZIP ainda passa por preflight contra traversal, links, conflitos e conteúdo inseguro. A pasta de destino deve estar vazia e não é sobrescrita silenciosamente.

## UAC, cancelamento e segurança

Operações que alteram o sistema passam pelo worker elevado via UAC, uma operação por vez. O worker deve existir, estar assinado e compartilhar o mesmo signatário confiável da GUI. O cancelamento encerra o processo filho e aguarda seu término.

O canal GUI↔worker é um named pipe protegido em três camadas: o token compartilhado nunca aparece na linha de comando (onde qualquer processo do mesmo usuário poderia lê-lo); a GUI confirma que o processo conectado é exatamente o worker que ela iniciou antes de enviar o pedido; e toda resposta do worker é autenticada com um HMAC do token, então um peer falsificado não consegue forjar resultados de sucesso ou falha. O processo filho elevado roda supervisionado e é encerrado se a conexão cair.

A GUI não descriptografa senhas protegidas, não exibe segredos e mantém a saída do win-acme mascarada quando necessário. Valores sensíveis são mascarados na prévia, nos logs e nos diagnósticos. Consulte o [guia de solução de problemas](troubleshooting.pt-BR.md) ao receber um código de diagnóstico.
