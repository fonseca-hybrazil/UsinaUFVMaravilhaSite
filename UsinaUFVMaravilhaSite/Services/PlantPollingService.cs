using Microsoft.Extensions.Options;

namespace UsinaUFVMaravilhaSite.Services;

/// <summary>
/// Lê a planta periodicamente (intervalo configurável, padrão 3s) e publica o resultado no <see cref="PlantStateStore"/>.
/// Em caso de falha de leitura, mantém os últimos valores bons na tela e apenas sinaliza "desconectado".
/// </summary>
public class PlantPollingService : BackgroundService
{
    private readonly IPlantDataSource _dataSource;
    private readonly PlantStateStore _store;
    private readonly PlantOptions _options;
    private readonly ILogger<PlantPollingService> _logger;

    private ushort? _lastAlive1;
    private ushort? _lastAlive2;
    private int _staleCount1;
    private int _staleCount2;
    private const int StaleThreshold = 3; // ciclos sem variação no AliveIndicator até considerar comunicação do relé perdida

    public PlantPollingService(
        IPlantDataSource dataSource,
        PlantStateStore store,
        IOptions<PlantOptions> options,
        ILogger<PlantPollingService> logger)
    {
        _dataSource = dataSource;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(500, _options.PollingIntervalMs));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = await _dataSource.ReadAsync(stoppingToken);
                state.Ufv1.ComunicacaoRele = TrackAlive(state.Ufv1.Rele.AliveIndicator, ref _lastAlive1, ref _staleCount1);
                state.Ufv2.ComunicacaoRele = TrackAlive(state.Ufv2.Rele.AliveIndicator, ref _lastAlive2, ref _staleCount2);
                state.TimestampUtc = DateTimeOffset.UtcNow;
                _store.Current = state;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao ler dados da planta via Modbus");
                var previous = _store.Current;
                previous.Conectado = false;
                previous.UltimoErro = ex.Message;
                previous.TimestampUtc = DateTimeOffset.UtcNow;
                _store.Current = previous;
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static bool TrackAlive(ushort current, ref ushort? last, ref int staleCount)
    {
        if (last is null)
        {
            last = current;
            staleCount = 0;
            return true;
        }

        if (current != last)
        {
            last = current;
            staleCount = 0;
            return true;
        }

        last = current;
        staleCount++;
        return staleCount < StaleThreshold;
    }
}
