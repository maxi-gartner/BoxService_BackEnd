using System;

public class Vehicle
{
    public int VehiculoId { get; set; }
    public int ClienteId { get; set; }
    public string Marca { get; set; } = null!;
    public string Modelo { get; set; } = null!;
    public int? Ano { get; set; }
    public string Placa { get; set; } = null!;
    public DateTime CreadoEn { get; set; }
}
