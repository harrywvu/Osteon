using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Account-only UI for the live service. No quiz or AI requests are issued here.</summary>
public sealed class LearnerAccountPanel : MonoBehaviour
{
    public static bool CapturesDesktopInput { get; private set; }
    public readonly LearnerAccountState State = new LearnerAccountState();
    private ILearnerAccountService service;
    private RectTransform root;
    private Transform station;
    private readonly List<GameObject> controls = new List<GameObject>();
    private TextMeshProUGUI message;
    private TextMeshProUGUI digits;
    private Button submit;
    private Action close;
    private string code = "";
    private string creationId;
    private AccountCreated issued;
    private string screen;
    private bool busy;
    private bool reveal;
    private float retryUntil;
    private Keyboard textKeyboard;
    private ProgramOption[] programs = new ProgramOption[0];
    private LearnerProfile draftDetails = new LearnerProfile();
    private TMP_InputField firstInput, middleInput, lastInput, programSearch;
    private Button programButton, yearButton;
    private int programIndex, yearLevel = 1;
    private bool creatingProfile;
    public SimulatedLearnerAccounts Simulation { get { return service as SimulatedLearnerAccounts; } }

    [ContextMenu("Simulation/Toggle service unavailable")]
    private void ToggleUnavailable() { if (Simulation != null) Simulation.Unavailable = !Simulation.Unavailable; }
    [ContextMenu("Simulation/Lose next creation response")]
    private void LoseCreation() { if (Simulation != null) Simulation.LoseNextCreationResponse = true; }
    [ContextMenu("Simulation/Timeout next request")]
    private void TimeoutNext() { if (Simulation != null) Simulation.HangNextRequest = true; }

    [ContextMenu("Simulation/Use simulated accounts (development only)")]
    private void UseSimulation()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        State.Clear();
        issued = null;
        creationId = null;
        busy = false;
        service = new SimulatedLearnerAccounts();
        if (root != null && root.gameObject.activeSelf) Welcome();
    }

    private void OnEnable()
    {
        textKeyboard = Keyboard.current;
        if (textKeyboard != null) textKeyboard.onTextInput += TypeDigit;
    }
    private void OnDisable()
    {
        if (textKeyboard != null) textKeyboard.onTextInput -= TypeDigit;
        CapturesDesktopInput = false;
    }
    private void TypeDigit(char value)
    { if (root != null && root.gameObject.activeSelf && screen == "entry") AddDigit(value); }

    public void Initialize(Camera viewer, Transform stationRoot, Action onClose)
    {
        service = new HttpLearnerAccounts(this);
        station = stationRoot;
        close = onClose;
        Canvas canvas = OsteonQuizDemo.CreateCanvas("Account panel", viewer, station);
        root = (RectTransform)canvas.transform;
        root.localScale = Vector3.one * 0.0012f;
        root.sizeDelta = new Vector2(780, 950);
        root.localPosition = Vector3.zero;
        State.Changed += SessionChanged;
        root.gameObject.SetActive(false);
    }

    public void Open()
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        PanelMoveHandle.Configure(station, root, TMP_Settings.defaultFontAsset,
            new Vector2(0, -655), new Vector2(700, 58));
        CapturesDesktopInput = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (issued != null) ShowIssued();
        else if (State.Login != null)
        {
            string token = State.Login.AccessToken;
            Run(() => service.Current(token), login => {
                State.Accept(State.Generation, login);
                Profile();
            }, Open);
        }
        else Welcome();
    }
    private void SessionChanged()
    {
        if (State.Login == null && root != null && root.gameObject.activeSelf)
        {
            busy = false;
            issued = null;
            creationId = null;
            Entry("Your session ended. Enter your code again.");
        }
    }
    private void Update()
    {
        State.Expire();
        if (root == null || !root.gameObject.activeSelf) return;
        if (submit != null) submit.interactable = !busy && Time.unscaledTime >= retryUntil && code.Length == 6;
        if (retryUntil > Time.unscaledTime && message != null)
            message.text = "Too many attempts. Retry in " + Mathf.CeilToInt(retryUntil - Time.unscaledTime) + " seconds.";
        if (screen != "entry" || busy) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Login();
    }
    private void ClearView(string view, string text)
    {
        var removed = new HashSet<GameObject>();
        foreach (GameObject item in controls)
        {
            Transform outer = item.transform;
            while (outer.parent != root) outer = outer.parent;
            if (removed.Add(outer.gameObject))
            {
                OsteonSettings.Instance?.ReleaseDynamicText(outer);
                outer.gameObject.SetActive(false);
                Destroy(outer.gameObject);
            }
        }
        controls.Clear();
        submit = null;
        digits = null;
        reveal = false;
        screen = view;
        Text(Simulation != null ? "SIMULATED ACCOUNTS — DEVELOPMENT ONLY" : "OSTEON ACCOUNTS", 416, 24, 70);
        Text(Simulation != null ? "Resets on app close. No cross-device storage or quiz connection." :
            "Use your saved code on Quest or web to return to your account.", 350, 21, 65);
        message = Text(text, 220, 28, 170);
        Button("Explore anatomy", -490, Exit);
        Style();
    }
    private TextMeshProUGUI Text(string value, float y, float size, float height)
    {
        var label = OsteonQuizDemo.AddText(root, value, new Vector2(0, y), new Vector2(700, height), size, TextAlignmentOptions.Center);
        label.richText = false;
        controls.Add(label.gameObject);
        return label;
    }
    private Button Button(string value, float y, Action action, float x = 0, float width = 700)
    {
        var button = OsteonQuizDemo.AddButton(root, value, new Vector2(x, y), new Vector2(width, 66));
        button.onClick.AddListener(() => action());
        controls.Add(button.gameObject);
        return button;
    }
    private void Style()
    {
        OsteonSettings.Instance?.ApplyTheme();
        // Register newly created labels with the existing baseline-based size system.
        if (message != null && screen != "entry") OsteonSettings.Instance?.PrepareQuizText(message);
        else OsteonSettings.Instance?.PrepareQuizText();
    }
    private void Welcome()
    {
        ClearView("welcome", "Create a named learning profile or use your saved code.");
        Button("New learner", 40, () => { draftDetails = new LearnerProfile(); LoadPrograms(() => ProfileForm(true)); });
        Button("Enter code", -60, () => Entry());
        Button("Forgot your code?", -160, Forgotten);
        Style();
    }
    private void Entry(string text = "Enter your six-digit code")
    {
        code = "";
        ClearView("entry", text);
        // Entry copy needs less vertical room than the code-loss notice.
        message.rectTransform.anchoredPosition = new Vector2(0, 270);
        message.rectTransform.sizeDelta = new Vector2(700, 90);
        digits = Text("_ _ _ _ _ _", 190, 36, 60);
        for (int i = 1; i <= 9; i++)
        {
            string digit = i.ToString();
            Button(digit, 105 - ((i - 1) / 3) * 78, () => AddDigit(digit[0]), ((i - 1) % 3 - 1) * 230, 210);
        }
        Button("Clear", -129, () => { code = ""; RefreshDigits(); }, -230, 210);
        Button("0", -129, () => AddDigit('0'), 0, 210);
        Button("Backspace", -129, Backspace, 230, 210);
        var show = Button("Hold to reveal", -210, () => { });
        var trigger = show.gameObject.AddComponent<EventTrigger>();
        Hold(trigger, EventTriggerType.PointerDown, true);
        Hold(trigger, EventTriggerType.PointerUp, false);
        Hold(trigger, EventTriggerType.PointerExit, false);
        submit = Button("Continue", -295, Login);
        Button("Cancel", -380, Welcome);
        RefreshDigits();
        Style();
    }
    private void Hold(EventTrigger trigger, EventTriggerType type, bool value)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => { reveal = value; RefreshDigits(); });
        trigger.triggers.Add(entry);
    }
    private void RefreshDigits()
    {
        if (digits == null) return;
        var slots = new string[6];
        for (int i = 0; i < 6; i++) slots[i] = i >= code.Length ? "_" : reveal ? code[i].ToString() : "•";
        digits.text = string.Join(" ", slots);
    }
    private void AddDigit(char c)
    { if (!busy && c >= '0' && c <= '9' && code.Length < 6) { code += c; RefreshDigits(); } }
    private void Backspace()
    { if (!busy && code.Length > 0) { code = code.Substring(0, code.Length - 1); RefreshDigits(); } }
    private void Create()
    {
        if (busy || Time.unscaledTime < retryUntil) return;
        if (creationId == null) creationId = Guid.NewGuid().ToString();
        Run(() => service.Create(creationId, draftDetails), result => { issued = result; ShowIssued(); }, Create,
            () => { ProfileForm(true); message.text = "Check the profile fields and try again."; });
    }
    private void ShowIssued()
    {
        ClearView("issued", "Your code: " + issued.AccessCode + "\nSave this code. Without it, you cannot return to this account after signing out.");
        Button("I've saved my code", -40, () => {
            var login = issued.Login;
            issued = null;
            creationId = null;
            State.Accept(State.Generation, login);
            if (State.Login != null) Profile(); else Entry("Session expired. Enter your saved code.");
        });
        Style();
    }
    private void Login()
    {
        if (busy || code.Length != 6 || Time.unscaledTime < retryUntil) return;
        Run(() => service.Login(code), login => {
            code = "";
            creationId = null;
            State.Accept(State.Generation, login);
            if (State.Login != null) Profile(); else Entry("Session expired. Enter your code again.");
        }, () => Entry());
    }
    private void Profile()
    {
        var login = State.Login;
        if (login == null) { Entry(); return; }
        if (login.Profile == null || !login.Profile.Complete)
        {
            draftDetails = login.Profile ?? new LearnerProfile();
            LoadPrograms(() => ProfileForm(false));
            return;
        }
        ClearView("profile", "Signed in as " + login.Profile.first_name + " " + login.Profile.last_name);
        Button("Continue", 70, Exit);
        Button("Edit profile", -20, () => { draftDetails = login.Profile; LoadPrograms(() => ProfileForm(false)); });
        Button("Switch learner", -110, () => Confirm(true));
        Button("Sign out", -200, () => Confirm(false));
        Style();
    }

    private void LoadPrograms(Action ready)
    {
        Run(() => service.Programs(), values => {
            programs = values ?? new ProgramOption[0];
            if (programs.Length == 0) { ClearView("error", "Program catalog unavailable."); Button("Retry", -60, () => LoadPrograms(ready)); return; }
            ready();
        }, () => LoadPrograms(ready));
    }

    private TMP_InputField Input(string label, string value, float y)
    {
        Text(label, y + 31, 20, 34);
        var shell = new GameObject(label + " input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        shell.layer = root.gameObject.layer;
        shell.transform.SetParent(root, false);
        var rect = (RectTransform)shell.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700, 54);
        rect.anchoredPosition = new Vector2(0, y);
        var image = shell.GetComponent<Image>();
        image.color = Color.white;
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = shell.layer;
        text.transform.SetParent(shell.transform, false);
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 4); textRect.offsetMax = new Vector2(-12, -4);
        var display = text.GetComponent<TextMeshProUGUI>();
        display.font = TMP_Settings.defaultFontAsset;
        display.fontSize = 24; display.color = new Color(0.05f, 0.15f, 0.23f); display.richText = false;
        display.alignment = TextAlignmentOptions.MidlineLeft;
        display.raycastTarget = false;
        var input = shell.GetComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = rect; input.textComponent = display;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 80;
        input.text = value ?? "";
        controls.Add(shell);
        return input;
    }

    private void ProfileForm(bool create)
    {
        creatingProfile = create;
        ClearView("details", create ? "Create your learner profile" : "Complete or correct your learner profile");
        message.rectTransform.anchoredPosition = new Vector2(0, 295);
        message.rectTransform.sizeDelta = new Vector2(700, 55);
        firstInput = Input("First name", draftDetails.first_name, 205);
        middleInput = Input("Middle name (optional)", draftDetails.middle_name, 115);
        lastInput = Input("Last name", draftDetails.last_name, 25);
        programSearch = Input("Search MMSU program", "", -65);
        programIndex = Math.Max(0, Array.FindIndex(programs, item => item.id == draftDetails.program_id));
        yearLevel = draftDetails.year_level > 0 ? draftDetails.year_level : 1;
        programButton = Button("", -170, NextProgram);
        yearButton = Button("", -255, NextYear);
        programSearch.onValueChanged.AddListener(_ => FindProgram());
        RefreshProgram();
        Button(create ? "Create account" : "Save profile", -365, SaveProfile);
        Style();
    }

    private void FindProgram()
    {
        string term = programSearch.text.Trim();
        if (term.Length == 0) return;
        int found = Array.FindIndex(programs, item => item.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        if (found >= 0) { programIndex = found; yearLevel = 1; RefreshProgram(); }
    }
    private void NextProgram()
    {
        if (programs.Length == 0) return;
        string term = programSearch.text.Trim();
        for (int i = 1; i <= programs.Length; i++)
        {
            int next = (programIndex + i) % programs.Length;
            if (term.Length == 0 || programs[next].name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            { programIndex = next; yearLevel = 1; break; }
        }
        RefreshProgram();
    }
    private void NextYear()
    {
        int[] years = programs[programIndex].year_options;
        if (years == null || years.Length == 0) return;
        int at = Array.IndexOf(years, yearLevel);
        yearLevel = years[(at + 1) % years.Length];
        RefreshProgram();
    }
    private void RefreshProgram()
    {
        if (programs.Length == 0) return;
        ProgramOption chosen = programs[programIndex];
        programButton.GetComponentInChildren<TextMeshProUGUI>().text = chosen.name + "  (tap to change)";
        if (Array.IndexOf(chosen.year_options ?? new int[0], yearLevel) < 0) yearLevel = 1;
        yearButton.GetComponentInChildren<TextMeshProUGUI>().text = "Year " + yearLevel + "  (tap to change)";
    }
    private void SaveProfile()
    {
        if (busy || programs.Length == 0) return;
        draftDetails = new LearnerProfile { first_name = firstInput.text.Trim(), middle_name = middleInput.text.Trim(),
            last_name = lastInput.text.Trim(), program_id = programs[programIndex].id, year_level = yearLevel };
        if (!draftDetails.Complete) { message.text = "First name and last name are required."; return; }
        if (creatingProfile) { Create(); return; }
        var login = State.Login;
        if (login == null) { Entry(); return; }
        Run(() => service.UpdateProfile(login.AccessToken, draftDetails), updated => {
            State.Accept(State.Generation, updated);
            Profile();
        }, () => ProfileForm(false), () => { ProfileForm(false); message.text = "Check the profile fields and try again."; });
    }
    private void Confirm(bool switching)
    {
        ClearView("confirm", "End this device's login? Save your code before signing out.");
        Button(switching ? "Switch learner" : "Sign out", -20, () => {
            var login = State.Login;
            State.Clear();
            issued = null;
            creationId = null;
            code = "";
            if (login != null) StartCoroutine(Revoke(login.AccessToken));
            if (switching) Entry(); else Welcome();
        });
        Button("Cancel", -120, Profile);
        Style();
    }
    private IEnumerator Revoke(string token)
    {
        Task task = null;
        try { task = service.Logout(token); } catch (AccountFailure) { }
        float until = Time.unscaledTime + 20;
        while (task != null && !task.IsCompleted && Time.unscaledTime < until) yield return null;
        if (task != null && task.IsFaulted) { var ignored = task.Exception; }
    }
    private void Forgotten()
    {
        ClearView("forgotten", "Code recovery is unavailable. Without your code you must create a new account. Previous accounts cannot be restored or merged.");
        Button("New learner", -60, () => { draftDetails = new LearnerProfile(); LoadPrograms(() => ProfileForm(true)); });
        Button("Back", -160, Welcome);
        Style();
    }
    private void Run<T>(Func<Task<T>> operation, Action<T> success, Action retry, Action invalid = null)
    { if (!busy) StartCoroutine(Request(operation, success, retry, invalid)); }
    private IEnumerator Request<T>(Func<Task<T>> operation, Action<T> success, Action retry, Action invalid)
    {
        State.CancelPending();
        int generation = State.Generation;
        busy = true;
        ClearView("loading", Simulation != null ? "Connecting to simulated accounts..." : "Connecting to your account...");
        Task<T> task = null;
        Exception failure = null;
        try { task = operation(); } catch (Exception error) { failure = error; }
        float until = Time.unscaledTime + 20;
        while (task != null && !task.IsCompleted && State.Generation == generation && Time.unscaledTime < until) yield return null;
        if (State.Generation != generation) yield break;
        busy = false;
        if (task != null && task.IsFaulted) failure = task.Exception.GetBaseException();
        if (task != null && task.Status == TaskStatus.RanToCompletion) { success(task.Result); yield break; }
        var accountFailure = failure as AccountFailure;
        int status = accountFailure != null ? accountFailure.Status : 503;
        if (status == 400 && invalid != null) { invalid(); yield break; }
        if (status == 401 || status == 400)
        {
            State.Clear();
            Entry("Unable to use this code. Check it and try again.");
            yield break;
        }
        if (status == 429) retryUntil = Time.unscaledTime + Math.Max(1, accountFailure.RetryAfterSeconds);
        ClearView("error", status == 429 ? "Too many attempts." : "Account service unavailable. You can retry or explore anatomy.");
        var retryButton = Button("Retry", -60, () => { if (Time.unscaledTime >= retryUntil) retry(); });
        StartCoroutine(EnableRetry(retryButton));
        Style();
    }
    private IEnumerator EnableRetry(Button button)
    {
        while (button != null && Time.unscaledTime < retryUntil) { button.interactable = false; yield return null; }
        if (button != null) { button.interactable = true; message.text = "You can retry now."; }
    }
    private void Exit()
    {
        State.CancelPending();
        busy = false;
        code = "";
        root.gameObject.SetActive(false);
        CapturesDesktopInput = false;
        close();
    }
    private void OnDestroy()
    { State.Changed -= SessionChanged; State.Clear(); CapturesDesktopInput = false; }
}
