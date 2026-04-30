# NoiseFacade GH

<p align="center">
  <img src="NoiseFacade_logo.png" alt="NoiseFacade logo" width="320"/>
</p>

<p align="center">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg"/>
  <img alt="Platform: Rhino 8" src="https://img.shields.io/badge/Rhino-8-darkblue.svg"/>
  <img alt=".NET 4.8" src="https://img.shields.io/badge/.NET-4.8-purple.svg"/>
  <img alt="Grasshopper Plugin" src="https://img.shields.io/badge/Grasshopper-Plugin-green.svg"/>
</p>

> **Paint a live acoustic noise heat-map onto any architectural geometry — facade panels, organic surfaces, Breps, SubDs.  
> Wire up Interior dB to Galapagos and let the solver optimise your topology for minimum interior noise.**

---

## Features

| | |
|---|---|
| **Live heat-map** | Blue (quiet) → Cyan → Yellow → Orange → Red (loud) painted directly in the Rhino viewport |
| **Physically-based model** | Inverse-square law + Lambert cosine incidence, multi-source energy summation |
| **First-order reflections** | Ray-cast each source off every face — find hotspots from concave surfaces and re-entrant geometry |
| **Interior exposure score** | Single dB scalar that measures how much noise reaches a point inside the building |
| **Galapagos-ready** | Wire *Interior dB* into a Galapagos fitness slider — the solver minimises interior noise automatically |
| **All geometry types** | Mesh, Surface, Brep, SubD, Extrusion — the plugin converts whatever you throw at it |

---

## Acoustic model

### Direct path

```
L  =  L_source  −  20·log₁₀(d)  −  11  +  10·log₁₀(cos θ + 0.01)
```

| Symbol | Meaning |
|---|---|
| `L_source` | Sound power level at source (dB SPL) |
| `d` | Distance from source to face centroid (m), clamped ≥ 0.1 m |
| `θ` | Angle of incidence — between face normal and direction toward source |

Multiple sources are combined by energy summation:

```
L_total  =  10·log₁₀( Σ 10^(Lᵢ / 10) )
```

### First-order reflections

When **Reflections** is enabled the plugin casts a reflected ray off each face for every source:

```
reflected direction  =  d̂  −  2 (d̂ · n̂) n̂

L_reflected  =  L_source  −  20·log₁₀(d₁ + d₂)  −  11  +  10·log₁₀(cos θᵢ + 0.01)  −  α
```

| Symbol | Meaning |
|---|---|
| `d₁` | Source → reflecting face |
| `d₂` | Reflecting face → receiving face |
| `α` | Absorption per bounce in dB (input **α**, default 3 dB) |

Reflected energy is added to the total before painting.

### Interior exposure score

Given an **Interior Point** inside the building, each facade face contributes noise to the interior proportional to its sound level, area, and proximity:

```
E_interior  =  Σ  [ 10^(dBᵢ / 10) × areaᵢ / distᵢ² ]

Interior dB  =  10·log₁₀( E_interior )
```

Minimise *Interior dB* with Galapagos to find the quietest topology.

---

## Installation

### From release (recommended)
1. Download `NoiseFacadeGH.gha` from [Releases](../../releases)
2. Right-click → Properties → **Unblock** (Windows internet zone)
3. Copy to `%APPDATA%\Grasshopper\Libraries\`
4. Restart Rhino

### From source
Requirements: .NET SDK, Rhino 8

```bat
git clone https://github.com/LCS3002/NoiseFacadeGH
cd NoiseFacadeGH
build.bat
```

`build.bat` compiles and copies the `.gha` to your Grasshopper Libraries folder automatically. Close Rhino before running.

---

## Inputs

| # | Name | Nick | Type | Default | Description |
|---|---|---|---|---|---|
| 0 | Geometry | G | GeometryBase | — | Facade: Mesh, Surface, Brep, SubD or Extrusion |
| 1 | Sources | S | Point3d list | — | Noise source positions |
| 2 | dB Levels | dB | number list | — | Sound power level at each source (dB SPL) |
| 3 | Quality | Q | integer | 1 | Mesh resolution: 0 fast → 3 fine |
| 4 | Min dB | Min | number | auto | Pin lower bound of colour scale |
| 5 | Max dB | Max | number | auto | Pin upper bound of colour scale |
| 6 | Interior Pt | IP | Point3d | — | Point inside building — activates interior score |
| 7 | Reflections | R | boolean | false | Enable first-order acoustic reflections |
| 8 | Absorption | α | number | 3.0 | Reflection loss per bounce (dB) |

## Outputs

| # | Name | Nick | Type | Description |
|---|---|---|---|---|
| 0 | Mesh | M | Mesh | Vertex-coloured mesh (total: direct + reflected) |
| 1 | Face dB | dB | number list | Per-face dB values |
| 2 | Min dB | Min | number | Actual colour-scale minimum |
| 3 | Max dB | Max | number | Actual colour-scale maximum |
| 4 | Interior dB | IntdB | number | Interior exposure score → wire to Galapagos fitness |
| 5 | Reflected Mesh | RM | Mesh | False-colour mesh of reflection hotspots |

---

## Workflow: Galapagos topology optimisation

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│   Parametric geometry     Noise source(s)    Interior point        │
│        (Sliders)               (Point)          (Point)            │
│            │                      │                │               │
│            ▼                      ▼                ▼               │
│      ┌─────────────────────────────────────────┐                   │
│      │            NoiseFacade GH               │                   │
│      │  G ← facade   S ← source  IP ← interior│                   │
│      │  R = true      α = 3.0                  │                   │
│      └───────────────────────┬─────────────────┘                   │
│                              │ Interior dB                         │
│                              ▼                                     │
│                      ┌──────────────┐                              │
│                      │  Galapagos   │  minimise fitness            │
│                      └──────────────┘                              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

1. Build a parametric facade driven by number sliders (panel depth, angle, curvature…)
2. Place a noise source (road, HVAC, etc.) and an interior point
3. Set **Reflections = true** to account for re-entrant surfaces
4. Connect **Interior dB** → Galapagos fitness input and set it to **Minimise**
5. Run — the solver explores the parameter space and converges on the quietest geometry

**Tip:** Pin **Min dB / Max dB** to a fixed range (e.g. 55–85 dB) so the gradient stays comparable across iterations.

---

## Colour gradient

| Colour | Mapped to |
|---|---|
| Blue (0, 0, 255) | Scale minimum — quietest |
| Cyan (0, 220, 255) | 25 % |
| Yellow (255, 240, 0) | 50 % |
| Orange (255, 110, 0) | 75 % |
| Red (255, 0, 0) | Scale maximum — loudest |

Per-vertex colour is the average of all incident face dBs, giving a smooth continuous gradient across the surface.

---

## Supported geometry

| Type | Conversion path |
|---|---|
| `Mesh` | Used directly |
| `Surface` | → Brep → mesh |
| `Brep` | → mesh via `MeshingParameters` |
| `Extrusion` | → Brep → mesh |
| `SubD` | → Brep → mesh (falls back to limit-surface mesh) |

---

## Requirements

- Rhino 8 (RhinoCommon 8.x + Grasshopper 1.x)
- .NET Framework 4.8

---

## Project structure

```
NoiseFacadeGH/
├── NoiseFacadeComponent.cs   # Component logic — acoustics, reflections, interior score
├── NoiseFacadeGH.csproj      # SDK-style .NET 4.8 class library
├── build.bat                 # Compile + install to Grasshopper Libraries
├── NoiseFacade_24.png        # 24 × 24 component icon (embedded)
├── NoiseFacade_48.png        # 48 × 48 assembly icon (embedded)
├── NoiseFacade_logo.png      # 512 × 512 logo (GitHub / README)
├── GenerateIcon/             # Standalone icon generator (System.Drawing)
└── MathTest/                 # Unit tests for acoustic maths and colour gradient
```

---

## License

MIT — see [LICENSE](LICENSE)
