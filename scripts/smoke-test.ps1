#requires -Version 5.1
<#
.SYNOPSIS
    End-to-end smoke test for the running TimeBank API.

.DESCRIPTION
    Exercises health, auth, registration, the full request -> offer -> accept ->
    complete -> review flow, balances, statement (stored procedure), duplicate
    operations (409), role checks (403) and suspension with an old JWT (403).

    Start the API first:
        dotnet run --project src/TimeBank.API --urls http://localhost:5080
    Then:
        pwsh ./scripts/smoke-test.ps1

.NOTES
    Creates two new members with unique emails on every run. Safe for a dev database.
#>
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$DevPassword = 'Sa3a@2026'
)

$ErrorActionPreference = 'Stop'
$script:Failures = 0
$script:Checks = 0

Add-Type -AssemblyName System.Net.Http
$script:HttpClient = New-Object System.Net.Http.HttpClient
$script:HttpClient.Timeout = [TimeSpan]::FromSeconds(60)

function Assert-Equal {
    param($Actual, $Expected, [string]$Name)
    $script:Checks++

    $actualText = "$Actual"
    $expectedText = "$Expected"
    $isMatch = $actualText -eq $expectedText

    if (-not $isMatch) {
        # JSON decimals arrive as 2.00 while the test expects 2.
        $actualNumber = [decimal]0
        $expectedNumber = [decimal]0
        if ([decimal]::TryParse($actualText, [ref]$actualNumber) -and
            [decimal]::TryParse($expectedText, [ref]$expectedNumber)) {
            $isMatch = $actualNumber -eq $expectedNumber
        }
    }

    if ($isMatch) {
        Write-Host "  PASS  $Name ($Actual)" -ForegroundColor Green
    }
    else {
        $script:Failures++
        Write-Host "  FAIL  $Name (expected $Expected, got $Actual)" -ForegroundColor Red
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Name)
    $script:Checks++
    if ($Condition) {
        Write-Host "  PASS  $Name" -ForegroundColor Green
    }
    else {
        $script:Failures++
        Write-Host "  FAIL  $Name" -ForegroundColor Red
    }
}

function Invoke-Api {
    param(
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [string]$Path,
        $Body = $null,
        [string]$Token = $null
    )

    $request = New-Object System.Net.Http.HttpRequestMessage
    $request.Method = New-Object System.Net.Http.HttpMethod($Method)
    $request.RequestUri = New-Object Uri("$BaseUrl$Path")

    if ($Token) {
        $request.Headers.Authorization =
            New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
    }

    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $request.Content = New-Object System.Net.Http.StringContent(
            $json, [Text.Encoding]::UTF8, 'application/json')
    }

    $response = $script:HttpClient.SendAsync($request).GetAwaiter().GetResult()
    $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    $parsed = $null
    if ($raw) {
        try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $raw }
    }

    [pscustomobject]@{
        Status = [int]$response.StatusCode
        Body   = $parsed
        Raw    = $raw
    }
}

function Login {
    param([string]$Email, [string]$Password = $DevPassword)
    Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $Email; password = $Password }
}

Write-Host "`n== Health and public endpoints ==" -ForegroundColor Cyan
$health = Invoke-Api -Method GET -Path '/health'
Assert-Equal $health.Status 200 'GET /health'
Assert-True ($health.Raw -match 'Healthy') 'health body is Healthy'

$stats = Invoke-Api -Method GET -Path '/api/stats'
Assert-Equal $stats.Status 200 'GET /api/stats'
Assert-True ($stats.Body.members -ge 10) 'stats reports seeded members'

$categories = Invoke-Api -Method GET -Path '/api/categories'
Assert-Equal $categories.Status 200 'GET /api/categories'
Assert-True ($categories.Body.Count -ge 10) 'categories include seeded list'

$requests = Invoke-Api -Method GET -Path '/api/requests?status=Open'
Assert-Equal $requests.Status 200 'GET /api/requests?status=Open'

$missing = Invoke-Api -Method GET -Path '/api/requests/999999'
Assert-Equal $missing.Status 404 'unknown request -> 404'

Write-Host "`n== Moderator login and role check ==" -ForegroundColor Cyan
$moderatorLogin = Login 'mona.adel@sa3a.test'
Assert-Equal $moderatorLogin.Status 200 'moderator login'
$moderatorToken = $moderatorLogin.Body.token
$moderatorId = $moderatorLogin.Body.member.id
Assert-True ($null -ne $moderatorToken) 'moderator JWT issued'

Write-Host "`n== Registration and welcome hours (BR-02) ==" -ForegroundColor Cyan
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$emailA = "smoke.requester.$suffix@sa3a.test"
$emailB = "smoke.helper.$suffix@sa3a.test"

$registerA = Invoke-Api -Method POST -Path '/api/auth/register' -Body @{
    fullName = 'Smoke Requester'
    email    = $emailA
    password = 'Smoke@2026!'
    district = 'Nasr City'
    bio      = 'Created by smoke-test.ps1'
    skills   = @(@{ skillId = 6; level = 'Intermediate' })
}
Assert-Equal $registerA.Status 201 'register requester -> 201'
$tokenA = $registerA.Body.token
$memberAId = $registerA.Body.member.id

$registerB = Invoke-Api -Method POST -Path '/api/auth/register' -Body @{
    fullName = 'Smoke Helper'
    email    = $emailB
    password = 'Smoke@2026!'
    district = 'Nasr City'
    bio      = 'Created by smoke-test.ps1'
    skills   = @(@{ skillId = 1; level = 'Expert' })
}
Assert-Equal $registerB.Status 201 'register helper -> 201'
$tokenB = $registerB.Body.token
$memberBId = $registerB.Body.member.id

$duplicateRegistration = Invoke-Api -Method POST -Path '/api/auth/register' -Body @{
    fullName = 'Smoke Requester'
    email    = $emailA
    password = 'Smoke@2026!'
    district = 'Nasr City'
    skills   = @(@{ skillId = 6; level = 'Intermediate' })
}
Assert-Equal $duplicateRegistration.Status 409 'duplicate email -> 409'

$balanceA = Invoke-Api -Method GET -Path '/api/me/balance' -Token $tokenA
Assert-Equal $balanceA.Status 200 'GET /api/me/balance'
Assert-Equal $balanceA.Body.balance 2 'new member balance is 2 hours'
Assert-Equal $balanceA.Body.locked 0 'new member locked is 0'
Assert-Equal $balanceA.Body.available 2 'new member available is 2'

$badLogin = Login $emailA 'WrongPassword1!'
Assert-Equal $badLogin.Status 401 'wrong password -> 401'

Write-Host "`n== Request validation (BR-03) ==" -ForegroundColor Cyan
$tooManyHours = Invoke-Api -Method POST -Path '/api/requests' -Token $tokenA -Body @{
    title          = 'Invalid hours'
    description    = 'Nine hours is above the 8 hour maximum.'
    categoryId     = 1
    district       = 'Nasr City'
    estimatedHours = 9
    neededBy       = '2026-12-31'
}
Assert-Equal $tooManyHours.Status 400 'estimatedHours 9 -> 400'

Write-Host "`n== Full exchange flow ==" -ForegroundColor Cyan
$createRequest = Invoke-Api -Method POST -Path '/api/requests' -Token $tokenA -Body @{
    title          = 'Smoke test: leaking sink'
    description    = 'The kitchen sink has leaked since this morning.'
    categoryId     = 1
    district       = 'Nasr City'
    estimatedHours = 1.5
    neededBy       = '2026-12-31'
}
Assert-Equal $createRequest.Status 201 'create request -> 201'
$requestId = $createRequest.Body.id
Assert-True ($createRequest.Body.status -eq 'Open') 'new request is Open'

$selfOffer = Invoke-Api -Method POST -Path "/api/requests/$requestId/offers" -Token $tokenA -Body @{
    message = 'I will help myself.'
    hours   = 1.5
}
Assert-Equal $selfOffer.Status 403 'offer on own request -> 403 (BR-06)'

$offer = Invoke-Api -Method POST -Path "/api/requests/$requestId/offers" -Token $tokenB -Body @{
    message = 'I am a plumber and can come today.'
    hours   = 1.5
}
Assert-Equal $offer.Status 201 'helper sends offer -> 201'
$offerId = $offer.Body.id

$duplicateOffer = Invoke-Api -Method POST -Path "/api/requests/$requestId/offers" -Token $tokenB -Body @{
    message = 'A second offer should be refused.'
    hours   = 1
}
Assert-Equal $duplicateOffer.Status 409 'duplicate offer -> 409 (BR-06)'

$offersAsHelper = Invoke-Api -Method GET -Path "/api/requests/$requestId/offers" -Token $tokenB
Assert-Equal $offersAsHelper.Status 403 'non-owner cannot list offers -> 403'

$offersAsOwner = Invoke-Api -Method GET -Path "/api/requests/$requestId/offers" -Token $tokenA
Assert-Equal $offersAsOwner.Status 200 'owner lists offers -> 200'
Assert-Equal @($offersAsOwner.Body).Count 1 'request has exactly one offer'

$accept = Invoke-Api -Method POST -Path "/api/offers/$offerId/accept" -Token $tokenA
Assert-Equal $accept.Status 200 'owner accepts offer -> 200'
Assert-Equal $accept.Body.status 'InProgress' 'request is InProgress'

$duplicateAccept = Invoke-Api -Method POST -Path "/api/offers/$offerId/accept" -Token $tokenA
Assert-Equal $duplicateAccept.Status 409 'repeated accept -> 409 (BR-07)'

$balanceLocked = Invoke-Api -Method GET -Path '/api/me/balance' -Token $tokenA
Assert-Equal $balanceLocked.Body.balance 2 'balance unchanged after accept'
Assert-Equal $balanceLocked.Body.locked 1.5 'accepted hours are locked'
Assert-Equal $balanceLocked.Body.available 0.5 'available = balance - locked (BR-05)'

$complete = Invoke-Api -Method POST -Path "/api/requests/$requestId/complete" -Token $tokenA -Body @{
    actualHours = 1.5
}
Assert-Equal $complete.Status 200 'owner completes request -> 200'
Assert-Equal $complete.Body.status 'Completed' 'request is Completed'

$duplicateComplete = Invoke-Api -Method POST -Path "/api/requests/$requestId/complete" -Token $tokenA -Body @{
    actualHours = 1.5
}
Assert-Equal $duplicateComplete.Status 409 'repeated complete -> 409 (BR-08)'

$balanceAAfter = Invoke-Api -Method GET -Path '/api/me/balance' -Token $tokenA
$balanceBAfter = Invoke-Api -Method GET -Path '/api/me/balance' -Token $tokenB
Assert-Equal $balanceAAfter.Body.balance 0.5 'requester debited 1.5 hours'
Assert-Equal $balanceAAfter.Body.locked 0 'lock released after completion'
Assert-Equal $balanceBAfter.Body.balance 3.5 'helper credited 1.5 hours'

Write-Host "`n== Reviews (BR-11) ==" -ForegroundColor Cyan
$reviewA = Invoke-Api -Method POST -Path "/api/requests/$requestId/reviews" -Token $tokenA -Body @{
    rating  = 5
    comment = 'Excellent work, very quick.'
}
Assert-Equal $reviewA.Status 201 'requester review -> 201'

$duplicateReviewA = Invoke-Api -Method POST -Path "/api/requests/$requestId/reviews" -Token $tokenA -Body @{
    rating  = 4
    comment = 'A second review should be refused.'
}
Assert-Equal $duplicateReviewA.Status 409 'duplicate review -> 409 (BR-11)'

$reviewB = Invoke-Api -Method POST -Path "/api/requests/$requestId/reviews" -Token $tokenB -Body @{
    rating  = 5
    comment = 'Clear instructions, thank you.'
}
Assert-Equal $reviewB.Status 201 'helper review -> 201'

Write-Host "`n== Statement stored procedure ==" -ForegroundColor Cyan
$statementA = Invoke-Api -Method GET -Path '/api/me/statement?from=2026-01-01&to=2026-12-31' -Token $tokenA
Assert-Equal $statementA.Status 200 'GET /api/me/statement'
Assert-True ($statementA.Body.entries.Count -ge 2) 'statement has welcome bonus + debit'
Assert-Equal $statementA.Body.openingBalance 0 'statement opening balance'
Assert-Equal $statementA.Body.closingBalance 0.5 'statement closing balance matches ledger'
$lastA = $statementA.Body.entries[-1]
Assert-Equal $lastA.runningBalance 0.5 'last running balance is 0.5'

$statementB = Invoke-Api -Method GET -Path '/api/me/statement?from=2026-01-01&to=2026-12-31' -Token $tokenB
Assert-Equal $statementB.Body.closingBalance 3.5 'helper statement closing balance is 3.5'

Write-Host "`n== Roles and suspension (BR-13, BR-14, BR-15) ==" -ForegroundColor Cyan
$memberCallsAdmin = Invoke-Api -Method GET -Path '/api/admin/members' -Token $tokenA
Assert-Equal $memberCallsAdmin.Status 403 'Member calls Moderator endpoint -> 403 (BR-13)'

$suspend = Invoke-Api -Method PUT -Path "/api/admin/members/$memberBId/suspend" -Token $moderatorToken -Body @{
    reason = 'Smoke test suspension'
}
Assert-Equal $suspend.Status 204 'Moderator suspends helper -> 204'

$suspendedLogin = Login $emailB 'Smoke@2026!'
Assert-Equal $suspendedLogin.Status 403 'suspended member login -> 403 (BR-14)'

$oldTokenRequest = Invoke-Api -Method POST -Path '/api/requests' -Token $tokenB -Body @{
    title          = 'Old token should fail'
    description    = 'This token was issued before the suspension.'
    categoryId     = 1
    district       = 'Nasr City'
    estimatedHours = 1
    neededBy       = '2026-12-31'
}
Assert-Equal $oldTokenRequest.Status 403 'old JWT cannot create request -> 403 (BR-14)'

$oldTokenOffer = Invoke-Api -Method POST -Path '/api/requests/1/offers' -Token $tokenB -Body @{
    message = 'Old token should not be able to offer.'
    hours   = 1
}
Assert-Equal $oldTokenOffer.Status 403 'old JWT cannot send offer -> 403 (BR-14)'

$auditLogs = Invoke-Api -Method GET -Path '/api/admin/audit-logs?take=50' -Token $moderatorToken
Assert-Equal $auditLogs.Status 200 'Moderator reads audit logs'
Assert-True (@($auditLogs.Body | Where-Object { $_.action -eq 'SuspendMember' -and $_.actor -eq "$moderatorId" }).Count -ge 1) 'suspension audit row has moderator id as actor (BR-15)'

$reinstate = Invoke-Api -Method PUT -Path "/api/admin/members/$memberBId/reinstate" -Token $moderatorToken
Assert-Equal $reinstate.Status 204 'Moderator reinstates helper -> 204'

$loginAfterReinstate = Login $emailB 'Smoke@2026!'
Assert-Equal $loginAfterReinstate.Status 200 'reinstated member can login -> 200'

$memberProfile = Invoke-Api -Method GET -Path "/api/members/$memberBId"
Assert-Equal $memberProfile.Status 200 'public member profile'
Assert-Equal $memberProfile.Body.averageRating 5 'helper average rating is 5'
Assert-True (@($memberProfile.Body.badges | Where-Object { $_.code -eq 'FIRST_HAND' }).Count -eq 1) 'First Hand badge awarded once (BR-12)'

Write-Host "`n== Result ==" -ForegroundColor Cyan
Write-Host "$($script:Checks - $script:Failures)/$($script:Checks) checks passed."
if ($script:Failures -gt 0) {
    Write-Host "$($script:Failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All smoke checks passed.' -ForegroundColor Green
