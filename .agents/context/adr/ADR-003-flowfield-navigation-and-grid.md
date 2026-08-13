# ADR-003: Deterministic Grid & FlowField Navigation Architecture

- Status: Accepted
- Date: 2026-08-12
- Decision Makers: Game Development Team & AI Agents

## Context

In survivor-style games with hundreds or thousands of active enemy entities targeting the player car simultaneously, individual NavMeshAgent pathfinding causes severe CPU overhead and main-thread allocation spikes. A central vector field (FlowField) calculated over a 2D/3D spatial grid provides `O(1)` direction lookups per enemy.

## Decision

We standardize swarm navigation using the **GridSystem** and **FlowFieldSystem**:

1. **Centralized Calculation:** The FlowField system calculates vector directions relative to the target (player car) once per update tick or grid recalculation.
2. **O(1) Entity Sampling:** Enemies query the flow field for their current grid cell direction vector without computing individual A* or NavMesh paths.
3. **Allocation-Conscious Memory Model:** Reuse native arrays / job structures for grid updates to avoid garbage collection frame drops.

## Consequences

### Positive
- High scalability supporting hundreds of active units without NavMesh agent performance bottlenecks.
- Deterministic movement patterns easy to debug and optimize.

### Negative / Trade-offs
- Obstacle integration requires grid cost map updates rather than runtime NavMesh baking.
- Agents modifying navigation must keep grid coordinate conversions deterministic and bound checking explicit.
