# pm

`pm` is currently a `.NET 9` backend API for the early MVP of a freelancer-oriented project/payment management system.

At the moment, this repository covers:

- user registration and login
- refresh token based session flow
- logout
- current user profile retrieval
- current user profile update
- client listing
- client creation
- client update
- client soft delete
- database migration on startup
- demo data seeding on startup
- presentation-ready JSON request payloads

This repository does **not** currently contain a frontend application. It is a backend-only solution with the API in [`src/pm.API`](/Users/adojas/RiderProjects/pm/src/pm.API), application layer in [`src/pm.Application`](/Users/adojas/RiderProjects/pm/src/pm.Application), domain models in [`src/pm.Domain`](/Users/adojas/RiderProjects/pm/src/pm.Domain), and infrastructure in [`src/pm.Infrastructure`](/Users/adojas/RiderProjects/pm/src/pm.Infrastructure).

## Current Scope

The current backend implements two main product areas from the project summary:

- `Vartotojo paskyros valdymas`
- `Klientų valdymas`

It does **not** yet implement:

- `Projekto valdymas`
- `Projekto eiga`
- `Mokėjimų procesas`
- `Statistika`

That means the project currently supports account and client management, but not yet the main MVP flow described in the summary:

`projektas -> sąskaita -> apmokėjimas`

## API Surface

Implemented endpoints:

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/users/me`
- `PUT /api/v1/users/me`
- `GET /api/v1/clients`
- `GET /api/v1/clients/{id}`
- `POST /api/v1/clients`
- `PUT /api/v1/clients/{id}`
- `DELETE /api/v1/clients/{id}`

Main entry point:

- [`src/pm.API/Program.cs`](/Users/adojas/RiderProjects/pm/src/pm.API/Program.cs:1)

## Database Startup Behavior

On API startup, the app currently does two things automatically:

1. runs schema migration via [`DatabaseMigrator`](/Users/adojas/RiderProjects/pm/src/pm.Infrastructure/DatabaseMigrator.cs:1)
2. runs demo seed data via [`DemoDataSeeder`](/Users/adojas/RiderProjects/pm/src/pm.Infrastructure/DemoDataSeeder.cs:1)

This is wired in [`Program.cs`](/Users/adojas/RiderProjects/pm/src/pm.API/Program.cs:65).

## Demo Data Seeder

The demo data seeder is intended for presentation/demo use.

Seeded data currently includes:

- 2 demo users
- 5 demo clients
- 2 seeded refresh-token sessions

Seeder implementation:

- [`src/pm.Infrastructure/DemoDataSeeder.cs`](/Users/adojas/RiderProjects/pm/src/pm.Infrastructure/DemoDataSeeder.cs:1)

Seeded primary demo user:

- email: `roberta.demo@pm.local`
- password: `PmDemo123!`
- seeded refresh token: `pm-demo-refresh-roberta-2026`

Seeded secondary demo user:

- email: `liveta.demo@pm.local`
- password: `PmDemo123!`
- seeded refresh token: `pm-demo-refresh-liveta-2026`

Important behavior:

- the seeder runs on every startup
- seeded users/clients use fixed IDs
- inserts are idempotent through `ON CONFLICT`
- refresh/logout presentation payloads rely on seeded refresh tokens
- if you use the refresh endpoint during a demo, that session is revoked; restarting the API will seed it again

## Presentation Files

Presentation data and ready request payloads are stored under [`presentation`](/Users/adojas/RiderProjects/pm/presentation).

Reference files:

- [`presentation/demo-reference.json`](/Users/adojas/RiderProjects/pm/presentation/demo-reference.json:1)
- [`presentation/demo-scenarios.json`](/Users/adojas/RiderProjects/pm/presentation/demo-scenarios.json:1)

Actual request payload files:

- [`presentation/requests/auth/register-new-user.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/register-new-user.json:1)
- [`presentation/requests/auth/login-primary-user.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/login-primary-user.json:1)
- [`presentation/requests/auth/refresh-token.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/refresh-token.json:1)
- [`presentation/requests/auth/logout.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/logout.json:1)
- [`presentation/requests/users/update-profile.json`](/Users/adojas/RiderProjects/pm/presentation/requests/users/update-profile.json:1)
- [`presentation/requests/clients/create-company-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/create-company-client.json:1)
- [`presentation/requests/clients/create-individual-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/create-individual-client.json:1)
- [`presentation/requests/clients/update-seeded-company-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/update-seeded-company-client.json:1)

These JSON files contain the actual payloads intended for presentation use.

## Running The API

Requirements:

- `.NET 9 SDK`
- access to the database configured in [`src/pm.API/appsettings.json`](/Users/adojas/RiderProjects/pm/src/pm.API/appsettings.json:1)

Run from the repository root:

```bash
dotnet run --project src/pm.API
```

If started successfully:

- database migration will run
- demo seed data will run
- API will be available based on [`launchSettings.json`](/Users/adojas/RiderProjects/pm/src/pm.API/Properties/launchSettings.json:1)
- default local URLs are `http://localhost:5216` and `https://localhost:7139`

## Presentation Demo Script

This is the safest backend-only demo flow for the current state of the project.

Base URL:

- `http://localhost:5216`

Authorization header format for protected endpoints:

```text
Authorization: Bearer <access_token>
```

Recommended demo order:

1. Start the API with `dotnet run --project src/pm.API`
2. Confirm seed data is present by using the seeded primary user from [`presentation/demo-reference.json`](/Users/adojas/RiderProjects/pm/presentation/demo-reference.json:1)
3. Login and capture the returned `accessToken`
4. Use that `accessToken` for profile and client endpoints
5. Optionally show refresh-token flow
6. End with logout

Suggested presentation sequence:

1. `POST /api/v1/auth/login`
   Use [`presentation/requests/auth/login-primary-user.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/login-primary-user.json:1)
   Save the `accessToken` from the response.

2. `GET /api/v1/users/me`
   Send with `Authorization: Bearer <accessToken>`
   This shows the currently logged-in seeded user.

3. `PUT /api/v1/users/me`
   Send with `Authorization: Bearer <accessToken>`
   Use [`presentation/requests/users/update-profile.json`](/Users/adojas/RiderProjects/pm/presentation/requests/users/update-profile.json:1)
   This shows profile update working for the logged-in user.

4. `GET /api/v1/clients`
   Send with `Authorization: Bearer <accessToken>`
   This shows the seeded client list for the current user.

5. `POST /api/v1/clients`
   Send with `Authorization: Bearer <accessToken>`
   Use [`presentation/requests/clients/create-company-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/create-company-client.json:1)
   This shows company client creation.

6. `POST /api/v1/clients`
   Send with `Authorization: Bearer <accessToken>`
   Use [`presentation/requests/clients/create-individual-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/create-individual-client.json:1)
   This shows individual client creation.

7. `PUT /api/v1/clients/4423dc96-64da-4825-af57-900fb01c1d81`
   Send with `Authorization: Bearer <accessToken>`
   Use [`presentation/requests/clients/update-seeded-company-client.json`](/Users/adojas/RiderProjects/pm/presentation/requests/clients/update-seeded-company-client.json:1)
   This updates an already seeded company client.

8. `GET /api/v1/clients`
   Send with `Authorization: Bearer <accessToken>`
   This confirms the new/updated client data is present.

9. `POST /api/v1/auth/refresh`
   Use [`presentation/requests/auth/refresh-token.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/refresh-token.json:1)
   This demonstrates the refresh-token endpoint.

10. `POST /api/v1/auth/logout`
   Send with `Authorization: Bearer <accessToken>`
   Use [`presentation/requests/auth/logout.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/logout.json:1)

Optional extra step:

- `POST /api/v1/auth/register`
  Use [`presentation/requests/auth/register-new-user.json`](/Users/adojas/RiderProjects/pm/presentation/requests/auth/register-new-user.json:1)
  This is useful if you want to show new account creation, but it is usually better to start with the seeded user because it leads directly into the rest of the demo.

Presentation notes:

- `GET /api/v1/users/me`, `PUT /api/v1/users/me`, and all `/api/v1/clients` endpoints require a valid bearer token.
- `POST /api/v1/auth/login` returns a fresh `accessToken` and `refreshToken`.
- `POST /api/v1/auth/refresh` can be shown using the seeded refresh token from the JSON file, but once used it revokes that session as part of the current implementation.
- if you want the seeded refresh-token demo to work again exactly the same way, restart the API so the seeder runs again.
- if you want the simplest possible presentation, skip refresh and just demo login, profile, client list, client create, client update, and logout.

## Jira Coverage

This section reflects the current backend code in this repository, not the planned scope in Jira.

### Covered

- `ISKPVM-17 Paskyros sukūrimas`
- `ISKPVM-72 Naujos vartotojo paskyros sukūrimo ir išsaugojimo sistemoje įgyvendinimas`
- `ISKPVM-24 Prisijungimo duomenų validavimas ir sesijos sukūrimas`
- `ISKPVM-26 Aktyvios vartotojo sesijos nutraukimas`
- `ISKPVM-38 Paskyros informacijos peržiūra`
- `ISKPVM-63 Pakeistų paskyros duomenų išsaugojimo ir atnaujinimo sistemoje įgyvendinimas`
- `ISKPVM-25 Kliento informacijos redagavimas ir šalinimas`

### Partially Covered

- `ISKPVM-29 Paskyros duomenų atnaujinimas`
  Backend update exists, but Jira item also includes validation and UI behavior not present in this repo.
- `ISKPVM-23 Naujo kliento sukūrimas`
  Backend client creation exists, but acceptance criteria are not fully covered.
- `ISKPVM-36 Paskyros duomenų saugumas`
  JWT protection and per-user scoping exist, but the whole security/non-functional scope is broader than the current implementation.
- `ISKPVM-10 Klientų valdymas`
  Epic is only partially covered because client CRUD exists, but requirements are incomplete.

### Explicit Gaps Inside Implemented Areas

The following Jira-aligned pieces are still missing even though account/client functionality exists:

- `ISKPVM-73 Implementuoti kliento unikalumo patvirtinimą`
- `ISKPVM-71 Implementuoti duomenų formato validaciją`

Additional client-creation gaps against `ISKPVM-23` acceptance criteria:

- required field validation is incomplete
- duplicate client prevention is missing
- IBAN field is not modeled
- frontend instant list refresh is not part of this backend repo

### Not Covered

- `ISKPVM-11 Projekto valdymas`
- `ISKPVM-12 Projekto eiga`
- `ISKPVM-13 Mokėjimų procesas`
- `ISKPVM-14 Statistika`
- `ISKPVM-18 Projekto kūrimo inicijavimas`
- `ISKPVM-19 Projekto kūrimas`
- `ISKPVM-20 Projekto būsenos pažymėjimas`
- `ISKPVM-21 Automatinis projekto būsenos atnaujinimas`
- `ISKPVM-27 Projekto informacijos peržiūrėjimas`
- `ISKPVM-28 Projekto užbaigimas`
- `ISKPVM-39 Automatinis PDF sąskaitos sugeneravimas`
- `ISKPVM-40 Sąskaitų apmokėjimo statusų sekimas`
- `ISKPVM-44 Unikalios mokėjimo nuorodos generavimas`
- `ISKPVM-83 Sukurti pajamų statistikos pajamų endpointą`

Frontend/UI Jira items are also not covered in this repository, because there is no frontend project at the moment 2026-04-14.

## Recommended Next Improvement

The next most valuable improvement, based on the project summary and Jira priorities, is:

- project CRUD with client association and project status

Why this is the next step:

- it closes the biggest gap between the current code and the MVP flow
- it unlocks a believable presentation path beyond account/client management
- it aligns with the highest-value missing work in `ISKPVM-18`, `ISKPVM-19`, `ISKPVM-20`, `ISKPVM-21`, and `ISKPVM-27`

## Known Limits

- no automated tests are present in this repository yet
- no frontend project is present
- no project entity/module exists yet
- no invoice generation exists yet
- no Stripe/payment integration exists yet
- no statistics endpoint exists yet
- client validation is currently weaker than Jira acceptance criteria require

## Notes

- [`src/pm.API/appsettings.json`](/Users/adojas/RiderProjects/pm/src/pm.API/appsettings.json:1) currently contains the runtime database/JWT configuration used by the API
- startup currently validates JWT config in [`Program.cs`](/Users/adojas/RiderProjects/pm/src/pm.API/Program.cs:100)
- I was not able to build/run the API from this Codex environment because `dotnet` is not installed here, so runtime behavior should still be verified locally in Rider or with the `dotnet` CLI
