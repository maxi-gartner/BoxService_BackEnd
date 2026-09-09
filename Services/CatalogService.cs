using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class CatalogService
    {
        private readonly CatalogRepository _repo = new();

        public List<CatalogItem> GetAll() => _repo.GetAll();

        public CatalogItem Create(CatalogItemCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                throw new ArgumentException("name is required.");

            if (req.Price < 0)
                throw new ArgumentException("price cannot be negative.");

            var type = string.IsNullOrWhiteSpace(req.Type) ? "labor" : req.Type;
            if (type != "labor" && type != "part")
                throw new ArgumentException("type must be 'labor' or 'part'.");

            return _repo.Create(new CatalogItem
            {
                Name = req.Name.Trim(),
                Type = type,
                Price = req.Price
            });
        }

        public (bool ok, bool notFound, string error) Update(int id, CatalogItemUpdateRequest req)
        {
            if (req.Name is not null && string.IsNullOrWhiteSpace(req.Name))
                return (false, false, "name cannot be empty.");

            if (req.Price is < 0)
                return (false, false, "price cannot be negative.");

            if (req.Type is not null && req.Type != "labor" && req.Type != "part")
                return (false, false, "type must be 'labor' or 'part'.");

            var updated = _repo.Update(id, req);
            return updated ? (true, false, "") : (false, true, "Catalog item not found");
        }

        public bool Delete(int id) => _repo.Delete(id);
    }
}
