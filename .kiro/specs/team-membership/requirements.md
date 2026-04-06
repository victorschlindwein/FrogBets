# Documento de Requisitos

## Introdução

O FrogBets é uma plataforma fechada de apostas virtuais P2P. Esta feature introduz o conceito de **pertencimento a times** para os usuários da plataforma. Um usuário pode ser vinculado a um `CS2Team` no momento do cadastro ou posteriormente por um líder de time ou pelo Administrador. Além disso, é introduzido o papel de **Líder de Time**, que possui acesso privilegiado ao marketplace de trocas de jogadores — uma nova seção separada do marketplace de apostas P2P já existente. O marketplace de apostas P2P não é alterado por esta feature.

---

## Glossário

- **FrogBets**: A plataforma web de apostas P2P descrita neste documento.
- **Administrador**: Usuário com `IsAdmin = true`, com permissões elevadas sobre toda a plataforma.
- **Usuário**: Membro autenticado da plataforma FrogBets, representado pela entidade `User`.
- **Time**: Entidade `CS2Team` representando um time de CS2 cadastrado na plataforma.
- **Líder de Time**: Usuário com `IsTeamLeader = true` vinculado a um Time específico, com permissões privilegiadas sobre os membros desse Time.
- **Membro**: Usuário cujo campo `TeamId` aponta para um Time.
- **Marketplace de Apostas P2P**: Seção existente do marketplace onde usuários cobrem apostas de outros usuários. Não é alterada por esta feature.
- **Marketplace de Trocas**: Nova seção do marketplace onde Líderes de Time propõem e aceitam trocas de Membros entre Times.
- **Oferta de Troca**: Proposta criada por um Líder de Time para trocar um Membro do seu Time por um Membro de outro Time que está disponível para troca.
- **Disponível para Troca**: Estado de um Membro marcado pelo seu Líder de Time como elegível para receber Ofertas de Troca.
- **TeamMembershipService**: Serviço responsável pelas operações de vínculo de usuários a times e gestão de líderes.
- **TradeService**: Serviço responsável pelas operações do Marketplace de Trocas.

---

## Requisitos

### Requisito 1: Vínculo de Time no Cadastro

**User Story:** Como convidado, quero opcionalmente selecionar um time durante o cadastro, para que eu já entre na plataforma vinculado ao meu time.

#### Critérios de Aceitação

1. THE FrogBets SHALL disponibilizar no formulário de cadastro um campo opcional de seleção de Time, exibindo a lista de todos os Times cadastrados na plataforma.
2. WHEN um convidado submete o formulário de cadastro sem selecionar um Time, THE TeamMembershipService SHALL criar o Usuário com `TeamId` nulo.
3. WHEN um convidado submete o formulário de cadastro com um Time selecionado, THE TeamMembershipService SHALL criar o Usuário com o `TeamId` correspondente ao Time selecionado.
4. IF o `TeamId` informado no cadastro não corresponder a um Time existente, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro indicando que o Time não foi encontrado.
5. THE FrogBets SHALL exibir o Time do Usuário no perfil ou dashboard após o cadastro, quando o `TeamId` estiver preenchido.

---

### Requisito 2: Papel de Líder de Time

**User Story:** Como Administrador, quero designar e remover líderes de times, para que cada time tenha um responsável com permissões privilegiadas.

#### Critérios de Aceitação

1. THE FrogBets SHALL adicionar o campo `IsTeamLeader` (booleano, padrão `false`) à entidade `User`.
2. WHEN o Administrador designa um Usuário como Líder de Time, THE TeamMembershipService SHALL definir `IsTeamLeader = true` para esse Usuário.
3. WHEN o Administrador remove o papel de Líder de Time de um Usuário, THE TeamMembershipService SHALL definir `IsTeamLeader = false` para esse Usuário.
4. THE TeamMembershipService SHALL garantir que cada Time possua no máximo um Líder de Time ativo simultaneamente.
5. IF o Administrador tenta designar como Líder um Usuário que não pertence ao Time em questão, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro indicando que o Usuário não é membro do Time.
6. IF o Administrador tenta designar como Líder um Usuário que já é Líder de outro Time, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro indicando que o Usuário já é Líder de outro Time.
7. WHEN um Usuário com `IsTeamLeader = true` é removido do seu Time, THE TeamMembershipService SHALL automaticamente definir `IsTeamLeader = false` para esse Usuário.

---

### Requisito 3: Movimentação de Usuários entre Times

**User Story:** Como Líder de Time ou Administrador, quero mover usuários entre times fora do marketplace, para que eu possa corrigir vínculos de time manualmente.

#### Critérios de Aceitação

1. WHEN o Líder de Time solicita a movimentação de um Membro do seu próprio Time para outro Time, THE TeamMembershipService SHALL atualizar o `TeamId` do Membro para o Time de destino.
2. WHEN o Administrador solicita a movimentação de qualquer Usuário para qualquer Time, THE TeamMembershipService SHALL atualizar o `TeamId` do Usuário para o Time de destino.
3. WHEN o Administrador solicita a remoção de um Usuário de seu Time (sem atribuir novo Time), THE TeamMembershipService SHALL definir o `TeamId` do Usuário como nulo.
4. IF um Usuário sem papel de Líder de Time ou sem `IsAdmin = true` tenta mover outro Usuário de Time, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro de autorização.
5. IF o Líder de Time tenta mover um Membro que não pertence ao seu próprio Time, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro de autorização.
6. IF o Time de destino informado não existir, THEN THE TeamMembershipService SHALL rejeitar a operação e retornar um erro indicando que o Time não foi encontrado.

---

### Requisito 4: Disponibilização de Membros para Troca no Marketplace

**User Story:** Como Líder de Time, quero marcar membros do meu time como disponíveis para troca, para que outros líderes possam fazer ofertas por eles.

#### Critérios de Aceitação

1. WHEN o Líder de Time marca um Membro do seu Time como Disponível para Troca, THE TradeService SHALL registrar esse Membro como disponível no Marketplace de Trocas.
2. WHEN o Líder de Time remove a disponibilidade de um Membro do seu Time, THE TradeService SHALL remover esse Membro da listagem de disponíveis no Marketplace de Trocas.
3. IF o Líder de Time tenta marcar como disponível um Membro que não pertence ao seu Time, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.
4. IF um Usuário sem papel de Líder de Time ou sem `IsAdmin = true` tenta marcar um Membro como disponível, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.
5. THE FrogBets SHALL exibir no Marketplace de Trocas a lista de todos os Membros atualmente marcados como Disponíveis para Troca, agrupados por Time.
6. WHEN um Membro marcado como Disponível para Troca é transferido de Time (por troca aceita ou movimentação direta), THE TradeService SHALL automaticamente remover a disponibilidade desse Membro.

---

### Requisito 5: Criação de Ofertas de Troca

**User Story:** Como Líder de Time, quero fazer uma oferta de troca propondo meu jogador por um jogador disponível de outro time, para que possamos negociar a composição dos times.

#### Critérios de Aceitação

1. WHEN o Líder de Time cria uma Oferta de Troca, THE TradeService SHALL registrar a oferta contendo: o Membro ofertado (do time do Líder proponente), o Membro alvo (Disponível para Troca em outro Time) e o status inicial `Pendente`.
2. IF o Membro ofertado não pertencer ao Time do Líder proponente, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.
3. IF o Membro alvo não estiver marcado como Disponível para Troca, THEN THE TradeService SHALL rejeitar a operação e retornar um erro indicando que o Membro alvo não está disponível para troca.
4. IF o Membro alvo pertencer ao mesmo Time do Líder proponente, THEN THE TradeService SHALL rejeitar a operação e retornar um erro indicando que não é possível trocar membros do mesmo time.
5. IF um Usuário sem papel de Líder de Time ou sem `IsAdmin = true` tenta criar uma Oferta de Troca, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.
6. THE FrogBets SHALL exibir ao Líder de Time as Ofertas de Troca recebidas (onde um Membro do seu Time é o alvo) com status `Pendente`.

---

### Requisito 6: Aceitação e Recusa de Ofertas de Troca

**User Story:** Como Líder de Time, quero aceitar ou recusar ofertas de troca recebidas, para que eu tenha controle sobre as mudanças na composição do meu time.

#### Critérios de Aceitação

1. WHEN o Líder de Time aceita uma Oferta de Troca com status `Pendente`, THE TradeService SHALL atualizar o `TeamId` do Membro ofertado para o Time do Líder que aceitou e o `TeamId` do Membro alvo para o Time do Líder proponente.
2. WHEN uma Oferta de Troca é aceita, THE TradeService SHALL atualizar o status da Oferta para `Aceita`.
3. WHEN uma Oferta de Troca é aceita, THE TradeService SHALL remover automaticamente a marcação de Disponível para Troca dos dois Membros envolvidos.
4. WHEN o Líder de Time recusa uma Oferta de Troca com status `Pendente`, THE TradeService SHALL atualizar o status da Oferta para `Recusada` sem alterar o `TeamId` de nenhum Membro.
5. IF o Líder de Time tenta aceitar ou recusar uma Oferta de Troca cujo Membro alvo não pertence ao seu Time, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.
6. IF o Líder de Time tenta aceitar ou recusar uma Oferta de Troca com status diferente de `Pendente`, THEN THE TradeService SHALL rejeitar a operação e retornar um erro indicando que a oferta não está mais pendente.
7. WHEN uma Oferta de Troca é aceita, THE TradeService SHALL cancelar automaticamente todas as outras Ofertas de Troca `Pendente` que envolvam qualquer um dos dois Membros trocados.

---

### Requisito 7: Troca Direta pelo Administrador

**User Story:** Como Administrador, quero realizar trocas de membros entre times diretamente, sem necessidade de oferta e aceite, para que eu possa corrigir situações excepcionais rapidamente.

#### Critérios de Aceitação

1. WHEN o Administrador solicita uma troca direta entre dois Membros de Times distintos, THE TradeService SHALL atualizar o `TeamId` de cada Membro para o Time do outro Membro.
2. WHEN o Administrador realiza uma troca direta, THE TradeService SHALL cancelar automaticamente todas as Ofertas de Troca `Pendente` que envolvam qualquer um dos dois Membros trocados.
3. WHEN o Administrador realiza uma troca direta, THE TradeService SHALL remover automaticamente a marcação de Disponível para Troca dos dois Membros envolvidos.
4. IF os dois Membros informados pertencerem ao mesmo Time, THEN THE TradeService SHALL rejeitar a operação e retornar um erro indicando que os Membros pertencem ao mesmo Time.
5. IF um Usuário sem `IsAdmin = true` tenta realizar uma troca direta, THEN THE TradeService SHALL rejeitar a operação e retornar um erro de autorização.

---

### Requisito 8: Criação de Apostas por Qualquer Usuário Autenticado

**User Story:** Como usuário autenticado, quero criar apostas em mercados abertos, para que eu possa participar das apostas P2P da plataforma independentemente do meu vínculo com um time.

#### Critérios de Aceitação

1. THE FrogBets SHALL permitir que qualquer Usuário autenticado crie apostas em mercados com status `Aberto`, independentemente de possuir `TeamId` preenchido, `IsTeamLeader = true` ou `IsAdmin = true`.
2. WHEN um Usuário autenticado submete uma aposta válida em um mercado aberto, THE BetsController SHALL processar a criação da aposta sem verificar o vínculo de time do Usuário.
3. THE FrogBets SHALL garantir que nenhuma restrição de papel (Líder de Time, membro de time específico) seja aplicada ao endpoint de criação de apostas P2P.
