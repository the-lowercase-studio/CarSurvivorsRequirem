using System;
using System.Collections.Generic;
using Assets.Scripts.UI.DevConsole.Constants;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.UI.DevConsole
{
    public interface IDevConsoleService
    {
        event Action<string> OnLogAppended;
        event Action OnLogsCleared;

        bool IsEnabled { get; }
        IReadOnlyList<DevCommandInfo> RegisteredCommands { get; }
        IReadOnlyList<string> LogHistory { get; }

        void RegisterCommand(string name, string syntax, string description, Action<string[]> handler);
        void UnregisterCommand(string name);
        bool ExecuteCommand(string rawInputLine);
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void Clear();
    }

    public class DevConsoleService : IDevConsoleService
    {
        private readonly struct CommandEntry
        {
            public DevCommandInfo Info { get; }
            public Action<string[]> Handler { get; }

            public CommandEntry(DevCommandInfo info, Action<string[]> handler)
            {
                Info = info;
                Handler = handler;
            }
        }

        private static bool IsConsoleSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private readonly Dictionary<string, CommandEntry> _commandsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<DevCommandInfo> _registeredCommandsList = new();
        private readonly List<string> _logHistory = new();
        private readonly int _maxLogLines;

        public event Action<string> OnLogAppended;
        public event Action OnLogsCleared;

        public bool IsEnabled
        {
            get
            {
                return IsConsoleSupported;
            }
        }

        public IReadOnlyList<DevCommandInfo> RegisteredCommands
        {
            get
            {
                return _registeredCommandsList;
            }
        }

        public IReadOnlyList<string> LogHistory
        {
            get
            {
                return _logHistory;
            }
        }

        [ReflexConstructor]
        public DevConsoleService() : this(DevConsoleConstants.DEFAULT_MAX_LOG_LINES)
        {
        }

        public DevConsoleService(int maxLogLines)
        {
            _maxLogLines = maxLogLines > 0 ? maxLogLines : DevConsoleConstants.DEFAULT_MAX_LOG_LINES;
            if (IsConsoleSupported)
            {
                RegisterCommand("help", "help", "Displays all registered commands.", HandleHelpCommand);
                RegisterCommand("clear", "clear", "Clears the console log window.", HandleClearCommand);
            }
        }

        public void RegisterCommand(string name, string syntax, string description, Action<string[]> handler)
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command name cannot be null or whitespace.", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            string normalizedName = name.Trim();
            DevCommandInfo info = new DevCommandInfo(normalizedName, syntax, description);
            CommandEntry entry = new CommandEntry(info, handler);

            if (_commandsByName.ContainsKey(normalizedName))
            {
                _commandsByName[normalizedName] = entry;
                for (int i = 0; i < _registeredCommandsList.Count; i++)
                {
                    if (string.Equals(_registeredCommandsList[i].Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        _registeredCommandsList[i] = info;
                        break;
                    }
                }
            }
            else
            {
                _commandsByName.Add(normalizedName, entry);
                _registeredCommandsList.Add(info);
            }
        }

        public void UnregisterCommand(string name)
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string normalizedName = name.Trim();
            if (_commandsByName.Remove(normalizedName))
            {
                for (int i = 0; i < _registeredCommandsList.Count; i++)
                {
                    if (string.Equals(_registeredCommandsList[i].Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        _registeredCommandsList.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public bool ExecuteCommand(string rawInputLine)
        {
            if (!IsConsoleSupported)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(rawInputLine))
            {
                return false;
            }

            string trimmed = rawInputLine.Trim();
            Log(DevConsoleConstants.PROMPT_PREFIX + trimmed);

            string[] tokens = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            string commandName = tokens[0];
            string[] args = new string[tokens.Length - 1];
            for (int i = 1; i < tokens.Length; i++)
            {
                args[i - 1] = tokens[i];
            }

            if (!_commandsByName.TryGetValue(commandName, out CommandEntry entry))
            {
                LogError($"Unknown command: '{commandName}'. Type 'help' for a list of available commands.");
                return false;
            }

            try
            {
                entry.Handler.Invoke(args);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error executing '{commandName}': {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        public void Log(string message)
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            AppendLog(message);
        }

        public void LogWarning(string message)
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            AppendLog($"<color={DevConsoleConstants.COLOR_WARNING_HEX}>[WARN] {message}</color>");
        }

        public void LogError(string message)
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            AppendLog($"<color={DevConsoleConstants.COLOR_ERROR_HEX}>[ERROR] {message}</color>");
        }

        public void Clear()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            _logHistory.Clear();
            OnLogsCleared?.Invoke();
        }

        private void AppendLog(string formattedMessage)
        {
            _logHistory.Add(formattedMessage);
            while (_logHistory.Count > _maxLogLines)
            {
                _logHistory.RemoveAt(0);
            }

            OnLogAppended?.Invoke(formattedMessage);
        }

        private void HandleHelpCommand(string[] args)
        {
            Log("Available Commands:");
            for (int i = 0; i < _registeredCommandsList.Count; i++)
            {
                DevCommandInfo commandInfo = _registeredCommandsList[i];
                Log($"  {commandInfo.Syntax} - {commandInfo.Description}");
            }
        }

        private void HandleClearCommand(string[] args)
        {
            Clear();
        }
    }
}
