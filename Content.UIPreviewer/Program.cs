using Robust.Client;


namespace Content.UIPreviewer;

public sealed class Program
{
    public static TypeRepresentation TypeName;

    static void Main(string[] args)
    {
        TypeName = args.Length == 0 ? TypeRepresentation.None : new(args[0]);

        ContentStart.StartLibrary([], new()
        {
            Sandboxing = false,

            ContentModulePrefix = "Content.",
            ContentBuildDirectory = "Content.UIPreview",
            LoadConfigAndUserData = true,
            LoadContentResources = true,
            ConfigFileName = "ui_previewer.toml",
        });
    }
}
