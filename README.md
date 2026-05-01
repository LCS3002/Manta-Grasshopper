# Manta

<p align="center">
  <img src="Manta_logo.png" alt="Manta logo" width="360"/>
</p>

<p align="center">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue.svg"/>
  <img alt="Platform: Rhino 8" src="https://img.shields.io/badge/Rhino-8-darkblue.svg"/>
  <img alt=".NET 4.8" src="https://img.shields.io/badge/.NET-4.8-purple.svg"/>
  <img alt="Grasshopper" src="https://img.shields.io/badge/Grasshopper-Plugin-green.svg"/>
  <img alt="60fps" src="https://img.shields.io/badge/animation-60fps-teal.svg"/>
</p>

> **Environmental analysis for Grasshopper — one plugin, all physics.**  
> Acoustic noise heat-maps, animated wind streamlines, solar path sweep, pressure wavefronts. Self-contained, no Ladybug required.

---

## Why "Manta"?

Manta rays are masters of fluid dynamics — they read pressure fields, glide on curl vortices, and navigate by sensing wave propagation. Manta does the same: it simulates acoustic noise as ray-cast energy fields, wind as a divergence-free curl-noise velocity field, and solar radiation as a real-time shadow sweep. The name fits the Grasshopper animal-plugin tradition (Ladybug, Weaverbird, Kangaroo…).

---

## Components

### Acoustic — `Analysis → Acoustic`

| Icon | Name | Nickname | What it does |
|------|------|----------|--------------|
| ![](BatSource_24.png) | **BT Source** | BT Src | Point and line (road/rail) noise sources |
| ![](BatMesh_24.png)   | **BT Mesh**   | BT Msh | Convert any geometry to an analysis mesh |
| ![](BatNoise_24.png)  | **BT Noise**  | BT Nse | Direct + reflected noise, heat-map, compliance |
| ![](BatInterior_24.png) | **BT Interior** | BT Int | Interior exposure score — wire to Galapagos |
| ![](BatContours_24.png) | **BT Contours** | BT Con | Isodecibel contour polylines as a data tree |
| ![](BatLegend_24.png) | **BT Legend** | BT Leg | dB colour-scale legend in the Rhino viewport |

### Environment — `Analysis → Environment`

| Icon | Name | Nickname | What it does |
|------|------|----------|--------------|
| ![](MantaWind_24.png)     | **MN Wind**     | MN Wnd | Animated wind streamlines via curl-noise turbulence |
| ![](MantaSun_24.png)      | **MN Sun**      | MN Sun | Animated solar path + real-time shadow sweep |
| ![](MantaPressure_24.png) | **MN Pressure** | MN Pre | Animated acoustic pressure wavefronts |

---

## Typical workflow

```
  BT Source ──► BT Mesh ──► BT Noise ──► BT Contours
                   │                          │
                   │                     BT Legend
                   │
                   ├──────────────────► MN Wind
                   └──────────────────► MN Sun

  BT Source ──────────────────────────► MN Pressure
  BT Noise  ──► BT Interior ──► Galapagos (minimise)
```

---

## Acoustic model

### Direct sound

```
L = L_src − 20·log₁₀(d) − 11 + 10·log₁₀(cosθ + 0.01)
```

Multiple sources combine by energy summation: `L_total = 10·log₁₀(Σ 10^(Lᵢ/10))`

### First-order reflections

```
L_ref = L_src − 20·log₁₀(d₁+d₂) − 11 + 10·log₁₀(cosθ + 0.01) − α_dB − mat_loss
```

### Interior exposure score

```
E_int = Σ [ 10^(dBᵢ/10) × area_i / dist_i² ]
Interior dB = 10·log₁₀(E_int)
```

Connect **Interior dB** → Galapagos fitness input → **Minimise**.

---

## Wind model

Particles advect through a **curl-noise velocity field** at ~60fps. Curl noise is divergence-free — particles never bunch or spread, producing organic-looking streamlines identical to real CFD visualisations.

```
v(x,t) = V_wind + curl( N(x/scale + t·0.1, y/scale, z/scale) ) × turbulence
```

Positions integrated with **RK2 (midpoint method)**. Golden-ratio phase offsets spread particles evenly along each path.

---

## Solar model

Real solar position via the **NOAA SPA algorithm** — accurate to ±0.01° for 2000–2050. The sun disc animates across the sky, painting a warm/cool shadow gradient in real time.

---

## Component reference

### BT Source

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Point Sources | — | Individual point sources |
| 1 | Point dB | — | Level for each point source |
| 2 | Rail/Road | — | Line sources (roads, railways) |
| 3 | Line dB | — | Level for each line source |
| 4 | Subdivisions | 20 | Sub-points along each curve |

Outputs: **Sources** (Point list), **Levels** (dB list), **Count**.

---

### BT Mesh

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Geometry | — | Mesh, Surface, Brep, SubD, Extrusion |
| 1 | Quality | 1 | 0 fast → 3 fine |

Outputs: **Mesh**, **Face Count**, **Area** (m²).

---

### BT Noise

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Mesh | — | From BT Mesh |
| 1 | Sources | — | From BT Source |
| 2 | Levels | — | From BT Source |
| 3 | Min dB | auto | Pin colour-scale lower bound |
| 4 | Max dB | auto | Pin colour-scale upper bound |
| 5 | Reflections | false | First-order ray-cast reflections |
| 6 | Absorption α | 3.0 | Bounce loss per reflection (dB) |
| 7 | Materials | — | Per-face absorption coefficient 0–1 |
| 8 | Limit dB | — | WHO / regulatory limit |

| # | Output | Description |
|---|--------|-------------|
| 0 | Mesh | Vertex-coloured facade mesh |
| 1 | Face dB | Per-face dB |
| 2 | Min dB | Colour-scale minimum |
| 3 | Max dB | Colour-scale maximum |
| 4 | Exceeded m² | Facade area above Limit dB |
| 5 | % Exceeded | Percentage above limit |
| 6 | Reflect Mesh | Reflection hotspot heat-map |

---

### BT Interior

| # | Input | Description |
|---|-------|-------------|
| 0 | Mesh | From BT Noise |
| 1 | Face dB | From BT Noise |
| 2 | Interior Pt | Point inside the building |

Outputs: **Interior dB** (→ Galapagos), **Area-wtd mean**, **Peak face dB**.

---

### BT Contours

| # | Input | Description |
|---|-------|-------------|
| 0 | Mesh | From BT Noise |
| 1 | Face dB | From BT Noise |
| 2 | Levels | dB levels to contour — e.g. {50,55,60,65,70,75} |

Outputs: **Contours** (curve data tree), **Levels**, **Count**.

---

### BT Legend

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Origin | (0,0,0) | Base-left corner in world space |
| 1 | Min dB | 40 | From BT Noise Min |
| 2 | Max dB | 80 | From BT Noise Max |
| 3 | Height | 5 | Bar height (model units) |
| 4 | Width | 1 | Bar width (model units) |
| 5 | Ticks | 5 | Number of tick labels |
| 6 | Limit dB | — | Draw limit line on legend |

Outputs: **Legend Mesh**, **Labels**, **Label Pts**.

---

### MN Wind

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Mesh | — | Analysis mesh |
| 1 | Wind Dir | (1,0,0) | Wind direction vector |
| 2 | Speed | 5.0 | Wind speed |
| 3 | Turbulence | 1.5 | Curl-noise intensity |
| 4 | Scale | 10.0 | Noise scale relative to geometry |
| 5 | Particles | 80 | Number of streamline particles |
| 6 | Trail | 20 | Trail length in steps |
| 7 | Seed | 0 | Random seed |

Outputs: **Streamlines** (polylines), **Field Pts** (vectors at face centres).

---

### MN Sun

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Mesh | — | Analysis mesh |
| 1 | Latitude | 51.5 | Site latitude (°N) |
| 2 | Longitude | −0.1 | Site longitude (°E) |
| 3 | Year | 2026 | Year |
| 4 | Month | 6 | Month (1–12) |
| 5 | Day | 21 | Day (1–31) |
| 6 | Start Hr | 6.0 | Analysis window start (UTC hours) |
| 7 | End Hr | 20.0 | Analysis window end (UTC hours) |
| 8 | Anim Spd | 1.0 | Animation speed multiplier |

Outputs: **Sun Path**, **Sun Dir**, **Elevation**, **Azimuth**, **Face Solar**, **Peak Hours**.

---

### MN Pressure

| # | Input | Default | Description |
|---|-------|---------|-------------|
| 0 | Sources | — | Noise source points (from BT Source) |
| 1 | Levels | — | dB levels per source (from BT Source) |
| 2 | Wave Speed | 343 | Speed of sound (m/s) |
| 3 | Scale | 0.05 | Visual scale |
| 4 | Rings | 5 | Wavefront rings per source |

---

## Colour palettes

### Acoustic (BT components)

| Colour | Maps to |
|--------|---------|
| Blue `(0, 0, 255)` | Scale minimum — quietest |
| Cyan `(0, 220, 255)` | 25 % |
| Yellow `(255, 240, 0)` | 50 % |
| Orange `(255, 110, 0)` | 75 % |
| Red `(255, 0, 0)` | Scale maximum — loudest |

### Environment (MN components)

| Colour | Hex | Used for |
|--------|-----|---------|
| Navy | `#080C1C` | Background |
| Teal | `#00D2B4` | Wind trails, icons, pressure arcs |
| Cyan | `#3CDCFF` | Particle heads, sun disc, glows |

---

## Installation

### Build from source

Requirements: .NET SDK, Rhino 8

```bat
git clone https://github.com/LCS3002/NoiseFacadeGH
cd NoiseFacadeGH
build.bat
```

`build.bat` generates icons, compiles, and installs `Manta.gha` automatically. Close Rhino first.

### Manual install

Copy `Manta.gha` to `%APPDATA%\Grasshopper\Libraries\` and restart Rhino.

---

## Requirements

- Rhino 8 (RhinoCommon 8.x, Grasshopper 1.x)
- .NET Framework 4.8
- No Ladybug or other plugins required

---

## Project structure

```
NoiseFacadeGH/
├── BatInfo.cs              # Assembly info, icon loader, brand colours
├── BatAcoustics.cs         # Acoustic math (direct, reflections, interior, contours)
├── BatContourAlgo.cs       # Marching-triangles isodecibel extractor
├── BatSourceComponent.cs   # BT Source
├── BatMeshComponent.cs     # BT Mesh
├── BatNoiseComponent.cs    # BT Noise
├── BatInteriorComponent.cs # BT Interior
├── BatContoursComponent.cs # BT Contours
├── BatLegendComponent.cs   # BT Legend
├── MantaMath.cs            # Curl noise, RK2 advection, NOAA SPA, shadow
├── MantaWindComponent.cs   # MN Wind
├── MantaSunComponent.cs    # MN Sun
├── MantaPressureComponent.cs # MN Pressure
├── NoiseFacadeGH.csproj    # SDK-style .NET 4.8 project
├── build.bat               # One-click build + install
├── Manta_24/48.png         # Assembly icons — manta ray (embedded)
├── BatSource/Mesh/.../24.png # BT component icons (embedded)
├── MantaWind/Sun/Pressure_24.png # MN component icons (embedded)
├── Manta_logo.png          # 512px logo for GitHub
├── GenerateIcon/           # Icon generator (System.Drawing, no Rhino dep)
└── MathTest/               # 65-test battle suite for all math
```

---

## License

MIT — see [LICENSE](LICENSE)
