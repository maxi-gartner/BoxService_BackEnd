using System;
using System.Collections.Generic;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class FacturasService
    {
        private readonly FacturasRepository     _repo     = new();
        private readonly PresupuestosRepository _repoPrep = new();

        public List<Factura> GetAll() => _repo.GetAll();

        public Factura? GetById(int id) => _repo.GetById(id);

        public (bool ok, string error, Factura? resultado) Create(string body)
        {
            try
            {
                var doc = JsonDocument.Parse(body).RootElement;

                if (!doc.TryGetProperty("id_service", out var sProp))
                    return (false, "id_service es obligatorio", null);

                var idService = sProp.GetInt32();

                if (_repo.ExisteFacturaParaService(idService))
                    return (false, "El service ya tiene una factura emitida", null);

                decimal total = 0;
                int? idPresupuesto = null;
                if (doc.TryGetProperty("id_presupuesto", out var pProp))
                {
                    idPresupuesto = pProp.GetInt32();
                    var detalles = _repoPrep.GetDetalles(idPresupuesto.Value);
                    foreach (var d in detalles) total += d.Subtotal;
                }

                var ultimo = _repo.GetUltimoNumero();
                var nro    = int.Parse(ultimo.Split('-')[1]) + 1;
                var numero = $"F-{nro:D4}";

                var factura = new Factura
                {
                    Numero        = numero,
                    Total         = total,
                    Estado        = "emitida",
                    IdService     = idService,
                    IdPresupuesto = idPresupuesto
                };

                var id = _repo.CrearConTransaccion(factura);
                factura.IdFactura = id;
                factura.Fecha     = DateTime.Today.ToString("yyyy-MM-dd");

                return (true, "", factura);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public (bool ok, string error) CambiarEstado(int id, string body)
        {
            var factura = _repo.GetById(id);
            if (factura == null)              return (false, "Factura no encontrada");
            if (factura.Estado == "anulada")  return (false, "No se puede modificar una factura anulada");

            var doc    = JsonDocument.Parse(body).RootElement;
            var estado = doc.GetProperty("estado").GetString() ?? "";

            var validos = new[] { "cobrada", "anulada" };
            if (!Array.Exists(validos, e => e == estado))
                return (false, "Estado inválido. Usar: cobrada | anulada");

            _repo.CambiarEstado(id, estado);
            return (true, "");
        }
    }
}
