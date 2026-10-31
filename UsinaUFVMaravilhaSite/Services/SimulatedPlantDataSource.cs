namespace UsinaUFVMaravilhaSite.Services;

/// <summary>
/// Gera dados fictícios para permitir testar a tela sem um CLP real conectado.
/// Habilitado via "Plant:Simulate": true no appsettings.
/// </summary>
public class SimulatedPlantDataSource : IPlantDataSource
{
    private readonly Random _random = new();
    private double _dailyYield1 = 1.2;
    private double _dailyYield2 = 1.15;
    private readonly double _totalYield1 = 5_027.75;
    private readonly double _totalYield2 = 4_070.67;
    private ushort _alive1;
    private ushort _alive2;
    private BreakerStatus _breaker1 = BreakerStatus.Fechado;
    private BreakerStatus _breaker2 = BreakerStatus.Fechado;

    public Task<PlantState> ReadAsync(CancellationToken cancellationToken)
    {
        _alive1++;
        _alive2++;
        _dailyYield1 += _random.NextDouble() * 0.005;
        _dailyYield2 += _random.NextDouble() * 0.005;

        var power1 = 480 + _random.NextDouble() * 40;
        var power2 = 460 + _random.NextDouble() * 40;

        var state = new PlantState
        {
            Conectado = true,
            Ufv1 = BuildUfv("UFV-01", power1, _dailyYield1, _totalYield1, _alive1, _breaker1),
            Ufv2 = BuildUfv("UFV-02", power2, _dailyYield2, _totalYield2, _alive2, _breaker2)
        };

        return Task.FromResult(state);
    }

    public Task ResetAsync(ResetTarget target, CancellationToken cancellationToken)
    {
        // Modo simulado: nada para resetar no CLP, apenas confirma a chamada.
        return Task.CompletedTask;
    }

    public Task BreakerCommandAsync(BreakerCommand command, CancellationToken cancellationToken)
    {
        // Modo simulado: aplica o estado localmente pra permitir testar os botões sem CLP real.
        switch (command)
        {
            case BreakerCommand.Ufv1Abrir:
                _breaker1 = BreakerStatus.Aberto;
                break;
            case BreakerCommand.Ufv1Fechar:
                _breaker1 = BreakerStatus.Fechado;
                break;
            case BreakerCommand.Ufv2Abrir:
                _breaker2 = BreakerStatus.Aberto;
                break;
            case BreakerCommand.Ufv2Fechar:
                _breaker2 = BreakerStatus.Fechado;
                break;
        }

        return Task.CompletedTask;
    }

    private UfvState BuildUfv(string nome, double activePowerKw, double dailyYieldMwh, double totalYieldMwh, ushort alive, BreakerStatus breaker)
    {
        return new UfvState
        {
            Nome = nome,
            Disjuntor = breaker,
            ComunicacaoRele = true,
            Protecoes = new ProtectionFlags(),
            Rele = new UfvRelayData
            {
                AliveIndicator = alive,
                VAB = 13800 + (float)(_random.NextDouble() * 20 - 10),
                VBC = 13800 + (float)(_random.NextDouble() * 20 - 10),
                VCA = 13800 + (float)(_random.NextDouble() * 20 - 10)
            },
            Geracao = new UfvGenerationData
            {
                ActivePowerKw = (float)activePowerKw,
                ReactivePowerKvar = (float)(activePowerKw * 0.08),
                VAB = 380 + (float)(_random.NextDouble() * 4 - 2),
                VBC = 380 + (float)(_random.NextDouble() * 4 - 2),
                VCA = 380 + (float)(_random.NextDouble() * 4 - 2),
                IA = (float)(activePowerKw / 0.66),
                IB = (float)(activePowerKw / 0.66),
                IC = (float)(activePowerKw / 0.66),
                TotalYieldMwh = (float)totalYieldMwh,
                DailyYieldMwh = (float)dailyYieldMwh,
                PowerFactor = 0.98f
            }
        };
    }
}
