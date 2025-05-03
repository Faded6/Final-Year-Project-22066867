using UnityEngine;

public class GUI_Agent : MonoBehaviour
{
    [SerializeField] private ObstacleAvoidanceAgent _agent;

    private GUIStyle _defaultStyle = new GUIStyle();
    private GUIStyle _positiveStyle = new GUIStyle();
    private GUIStyle _negativeStyle = new GUIStyle();

    void Start()
    {
        _defaultStyle.fontSize = 20;
        _defaultStyle.normal.textColor = Color.yellow;

        _positiveStyle.fontSize = 20;
        _positiveStyle.normal.textColor = Color.green;

        _negativeStyle.fontSize = 20;
        _negativeStyle.normal.textColor = Color.red;
    }

    private void OnGUI()
    {
        if (_agent == null) return;

        string debugEpisode = "Episode: " + _agent.CurrentEpisode + " - Steps: " + _agent.StepCount;
        string debugReward = "Reward: " + _agent.CumulativeReward.ToString("F2");

        GUIStyle rewardStyle = _agent.CumulativeReward >= 0 ? _positiveStyle : _negativeStyle;

        GUI.Label(new Rect(20, 20, 500, 30), debugEpisode, _defaultStyle);
        GUI.Label(new Rect(20, 60, 500, 30), debugReward, rewardStyle);
    }
}
