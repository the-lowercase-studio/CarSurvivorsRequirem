# Car Survivors Test Harness Quick-Reference

Concise, copy-ready EditMode test harness patterns and override checklist for agents (Gemini/Antigravity, Codex, Claude) executing .agents/skills/unity-write-tests/SKILL.md.

## 1. Precedence Override Summary

| Generic Skill Rule | Car Survivors Override |
| --- | --- |
| Create `.asmdef` for test assemblies (`unity-testing`) | Use the established repository assemblies: EditMode tests go in Assets/Scripts/Editor/Tests/ (`CarSurvivors.Editor.Tests.asmdef`), and PlayMode tests go in Assets/Tests/PlayMode/ (`CarSurvivors.PlayMode.Tests.asmdef`). Do not introduce extra `.asmdef` files without approval. |
| Use UTF 2.0 attributes `[PreservedValues]`, `[RequirePlatformSupport]` (`unity-testing`) | Do NOT use UTF 2.0-only attributes; repository baseline is `com.unity.test-framework` `1.6.0` (NUnit 3.5). |
| Edit `.csproj` `<Compile Include>` or run `dotnet test` (`code-testing`) | Do NOT edit `.csproj` or run `dotnet test`; compile with `dotnet build` on generated `.csproj` files and execute via `unity command test run --json`. |
| Create `<TESTAGENT_DIR>/research.md`, `plan.md`, `status.md` (`code-testing`) | Do NOT use `<TESTAGENT_DIR>` or `.git/testagent`; use .agents/context/implementations/plans/ and .agents/context/implementations/summaries/ (and .agents/context/tmp/ for scratch files). |
| Invoke external `test-engineer` or extension catalogs (`code-testing`, `test-anti-patterns`) | Execute all 5 phases inline; ignore external custom agent and extension catalog names. |
| Use `Moq`, `NSubstitute`, or `FluentAssertions` | Use private hand-written interface fakes, `ScriptableObject.CreateInstance<T>()`, and NUnit `Assert.*`. |
| Use expression-bodied methods `=>` or `System.Linq` (`unity-testing`) | Strictly forbidden by .agents/scripts/check-architecture.ps1 (`ARCH005`, `ARCH006`); always use block bodies `{}` and explicit loops. |
| Use `snake_case` test method names (`writing-unit-tests`) | Use PascalCase `SubjectOrMethod_Scenario_ExpectedOutcome`. |

## 2. Standard EditMode Test Fixture Template (Reflex DI + Lifecycle + DOTween Safe)

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class ExampleSystemTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

            for (int i = 0; i < _createdContainers.Count; i++)
            {
                _createdContainers[i]?.Dispose();
            }

            _createdContainers.Clear();

            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        private GameObject TrackGameObject(string name, bool active = true)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            _createdObjects.Add(go);
            return go;
        }

        private T TrackScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("TestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
```
