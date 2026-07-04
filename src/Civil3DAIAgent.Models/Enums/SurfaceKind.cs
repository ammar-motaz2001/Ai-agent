namespace Civil3DAIAgent.Models.Enums
{
    /// <summary>
    /// Identifies which logical surface is being referenced when the automation creates or
    /// queries Civil 3D TIN surfaces.
    /// </summary>
    public enum SurfaceKind
    {
        /// <summary>Existing ground (natural terrain), built from contours and/or survey points.</summary>
        ExistingGround = 0,

        /// <summary>Corridor top surface (finished grade / top of pavement + daylight).</summary>
        CorridorTop = 1,

        /// <summary>Corridor datum surface (bottom of construction / subgrade).</summary>
        CorridorDatum = 2
    }
}
