using System.Collections.Generic;
using UnityEngine;

public class WolfBehaviour : MonoBehaviour
{
    private enum State { Wandering, Chasing, Attacking, Eating, SeekingMate, Mating, Dead }

    [Header("Deplacement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float maxWanderAngle = 60f;     // ecart maximal du nouveau cap
    [SerializeField, Range(0f, 1f)] private float sharpTurnSpeedFactor = 0.2f; // vitesse gardee pendant un demi-tour
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Corps")]
    [SerializeField] private float bodyRadius = 0.6f;        // rayon adulte

    [Header("Meute (Boids)")]
    [SerializeField] private float packRadius = 10f;         // distance a laquelle il voit les autres loups
    [SerializeField] private float personalSpace = 2f;       // en dessous : il s'ecarte
    [SerializeField] private float cohesionWeight = 1.5f;
    [SerializeField] private float separationWeight = 2f;
    [SerializeField] private float alignmentWeight = 0.5f;

    [Header("Chasse")]
    [SerializeField] private float visionRadius = 7f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float chaseTurnSpeed = 270f;
    [SerializeField] private float catchDistance = 1.3f;     // doit rester > somme des rayons des corps

    [Header("Energie")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyLossPerSecond = 1.5f;
    [SerializeField] private float hungerThreshold = 70f;    // en dessous : il chasse

    [Header("Repas")]
    [SerializeField] private float biteSize = 20f;           // viande arrachee a chaque cycle de l'animation Eating

    [Header("Croissance")]
    [SerializeField] private float adultAge = 40f;           // secondes avant de pouvoir se reproduire
    [SerializeField, Range(0.1f, 1f)] private float newbornScale = 0.5f;

    [Header("Reproduction")]
    [SerializeField] private float mateEnergyThreshold = 80f; // assez nourri pour se reproduire
    [SerializeField] private float mateEnergyCost = 30f;      // energie perdue par chaque parent
    [SerializeField] private float mateCooldown = 40f;        // repos avant une nouvelle reproduction
    [SerializeField] private float mateDistance = 1.5f;       // doit rester > somme des rayons des corps
    [SerializeField] private float mateDuration = 3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;              // l'Animator du modele (enfant)

    [Header("Debug (lecture seule)")]
    [SerializeField] private State state = State.Wandering;
    [SerializeField] private float energy;
    [SerializeField] private float age;

    // Noms des parametres de WolfAnimator
    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");
    private static readonly int IsEatingHash = Animator.StringToHash("IsEating");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DieHash = Animator.StringToHash("Die");

    // ----- Lus par les autres animaux -----
    public bool IsDead => state == State.Dead;
    public float BodyRadius => bodyRadius * transform.localScale.x;   // un louveteau est plus petit
    public bool IsReadyToMate => state == State.Wandering && IsAdult
                                 && energy >= mateEnergyThreshold && Time.time >= nextMateTime;
    public bool IsCourting(WolfBehaviour other) =>
        (state == State.SeekingMate || state == State.Mating) && mate == other;

    private bool IsAdult => age >= adultAge;

    // Remplis a chaque image par ScanSurroundings()
    private readonly List<WolfBehaviour> packMates = new List<WolfBehaviour>();
    private readonly List<SheepBehaviour> nearbyLiveSheep = new List<SheepBehaviour>();
    private SheepBehaviour nearestLiveSheep;
    private SheepBehaviour nearestCarcass;

    private Simulation simulation;                           // pour faire naitre les louveteaux
    private bool isNewborn;

    private SheepBehaviour target;                           // le mouton chasse ou la carcasse mangee
    private float attackDuration;                            // durees lues dans les clips d'animation
    private float biteDuration;
    private float deathDuration;
    private float attackEndTime;
    private float nextBiteTime;

    private WolfBehaviour mate;                              // partenaire choisi
    private bool isLeader;                                   // celui des deux qui fait naitre le louveteau
    private float mateEndTime;
    private float nextMateTime;

    private float wanderHeading;                             // cap souhaite en rodant, en degres
    private float nextTurnTime;
    private Quaternion targetRotation;

    // Appelee par Simulation juste apres Instantiate, avant Start()
    public void BeBorn()
    {
        isNewborn = true;
    }

    void Start()
    {
        energy = maxEnergy;
        age = isNewborn ? 0f : adultAge;
        UpdateGrowth();
        nextMateTime = Time.time + (isNewborn ? 0f : Random.Range(0f, mateCooldown));

        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);  // cap de depart au hasard
        StartWandering();

        simulation = GetComponentInParent<Simulation>();
        if (simulation == null)
            Debug.LogWarning(name + " : pas de Simulation parente, pas de reproduction possible");

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning(name + " : aucun Animator trouve dans les enfants");

        attackDuration = ClipLength("Attack", 1.3f);
        biteDuration = ClipLength("Eating", 2.5f);   // une bouchee = un cycle de l'animation
        deathDuration = ClipLength("Death", 1f);
    }

    void Update()
    {
        if (state == State.Dead)
            return;

        UpdateEnergy();
        if (energy <= 0f)
        {
            Die();
            return;
        }

        UpdateGrowth();
        ScanSurroundings(); // 1. percevoir
        UpdateState();      // 2. decider
        Act();              // 3. agir
        ResolveOverlaps();  // 4. ne pas traverser les autres
        UpdateAnimation();  // 5. montrer
    }

    // ================= Perception =================
    // Un seul balayage : la meute, les moutons vivants (le plus proche = exercice 1) et les carcasses
    private void ScanSurroundings()
    {
        packMates.Clear();
        nearbyLiveSheep.Clear();
        nearestLiveSheep = null;
        nearestCarcass = null;
        float bestLive = float.MaxValue;
        float bestCarcass = float.MaxValue;

        float radius = Mathf.Max(packRadius, visionRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            float d = FlatDistanceTo(hit.transform.position);

            SheepBehaviour sheep = hit.GetComponent<SheepBehaviour>();
            if (sheep != null)
            {
                if (sheep.IsDead)
                {
                    if (sheep.HasMeat && d <= visionRadius && d < bestCarcass)
                    {
                        bestCarcass = d;
                        nearestCarcass = sheep;
                    }
                }
                else
                {
                    nearbyLiveSheep.Add(sheep);
                    if (d <= visionRadius && d < bestLive)
                    {
                        bestLive = d;
                        nearestLiveSheep = sheep;
                    }
                }
                continue;
            }

            WolfBehaviour wolf = hit.GetComponent<WolfBehaviour>();
            if (wolf != null && wolf != this && !wolf.IsDead && d <= packRadius)
                packMates.Add(wolf);
        }
    }

    private float FlatDistanceTo(Vector3 point)
    {
        Vector3 diff = point - transform.position;
        diff.y = 0f;                            // on ignore la hauteur
        return diff.magnitude;
    }

    // ================= Transitions (machine a etats des slides) =================
    private void UpdateState()
    {
        switch (state)
        {
            case State.Wandering:
                // Priorite : manger avant de se reproduire ; une carcasse passe avant un mouton vivant
                if (IsHungry())
                {
                    target = nearestCarcass != null ? nearestCarcass : nearestLiveSheep;
                    if (target != null)
                        state = State.Chasing;
                }
                else if (IsReadyToMate)
                {
                    TryFindMate();
                }
                break;

            case State.Chasing:
                // Cible disparue ou partie trop loin -> abandon
                if (target == null || FlatDistanceTo(target.transform.position) > visionRadius * 1.5f)
                {
                    StartWandering();
                }
                else if (FlatDistanceTo(target.transform.position) <= catchDistance)
                {
                    if (target.IsDead)
                    {
                        StartEating();                  // deja mort : on mange directement
                    }
                    else
                    {
                        target.Die("tue par un loup");  // exercice 2 : la capture
                        state = State.Attacking;
                        attackEndTime = Time.time + attackDuration;
                        if (animator != null)
                            animator.SetTrigger(AttackHash);
                    }
                }
                break;

            case State.Attacking:
                if (Time.time >= attackEndTime)
                    StartEating();
                break;

            case State.Eating:
                if (target == null)                     // un autre loup a fini la carcasse
                {
                    StartWandering();
                    break;
                }

                if (Time.time >= nextBiteTime)          // un cycle d'animation Eating = une bouchee
                {
                    GainEnergy(target.TakeBite(biteSize));
                    nextBiteTime += biteDuration;

                    if (!target.HasMeat || energy >= maxEnergy)
                        StartWandering();               // carcasse vide ou loup rassasie
                }
                break;

            case State.SeekingMate:
                if (!PartnerStillCourting())
                {
                    StartWandering();
                }
                else if (FlatDistanceTo(mate.transform.position) <= mateDistance)
                {
                    state = State.Mating;
                    mateEndTime = Time.time + mateDuration;
                }
                break;

            case State.Mating:
                if (!PartnerStillCourting())            // partenaire mort ou parti
                {
                    StartWandering();
                }
                else if (isLeader && Time.time >= mateEndTime)
                {
                    Vector3 birthPlace = (transform.position + mate.transform.position) * 0.5f;
                    if (simulation != null)
                        simulation.SpawnCub(birthPlace);

                    mate.FinishMating();
                    FinishMating();
                }
                break;
        }
    }

    private void StartWandering()
    {
        state = State.Wandering;
        target = null;
        mate = null;
        wanderHeading = transform.eulerAngles.y;    // repart dans la direction ou il regarde
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
    }

    private void StartEating()
    {
        state = State.Eating;
        nextBiteTime = Time.time + biteDuration;
    }

    // ================= Reproduction =================
    // Choisit le loup pret le plus proche dans la meute et lui fait une proposition
    private void TryFindMate()
    {
        WolfBehaviour best = null;
        float bestDistance = float.MaxValue;

        foreach (WolfBehaviour other in packMates)
        {
            if (!other.IsReadyToMate)
                continue;

            float d = FlatDistanceTo(other.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = other;
            }
        }

        if (best != null && best.ProposeMate(this))
        {
            mate = best;
            isLeader = true;                    // celui qui propose fait naitre le petit
            state = State.SeekingMate;
        }
    }

    // Appelee par un autre loup. Renvoie true si la proposition est acceptee.
    public bool ProposeMate(WolfBehaviour suitor)
    {
        if (!IsReadyToMate)
            return false;

        mate = suitor;
        isLeader = false;                       // celui qui accepte attend le meneur
        state = State.SeekingMate;
        return true;
    }

    // Appelee sur les deux parents a la naissance
    public void FinishMating()
    {
        energy -= mateEnergyCost;
        nextMateTime = Time.time + mateCooldown;
        StartWandering();
    }

    private bool PartnerStillCourting()
    {
        return mate != null && !mate.IsDead && mate.IsCourting(this);
    }

    private void UpdateGrowth()
    {
        if (age >= adultAge)
        {
            transform.localScale = Vector3.one;
            return;
        }

        age += Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(newbornScale, 1f, age / adultAge);
    }

    // ================= Actions =================
    private void Act()
    {
        switch (state)
        {
            case State.Wandering:
                Wander();
                break;

            case State.Chasing:
                SteerTowards(target.transform.position - transform.position, chaseTurnSpeed);
                if (FlatDistanceTo(target.transform.position) > catchDistance)
                    MoveForward(chaseSpeed);
                break;

            case State.Attacking:
            case State.Eating:
                if (target != null)                     // reste face a sa proie, sans avancer
                    SteerTowards(target.transform.position - transform.position, chaseTurnSpeed);
                break;

            case State.SeekingMate:
                SteerTowards(mate.transform.position - transform.position, chaseTurnSpeed);
                if (FlatDistanceTo(mate.transform.position) > mateDistance)
                    MoveForward(speed);
                break;

            case State.Mating:
                // immobile
                break;
        }
    }

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

    // ================= Collisions douces =================
    private void ResolveOverlaps()
    {
        foreach (WolfBehaviour other in packMates)
            PushAwayFrom(other.transform.position, BodyRadius + other.BodyRadius);

        foreach (SheepBehaviour sheep in nearbyLiveSheep)
            PushAwayFrom(sheep.transform.position, BodyRadius + sheep.BodyRadius);
    }

    private void PushAwayFrom(Vector3 otherPosition, float minDistance)
    {
        Vector3 away = transform.position - otherPosition;
        away.y = 0f;
        float d = away.magnitude;
        if (d >= minDistance || d < 0.0001f)
            return;

        transform.position += away / d * ((minDistance - d) * 0.5f);
    }

    // ================= Energie et mort =================
    private bool IsHungry() => energy < hungerThreshold;

    private void UpdateEnergy()
    {
        energy -= energyLossPerSecond * Time.deltaTime;
    }

    private void GainEnergy(float amount)
    {
        energy = Mathf.Min(energy + amount, maxEnergy);
    }

    private void Die()
    {
        Debug.Log(name + " est mort de faim");
        state = State.Dead;
        target = null;
        mate = null;

        if (animator != null)
        {
            animator.SetBool(IsChasingHash, false);
            animator.SetBool(IsEatingHash, false);
            animator.SetTrigger(DieHash);
        }

        Destroy(gameObject, deathDuration + 2f);    // laisse le temps de voir l'animation
    }

    // ================= Animation =================
    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(IsChasingHash, state == State.Chasing);
        animator.SetBool(IsEatingHash, state == State.Attacking || state == State.Eating);
    }

    // Duree d'un clip de WolfAnimator, pour caler le code sur l'animation
    private float ClipLength(string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallback;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        Debug.LogWarning(name + " : clip '" + clipName + "' absent de WolfAnimator, duree par defaut utilisee");
        return fallback;
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
        Gizmos.DrawWireSphere(transform.position, packRadius);     // meute et partenaires
    }
}