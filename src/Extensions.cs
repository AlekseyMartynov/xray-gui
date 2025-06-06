namespace Project;

static class Extensions {

    public static string Quote(this string text) {
        if(text.Contains('"')) {
            throw new NotSupportedException();
        }
        return "\"" + text + "\"";
    }
}
