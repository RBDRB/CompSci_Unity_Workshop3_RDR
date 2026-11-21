using UnityEngine;

public class WolfBehaviour : MonoBehaviour
{
    [Header("Deplacement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float fieldHalfSize = 20f;

    private Quaternion targetRotation;
    private float nextTurnTime;

    void Start()
    {
        PickRandomDirection();
        transform.rotation = targetRotation;
    }

    void Update()
    {
        Wander();
    }

    // ================= Deplacement =================
    private void Wander()
    {
        if (Time.time >= nextTurnTime)
            PickRandomDirection();
        StayInField();
        RotateTowardsTarget(turnSpeed);
        MoveForward();
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

    private void MoveForward()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}