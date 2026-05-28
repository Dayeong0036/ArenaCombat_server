using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class LeaderboardTester : MonoBehaviour
{
    const string ProjectId  = "leaderboard-fc0ae";
    const string ApiKey     = "AIzaSyARoaw4hv5z2T4171zFu4gSWxsKldZbXNI";
    const string Collection = "leaderboard";
    const string BaseUrl    = "https://firestore.googleapis.com/v1/projects/"
                            + ProjectId + "/databases/(default)/documents/" + Collection;

    [Header("컬럼 컨테이너")]
    public Transform rankColumn;
    public Transform nameColumn;
    public Transform stageColumn;
    public Transform skillColumn;
    public Transform timeColumn;

    [Header("텍스트 아이템 프리팹 (비워두면 자동 생성)")]
    public GameObject textItemPrefab;

    [Header("한글 폰트 (비워두면 씬에서 자동 탐색)")]
    public TMP_FontAsset entryFont;

    [Header("테스트 모드")]
    public bool useTestData = false;

    // 컬럼별 고정 너비 (순위, 이름, 스테이지, 스킬, 시간)
    static readonly float[] ColWidths = { 80f, 190f, 110f, 270f, 210f };
    const float RowHeight = 50f;
    const float RowSpacing = 8f;

    bool isLoading = false;
    TMP_FontAsset _resolvedFont;

    TMP_FontAsset ResolveFont()
    {
        if (_resolvedFont != null) return _resolvedFont;
        if (entryFont != null) { _resolvedFont = entryFont; return _resolvedFont; }

        // 씬의 모든 TMP 텍스트 중 한글('가') 지원 폰트를 우선 사용
        foreach (var tmp in FindObjectsOfType<TextMeshProUGUI>())
        {
            if (tmp.font != null && tmp.font.HasCharacter('가'))
            {
                _resolvedFont = tmp.font;
                return _resolvedFont;
            }
        }

        Debug.LogWarning("[LB] 한글 지원 폰트를 찾지 못했습니다. Entry Font 필드에 직접 연결해주세요.");
        return null;
    }

    void Start()
    {
        if (rankColumn == null) { Debug.LogError("[LB] rankColumn이 비어 있습니다!"); return; }

        InitLayout();

        if (useTestData)
            GenerateTestData();
        else
            StartCoroutine(LoadLeaderboard());
    }

    // Content와 5개 컬럼의 레이아웃을 코드에서 자동 설정
    void InitLayout()
    {
        // Content (컬럼들의 부모) → 기존 VerticalLayoutGroup 제거 후 HorizontalLayoutGroup 교체
        var content = rankColumn.parent;
        if (content != null)
        {
            var existing = content.GetComponent<VerticalLayoutGroup>();
            if (existing != null) DestroyImmediate(existing);

            var hlg = content.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (hlg == null) { Debug.LogError("[LB] HorizontalLayoutGroup 추가 실패"); return; }
            hlg.childAlignment         = TextAnchor.UpperLeft;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing                = 0f;
            hlg.padding                = new RectOffset(10, 10, 6, 6);
        }

        // 5개 컬럼 각각 너비 + Vertical Layout Group 설정
        SetupColumn(rankColumn,  ColWidths[0]);
        SetupColumn(nameColumn,  ColWidths[1]);
        SetupColumn(stageColumn, ColWidths[2]);
        SetupColumn(skillColumn, ColWidths[3]);
        SetupColumn(timeColumn,  ColWidths[4]);
    }

    void SetupColumn(Transform col, float width)
    {
        if (col == null) return;

        // 너비 고정
        var le = col.GetComponent<LayoutElement>();
        if (le == null) le = col.gameObject.AddComponent<LayoutElement>();
        le.minWidth       = width;
        le.preferredWidth = width;
        le.flexibleWidth  = 0f;

        // 세로 스택 정렬
        var vlg = col.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing                = RowSpacing;

        // 컬럼 높이 자동 확장
        var csf = col.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = col.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void GenerateTestData()
    {
        ClearColumns();

        var list = new List<(string name, int stage, string skill)>();
        for (int i = 0; i < 15; i++)
        {
            string name  = randomNames[Random.Range(0, randomNames.Length)] + Random.Range(1, 100);
            int    stage = Random.Range(1, 21);
            string skill = randomSkills[Random.Range(0, randomSkills.Length)]
                         + ", " + randomSkills[Random.Range(0, randomSkills.Length)];
            list.Add((name, stage, skill));
        }
        list.Sort((a, b) => b.stage.CompareTo(a.stage));

        for (int i = 0; i < list.Count; i++) AddText(rankColumn,  (i + 1).ToString());
        foreach (var e in list)              AddText(nameColumn,   e.name);
        foreach (var e in list)              AddText(stageColumn,  e.stage.ToString());
        foreach (var e in list)              AddText(skillColumn,  e.skill);
        foreach (var e in list)              AddText(timeColumn,   FormatClearTime(Random.Range(30f, 600f)));

        Debug.Log("[LB] 테스트 데이터 15개 생성 완료");
        ScrollToTop();
    }

    public IEnumerator LoadLeaderboard()
    {
        if (isLoading) yield break;
        isLoading = true;
        ClearColumns();
        Debug.Log("[LB] Firebase 요청 시작...");

        string url = $"{BaseUrl}?key={ApiKey}&pageSize=15";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LB] 네트워크 오류: {req.responseCode} / {req.error}");
            isLoading = false;
            yield break;
        }

        var response = JsonUtility.FromJson<FirestoreListResponse>(req.downloadHandler.text);
        if (response?.documents == null || response.documents.Length == 0)
        {
            Debug.LogWarning("[LB] 문서 없음");
            isLoading = false;
            yield break;
        }

        var docs = new List<FirestoreDocument>(response.documents);
        docs.Sort((a, b) =>
        {
            int sa = int.Parse(a.fields.stage.integerValue);
            int sb = int.Parse(b.fields.stage.integerValue);
            if (sb != sa) return sb.CompareTo(sa);

            double ta = double.TryParse(a.fields.clearTime?.doubleValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double _ta) ? _ta : double.MaxValue;
            double tb = double.TryParse(b.fields.clearTime?.doubleValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double _tb) ? _tb : double.MaxValue;
            return ta.CompareTo(tb);
        });

        for (int i = 0; i < docs.Count; i++) AddText(rankColumn,  (i + 1).ToString());
        foreach (var d in docs)              AddText(nameColumn,   d.fields.playerName.stringValue);
        foreach (var d in docs)              AddText(stageColumn,  d.fields.stage.integerValue);
        foreach (var d in docs)              AddText(skillColumn,  JoinSkills(d.fields.skill?.arrayValue?.values));
        foreach (var d in docs)
        {
            string ct = d.fields.clearTime?.doubleValue;
            string timeText = !string.IsNullOrEmpty(ct)
                ? FormatClearTime(ct)
                : FormatTimestamp(d.fields.timestamp?.timestampValue ?? "");
            AddText(timeColumn, timeText);
        }

        Debug.Log("[LB] 출력 완료");
        isLoading = false;

        ScrollToTop();
    }

    void AddText(Transform column, string text)
    {
        TextMeshProUGUI tmp;
        if (textItemPrefab != null)
        {
            var go = Instantiate(textItemPrefab, column);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            var go = new GameObject("Entry", typeof(RectTransform));
            go.transform.SetParent(column, false);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight       = RowHeight;
            le.preferredHeight = RowHeight;

            tmp = go.AddComponent<TextMeshProUGUI>();
            var font = ResolveFont();
            if (font != null) tmp.font = font;
            tmp.fontSize            = 26;
            tmp.color               = Color.white;
            tmp.alignment           = TextAlignmentOptions.Center;
            tmp.enableWordWrapping  = false;
            tmp.overflowMode        = TextOverflowModes.Ellipsis;
        }
        if (tmp != null) tmp.text = text;
    }

    void ScrollToTop()
    {
        var scrollRect = rankColumn.GetComponentInParent<ScrollRect>();
        if (scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    void ClearColumns()
    {
        ClearChildren(rankColumn);
        ClearChildren(nameColumn);
        ClearChildren(stageColumn);
        ClearChildren(skillColumn);
        ClearChildren(timeColumn);
    }

    static void ClearChildren(Transform t)
    {
        if (t == null) return;
        foreach (Transform child in t) Destroy(child.gameObject);
    }

    // ── 수동 테스트 ────────────────────────────────────
    [Header("수동 테스트 입력")]
    public string inputName      = "테스터";
    public int    inputStage     = 5;
    public double inputClearTime = 120.0;
    public string inputSkills    = "파이어볼, 대시";

    [ContextMenu("입력한 데이터로 항목 추가하기")]
    public void AddEntryManual()
    {
        int pos = rankColumn != null ? rankColumn.childCount + 1 : 1;
        AddText(rankColumn,  pos.ToString());
        AddText(nameColumn,  inputName);
        AddText(stageColumn, inputStage.ToString());
        AddText(skillColumn, inputSkills);
        AddText(timeColumn,  System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));
        Debug.Log($"{inputName} 추가됨 (stage={inputStage})");
    }

    [ContextMenu("리스트 모두 지우기")]
    public void ClearLeaderboard() => ClearColumns();

    readonly string[] randomNames  = { "용사", "마법사", "궁수", "도적", "성기사", "초보자", "전사", "힐러" };
    readonly string[] randomSkills = { "파이어볼", "회복", "대시", "연속베기", "은신", "방패치기", "번개", "빙결" };

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

    // "2026-05-27T04:15:00Z" → "2026-05-27 04:15"
    static string FormatTimestamp(string ts)
    {
        if (string.IsNullOrEmpty(ts)) return "-";
        ts = ts.Replace("T", " ").Replace("Z", "");
        return ts.Length > 16 ? ts.Substring(0, 16) : ts;
    }

    // "123.5" 또는 float 값 → "2분 03초"
    static string FormatClearTime(string doubleValueStr)
    {
        if (string.IsNullOrEmpty(doubleValueStr)) return "-";
        if (!double.TryParse(doubleValueStr,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double seconds)) return "-";
        return FormatClearTime((float)seconds);
    }

    static string FormatClearTime(float seconds)
    {
        int total = Mathf.Max(0, (int)seconds);
        int m = total / 60;
        int s = total % 60;
        return m > 0 ? $"{m}분 {s:D2}초" : $"{s}초";
    }
}
