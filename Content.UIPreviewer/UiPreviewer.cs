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

        public void UpdateScreen(string typeStr)
        {
            Screen.ClearMessages();

            var type = _reflectionManager.GetType(typeStr);

            if (type == null)
            {
                Screen.AddMessage($"Can't load type {typeStr}");
                return;
            }

            if (!type.IsAssignableTo(typeof(Control)))
            {
                Screen.AddMessage($"Type {typeStr} not assignable to Control");
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

                _taskManager.RunOnMainThread(() => UpdateScreen(name));
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
