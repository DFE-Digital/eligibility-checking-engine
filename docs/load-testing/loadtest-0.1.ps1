<#
.SYNOPSIS
    A PowerShell script to perform load testing on the API, firing concurrent POST requests and  polling for results.
.DESCRIPTION
    This script allows you to specify a set of API endpoints and configurations for load testing. It can fire concurrent POST requests to the specified endpoints, measure the time taken for each batch of requests, and optionally poll for results if the API supports it. The script is designed to be flexible and can be easily modified to test different endpoints or configurations.
.PARAMETER Username
    The client ID to use for authentication when requesting a token.
.PARAMETER Password
    The client secret to use for authentication when requesting a token.
.PARAMETER Root
    The root URL of the API to test
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Username = "",

    [Parameter()]
    [string]$Password = "",

    [string]$Root = "",

    [switch]$IgnoreCertErrors
)

# Normalize Root to always end with /
if ($Root -notmatch "/$") { $Root += "/" }

# ---------------------------
# HTTP client setup
# ---------------------------
$handler = [System.Net.Http.HttpClientHandler]::new()

if ($IgnoreCertErrors) {
    # Ignore certificate errors (useful for localhost/dev only)
    $handler.ServerCertificateCustomValidationCallback = { param($sender,$cert,$chain,$errors) return $true }
}

$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(180)


$tests = [ordered]@{
    # "https://www.google.com/generate_204" = @{
    #     body     = ("whatever" * 10)
    #     token    = $false
    #     response = $false
    # }
    "/oauth2/token#" = @{
        body     = "client_id=$Username&client_secret=$Password&scope=bulk_check check free_school_meals local_authority"
        token    = $false
        response = $false
    }
    "/bulk-check/free-school-meals#1"  = @{ file = $true; requestCount = 1 }
    "/bulk-check/free-school-meals#2" = @{ file = $true; requestCount = 5000 }
  # "/bulk-check/free-school-meals#3" = @{ file = $true; requestCount = 5000 }
}

#Each number represents how many concurrent POST requests the script will fire for each test endpoint.
$concurrentPostRequests = @(1,5)

# Check response settings with default of true if not set
function Response-Check([hashtable]$settings, [string]$key) {
    if ($settings.ContainsKey($key)) {
        return ($settings[$key] -ne $false)
    }
    return $true
}
function Combine-Url([string]$root, [string]$url) {
    if ($url -match "^https://") { return $url }
    if ($url.StartsWith("/")) {
        return $root.TrimEnd("/") + $url
    }
    return $root.TrimEnd("/") + "/" + $url
}

function New-RandomNIN {
    $n = Get-Random -Minimum 100000 -Maximum 1000000
    return "AB{0}C" -f $n
}

function Build-RequestBody([hashtable]$settings) {
    if ($settings.ContainsKey('body') -and $null -ne $settings['body']) {
        return [string]$settings['body']
    }
    elseif ($settings.ContainsKey('file') -and $settings['file']) {
        $qty = [int]$settings['requestCount']
        $people = for ($i = 0; $i -lt $qty; $i++) {
            @{
                type                    = "FreeSchoolMeals"
                nationalInsuranceNumber  = (New-RandomNIN)
                lastName                = "Smith"
                dateOfBirth             = "2000-01-01"
            }
        }
        return (@{ data = $people } | ConvertTo-Json -Depth 10 -Compress)
    }
    else {
        $nin = New-RandomNIN
        # Keep it similar to the PHP literal JSON shape
        return @"
{
  "data": {
    "type": "FreeSchoolMeals",
    "NationalInsuranceNumber": "$nin",
    "lastName": "Smith",
    "dateOfBirth": "2000-01-01"
  }
}
"@
    }
}

function Send-PostAsync(
    [System.Net.Http.HttpClient]$client,
    [string]$uri,
    [string]$body,
    [string]$contentType,
    [string]$bearerToken,
    [bool]$addAuthHeader
) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $uri)

    $content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, $contentType)
    $req.Content = $content

    if ($addAuthHeader -and $bearerToken) {
        $req.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $bearerToken)
    }

    # Return the Task<HttpResponseMessage>
    return $client.SendAsync($req)
}

function Send-Get(
    [System.Net.Http.HttpClient]$client,
    [string]$uri,
    [string]$bearerToken
) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $uri)
    $req.Headers.Accept.ParseAdd("application/json")
    if ($bearerToken) {
        $req.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $bearerToken)
    }
    $resp = $client.SendAsync($req).GetAwaiter().GetResult()
    $text = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return $text
}

# ---------------------------
# Execution
# ---------------------------
$token = ""
$times = @{}

# $headerParts = @("URL")
# foreach ($q in $concurrentPostRequests) {
#     $headerParts += "$q"
#     $headerParts += "Complete"
# }
# Write-Host ($headerParts -join "`t")

# Loop over each test URL and concurrentPostRequests, firing concurrent requests and measuring time
foreach ($testUrl in $tests.Keys) {
    $settings = [hashtable]$tests[$testUrl]

    Write-Host -NoNewline "$testUrl" -ForegroundColor DarkYellow

    foreach ($requestCount in $concurrentPostRequests) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        $count = [int][Math]::Round([double]$requestCount)
        if ($count -lt 0) { $count = 0 }

        $fullUri = Combine-Url -root $Root -url $testUrl.Substring(0, $testUrl.IndexOf("#"))
        
        $useTokenHeader = (Response-Check $settings 'token')
        $shouldPoll     = (Response-Check $settings 'response')
          
        $contentType = if ($useTokenHeader) { "application/json" } else { "application/x-www-form-urlencoded" }

        # Fire $count concurrent POSTs
        $tasks = New-Object System.Collections.Generic.List[System.Threading.Tasks.Task[System.Net.Http.HttpResponseMessage]]

        Write-Host -NoNewline ("`tFiring {0} concurrent requests..." -f $count) -ForegroundColor Green
        
        for ($i = 0; $i -lt $count; $i++) {
            $body = Build-RequestBody $settings
            $tasks.Add((Send-PostAsync -client $client -uri $fullUri -body $body -contentType $contentType -bearerToken $token -addAuthHeader $useTokenHeader))
        }

        $responses = @()
        if ($tasks.Count -gt 0) {
            try {
                $responses = [System.Threading.Tasks.Task]::WhenAll($tasks.ToArray()).GetAwaiter().GetResult()
            }
            catch {
                Write-Warning "One or more requests failed for $fullUri : $($_.Exception.Message)"
                throw
            }
        }

        $last = $null

        foreach ($resp in $responses) {
            $text = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()

            try {
                $responseData = $text | ConvertFrom-Json -AsHashtable
                $last = $responseData
            } catch {
                $responseData = $null
                $last = $null
            }
           
            if ($settings.ContainsKey('token') -and $settings['token'] -eq $false -and $responseData -and $responseData.ContainsKey('access_token')) {
                $token = [string]$responseData['access_token']
            }
        }

        $stopwatch.Stop()
        $elapsed = $stopwatch.Elapsed.TotalSeconds

        if (-not $times.ContainsKey($testUrl)) { $times[$testUrl] = @{} }
        $times[$testUrl]["$requestCount"] = $elapsed

        Write-Host -NoNewline (" → Posted in `t{0:N2}`n" -f $elapsed)

        # Polling phase (if response !== false)
        if ($shouldPoll) {
            $answer = $null

            do {
                if (-not $last -or -not $last.ContainsKey('links')) {
                    $answer = "noLinks"
                    break
                }

                $links = $last['links']
                $linkPath =
                    if ($links.ContainsKey('get_EligibilityCheck') -and $links['get_EligibilityCheck']) { $links['get_EligibilityCheck'] }
                    elseif ($links.ContainsKey('get_Progress_Check') -and $links['get_Progress_Check']) { $links['get_Progress_Check'] }
                    else { $null }

                if (-not $linkPath) {
                    $answer = "noProgressLink"
                    break
                }

                $pollUri = Combine-Url -root $Root -url ([string]$linkPath)
                $pollText = Send-Get -client $client -uri $pollUri -bearerToken $token

                try {
                    $pollJson = $pollText | ConvertFrom-Json -AsHashtable
                    $data = $pollJson['data']
                } catch {
                    $data = $null
                }

                if (-not $data) {
                    Write-Warning "Polling returned non-JSON or missing data: $pollText" -ForegroundColor Yellow
                    $answer = "invalidPollResponse"
                    break
                }

                if (-not $settings.ContainsKey('file')) {
                    $answer = [string]$data['status']
                }

                else {
                    $complete = $data['complete']
                    $total    = $data['total']

                    if ($complete -lt $total) {
                        $answer = "queuedForProcessing"
                    }
                    else {
                        $answer = "$complete=$total"
                    }
                }

                # keep $last updated in case links/status change
                $last = $pollJson

            } while ($answer -eq "queuedForProcessing")

            $totalElapsed = ([System.TimeSpan]::FromTicks(([System.Diagnostics.Stopwatch]::GetTimestamp())) ) | Out-Null
        }
    }
}

Write-Host ($times | ConvertTo-Json -Depth 10)