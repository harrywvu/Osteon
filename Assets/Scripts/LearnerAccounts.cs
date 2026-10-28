using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// Plain C# contract: the simulator never supplies credentials to the quiz HTTP client.
public sealed class AccountLogin
{
    public string LearnerId { get; private set; }
    public string ProfileLabel { get; private set; }
    public string AccessToken { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public AccountLogin(string id, string label, string token, DateTimeOffset expiry)
    { LearnerId = id; ProfileLabel = label; AccessToken = token; ExpiresAt = expiry; }
}

public sealed class AccountCreated
{
    public AccountLogin Login { get; private set; }
    public string AccessCode { get; private set; }
    public AccountCreated(AccountLogin login, string code) { Login = login; AccessCode = code; }
}

public sealed class AccountFailure : Exception
{
    public int Status { get; private set; }
    public int RetryAfterSeconds { get; private set; }
    public AccountFailure(int status, int retry = 0) : base("Account operation failed")
    { Status = status; RetryAfterSeconds = retry; }
}

public interface ILearnerAccountService
{
    Task<AccountCreated> Create(string requestId);
    Task<AccountLogin> Login(string code);
    Task<AccountLogin> Current(string token);
    Task Logout(string token);
}

public sealed class LearnerAccountState
{
    private readonly Func<DateTimeOffset> clock;
    private AccountLogin login;
    public int Generation { get; private set; }
    public event Action Changed;
    public LearnerAccountState(Func<DateTimeOffset> clock = null)
    { this.clock = clock ?? (() => DateTimeOffset.UtcNow); }
    public AccountLogin Login { get { Expire(); return login; } }
    public bool Accept(int generation, AccountLogin value)
    {
        if (generation != Generation || value == null || value.ExpiresAt <= clock()) return false;
        login = value;
        Changed?.Invoke();
        return true;
    }
    public void CancelPending() { Generation++; }
    public void Clear() { Generation++; login = null; Changed?.Invoke(); }
    public void Expire() { if (login != null && login.ExpiresAt <= clock()) Clear(); }
}

/// <summary>Development fixture only. Accounts and retry records reset when the process exits.</summary>
public sealed class SimulatedLearnerAccounts : ILearnerAccountService
{
    private sealed class Profile { public string Id; public string Label; public bool Disabled; }
    private sealed class Creation { public AccountCreated Result; public DateTimeOffset Until; }
    private readonly Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
    private readonly Dictionary<string, AccountLogin> sessions = new Dictionary<string, AccountLogin>();
    private readonly Dictionary<string, Creation> creations = new Dictionary<string, Creation>();
    private readonly Func<DateTimeOffset> clock;
    private readonly Func<string> codeGenerator;
    private readonly byte[] key = new byte[32];
    private DateTimeOffset blockedUntil;
    private int failures;
    public bool Unavailable { get; set; }
    public bool LoseNextCreationResponse { get; set; }
    public bool HangNextRequest { get; set; }

    public SimulatedLearnerAccounts(Func<DateTimeOffset> clock = null, Func<string> codeGenerator = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.codeGenerator = codeGenerator ?? RandomCode;
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
    }
    public static bool ValidCode(string code)
    {
        if (code == null || code.Length != 6) return false;
        foreach (char c in code) if (c < '0' || c > '9') return false;
        return true;
    }
    private static string RandomCode()
    {
        var bytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
        {
            uint value;
            do { rng.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); }
            while (value >= 4294000000u);
            return (value % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    private string Digest(string value)
    {
        using (var hmac = new HMACSHA256(key))
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
    private bool CheckService()
    {
        if (Unavailable) throw new AccountFailure(503);
        if (!HangNextRequest) return false;
        HangNextRequest = false;
        return true;
    }
    private void Throttle()
    {
        if (blockedUntil > clock()) throw new AccountFailure(429, (int)Math.Ceiling((blockedUntil - clock()).TotalSeconds));
    }
    private AccountLogin Issue(Profile profile)
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        var login = new AccountLogin(profile.Id, profile.Label, Convert.ToBase64String(bytes), clock().AddHours(1));
        sessions.Add(Digest(login.AccessToken), login);
        return login;
    }
    public Task<AccountCreated> Create(string requestId)
    {
        if (CheckService()) return new TaskCompletionSource<AccountCreated>().Task;
        Throttle();
        Guid parsed;
        if (!Guid.TryParse(requestId, out parsed)) throw new AccountFailure(400);
        // Expire code-bearing retry records, including requests that are never replayed.
        var expired = new List<string>();
        foreach (var entry in creations) if (entry.Value.Until <= clock()) expired.Add(entry.Key);
        foreach (string entry in expired) creations.Remove(entry);
        Creation previous;
        if (creations.TryGetValue(requestId, out previous)) return Task.FromResult(previous.Result);
        if (profiles.Count >= 10000) throw new AccountFailure(429, 60);
        string code = null;
        for (int i = 0; i < 100; i++)
        {
            string candidate = codeGenerator();
            if (!ValidCode(candidate)) throw new AccountFailure(503);
            if (!profiles.ContainsKey(Digest(candidate))) { code = candidate; break; }
        }
        if (code == null) throw new AccountFailure(503);
        string id = Guid.NewGuid().ToString("N");
        var profile = new Profile { Id = id, Label = "Learner " + id.Substring(0, 6) };
        profiles.Add(Digest(code), profile);
        var result = new AccountCreated(Issue(profile), code);
        creations.Add(requestId, new Creation { Result = result, Until = clock().AddHours(24) });
        if (LoseNextCreationResponse) { LoseNextCreationResponse = false; throw new AccountFailure(503); }
        return Task.FromResult(result);
    }
    public Task<AccountLogin> Login(string code)
    {
        if (CheckService()) return new TaskCompletionSource<AccountLogin>().Task;
        Throttle();
        if (!ValidCode(code)) throw new AccountFailure(400);
        Profile profile;
        if (!profiles.TryGetValue(Digest(code), out profile) || profile.Disabled)
        {
            if (++failures >= 5) { failures = 0; blockedUntil = clock().AddSeconds(60); throw new AccountFailure(429, 60); }
            throw new AccountFailure(401);
        }
        // Count guesses across identities, even when successful logins are interspersed.
        return Task.FromResult(Issue(profile));
    }
    public Task<AccountLogin> Current(string token)
    {
        if (CheckService()) return new TaskCompletionSource<AccountLogin>().Task;
        AccountLogin login;
        if (string.IsNullOrEmpty(token) || !sessions.TryGetValue(Digest(token), out login) || login.ExpiresAt <= clock())
            throw new AccountFailure(401);
        foreach (var profile in profiles.Values)
            if (profile.Id == login.LearnerId && profile.Disabled) throw new AccountFailure(401);
        return Task.FromResult(login);
    }
    public Task Logout(string token)
    {
        if (CheckService()) return new TaskCompletionSource<bool>().Task;
        if (!string.IsNullOrEmpty(token)) sessions.Remove(Digest(token));
        return Task.FromResult(true);
    }
    public void Disable(string code) { profiles[Digest(code)].Disabled = true; }
}
