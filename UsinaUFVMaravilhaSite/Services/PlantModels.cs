namespace UsinaUFVMaravilhaSite.Services;

public enum BreakerStatus
{
    Indeterminado,
    Aberto,
    Fechado
}

public class ProtectionFlags
{
    public bool TripT1 { get; set; }
    public bool Bloqueio86T2 { get; set; }
    public bool Sobretensao591 { get; set; }
    public bool SobretensaoNeutro59N1 { get; set; }
    public bool TrafoAlarme { get; set; }
    public bool TrafoTrip { get; set; }
    public bool TrafoFalha { get; set; }

    public bool AlgumaAtuada =>
        TripT1 || Bloqueio86T2 || Sobretensao591 || SobretensaoNeutro59N1 ||
        TrafoAlarme || TrafoTrip || TrafoFalha;
}

public class UfvRelayData
{
    public ushort AliveIndicator { get; set; }
    public float VAB { get; set; }
    public float VBC { get; set; }
    public float VCA { get; set; }
}

public class UfvGenerationData
{
    public float ActivePowerKw { get; set; }
    public float ReactivePowerKvar { get; set; }
    public float VAB { get; set; }
    public float VBC { get; set; }
    public float VCA { get; set; }
    public float IA { get; set; }
    public float IB { get; set; }
    public float IC { get; set; }
    public float TotalYieldMwh { get; set; }
    public float DailyYieldMwh { get; set; }
    public float PowerFactor { get; set; }
}

public class UfvState
{
    public required string Nome { get; set; }
    public BreakerStatus Disjuntor { get; set; } = BreakerStatus.Indeterminado;
    public bool ComunicacaoRele { get; set; }
    public ProtectionFlags Protecoes { get; set; } = new();
    public UfvRelayData Rele { get; set; } = new();
    public UfvGenerationData Geracao { get; set; } = new();
}

public class PlantState
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Conectado { get; set; }
    public string? UltimoErro { get; set; }

    public UfvState Ufv1 { get; set; } = new() { Nome = "UFV-01" };
    public UfvState Ufv2 { get; set; } = new() { Nome = "UFV-02" };

    public float PotenciaAtivaTotalKw => Ufv1.Geracao.ActivePowerKw + Ufv2.Geracao.ActivePowerKw;
    public float TotalDiarioMwh => Ufv1.Geracao.DailyYieldMwh + Ufv2.Geracao.DailyYieldMwh;
    public float TotalGeralMwh => Ufv1.Geracao.TotalYieldMwh + Ufv2.Geracao.TotalYieldMwh;
}

public enum ResetTarget
{
    Geral,
    Ufv1,
    Ufv2
}

public enum BreakerCommand
{
    Ufv1Abrir,
    Ufv1Fechar,
    Ufv2Abrir,
    Ufv2Fechar
}
