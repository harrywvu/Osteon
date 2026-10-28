$ErrorActionPreference = 'Stop'
$accountSource = Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/LearnerAccounts.cs') -Raw
$accountChecks = @'
public static class AccountChecks
{
    private static int count;
    private static void Check(bool value, string description)
    { if (!value) throw new System.Exception(description); count++; }
    private static void Fails(System.Action action, int status)
    {
        try { action(); } catch (AccountFailure error) { Check(error.Status == status, "Wrong failure status"); return; }
        throw new System.Exception("Expected failure " + status);
    }
    public static int Run()
    {
        var now = System.DateTimeOffset.UtcNow;
        var codes = new System.Collections.Generic.Queue<string>(new [] { "000123", "000123", "345678", "456789" });
        var service = new SimulatedLearnerAccounts(() => now, () => codes.Dequeue());
        string request = System.Guid.NewGuid().ToString();
        service.LoseNextCreationResponse = true;
        Fails(() => service.Create(request), 503);
        var a = service.Create(request).Result;
        Check(a.AccessCode == "000123", "Leading zero lost");
        Check(service.Create(request).Result.Login.LearnerId == a.Login.LearnerId, "Creation replay duplicated profile");
        var b = service.Create(System.Guid.NewGuid().ToString()).Result;
        Check(b.AccessCode == "345678", "Code collision not retried");
        Check(a.Login.LearnerId != b.Login.LearnerId, "Account identities collide");
        var a2 = service.Login(a.AccessCode).Result;
        Check(a2.LearnerId == a.Login.LearnerId && a2.AccessToken != a.Login.AccessToken, "Device login identity/token wrong");
        service.Logout(a.Login.AccessToken).Wait();
        service.Logout(a.Login.AccessToken).Wait();
        Fails(() => service.Current(a.Login.AccessToken), 401);
        Check(service.Current(a2.AccessToken).Result.LearnerId == a.Login.LearnerId, "Logout revoked other device");
        Check(service.Current(b.Login.AccessToken).Result.LearnerId == b.Login.LearnerId, "Logout changed account B");
        Fails(() => service.Login("123"), 400);
        Fails(() => service.Login("１２３４５６"), 400);
        Fails(() => service.Create("bad request"), 400);
        for (int i = 0; i < 4; i++) Fails(() => service.Login("999999"), 401);
        Fails(() => service.Login("888888"), 429);
        Fails(() => service.Login(a.AccessCode), 429);
        now = now.AddSeconds(61);
        Check(service.Login(a.AccessCode).Result.LearnerId == a.Login.LearnerId, "Throttle did not expire");
        service.Disable(b.AccessCode);
        Fails(() => service.Login(b.AccessCode), 401);
        Fails(() => service.Current(b.Login.AccessToken), 401);
        service.Unavailable = true;
        Fails(() => service.Create(request), 503);
        Fails(() => service.Login(a.AccessCode), 503);
        service.Unavailable = false;
        service.HangNextRequest = true;
        Check(!service.Login(a.AccessCode).IsCompleted, "Timeout fixture completed");
        var state = new LearnerAccountState(() => now);
        int generation = state.Generation;
        Check(state.Accept(generation, a2), "Valid login rejected");
        state.CancelPending();
        Check(!state.Accept(generation, b.Login), "Cancelled response replaced login");
        state.Clear();
        Check(state.Login == null && !state.Accept(generation, a2), "Late response resurrected signed-out login");
        Check(state.Accept(state.Generation, a2), "Relogin rejected");
        int events = 0;
        state.Changed += () => events++;
        now = now.AddHours(2);
        state.Expire();
        Check(state.Login == null && events == 1, "Session expiry did not clear exactly once");
        Fails(() => service.Current(a2.AccessToken), 401);
        Check(!state.Accept(state.Generation, a2), "Expired login accepted");
        Check(service.Login(a.AccessCode).Result.LearnerId == a.Login.LearnerId, "Account did not survive session expiry");
        var restarted = new SimulatedLearnerAccounts(() => now);
        Fails(() => restarted.Login(a.AccessCode), 401);
        now = now.AddDays(2);
        Check(service.Create(request).Result.Login.LearnerId != a.Login.LearnerId, "Expired creation record replayed");
        return count;
    }
}
'@
Add-Type -TypeDefinition ($accountSource + "`n" + $accountChecks)
$assertions = [AccountChecks]::Run()
Write-Output "Learner accounts: $assertions assertions passed."
