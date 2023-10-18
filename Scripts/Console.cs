using Godot;
using System;
using System.Collections.Generic;

namespace Scripts;
 
public partial class Console : Panel
{
    public struct CommandData
    {
        public readonly Action<string[]> ActionToUse;
        public readonly string Description;
        
        /// <summary>
        /// Assign an action that takes in the command string as an argument, with an optional description (add to the main command).
        /// </summary>
        /// <param name="action">Action to take when command is run. string[] = Command.</param>
        /// <param name="description">Description to show when help is run.</param>
        public CommandData(Action<string[]> action, string description = "")
        {
            ActionToUse = action;
            Description = description;
        }
    }

    private const int HistoryLineCount = 5;
    private const int HistoryLineCountMinusOne = HistoryLineCount - 1;
    private const float TweenDuration = 0.2f;

    public static Console Inst { get; private set; }

    private static readonly StringName _toggleConsoleName = "toggle_console", _prevCommandName = "prev_command", _nextCommandName = "next_command";
    private static readonly StringName _grabFocusName = "grab_focus";
    private static readonly NodePath _positionYPath = "position:y";

    public event Action Opened;
    public event Action Closed;
    
    public bool IsOpen { get; private set; }

    private RichTextLabel _output;
    private LineEdit _input;

    private Tween _openTween;
    private Callable _setVisibleFalse;
    private bool _isActive;

    private readonly Dictionary<StringName, CommandData> _commands;
    private readonly string[] _historyLines = new string[HistoryLineCount];
    private int _historyIdx;

    public Console()
    {
        Inst = this;
        _commands = new()
        {
            {
                "help", new
                (
                    new(Help),
                    "Prints all the available commands."       
                )
            },
            {
                "clear", new
                (
                    new((_) => _output.Clear()),
                    "Clears the console output."
                )
            }
        };
    }

    public override void _Ready()
    {
        base._Ready();

        // References //
        _output = GetNode<RichTextLabel>("VBoxContainer/Output");
        _input = GetNode<LineEdit>("VBoxContainer/Input");

        _openTween = CreateTween();
        _setVisibleFalse = Callable.From(() => Visible = false);
        //

        _openTween.Kill();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Events //
        _input.TextSubmitted += OnInput_TextSubmitted;
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (@event.IsActionPressed(_toggleConsoleName))
        {
            _isActive = !_isActive;

            _openTween.Kill();
            _openTween = CreateTween();

            if (_isActive)
            {
                _openTween.TweenProperty(this, _positionYPath, 0f, TweenDuration);
                Visible = true;

                _input.CallDeferred(_grabFocusName); // Focus after (so nothing is typed)
                Input.MouseMode = Input.MouseModeEnum.Visible;

                IsOpen = true;
                Opened?.Invoke();
            }
            else
            {
                _openTween.TweenProperty(this, _positionYPath, -Size.Y, TweenDuration);
                _openTween.TweenCallback(_setVisibleFalse);

                _input.ReleaseFocus();
                Input.MouseMode = Input.MouseModeEnum.Captured;

                IsOpen = false;
                Closed?.Invoke();
            }         
        }

        if (!_isActive) { return; }

        if (@event.IsActionPressed(_prevCommandName))
        { 
            if (_historyIdx < HistoryLineCountMinusOne && _historyLines[_historyIdx + 1] != null)
            {
                _input.Text = _historyLines[++_historyIdx];
            }          
        }
        else if (@event.IsActionPressed(_nextCommandName))
        {
            if (_historyIdx > 0)
            {
                _input.Text = _historyLines[--_historyIdx];
            }
        }
    }

    public void AppendLine(string text)
    {
        _output.AppendText(text + '\n');
    }

    /// <summary>
    /// Add command to lookup, and if it already exists merge the actions.<br/>
    /// </summary>
    /// <param name="name">Name of the command that will be typed to run it.</param>
    /// <param name="data"><see cref="CommandData"/> with info and what should run.</param>
    public void AddCommand(StringName name, CommandData data)
    {
        if (_commands.TryAdd(name, data)) { return; }

        CommandData storedData = _commands[name];
        _commands[name] = new
        (
            storedData.ActionToUse + data.ActionToUse,
            (data.Description == string.Empty) ? storedData.Description : data.Description
        );
    }

    private void RunCommand(string command)
    {
        string[] commandSplit = command.Split(' ');

        if (!_commands.TryGetValue(commandSplit[0], out CommandData data))
        {
            AppendLine($"\"{commandSplit[0]}\" is not a valid command. Enter \"help\" to receive a list of commands.");
            return;
        }
        data.ActionToUse.Invoke(commandSplit); // Actions shouldn't be null in the dictionary (hence the lack of ?)
    }

    // Console Specific Commands //
    private void Help(string[] _)
    {
        AppendLine("--- Commands ---");
        foreach (KeyValuePair<StringName, CommandData> pair in _commands)
        {
            AppendLine($"{pair.Key}: {pair.Value.Description}");
        }
    }

    // Events //
    private void OnInput_TextSubmitted(string newText)
    {
        if (newText == string.Empty) { return; }

        Array.Copy(_historyLines, 0, _historyLines, 1, HistoryLineCountMinusOne); // Shift elements right 1
        _historyLines[0] = _input.Text;
        _historyIdx = -1;

        _input.Clear();

        RunCommand(newText);
    }
}
