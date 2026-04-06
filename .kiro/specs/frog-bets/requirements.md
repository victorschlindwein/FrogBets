# Requirements Document

## Introduction

FrogBets é uma plataforma web interna para membros do grupo FrogEventos realizarem apostas P2P (peer-to-peer) nos jogos de Counter-Strike organizados pelo grupo. A plataforma substitui as apostas informais feitas "boca a boca" no WhatsApp, oferecendo um ambiente centralizado para criação, cobertura e liquidação de apostas. Não há envolvimento de dinheiro real: todos os valores são fictícios e servem exclusivamente como métrica de desempenho. Não há envolvimento de casa: toda aposta precisa ser coberta por outro usuário para ser válida. Apostas combinadas (parlay) não são suportadas.

## Glossary

- **FrogBets**: A plataforma web de apostas P2P descrita neste documento.
- **Usuário**: Membro autenticado do grupo FrogEventos que utiliza a plataforma.
- **Aposta**: Proposta de um Usuário sobre o resultado de um evento de um Jogo, com valor em Saldo Virtual.
- **Cobertura**: Ação de um segundo Usuário aceitar a posição oposta de uma Aposta existente, tornando-a válida.
- **Aposta Pendente**: Aposta criada mas ainda não coberta por outro Usuário.
- **Aposta Ativa**: Aposta que foi coberta e está aguardando o resultado do Jogo.
- **Aposta Liquidada**: Aposta cujo resultado foi registrado e os valores foram distribuídos.
- **Jogo**: Partida ou série de partidas de Counter-Strike organizada pelo FrogEventos.
- **Mapa**: Partida individual dentro de uma série de um Jogo.
- **Série**: Conjunto de Mapas que compõem um confronto entre dois times.
- **Time**: Equipe participante de um Jogo.
- **Jogador**: Membro de um Time que participa de um Jogo.
- **Mercado**: Tipo de evento sobre o qual uma Aposta pode ser feita (ex: vencedor do Mapa, top kills).
- **Saldo Virtual**: Valor fictício em moeda virtual disponível na conta do Usuário dentro da plataforma, sem qualquer correspondência a dinheiro real. Serve exclusivamente como métrica de desempenho nas apostas.
- **Administrador**: Usuário com permissão para gerenciar Jogos, registrar resultados e moderar a plataforma.

---

## Requirements

### Requirement 1: Autenticação de Usuários

**User Story:** Como membro do FrogEventos, quero me autenticar na plataforma FrogBets, para que apenas membros do grupo possam acessar e realizar apostas.

#### Acceptance Criteria

1. THE FrogBets SHALL exigir autenticação para acesso a qualquer funcionalidade de apostas.
2. WHEN um Usuário fornece credenciais válidas, THE FrogBets SHALL autenticar o Usuário e iniciar uma sessão.
3. IF um Usuário fornece credenciais inválidas, THEN THE FrogBets SHALL retornar uma mensagem de erro sem revelar qual campo está incorreto.
4. WHEN a sessão de um Usuário expira, THE FrogBets SHALL redirecionar o Usuário para a tela de autenticação.
5. WHEN um Usuário realiza logout, THE FrogBets SHALL encerrar a sessão e invalidar o token de acesso.

---

### Requirement 2: Gerenciamento de Saldo Virtual

**User Story:** Como Usuário, quero ter um Saldo Virtual na plataforma, para que eu possa realizar apostas e acompanhar meu desempenho sem envolvimento de dinheiro real.

#### Acceptance Criteria

1. THE FrogBets SHALL manter um Saldo Virtual individual para cada Usuário, sem qualquer correspondência a dinheiro real.
2. WHEN um novo Usuário se cadastra, THE FrogBets SHALL atribuir automaticamente um Saldo Virtual inicial de 1000 unidades ao Usuário.
3. WHEN uma Aposta é criada, THE FrogBets SHALL reservar o valor da Aposta do Saldo Virtual do Usuário criador.
4. WHEN uma Aposta Pendente é cancelada pelo criador, THE FrogBets SHALL devolver o valor reservado ao Saldo Virtual do Usuário criador.
5. WHEN uma Cobertura é realizada, THE FrogBets SHALL reservar o valor da Cobertura do Saldo Virtual do Usuário que cobriu.
6. WHEN uma Aposta é Liquidada com resultado favorável ao Usuário, THE FrogBets SHALL creditar o valor total da Aposta (valor próprio mais valor da Cobertura) ao Saldo Virtual do vencedor.
7. IF um Usuário tenta criar uma Aposta com valor superior ao seu Saldo Virtual disponível, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando o Saldo Virtual insuficiente.
8. IF um Usuário tenta cobrir uma Aposta com valor superior ao seu Saldo Virtual disponível, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando o Saldo Virtual insuficiente.
9. THE FrogBets SHALL exibir ao Usuário o Saldo Virtual disponível e o valor total reservado em Apostas Ativas.

---

### Requirement 3: Gerenciamento de Jogos pelo Administrador

**User Story:** Como Administrador, quero cadastrar e gerenciar Jogos, para que os Usuários possam realizar apostas nos confrontos do campeonato.

#### Acceptance Criteria

1. THE Administrador SHALL cadastrar Jogos informando os Times participantes, data e formato (número de Mapas da Série).
2. WHEN um Jogo é cadastrado, THE FrogBets SHALL disponibilizar o Jogo para criação de Apostas.
3. WHEN um Jogo é iniciado pelo Administrador, THE FrogBets SHALL encerrar a criação de novas Apostas para aquele Jogo.
4. WHEN o Administrador registra o resultado de um Mapa, THE FrogBets SHALL armazenar o resultado e atualizar os Mercados relacionados ao Mapa.
5. WHEN o Administrador registra o resultado completo de uma Série, THE FrogBets SHALL liquidar todas as Apostas Ativas relacionadas àquela Série.
6. IF o Administrador tenta registrar um resultado para um Jogo já Liquidado, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem de erro.

---

### Requirement 4: Criação de Apostas

**User Story:** Como Usuário, quero criar apostas em Mercados disponíveis de um Jogo, para que eu possa competir com outros membros do grupo.

#### Acceptance Criteria

1. WHEN um Jogo está disponível para apostas, THE FrogBets SHALL exibir os Mercados disponíveis para aquele Jogo.
2. WHEN um Usuário cria uma Aposta, THE FrogBets SHALL registrar o Mercado escolhido, a opção selecionada, o valor apostado e o Usuário criador.
3. THE FrogBets SHALL suportar os seguintes Mercados: vencedor do Mapa, vencedor da Série, top kills da partida, Jogador com mais mortes, Jogador com maior dano por utilitários.
4. IF um Usuário tenta criar uma Aposta em um Jogo que já foi iniciado, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando que as apostas estão encerradas.
5. THE FrogBets SHALL proibir a criação de Apostas combinadas (parlay), aceitando apenas Apostas em Mercados individuais.
6. IF um Usuário tenta criar uma segunda Aposta no mesmo Mercado do mesmo Jogo, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando que já existe uma Aposta do Usuário naquele Mercado.

---

### Requirement 5: Cobertura de Apostas (P2P)

**User Story:** Como Usuário, quero cobrir apostas criadas por outros membros, para que as apostas se tornem válidas e o confronto seja estabelecido.

#### Acceptance Criteria

1. THE FrogBets SHALL exibir todas as Apostas Pendentes disponíveis para cobertura.
2. WHEN um Usuário cobre uma Aposta Pendente, THE FrogBets SHALL registrar o Usuário como contraparte, reservar o valor do Saldo do Usuário e alterar o status da Aposta para Ativa.
3. IF um Usuário tenta cobrir a própria Aposta, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem de erro.
4. IF um Usuário tenta cobrir uma Aposta que já foi coberta por outro Usuário, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando que a Aposta não está mais disponível.
5. WHEN um Usuário cobre uma Aposta, THE FrogBets SHALL atribuir automaticamente a opção oposta ao Usuário que cobriu.
6. THE FrogBets SHALL exibir ao Usuário criador uma notificação quando a Aposta Pendente for coberta.

---

### Requirement 6: Cancelamento de Apostas

**User Story:** Como Usuário, quero cancelar apostas que ainda não foram cobertas, para que eu possa recuperar meu saldo caso mude de ideia.

#### Acceptance Criteria

1. WHEN uma Aposta está com status Pendente, THE FrogBets SHALL permitir que o Usuário criador cancele a Aposta.
2. WHEN uma Aposta Pendente é cancelada, THE FrogBets SHALL devolver o valor reservado ao Saldo do Usuário criador e remover a Aposta da listagem de Apostas Pendentes.
3. IF um Usuário tenta cancelar uma Aposta Ativa, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem informando que a Aposta já foi coberta e não pode ser cancelada.
4. IF um Usuário tenta cancelar uma Aposta de outro Usuário, THEN THE FrogBets SHALL rejeitar a operação e exibir uma mensagem de erro.

---

### Requirement 7: Liquidação de Apostas

**User Story:** Como Administrador, quero registrar os resultados dos Jogos, para que as apostas sejam liquidadas automaticamente e os saldos sejam atualizados.

#### Acceptance Criteria

1. WHEN o Administrador registra o resultado de um Mercado, THE FrogBets SHALL identificar todas as Apostas Ativas relacionadas àquele Mercado.
2. WHEN uma Aposta é Liquidada, THE FrogBets SHALL creditar o valor total (valor do criador mais valor da cobertura) ao Saldo do Usuário vencedor.
3. WHEN uma Aposta é Liquidada, THE FrogBets SHALL registrar o resultado e alterar o status da Aposta para Liquidada.
4. THE FrogBets SHALL exibir ao Usuário o histórico de Apostas Liquidadas com o resultado e o valor ganho ou perdido.
5. IF o resultado de um Mercado for empate e o Mercado não admitir empate, THEN THE FrogBets SHALL devolver os valores reservados a ambos os Usuários e registrar a Aposta como anulada.

---

### Requirement 8: Visualização de Apostas e Histórico

**User Story:** Como Usuário, quero visualizar minhas apostas e o histórico de resultados, para que eu possa acompanhar meu desempenho na plataforma.

#### Acceptance Criteria

1. THE FrogBets SHALL exibir ao Usuário uma lista de suas Apostas Pendentes, Ativas e Liquidadas.
2. THE FrogBets SHALL exibir para cada Aposta: o Mercado, a opção escolhida, o valor apostado, o Usuário contraparte (quando existir) e o status atual.
3. WHEN um Usuário acessa o histórico, THE FrogBets SHALL exibir as Apostas Liquidadas ordenadas da mais recente para a mais antiga.
4. THE FrogBets SHALL exibir uma página pública de Apostas Pendentes de todos os Usuários, permitindo que qualquer Usuário autenticado visualize e cubra apostas disponíveis.

---

### Requirement 9: Tabela de Classificação de Apostadores

**User Story:** Como Usuário, quero visualizar um ranking dos apostadores, para que eu possa comparar meu desempenho com o dos outros membros do grupo.

#### Acceptance Criteria

1. THE FrogBets SHALL exibir uma tabela de classificação com todos os Usuários ordenados pelo Saldo Virtual atual em ordem decrescente, refletindo o desempenho de cada Usuário nas apostas.
2. THE FrogBets SHALL atualizar a tabela de classificação após cada liquidação de Aposta.
3. THE FrogBets SHALL exibir na tabela de classificação o nome do Usuário, o Saldo Virtual atual e o número total de Apostas Liquidadas ganhas e perdidas.
