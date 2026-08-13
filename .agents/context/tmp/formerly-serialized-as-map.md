# Mapa Użyć Atrybutu FormerlySerializedAs i Wartości Konfiguracyjnych

Dokument zawiera kompletne zestawienie wszystkich historycznych i aktualnych wystąpień atrybutu `FormerlySerializedAs` oraz powiązanych pól zserializowanych w projekcie Car Survivors. Mapa zawiera dokładne, aktualne wartości pól (wraz z referencjami guid, fileID, typami oraz nazwami assetów), co umożliwia bezbłędną konfigurację scen, prefabów i assetów Unity na innych branchach.

---

## 1. Assets/Scripts/LevelSystem/Exp/ExpParticle.cs

- **Klasa / Struktura**: `ExpParticle.ExpParticleApearanceByTreshold` (struktura wewnętrzna)
- **Komponent Unity**: `ExpParticle` (skrypt guid: `5de08d9a20f99324795dfd1638919701`)
- **Asset bazowy**: Assets/Prefabs/Exp/ExpParticle.prefab (MonoBehaviour fileID: `2432047573316968158`)
- **Status skryptu**: Aktywny w projekcie
- **Historia atrybutu**: Wprowadzony w CSR-2, usunięty w CSR-4

### Mapowanie pól

| Stara nazwa pola (`FormerlySerializedAs`) | Nowa nazwa pola w C# | Typ | Właściwość publiczna | Status w prefabach/scenach |
| :--- | :--- | :--- | :--- | :--- |
| `Treshold` | `_treshold` | `float` | `Treshold` | Zaktualizowano w Assets/Prefabs/Exp/ExpParticle.prefab do `_treshold` |
| `Material` | `_material` | `Material` | `Material` | Zaktualizowano w Assets/Prefabs/Exp/ExpParticle.prefab do `_material` |
| `ScaleValueRange` | `_scaleValueRange` | `FloatValueRange` | `ScaleValueRange` | Zaktualizowano w Assets/Prefabs/Exp/ExpParticle.prefab do `_scaleValueRange` |

### Aktualne wartości w Assets/Prefabs/Exp/ExpParticle.prefab

Tablica `_particleApearanceByTreshold` (3 elementy):

- **Element 0**:
  - `_treshold`: `0`
  - `_material`: Assets/Materials/Particles/Exp/ExpLow.mat (guid: `a4d8d66ba61208a43b2ca2d38a43b042`, fileID: `2100000`, type: `2`)
  - `_scaleValueRange`: Min = `0.6`, Max = `0.64` (`<Min>k__BackingField: 0.6`, `<Max>k__BackingField: 0.64`)
- **Element 1**:
  - `_treshold`: `100`
  - `_material`: Assets/Materials/Particles/Exp/ExpMedium.mat (guid: `df2e32090d5b80a489de2919c5c83932`, fileID: `2100000`, type: `2`)
  - `_scaleValueRange`: Min = `0.7`, Max = `0.74` (`<Min>k__BackingField: 0.7`, `<Max>k__BackingField: 0.74`)
- **Element 2**:
  - `_treshold`: `250`
  - `_material`: Assets/Materials/Particles/Exp/ExpHigh.mat (guid: `51d97dc8c0d9f3e438bf7e456384d929`, fileID: `2100000`, type: `2`)
  - `_scaleValueRange`: Min = `0.8`, Max = `0.84` (`<Min>k__BackingField: 0.8`, `<Max>k__BackingField: 0.84`)

#### Dodatkowe zserializowane pola komponentu `ExpParticle`:
- `_movementSpeed`: `8.8`
- `_disapearingDuration`: `0.1`
- `_visual`: referencja do Transform/GameObject `Visual` (fileID: `2497718662250730421`)

---

## 2. Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs

- **Klasa / Struktura**: `ExpParticleSpawner.ExpTresholdDevider` (struktura wewnętrzna)
- **Komponent Unity**: `ExpParticleSpawner` (skrypt guid: `d97bba37f4106fb47a29d53d52dc8900`)
- **Asset bazowy**: Assets/Scenes/RuinedBloodCity.unity (GameObject: `ExpParticlesSpawner`, fileID: `375309167`, MonoBehaviour fileID: `375309169`)
- **Status skryptu**: Aktywny w projekcie
- **Historia atrybutu**: Wprowadzony w CSR-2, usunięty w CSR-4

### Mapowanie pól

| Stara nazwa pola (`FormerlySerializedAs`) | Nowa nazwa pola w C# | Typ | Właściwość publiczna | Status w prefabach/scenach |
| :--- | :--- | :--- | :--- | :--- |
| `Treshold` | `_treshold` | `float` | `Treshold` | Zaktualizowano w Assets/Scenes/RuinedBloodCity.unity do `_treshold` |
| `Divider` | `_divider` | `float` | `Divider` | Zaktualizowano w Assets/Scenes/RuinedBloodCity.unity do `_divider` |

### Aktualne wartości w Assets/Scenes/RuinedBloodCity.unity

Tablica `_expTresholdDeviders` (3 elementy):

- **Element 0**:
  - `_treshold`: `0`
  - `_divider`: `1`
- **Element 1**:
  - `_treshold`: `50`
  - `_divider`: `2`
- **Element 2**:
  - `_treshold`: `100`
  - `_divider`: `3`

#### Dodatkowe zserializowane pola komponentu `ExpParticleSpawner`:
- `_expParticlesParent`: `{fileID: 825131016}` (Transform GameObjectu `ExpParticles` na scenie)
- `_particlesYOffset`: `1`
- `_expParticlePrefab`: Assets/Prefabs/Exp/ExpParticle.prefab (fileID: `2432047573316968158`, guid: `e3766f3b3f9025548927256069c08ace`, type: `3`)
- `_spawningCircleRadius`: `1.2`

---

## 3. Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs

- **Klasa / Struktura**: `FlowFieldMovementController`
- **Komponent Unity**: `FlowFieldMovementController` (skrypt guid: `00e6a945254f7014a8a9a2f6700ad6b6`)
- **Status skryptu**: Aktywny w projekcie (atrybuty `[FormerlySerializedAs]` są obecne w kodzie C#)
- **Historia atrybutu**: Wprowadzony w CSR-6

### Mapowanie pól

| Stara nazwa pola (`FormerlySerializedAs`) | Nowa nazwa pola w C# | Typ | Wartość domyślna w C# | Status w prefabach/scenach |
| :--- | :--- | :--- | :--- | :--- |
| `separationRadius` | `_separationRadius` | `float` | `1.2f` | Zaktualizowano w prefabach wrogów i cząsteczki Exp |
| `separationStrength` | `_separationStrength` | `float` | `0.5f` | Zaktualizowano w prefabach wrogów i cząsteczki Exp |

### Aktualne wartości w poszczególnych prefabach

1. **Assets/Prefabs/Enemies/BarrelEnemy.prefab** (MonoBehaviour fileID: `335` / `4444089047832875064`):
   - `_separationRadius`: `1.2`
   - `_separationStrength`: `0.5`

2. **Assets/Prefabs/Enemies/Zombies/Crawling/CrawlingZombie.prefab** (MonoBehaviour fileID: `4599289953647572422`):
   - `_separationRadius`: `1.2`
   - `_separationStrength`: `0.5`

3. **Assets/Prefabs/Enemies/Zombies/Standing/StandingZombie.prefab** (MonoBehaviour fileID: `4599289953647572422`):
   - `_separationRadius`: `1.2`
   - `_separationStrength`: `0.5`

4. **Assets/Prefabs/Exp/ExpParticle.prefab** (MonoBehaviour fileID: `3206386065100887514`):
   - `_separationRadius`: `1.2`
   - `_separationStrength`: `0.5`

---

## 4. Assets/Scripts/Player/Car/CarController.cs

- **Klasa / Struktura**: `CarController.Wheel` (struktura wewnętrzna)
- **Komponent Unity**: `CarController` (skrypt guid: `b9556b9460d41014cbfe327ac1a28644`)
- **Asset bazowy**: Assets/Prefabs/Player/Player.prefab (MonoBehaviour fileID: `8489968326519551025`)
- **Status skryptu**: Aktywny w projekcie (zrefaktorowany w CSR-26 do modelu arcade)
- **Historia atrybutu**:
  - W CSR-4 zmieniono: `WheelModel` -> `_wheelModel`, `WheelCollider` -> `_wheelCollider`, `Axel` -> `_axel`
  - W CSR-26 usunięto pole `_wheelCollider` z C# po przejściu na model arcade (raycasty i wirtualne zawieszenie), natomiast pola `_wheelModel` i `_axel` pozostały w strukturze `Wheel`

### Mapowanie pól

| Stara nazwa pola (`FormerlySerializedAs`) | Nowa nazwa pola w C# | Typ | Status w kodzie i assetach |
| :--- | :--- | :--- | :--- |
| `WheelModel` | `_wheelModel` | `GameObject` | Aktywne pole w strukturze `Wheel` |
| `Axel` | `_axel` | `Axel` (enum: `Front = 0`, `Rear = 1`) | Aktywne pole w strukturze `Wheel` |
| `WheelCollider` | `_wheelCollider` | `WheelCollider` | Usunięte z C# w CSR-26 (fizyka kół zastąpiona raycastami) |

### Aktualne wartości w Assets/Prefabs/Player/Player.prefab

Lista `_wheels` (4 elementy):

- **Element 0 (Przednie Lewe - FL)**:
  - `_wheelModel`: `{fileID: 9106598245786203359}` (GameObject `FL` zagnieżdżony w prefabie `WheelL.fbx`, guid: `c378eff32a7a1b948bb79a5f7ebf6395`)
  - `_axel`: `0` (`Axel.Front`)
- **Element 1 (Przednie Prawe - FR)**:
  - `_wheelModel`: `{fileID: 6389166653650457024}` (GameObject `FR` zagnieżdżony w prefabie `WheelL.fbx`, guid: `c378eff32a7a1b948bb79a5f7ebf6395`)
  - `_axel`: `0` (`Axel.Front`)
- **Element 2 (Tylne Lewe - RL)**:
  - `_wheelModel`: `{fileID: 8745980457491291175}` (GameObject `RL` zagnieżdżony w prefabie `WheelL.fbx`, guid: `c378eff32a7a1b948bb79a5f7ebf6395`)
  - `_axel`: `1` (`Axel.Rear`)
- **Element 3 (Tylne Prawe - RR)**:
  - `_wheelModel`: `{fileID: 3255664546788292463}` (GameObject `RR` zagnieżdżony w prefabie `WheelL.fbx`, guid: `c378eff32a7a1b948bb79a5f7ebf6395`)
  - `_axel`: `1` (`Axel.Rear`)

#### Powiązane parametry arcade kół/zawieszenia w `CarController` (CSR-26):
- `_wheelVisualRadius`: `0.272`
- `_groundCheckDistance`: `0.4`
- `_groundTargetYOffset`: `0`
- `_raycastOriginYOffset`: `-0.1`
- `_groundLayerMask`: bitmask `8`

---

## 5. Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs

- **Klasa / Struktura**: `CollectibleItemsSpawner.CollectibleItemSpawnData` oraz `CollectibleItemsSpawner`
- **Komponent Unity**: `CollectibleItemsSpawner` (historyczny skrypt guid: `03e03dcbbbe0fa74e94f36fe61d25633`)
- **Asset bazowy**: Assets/Scenes/RuinedBloodCity.unity (GameObject: `CollectiblesSpawner`, fileID: `36651505`, MonoBehaviour fileID: `36651507`)
- **Status skryptu**: Usunięty w CSR-20 (zastąpiony dropami z zabitych wrogów przez `EnemyDeathHandler` / `CollectibleDropNotifier`)
- **Historia atrybutu**: Wprowadzony w CSR-2, usunięty w CSR-4, cały plik usunięty w CSR-20

### Mapowanie pól

| Stara nazwa pola (`FormerlySerializedAs`) | Nowa nazwa pola w C# | Typ | Status w kodzie i assetach |
| :--- | :--- | :--- | :--- |
| `Prefab` | `_prefab` | `GameObject` | Skrypt usunięty z projektu |
| `SpawnYOffset` | `_spawnYOffset` | `float` | Skrypt usunięty z projektu |
| `SpawnChance` | `_spawnChance` | `float` | Skrypt usunięty z projektu |
| `maxSpawnedCollectiblesCount` | `_maxSpawnedCollectiblesCount` | `byte` | Skrypt usunięty z projektu |

### Ostatnie znane wartości w scenie Assets/Scenes/RuinedBloodCity.unity (przed usunięciem w CSR-20)

- `_maxSpawnedCollectiblesCount`: `6`
- `_spawnDelay`: `8`
- `_collectibleItemsParent`: `{fileID: 39965858}` (Transform GameObjectu `Collectibles` na scenie)
- `_collectibleItemsSpawnData` (tablica 1 element):
  - **Element 0**:
    - `_prefab`: Assets/Prefabs/Collectibles/SkilCrate.prefab (guid: `2fbb8fc2b6bce83498d7142c70a1a7f0`, fileID: `8743226647122829456`, type: `3`)
    - `_spawnYOffset`: `0.925`
    - `_spawnChance`: `100`

---

## Podsumowanie i Wskazówki Konfiguracyjne dla Unity

1. **Formaty YAML i referencje Unity**:
   - Pola typu `float` zapisywane są bezpośrednio jako liczby (np. `1.2`, `0.5`).
   - Wartości `enum` w YAML to indeksy liczbowe zaczynające się od `0` (`Axel.Front` = `0`, `Axel.Rear` = `1`).
   - Obiekty typu `FloatValueRange` posiadają pola wewnętrzne `<Min>k__BackingField` oraz `<Max>k__BackingField`.
   - Referencje do assetów zewnętrznych (materiały, prefaby) wymagają podania `fileID`, `guid` oraz `type: 2` (dla assetów/materiałów) lub `type: 3` (dla skryptów/prefabów).

2. **Zasada reserializacji w Unity**:
   - Otworzenie i zapisanie sceny lub prefabu w Unity Editor (lub wywołanie `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()`) utrwala nowe nazwy pól `_camelCase` w plikach YAML.
