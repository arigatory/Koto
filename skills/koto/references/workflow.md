# Spec-driven development workflow (Koto style)

The process used to build Koto itself; apply it to new packages, services, and non-trivial features.

```
spec → plan → ADR(s) → implement (test-first) → verify → docs + skill → release
```

## 1. Spec first

Before code, write a spec document (pattern: `docs/packages/NN-<name>.md` in the Koto repo):
- **Goals / non-goals** — what the package/feature does and explicitly does not.
- **Public API sketch** — types, signatures, DI extension methods, expected usage snippet.
- **Dependencies** — packages, license check (see license traps in SKILL.md).
- **Open questions** — resolved before implementation or promoted to ADRs.

## 2. Record decisions as ADRs

Any decision someone might later ask "why?" about gets an ADR (pattern: `adr/ADR-NNN-<slug>.md`):
- Header: status (✅ Принято / proposed / superseded), date.
- Sections: Context → Requirements/Options considered → Decision → Consequences.
- ADRs are revised, not silently rewritten — note revisions in the ADR itself (e.g. ADR-005 repository move, ADR-002/003/020 revised by ADR-026).

## 3. Keep a plan index

`PLAN.md` at repo root: phase index with status plus the per-release change list (Russian). Consumer-facing **migration guides** live in `docs/migrations/<from>-to-<to>.md` (English, before/after code — see `0.4-to-0.5.md` for the level of detail expected).

## 4. Implement test-first

- Each package/feature has a paired test project (`tests/<Package>.Tests`).
- Use `Koto.Testing` helpers: `AggregateTestFixture` (Given/When/Then), `FakeRepository`, `Result`/`AppResult` assertions, `ErrorCatalog`.
- Warnings are errors; `ConfigureAwait(false)` in libraries (CA2007 = error).

## 5. Definition of done

A change is done when **spec, ADRs, tests, the package README and the agent skill (`skills/koto/`) are all consistent** with the code — README and skill updates ship in the same change, not later. If the public API changed, update the guide in `docs/migrations/`, the `PLAN.md` entry and the skill's migration checklist.

## 6. Release

See [publishing.md](publishing.md).
