using System.Net.Sockets;
using Microsoft.Extensions.Options;
using NModbus;

namespace UsinaUFVMaravilhaSite.Services;

/// <summary>
/// Lê/escreve no CLP via Modbus TCP. Mapa de registradores definido pelo MODBUS_Symbol_Server do CLP
/// (ver tabela fornecida): offset 1-based da tabela == endereço de protocolo (0-based) + 1.
/// </summary>
public class ModbusPlantDataSource : IPlantDataSource, IDisposable
{
    // Holding registers (offset 1-based, conforme tabela do CLP)
    private const int OffUfv1Alive = 1;
    private const int OffUfv1ReleVab = 2;
    private const int OffUfv1ReleVbc = 4;
    private const int OffUfv1ReleVca = 6;
    private const int OffUfv2Alive = 9;
    private const int OffUfv2ReleVab = 10;
    private const int OffUfv2ReleVbc = 12;
    private const int OffUfv2ReleVca = 14;
    private const int OffStatus1 = 17;
    private const int OffStatus2 = 18;
    private const int OffSlUfv1ActivePower = 19;
    private const int OffSlUfv1ReactivePower = 21;
    private const int OffSlUfv1Vab = 23;
    private const int OffSlUfv1Vbc = 25;
    private const int OffSlUfv1Vca = 27;
    private const int OffSlUfv1Ia = 29;
    private const int OffSlUfv1Ib = 31;
    private const int OffSlUfv1Ic = 33;
    private const int OffSlUfv1TotalYield = 35;
    private const int OffSlUfv1DailyYield = 37;
    private const int OffSlUfv1Pf = 39;
    private const int OffSlUfv2ActivePower = 41;
    private const int OffSlUfv2ReactivePower = 43;
    private const int OffSlUfv2Vab = 45;
    private const int OffSlUfv2Vbc = 47;
    private const int OffSlUfv2Vca = 49;
    private const int OffSlUfv2Ia = 51;
    private const int OffSlUfv2Ib = 53;
    private const int OffSlUfv2Ic = 55;
    private const int OffSlUfv2TotalYield = 57;
    private const int OffSlUfv2DailyYield = 59;
    private const int OffSlUfv2Pf = 61;

    private const int TotalRegisters = 62; // offsets 1..62 (último REAL em 61 ocupa 61-62)

    // Coils (função 15 - Write Multiple Coils), offset 1-based conforme solicitado
    private const ushort CoilResetGeral = 0;
    private const ushort CoilResetUfv1 = 1;
    private const ushort CoilResetUfv2 = 2;
    private const ushort CoilUfv1DjAbre = 3;
    private const ushort CoilUfv1DjFecha = 4;
    private const ushort CoilUfv2DjAbre = 5;
    private const ushort CoilUfv2DjFecha = 6;

    private readonly PlantOptions _options;
    private readonly ILogger<ModbusPlantDataSource> _logger;
    private readonly ModbusFactory _factory = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private TcpClient? _tcpClient;
    private IModbusMaster? _master;

    public ModbusPlantDataSource(IOptions<PlantOptions> options, ILogger<ModbusPlantDataSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlantState> ReadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var master = await EnsureConnectedAsync(cancellationToken);
            var regs = await master.ReadHoldingRegistersAsync(_options.UnitId, 0, TotalRegisters);
            return Parse(regs);
        }
        catch
        {
            DropConnection();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task ResetAsync(ResetTarget target, CancellationToken cancellationToken)
    {
        ushort coilAddress = target switch
        {
            ResetTarget.Geral => CoilResetGeral,
            ResetTarget.Ufv1 => CoilResetUfv1,
            ResetTarget.Ufv2 => CoilResetUfv2,
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };

        return PulseCoilAsync(coilAddress, cancellationToken);
    }

    public Task BreakerCommandAsync(BreakerCommand command, CancellationToken cancellationToken)
    {
        ushort coilAddress = command switch
        {
            BreakerCommand.Ufv1Abrir => CoilUfv1DjAbre,
            BreakerCommand.Ufv1Fechar => CoilUfv1DjFecha,
            BreakerCommand.Ufv2Abrir => CoilUfv2DjAbre,
            BreakerCommand.Ufv2Fechar => CoilUfv2DjFecha,
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        return PulseCoilAsync(coilAddress, cancellationToken);
    }

    /// <summary>
    /// Liga o coil uma vez; o próprio CLP desliga a memória depois de processar o comando
    /// (confirmado pelo usuário para os coils de reset - assumindo o mesmo comportamento aqui).
    /// </summary>
    private async Task PulseCoilAsync(ushort coilAddress, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var master = await EnsureConnectedAsync(cancellationToken);
            await master.WriteMultipleCoilsAsync(_options.UnitId, coilAddress, new[] { true });
        }
        catch
        {
            DropConnection();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IModbusMaster> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_master is not null && _tcpClient is { Connected: true })
        {
            return _master;
        }

        DropConnection();

        var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
        _tcpClient = client;
        _master = _factory.CreateMaster(client);
        return _master;
    }

    private void DropConnection()
    {
        _master?.Dispose();
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    private PlantState Parse(ushort[] regs)
    {
        var status1 = regs[OffStatus1 - 1];
        var status2 = regs[OffStatus2 - 1];

        var state = new PlantState
        {
            Conectado = true,
            Ufv1 = new UfvState
            {
                Nome = "UFV-01",
                Disjuntor = DeriveBreaker(GetBit(status1, 8), GetBit(status1, 9)),
                Protecoes = new ProtectionFlags
                {
                    TripT1 = GetBit(status1, 0),
                    Bloqueio86T2 = GetBit(status1, 1),
                    Sobretensao591 = GetBit(status1, 2),
                    SobretensaoNeutro59N1 = GetBit(status1, 3),
                    TrafoAlarme = GetBit(status2, 0),
                    TrafoTrip = GetBit(status2, 1),
                    TrafoFalha = GetBit(status2, 2)
                },
                Rele = new UfvRelayData
                {
                    AliveIndicator = regs[OffUfv1Alive - 1],
                    VAB = ToFloat(regs, OffUfv1ReleVab),
                    VBC = ToFloat(regs, OffUfv1ReleVbc),
                    VCA = ToFloat(regs, OffUfv1ReleVca)
                },
                Geracao = new UfvGenerationData
                {
                    ActivePowerKw = ToFloat(regs, OffSlUfv1ActivePower),
                    ReactivePowerKvar = ToFloat(regs, OffSlUfv1ReactivePower),
                    VAB = ToFloat(regs, OffSlUfv1Vab),
                    VBC = ToFloat(regs, OffSlUfv1Vbc),
                    VCA = ToFloat(regs, OffSlUfv1Vca),
                    IA = ToFloat(regs, OffSlUfv1Ia),
                    IB = ToFloat(regs, OffSlUfv1Ib),
                    IC = ToFloat(regs, OffSlUfv1Ic),
                    TotalYieldMwh = ToFloat(regs, OffSlUfv1TotalYield),
                    DailyYieldMwh = ToFloat(regs, OffSlUfv1DailyYield),
                    PowerFactor = ToFloat(regs, OffSlUfv1Pf)
                }
            },
            Ufv2 = new UfvState
            {
                Nome = "UFV-02",
                Disjuntor = DeriveBreaker(GetBit(status1, 10), GetBit(status1, 11)),
                Protecoes = new ProtectionFlags
                {
                    TripT1 = GetBit(status1, 4),
                    Bloqueio86T2 = GetBit(status1, 5),
                    Sobretensao591 = GetBit(status1, 6),
                    SobretensaoNeutro59N1 = GetBit(status1, 7),
                    TrafoAlarme = GetBit(status2, 3),
                    TrafoTrip = GetBit(status2, 4),
                    TrafoFalha = GetBit(status2, 5)
                },
                Rele = new UfvRelayData
                {
                    AliveIndicator = regs[OffUfv2Alive - 1],
                    VAB = ToFloat(regs, OffUfv2ReleVab),
                    VBC = ToFloat(regs, OffUfv2ReleVbc),
                    VCA = ToFloat(regs, OffUfv2ReleVca)
                },
                Geracao = new UfvGenerationData
                {
                    ActivePowerKw = ToFloat(regs, OffSlUfv2ActivePower),
                    ReactivePowerKvar = ToFloat(regs, OffSlUfv2ReactivePower),
                    VAB = ToFloat(regs, OffSlUfv2Vab),
                    VBC = ToFloat(regs, OffSlUfv2Vbc),
                    VCA = ToFloat(regs, OffSlUfv2Vca),
                    IA = ToFloat(regs, OffSlUfv2Ia),
                    IB = ToFloat(regs, OffSlUfv2Ib),
                    IC = ToFloat(regs, OffSlUfv2Ic),
                    TotalYieldMwh = ToFloat(regs, OffSlUfv2TotalYield),
                    DailyYieldMwh = ToFloat(regs, OffSlUfv2DailyYield),
                    PowerFactor = ToFloat(regs, OffSlUfv2Pf)
                }
            }
        };

        return state;
    }

    private float ToFloat(ushort[] regs, int offsetOneBased)
    {
        var idx = offsetOneBased - 1;
        var high = regs[idx];
        var low = regs[idx + 1];
        if (_options.SwapRealWords)
        {
            (high, low) = (low, high);
        }

        var bits = ((uint)high << 16) | low;
        return BitConverter.Int32BitsToSingle(unchecked((int)bits));
    }

    private static bool GetBit(ushort word, int bitIndex) => (word & (1 << bitIndex)) != 0;

    private static BreakerStatus DeriveBreaker(bool aberto, bool fechado)
    {
        if (aberto == fechado)
        {
            return BreakerStatus.Indeterminado;
        }

        return fechado ? BreakerStatus.Fechado : BreakerStatus.Aberto;
    }

    public void Dispose()
    {
        DropConnection();
        _lock.Dispose();
    }
}
