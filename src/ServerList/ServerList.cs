namespace Project;

static class ServerList {
    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "servers.txt");

    public static IReadOnlyList<Uri> Uri { get; private set; } = [];
    public static IReadOnlyList<string> DisplayName { get; private set; } = [];
    public static IReadOnlyList<bool> Separator { get; private set; } = [];

    public static int Count => Uri.Count;

    public static void Load() {
        if(!File.Exists(FilePath)) {
            File.WriteAllLines(FilePath, [
                "# " + XrayOutbound.VLESS_SAMPLE
            ]);
        }

        var newUriList = new List<Uri>();
        var newDisplayNameList = new List<string>();
        var newSeparatorList = new List<bool>();

        var fileText = File.ReadAllText(FilePath);

        foreach(var rawLine in fileText.AsSpan().EnumerateLines()) {
            var line = rawLine.Trim();

            if(line.IsEmpty || line.StartsWith('#'))
                continue;

            if(line.Trim('-').IsEmpty) {
                var count = newSeparatorList.Count;
                if(count > 0) {
                    newSeparatorList[count - 1] = true;
                }
                continue;
            }

            var uri = new Uri(line.ToString());
            newUriList.Add(uri);
            newDisplayNameList.Add(FormatDisplayName(uri));
            newSeparatorList.Add(false);
        }

        Uri = newUriList;
        DisplayName = newDisplayNameList;
        Separator = newSeparatorList;
    }

    public static int FindByDisplayName(string displayName) {
        if(displayName.Length > 0) {
            var displayNameList = DisplayName;
            var count = displayNameList.Count;
            for(var i = 0; i < count; i++) {
                if(displayNameList[i] == displayName) {
                    return i;
                }
            }
        }
        return -1;
    }

    static string FormatDisplayName(Uri uri) {
        var result = uri.Fragment;

        if(result.Length > 1)
            return System.Uri.UnescapeDataString(result.AsSpan().Slice(1));

        if(uri.Host.Length > 0 && uri.Port > -1)
            return uri.Host + ':' + uri.Port;

        return uri.ToString();
    }
}
