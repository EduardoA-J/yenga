using UnityEngine;

/// <summary>
/// Ajustes de PhysX para piezas pequeñas de Jenga (1.5 cm de alto).
/// El contactOffset por defecto de Unity (1 cm) es casi tan grande como el bloque
/// y hace que la torre explote o tiemble al activar la física.
/// </summary>
public static class JengaPhysics
{
    /// <summary>
    /// Gravedad reducida respecto a la real: a esta escala (bloques de 1.5 cm)
    /// 9.81 genera impactos que tumban la torre con cualquier roce.
    /// </summary>
    public const float Gravity = 4.2f;
    public const float BlockMass = 0.12f;

    public static void ConfigureWorld()
    {
        Physics.defaultContactOffset = 0.0006f;
        // Muy alto: evita microrrebotes entre piezas apiladas.
        Physics.bounceThreshold = 5f;
        Physics.sleepThreshold = 0.02f;
        Physics.defaultSolverIterations = 30;
        Physics.defaultSolverVelocityIterations = 16;
        Physics.defaultMaxDepenetrationVelocity = 0.08f;
    }

    public static PhysicsMaterial CreateWoodMaterial()
    {
        return new PhysicsMaterial
        {
            name = "JengaWood",
            staticFriction = 0.95f,
            dynamicFriction = 0.8f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
    }

    public static void ApplyTo(Rigidbody rb, BoxCollider box)
    {
        if (rb == null) return;

        rb.mass = BlockMass;
        rb.linearDamping = 0.45f;
        rb.angularDamping = 1.6f;
        rb.useGravity = false;
        rb.maxAngularVelocity = 2.5f;
        rb.maxDepenetrationVelocity = 0.08f;
        rb.solverIterations = 30;
        rb.solverVelocityIterations = 16;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.sleepThreshold = 0.02f;

        if (box != null)
        {
            // Sin hueco vertical: cualquier separación entre capas hace que la
            // torre "caiga" unos milímetros al activar la física y se desmorone.
            box.size = new Vector3(0.998f, 1f, 0.998f);
            box.contactOffset = 0.0006f;
        }
    }
}
