# UsinaUFVMaravilhaSite

Monitoramento em tempo real, via Modbus TCP, de duas UFVs (usinas fotovoltaicas) idênticas:

| UFV | Relé de proteção | Smartlogger |
|---|---|---|
| UFV1 | Relé1 | Smartlogger1 |
| UFV2 | Relé2 | Smartlogger2 |

A tela mostra status do disjuntor de acoplamento, proteções atuadas e dados de geração de
cada UFV, e permite comandos de reset e de abrir/fechar disjuntor.

## Arquitetura

```
CLP (Modbus TCP) --3s--> ModbusPlantDataSource --> PlantStateStore (em memória)
                                                          |
                                                   GET /api/plant/state (JSON)
                                                          |
                                                 navegador faz polling a cada 3s
```

- [`PlantPollingService`](UsinaUFVMaravilhaSite/Services/PlantPollingService.cs) roda em
  background e lê todos os 62 holding registers do CLP numa única requisição Modbus (função 3),
  a cada `Plant:PollingIntervalMs` (padrão 3000ms).
- [`ModbusPlantDataSource`](UsinaUFVMaravilhaSite/Services/ModbusPlantDataSource.cs) decodifica
  o array de `ushort` cru em [`PlantState`](UsinaUFVMaravilhaSite/Services/PlantModels.cs)
  (WORDs, bits de status, pares de registrador como `float` pros valores REAL).
- O resultado fica em memória (`PlantStateStore`) e é servido como JSON por
  `GET /api/plant/state` (sem banco de dados, sem SignalR — só polling HTTP simples no
  navegador via [`dashboard.js`](UsinaUFVMaravilhaSite/wwwroot/js/dashboard.js)).
- Comandos (reset, abrir/fechar disjuntor) são o caminho inverso: `POST` sem corpo num
  endpoint, que escreve direto no coil Modbus correspondente.
- [`SimulatedPlantDataSource`](UsinaUFVMaravilhaSite/Services/SimulatedPlantDataSource.cs)
  gera dados fictícios pra testar a tela sem CLP real (ativo por padrão, ver "Rodando
  localmente" abaixo).

## Mapa de registradores do CLP

Fonte: `MODBUS_Symbol_Server` do CLP. **O offset da tabela (coluna "Registro") é 1-based; o
endereço de protocolo Modbus (0-based, o que a lib usa) é `offset - 1`.** REAL ocupa 2
registradores consecutivos (32 bits), word alta primeiro (`SwapRealWords: false` — ver
"Observações" abaixo).

| Tag | Tipo | Registro | Endereço CLP |
|---|---|---|---|
| `MRR_P3U30_UFV1_AliveIndicator` | WORD | 1 | 400001 |
| `MRR_P3U30_UFV1_VAB` | REAL | 2 | 400002 |
| `MRR_P3U30_UFV1_VBC` | REAL | 4 | 400004 |
| `MRR_P3U30_UFV1_VCA` | REAL | 6 | 400006 |
| `MRR_DJ_ACOPL_UFV1_Estado` (lido, não usado — ver Observações) | WORD | 8 | 400008 |
| `MRR_P3U30_UFV2_AliveIndicator` | WORD | 9 | 400009 |
| `MRR_P3U30_UFV2_VAB` | REAL | 10 | 400010 |
| `MRR_P3U30_UFV2_VBC` | REAL | 12 | 400012 |
| `MRR_P3U30_UFV2_VCA` | REAL | 14 | 400014 |
| `MRR_DJ_ACOPL_UFV2_Estado` (lido, não usado) | WORD | 16 | 400016 |
| `MRR_Status1` (bitmap — ver tabela abaixo) | WORD | 17 | 400017 |
| `MRR_Status2` (bitmap — ver tabela abaixo) | WORD | 18 | 400018 |
| `MRR_SL_UFV1_ActivePower` | REAL | 19 | 400019 |
| `MRR_SL_UFV1_ReactivePower` | REAL | 21 | 400021 |
| `MRR_SL_UFV1_VAB` | REAL | 23 | 400023 |
| `MRR_SL_UFV1_VBC` | REAL | 25 | 400025 |
| `MRR_SL_UFV1_VCA` | REAL | 27 | 400027 |
| `MRR_SL_UFV1_IA` | REAL | 29 | 400029 |
| `MRR_SL_UFV1_IB` | REAL | 31 | 400031 |
| `MRR_SL_UFV1_IC` | REAL | 33 | 400033 |
| `MRR_SL_UFV1_TotalYield` (**MWh**, não kWh) | REAL | 35 | 400035 |
| `MRR_SL_UFV1_DailyYield` (**MWh**, não kWh) | REAL | 37 | 400037 |
| `MRR_SL_UFV1_PF` | REAL | 39 | 400039 |
| `MRR_SL_UFV2_ActivePower` | REAL | 41 | 400041 |
| `MRR_SL_UFV2_ReactivePower` | REAL | 43 | 400043 |
| `MRR_SL_UFV2_VAB` | REAL | 45 | 400045 |
| `MRR_SL_UFV2_VBC` | REAL | 47 | 400047 |
| `MRR_SL_UFV2_VCA` | REAL | 49 | 400049 |
| `MRR_SL_UFV2_IA` | REAL | 51 | 400051 |
| `MRR_SL_UFV2_IB` | REAL | 53 | 400053 |
| `MRR_SL_UFV2_IC` | REAL | 55 | 400055 |
| `MRR_SL_UFV2_TotalYield` (**MWh**, não kWh) | REAL | 57 | 400057 |
| `MRR_SL_UFV2_DailyYield` (**MWh**, não kWh) | REAL | 59 | 400059 |
| `MRR_SL_UFV2_PF` | REAL | 61 | 400061 |

### `MRR_Status1` (bitmap)

| Bit | Significado |
|---|---|
| B0 | Proteção UFV1 — TRIP T1 |
| B1 | Proteção UFV1 — 86 T2 (bloqueio) |
| B2 | Proteção UFV1 — 59-1 (sobretensão) |
| B3 | Proteção UFV1 — 59N1 (sobretensão de neutro) |
| B4 | Proteção UFV2 — TRIP T1 |
| B5 | Proteção UFV2 — 86 T2 |
| B6 | Proteção UFV2 — 59-1 |
| B7 | Proteção UFV2 — 59N1 |
| B8 | Disjuntor acoplamento UFV1 — Aberto |
| B9 | Disjuntor acoplamento UFV1 — Fechado |
| B10 | Disjuntor acoplamento UFV2 — Aberto |
| B11 | Disjuntor acoplamento UFV2 — Fechado |
| B12–B15 | não usados |

Disjuntor "Aberto" e "Fechado" são dois bits independentes (não um só): os dois `false` ou os
dois `true` ao mesmo tempo são tratados como estado indeterminado.

### `MRR_Status2` (bitmap)

| Bit | Significado |
|---|---|
| B0 | Relé trafo UFV1 — Alarme |
| B1 | Relé trafo UFV1 — Trip |
| B2 | Relé trafo UFV1 — Falha |
| B3 | Relé trafo UFV2 — Alarme |
| B4 | Relé trafo UFV2 — Trip |
| B5 | Relé trafo UFV2 — Falha |
| B6–B15 | não usados |

## Coils (comandos de escrita)

Escritos via função 15 (Write Multiple Coils, com 1 único coil). **O CLP desliga a memória
sozinho depois de processar o comando — a aplicação só escreve `True`, nunca escreve `False`
de volta.**

| Tag | Endereço CLP | Ação |
|---|---|---|
| `MX_CMD_Reset` | 1 | Reset geral |
| `MX_CMD_UFV1_Reset` | 2 | Reset UFV1 |
| `MX_CMD_UFV2_Reset` | 3 | Reset UFV2 |
| `MX_UFV1_DJ_Abre` | 4 | Abrir disjuntor UFV1 |
| `MX_UFV1_DJ_Fecha` | 5 | Fechar disjuntor UFV1 |
| `MX_UFV2_DJ_Abre` | 6 | Abrir disjuntor UFV2 |
| `MX_UFV2_DJ_Fecha` | 7 | Fechar disjuntor UFV2 |

## Configuração

Seção `Plant` do `appsettings.json` (ou variáveis de ambiente `Plant__<Chave>` em produção —
ver `deploy/`):

| Chave | Descrição | Padrão |
|---|---|---|
| `Simulate` | `true` usa dados fictícios, `false` conecta no CLP real | `true` |
| `Host` / `Port` / `UnitId` | Endereço Modbus TCP do CLP | placeholder |
| `PollingIntervalMs` | Intervalo de leitura | `3000` |
| `SwapRealWords` | Inverte a ordem das words dos REAL, se os valores vierem sem sentido | `false` |

## Rodando localmente

Por padrão (`appsettings.json` e `appsettings.Development.json`) a aplicação roda em modo
**simulado** — não precisa de um CLP real pra ver a tela funcionando:

```bash
dotnet run --project UsinaUFVMaravilhaSite/UsinaUFVMaravilhaSite.csproj
```

## Deploy

Ver [`deploy/`](deploy/) — arquivos de systemd, Nginx e `.env.example` pra CentOS/Nginx
(mesmo padrão usado no ReportSolarWeb), e `deploy/update.sh` pra atualizações de rotina.

## Observações / decisões não óbvias

- **Unidade de `TotalYield`/`DailyYield` é MWh, não kWh** — confirmado comparando a ordem de
  grandeza do valor real contra o esperado fisicamente pra potência nominal da planta (a tag
  não tem unidade explícita).
- **`MRR_DJ_ACOPL_UFVx_Estado`** é lido mas não usado — o status do disjuntor vem só dos bits
  `Aberto`/`Fechado` do `MRR_Status1`.
- **Comandos de reset e disjuntor nunca escrevem `False` de volta no coil** — confirmado que o
  CLP já limpa a memória sozinho.
- **Ordem das words do REAL** (`SwapRealWords: false`) foi validada com dado real: a corrente
  lida bate com `P = √3 · V · I · FP` pra a tensão/potência também lidas.
