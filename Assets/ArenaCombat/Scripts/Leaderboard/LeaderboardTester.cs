using System.Collections.Generic;
using UnityEngine;

public class LeaderboardTester : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject entryPrefab;
    public Transform contentArea;

    [Header("Firebase 연동 (선택)")]
    public FirebaseLeaderboardManager firebaseManager;

    [Header("테스트 데이터 직접 입력란")]
    public int inputRank = 1;
    public string inputName = "테스터";
    public int inputStage = 5;
    public string inputSkills = "파이어볼, 대시";

    private string[] randomNames = { "용사", "마법사", "궁수", "도적", "성기사", "초보자" };
    private string[] randomSkills = { "파이어볼", "회복", "대시", "연속베기", "은신", "방패치기" };

    void Start()
    {
        if (firebaseManager != null)
            StartCoroutine(firebaseManager.LoadLeaderboard());
    }

    private void AddRandomEntry()
    {
        if (entryPrefab == null || contentArea == null) return;

        string rName = randomNames[Random.Range(0, randomNames.Length)] + Random.Range(1, 100);
        int rStage = Random.Range(1, 21);
        string rSkills = randomSkills[Random.Range(0, randomSkills.Length)]
                       + ", " + randomSkills[Random.Range(0, randomSkills.Length)];
        string rTimestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        GameObject newEntry = Instantiate(entryPrefab, contentArea);
        LeaderboardEntryUI entryUI = newEntry.GetComponent<LeaderboardEntryUI>();

        if (entryUI != null)
        {
            entryUI.SetUI(inputRank, rName, rStage, rSkills, rTimestamp);
            inputRank++;
        }
    }

    [ContextMenu("입력한 데이터로 항목 추가하기")]
    public void AddEntryToLeaderboard()
    {
        if (entryPrefab == null || contentArea == null)
        {
            Debug.LogError("프리팹이나 Content Area가 연결되지 않았습니다!");
            return;
        }

        string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        GameObject newEntry = Instantiate(entryPrefab, contentArea);
        LeaderboardEntryUI entryUI = newEntry.GetComponent<LeaderboardEntryUI>();

        if (entryUI != null)
            entryUI.SetUI(inputRank, inputName, inputStage, inputSkills, timestamp);

        Debug.Log($"{inputRank}위 {inputName} 추가됨!");

        if (firebaseManager != null)
        {
            var skillList = new List<string>(inputSkills.Split(','));
            for (int i = 0; i < skillList.Count; i++)
                skillList[i] = skillList[i].Trim();
            StartCoroutine(firebaseManager.SaveEntry(inputName, inputRank, inputStage, skillList));
        }

        inputRank++;
    }

    [ContextMenu("리스트 모두 지우기")]
    public void ClearLeaderboard()
    {
        foreach (Transform child in contentArea)
            Destroy(child.gameObject);
        inputRank = 1;
    }
}
