using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Linq;

public class ObstacleAvoidanceAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Environment References")]
    [SerializeField] private Transform goal;
    [SerializeField] private Renderer groundRenderer;
    [SerializeField] private Transform smallGoalParent;
    [SerializeField] private Transform obstacle;

    private Rigidbody rb;
    private Renderer rendererComponent;
    private Coroutine flashGroundCoroutine;
    private Color defaultGroundColor;
    private Vector3 initialGoalPosition;
    private GameObject[] smallGoals;

    private float idleTime = 0f;
    private float wallCollisionTime = 0f;
    private int consecutiveRotationSteps = 0;

    private const float IdleThreshold = 3f;
    private const float WallPenaltyThreshold = 2f;

    [Header("Static/Random Spawning Settings")]
    public bool startStaticGoal = true;
    public int staticGoalEpisodes = 50;
    public float goalMinX = -3f, goalMaxX = 3f;
    public float goalMinZ = 1.5f, goalMaxZ = 4f;

    public bool startStaticAgent = true;
    public int staticAgentEpisodes = 50;
    public float agentMinX = -3f, agentMaxX = 3f;
    public float agentMinZ = -4f, agentMaxZ = -2.5f;

    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rendererComponent = GetComponent<Renderer>();
        initialGoalPosition = goal.localPosition;

        if (groundRenderer != null)
            defaultGroundColor = groundRenderer.material.color;

        smallGoals = smallGoalParent.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.CompareTag("SmallGoal"))
                    .Select(t => t.gameObject)
                    .ToArray();
    }

    public override void OnEpisodeBegin()
    {
        if (groundRenderer != null && CumulativeReward != 0f)
        {
            Color flashColor = (CumulativeReward > 0f) ? Color.green : Color.red;

            if (flashGroundCoroutine != null)
                StopCoroutine(flashGroundCoroutine);

            flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 3.0f));
        }

        CurrentEpisode++;
        CumulativeReward = 0f;
        idleTime = 0f;
        wallCollisionTime = 0f;
        consecutiveRotationSteps = 0;

        if (rendererComponent != null)
            rendererComponent.material.color = Color.blue;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        ReactivateSmallGoals();
        SpawnAgent();
        SpawnGoal();
        SpawnObstacle();
    }

    private void ReactivateSmallGoals()
    {
        foreach (GameObject goalObj in smallGoals)
        {
            if (goalObj != null)
                goalObj.SetActive(true);
        }
    }

    private void SpawnAgent()
    {
        if (!(startStaticAgent && CurrentEpisode <= staticAgentEpisodes))
        {
            transform.localPosition = new Vector3(
                Random.Range(agentMinX, agentMaxX),
                transform.localPosition.y,
                Random.Range(agentMinZ, agentMaxZ)
            );
            transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        else
        {
            transform.localPosition = new Vector3(0.9f, 0.39f, -9.12f);
            transform.localRotation = Quaternion.identity;
        }
    }

    private void SpawnGoal()
    {
        if (!(startStaticGoal && CurrentEpisode <= staticGoalEpisodes))
        {
            goal.localPosition = new Vector3(
                Random.Range(goalMinX, goalMaxX),
                goal.localPosition.y,
                Random.Range(goalMinZ, goalMaxZ)
            );
        }
        else
        {
            goal.localPosition = initialGoalPosition;
        }
    }

    private void SpawnObstacle()
    {
        if (obstacle != null)
        {
            float[] possibleLocalX = { 2f, -1.4f };
            float chosenLocalX = possibleLocalX[Random.Range(0, possibleLocalX.Length)];

            Vector3 localObstaclePosition = obstacle.localPosition;
            localObstaclePosition.x = chosenLocalX;
            obstacle.localPosition = localObstaclePosition;

            Rigidbody obstacleRb = obstacle.GetComponent<Rigidbody>();
            if (obstacleRb != null)
            {
                obstacleRb.linearVelocity = Vector3.zero;
                obstacleRb.angularVelocity = Vector3.zero;
            }

            if (smallGoalParent != null)
            {
                Transform smallGoal = smallGoalParent.Find("Goal(Small)");
                if (smallGoal != null)
                {
                    Vector3 smallGoalLocalPosition = smallGoal.localPosition;

                    if (chosenLocalX == 2f)
                        smallGoalLocalPosition.x = -3.6f;
                    else if (chosenLocalX == -1.4f)
                        smallGoalLocalPosition.x = 3.6f;

                    smallGoal.localPosition = smallGoalLocalPosition;
                }
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(goal.localPosition.x / 5f);
        sensor.AddObservation(goal.localPosition.z / 5f);
        sensor.AddObservation(transform.localPosition.x / 5f);
        sensor.AddObservation(transform.localPosition.z / 5f);
        sensor.AddObservation((transform.localRotation.eulerAngles.y / 360f) * 2f - 1f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];
        int turnAction = actions.DiscreteActions[1];

        MoveAgent(moveAction, turnAction);

        AddReward(-0.75f / MaxStep); 

        if (rb.linearVelocity.magnitude < 0.01f)
        {
            idleTime += Time.deltaTime;
            if (idleTime >= IdleThreshold)
            {
                AddReward(-0.1f); 
                idleTime = 0f;
            }
        }
        else
        {
            idleTime = 0f;
        }

        if ((turnAction != 0) && rb.linearVelocity.magnitude < 0.05f)
        {
            consecutiveRotationSteps++;
            if (consecutiveRotationSteps > 10)
                AddReward(-0.01f);
        }
        else
        {
            consecutiveRotationSteps = 0;
        }


        if (wallCollisionTime > WallPenaltyThreshold)
        {
            AddReward(-0.5f); 
            wallCollisionTime = 0f; 
        }

        CumulativeReward = GetCumulativeReward();
    }

    private void MoveAgent(int moveAction, int turnAction)
    {
        if (moveAction == 1)
            transform.position += transform.forward * moveSpeed * Time.deltaTime;

        if (turnAction == 1)
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
        else if (turnAction == 2)
            transform.Rotate(0f, -rotationSpeed * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Goal"))
        {
            AddReward(1.0f);
            CumulativeReward = GetCumulativeReward();
            EndEpisode();
        }
        else if (other.CompareTag("SmallGoal") && other.gameObject.activeSelf)
        {
            AddReward(0.2f);
            CumulativeReward = GetCumulativeReward();
            other.gameObject.SetActive(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(-0.05f);
            if (rendererComponent != null)
                rendererComponent.material.color = Color.red;
            wallCollisionTime = 0f; 
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle"))
        {
            wallCollisionTime += Time.deltaTime;
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle"))
        {
            wallCollisionTime = 0f;
            if (rendererComponent != null)
                rendererComponent.material.color = Color.blue;
        }
    }

    private IEnumerator FlashGround(Color targetColor, float duration)
    {
        if (groundRenderer == null) yield break;

        float elapsed = 0f;
        groundRenderer.material.color = targetColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            groundRenderer.material.color = Color.Lerp(targetColor, defaultGroundColor, elapsed / duration);
            yield return null;
        }
    }
}