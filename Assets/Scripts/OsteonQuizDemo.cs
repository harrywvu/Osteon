using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// One-learner practice quiz shown in the current Quest scene. The same HTTP
/// client code can run in a Unity web build; AWS owns answers and mastery.
/// </summary>
public sealed class OsteonQuizDemo : MonoBehaviour
{
    [Serializable] private sealed class Config
    {
        public string baseUrl;
        public string demoToken;
    }

    [Serializable] private sealed class Options
    {
        public string A;
        public string B;
        public string C;
        public string D;

        public string Get(string letter) => letter == "A" ? A : letter == "B" ? B : letter == "C" ? C : D;
    }

    [Serializable] private sealed class Question
    {
        public string question_id;
        public string bone_name;
        public string difficulty_level;
        public string question_text;
        public Options options;
    }

    [Serializable] private sealed class SessionResponse
    {
        public string session_id;
        public float mastery;
        public int answered_count;
        public bool completed;
        public Question question;
    }

    [Serializable] private sealed class AnswerRequest
    {
        public string question_id;
        public string choice;
        public string attempt_id;
    }

    [Serializable] private sealed class AnswerResponse
    {
        public string question_id;
        public bool correct;
        public string correct_option;
        public float updated_mastery;
        public int answered_count;
        public bool completed;
        public Question next_question;
    }

    private static readonly string[] Letters = { "A", "B", "C", "D" };
    private Config config;
    private GameObject panel;
    private GameObject launcherObject;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI questionText;
    private TextMeshProUGUI feedbackText;
    private readonly Button[] optionButtons = new Button[4];
    private Button actionButton;
    private TextMeshProUGUI actionLabel;
    private TextMeshProUGUI modeLabel;
    private string sessionId;
    private Question currentQuestion;
    private Question nextQuestion;
    private AnswerRequest pendingAnswer;
    private bool busy;
    private bool inPractice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "CONTROLLERS MIGRATION" ||
            FindFirstObjectByType<OsteonQuizDemo>() != null) return;
        Debug.Log("[OsteonQuiz] Starting in scene: " + SceneManager.GetActiveScene().name);
        new GameObject("Osteon practice quiz").AddComponent<OsteonQuizDemo>();
    }

    private IEnumerator Start()
    {
        TextAsset settings = Resources.Load<TextAsset>("QuizDemoConfig");
        if (settings != null)
        {
            try { config = JsonUtility.FromJson<Config>(settings.text); }
            catch (Exception error) { Debug.LogWarning("Quiz configuration is invalid: " + error.Message); }
        }
        Camera viewer = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        while (viewer == null)
        {
            yield return null;
            viewer = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }
        BuildUi(viewer);
    }

    private void BuildUi(Camera viewer)
    {
        // Keep the station beside the existing anatomy information board. It
        // belongs to the scene, not the HMD, so looking away does not move it.
        Transform station = new GameObject("Quiz station").transform;
        station.SetParent(transform, false);
        GameObject anatomyPanel = GameObject.Find("Anatomy information panel");
        if (anatomyPanel != null)
        {
            Transform reference = anatomyPanel.transform;
            station.SetPositionAndRotation(reference.position + reference.right * 0.95f,
                reference.rotation);
        }
        else
        {
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            station.SetPositionAndRotation(viewer.transform.position + forward * 1.55f,
                Quaternion.LookRotation(forward, Vector3.up));
            Debug.LogWarning("[OsteonQuiz] Anatomy panel not found; using a fixed fallback position.");
        }

        Canvas launcher = CreateCanvas("Quiz launcher", viewer, station);
        launcherObject = launcher.gameObject;
        RectTransform launcherRect = (RectTransform)launcher.transform;
        launcherRect.localPosition = Vector3.zero;
        launcherRect.localRotation = Quaternion.identity;
        launcherRect.localScale = Vector3.one * 0.001f;
        launcherRect.sizeDelta = new Vector2(360, 100);
        launcher.GetComponent<Image>().color = new Color(0.04f, 0.20f, 0.29f, 0.96f);
        Button open = launcher.gameObject.AddComponent<Button>();
        open.targetGraphic = launcher.GetComponent<Image>();
        open.onClick.AddListener(Open);
        AddText(launcherRect, "Practice Quiz", Vector2.zero, new Vector2(350, 90), 30, TextAlignmentOptions.Center);

        Canvas quizCanvas = CreateCanvas("Quiz panel", viewer, station);
        panel = quizCanvas.gameObject;
        RectTransform panelRect = (RectTransform)quizCanvas.transform;
        panelRect.localPosition = Vector3.zero;
        panelRect.localRotation = Quaternion.identity;
        panelRect.sizeDelta = new Vector2(780, 950);
        panelRect.localScale = Vector3.one * 0.0012f;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.08f, 0.98f);
        AddText(panelRect, "OSTEON PRACTICE QUIZ", new Vector2(-45, 413),
            new Vector2(620, 72), 34, TextAlignmentOptions.Center);
        Button modeButton = AddButton(panelRect, "Hide", new Vector2(328, 413), new Vector2(84, 64));
        modeLabel = modeButton.GetComponentInChildren<TextMeshProUGUI>();
        modeButton.onClick.AddListener(() => {
            if (inPractice) ShowMenu();
            else HideMenu();
        });
        statusText = AddText(panelRect, "", new Vector2(0, 349),
            new Vector2(700, 52), 22, TextAlignmentOptions.Center);
        questionText = AddText(panelRect, "", new Vector2(0, 218),
            new Vector2(700, 190), 29, TextAlignmentOptions.Center);
        for (int i = 0; i < 4; i++)
        {
            string letter = Letters[i];
            optionButtons[i] = AddButton(panelRect, "", new Vector2(0, 82 - i * 99), new Vector2(700, 84));
            optionButtons[i].onClick.AddListener(() => Choose(letter));
        }
        feedbackText = AddText(panelRect, "", new Vector2(0, -332),
            new Vector2(700, 80), 24, TextAlignmentOptions.Center);
        actionButton = AddButton(panelRect, "", new Vector2(0, -416), new Vector2(700, 76));
        actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();
        PanelMoveHandle.Configure(station, panelRect, TMP_Settings.defaultFontAsset,
            new Vector2(0, -520), new Vector2(700, 58));
        panel.SetActive(false);
        Debug.Log("[OsteonQuiz] Stationary quiz station created beside the anatomy panel.");
        // Show the menu first. The learner explicitly starts or resumes practice.
        Open();
    }

    private void Open()
    {
        Debug.Log("[OsteonQuiz] Opening the practice menu.");
        launcherObject.SetActive(false);
        panel.SetActive(true);
        ShowMenu();
    }

    private void HideMenu()
    {
        panel.SetActive(false);
        launcherObject.SetActive(true);
    }

    private void ShowMenu()
    {
        inPractice = false;
        currentQuestion = null;
        modeLabel.text = "Hide";
        HideChoices();
        if (!Configured())
        {
            statusText.text = "Quiz service not configured";
            questionText.text = "Add the private QuizDemoConfig.json before building the Quest app.";
            feedbackText.text = "";
            actionButton.gameObject.SetActive(false);
            return;
        }
        statusText.text = busy ? "Finishing the last request..." : "Ready to practice";
        questionText.text = "Test your anatomy knowledge.\nYour progress is saved when you stop.";
        feedbackText.text = "";
        SetAction(busy ? "Please wait" : "Start / Resume Practice",
            () => StartCoroutine(StartSession()));
        actionButton.interactable = !busy;
    }

    private bool Configured()
    {
        return config != null && !string.IsNullOrWhiteSpace(config.demoToken) &&
               Uri.TryCreate(config.baseUrl, UriKind.Absolute, out Uri url) && url.Scheme == Uri.UriSchemeHttps;
    }

    private IEnumerator StartSession()
    {
        if (busy || !Configured()) yield break;
        inPractice = true;
        modeLabel.text = "Stop";
        busy = true;
        statusText.text = "Connecting to the quiz service...";
        questionText.text = "";
        feedbackText.text = "";
        HideChoices();
        actionButton.gameObject.SetActive(false);
        using (UnityWebRequest request = NewRequest("/v1/sessions", "POST", "{}"))
        {
            yield return request.SendWebRequest();
            busy = false;
            if (!inPractice)
            {
                ShowMenu();
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowError(request, "Start quiz", () => StartCoroutine(StartSession()));
                yield break;
            }
            SessionResponse result = JsonUtility.FromJson<SessionResponse>(request.downloadHandler.text);
            if (result == null || string.IsNullOrEmpty(result.session_id))
            {
                ShowMessage("The quiz service returned an invalid session.", "Retry", () => StartCoroutine(StartSession()));
                yield break;
            }
            sessionId = result.session_id;
            currentQuestion = result.question;
            if (result.completed)
                ShowMessage("Practice complete. Mastery: " + Percent(result.mastery), "Start again", () => StartCoroutine(StartSession()));
            else
                ShowQuestion(currentQuestion, "Mastery: " + Percent(result.mastery));
        }
    }

    private void Choose(string letter)
    {
        if (!inPractice || busy || currentQuestion == null) return;
        pendingAnswer = new AnswerRequest {
            question_id = currentQuestion.question_id,
            choice = letter,
            attempt_id = Guid.NewGuid().ToString()
        };
        StartCoroutine(SubmitAnswer());
    }

    private IEnumerator SubmitAnswer()
    {
        busy = true;
        SetChoicesInteractable(false);
        statusText.text = "Checking answer...";
        actionButton.gameObject.SetActive(false);
        using (UnityWebRequest request = NewRequest("/v1/sessions/" + sessionId + "/answers",
                   "POST", JsonUtility.ToJson(pendingAnswer)))
        {
            yield return request.SendWebRequest();
            busy = false;
            if (!inPractice)
            {
                ShowMenu();
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowError(request, "Retry answer", () => StartCoroutine(SubmitAnswer()));
                yield break;
            }
            AnswerResponse result = JsonUtility.FromJson<AnswerResponse>(request.downloadHandler.text);
            if (result == null || result.question_id != pendingAnswer.question_id ||
                (!result.completed && result.next_question == null))
            {
                ShowMessage("The quiz service returned an invalid answer.", "Retry answer", () => StartCoroutine(SubmitAnswer()));
                yield break;
            }
            feedbackText.text = result.correct ? "Correct!" : "Not quite. Correct answer: " + result.correct_option;
            statusText.text = "Mastery: " + Percent(result.updated_mastery) +
                              "  |  Answered: " + result.answered_count;
            nextQuestion = result.next_question;
            currentQuestion = null;
            if (result.completed)
                SetAction("Practice complete - start again", () => StartCoroutine(StartSession()));
            else
                SetAction("Next question", () => {
                    currentQuestion = nextQuestion;
                    ShowQuestion(currentQuestion, statusText.text);
                });
        }
    }

    private UnityWebRequest NewRequest(string path, string method, string body)
    {
        var request = new UnityWebRequest(config.baseUrl.TrimEnd('/') + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + config.demoToken);
        request.timeout = 20;
        return request;
    }

    private void ShowQuestion(Question question, string status)
    {
        if (question == null || question.options == null)
        {
            ShowMessage("The quiz service returned an invalid question.", "Reload quiz", () => StartCoroutine(StartSession()));
            return;
        }
        statusText.text = status;
        questionText.text = question.bone_name + " | " + question.difficulty_level + "\n" + question.question_text;
        feedbackText.text = "Select one answer";
        for (int i = 0; i < 4; i++)
        {
            string letter = Letters[i];
            optionButtons[i].gameObject.SetActive(true);
            optionButtons[i].interactable = true;
            optionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = letter + ". " + question.options.Get(letter);
        }
        actionButton.gameObject.SetActive(false);
    }

    private void ShowError(UnityWebRequest request, string action, Action retry)
    {
        string detail = request.responseCode > 0 ? "HTTP " + request.responseCode : request.error;
        ShowMessage("Quiz service unavailable (" + detail + ").", action, retry);
    }

    private void ShowMessage(string message, string action, Action callback)
    {
        statusText.text = message;
        questionText.text = "";
        feedbackText.text = "";
        HideChoices();
        SetAction(action, callback);
    }

    private void SetAction(string label, Action callback)
    {
        actionButton.gameObject.SetActive(true);
        actionButton.interactable = true;
        actionLabel.text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => callback());
    }

    private void HideChoices()
    {
        foreach (Button button in optionButtons)
            if (button != null) button.gameObject.SetActive(false);
    }

    private void SetChoicesInteractable(bool value)
    {
        foreach (Button button in optionButtons)
            if (button != null) button.interactable = value;
    }

    private static string Percent(float mastery) => Mathf.RoundToInt(mastery * 100f) + "%";

    private static Canvas CreateCanvas(string name, Camera viewer, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) root.layer = uiLayer;
        root.transform.SetParent(parent, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer;
        return canvas;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string value, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        Place(rect, position, size);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value;
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.72f;
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static Button AddButton(RectTransform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject("Button " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        Place(rect, position, size);
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.07f, 0.27f, 0.39f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        AddText(rect, label, Vector2.zero, size - new Vector2(24, 12), 25, TextAlignmentOptions.Center);
        return button;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
}
