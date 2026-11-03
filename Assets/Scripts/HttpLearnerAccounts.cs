using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Account-only HTTPS transport shared by Quest and WebGL. No embedded credentials.</summary>
public sealed class HttpLearnerAccounts : ILearnerAccountService
{
    public const string DefaultBaseUrl = "https://x0pgrcdyh1.execute-api.ap-southeast-2.amazonaws.com";
    private readonly MonoBehaviour runner;
    private readonly string baseUrl;
    [Serializable] private sealed class CreateBody { public string request_id; }
    [Serializable] private sealed class LoginBody { public string access_code; }
    [Serializable] private sealed class Reply
    {
        public string learner_id;
        public string profile_label;
        public string access_token;
        public string access_code;
        public int expires_in;
        public int retry_after_seconds;
        public bool signed_out;
    }

    public HttpLearnerAccounts(MonoBehaviour runner, string baseUrl = DefaultBaseUrl)
    {
        if (runner == null) throw new ArgumentNullException(nameof(runner));
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Account endpoint must be an HTTPS base URL.", nameof(baseUrl));
        this.runner = runner;
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    public Task<AccountCreated> Create(string requestId)
    {
        Guid parsed;
        if (!Guid.TryParse(requestId, out parsed)) throw new AccountFailure(400);
        var body = JsonUtility.ToJson(new CreateBody { request_id = requestId });
        return Send("/v1/auth/profiles", "POST", body, null, reply => {
            if (!SimulatedLearnerAccounts.ValidCode(reply.access_code)) throw new AccountFailure(503);
            return new AccountCreated(ToLogin(reply), reply.access_code);
        });
    }
    public Task<AccountLogin> Login(string code)
    {
        if (!SimulatedLearnerAccounts.ValidCode(code)) throw new AccountFailure(400);
        return Send("/v1/auth/login", "POST", JsonUtility.ToJson(new LoginBody { access_code = code }), null, ToLogin);
    }
    public Task<AccountLogin> Current(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new AccountFailure(401);
        return Send("/v1/auth/me", "GET", null, token, reply => {
            reply.access_token = token;
            return ToLogin(reply);
        });
    }
    public Task Logout(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult(true);
        return Send("/v1/auth/logout", "POST", "{}", token, reply => {
            if (!reply.signed_out) throw new AccountFailure(503);
            return true;
        });
    }
    private static AccountLogin ToLogin(Reply reply)
    {
        if (string.IsNullOrWhiteSpace(reply.learner_id) || string.IsNullOrWhiteSpace(reply.profile_label) ||
            string.IsNullOrWhiteSpace(reply.access_token) || reply.expires_in < 0 || reply.expires_in > 86400)
            throw new AccountFailure(503);
        return new AccountLogin(reply.learner_id, reply.profile_label, reply.access_token,
            DateTimeOffset.UtcNow.AddSeconds(reply.expires_in));
    }
    private Task<T> Send<T>(string path, string method, string body, string token, Func<Reply, T> read)
    {
        var completion = new TaskCompletionSource<T>();
        runner.StartCoroutine(Request(path, method, body, token, read, completion));
        return completion.Task;
    }
    private IEnumerator Request<T>(string path, string method, string body, string token,
        Func<Reply, T> read, TaskCompletionSource<T> completion)
    {
        using (var request = new UnityWebRequest(baseUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (token != null) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 20;
            request.redirectLimit = 0;
            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (Exception) { completion.TrySetException(new AccountFailure(503)); }
            if (operation == null) yield break;
            yield return operation;
            try
            {
                Reply reply = null;
                try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    int status = request.responseCode > 0 ? (int)request.responseCode : 503;
                    throw new AccountFailure(status, reply != null ? Math.Max(0, reply.retry_after_seconds) : 0);
                }
                if (reply == null) throw new AccountFailure(503);
                completion.TrySetResult(read(reply));
            }
            catch (AccountFailure error) { completion.TrySetException(error); }
            catch (Exception) { completion.TrySetException(new AccountFailure(503)); }
        }
    }
}
