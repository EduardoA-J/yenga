using UnityEngine;

/// <summary>
/// Ajustes de PhysX para piezas pequeñas de Jenga (1.5 cm de alto).
/// El contactOffset por defecto de Unity (1 cm) es casi tan grande como el bloque
/// y hace que la torre explote o tiemble al activar la física.
/// </summary>
public static class JengaPhysics
{
    public const float Gravity = 9.81f;
    public const float BlockMass = 0.07f;

    public static void ConfigureWorld()
    {
        Physics.defaultContactOffset = 0.00035f;
        Physics.bounceThreshold = 0.15f;
        Physics.sleepThreshold = 0.008f;
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 12;
        Physics.defaultMaxDepenetrationVelocity = 0.8f;
    }

    public static PhysicsMaterial CreateWoodMaterial()
    {
        return new PhysicsMaterial
        {
            name = "JengaWood",
            staticFriction = 0.78f,
            dynamicFriction = 0.52f,
            bounciness = 0.01f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
    }

    public static void ApplyTo(Rigidbody rb, BoxCollider box)
    {
        if (rb == null) return;

        rb.mass = BlockMass;
        rb.linearDamping = 0.06f;
        rb.angularDamping = 0.28f;
        rb.useGravity = false;
        rb.maxAngularVelocity = 8f;
        rb.maxDepenetrationVelocity = 0.6f;
        rb.solverIterations = 18;
        rb.solverVelocityIterations = 10;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.None;

        if (box != null)
        {
            // Hueco mínimo entre capas para que no se interpongan al despertar.
            box.size = new Vector3(0.992f, 0.975f, 0.992f);
            box.contactOffset = 0.0003f;
        }
    }
}
