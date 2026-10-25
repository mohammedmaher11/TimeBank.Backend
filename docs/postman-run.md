# Postman / Newman run

The collection is the executable API contract for the backend. It covers the
whole member journey plus the required error paths, role checks, suspension,
moderator category CRUD, and audit logging.

- Collection: `postman/TimeBank.postman_collection.json`
- Convenience copy: `docs/postman/TimeBank.postman_collection.json`
- Collection schema: Postman v2.1
- Requests: **37**, in dependency order
- Default base URL: `http://localhost:5080`

> The copy under `docs/postman/` is byte-identical to the canonical file under
> `postman/`. If you edit one, copy it over the other.

## Prerequisites

1. SQL Server is running and `TimeBankDb` exists.
2. The API is running with the Development environment, so migrations and seed
   data are applied:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   dotnet run --project src\TimeBank.API --urls http://localhost:5080
   ```

3. `GET http://localhost:5080/health` returns `Healthy`.
4. The seeded moderator exists (`mona.adel@sa3a.test` / `Sa3a@2026`).

The collection creates two new members per run with a timestamp-based
`runId`, so it can be run repeatedly against the same development database.
It does not truncate or reset the database.

## Run in the Postman UI

1. **Import** `postman/TimeBank.postman_collection.json`.
2. Open the collection **Variables** tab and check:

   | Variable | Default | Purpose |
   |---|---|---|
   | `baseUrl` | `http://localhost:5080` | API origin |
   | `moderatorEmail` | `mona.adel@sa3a.test` | seeded moderator |
   | `moderatorPassword` | `Sa3a@2026` | development password |
   | `categoryId` | `1` | seeded Home Repair category |
   | `requestedHours` | `1.5` | exchange size |
   | `neededBy` | `2026-12-31` | request date |

   `runId`, `requesterEmail`, `helperEmail`, and `categoryName` are generated
   automatically by the collection-level pre-request script at
   `00 - Health`.

3. Click **Run** and run the collection **in order**, top to bottom. The
   requests depend on variables saved by earlier requests, so do not skip or
   reorder folders.
4. Expect **37 requests / 37 assertions / 0 failures**.

No Postman environment is required; collection variables are sufficient.

## Run headlessly with Newman

Install Node.js 18+ and Newman once:

```powershell
npm install -g newman
newman --version
```

Run from the backend root:

```powershell
newman run postman\TimeBank.postman_collection.json `
  --reporters cli,json `
  --reporter-json-export .run\newman-result.json
```

The command exits with code `0` only when every assertion passes.

## Actual verified run

Executed on **2026-10-25** against the real API and real SQL Server database:

| Item | Result |
|---|---|
| Runner | Newman **6.2.3** on Node.js **v24.22.1** |
| Collection | `TimeBank.postman_collection.json` |
| Base URL | `http://localhost:5080` |
| Iterations | **1** (0 failed) |
| Requests | **37** (0 failed) |
| Pre-request scripts | **37** (0 failed) |
| Test scripts | **37** (0 failed) |
| Assertions | **37** (0 failed) |
| Run duration | **13.6 s** |
| Average response | **281 ms** (min 12 ms, max 2.27 s) |
| Database | real SQL Server `TimeBankDb`, seeded through migrations |

Result: **37/37 requests and 37/37 assertions passed, exit code 0.**

### Defect found and fixed by this run

The first headless run failed one assertion: `GET /api/members` returned
`500`. EF Core could not translate the directory projection when
`OrderBy(p => p.FullName)` was applied *after* the DTO projection containing
correlated aggregates. The fix orders members **before** projecting in
`MemberRepository.ListProfilesAsync`, and the regression test
`MemberDirectory_ReturnsSeededProfilesOrderedByName` now covers the endpoint.
The rerun above is the passing result after that fix.

## What the 37 requests prove

- health, public stats, seeded categories and skills;
- registration of two members with skills, JWT issuance, seeded moderator
  login;
- request creation, invalid-hours `400`, offer creation, duplicate-offer
  `409`;
- owner-only offer listing (`403` for a non-owner), accept, repeated accept
  `409`;
- balance and locked hours after accept, completion, repeated completion
  `409`, cancel-after-complete `409`;
- reviews by both parties, duplicate review `409`;
- balances after completion, statement through `sp_GetMemberStatement`,
  member directory, public profile with badge and rating;
- member calling moderator endpoint `403`;
- moderator suspension, old JWT rejected with `403`, suspended login `403`,
  reinstatement, login again;
- moderator category create/update/delete and audit-log assertions;
- final stats reflecting the completed exchange.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `ECONNREFUSED` | API is not running, or `baseUrl` is wrong. |
| `401` on login | Wrong seeded moderator password; check `appsettings.Development.json`. |
| `403` on `00 - Health` | A stale `Authorization` header on the collection; clear it. |
| `409` on registration | A stale `runId`; start the run at `00 - Health`, or clear `runId`. |
| `500` on a request | Check the API console; the collection is designed to fail loudly. |
| Assertion fails after a code change | Fix the API or update the expected contract deliberately, then rerun. |
