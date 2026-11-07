-- 017_crear_detalle_factura.sql
-- Guarda los conceptos facturados, con o sin presupuesto previo.

CREATE TABLE IF NOT EXISTS public.detalle_factura (
    id_detalle SERIAL PRIMARY KEY,
    id_factura INT NOT NULL
        REFERENCES public.facturas(id_factura) ON DELETE CASCADE,
    tipo VARCHAR(20) NOT NULL
        CHECK (tipo IN ('labor', 'part')),
    descripcion VARCHAR(200) NOT NULL
        CHECK (length(trim(descripcion)) > 0),
    cantidad DECIMAL(10,2) NOT NULL
        CHECK (cantidad > 0),
    precio_unitario DECIMAL(10,2) NOT NULL
        CHECK (precio_unitario >= 0),
    subtotal DECIMAL(10,2) NOT NULL
        CHECK (subtotal >= 0)
);

CREATE INDEX IF NOT EXISTS idx_detalle_factura_factura
    ON public.detalle_factura(id_factura);

ALTER TABLE public.detalle_factura ENABLE ROW LEVEL SECURITY;
