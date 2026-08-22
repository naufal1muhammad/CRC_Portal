using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CRC.Data.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRC.Api.Infrastructure
{
    // =================================================================================================
    // 🔴 THE ONLY THING GUARDING THE AGENT API.
    //
    // AgentApiController carries [AllowAnonymous], which switches off the GLOBAL AuthorizeFilter that
    // every other action in nucentra relies on (CoreFlow.md §2.2). This class is what replaces it. If it
    // is removed from the controller, mis-registered in Program.cs, or ever made to fail open, the
    // endpoints behind it become an unauthenticated read of patient data. Nothing else would notice.
    //
    // IT IS AN AUTHORIZATION FILTER, NOT AN ACTION FILTER, AND THAT IS THE POINT. IAsyncAuthorizationFilter
    // runs in MVC's FIRST filter stage — before model binding, before every action filter, and before a
    // line of the action itself. So a request with a bad key never reaches business code, never binds a
    // parameter, and (see step iv) never causes a database query. An action filter would run after model
    // binding and would already have let the framework do work on an anonymous caller's behalf.
    //
    // It is registered `AddScoped<AgentApiKeyFilter>()` and reached through [ServiceFilter], because it
    // constructor-injects IDatabaseData — a scoped service. A filter attribute cannot take one.
    // =================================================================================================
    public sealed class AgentApiKeyFilter : IAsyncAuthorizationFilter
    {
        /// <summary>The header an agent request presents its shared key in.</summary>
        public const string HeaderName = "X-Agent-Key";

        /// <summary>
        /// The authentication type stamped on the ClaimsIdentity this filter builds. A ClaimsIdentity
        /// with a null authentication type reports <c>IsAuthenticated == false</c>, which would leave the
        /// request looking anonymous to anything downstream that asks; naming it makes the principal a
        /// real one. It also labels the identity for a reader: this caller signed in with a key, not a
        /// cookie.
        /// </summary>
        public const string AuthenticationType = "AgentApiKey";

        private readonly AgentApiOptions _options;
        private readonly IDatabaseData _data;
        private readonly ILogger<AgentApiKeyFilter> _logger;

        public AgentApiKeyFilter(
            IOptions<AgentApiOptions> options,
            IDatabaseData data,
            ILogger<AgentApiKeyFilter> logger)
        {
            _options = options.Value;
            _data = data;
            _logger = logger;
        }

        // 🔴 THE ORDER OF THE STEPS BELOW IS THE SECURITY PROPERTY, NOT A STYLE. Do not rearrange them.
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var endpoint = $"{httpContext.Request.Method} {httpContext.Request.Path.Value}";

            // ---- i. Is a key configured at all? ---------------------------------------------------
            // 🔴 THE REFUSAL CONDITION IS WRITTEN ONCE, AS ONE EXPRESSION, AND IT MUST MEAN "THERE IS NOT
            // A SINGLE USABLE KEY". Agent:ApiKey is a string[] (see AgentApiOptions), and THREE different
            // inputs are all "not configured" — all three must answer 401:
            //
            //     • the array is NULL          (the "Agent" section is absent, or nothing bound to it —
            //                                   including a SCALAR Agent__ApiKey app setting, which binds
            //                                   to no element of an array property)
            //     • the array has NO MEMBERS   ("ApiKey": [])
            //     • every member is BLANK      ("ApiKey": [""] — the case a naive .Length == 0 check lets
            //                                   straight through, which would be an OPEN API)
            //
            // Filtering the blanks out first collapses all three into one Length == 0 test, and the array
            // that survives is the one step iii compares against — so a blank member can never be matched
            // by a caller sending a blank header either. This is the property that is easiest to lose when
            // a scalar becomes a collection, and losing it is the whole surface.
            //
            // Fail CLOSED. An empty Agent:ApiKey never means "no key required"; it means the app setting is
            // missing or misspelled, and an API whose key is the empty string would be an open API. The
            // caller correctly sees an undifferentiated 401, but this is a MISCONFIGURATION of the portal
            // rather than a caller error, so it is also logged as an error on the operational channel where
            // an operator will see it — otherwise a mistyped app setting looks exactly like an agent using
            // a stale key.
            var configuredKeys = (_options.ApiKey ?? Array.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToArray();

            if (configuredKeys.Length == 0)
            {
                // 🔴 THIS MESSAGE NAMES THE INDEXED FORM ON PURPOSE. Agent__ApiKey — the scalar an operator
                // would otherwise copy from this line — binds to NOTHING now that the property is an array,
                // so naming it here would send them to add exactly the setting that does not work. This log
                // line is the only thing separating "the portal is misconfigured" from "the caller's key is
                // wrong": the wire says the same Unauthorized. to both (§13.3, §13.6). Its text is
                // load-bearing.
                _logger.LogError(
                    "Agent API is not configured: Agent:ApiKey holds no usable key (the array is absent, empty, or " +
                    "every member is blank), so every request to {Endpoint} is refused. In Azure the value is the App " +
                    "Service app setting Agent__ApiKey__0 — TWO underscores between every segment, and the __0 index " +
                    "is required because the setting is an ARRAY; a scalar Agent__ApiKey binds to nothing. Add " +
                    "Agent__ApiKey__1 for a second key during a rotation. Locally it is the Agent:ApiKey array in " +
                    "appsettings.Development.json.",
                    endpoint);

                AgentAuditLog.AgentRequestRejected(httpContext, endpoint, "not configured");
                context.Result = Unauthorized();
                return;
            }

            // ---- ii. Did the caller present one? --------------------------------------------------
            if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var supplied)
                || string.IsNullOrEmpty(supplied.ToString()))
            {
                AgentAuditLog.AgentRequestRejected(httpContext, endpoint, "missing key");
                context.Result = Unauthorized();
                return;
            }

            // ---- iii. Does it match ANY configured key? -------------------------------------------
            // Every usable key is compared, and each comparison is the hash-then-fixed-time one below.
            //
            // 🔴 THE RESULT IS ACCUMULATED ACROSS THE LOOP — there is deliberately no `return true` on the
            // first match, and `|=` is used rather than `||=` precisely because it does NOT short-circuit.
            // Being honest about why: an early exit leaks WHICH key matched through timing, and a caller
            // holding a valid key already knows which one they hold, so the practical risk is close to nil.
            // It is written this way because it costs one line, it keeps every request through this filter
            // doing identical work, and it removes the need to have this argument again the next time
            // somebody reads it. Do not "optimise" it: the loop runs over one or two keys, once per
            // request, against a caller whose entire traffic is a daily sweep plus a conversation.
            var suppliedKey = supplied.ToString();
            var matched = false;

            foreach (var configuredKey in configuredKeys)
            {
                matched |= KeysMatch(configuredKey, suppliedKey);
            }

            if (!matched)
            {
                AgentAuditLog.AgentRequestRejected(httpContext, endpoint, "invalid key");
                context.Result = Unauthorized();
                return;
            }

            // ---- iv. ONLY NOW does the portal touch the database. ---------------------------------
            // 🔴 An unauthenticated caller must never be able to make nucentra run a query. Every step
            // above answers out of the request and the options object alone, so an unauthenticated
            // request costs a header read and a hash — no connection, no round trip, and nothing an
            // attacker can turn into load by sending a lot of them. This is why the service-account
            // lookup is HERE and not at the top of the method where it would read more naturally.
            var account = await _data.GetAgentServiceAccountAsync();

            // ---- v. No service account: refuse, loudly, and DO NOT CONTINUE. ----------------------
            // 🔴 There is no fall-through here and there must never be one. Not a zero, not a default,
            // not "carry on without an actor". A missing row means the database was published without
            // the seed, and continuing would mean every write this request made was audited as
            // AuditTrails.User_Id = 0 — nobody — with no error and no failed request (CoreFlow.md §0.1).
            // The two outcomes on offer are "fail loudly now" and "corrupt the audit trail quietly
            // forever". 503 rather than 401: the caller's credentials were fine, the portal is not.
            if (account == null)
            {
                _logger.LogError(
                    "Agent API cannot serve {Endpoint}: no dbo.Users row with Username = 'AGENT_SERVICE'. " +
                    "The database was published without the seed in CRC.Database/Scripts/Seed_Users.sql, or the row " +
                    "was renamed or deleted. Refusing with 503 rather than writing an appointment audited as user 0.",
                    endpoint);

                AgentAuditLog.AgentServiceAccountMissing(httpContext, endpoint);
                context.Result = ServiceUnavailable();
                return;
            }

            // ---- vi. Build the principal the rest of the request runs as. -------------------------
            var claims = new List<Claim>
            {
                // 🔴 THIS IS THE CLAIM THE AUDIT TRAIL TURNS ON. DatabaseHelper.CurrentUserId reads
                // exactly ClaimTypes.NameIdentifier off HttpContext.User, and SqlData passes what it
                // finds as @User_ID to the 19 audit-actor procedures. It must be a PLAIN INTEGER STRING —
                // CurrentUserId does an int.TryParse and answers null on anything else, which puts the
                // request straight back into the failure this whole class exists to prevent. Hence
                // InvariantCulture: a culture-specific format could introduce a group separator.
                //
                // The deleted CRC.Api (commit 291ab458) set this claim to the literal "0", with a comment
                // saying it "keeps DatabaseHelper happy". It does not. `0` IS the failure: it is the
                // value spPatientAppointment_Insert's ISNULL(@User_ID, 0) writes when no actor was
                // supplied at all, so every write would have been audited as nobody, successfully and
                // silently. See CoreFlow.md §0.1 and §13.3.
                new Claim(ClaimTypes.NameIdentifier, account.User_ID.ToString(CultureInfo.InvariantCulture)),

                // The name that appears wherever the portal shows an actor by name. AGENT_SERVICE, per
                // the seeded row — the username is the contract, not the id (§13.3).
                new Claim(ClaimTypes.Name, account.Username),

                // The claim nucentra's five authorization policies check (§2.1). Taken from the row
                // rather than hard-coded, so changing the seeded account's User_Type changes what the
                // agent may reach, in one place. Nothing on this controller uses a policy today.
                new Claim("UserType", account.User_Type.ToString(CultureInfo.InvariantCulture))
            };

            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));

            // ---- vii. Record who this request is, in the message text. ----------------------------
            // The audit line names the resolved user id explicitly. It has to: Serilog's [User:…] field
            // was filled by CorrelationIdMiddleware before this filter ran, and still reads "anonymous".
            // See AgentAuditLog's header and CoreFlow.md §13.3.
            AgentAuditLog.AgentRequestAuthenticated(httpContext, endpoint, account.User_ID);
        }

        // 🔴 FIXED-TIME COMPARISON, AND IT HASHES FIRST. DO NOT "SIMPLIFY" EITHER HALF.
        //
        // `==` on two strings returns as soon as it finds a differing character, so how long it takes to
        // say "no" depends on how many leading characters were right. Given enough requests that is a
        // measurable oracle, and an attacker can walk a key out of it one character at a time.
        // CryptographicOperations.FixedTimeEquals always reads both operands to the end.
        //
        // The SHA-256 is not paranoia about the key's contents — it is what makes the fixed-time
        // comparison honest. FixedTimeEquals requires two spans of EQUAL LENGTH and returns false
        // immediately when they differ, so passing the raw UTF-8 bytes would leak the configured key's
        // LENGTH through exactly the timing channel the call was chosen to close (and would need a length
        // check first, which leaks it outright). Hashing maps both values to 32 bytes whatever they were,
        // so every comparison does identical work and the answer depends on nothing but equality.
        //
        // Called ONCE PER CONFIGURED KEY by step iii, which accumulates the results rather than returning
        // on the first match — both halves below are unchanged by that, and both are still required.
        private static bool KeysMatch(string configured, string supplied)
        {
            Span<byte> configuredHash = stackalloc byte[32];
            Span<byte> suppliedHash = stackalloc byte[32];

            SHA256.HashData(Encoding.UTF8.GetBytes(configured), configuredHash);
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied), suppliedHash);

            return CryptographicOperations.FixedTimeEquals(configuredHash, suppliedHash);
        }

        // 🔴 THE THREE 401 PATHS ANSWER IDENTICALLY, ON PURPOSE. "Not configured", "missing key" and
        // "invalid key" are three different facts about the portal and about the caller, and telling an
        // unauthenticated caller which one applies tells them whether the key they hold is wrong or the
        // server is broken — free reconnaissance. The distinction is written to the log, where an
        // operator can read it, and never onto the wire. The body carries no key, no fragment of one, and
        // no exception message; it is JSON in the house shape so that a caller parsing every response as
        // JSON does not fall over on the error path.
        private static ObjectResult Unauthorized() =>
            new(new { success = false, message = "Unauthorized." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };

        private static ObjectResult ServiceUnavailable() =>
            new(new { success = false, message = "The service is temporarily unavailable." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
    }
}
