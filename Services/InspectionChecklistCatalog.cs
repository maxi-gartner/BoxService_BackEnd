namespace BoxService_BackEnd.Services;

public static class InspectionChecklistCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Items =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["front_bumper"] = "Paragolpes delantero",
            ["rear_bumper"] = "Paragolpes trasero",
            ["doors"] = "Puertas",
            ["hood"] = "Capó",
            ["trunk"] = "Baúl",
            ["windshield_windows"] = "Parabrisas y vidrios",
            ["mirrors"] = "Espejos",
            ["lights"] = "Luces",
            ["tires"] = "Neumáticos",
            ["interior_upholstery"] = "Interior y tapizados",
            ["dashboard"] = "Tablero",
            ["spare_tire_tools"] = "Rueda de auxilio y herramientas",
            ["personal_items"] = "Objetos personales"
        };
}
