# SPEC-auth.md
> Feature: Authentication & Authorization
> Phase: 1
> Status: Draft

## Context
- [CONTEXT-auth.md](./CONTEXT-auth.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Manages the authentication lifecycle of `Users` in the system.
Uses **ASP.NET Core Identity** for user management and **JWT Bearer** for stateless authentication.

Responsibilities:
- New user registration (`Customer`)
- Authentication via email/password
- JWT token issuance and renewal
- Session revocation (logout)
- Password recovery via mocked email

---

## Endpoints

### POST /api/v1/auth/register
Creates a new `User` with role `Customer`.

- **Auth:** Public
- **Rate Limit:** `auth-register` — 3 req/min per IP

**Request:**
```json
{
  "first_name": "John",
  "last_name": "Doe",
  "email": "john@example.com",
  "password": "Password@123"
}
```

**Response 201 Created:**
```json
{
  "id": "uuid",
  "first_name": "John",
  "last_name": "Doe",
  "email": "john@example.com",
  "created_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Required fields missing or invalid |
| 409 | Email already registered |
| 422 | Password does not meet requirements |
| 429 | Rate limit exceeded |

---

### POST /api/v1/auth/login
Authenticates an existing `User` and returns JWT + Refresh Token.

- **Auth:** Public
- **Rate Limit:** `auth-strict` — 5 req/min per IP

**Request:**
```json
{
  "email": "john@example.com",
  "password": "Password@123"
}
```

**Response 200 OK:**
```json
{
  "access_token": "eyJhbGci...",
  "refresh_token": "abc123...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Required fields missing |
| 401 | Invalid credentials |
| 423 | Account locked out |
| 429 | Rate limit exceeded |

---

### POST /api/v1/auth/refresh
Renews the Access Token using a valid Refresh Token.

- **Auth:** Public
- **Rate Limit:** `auth-strict` — 5 req/min per IP

**Request:**
```json
{ "refresh_token": "abc123..." }
```

**Response 200 OK:**
```json
{
  "access_token": "eyJhbGci...",
  "refresh_token": "xyz789...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Refresh token missing |
| 401 | Refresh token invalid or expired |
| 429 | Rate limit exceeded |

---

### POST /api/v1/auth/logout
Revokes the authenticated user's Refresh Token.

- **Auth:** JWT required
- **Rate Limit:** `user` — 60 req/min per UserId

**Request:**
```json
{ "refresh_token": "abc123..." }
```

**Response 204 No Content**

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Refresh token missing |
| 401 | JWT invalid or expired |
| 429 | Rate limit exceeded |

---

### POST /api/v1/auth/forgot-password
Requests password recovery. Generates a reset token and simulates email sending (mock via `IEmailService`).

- **Auth:** Public
- **Rate Limit:** `auth-strict` — 5 req/min per IP

**Request:**
```json
{ "email": "john@example.com" }
```

**Response 200 OK:**
```json
{
  "message": "If the email is registered, you will receive instructions shortly."
}
```

> ⚠️ Always return `200` regardless of whether the email exists (avoid user enumeration).

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Email missing or invalid |
| 429 | Rate limit exceeded |

---

### POST /api/v1/auth/reset-password
Resets the password using the token received by email.

- **Auth:** Public
- **Rate Limit:** `auth-strict` — 5 req/min per IP

**Request:**
```json
{
  "email": "john@example.com",
  "token": "abc123...",
  "new_password": "NewPassword@456"
}
```

**Response 200 OK:**
```json
{ "message": "Password successfully reset." }
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Fields missing or invalid |
| 401 | Token invalid or expired |
| 422 | New password does not meet requirements |
| 429 | Rate limit exceeded |

---

## Business Rules

### Registration
- `BR-AUTH-001` Email must be unique in the system. Return `409` if already exists.
- `BR-AUTH-002` Password must have at least 8 characters, 1 uppercase letter, 1 number, and 1 special character.
- `BR-AUTH-003` Role `Customer` must be automatically assigned on registration.
- `BR-AUTH-004` Event `UserRegistered` must be published after successful registration.
- `BR-AUTH-005` Never return different messages for "email does not exist" vs "wrong password" (avoid enumeration).

### Login
- `BR-AUTH-006` After **5 consecutive** failed attempts, the account must be locked for **15 minutes** (Lockout).
- `BR-AUTH-007` Refresh Token must have a validity of **7 days**.
- `BR-AUTH-008` Access Token must have a validity of **1 hour**.
- `BR-AUTH-009` Refresh Token must be stored hashed in the database (never plain text).
- `BR-AUTH-010` Event `UserLoggedIn` must be published after successful login.

### Refresh
- `BR-AUTH-011` On renewal, the previous Refresh Token must be invalidated (rotation).
- `BR-AUTH-012` Expired or already-used Refresh Token must return `401`.

### Logout
- `BR-AUTH-013` Logout must invalidate the Refresh Token in the database.
- `BR-AUTH-014` Logout must not invalidate the Access Token (stateless — expires naturally).

### Password Recovery
- `BR-AUTH-015` Always return `200` on `forgot-password` regardless of whether the email exists.
- `BR-AUTH-016` Reset token generated by Identity, valid for **1 hour**.
- `BR-AUTH-017` Reset token is single-use — invalidated after successful use.
- `BR-AUTH-018` New password must meet the same requirements as registration (`BR-AUTH-002`).
- `BR-AUTH-019` Email sending simulated via `IEmailService` (mock) — logs token to Seq in development.
- `BR-AUTH-020` After successful reset, all active Refresh Tokens for the user must be invalidated.

---

## Domain Events

| Event | Published when | Data |
|-------|---------------|------|
| `UserRegistered` | Successful registration | `UserId`, `Email`, `OccurredAt` |
| `UserLoggedIn` | Successful login | `UserId`, `Email`, `OccurredAt` |

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-AUTH-U01 | Register user with valid data | Valid fields | User created, role `Customer` assigned, `UserRegistered` published |
| AC-AUTH-U02 | Register with existing email | Duplicate email | Conflict error, no user created |
| AC-AUTH-U03 | Register with weak password | Password without uppercase/number/special | Validation error |
| AC-AUTH-U04 | Register with missing fields | Empty `email` | Validation error |
| AC-AUTH-U05 | Login with valid credentials | Email + correct password | JWT + Refresh Token returned, `UserLoggedIn` published |
| AC-AUTH-U06 | Login with wrong password | Wrong password | Auth error, attempt counter incremented |
| AC-AUTH-U07 | Login after 5 failed attempts | 5 consecutive wrong passwords | Account locked for 15 min |
| AC-AUTH-U08 | Refresh with valid token | Valid Refresh Token | New Access Token + rotated Refresh Token |
| AC-AUTH-U09 | Refresh with expired token | Expired Refresh Token | Auth error |
| AC-AUTH-U10 | Refresh with already-used token | Already rotated Refresh Token | Auth error |
| AC-AUTH-U11 | Logout revokes refresh token | Valid Refresh Token + JWT | Refresh Token invalidated in database |
| AC-AUTH-U12 | Generic message for invalid credentials | Non-existent email or wrong password | Same error message in both cases |
| AC-AUTH-U13 | Forgot password with existing email | Registered email | Token generated, mock email logged to Seq |
| AC-AUTH-U14 | Forgot password with non-existing email | Unregistered email | No token generated, identical response to success case |
| AC-AUTH-U15 | Reset password with valid token | Token + valid new password | Password updated, all Refresh Tokens invalidated |
| AC-AUTH-U16 | Reset password with expired token | Expired token | Auth error |
| AC-AUTH-U17 | Reset password with already-used token | Already used token | Auth error |
| AC-AUTH-U18 | Reset password with weak password | Invalid new password | Validation error |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-AUTH-I01 | POST /auth/register with valid data | Valid body | `201 Created` + user body |
| AC-AUTH-I02 | POST /auth/register with duplicate email | Already registered email | `409 Conflict` |
| AC-AUTH-I03 | POST /auth/register with weak password | Invalid password | `422 Unprocessable Entity` |
| AC-AUTH-I04 | POST /auth/register with empty body | Body `{}` | `400 Bad Request` |
| AC-AUTH-I05 | POST /auth/login with valid credentials | Email + correct password | `200 OK` + `access_token` + `refresh_token` |
| AC-AUTH-I06 | POST /auth/login with wrong password | Wrong password | `401 Unauthorized` |
| AC-AUTH-I07 | POST /auth/login after 5 failed attempts | 5 logins with wrong password | `423 Locked` |
| AC-AUTH-I08 | POST /auth/login with empty body | Body `{}` | `400 Bad Request` |
| AC-AUTH-I09 | POST /auth/refresh with valid token | Valid Refresh Token | `200 OK` + new tokens |
| AC-AUTH-I10 | POST /auth/refresh with invalid token | Random token | `401 Unauthorized` |
| AC-AUTH-I11 | POST /auth/refresh with expired token | Expired token | `401 Unauthorized` |
| AC-AUTH-I12 | POST /auth/logout with valid JWT | JWT + Refresh Token | `204 No Content` |
| AC-AUTH-I13 | POST /auth/logout without JWT | No Authorization header | `401 Unauthorized` |
| AC-AUTH-I14 | POST /auth/login rate limit | 6 req/min | `429 Too Many Requests` with `Retry-After` |
| AC-AUTH-I15 | POST /auth/register rate limit | 4 req/min | `429 Too Many Requests` with `Retry-After` |
| AC-AUTH-I16 | POST /auth/forgot-password existing email | Registered email | `200 OK` with generic message |
| AC-AUTH-I17 | POST /auth/forgot-password non-existing email | Unregistered email | `200 OK` with same generic message |
| AC-AUTH-I18 | POST /auth/forgot-password with empty body | Body `{}` | `400 Bad Request` |
| AC-AUTH-I19 | POST /auth/reset-password with valid token | Token + valid new password | `200 OK` |
| AC-AUTH-I20 | POST /auth/reset-password with expired token | Expired token | `401 Unauthorized` |
| AC-AUTH-I21 | POST /auth/reset-password with already-used token | Reused token | `401 Unauthorized` |
| AC-AUTH-I22 | POST /auth/reset-password with weak password | Invalid new password | `422 Unprocessable Entity` |
| AC-AUTH-I23 | Login with old password after reset | Password before reset | `401 Unauthorized` |
| AC-AUTH-I24 | Refresh Token invalidated after reset | Refresh Token prior to reset | `401 Unauthorized` |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| ASP.NET Core Identity | Infrastructure | User management and lockout |
| JWT Bearer | Infrastructure | Token generation and validation |
| PostgreSQL | Infrastructure | User and refresh token persistence |
| IEmailService (Mock) | Infrastructure | Simulated email sending for password reset |

## Feature Dependencies
- None. Auth is the foundation for all other features.

---

## Implementation Notes
- Use `UserManager<ApplicationUser>` from Identity for user operations.
- Use `SignInManager<ApplicationUser>` for authentication (automatically manages lockout).
- Refresh Token generated via `Guid.NewGuid().ToString("N")` + stored with SHA256 hash.
- Required JWT claims: `sub` (UserId), `email`, `role`, `jti` (JWT ID).
- Reset token generated via `UserManager.GeneratePasswordResetTokenAsync()`.
- `IEmailService` with `MockEmailService` implementation that logs the token to Seq in development.
- In production, replace `MockEmailService` with `SendGridEmailService` or `SmtpEmailService` without changing Application.