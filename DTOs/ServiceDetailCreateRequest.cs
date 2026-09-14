namespace BoxService_BackEnd.DTOs;

public sealed class ServiceDetailCreateRequest
{
    public string Description { get; set; } = "";
    public bool Done { get; set; } = true;
}
