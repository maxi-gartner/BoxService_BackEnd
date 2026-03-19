using System;
using System.Collections.Generic;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class PresupuestosService
    {
        private readonly PresupuestosRepository _repo = new();

        public List<Presupuesto> GetAll() => _repo.GetAll();

        public PresupuestoConDetalle? GetById(int id)
        {
            var presupuesto = _repo.GetById(id);
            if (presupuesto == null) return null;
            return new PresupuestoConDetalle
            {
                Presupuesto = presupuesto,
                Detalles    = _repo.GetDetalles(id)
            };
        }

        public (bool ok, string error, Presupuesto? resultado) Create(string body)
        {
            try
            {
                var doc = JsonDocument.Parse(body).RootElement;

                if (!doc.TryGetProperty("id_vehiculo", out var vProp))
                    return (false, "id_vehiculo es obligatorio", null);

                var detalles = new List<DetallePresupuesto>();
                if (doc.TryGetProperty("detalles", out var detallesProp))
                {
                    foreach (var item in detallesProp.EnumerateArray())
                    {
                        var cantidad = item.GetProperty("cantidad").GetDecimal();
                        var precio   = item.GetProperty("precio_unitario").GetDecimal();
                        detalles.Add(new DetallePresupuesto
                        {
                            Tipo           = item.GetProperty("tipo").GetString() ?? "",
                            Descripcion    = item.GetProperty("descripcion").GetString() ?? "",
                            Cantidad       = cantidad,
                            PrecioUnitario = precio,
                            Subtotal       = cantidad * precio
                        });
                    }
                }

                var ultimo = _repo.GetUltimoNumero();
                var nro    = int.Parse(ultimo.Split('-')[1]) + 1;
                var numero = $"P-{nro:D4}";

                var presupuesto = new Presupuesto
                {
                    Numero        = numero,
                    IdVehiculo    = vProp.GetInt32(),
                    Observaciones = doc.TryGetProperty("observaciones", out var obs) ? obs.GetString() : null
                };

                var id = _repo.Create(presupuesto);
                presupuesto.IdPresupuesto = id;

                using var conn = Database.DatabaseConnection.GetConnection();
                using var tx   = conn.BeginTransaction();
                foreach (var d in detalles)
                {
                    d.IdPresupuesto = id;
                    _repo.CreateDetalle(d, conn, tx);
                }
                tx.Commit();

                return (true, "", presupuesto);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public (bool ok, string error) CambiarEstado(int id, string body)
        {
            var presupuesto = _repo.GetById(id);
            if (presupuesto == null) return (false, "Presupuesto no encontrado");

            var doc    = JsonDocument.Parse(body).RootElement;
            var estado = doc.GetProperty("estado").GetString() ?? "";

            var validos = new[] { "enviado", "rechazado" };
            if (!Array.Exists(validos, e => e == estado))
                return (false, "Estado inválido. Usar: enviado | rechazado");

            _repo.CambiarEstado(id, estado);
            return (true, "");
        }

        public (bool ok, string error, object? resultado) Aprobar(int id)
        {
            var presupuesto = _repo.GetById(id);
            if (presupuesto == null)               return (false, "Presupuesto no encontrado", null);
            if (presupuesto.Estado == "aprobado")  return (false, "El presupuesto ya fue aprobado anteriormente", null);
            if (presupuesto.Estado == "rechazado") return (false, "No se puede aprobar un presupuesto rechazado", null);

            var detalles  = _repo.GetDetalles(id);
            var idService = _repo.AprobarConTransaccion(id, detalles, presupuesto.IdVehiculo);

            return (true, "", new
            {
                id_presupuesto      = id,
                estado              = "aprobado",
                id_service_generado = idService
            });
        }
    }
}
