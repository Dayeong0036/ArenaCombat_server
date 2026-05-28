using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseLeaderboardManager : MonoBehaviour
{
    const string ProjectId = "leaderboard-fc0ae";
    const string ApiKey = "AIzaSyARoaw4hv5z2T4171zFu4gSWxsKldZbXNI";
    const string Collection = "leaderboard";
    const string BaseUrl = "https://firestore.googleapis.com/v1/projects/" + ProjectId + "/databases/(default)/documents/" + Collection;

    [Header("UI 연결")]
    public GameObject entryPrefab;
    public Transform contentArea;

    bool isLoading = false;

    // Start() 없음 — 로딩은 LeaderboardTester가 담당, 이 클래스는 SaveEntry 전용

    public IEnumerator LoadLeaderboard()
    {
        if (isLoading) yield break;
        isLoading = true;
        ClearEntries();

        string url = $"{BaseUrl}?key={ApiKey}&pageSize=20";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Firebase] 리더보드 로드 실패: {req.error}");
            isLoading = false;
            yield break;
        }

        var response = JsonUtility.FromJson<FirestoreListResponse>(req.downloadHandler.text);
        if (response?.documents == null || response.documents.Length == 0)
        {
            Debug.Log("[Firebase] 리더보드 데이터 없음");
            isLoading = false;
            yield break;
        }

        var docs = new List<FirestoreDocument>(response.documents);
        // 스테이지 높은 순 → 클리어 시간 짧은 순
        docs.Sort((a, b) =>
        {
            int stageA = int.Parse(a.fields.stage.integerValue);
            int stageB = int.Parse(b.fields.stage.integerValue);
            if (stageB != stageA) return stageB.CompareTo(stageA);

            double timeA = double.TryParse(a.fields.clearTime?.doubleValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double ta) ? ta : double.MaxValue;
            double timeB = double.TryParse(b.fields.clearTime?.doubleValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double tb) ? tb : double.MaxValue;
            return timeA.CompareTo(timeB);
        });

        for (int i = 0; i < docs.Count; i++)
        {
            var f = docs[i].fields;
            int stage = int.Parse(f.stage.integerValue);

            var skillValues = f.skill?.arrayValue?.values ?? new FirestoreStringVal[0];
            string skillStr = string.Join(", ", System.Array.ConvertAll(skillValues, v => v.stringValue));

            string timestamp = f.timestamp?.timestampValue ?? "";

            var go = Instantiate(entryPrefab, contentArea);
            go.GetComponent<LeaderboardEntryUI>()?.SetUI(i + 1, f.playerName.stringValue, stage, skillStr, timestamp);
        }
        isLoading = false;
    }

    public IEnumerator SaveEntry(string playerName, int stage, double clearTime, List<string> skills)
    {
        string body = BuildDocumentJson(playerName, stage, clearTime, skills);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        string url = $"{BaseUrl}?key={ApiKey}";
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[Firebase] 저장 완료: {playerName}");
            yield return LoadLeaderboard();
        }
        else
        {
            Debug.LogError($"[Firebase] 저장 실패: {req.error}\n{req.downloadHandler.text}");
        }
    }

    void ClearEntries()
    {
        foreach (Transform t in contentArea)
            Destroy(t.gameObject);
    }

    static string BuildDocumentJson(string playerName, int stage, double clearTime, List<string> skills)
    {
        string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string clearTimeStr = clearTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        var skillValues = new StringBuilder();
        for (int i = 0; i < skills.Count; i++)
        {
            if (i > 0) skillValues.Append(",");
            skillValues.Append($"{{\"stringValue\":\"{Escape(skills[i])}\"}}");
        }

        return "{\"fields\":{" +
            $"\"playerName\":{{\"stringValue\":\"{Escape(playerName)}\"}}," +
            $"\"stage\":{{\"integerValue\":\"{stage}\"}}," +
            $"\"clearTime\":{{\"doubleValue\":{clearTimeStr}}}," +
            $"\"timestamp\":{{\"timestampValue\":\"{timestamp}\"}}," +
            $"\"skill\":{{\"arrayValue\":{{\"values\":[{skillValues}]}}}}" +
            "}}";
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

// Firestore REST API 응답 구조
[System.Serializable] class FirestoreListResponse { public FirestoreDocument[] documents; }
[System.Serializable] class FirestoreDocument { public string name; public LeaderboardDocFields fields; }
[System.Serializable] class LeaderboardDocFields
{
    public FirestoreStringVal playerName;
    public FirestoreIntVal stage;
    public FirestoreDoubleVal clearTime;
    public FirestoreTimestampVal timestamp;
    public FirestoreArrayVal skill;
}
[System.Serializable] class FirestoreStringVal { public string stringValue; }
[System.Serializable] class FirestoreIntVal { public string integerValue; }
[System.Serializable] class FirestoreDoubleVal { public string doubleValue; }
[System.Serializable] class FirestoreTimestampVal { public string timestampValue; }
[System.Serializable] class FirestoreArrayVal { public FirestoreArrayValues arrayValue; }
[System.Serializable] class FirestoreArrayValues { public FirestoreStringVal[] values; }
