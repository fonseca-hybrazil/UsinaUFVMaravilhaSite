namespace UsinaUFVMaravilhaSite.Services;

public interface IPlantDataSource
{
    Task<PlantState> ReadAsync(CancellationToken cancellationToken);

    Task ResetAsync(ResetTarget target, CancellationToken cancellationToken);

    Task BreakerCommandAsync(BreakerCommand command, CancellationToken cancellationToken);
}
