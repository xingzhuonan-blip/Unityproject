using UnityEngine;
using UnityEngine.AI;

public class VRGuideController : MonoBehaviour
{
    [Header("Components")]
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Global components")]
    public Transform playerHead;  
    public Transform targetVisual; 

    [Header("Parameters")]
    public float waitDistance = 3.0f;
    public float continueDistance = 2.0f;
    public float wanderRadius = 10.0f;
    public float rotationSpeed = 5.0f;

    private bool isGuideMode = false;
    private float moveStartTime = -99f;
    public bool isEating = false;

    void Update()
    {
        // safety check
        if (agent == null || !agent.gameObject.activeInHierarchy) return;

        // 1. Data gathering: always collect regardless of mode so animator receives parameters
        float speed = agent.velocity.magnitude;
        float distToTarget = (agent.hasPath && !agent.pathPending) ? agent.remainingDistance : 9999f;
        float distToPlayer = (playerHead != null) ? Vector3.Distance(agent.transform.position, playerHead.position) : 0f;

        // 2. Pass parameters to Animator: do this before intercepts so "walking" and "Arrive" conditions always receive data
        if (animator != null)
        {
            animator.SetFloat("Speed", speed);
            animator.SetFloat("DistToTarget", distToTarget);
            // If eating cake, force IsGuideMode=false to prevent Arrive from triggering while eating
            animator.SetBool("IsGuideMode", isEating ? false : isGuideMode);
            // Sync IsEating to the Animator so transitions that check it behave correctly
            animator.SetBool("IsEating", isEating);
        }

        // 3. Intercept logic: if currently eating, skip the following guide and arrival cleanup logic
        if (isEating) return;

        // 4. Core guidance logic
        if (isGuideMode && agent.hasPath)
        {
            if (distToTarget > agent.stoppingDistance + 0.1f)
            {
                // start protection
                bool isStarting = Time.time < moveStartTime + 0.5f;
                if (distToTarget < 1.0f) isStarting = false;

                // stop logic: player too far
                if (distToPlayer > waitDistance && !isStarting)
                {
                    agent.isStopped = true;
                    RotateTowardsUser();
                }
                // walking logic: player is near
                else if (distToPlayer < continueDistance || isStarting)
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;
                }
            }
        }

        // 5. Arrival cleanup
        if (agent.hasPath && distToTarget <= agent.stoppingDistance + 0.1f)
        {
            agent.ResetPath();
            isGuideMode = false;
            if (targetVisual != null) targetVisual.gameObject.SetActive(false);
        }
    }

    void RotateTowardsUser()
    {
        if (playerHead == null || agent == null) return;
        agent.updateRotation = false;
        Vector3 direction = playerHead.position - agent.transform.position;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
        }
    }

    public void SetRandomDestination()
    {
        // Step 1: Ensure the Agent is alive
        if (agent == null || !agent.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("⚠️ Current Agent invalid; searching for an active Agent...");
            var allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var a in allAgents)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    agent = a; // found a live Racer
                    break;
                }
            }
        }

        if (agent == null)
        {
            Debug.LogError("❌ No active Agent found in the scene!");
            return;
        }

        // Step 2: Force-refresh Animator 

        animator = agent.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            Debug.Log($"✅ Animator connected: {animator.name} (belongs to {agent.name})");
        }
        else
        {
            Debug.LogError($"❌ Critical warning: No Animator component found on {agent.name} or its children!");
        }

        // Step 3: 🧹 Cleanup the scene 
        var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (var a in agents)
        {
            // If this Agent is not the one we're controlling, force-hide it
            if (a != agent && a.gameObject.activeInHierarchy)
            {
                a.gameObject.SetActive(false);
                Debug.Log($"🛑 Forced deactivated extra Avatar: {a.name}");
            }
        }
        // Step 4: Position safety fix
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
        // Step 5: Start moving
        isGuideMode = true;
        agent.isStopped = false;
        agent.updateRotation = true;
        moveStartTime = Time.time;

        Vector3 randomPos = Random.insideUnitSphere * wanderRadius;
        randomPos += agent.transform.position;
        NavMeshHit destinationHit;

        if (NavMesh.SamplePosition(randomPos, out destinationHit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(destinationHit.position);
            if (targetVisual != null)
            {
                targetVisual.position = destinationHit.position + Vector3.up * 0.05f;
                targetVisual.gameObject.SetActive(true);
            }
        }
    }
}