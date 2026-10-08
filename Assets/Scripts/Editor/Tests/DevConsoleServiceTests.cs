using System.Collections.Generic;
using Assets.Scripts.UI.DevConsole;
using NUnit.Framework;

namespace Assets.Scripts.Editor.Tests
{
    [TestFixture]
    public class DevConsoleServiceTests
    {
        private DevConsoleService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new DevConsoleService(maxLogLines: 10);
        }

        [Test]
        public void ServiceIsEnabledInEditorEnvironment()
        {
            Assert.That(_service.IsEnabled, Is.True);
        }

        [Test]
        public void ServiceInitializesWithHelpAndClearCommands()
        {
            Assert.That(_service.RegisteredCommands.Count, Is.EqualTo(2));
            Assert.That(_service.RegisteredCommands[0].Name, Is.EqualTo("help"));
            Assert.That(_service.RegisteredCommands[1].Name, Is.EqualTo("clear"));
        }

        [Test]
        public void RegisterCommand_AddsCommandToRegistry()
        {
            bool executed = false;
            _service.RegisterCommand("test", "test <arg>", "Test command", args =>
            {
                executed = true;
            });

            Assert.That(_service.RegisteredCommands.Count, Is.EqualTo(3));
            bool result = _service.ExecuteCommand("test");
            Assert.That(result, Is.True);
            Assert.That(executed, Is.True);
        }

        [Test]
        public void ExecuteCommand_ParsesArgumentsCorrectly()
        {
            string[] receivedArgs = null;
            _service.RegisterCommand("greet", "greet <name> <age>", "Greeting command", args =>
            {
                receivedArgs = args;
            });

            _service.ExecuteCommand("greet Alice 42");

            Assert.That(receivedArgs, Is.Not.Null);
            Assert.That(receivedArgs.Length, Is.EqualTo(2));
            Assert.That(receivedArgs[0], Is.EqualTo("Alice"));
            Assert.That(receivedArgs[1], Is.EqualTo("42"));
        }

        [Test]
        public void ExecuteCommand_IsCaseInsensitive()
        {
            bool executed = false;
            _service.RegisterCommand("mycommand", "mycommand", "Description", args =>
            {
                executed = true;
            });

            bool result = _service.ExecuteCommand("MYCOMMAND");
            Assert.That(result, Is.True);
            Assert.That(executed, Is.True);
        }

        [Test]
        public void UnregisterCommand_RemovesCommandFromRegistry()
        {
            _service.RegisterCommand("temporary", "temporary", "Temp", args => { });
            Assert.That(_service.RegisteredCommands.Count, Is.EqualTo(3));

            _service.UnregisterCommand("temporary");
            Assert.That(_service.RegisteredCommands.Count, Is.EqualTo(2));

            bool result = _service.ExecuteCommand("temporary");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ExecuteCommand_UnknownCommand_ReturnsFalseAndLogsError()
        {
            bool result = _service.ExecuteCommand("nonexistent");
            Assert.That(result, Is.False);
            Assert.That(_service.LogHistory.Count, Is.GreaterThan(0));
            Assert.That(_service.LogHistory[_service.LogHistory.Count - 1], Does.Contain("[ERROR]"));
        }

        [Test]
        public void ExecuteCommand_EmptyOrWhitespace_ReturnsFalse()
        {
            Assert.That(_service.ExecuteCommand(""), Is.False);
            Assert.That(_service.ExecuteCommand("   "), Is.False);
            Assert.That(_service.ExecuteCommand(null), Is.False);
        }

        [Test]
        public void Clear_RemovesAllLogsAndFiresEvent()
        {
            bool clearedEventFired = false;
            _service.OnLogsCleared += () => clearedEventFired = true;

            _service.Log("Message 1");
            _service.Log("Message 2");
            Assert.That(_service.LogHistory.Count, Is.EqualTo(2));

            _service.Clear();
            Assert.That(_service.LogHistory.Count, Is.EqualTo(0));
            Assert.That(clearedEventFired, Is.True);
        }

        [Test]
        public void LogHistory_RespectsMaxLogLinesLimit()
        {
            for (int i = 0; i < 15; i++)
            {
                _service.Log($"Log entry {i}");
            }

            Assert.That(_service.LogHistory.Count, Is.EqualTo(10));
            Assert.That(_service.LogHistory[9], Is.EqualTo("Log entry 14"));
        }

        [Test]
        public void HelpCommand_OutputsAllCommandsToLog()
        {
            _service.RegisterCommand("custom", "custom <x>", "Custom desc", args => { });

            _service.ExecuteCommand("help");

            bool foundCustom = false;
            for (int i = 0; i < _service.LogHistory.Count; i++)
            {
                if (_service.LogHistory[i].Contains("custom <x> - Custom desc"))
                {
                    foundCustom = true;
                    break;
                }
            }

            Assert.That(foundCustom, Is.True);
        }

        [Test]
        public void ExecuteCommand_MultiTokenSpawnCommand_PassesAllArgumentsInOrder()
        {
            string[] receivedArgs = null;
            _service.RegisterCommand("spawn", "spawn <entity> [count]", "Spawn entity", args =>
            {
                receivedArgs = args;
            });

            _service.ExecuteCommand("spawn zombie 2 3");

            Assert.That(receivedArgs, Is.Not.Null);
            Assert.That(receivedArgs.Length, Is.EqualTo(3));
            Assert.That(receivedArgs[0], Is.EqualTo("zombie"));
            Assert.That(receivedArgs[1], Is.EqualTo("2"));
            Assert.That(receivedArgs[2], Is.EqualTo("3"));
        }

        [Test]
        public void ExecuteCommand_ExpAutoFlagCommand_PassesFlagsCorrectly()
        {
            string[] receivedArgs = null;
            _service.RegisterCommand("exp", "exp <amount> [--auto]", "Grant EXP", args =>
            {
                receivedArgs = args;
            });

            _service.ExecuteCommand("exp 220 --auto");

            Assert.That(receivedArgs, Is.Not.Null);
            Assert.That(receivedArgs.Length, Is.EqualTo(2));
            Assert.That(receivedArgs[0], Is.EqualTo("220"));
            Assert.That(receivedArgs[1], Is.EqualTo("--auto"));
        }
    }
}
