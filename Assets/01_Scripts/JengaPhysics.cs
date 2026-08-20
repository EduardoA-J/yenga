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
        Physics.sleepThreshold = 0.012f;
        Physics.defaultSolverIterations = 18;
        Physics.defaultSolverVelocityIterations = 10;
        Physics.defaultMaxDepenetrationVelocity = 0.45f;
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
        rb.linearDamping = 0.14f;
        rb.angularDamping = 0.48f;
        rb.useGravity = false;
        rb.maxAngularVelocity = 4.5f;
        rb.maxDepenetrationVelocity = 0.35f;
        rb.solverIterations = 14;
        rb.solverVelocityIterations = 8;
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
