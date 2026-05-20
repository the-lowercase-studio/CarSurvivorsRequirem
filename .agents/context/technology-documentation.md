# Technology Documentation for ProjectLizard

## Purpose

This file provides official documentation links for core technologies used in this project.

## Documentation-First Rule

Agents must consult official documentation listed here before relying on general model knowledge.

Execution policy:

1. Read the relevant official documentation first.
2. Apply project-specific constraints from AGENTS.md and project-coding-standards.md.
3. If documentation is ambiguous, ask the user instead of guessing.
4. If implementation behavior in project code differs from docs, keep project behavior and request clarification.

## Official Documentation Links

### Unity

- Official docs: https://docs.unity.com/en-us
- Use for: engine APIs, lifecycle behavior, UI systems, serialization, editor workflows.

### Reflex (Dependency Injection)

- Official repository/docs: https://github.com/gustavopsantos/Reflex
- Use for: container setup, bindings, installers, injection patterns, best practices.

### DOTween

- Official docs: https://dotween.demigiant.com/documentation.php
- Use for: tween configuration, sequencing, easing behavior, performance-safe animation usage.

## Usage Guidance for Agents

1. Do not invent framework behavior when official docs are available.
2. Prefer documented API usage patterns over speculative shortcuts.
3. Keep references to official docs in implementation notes when changes depend on framework behavior.
4. For breaking or version-specific API uncertainty, ask the user to confirm project package/version constraints.

## Cross-References

- See AGENTS.md for architecture and repository-specific working rules.
- See ai-game-dev-best-practices.md for workflow, validation, and review practices.
- See project-coding-standards.md for code style and structure rules.
