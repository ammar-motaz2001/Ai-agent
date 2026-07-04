using System.Collections.Generic;

namespace Civil3DAIAgent.Models.Geometry
{
    /// <summary>
    /// A database-neutral snapshot of a polyline's geometry (world coordinates + per-vertex bulge for
    /// arc segments). Used to transfer the extracted road centreline from the source drawing to the
    /// new drawing without holding a live cross-database object, guaranteeing the coordinates are
    /// reproduced exactly.
    /// </summary>
    public sealed class PolylineData
    {
        /// <summary>Ordered vertices that define the polyline.</summary>
        public List<PolylineVertex> Vertices { get; } = new List<PolylineVertex>();

        /// <summary>Constant elevation (Z) of the 2D polyline, in drawing units.</summary>
        public double Elevation { get; set; }

        /// <summary>True when the polyline is closed.</summary>
        public bool Closed { get; set; }

        /// <summary>Total 2D length of the polyline, in drawing units (metres). Informational.</summary>
        public double Length { get; set; }

        /// <summary>Convenience: number of vertices.</summary>
        public int VertexCount => Vertices.Count;
    }

    /// <summary>A single polyline vertex: planar position plus the bulge to the next vertex.</summary>
    public sealed class PolylineVertex
    {
        /// <summary>Creates a vertex.</summary>
        /// <param name="x">Easting / X.</param>
        /// <param name="y">Northing / Y.</param>
        /// <param name="bulge">
        /// Bulge to the following vertex (tan of one quarter of the arc's included angle); 0 = straight.
        /// </param>
        public PolylineVertex(double x, double y, double bulge)
        {
            X = x;
            Y = y;
            Bulge = bulge;
        }

        /// <summary>Easting / X coordinate.</summary>
        public double X { get; }

        /// <summary>Northing / Y coordinate.</summary>
        public double Y { get; }

        /// <summary>Bulge to the next vertex (0 for a straight segment).</summary>
        public double Bulge { get; }
    }
}
