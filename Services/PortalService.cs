using System.Text.RegularExpressions;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    // Lado staff del portal del cliente: generar la invitación que un
    // miembro del taller le manda al dueño del auto por WhatsApp.
    public class PortalService(ClientRepository clients, PortalAccessRepository access, IConfiguration configuration)
    {
        public (bool ok, bool notFound, string error, PortalInviteResult? result) Invite(int clientId)
        {
            var client = clients.GetById(clientId);
            if (client is null) return (false, true, "Client not found", null);

            if (string.IsNullOrWhiteSpace(client.Phone))
                return (false, false, "El cliente no tiene teléfono cargado — hace falta para mandarle la invitación por WhatsApp.", null);

            var (token, expiresAt) = access.UpsertInvite(clientId);

            var appUrl = configuration["Portal:AppUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("No hay Portal:AppUrl configurada.");
            var inviteUrl = $"{appUrl}/portal/activar?token={token}";

            // wa.me solo acepta dígitos (con código de país) — se limpia
            // cualquier +, espacio, guion o paréntesis que haya quedado
            // tipeado al cargar el cliente.
            var digitsOnlyPhone = Regex.Replace(client.Phone, @"\D", "");
            var message = $"Hola {client.Name}! Ya podés ver el estado de tu vehículo en el taller desde acá: {inviteUrl}";
            var whatsappUrl = $"https://wa.me/{digitsOnlyPhone}?text={Uri.EscapeDataString(message)}";

            return (true, false, "", new PortalInviteResult(inviteUrl, whatsappUrl, expiresAt));
        }
    }

    public sealed record PortalInviteResult(string InviteUrl, string WhatsappUrl, DateTime ExpiresAt);
}
