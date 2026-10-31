namespace UsinaUFVMaravilhaSite.Services;

/// <summary>
/// Último estado lido da planta, compartilhado entre o serviço de polling e os endpoints HTTP.
/// </summary>
public class PlantStateStore
{
    private readonly object _gate = new();
    private PlantState _current = new() { Conectado = false, UltimoErro = "Aguardando primeira leitura" };

    public PlantState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
        set
        {
            lock (_gate)
            {
                _current = value;
            }
        }
    }
}
