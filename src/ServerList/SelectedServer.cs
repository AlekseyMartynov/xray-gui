namespace Project;

static class SelectedServer {

    public static Uri GetUri() {
        var index = AppConfig.SelectedServerIndex;
        if(index > -1 && index < ServerList.Count) {
            return ServerList.Uri[index];
        }
        throw new UIException("No server selected");
    }
}
