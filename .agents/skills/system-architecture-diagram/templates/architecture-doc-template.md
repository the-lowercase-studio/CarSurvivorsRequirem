# <System or Scope Name> – Architecture

*One-sentence summary: what this system or scope does in the game, and which parts the diagram covers.*

Last verified against source: YYYY-MM-DD.

---

## Architecture Diagram

```mermaid
flowchart TD
    subgraph DI["Reflex DI (scene boot)"]
        Installer["SceneInstaller"]
    end

    subgraph Sources["Triggers (other systems)"]
        Trigger["IExternalTrigger (what fires it)"]
    end

    subgraph Flow["Logic (pure C#)"]
        Service["ConcreteService (IService)"]
    end

    subgraph Core["Core"]
        Registry["ConcreteRegistry (IRegistry)"]
    end

    subgraph Data["Designer Data"]
        Config["ConfigSO (owned settings)"]
    end

    subgraph UI["Presentation"]
        Presenter["ConcretePresenter"]
    end

    subgraph Targets["Targets (other systems)"]
        Target["IExternalTarget"]
    end

    Installer -. "binds services" .-> Flow
    Trigger -- "OnSomethingHappened" --> Presenter
    Presenter -- "MethodName" --> Service
    Service -- "reads" --> Registry
    Registry -- "OnStateChanged" --> Presenter
    Registry -- "references" --> Config
    Registry -- "request effect" --> Target
```

**How to read it:** solid arrows show calls or events at runtime. Dotted arrows show DI wiring, limits from constants, or configuration references. Each box is described below.

---

## Responsibilities

### 1. <Group Name>

| Part | Responsibility |
| :--- | :--- |
| `TypeName` | What it owns, when it acts, what it hands off, and what it does not own. |

### 2. <Group Name>

| Part | Responsibility |
| :--- | :--- |
| `TypeName` | ... |

---

## Key Rules at a Glance

- <Invariant a human must respect, for example hierarchy, naming, or caps.>
- <Invariant about data mutation or ownership.>
