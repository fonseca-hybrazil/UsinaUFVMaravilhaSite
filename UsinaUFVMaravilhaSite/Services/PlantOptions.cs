namespace UsinaUFVMaravilhaSite.Services;

public class PlantOptions
{
    public const string SectionName = "Plant";

    public bool Simulate { get; set; } = true;

    public string Host { get; set; } = "192.168.0.100";

    public int Port { get; set; } = 502;

    public byte UnitId { get; set; } = 1;

    public int PollingIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Alguns CLPs enviam os dois registradores de um REAL (32 bits) na ordem inversa
    /// (word baixa antes da word alta). Ajustar aqui caso os valores lidos venham sem sentido.
    /// </summary>
    public bool SwapRealWords { get; set; } = false;
}
