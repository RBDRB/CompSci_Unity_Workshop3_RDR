using System.Collections.Generic;
using UnityEngine;

public class SheepBehaviour : MonoBehaviour
{
    private enum State { Wandering, SeekingFood, Eating, Fleeing, SeekingMate, Mating, Dead }

    [Header("Deplacement")]
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float seekTurnSpeed = 270f;     // tourne plus vite quand il vise quelque chose
    [SerializeField] private float minTurnDelay = 5f;
    [SerializeField] private float maxTurnDelay = 9f;
    [SerializeField] private float maxWanderAngle = 60f;     // ecart maximal du nouveau cap
    [SerializeField, Range(0f, 1f)] private float sharpTurnSpeedFactor = 0.2f; // vitesse gardee pendant un demi-tour
    [SerializeField] private float fieldHalfSize = 20f;

    [Header("Corps")]
    [SerializeField] private float bodyRadius = 0.5f;        // rayon adulte ; deux animaux restent a la somme de leurs rayons

    [Header("Troupeau (Boids)")]
    [SerializeField] private float herdRadius = 8f;          // distance a laquelle il voit les autres moutons
    [SerializeField] private float personalSpace = 1.5f;     // en dessous : il s'ecarte
    [SerializeField] private float cohesionWeight = 1.5f;    // envie de rejoindre le centre du troupeau
    [SerializeField] private float separationWeight = 2f;    // envie de ne pas se coller
    [SerializeField] private float alignmentWeight = 0.5f;   // envie d'aller dans le meme sens que les voisins

    [Header("Fuite")]
    [SerializeField] private float fleeStartRadius = 5f;     // un loup plus pres : panique
    [SerializeField] private float fleeSafeRadius = 12f;     // plus aucun loup dans ce rayon : il se calme
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float fleeTurnSpeed = 180f;
    [SerializeField] private float maxPanicAngle = 35f;      // ecart au hasard : le troupeau s'eparpille en eventail

    [Header("Energie")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyLossPerSecond = 2f;
    [SerializeField] private float hungerThreshold = 60f;    // en dessous : il a faim

    [Header("Nourriture")]
    [SerializeField] private float visionRadius = 6f;
    [SerializeField] private float eatDistance = 1f;
    [SerializeField] private float eatDuration = 2f;
    [SerializeField] private float maxSeekFoodDuration = 6f; // touffe pas atteinte : il abandonne

    [Header("Croissance")]
    [SerializeField] private float adultAge = 30f;           // secondes avant de pouvoir se reproduire
    [SerializeField, Range(0.1f, 1f)] private float newbornScale = 0.5f;

    [Header("Reproduction")]
    [SerializeField] private float mateEnergyThreshold = 70f; // assez nourri pour se reproduire
    [SerializeField] private float mateEnergyCost = 30f;      // energie perdue par chaque parent
    [SerializeField] private float mateCooldown = 20f;        // repos avant une nouvelle reproduction
    [SerializeField] private float mateDistance = 1.3f;       // doit rester > somme des rayons des corps
    [SerializeField] private float mateDuration = 3f;

    [Header("Mort et carcasse")]
    [SerializeField] private float meat = 100f;              // ce que les loups peuvent manger
    [SerializeField] private float corpseLifetime = 30f;     // une carcasse non finie disparait apres ce temps
    [SerializeField] private float fallDuration = 0.5f;      // duree du renversement
    [SerializeField] private float deadRoll = 180f;          // 180 = sur le dos, 90 = sur le flanc
    [SerializeField] private float deadHeightOffset = 0.8f;  // remonte le modele pour qu'il ne passe pas sous le sol

    [Header("Animation")]
    [SerializeField] private Animator animator;              // l'Animator du modele (enfant)

    [Header("Debug (lecture seule)")]
    [SerializeField] private State state = State.Wandering;
    [SerializeField] private float energy;
    [SerializeField] private float age;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    // ----- Lus par les autres animaux -----
    public bool IsDead => state == State.Dead;
    public bool HasMeat => state == State.Dead && meat > 0f;
    public float BodyRadius => bodyRadius * transform.localScale.x;   // un agneau est plus petit
    public bool IsReadyToMate => state == State.Wandering && IsAdult
                                 && energy >= mateEnergyThreshold && Time.time >= nextMateTime;
    public bool IsCourting(SheepBehaviour other) =>
        (state == State.SeekingMate || state == State.Mating) && mate == other;

    private bool IsAdult => age >= adultAge;

    // Remplis a chaque image par ScanSurroundings() (animaux vivants seulement)
    private readonly List<SheepBehaviour> neighbours = new List<SheepBehaviour>();
    private readonly List<WolfBehaviour> visibleWolves = new List<WolfBehaviour>();  // dans fleeSafeRadius
    private float nearestWolfDistance;

    private Simulation simulation;                           // pour faire naitre les agneaux
    private bool isNewborn;

    private float wanderHeading;                             // cap souhaite en flanant, en degres
    private float nextTurnTime;
    private Quaternion targetRotation;
    private Grass targetGrass;
    private float eatEndTime;
    private float seekFoodEndTime;
    private Grass lastFailedGrass;                           // touffe abandonnee : il ne la revise pas tout de suite
    private float panicAngle;                                // ecart de fuite, tire au debut de chaque fuite

    private SheepBehaviour mate;                             // partenaire choisi
    private bool isLeader;                                   // celui des deux qui fait naitre l'agneau
    private float mateEndTime;
    private float nextMateTime;

    private Transform model;                                 // le modele 3D, renverse a la mort
    private float deathTime;
    private Quaternion aliveModelRotation;
    private Vector3 aliveModelPosition;

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
        // Les adultes du depart ne se reproduisent pas tous a la meme seconde
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
        else
            model = animator.transform;                     // l'Animator est sur le modele
    }

    void Update()
    {
        if (state == State.Dead)
        {
            UpdateCorpse();
            return;
        }

        UpdateEnergy();
        if (energy <= 0f)
        {
            Die("mort de faim");
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
    private void ScanSurroundings()
    {
        neighbours.Clear();
        visibleWolves.Clear();
        nearestWolfDistance = float.MaxValue;

        float radius = Mathf.Max(herdRadius, fleeSafeRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            float d = FlatDistanceTo(hit.transform.position);

            SheepBehaviour sheep = hit.GetComponent<SheepBehaviour>();
            if (sheep != null)
            {
                if (sheep != this && !sheep.IsDead && d <= herdRadius)
                    neighbours.Add(sheep);
                continue;
            }

            WolfBehaviour wolf = hit.GetComponent<WolfBehaviour>();
            if (wolf != null && !wolf.IsDead && d <= fleeSafeRadius)
            {
                visibleWolves.Add(wolf);
                nearestWolfDistance = Mathf.Min(nearestWolfDistance, d);
            }
        }
    }

    private Grass FindNearestGrass()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius);

        Grass nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            Grass grass = hit.GetComponent<Grass>();
            if (grass == null || !grass.IsAvailableFor(this) || grass == lastFailedGrass)
                continue;                       // pas de l'herbe, deja visee par un autre, ou abandonnee

            float d = FlatDistanceTo(grass.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                nearest = grass;
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

    // ================= Transitions =================
    private void UpdateState()
    {
        // ----- Priorite absolue : la fuite, avec deux seuils (hysteresis) -----
        if (state == State.Fleeing)
        {
            if (visibleWolves.Count == 0)       // plus aucun loup dans fleeSafeRadius : il se calme
                StartWandering();
            return;                             // sinon il continue de fuir, meme entre 5 et 12 m
        }

        if (nearestWolfDistance <= fleeStartRadius)
        {
            StartFleeing();
            return;
        }

        // ----- Comportements normaux -----
        switch (state)
        {
            case State.Wandering:
                // Priorite : manger avant de se reproduire
                if (IsHungry())
                {
                    Grass grass = FindNearestGrass();
                    if (grass != null && grass.TryReserve(this))   // la touffe devient la sienne
                    {
                        targetGrass = grass;
                        lastFailedGrass = null;         // il ne boude qu'une seule touffe a la fois
                        state = State.SeekingFood;
                        seekFoodEndTime = Time.time + maxSeekFoodDuration;
                    }
                }
                else if (IsReadyToMate)
                {
                    TryFindMate();
                }
                break;

            case State.SeekingFood:
                if (targetGrass == null)
                {
                    StartWandering();
                }
                else if (Time.time >= seekFoodEndTime)  // bloque trop longtemps : abandon
                {
                    lastFailedGrass = targetGrass;
                    ReleaseGrass();
                    StartWandering();
                }
                else if (FlatDistanceTo(targetGrass.transform.position) <= eatDistance)
                {
                    state = State.Eating;
                    eatEndTime = Time.time + eatDuration;
                }
                break;

            case State.Eating:
                if (targetGrass == null)
                {
                    StartWandering();
                }
                else if (Time.time >= eatEndTime)
                {
                    GainEnergy(targetGrass.BeEaten());
                    targetGrass = null;
                    lastFailedGrass = null;
                    StartWandering();
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
                if (!PartnerStillCourting())    // partenaire mort ou parti
                {
                    StartWandering();
                }
                else if (isLeader && Time.time >= mateEndTime)
                {
                    Vector3 birthPlace = (transform.position + mate.transform.position) * 0.5f;
                    if (simulation != null)
                        simulation.SpawnLamb(birthPlace);

                    mate.FinishMating();
                    FinishMating();
                }
                break;
        }
    }

    private void StartWandering()
    {
        state = State.Wandering;
        mate = null;
        wanderHeading = transform.eulerAngles.y;    // repart dans la direction ou il regarde
        nextTurnTime = Time.time + Random.Range(minTurnDelay, maxTurnDelay);
    }

    private void StartFleeing()
    {
        state = State.Fleeing;
        ReleaseGrass();
        mate = null;                                // le partenaire verra que l'on ne le courtise plus
        panicAngle = Random.Range(-maxPanicAngle, maxPanicAngle);
    }

    // Libere la touffe visee pour que d'autres moutons puissent la manger
    private void ReleaseGrass()
    {
        if (targetGrass != null)
            targetGrass.Release(this);
        targetGrass = null;
    }

    // ================= Reproduction =================
    // Choisit le voisin pret le plus proche et lui fait une proposition
    private void TryFindMate()
    {
        SheepBehaviour best = null;
        float bestDistance = float.MaxValue;

        foreach (SheepBehaviour other in neighbours)
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

    // Appelee par un autre mouton. Renvoie true si la proposition est acceptee.
    public bool ProposeMate(SheepBehaviour suitor)
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

            case State.SeekingFood:
                SteerTowards(targetGrass.transform.position - transform.position, seekTurnSpeed);
                MoveForward(speed);
                break;

            case State.Fleeing:
                Flee();
                break;

            case State.SeekingMate:
                SteerTowards(mate.transform.position - transform.position, seekTurnSpeed);
                if (FlatDistanceTo(mate.transform.position) > mateDistance)
                    MoveForward(speed);
                break;

            case State.Eating:
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
        // Affame : le troupeau le retient moins, il s'ecarte pour chercher de l'herbe ailleurs
        float herdInfluence = IsHungry() ? 0.3f : 1f;
        SteerTowards(wanderDirection + HerdDirection() * herdInfluence, turnSpeed);
        MoveForward(speed);
    }

    // Les 3 regles des Boids : cohesion, separation, alignement
    private Vector3 HerdDirection()
    {
        if (neighbours.Count == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;

        foreach (SheepBehaviour other in neighbours)
        {
            center += other.transform.position;
            alignment += other.transform.forward;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f && d < personalSpace)
                separation += away / d * (1f - d / personalSpace);  // plus il est pres, plus on s'ecarte
        }

        center /= neighbours.Count;
        Vector3 toCenter = center - transform.position;
        toCenter.y = 0f;
        Vector3 cohesion = toCenter / herdRadius;   // 0 au centre, environ 1 au bord du troupeau

        alignment.y = 0f;
        alignment = alignment.normalized;

        return cohesion * cohesionWeight
             + separation * separationWeight
             + alignment * alignmentWeight;
    }

    // Court a l'oppose des loups (les plus proches comptent plus), devie de son angle de panique.
    // Les regles du troupeau sont ignorees pendant la fuite.
    private void Flee()
    {
        Vector3 away = Vector3.zero;

        foreach (WolfBehaviour wolf in visibleWolves)
        {
            Vector3 diff = transform.position - wolf.transform.position;
            diff.y = 0f;
            float d = Mathf.Max(diff.magnitude, 0.1f);
            away += diff / (d * d);
        }

        Vector3 fleeDirection = Quaternion.Euler(0f, panicAngle, 0f) * away;
        SteerTowards(fleeDirection, fleeTurnSpeed);
        MoveForward(fleeSpeed);
    }

    // ================= Collisions douces =================
    // Recule de la moitie du chevauchement ; l'autre animal fait l'autre moitie
    private void ResolveOverlaps()
    {
        foreach (SheepBehaviour other in neighbours)
            PushAwayFrom(other.transform.position, BodyRadius + other.BodyRadius);

        foreach (WolfBehaviour wolf in visibleWolves)
            PushAwayFrom(wolf.transform.position, BodyRadius + wolf.BodyRadius);
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

    // ================= Mort et carcasse =================
    // Publique : appelee par le loup qui l'attaque, ou par Update quand il meurt de faim
    public void Die(string cause)
    {
        if (state == State.Dead)
            return;

        Debug.Log(name + " : " + cause);
        state = State.Dead;
        ReleaseGrass();
        mate = null;
        deathTime = Time.time;

        if (animator != null)
            animator.enabled = false;           // fige la pose : plus de saut

        if (model != null)
        {
            aliveModelRotation = model.localRotation;
            aliveModelPosition = model.localPosition;
        }
    }

    // Renverse le modele, puis fait disparaitre la carcasse si personne ne la finit
    private void UpdateCorpse()
    {
        float elapsed = Time.time - deathTime;

        if (model != null)
        {
            float t = Mathf.Clamp01(elapsed / fallDuration);   // 0 -> 1 pendant la chute
            model.localRotation = aliveModelRotation * Quaternion.Euler(0f, 0f, deadRoll * t);
            model.localPosition = aliveModelPosition + Vector3.up * (deadHeightOffset * t);
        }

        if (elapsed >= corpseLifetime)
            Destroy(gameObject);
    }

    // Appelee par un loup a chaque bouchee. Renvoie la quantite vraiment mangee.
    public float TakeBite(float amount)
    {
        if (!HasMeat)
            return 0f;

        float eaten = Mathf.Min(amount, meat);
        meat -= eaten;

        if (meat <= 0f)
            Destroy(gameObject);                // carcasse finie : elle disparait

        return eaten;
    }

    // ================= Animation =================
    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        bool isMoving = state != State.Eating && state != State.Mating;
        animator.SetBool(IsMovingHash, isMoving);
    }

    // ================= Energie =================
    private bool IsHungry() => energy < hungerThreshold;

    private void UpdateEnergy()
    {
        energy -= energyLossPerSecond * Time.deltaTime;
    }

    public void GainEnergy(float amount)
    {
        energy = Mathf.Min(energy + amount, maxEnergy);
    }

    // ================= Mouvement de base =================
    private void SteerTowards(Vector3 desiredDirection, float degreesPerSecond)
    {
        desiredDirection = KeepInsideField(desiredDirection);
        if (desiredDirection.sqrMagnitude > 0.001f)
            targetRotation = Quaternion.LookRotation(desiredDirection);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    // Au bord du terrain : on retire seulement la partie de la direction qui sort du terrain.
    // Le mouton longe la cloture au lieu de faire demi-tour (vers les loups, s'il fuit).
    private Vector3 KeepInsideField(Vector3 desiredDirection)
    {
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude < 0.000001f)
            return desiredDirection;
        desiredDirection.Normalize();

        Vector3 p = transform.position;
        bool corrected = false;

        if (Mathf.Abs(p.x) > fieldHalfSize && desiredDirection.x * p.x > 0f)
        {
            desiredDirection.x = -Mathf.Sign(p.x) * 0.5f;     // petite poussee vers l'interieur
            corrected = true;
        }
        if (Mathf.Abs(p.z) > fieldHalfSize && desiredDirection.z * p.z > 0f)
        {
            desiredDirection.z = -Mathf.Sign(p.z) * 0.5f;
            corrected = true;
        }

        if (corrected)
            wanderHeading = Quaternion.LookRotation(desiredDirection).eulerAngles.y;

        return desiredDirection;
    }

    // Avance, en ralentissant quand il reste un grand angle a tourner
    private void MoveForward(float currentSpeed)
    {
        float angleLeft = Quaternion.Angle(transform.rotation, targetRotation);   // 0 a 180 degres
        float speedFactor = Mathf.Lerp(1f, sharpTurnSpeedFactor, angleLeft / 180f);
        transform.position += transform.forward * currentSpeed * speedFactor * Time.deltaTime;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, visionRadius);     // herbe
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, herdRadius);       // troupeau et partenaires
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeStartRadius);  // panique
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeSafeRadius);   // retour au calme
    }
}