using System;
using System.IO;
using System.Linq;
using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Shared.Asynchronous;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;

namespace Content.UIPreviewer
{
    public sealed class UiPreviewer : GameClient
    {
        [Dependency] private readonly IUserInterfaceManager _interfaceManager = default!;
        [Dependency] private readonly IBaseClient _client = default!;
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;
        [Dependency] private readonly IResourceManager _resources = null!;
        [Dependency] private readonly ITaskManager _taskManager = default!;

        public static ControlScreen Screen;

        private const string MarkerFileName = "SpaceStation14.sln";
        private FileSystemWatcher? _watcher;

        private bool _invokeScreenInit = true;

        public override void PreInit()
        {
            Logger.GetSawmill("UiPreviewer").Info("Starting UIPreviewer");
        }

        public override void PostInit()
        {
            IoCManager.InjectDependencies(this);

            var codeLocation = InferCodeLocation();
            _watcher = CreateWatcher(codeLocation);
        }

        public override void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
        {
            if (!_invokeScreenInit)
                return;

            _client.StartSinglePlayer();
            _interfaceManager.StateRoot.Children.Clear();
            _interfaceManager.StateRoot.Children.Add(Screen = new ControlScreen());
            UpdateScreen(Program.TypeName);
            _invokeScreenInit = false;
        }

        public void UpdateScreen(TypeRepresentation typeRepresentation)
        {
            Screen.ClearMessages();

            if (typeRepresentation == TypeRepresentation.None)
            {
                Screen.AddMessage("Hello! Start changing .xaml or .cs files");
                return;
            }

            var type = _reflectionManager.GetType(typeRepresentation);

            if (type == null)
            {
                Screen.AddMessage($"Can't load type {typeRepresentation}");
                return;
            }

            if (!type.IsAssignableTo(typeof(Control)))
            {
                Screen.AddMessage($"Type {typeRepresentation} not assignable to Control");
                return;
            }

            try
            {
                Screen.SetControl((Control)ConstrHelper.CreateDefaultValue(type, IoCManager.Instance.TryResolveType));
            }
            catch (Exception e)
            {
                Screen.AddMessage($"Exception occurred while loading type {Program.TypeName}: {e.Message}");
            }
        }

        private FileSystemWatcher CreateWatcher(string location)
        {
            var watcher = new FileSystemWatcher(location)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };

            void OnWatcherEvent(object sender, FileSystemEventArgs args)
            {
                switch (args.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        return;

                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed:
                    case WatcherChangeTypes.All:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(args));
                }

                if (args.Name is null)
                    return;

                var ext = Path.GetExtension(args.Name);
                if (ext != ".xaml" && ext != ".cs")
                    return;

                var name = args.Name
                    .Replace('\\', '.')
                    .Replace(".xaml", "")
                    .Replace(".cs", "");

                _taskManager.RunOnMainThread(() => UpdateScreen((TypeRepresentation)name));
            }

            watcher.Changed += OnWatcherEvent;
            watcher.Renamed += OnWatcherEvent;
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private string? InferCodeLocation()
        {
            foreach (var contentRoot in _resources.GetContentRoots())
            {
                var systemPath = contentRoot.ToRelativeSystemPath();

                while (true)
                {
                    string[] files = Array.Empty<string>();

                    try
                    {
                        files = Directory.GetFiles(systemPath);
                    }
                    catch (IOException)
                    {
                        // Allowed to fail, continue moving up
                    }

                    if (files.Any(f => Path.GetFileName(f)
                        .Equals(MarkerFileName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return systemPath;
                    }

                    DirectoryInfo? newPath = null;

                    try
                    {
                        newPath = Directory.GetParent(systemPath);
                    }
                    catch (IOException)
                    {
                        // Allowed to fail
                    }

                    if (newPath == null)
                        break;

                    systemPath = newPath.FullName;
                }
            }

            return null;
        }
    }
}

public struct TypeRepresentation : IEquatable<TypeRepresentation>
{
    public string TypeName = "none";
    public static TypeRepresentation None = new TypeRepresentation();

    public TypeRepresentation() { }

    public TypeRepresentation(string typeName)
    {
        TypeName = typeName;
    }

    public static explicit operator TypeRepresentation(string typeName) => new(typeName);
    public static implicit operator string(TypeRepresentation type) => type.TypeName;

    public bool Equals(TypeRepresentation other) => TypeName == other.TypeName;

    public override bool Equals(object obj) => obj is TypeRepresentation other && Equals(other);

    public override int GetHashCode() => (TypeName != null ? TypeName.GetHashCode() : 0);
}
