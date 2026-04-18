using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 점수 관리: 터치당 5점, 같은 봉지 반복 터치 시 x2 콤보
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI 연결")]
    public TextMeshProUGUI scoreText;

    [Header("점수 설정")]
    public int pointsPerHit = 5;

    private int _totalScore = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    // HandBounceResponder에서 호출됨
    // hitCount: 해당 봉지의 누적 터치 횟수
    public void OnBagHit(int hitCount)
    {
        // 첫 터치: 5점, 두번째: 10점(x2), 세번째: 10점(x2 유지)...
        int multiplier = hitCount >= 2 ? 2 : 1;
        int points = pointsPerHit * multiplier;
        _totalScore += points;

        UpdateUI();
        Debug.Log($"[Score] +{points}점 (x{multiplier}) = 총 {_totalScore}점");
    }

    // 범용 점수 추가 (공을 상자에 넣을 때 등)
    public void AddScore(int points, string source = "")
    {
        _totalScore += points;
        UpdateUI();
        Debug.Log($"[Score] +{points}점 ({source}) = 총 {_totalScore}점");
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_totalScore}";
    }
}
