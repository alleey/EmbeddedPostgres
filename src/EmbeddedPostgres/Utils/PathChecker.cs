namespace EmbeddedPostgres.Utils;

public static class PathChecker
{
    public static bool IsWebUrl(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out Uri uriResult))
        {
            return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
        }
        return false;
    }

    public static bool IsLocalPath(string pathOrUrl)
    {
        return Path.IsPathRooted(pathOrUrl) && !IsWebUrl(pathOrUrl);
    }
}
