# NextStagePortal Visuals & Shader Concepts - Brainstorm Summary

## Overview

- **Feature**: `NextStagePortal` visual overhaul
- **Trigger**: Defeating the Ancient Golem boss (`GolemBoss`) in `RuinedBloodCity`
- **Current State**: Flattened Unity primitive cylinder mesh with basic unlit green emission (`EmisiveGreen.mat`), appearing like a flat green cone/disc.
- **Goal**: Replace the placeholder with a stylistically cohesive, visually striking portal effect tailored for a vehicle-based survivor game.

---

## Game Style & Environment Alignment

- **Aesthetic**: Grim post-apocalyptic urban wasteland ("Ruined Blood City"), asphalt highway carnage, intense particle combat, neon and blood-red emissives against dark asphalt and concrete.
- **Player Perspective**: Top-down / isometric high-speed chase. The player is driving a heavily armed combat vehicle at speed.
- **Physical Scale**: The portal must have clear depth and presence, wide enough to accommodate a car driving through at high speed, with unmistakable readability through active bullet and swarm chaos.

---

## 3 Portal Shader Concepts

### Concept 1: Bloodgate Singularity (Abyssal Blood Vortex)
- **Vibe**: Dark crimson and obsidian gravitational whirlpool left behind as the Golem's earthen core collapses.
- **Visual Elements**:
  - Deep pitch-black event horizon center that absorbs light.
  - Logarithmic inward polar swirl with turbulent blood-red and ember accretion ribbons.
  - Glowing procedural energy filaments breaking apart and spiraling into the center.
  - High-intensity Fresnel rim with subtle crimson chromatic aberration.
- **Shader Architecture**:
  - Polar coordinates: $r = \text{length}(uv)$, $\theta = \text{atan2}(uv.y, uv.x)$.
  - Time-driven logarithmic spiral distortion: $\theta' = \theta + \frac{k}{r + \epsilon} - t \cdot \text{speed}$.
  - Multi-octave Perlin / Simplex noise masks for tendril tearing.
  - Depth fade + soft particles for smooth ground intersection.

### Concept 2: Arcane Spacetime Rift (Electric Void Tear)
- **Vibe**: Violent dimensional fracture crackling with unstable cosmic lightning, warping the urban background behind it.
- **Visual Elements**:
  - Vertical or angled jagged fissure / elliptic rift with unstable vibrating perimeter.
  - Electric cyan core blending into deep ultraviolet / magenta turbulent edge fringes.
  - Dynamic Voronoi plasma arcs flickering erratically around the boundary.
  - Screen-space refraction (grab-pass/scene color distortion) bending the road and destroyed architecture behind the portal.
- **Shader Architecture**:
  - Voronoi edge detection with randomized phase jitter.
  - Dual scrolling noise maps driving UV distortion in opposite directions.
  - Fresnel edge mask with pulse oscillation: $\sin(t \cdot \omega) \cdot \text{amplitude}$.
  - Screen-space refraction offset map.

### Concept 3: Overclocked Tachyon Gateway (Hardlight Accelerator)
- **Vibe**: Cyber-apocalyptic runway accelerator; high-tech escape vector with rotating holographic hardlight rings and directional chevron indicators.
- **Visual Elements**:
  - Concentric rotating segmented rings (outer ring counter-rotates against inner ring).
  - Emissive animated road chevrons / arrows flowing inward, inviting the car to boost through.
  - Digital scanline interference, glitch flickers, and hexagonal energy lattice.
  - High-intensity amber/cyber-gold or toxic emerald energy core.
- **Shader Architecture**:
  - Polar stepped rings via $\text{floor}(r \cdot N)$ and angular segmentation via $\text{mod}(\theta \cdot M, 1.0)$.
  - UV translation along radius for continuous inward flow of chevron patterns.
  - Hexagonal grid mask multiplied by Fresnel rim intensity.
  - Glitch offset triggered by step functions over time.

---

## Visualization Options Available Outside Unity

1. **Interactive WebGL Generative UI Artifact (Real-time in Antigravity)**:
   - Full live GLSL fragment shader running in 60 FPS inside the Antigravity chat / side-pane.
   - 3D interactive camera (pan/orbit/tilt) to view the portal flat on the ground or upright like a gate.
   - Real-time parameter sliders (swirl speed, noise turbulence, rim glow, core darkness, orientation).
   - Instant switching between all 3 concepts with zero build or compile times.

2. **Concept Art Generation (`image-generator` Subagent)**:
   - High-fidelity concept art images showing the portal integrated into the game's actual night/dusk apocalyptic city scene with the player car approaching.

3. **Blender MCP Automation**:
   - Programmatic generation of 3D geometry and shader node setups via Blender Python API (if Blender is running with MCP).

4. **Technical Architecture Diagrams (Mermaid / Shader Graph node maps)**:
   - Complete breakdown of mathematical node networks for reproduction in Unity URP Shader Graph.
