using System.Collections.Generic;
using UnityEngine;

public class WolfBehaviour : MonoBehaviour
{
    [Header("Deplacement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float maxWanderAngle = 60f;     // ecart maximal du nouveau cap
    [SerializeField, Range(0f, 1f)] private float sharpTurnSpeedFactor = 0.2f; // vitesse gardee pendant un demi-tour
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Meute (Boids)")]
    [SerializeField] private float packRadius = 10f;         // distance a laquelle il voit les autres loups
    [SerializeField] private float personalSpace = 2f;       // en dessous : il s'ecarte
    [SerializeField] private float cohesionWeight = 1.5f;
    [SerializeField] private float separationWeight = 2f;
    [SerializeField] private float alignmentWeight = 0.5f;

    [Header("Chasse")]
    [SerializeField] private float visionRadius = 10f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float chaseTurnSpeed = 270f;
    [SerializeField] private float catchDistance = 1f;       // au contact : il s'arrete

    [Header("Animation")]
    [SerializeField] private Animator animator;              // l'Animator du modele (enfant)

    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

    // Remplis a chaque image par ScanSurroundings()
    private readonly List<WolfBehaviour> packMates = new List<WolfBehaviour>();
    private SheepBehaviour prey;

    private float wanderHeading;                             // cap souhaite en rodant, en degres
    private float nextTurnTime;
    private Quaternion targetRotation;

    void Start()
    {
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);  // cap de depart au hasard
        wanderHeading = transform.eulerAngles.y;
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning(name + " : aucun Animator trouve dans les enfants");
    }

    void Update()
    {
        ScanSurroundings();

        if (prey != null)
            Chase();
        else
            Wander();

        UpdateAnimation(prey != null);
    }

    // ================= Perception =================
    // Un seul balayage : les loups de la meute + le mouton le plus proche (exercice 1)
    private void ScanSurroundings()
    {
        packMates.Clear();
        prey = null;
        float bestDistance = float.MaxValue;

        float radius = Mathf.Max(packRadius, visionRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            float d = FlatDistanceTo(hit.transform.position);

            SheepBehaviour sheep = hit.GetComponent<SheepBehaviour>();
            if (sheep != null)
            {
                if (d <= visionRadius && d < bestDistance)
                {
                    bestDistance = d;
                    prey = sheep;
                }
                continue;
            }

            WolfBehaviour wolf = hit.GetComponent<WolfBehaviour>();
            if (wolf != null && wolf != this && d <= packRadius)
                packMates.Add(wolf);
        }
    }

    private float FlatDistanceTo(Vector3 point)
    {
        Vector3 diff = point - transform.position;
        diff.y = 0f;                            // on ignore la hauteur
        return diff.magnitude;
    }

    // ================= Chasse =================
    // Exercice 2 : chasser le mouton le plus proche
    private void Chase()
    {
        SteerTowards(prey.transform.position - transform.position, chaseTurnSpeed);

        if (FlatDistanceTo(prey.transform.position) > catchDistance)
            MoveForward(chaseSpeed);

        wanderHeading = transform.eulerAngles.y;    // apres la chasse, il repart tout droit
    }

    // ================= Errance en meute =================
    private void Wander()
    {
        if (Time.time >= nextTurnTime)
        {
            wanderHeading += Random.Range(-maxWanderAngle, maxWanderAngle);
            nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
        }

        Vector3 wanderDirection = Quaternion.Euler(0f, wanderHeading, 0f) * Vector3.forward;
        SteerTowards(wanderDirection + PackDirection(), turnSpeed);
        MoveForward(speed);
    }

    // Les 3 regles des Boids : cohesion, separation, alignement
    private Vector3 PackDirection()
    {
        if (packMates.Count == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;

        foreach (WolfBehaviour other in packMates)
        {
            center += other.transform.position;
            alignment += other.transform.forward;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f && d < personalSpace)
                separation += away / d * (1f - d / personalSpace);
        }

        center /= packMates.Count;
        Vector3 toCenter = center - transform.position;
        toCenter.y = 0f;
        Vector3 cohesion = toCenter / packRadius;

        alignment.y = 0f;
        alignment = alignment.normalized;

        return cohesion * cohesionWeight
             + separation * separationWeight
             + alignment * alignmentWeight;
    }

    // ================= Animation =================
    private void UpdateAnimation(bool isChasing)
    {
        if (animator == null)
            return;

        animator.SetBool(IsChasingHash, isChasing);
    }

    // ================= Mouvement de base =================
    private void SteerTowards(Vector3 desiredDirection, float degreesPerSecond)
    {
        desiredDirection = KeepInsideField(desiredDirection);
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude > 0.001f)
            targetRotation = Quaternion.LookRotation(desiredDirection);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    private Vector3 KeepInsideField(Vector3 desiredDirection)
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) <= fieldHalfSize && Mathf.Abs(p.z) <= fieldHalfSize)
            return desiredDirection;

        Vector3 toCenter = new Vector3(-p.x, 0f, -p.z);
        wanderHeading = Quaternion.LookRotation(toCenter).eulerAngles.y;
        return toCenter;
    }

    private void MoveForward(float currentSpeed)
    {
        float angleLeft = Quaternion.Angle(transform.rotation, targetRotation);   // 0 a 180 degres
        float speedFactor = Mathf.Lerp(1f, sharpTurnSpeedFactor, angleLeft / 180f);
        transform.position += transform.forward * currentSpeed * speedFactor * Time.deltaTime;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visionRadius);   // moutons
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, packRadius);     // meute
    }
}