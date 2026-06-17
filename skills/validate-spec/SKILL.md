# SKILL: validate-spec
> Validates whether a SPEC.md is complete and well-formed before starting implementation.

---

## Objective

Read a `SPEC-*.md` and verify it contains all required sections with valid content.
Returns a validation report with approved items, failed items, and improvement suggestions.

---

## Documents this skill reads (required)

```
1. docs/GUARDRAILS.md
2. docs/context/CONVENTIONS.md
3. docs/context/DOMAIN-GLOSSARY.md
4. docs/specs/[feature]/SPEC-[feature].md
```

---

## Output

```markdown
## Validation: SPEC-[feature].md

### ✅ Passed
- [ validated item ]

### ❌ Failed
- [ item with issue ] → [ correction suggestion ]

### ⚠️ Warning
- [ optional item missing or incomplete ]

### Result: PASSED | FAILED
> [feature] is ready for implementation. | [feature] needs corrections before implementation.
```

---

## Validation Rules

### Required sections

| Section | Required | Valid when |
|---------|----------|------------|
| `## Overview` | ✅ | Has feature description (min. 2 lines) |
| `## Endpoints` | ✅ | Has at least 1 documented endpoint |
| `## Business Rules` | ✅ | Has at least 1 rule with code `BR-[FEATURE]-NNN` |
| `## Domain Events` | ✅ | Present (may be empty with justification) |
| `## Validation Criteria` | ✅ | Has Unit Tests and Integration Tests tables |
| `## Dependencies` | ✅ | Present (may be empty) |

### Endpoint validation
Each endpoint must have:
- [ ] HTTP method + full path (e.g. `POST /api/v1/auth/login`)
- [ ] Auth indication (Public or JWT)
- [ ] Rate Limit defined
- [ ] Request body (if POST/PUT)
- [ ] Response with status code and body example
- [ ] Error table with status codes

### Business Rules validation
- [ ] Each rule has a unique code in the format `BR-[FEATURE]-NNN`
- [ ] Rules are clear statements (not vague)
- [ ] No rule duplicates another

### Validation Criteria validation
- [ ] Unit Tests table exists with columns: ID, Scenario, Input, Expected
- [ ] Integration Tests table exists with columns: ID, Scenario, Input, Expected
- [ ] IDs follow pattern `AC-[FEATURE]-U[NN]` for unit and `AC-[FEATURE]-I[NN]` for integration
- [ ] Each endpoint has at least 1 success and 1 error scenario in integration tests
- [ ] Each critical Business Rule has at least 1 corresponding test
- [ ] Minimum of 5 unit tests and 5 integration tests

### Terminology validation
- [ ] Uses only terms defined in `DOMAIN-GLOSSARY.md`
- [ ] Endpoint names in English and kebab-case

### Contract validation
- [ ] Request/response fields in snake_case
- [ ] Correct status codes (201 for creation, 204 for delete, 202 for async)
- [ ] UUIDs used as identifiers (not integers)
- [ ] Dates in ISO 8601 format

---

## Example Output — Passing

```markdown
## Validation: SPEC-auth.md

### ✅ Passed
- Overview present and descriptive
- 6 endpoints documented with method, path, auth, and rate limit
- 20 Business Rules with unique codes BR-AUTH-001 to BR-AUTH-020
- Domain Events documented (UserRegistered, UserLoggedIn)
- 18 Unit Tests in Validation Criteria table
- 24 Integration Tests in Validation Criteria table
- All fields in snake_case
- Terminology consistent with DOMAIN-GLOSSARY.md
- Status codes correct (201, 204, 401, 403, 422, 429)

### ❌ Failed
(no failed items)

### ⚠️ Warning
- BR-AUTH-015 mentions "reset token valid for 1 hour" but TECH-STACK.md
  does not document this TTL → consider adding to TECH-STACK.md

### Result: PASSED
> auth is ready for implementation.
```

---

## Example Output — With Failures

```markdown
## Validation: SPEC-catalog.md

### ✅ Passed
- Overview present
- Endpoints documented

### ❌ Failed
- Endpoint GET /catalog/products has no error table
  → Add table with at least 400 and 429
- Business Rule BR-CAT-003 is vague: "Price must be valid"
  → Specify: "Price must be greater than zero (decimal)"
- Integration test AC-CAT-I11 does not cover POST /catalog/products
  → Add success scenario for product creation

### ⚠️ Warning
- No test covers cache miss scenario
  → Consider adding AC-CAT-I09 to validate behavior without cache

### Result: FAILED
> catalog needs corrections before implementation.
> Items to fix: 3
```

---

## How to use this skill

```
Prompt:

"Read the following documents in order:
1. docs/GUARDRAILS.md
2. docs/context/CONVENTIONS.md
3. docs/context/DOMAIN-GLOSSARY.md
4. docs/specs/catalog/SPEC-catalog.md

Using the validate-spec skill (docs/skills/validate-spec/SKILL.md),
validate the catalog feature SPEC and return the full report."
```

---

## Internal checklist

- [ ] All required sections present
- [ ] All endpoints have auth, rate limit, request, response, and errors
- [ ] Business Rule IDs sequential and unique
- [ ] Validation Criteria IDs sequential and unique
- [ ] Minimum test coverage (5 unit + 5 integration)
- [ ] Terminology aligned with glossary
- [ ] snake_case in all JSON fields
- [ ] Status codes semantically correct