using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Input
{
    public enum InputActionId
    {
        None = 0,
        PlayerMove = 10,
        PlayerBrake = 11,
        PlayerInteract = 12,
        PlayerReverse = 13,
        GlobalPause = 20,
        GlobalToggleConsole = 21,
        UiNavigate = 30,
        UiSubmit = 31,
        UiCancel = 32,
        UiPoint = 33,
        UiClick = 34,
        UiScrollWheel = 35,
        UpgradeSelectChoice1 = 40,
        UpgradeSelectChoice2 = 41,
        UpgradeSelectChoice3 = 42,
        DevConsoleClose = 50,
        DevConsoleSubmit = 51,
        DevConsoleHistoryPrevious = 52,
        DevConsoleHistoryNext = 53
    }

    public enum InputCommandKind
    {
        None = 0,
        Pause = 1,
        ToggleConsole = 2,
        UiCancel = 3,
        UpgradeSelectChoice = 4,
        DevConsoleClose = 5,
        DevConsoleSubmit = 6,
        DevConsoleHistoryPrevious = 7,
        DevConsoleHistoryNext = 8
    }

    public enum InputScheme
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    public enum InputDeviceFamily
    {
        KeyboardMouse = 0,
        Xbox = 1,
        PlayStation = 2,
        Switch = 3,
        GenericGamepad = 4,
        Unavailable = 5
    }

    public enum KeyboardGlyphTheme
    {
        Light = 0,
        Dark = 1
    }

    public enum InputPresentationOverride
    {
        Auto = 0,
        Xbox = 1,
        PlayStation = 2,
        Switch = 3,
        GenericGamepad = 4
    }

    public enum InputContextScopeKind
    {
        None = 0,
        Menu = 1,
        LiveReward = 2,
        Pause = 3,
        PauseChildPanel = 4,
        Dying = 5,
        Death = 6,
        DevConsole = 7,
        Loading = 8
    }

    public readonly struct InputScopeToken : IEquatable<InputScopeToken>
    {
        public uint Id { get; }
        public uint SceneEpoch { get; }
        public InputContextScopeKind Kind { get; }
        public int OwnerId { get; }

        public bool IsValid
        {
            get { return Id != 0; }
        }

        public InputScopeToken(uint id, uint sceneEpoch, InputContextScopeKind kind, int ownerId)
        {
            Id = id;
            SceneEpoch = sceneEpoch;
            Kind = kind;
            OwnerId = ownerId;
        }

        public bool Equals(InputScopeToken other)
        {
            return Id == other.Id
                && SceneEpoch == other.SceneEpoch
                && Kind == other.Kind
                && OwnerId == other.OwnerId;
        }

        public override bool Equals(object obj)
        {
            return obj is InputScopeToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, SceneEpoch, (int)Kind, OwnerId);
        }
    }

    public readonly struct InputCommand
    {
        public InputCommandKind Kind { get; }
        public InputActionId ActionId { get; }
        public int SlotIndex { get; }
        public int SourceDeviceId { get; }
        public string SourceControlPath { get; }
        public uint InputUpdateId { get; }
        public int FrameCount { get; }
        public uint SceneEpoch { get; }
        public uint ContextGeneration { get; }

        public InputCommand(
            InputCommandKind kind,
            InputActionId actionId,
            int slotIndex,
            int sourceDeviceId,
            string sourceControlPath,
            uint inputUpdateId,
            int frameCount,
            uint sceneEpoch,
            uint contextGeneration)
        {
            Kind = kind;
            ActionId = actionId;
            SlotIndex = slotIndex;
            SourceDeviceId = sourceDeviceId;
            SourceControlPath = sourceControlPath;
            InputUpdateId = inputUpdateId;
            FrameCount = frameCount;
            SceneEpoch = sceneEpoch;
            ContextGeneration = contextGeneration;
        }
    }

    public readonly struct InputPromptRequest : IEquatable<InputPromptRequest>
    {
        public InputActionId ActionId { get; }
        public string CompositePartName { get; }
        public bool IncludeAlternatives { get; }

        public InputPromptRequest(
            InputActionId actionId,
            string compositePartName = null,
            bool includeAlternatives = false)
        {
            ActionId = actionId;
            CompositePartName = compositePartName ?? string.Empty;
            IncludeAlternatives = includeAlternatives;
        }

        public bool Equals(InputPromptRequest other)
        {
            return ActionId == other.ActionId
                && string.Equals(CompositePartName, other.CompositePartName, StringComparison.OrdinalIgnoreCase)
                && IncludeAlternatives == other.IncludeAlternatives;
        }

        public override bool Equals(object obj)
        {
            return obj is InputPromptRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            int partHash = StringComparer.OrdinalIgnoreCase.GetHashCode(CompositePartName ?? string.Empty);
            return HashCode.Combine((int)ActionId, partHash, IncludeAlternatives);
        }
    }

    public readonly struct InputPromptPartDescriptor
    {
        public string PartName { get; }
        public Sprite Sprite { get; }
        public string Label { get; }
        public string ControlPath { get; }

        public InputPromptPartDescriptor(string partName, Sprite sprite, string label, string controlPath)
        {
            PartName = partName;
            Sprite = sprite;
            Label = label;
            ControlPath = controlPath;
        }
    }

    public sealed class InputPromptDescriptor
    {
        private static readonly InputPromptPartDescriptor[] EMPTY_PARTS = Array.Empty<InputPromptPartDescriptor>();

        public bool IsValid { get; }
        public bool IsBound { get; }
        public bool IsAvailable { get; }
        public InputDeviceFamily Family { get; }
        public Sprite PrimarySprite { get; }
        public string PrimaryLabel { get; }
        public string ControlPath { get; }
        public IReadOnlyList<InputPromptPartDescriptor> Parts { get; }
        public IReadOnlyList<InputPromptPartDescriptor> Alternatives { get; }

        public InputPromptDescriptor(
            bool isValid,
            bool isBound,
            bool isAvailable,
            InputDeviceFamily family,
            Sprite primarySprite,
            string primaryLabel,
            string controlPath,
            IReadOnlyList<InputPromptPartDescriptor> parts = null,
            IReadOnlyList<InputPromptPartDescriptor> alternatives = null)
        {
            IsValid = isValid;
            IsBound = isBound;
            IsAvailable = isAvailable;
            Family = family;
            PrimarySprite = primarySprite;
            PrimaryLabel = primaryLabel ?? string.Empty;
            ControlPath = controlPath ?? string.Empty;
            Parts = parts ?? EMPTY_PARTS;
            Alternatives = alternatives ?? EMPTY_PARTS;
        }

        public static InputPromptDescriptor CreateUnavailable(InputDeviceFamily family, string label)
        {
            return new InputPromptDescriptor(
                isValid: true,
                isBound: false,
                isAvailable: false,
                family: family,
                primarySprite: null,
                primaryLabel: label,
                controlPath: string.Empty);
        }
    }
}
