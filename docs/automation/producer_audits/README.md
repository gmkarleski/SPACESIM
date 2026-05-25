# Producer audits

Daily output from the producer-audit scheduled task. One file per day,
named `<YYYY-MM-DD>.md`.

Symmetric to `docs/automation/tech_director_audits/` for the tech-director-bot
audit cadence.

## Persistence convention

The producer-audit scheduled task writes its daily output to this directory
per the canonical write-path locked at commit 057b D2.

See `docs/DECISIONS.md` commit 057 entry D2 for the rationale.
