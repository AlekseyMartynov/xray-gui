namespace Project;

static class ServerList {
    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "servers.txt");

    public static IReadOnlyList<Uri> Uri { get; private set; } = [];

    public static int Count => Uri.Count;

    public static void Load() {
        if(!File.Exists(FilePath)) {
            File.WriteAllLines(FilePath, [
                "# " + XrayOutbound.VLESS_SAMPLE
            ]);
        }

        var newUriList = new List<Uri>();
        var fileText = File.ReadAllText(FilePath);

        foreach(var rawLine in fileText.AsSpan().EnumerateLines()) {
            var line = rawLine.Trim();

            if(line.IsEmpty || line.StartsWith('#'))
                continue;

            var uri = new Uri(line.ToString());
            newUriList.Add(uri);
        }

        Uri = newUriList;
    }
}
