using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CakePathFollower : MonoBehaviour
{
    public NavMeshAgent agent;
    public GameObject cakePrefab;
    public string eatAnimationTag = "Eat";
    public Animator animator;
    public float cakeInterval = 2f;

    [Header("Stop Distance")]
    public float stopDistance = 1.2f;

    private List<GameObject> spawnedCakes = new List<GameObject>();

    public void MoveAndSpawnCakes(Vector3 targetPosition)
    {
        StopAllCoroutines();

        // Reset eating state when interrupted — prevents isEating from getting stuck
        VRGuideController gc = FindFirstObjectByType<VRGuideController>();
        if (gc != null)
        {
            gc.isEating = false;
            if (gc.animator != null) gc.animator.SetBool("IsEating", false);
        }

        ClearCakes();

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(targetPosition, path))
        {
            SpawnCakesAlongPath(path.corners);
            StartCoroutine(FollowPathAndEat());
        }
    }

    void SpawnCakesAlongPath(Vector3[] corners)
    {
        if (corners.Length < 2) return;

        // 1. generate cake along the path
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 start = corners[i];
            Vector3 end = corners[i + 1];
            float segmentLength = Vector3.Distance(start, end);

            for (float d = 0; d < segmentLength; d += cakeInterval)
            {
                Vector3 spawnPos = Vector3.Lerp(start, end, d / segmentLength);
                spawnPos.y = 0.05f;
                GameObject cake = Instantiate(cakePrefab, spawnPos, Quaternion.identity);
                spawnedCakes.Add(cake);
            }
        }

        // add the final cake at the end position
        Vector3 finalPos = corners[corners.Length - 1];
        finalPos.y = 0.5f;
        GameObject finalCake = Instantiate(cakePrefab, finalPos, Quaternion.identity);
        spawnedCakes.Add(finalCake);
    }

    IEnumerator FollowPathAndEat()
    {
        VRGuideController guideController = FindFirstObjectByType<VRGuideController>();
        if (guideController != null) guideController.isEating = true;

        // Use the animator from the guide controller if available, otherwise fallback to local
        Animator currentAnimator = (guideController != null && guideController.animator != null) 
            ? guideController.animator 
            : animator;

        // Set IsEating bool on the Animator so transitions that check it behave correctly
        if (currentAnimator != null) currentAnimator.SetBool("IsEating", true);

        float originalStoppingDist = agent.stoppingDistance;

        // loop through each cake
        for (int i = 0; i < spawnedCakes.Count; i++)
        {
            GameObject currentCake = spawnedCakes[i];
            if (currentCake == null) continue;

            Vector3 cakePos = currentCake.transform.position;

            // stopping distance
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(cakePos);
            agent.isStopped = false;

            // wait until reach the cake — update animator Speed to drive walking animation
            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            {
                if (currentAnimator != null)
                {
                    currentAnimator.SetFloat("Speed", agent.velocity.magnitude);
                }
                yield return null;
            }

            // stop and ready to eat
            agent.isStopped = true;
            agent.velocity = Vector3.zero; // avoid sliding
            if (currentAnimator != null) currentAnimator.SetFloat("Speed", 0f);
            transform.LookAt(new Vector3(cakePos.x, transform.position.y, cakePos.z));

            // Use CrossFade to force the Eat state regardless of current animator state
            // (SetTrigger only works if there's a transition from the CURRENT state using the trigger,
            //  but the character may already be in Idle when this fires)
            if (currentAnimator != null) currentAnimator.CrossFade("Eat", 0.25f);

            // time to eat
            yield return new WaitForSeconds(3.0f);

            Destroy(currentCake);

            // check if it's the last cake
            if (i == spawnedCakes.Count - 1)
            {
                Debug.Log("finsish the last cake, Arrive！");

                // forced to Arrive state
                if (currentAnimator != null)
                {
                    currentAnimator.CrossFade("Arrive", 0.1f);
                }

                // ensure the agent is fully stopped
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();

                // exit eating state
                if (currentAnimator != null) currentAnimator.SetBool("IsEating", false);
                if (guideController != null) guideController.isEating = false;
                agent.stoppingDistance = originalStoppingDist; // reset stopping distance
                yield break;
            }
        }

        // Safety: reset eating state if loop ends without hitting the last-cake branch
        if (currentAnimator != null) currentAnimator.SetBool("IsEating", false);
        if (guideController != null) guideController.isEating = false;
        agent.stoppingDistance = originalStoppingDist;
    }

    void ClearCakes()
    {
        foreach (var cake in spawnedCakes) if (cake != null) Destroy(cake);
        spawnedCakes.Clear();
    }
}