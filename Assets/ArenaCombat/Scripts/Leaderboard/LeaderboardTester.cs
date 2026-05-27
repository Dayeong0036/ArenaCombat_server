using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LeaderboardTester : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject entryPrefab;
    public Transform contentArea;

    const string ProjectId  = "leaderboard-fc0ae";
    const string ApiKey     = "AIzaSyARoaw4hv5z2T4171zFu4gSWxsKldZbXNI";
    const string Collection = "leaderboard";
    const string BaseUrl    = "https://firestore.googleapis.com/v1/projects/"
                            + ProjectId + "/databases/(default)/documents/" + Collection;

    [Header("테스트 모드")]
    public bool useTestData = false; // true: 랜덤 15개 / false: Firebase

    void Start()
    {
        if (entryPrefab == null) { Debug.LogError("[LB] entryPrefab이 비어 있습니다!"); return; }
        if (contentArea == null) { Debug.LogError("[LB] contentArea가 비어 있습니다!"); return; }

        if (useTestData)
            GenerateTestData();
        else
            StartCoroutine(LoadLeaderboard());
    }

    void GenerateTestData()
    {
        ClearEntries();
        for (int i = 1; i <= 15; i++)
        {
            string rName  = randomNames[Random.Range(0, randomNames.Length)]
                          + Random.Range(1, 100);
            int    rStage = Random.Range(1, 21);
            string skill1 = randomSkills[Random.Range(0, randomSkills.Length)];
            string skill2 = randomSkills[Random.Range(0, randomSkills.Length)];
            string rSkill = $"{skill1}, {skill2}";
            string rTime  = System.DateTime.UtcNow.AddSeconds(-Random.Range(0, 3600))
                                .ToString("yyyy-MM-dd HH:mm");

            var go = Instantiate(entryPrefab, contentArea);
            go.GetComponent<LeaderboardEntryUI>()
              ?.SetUI(i, rName, rStage, rSkill, rTime);
        }
        Debug.Log("[LB] 테스트 데이터 15개 생성 완료");
    }

    // ── Firebase 읽기 → 5개 컬럼 출력 ──────────────────
    public IEnumerator LoadLeaderboard()
    {
        ClearEntries();
        Debug.Log("[LB] Firebase 요청 시작...");

        string url = $"{BaseUrl}?key={ApiKey}&pageSize=15";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LB] 네트워크 오류: {req.responseCode} / {req.error}");
            yield break;
        }

        Debug.Log($"[LB] 응답 수신: {req.downloadHandler.text}");

        var response = JsonUtility.FromJson<FirestoreListResponse>(req.downloadHandler.text);
        if (response?.documents == null || response.documents.Length == 0)
        {
            Debug.LogWarning("[LB] 문서 없음 (documents null 또는 빈 배열)");
            yield break;
        }

        Debug.Log($"[LB] 문서 수: {response.documents.Length}");

        var docs = new List<FirestoreDocument>(response.documents);
        docs.Sort((a, b) =>
            int.Parse(a.fields.rank.integerValue)
            .CompareTo(int.Parse(b.fields.rank.integerValue)));

        foreach (var doc in docs)
        {
            var f = doc.fields;

            int    rank      = int.Parse(f.rank.integerValue);
            string name      = f.playerName.stringValue;
            int    stage     = int.Parse(f.stage.integerValue);
            string skill     = JoinSkills(f.skill?.arrayValue?.values);
            string timestamp = f.timestamp?.timestampValue ?? "";

            Debug.Log($"[LB] 항목: rank={rank}, name={name}, stage={stage}, skill={skill}");

            var go = Instantiate(entryPrefab, contentArea);
            go.GetComponent<LeaderboardEntryUI>()
              ?.SetUI(rank, name, stage, skill, timestamp);
        }

        Debug.Log("[LB] 출력 완료");
    }

    // ── 수동 테스트 ────────────────────────────────────
    [Header("수동 테스트 입력")]
    public int    inputRank   = 1;
    public string inputName   = "테스터";
    public int    inputStage  = 5;
    public string inputSkills = "파이어볼, 대시";

    [ContextMenu("입력한 데이터로 항목 추가하기")]
    public void AddEntryManual()
    {
        if (entryPrefab == null || contentArea == null)
        {
            Debug.LogError("프리팹 또는 Content Area가 연결되지 않았습니다!");
            return;
        }
        string ts = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var go = Instantiate(entryPrefab, contentArea);
        go.GetComponent<LeaderboardEntryUI>()
          ?.SetUI(inputRank, inputName, inputStage, inputSkills, ts);
        Debug.Log($"{inputRank}위 {inputName} 추가됨");
        inputRank++;
    }

    [ContextMenu("리스트 모두 지우기")]
    public void ClearLeaderboard() { ClearEntries(); inputRank = 1; }

    // ── 랜덤 데이터 풀 ─────────────────────────────────
    readonly string[] randomNames  = { "용사", "마법사", "궁수", "도적", "성기사", "초보자", "전사", "힐러" };
    readonly string[] randomSkills = { "파이어볼", "회복", "대시", "연속베기", "은신", "방패치기", "번개", "빙결" };

    // ── 유틸 ───────────────────────────────────────────
    void ClearEntries()
    {
        foreach (Transform t in contentArea) Destroy(t.gameObject);
    }

    static string JoinSkills(FirestoreStringVal[] values)
    {
        if (values == null || values.Length == 0) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i].stringValue);
        }
        return sb.ToString();
    }
}
