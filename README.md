# Manta

<p align="center">
  <img src="Manta_logo.png" alt="Manta logo" width="360"/>
</p>

<p align="center">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg"/>
  <img alt="Platform: Rhino 8" src="https://img.shields.io/badge/Rhino-8-darkblue.svg"/>
  <img alt=".NET 4.8" src="https://img.shields.io/badge/.NET-4.8-purple.svg"/>
  <img alt="Grasshopper" src="https://img.shields.io/badge/Grasshopper-Plugin-green.svg"/>
  <img alt="9 components" src="https://img.shields.io/badge/components-9-teal.svg"/>
</p>

> **One Grasshopper plugin for all environmental physics.**  
> Acoustic noise mapping, animated wind streamlines, solar path analysis, and pressure wave visualisation — live in the Rhino viewport at 60fps.

---

## Why "Manta"?

Manta rays are masters of fluid dynamics — they read pressure fields, glide on curl vortices, and navigate by sensing wave propagation. Manta does the same: it simulates acoustic noise as ray-cast energy fields, wind as a divergence-free curl-noise velocity field, and solar radiation as a real-time shadow sweep. The name fits the Grasshopper animal-plugin tradition (Weaverbird, Kangaroo, Pufferfish…).

---

## Components

### Acoustic — `Analysis → Acoustic`

| Icon | Name | Nickname | What it does |
|------|------|----------|--------------|
| ![](BatSource_24.png) | **MN Source** | MN Src | Define point and line noise sources (road, rail) |
| ![](BatMesh_24.png) | **MN Mesh** | MN Msh | Convert any geometry to analysis mesh |
| ![](BatNoise_24.png) | **MN Noise** | MN Nse | Acoustic analysis — false-colour heat-map, reflections |
| ![](BatInterior_24.png) | **MN Interior** | MN Int | Interior exposure score for Galapagos optimisation |
| ![](BatContours_24.png) | **MN Contours** | MN Con | Isodecibel contour polylines |
| ![](BatLegend_24.png) | **MN Legend** | MN Leg | Colour-scale legend in the Rhino viewport |

### Environment — `Analysis → Environment`

| Icon | Name | Nickname | What it does |
|------|------|----------|--------------|
| ![](MantaWind_24.png) | **MN Wind** | MN Wnd | Animated wind streamlines via curl-noise turbulence |
| ![](MantaSun_24.png) | **MN Sun** | MN Sun | Animated solar path + real-time shadow sweep |
| ![](MantaPressure_24.png) | **MN Pressure** | MN Pre | Animated acoustic pressure wavefronts |

---

## Typical workflow

```
MN Source ──► MN Mesh ──► MN Noise ──► MN Interior ──► Galapagos
                │              │
                │              └──► MN Contours ──► MN Legend
                │
                ├──► MN Wind      (wind over same facade mesh)
                └──► MN Sun       (solar on same mesh)
MN Source ──────────► MN Pressure (animated wavefronts from same sources)
```

---

## Acoustic reference

### MN Source

Point sources and/or line sources (road/rail centrelines). Line sources subdivide into N equal-power sub-points:

```
L_sub = L_total − 10·log10(N)
```

| Input | Default | Description |
|-------|---------|-------------|
| P – Point Sources | — | Individual noise source points |
| dBP – Point dB | — | Sound power level per point (dB SPL) |
| T – Rail/Road | — | Line-source curve (road or rail centreline) |
| dBT – Line dB | — | Sound power level per line source |
| N – Subdivisions | 20 | Sub-sources per line source |

---

### MN Mesh

Converts Mesh, Surface, Brep, SubD or Extrusion to a normals-ready analysis mesh. Quality 0–3 maps to Rhino's fast/default/analysis/fine meshing parameters.

---

### MN Noise

Core acoustic analysis. Computes per-face dB using:

```
L = L_src − 20·log10(d) − 11 + 10·log10(cosθ + 0.01)
L_total = 10·log10(Σ 10^(Li/10))
```

First-order reflections (optional):
```
L_ref = L_src − 20·log10(d1+d2) − 11 + 10·log10(cosθ+0.01) − α_dB − mat_loss
```

| Input | Default | Description |
|-------|---------|-------------|
| M – Mesh | — | From MN Mesh |
| S – Sources | — | From MN Source |
| dB – Levels | — | From MN Source |
| Min / Max | auto | Pin colour-scale bounds |
| R – Reflections | false | Enable first-order reflections |
| α – Absorption | 3 dB | Reflection loss per bounce |
| Mat – Materials | — | Per-face absorption coefficient 0–1 |
| Lim – Limit dB | — | Activates compliance overlay (green/yellow/red) |

| Output | Description |
|--------|-------------|
| M – Mesh | Vertex-coloured facade mesh |
| dB – Face dB | Per-face total → MN Interior / MN Contours |
| Min / Max | Colour-scale bounds → MN Legend |
| ExA – Exceeded m² | Facade area exceeding limit |
| Ex% – % Exceeded | Percentage of facade exceeding limit |
| RM – Reflect Mesh | False-colour reflection hotspot mesh |

---

### MN Interior

Scores interior noise exposure as a single dB fitness scalar — wire to Galapagos and set to **Minimise**:

```
Score = 10·log10(Σ [10^(dBi/10) × area_i / dist_i²])
```

---

### MN Contours

Marching-triangles isodecibel contour extraction. Connect `{50,55,60,65,70,75}` to Levels for a full contour map. Output is a GH tree — one branch per level.

---

### MN Legend

Draws a gradient colour-scale bar in the Rhino viewport. Position with Origin, size with Height/Width. Optional limit line overlay.

---

## Environment reference

### MN Wind

Particles advect through a curl-noise velocity field at ~60fps. Curl noise is divergence-free — trajectories are organic, not bunched.

```
v(x,t) = V_wind + curl( N(x/scale + t·0.1, y/scale, z/scale) ) × turbulence
```

Integrated with **RK2 (midpoint method)**. Golden-ratio phase offsets spread particles evenly.

| Input | Default | Description |
|-------|---------|-------------|
| M – Mesh | — | Analysis mesh |
| V – Wind Dir | (1,0,0) | Wind direction (normalised) |
| Sp – Speed | 5.0 | Animation rate |
| Tu – Turbulence | 1.5 | Curl-noise intensity (0 = laminar) |
| Sc – Scale | 10.0 | Noise scale relative to geometry |
| N – Particles | 80 | Streamline count |
| Tr – Trail | 20 | Trail length (steps) |
| S – Seed | 0 | Random seed |

---

### MN Sun

NOAA SPA algorithm — accurate to ±0.01° for 2000–2050. Animates the sun across the sky, painting the mesh with warm/cool shadow colours in real time.

| Input | Default | Description |
|-------|---------|-------------|
| M – Mesh | — | Analysis mesh |
| Lat | 51.5 | Latitude (°N) |
| Lon | −0.1 | Longitude (°E) |
| Yr / Mo / Dy | 2026-06-21 | Date |
| H0 / H1 | 6 / 20 | Analysis window (UTC hours) |
| As – Anim Spd | 1.0 | Speed multiplier |

Outputs: sun path arc, current direction/elevation/azimuth, per-face solar incidence, peak sun hours per face.

---

### MN Pressure

Spherical pressure wavefronts from noise sources. Each source emits concentric rings across three planes. Colour shifts warm (high dB) → cool (low dB).

| Input | Default | Description |
|-------|---------|-------------|
| S – Sources | — | From MN Source |
| dB – Levels | — | From MN Source |
| c – Wave Speed | 343 | Speed of sound (m/s) |
| Sc – Scale | 0.05 | Visual scale |
| R – Rings | 5 | Wavefront rings per source |

---

## Installation

### Build from source

Requirements: .NET SDK, Rhino 8

```bat
git clone https://github.com/LCS3002/Manta-Grasshopper
cd Manta-Grasshopper
build.bat
```

`build.bat` generates icons, compiles, and installs `Manta.gha`. Close Rhino before running.

### Manual install

Copy `Manta.gha` to `%APPDATA%\Grasshopper\Libraries\` and restart Rhino.

---

## Requirements

- Rhino 8 (RhinoCommon 8.x, Grasshopper 1.x)
- .NET Framework 4.8
- No external plugin dependencies

---

## Colour palette

| Colour | Hex | Used for |
|--------|-----|---------|
| Navy | `#080C1C` | Background |
| Teal | `#00D2B4` | Environment icons, wind trails |
| Cyan | `#3CDCFF` | Particle heads, solar glow |
| Amber | `#F5A623` | Acoustic icons, source indicators |

---

## Project structure

```
Manta-Grasshopper/
├── BatInfo.cs                  # Assembly info, icon loader, brand colours
├── BatAcoustics.cs             # Acoustic math — direct, reflections, energy sum
├── BatContourAlgo.cs           # Marching-triangles isodecibel contour extraction
├── BatSourceComponent.cs       # MN Source
├── BatMeshComponent.cs         # MN Mesh
├── BatNoiseComponent.cs        # MN Noise
├── BatInteriorComponent.cs     # MN Interior
├── BatContoursComponent.cs     # MN Contours
├── BatLegendComponent.cs       # MN Legend
├── MantaMath.cs                # Curl noise, RK2 advection, NOAA SPA, shadow
├── MantaWindComponent.cs       # MN Wind
├── MantaSunComponent.cs        # MN Sun
├── MantaPressureComponent.cs   # MN Pressure
├── NoiseFacadeGH.csproj        # SDK-style .NET 4.8 project
├── build.bat                   # One-click build + install
└── GenerateIcon/               # Programmatic icon generator (System.Drawing)
```

---

## License

MIT — see [LICENSE](LICENSE)
