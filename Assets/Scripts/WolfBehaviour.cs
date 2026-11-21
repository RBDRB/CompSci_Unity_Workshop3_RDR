using UnityEngine;

public class WolfBehaviour : MonoBehaviour
{
    [Header("Deplacement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Chasse")]
    [SerializeField] private float visionRadius = 10f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float chaseTurnSpeed = 270f;

    [Header("Animation")]
    [SerializeField] private Animator animator;            // l'Animator du modele (enfant)

    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

    private Quaternion targetRotation;
    private float nextTurnTime;

    void Start()
    {
        PickRandomDirection();
        transform.rotation = targetRotation;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning(name + " : aucun Animator trouve dans les enfants");
    }

    void Update()
    {
        SheepBehaviour prey = FindNearestSheep();

        if (prey != null)
            Chase(prey);
        else
            Wander();

        UpdateAnimation(prey != null);
    }

    // ================= Perception =================
    // Exercice 1 : trouver le mouton le plus proche dans le rayon de vision
    private SheepBehaviour FindNearestSheep()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius);

        SheepBehaviour nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            SheepBehaviour sheep = hit.GetComponent<SheepBehaviour>();
            if (sheep == null)
                continue;                       // ce collider n'est pas un mouton (herbe, sol...)

            float d = FlatDistanceTo(sheep.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                nearest = sheep;
            }
        }
        return nearest;
    }

    private float FlatDistanceTo(Vector3 point)
    {
        Vector3 diff = point - transform.position;
        diff.y = 0f;                            // on ignore la hauteur
        return diff.magnitude;
    }

    // ================= Chasse =================
    // Exercice 2 : chasser le mouton le plus proche
    private void Chase(SheepBehaviour prey)
    {
        LookAt(prey.transform.position);
        RotateTowardsTarget(chaseTurnSpeed);
        MoveForward(chaseSpeed);
    }

    // ================= Animation =================
    private void UpdateAnimation(bool isChasing)
    {
        if (animator == null)
            return;

        animator.SetBool(IsChasingHash, isChasing);
    }

    // ================= Deplacement =================
    private void Wander()
    {
        if (Time.time >= nextTurnTime)
            PickRandomDirection();
        StayInField();
        RotateTowardsTarget(turnSpeed);
        MoveForward(speed);
    }

    private void PickRandomDirection()
    {
        targetRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
    }

    private void StayInField()
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) > fieldHalfSize || Mathf.Abs(p.z) > fieldHalfSize)
            LookAt(Vector3.zero);
    }

    private void LookAt(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            targetRotation = Quaternion.LookRotation(direction);
    }

    private void RotateTowardsTarget(float degreesPerSecond)
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    private void MoveForward(float currentSpeed)
    {
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    // Dessine le champ de vision dans la vue Scene quand le loup est selectionne
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visionRadius);
    }
}