namespace Project;

static class SelectedServer {

    public static Uri GetUri() {
        var index = AppConfig.SelectedServerIndex;
        if(CheckBounds(index)) {
            return ServerList.Uri[index];
        }
        throw new UIException("No server selected");
    }

    public static string GetDisplayName() {
        var index = AppConfig.SelectedServerIndex;
        if(CheckBounds(index)) {
            return ServerList.DisplayName[index];
        }
        return "";
    }

    static bool CheckBounds(int index) {
        return index > -1 && index < ServerList.Count;
    }
}
