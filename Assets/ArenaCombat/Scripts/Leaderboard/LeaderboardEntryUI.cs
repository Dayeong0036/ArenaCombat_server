using UnityEngine;
using TMPro;

public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("출력 순서: rank / playerName / stage / skill / timestamp")]
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI skillText;
    public TextMeshProUGUI timeText;

    public void SetUI(int rank, string playerName, int stage, string skill, string timestamp)
    {
        rankText.text  = rank.ToString();
        nameText.text  = playerName;
        stageText.text = stage.ToString();
        skillText.text = skill;
        timeText.text  = FormatTimestamp(timestamp);
    }

    // "2026-05-25T12:00:00Z" → "2026-05-25 12:00"
    static string FormatTimestamp(string ts)
    {
        if (string.IsNullOrEmpty(ts)) return "";
        ts = ts.Replace("T", " ").Replace("Z", "");
        return ts.Length > 16 ? ts.Substring(0, 16) : ts;
    }
}
