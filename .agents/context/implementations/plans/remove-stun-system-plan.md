# Plan: Całkowite usunięcie systemu stunowania (Stun System Removal)

**Data:** 2026-09-01  
**Status:** Oczekuje na zatwierdzenie  

## 1. Cel i zakres zmian

Celem jest całkowite wycofanie mechaniki stunowania przeciwników ze wszystkich systemów w grze:
1. Usunięcie interfejsów i komponentów stanu stuna (`IStunnable`, `IStunController`, `StunController`).
2. Usunięcie wywołań stunowania ze skilli gracza (`SawBlade`, `Landmine`, `EntityManipulationHelper`).
3. Oczyszczenie wrogów (`Enemy`, `EnemyMovementController`) z pól, właściwości i logiki sprawdzania stuna.
4. Aktualizacja dokumentacji systemowej (`status-effects-system.md`, `enemies-system.md`).
5. Oczyszczenie / weryfikacja prefabów wrogów (`BarrelEnemy`, `CrawlingZombie`, `StandingZombie`).

---

## 2. Pytania otwarte / Decyzje do potwierdzenia

> [!IMPORTANT]
> **Obsługa prefabów w edytorze Unity vs edycja YAML**:  
> Komponent `StunController` jest przypisany na 3 prefabach wrogów (`BarrelEnemy.prefab`, `CrawlingZombie.prefab`, `StandingZombie.prefab`).  
> Zgodnie z zasadami projektu, prefaby mogą zostać oczyszczone z komponentu w edytorze Unity po usunięciu skryptu (przycisk "Remove Component" / automatyczne oczyszczenie brakującego skryptu) LUB możemy bezpiecznie usunąć powiązane sekcje YAML z plików `.prefab` na Twoje wyraźne życzenie.

---

## 3. Szczegółowy plan zmian w plikach

### Moduł Status Effects & Helpers
- **[DELETE]** `Assets/Scripts/StatusEffects/IStunnable.cs`
- **[DELETE]** `Assets/Scripts/StatusEffects/StunController.cs`
- **[MODIFY]** `Assets/Scripts/StatusEffects/EntityManipulationHelper.cs`
  - Usunięcie metody `public static void Stun(Collider target, float duration)`.

### Moduł Przeciwników (Enemies)
- **[MODIFY]** `Assets/Scripts/Enemies/Base/Enemy.cs`
  - Usunięcie implementacji interfejsu `IStunnable`.
  - Usunięcie właściwości `public IStunController StunController { get; private set; }`.
  - Usunięcie pobierania komponentu `StunController = GetComponent<IStunController>();` w `Awake()`.
  - Usunięcie metody `public void ApplyStun(float duration)`.
- **[MODIFY]** `Assets/Scripts/Enemies/Base/EnemyMovementController.cs`
  - Usunięcie martwego pola `private bool _isStunnable = false;`.
  - Usunięcie zmiennej `bool isStunned = ...`.
  - Uproszczenie warunku ruchu `canMoveOnGrid`:
    ```csharp
    bool canMoveOnGrid = !_enemy.EnemyAnimator.IsPlayingAttackAnimation
        && _currentMovementDelayAfterAttack <= 0;
    ```

### Moduł Umiejętności Gracza (Player Skills)
- **[MODIFY]** `Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs`
  - Usunięcie bloku nakładającego stun (`other.TryGetComponent(out IStunnable stunnable)...`).
- **[MODIFY]** `Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs`
  - Usunięcie wywołania `EntityManipulationHelper.Stun(collider, timeToArriveAtLocation);`.

### Dokumentacja techniczna
- **[MODIFY]** `.agents/context/game-systems/status-effects-system.md`
  - Usunięcie sekcji dotyczących stuna; status effects ograniczone do Damage i Knockback.
- **[MODIFY]** `.agents/context/game-systems/enemies-system.md`
  - Usunięcie odniesień do `IStunnable` i `StunController`.

---

## 4. Plan weryfikacji (Verification Plan)

### Kompilacja automatyczna
- Wywołanie weryfikacji kompilacji rozwiązania C#:
  ```powershell
  dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  ```
  Oczekiwany rezultat: 0 błędów, 0 ostrzeżeń.

### Weryfikacja w Unity Editor
1. Uruchomienie sceny `RuinedBloodCity`.
2. Sprawdzenie zachowania `SawBlade` (piła) i `Landmine` (mina) — zadają obrażenia i knockback, bez odwołań do stuna.
3. Sprawdzenie braku błędów `MissingReferenceException` lub `NullReferenceException` w konsoli Unity.
