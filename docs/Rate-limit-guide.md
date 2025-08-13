# Rate Limiting Documentation

## Overiview
The Rate Limiting used in this project is implemented from scratch as middleware, using the sql database as a means to synchronise rate limiting information across nodes running in parallel when horizontally scaled. The implementation tries to follow the genral concepts of the dotnet rate limiter implementation, so that it may be used in the future if support for horizontal scaling is added to the package.

The rate limit policy is based on the loacl authority id provided in the scopes when authenticating, meaning that limits are applied per authority rather than per IP, as some authorities use multiple IPs and some IPs serve multiple authorities. Multiple policies can be configured, in Program.cs, and each specifies PartitionName, WindowLength, and PermitLimit.

The rateLimiters are currently applied in sequence in the middleware, meaning that a request must pass all applicable rate limit policies to be able to be accepted by the API. A request that fails to meet the rate limiting policy will return a 429 response, with a `retry-after` header that specifies the WindowLength of the policy on which it failed. It may be possible that a request would have been rejected by multiple rate limit policies, but only the first policy that rejects it will determine the response headers.

## Implementation details
### Policy parameters
`PartitionName` is the name of the policy, and is used in conjunction with the local authority id to track events that apply to the particular rate limiter and client. It should have a descriptive name of what the policy is checking for
`WindowLength` is the size of the timeframe over which to check for requests that have been received when determining whether the rate limit has been exceeded.
`PermitLimit` is the maximum number of permits that can be used by requests for the policy over the given windowLength. (Note: In this implementation of rate limiting, a request can use multiple permits)

### Storage and validation
Each request that matches a policy is written to the RateLimitEvents table, regardless of the outcome. Each evet contains the PartitionName (When combined with the local authority id), the timestamp of the event, the size of the query (E.g. the number of checks being submitted) , and whether the event was accepted or rejected by the limiter. Each request will have a corresponding event in the table for each rate limit policy which checks it.

When an event is received, the RateLimitEvents table is queried to find the sum of all the qurySizes from events in the same partition withing the WindowLength. If the result + the query size of the request currently being processed is less than or equal to the PermitLimit, then the request is allowed to continue processing. Otherwise a 429 response is returned.

### Middleware ordering
Typically ratelimiting solutions would occurr before request authorisation so that it can mitigate the risk of malicious use e.g. DoS attacks. In this implementation, the rate limiting occurs after the authentication step because the rate limiter uses the authentication context to determine which partition to separate requests into i.e. it uses the local authority id from the scopes supplied during authentication. There is a suitable rate limiting policiy in Azure that mitigates the risk of DoS attacks instead.

### Possible future extensions
- Determine retry period via a db query to find when the window has capcaity to handle the request
- Integrate with the dotnet rate limit framework, using the table that has been created to synchornise across horizontally scaled nodes
- Create builder methods for ratelimiters so that all rate limiters can be set-up and configured from the definitions in the appsettings