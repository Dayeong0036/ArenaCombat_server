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

    void Start() => StartCoroutine(LoadLeaderboard());

    public IEnumerator LoadLeaderboard()
    {
        ClearEntries();

        string url = $"{BaseUrl}?key={ApiKey}&pageSize=20";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Firebase] 리더보드 로드 실패: {req.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<FirestoreListResponse>(req.downloadHandler.text);
        if (response?.documents == null || response.documents.Length == 0)
        {
            Debug.Log("[Firebase] 리더보드 데이터 없음");
            yield break;
        }

        var docs = new List<FirestoreDocument>(response.documents);
        docs.Sort((a, b) =>
            int.Parse(a.fields.rank.integerValue)
            .CompareTo(int.Parse(b.fields.rank.integerValue)));

        for (int i = 0; i < docs.Count; i++)
        {
            var f = docs[i].fields;
            int rank = int.Parse(f.rank.integerValue);
            int stage = int.Parse(f.stage.integerValue);

            var skillValues = f.skill?.arrayValue?.values ?? new FirestoreStringVal[0];
            string skillStr = string.Join(", ", System.Array.ConvertAll(skillValues, v => v.stringValue));

            string timestamp = f.timestamp?.timestampValue ?? "";

            var go = Instantiate(entryPrefab, contentArea);
            go.GetComponent<LeaderboardEntryUI>()?.SetUI(rank, f.playerName.stringValue, stage, skillStr, timestamp);
        }
    }

    public IEnumerator SaveEntry(string playerName, int rank, int stage, List<string> skills)
    {
        string body = BuildDocumentJson(playerName, rank, stage, skills);
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

    static string BuildDocumentJson(string playerName, int rank, int stage, List<string> skills)
    {
        string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var skillValues = new StringBuilder();
        for (int i = 0; i < skills.Count; i++)
        {
            if (i > 0) skillValues.Append(",");
            skillValues.Append($"{{\"stringValue\":\"{Escape(skills[i])}\"}}");
        }

        return "{\"fields\":{" +
            $"\"playerName\":{{\"stringValue\":\"{Escape(playerName)}\"}}," +
            $"\"rank\":{{\"integerValue\":\"{rank}\"}}," +
            $"\"stage\":{{\"integerValue\":\"{stage}\"}}," +
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
    public FirestoreIntVal rank;
    public FirestoreIntVal stage;
    public FirestoreTimestampVal timestamp;
    public FirestoreArrayVal skill;
}
[System.Serializable] class FirestoreStringVal { public string stringValue; }
[System.Serializable] class FirestoreIntVal { public string integerValue; }
[System.Serializable] class FirestoreTimestampVal { public string timestampValue; }
[System.Serializable] class FirestoreArrayVal { public FirestoreArrayValues arrayValue; }
[System.Serializable] class FirestoreArrayValues { public FirestoreStringVal[] values; }
